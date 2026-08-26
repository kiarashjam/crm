// Client-side bookkeeping for lead status auto-sync.
//
// Deliberately NOT stored inside `lead.pipelineState`: SalesTrackerCard reseeds
// its form whenever `lead.pipelineState` changes, so writing sync metadata into
// that blob would discard a user's unsaved sidebar edits every time a status
// moved. It also means a status-only operation never has to PUT a pipeline blob
// that may be minutes stale.
//
// Nothing here is load-bearing for correctness — if it is wiped, auto-sync
// simply behaves as though every lead were untouched.

import type { CanonicalStage } from './leadStatusSync';

const PREFS_KEY = 'crm.leadStatusSync.prefs.v1';
const META_KEY = 'crm.leadStatusSync.meta.v1';
const CHANGED_EVENT = 'crm.leadStatusSync.changed';

export interface StatusSyncPrefs {
  /** Master switch for the current user. */
  enabled: boolean;
  /** Org vocabulary pins: canonical stage → exact status name. */
  overrides: Partial<Record<CanonicalStage, string>>;
}

const DEFAULT_PREFS: StatusSyncPrefs = { enabled: true, overrides: {} };

export interface LeadSyncMeta {
  /** The status auto-sync last wrote. Divergence ⇒ someone changed it by hand,
   *  possibly on another device where we have no local record. */
  lastAutoStatus?: string;
  /** Whichever rule produced it, for the "why is this Qualified?" tooltip. */
  lastRule?: string;
  /** Human cause, e.g. "Still interested after the meeting". */
  lastBecause?: string;
  /** ISO timestamp of the last automatic write. */
  lastAt?: string;
  /**
   * Status the lead had before the current burst of edits. The inline popover
   * commits once per field, so a per-write undo target would return the user to
   * intermediate value #4 rather than where they started.
   */
  undoBase?: string;
  /** When the burst started, so a stale base expires rather than lingering. */
  undoBaseAt?: string;
  /** Suggestions the user explicitly dismissed, keyed by `rule:statusName`. */
  dismissed?: string[];
}

/**
 * How long consecutive auto-status writes count as ONE burst for Undo.
 *
 * The inline popover commits per field, so a user filling six selects produces
 * six writes in a few seconds; Undo should return them to where they started,
 * not to intermediate value #5. But two *deliberate* edits a minute apart are
 * separate acts, and Undo on the second must not silently revert the first as
 * well. Seconds, therefore — not minutes.
 */
const BURST_WINDOW_MS = 20_000;

function notify(): void {
  try {
    window.dispatchEvent(new Event(CHANGED_EVENT));
  } catch {
    // non-browser
  }
}

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return fallback;
    const parsed = JSON.parse(raw) as T;
    return parsed && typeof parsed === 'object' ? parsed : fallback;
  } catch {
    return fallback;
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
    notify();
  } catch {
    // quota / private mode — auto-sync degrades to "no memory", which is safe
  }
}

// ── Prefs ────────────────────────────────────────────────────────────────────

export function loadStatusSyncPrefs(): StatusSyncPrefs {
  const stored = readJson<Partial<StatusSyncPrefs>>(PREFS_KEY, {});
  return {
    // Absent means "never configured", which should read as on.
    enabled: stored.enabled !== false,
    overrides: stored.overrides && typeof stored.overrides === 'object'
      ? stored.overrides
      : DEFAULT_PREFS.overrides,
  };
}

export function setStatusSyncEnabled(enabled: boolean): void {
  writeJson(PREFS_KEY, { ...loadStatusSyncPrefs(), enabled });
}

export function setStageOverride(stage: CanonicalStage, statusName: string | null): void {
  const prefs = loadStatusSyncPrefs();
  const overrides = { ...prefs.overrides };
  if (statusName) overrides[stage] = statusName;
  else delete overrides[stage];
  writeJson(PREFS_KEY, { ...prefs, overrides });
}

// ── Per-lead metadata ────────────────────────────────────────────────────────

type MetaMap = Record<string, LeadSyncMeta>;

export function loadAllSyncMeta(): MetaMap {
  return readJson<MetaMap>(META_KEY, {});
}

export function getLeadSyncMeta(leadId: string): LeadSyncMeta {
  return loadAllSyncMeta()[leadId] ?? {};
}

function putLeadSyncMeta(leadId: string, patch: Partial<LeadSyncMeta>): void {
  const all = loadAllSyncMeta();
  const next: LeadSyncMeta = { ...(all[leadId] ?? {}), ...patch };
  writeJson(META_KEY, { ...all, [leadId]: next });
}

/**
 * Record a completed automatic write. Establishes an undo base on the first
 * write of a burst and reuses it for rapid follow-ups, so Undo always returns
 * the user to the status they started from.
 */
export function recordAutoStatus(
  leadId: string,
  args: { from: string; to: string; rule: string; because: string; now?: number },
): void {
  const meta = getLeadSyncMeta(leadId);
  const now = args.now ?? Date.now();
  const baseAt = meta.undoBaseAt ? Date.parse(meta.undoBaseAt) : NaN;
  const burstLive = Number.isFinite(baseAt) && now - baseAt < BURST_WINDOW_MS;
  putLeadSyncMeta(leadId, {
    lastAutoStatus: args.to,
    lastRule: args.rule,
    lastBecause: args.because,
    lastAt: new Date(now).toISOString(),
    // The hold released and we wrote over it, so the human's pick no longer
    // describes the live status.
    undoBase: burstLive ? meta.undoBase : args.from,
    undoBaseAt: burstLive ? meta.undoBaseAt : new Date(now).toISOString(),
  });
}


export function dismissSuggestion(leadId: string, key: string): void {
  const meta = getLeadSyncMeta(leadId);
  const dismissed = new Set(meta.dismissed ?? []);
  dismissed.add(key);
  putLeadSyncMeta(leadId, { dismissed: [...dismissed] });
}

export function isSuggestionDismissed(leadId: string, key: string): boolean {
  return (getLeadSyncMeta(leadId).dismissed ?? []).includes(key);
}

export function onStatusSyncChange(handler: () => void): () => void {
  const local = () => handler();
  const cross = (e: StorageEvent) => {
    if (e.key === PREFS_KEY || e.key === META_KEY) handler();
  };
  window.addEventListener(CHANGED_EVENT, local);
  window.addEventListener('storage', cross);
  return () => {
    window.removeEventListener(CHANGED_EVENT, local);
    window.removeEventListener('storage', cross);
  };
}

/** Human sentence for the status badge tooltip. */
export function describeStatusOrigin(meta: LeadSyncMeta, currentStatus: string): string | null {
  if (!meta.lastAutoStatus || !meta.lastAt) return null;
  const when = new Date(meta.lastAt);
  const stamp = Number.isNaN(when.getTime())
    ? ''
    : ` · ${when.toLocaleDateString(undefined, { day: 'numeric', month: 'short' })}`;
  if (meta.lastAutoStatus !== currentStatus) {
    // No longer "somebody changed it" — nobody can. It means an import, a seed,
    // or a colleague's tracker edit this browser has no record of.
    return `Out of step with the tracker, which last set it to "${meta.lastAutoStatus}"${stamp}.`;
  }
  return `Set automatically from the lead pipeline — ${meta.lastBecause ?? 'pipeline progress'}${stamp}.`;
}

// The records the sales agent reasons over: loaded once, shared, kept fresh.
//
// The panel sits in the header of every page, and every page renders its own
// header — so the panel remounted on every navigation and re-read eight full
// lists each time (leads, deals, tasks, enrollments, sequences, contacts,
// companies, automations). Opening Settings cost eight list loads. And because it
// only ever loaded on mount, it never noticed an edit made on the page beneath
// it: log "no answer" on a lead and the panel above kept saying "make the first
// call", telling you to do what you had just done.
//
// So the lists live here, outside any component:
//
//   · loaded once per workspace, and reused across navigations;
//   · re-read quietly behind what is already shown when anything is saved
//     (see api/dataChanged), when the tab comes back into view, or when the copy
//     is older than FRESH_FOR_MS — never with a skeleton once there is data;
//   · discarded the moment the workspace changes, so one organisation's work can
//     never be shown under another's.

import { useEffect, useSyncExternalStore } from 'react';
import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { onDataChanged } from '@/app/api/dataChanged';
import { getAuthToken } from '@/app/lib/auth';
import type { SuggestableLead } from './nextAction';
import type {
  SuggestableAutomation,
  SuggestableCompany,
  SuggestableContact,
  SuggestableDeal,
  SuggestableEnrollment,
  SuggestableSequence,
  SuggestableTask,
} from './workspace';

export interface AgentLists {
  leads: SuggestableLead[];
  deals: SuggestableDeal[];
  tasks: SuggestableTask[];
  enrollments: SuggestableEnrollment[];
  sequences: SuggestableSequence[];
  contacts: SuggestableContact[];
  companies: SuggestableCompany[];
  automations: SuggestableAutomation[];
}

export interface AgentSnapshot {
  /** Which workspace these lists belong to. */
  key: string | null;
  /** The lists, or null before the first successful load for this workspace. */
  lists: AgentLists | null;
  /** First load in flight — there is nothing to show yet. */
  loading: boolean;
  /** A reload is running behind lists that are already shown. */
  refreshing: boolean;
  /** The last load failed outright. Older lists, if any, are still shown. */
  failed: boolean;
}

/** How long a loaded copy is trusted before a navigation re-reads it. */
export const FRESH_FOR_MS = 60_000;
/** A save usually arrives as a burst (the record, then its activity log). */
export const SETTLE_MS = 400;

const LIST_NAMES = ['leads', 'deals', 'tasks', 'enrollments', 'sequences', 'contacts', 'companies', 'automations'] as const;

function fetchAll(): Promise<PromiseSettledResult<unknown>[]> {
  return Promise.allSettled([
    getLeads(), getDeals(), getTasks(), getEnrollments(), getSequences(),
    getContacts(), getCompanies(), getAutomations(),
  ]);
}

function rows<T>(value: unknown): T[] {
  return Array.isArray(value) ? value.filter((item): item is T => !!item && typeof item === 'object') : [];
}

// ── store state ──────────────────────────────────────────────────────────────

let key: string | null = null;
let lists: AgentLists | null = null;
let loadedAt = 0;
/** The read in flight, and which workspace it is for. */
let inflight: { key: string; promise: Promise<void> } | null = null;
let reloadAfter = false;
let failed = false;
let settleTimer: ReturnType<typeof setTimeout> | null = null;
let installed: (() => void) | null = null;

const EMPTY: AgentSnapshot = { key: null, lists: null, loading: true, refreshing: false, failed: false };
let snapshot: AgentSnapshot = EMPTY;
const listeners = new Set<() => void>();

function emit(): void {
  snapshot = {
    key,
    lists,
    loading: lists === null && inflight !== null,
    refreshing: lists !== null && inflight !== null,
    failed,
  };
  for (const listener of listeners) listener();
}

function load(forKey: string): Promise<void> {
  if (inflight && inflight.key === forKey) {
    // A save landed mid-read: what is being read may already be stale.
    reloadAfter = true;
    return inflight.promise;
  }
  // A read for ANOTHER workspace may still be running. It is not reused: its
  // results will be dropped on arrival, and merging into it would mean this
  // workspace never loads at all.
  const mine = { key: forKey } as { key: string; promise: Promise<void> };
  mine.promise = (async () => {
    const results = await fetchAll();
    // The workspace changed while this was in flight. These lists belong to
    // the old one and must not be shown under the new.
    if (key !== forKey) return;
    if (results.every((result) => result.status === 'rejected')) {
      failed = true;
      return;
    }
    const previous = lists;
    const next = {} as Record<(typeof LIST_NAMES)[number], unknown[]>;
    LIST_NAMES.forEach((name, index) => {
      const result = results[index]!;
      // One list failing must not erase it: an outage on deals would otherwise
      // read as "no deals need anything", which is a quiet lie.
      next[name] = result.status === 'fulfilled' ? rows(result.value) : previous?.[name] ?? [];
    });
    lists = next as unknown as AgentLists;
    loadedAt = Date.now();
    failed = false;
  })().finally(() => {
    // A newer read has taken over; it owns the state now.
    if (inflight !== mine) return;
    inflight = null;
    const again = reloadAfter && key === forKey;
    reloadAfter = false;
    emit();
    if (again) void load(forKey);
  });
  inflight = mine;
  emit();
  return mine.promise;
}

function install(): void {
  if (installed || typeof window === 'undefined') return;
  const offChange = onDataChanged(() => {
    if (!key) return;
    // Nobody is looking (the panel is on no page right now — the till, say, where
    // every item rung up is a write). Note that the copy is stale and let the
    // next panel to appear re-read it, instead of re-reading eight lists per sale.
    if (listeners.size === 0) {
      loadedAt = 0;
      return;
    }
    if (settleTimer) clearTimeout(settleTimer);
    const target = key;
    settleTimer = setTimeout(() => {
      settleTimer = null;
      if (key === target) void load(target);
    }, SETTLE_MS);
  });
  const onVisible = () => {
    if (document.visibilityState === 'visible' && key) ensureFresh(key);
  };
  document.addEventListener('visibilitychange', onVisible);
  installed = () => {
    offChange();
    document.removeEventListener('visibilitychange', onVisible);
  };
}

/** The identity the lists belong to: this organisation, for this signed-in session. */
export function workspaceKey(orgId: string | null | undefined): string {
  return `${orgId ?? 'no-org'}|${getAuthToken() ?? 'no-session'}`;
}

/** Make sure the lists for `forKey` are loaded and not older than FRESH_FOR_MS. */
export function ensureFresh(forKey: string): void {
  install();
  if (forKey !== key) {
    key = forKey;
    lists = null;
    loadedAt = 0;
    failed = false;
    reloadAfter = false;
    emit();
    void load(forKey);
    return;
  }
  if (inflight) return;
  if (lists === null || Date.now() - loadedAt > FRESH_FOR_MS) void load(forKey);
}

/** Re-read now, behind whatever is shown. Used by the panel's retry. */
export function reloadAgentData(): void {
  if (key) void load(key);
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot(): AgentSnapshot {
  return snapshot;
}

/**
 * The agent's lists for this organisation. Loads on first use, re-reads behind
 * the scenes after any save, and never returns another workspace's lists.
 */
const noSubscription = () => () => {};

export function useAgentData(orgId: string | null | undefined, enabled = true): AgentSnapshot {
  // Disabled where the panel is hidden: no subscription, so the store does not
  // count this page as watching, and no load.
  const current = useSyncExternalStore(enabled ? subscribe : noSubscription, getSnapshot, getSnapshot);
  const wanted = workspaceKey(orgId);
  useEffect(() => {
    if (enabled) ensureFresh(wanted);
  }, [wanted, enabled]);
  // Until the store has switched to this workspace, show nothing from the last one.
  return current.key === wanted ? current : EMPTY;
}

/** Test hook: forget everything, including installed listeners. */
export function resetAgentDataForTests(): void {
  installed?.();
  installed = null;
  if (settleTimer) clearTimeout(settleTimer);
  settleTimer = null;
  key = null;
  lists = null;
  loadedAt = 0;
  inflight = null;
  reloadAfter = false;
  failed = false;
  snapshot = EMPTY;
  listeners.clear();
}

// Derives a lead's `status` from its 5-phase pipeline, so advancing a step in
// the tracker keeps the status badge honest without anyone retyping it.
//
// Everything here is PURE. The single impure write path lives in
// `saveLeadPipeline.ts`; the three interactive editors (LeadPipelineTracker,
// SalesTrackerCard, InlineSalesEditorPopover) all go through it, so they cannot
// disagree about what a click means.
//
// ── Invariants ────────────────────────────────────────────────────────────────
//  1. DATES ARE NEVER DERIVATION INPUTS. Only the categorical fields decide the
//     stage. Adding or clearing outreachDate / meetingDate / contractSentDate /
//     signatureDate / paymentDate must never move the status — filling in a date
//     you forgot last week is bookkeeping, not progress.
//  2. STRICT LITERAL COMPARISON. `parsePipeline()` does no validation, so legacy
//     or hand-edited JSON can hold `"yes"` / `1` / `"YES"` where a boolean or a
//     specific union member belongs. Every check below is `===` against an exact
//     literal; anything else reads as unset rather than as progress.
//  3. NEVER INVENT A STATUS. We only ever write a string that is already in the
//     org's own status list. When nothing suitable exists we stay silent.
//  4. FORWARD-ONLY AUTO-WRITES. A non-terminal stage that sits below the current
//     status is offered as a suggestion, never applied silently.

import type { LeadPipeline } from './leadPipeline';

// ── Canonical stages ─────────────────────────────────────────────────────────

/** Vocabulary-independent name for "where this lead has got to". */
export type CanonicalStage =
  | 'new'
  | 'attempted'
  | 'contacted'
  | 'meeting_scheduled'
  | 'meeting_held'
  | 'qualified'
  | 'contract_pending'
  | 'contract_sent'
  | 'signed'
  | 'deposit_paid'
  | 'unqualified'
  | 'lost';

/**
 * Position on the org's progress ladder. Higher = further along.
 * Terminal stages are deliberately `null` — they sit OFF the ladder, which is
 * what makes "moving to a terminal is never a regression, and moving away from
 * one is always forward" fall out of the comparison instead of needing a
 * special case.
 */
export const STAGE_TIER: Record<CanonicalStage, number | null> = {
  new: 0,
  attempted: 1,
  contacted: 2,
  meeting_scheduled: 3,
  meeting_held: 4,
  qualified: 5,
  contract_pending: 6,
  contract_sent: 7,
  signed: 8,
  deposit_paid: 9,
  unqualified: null,
  lost: null,
};

/** Which pipeline phase (1-5) each stage originates in; 0 for "nothing yet". */
export const STAGE_PHASE: Record<CanonicalStage, number> = {
  new: 0,
  attempted: 1,
  contacted: 1,
  meeting_scheduled: 1,
  meeting_held: 2,
  qualified: 2,
  contract_pending: 3,
  contract_sent: 3,
  signed: 4,
  deposit_paid: 5,
  unqualified: 3,
  lost: 0,
};

export interface DerivedStage {
  stage: CanonicalStage;
  /** Pipeline phase that produced this stage (1-5; 0 = no signal at all). */
  phase: number;
  /** Terminal stages halt forward progress (lost / unqualified). */
  terminal: boolean;
  /**
   * `explicit` — the user stated this outcome, so it is safe to act on.
   * `inferred` — we deduced it from an absence (a bare no-show with no word on
   * interest). Never auto-written; only ever offered as a suggestion.
   */
  certainty: 'explicit' | 'inferred';
  /**
   * Human-readable cause, always describing the signal that actually decided
   * this stage. Deliberately NOT `lostReason()`: that scans shallowest-first
   * while derivation scans deepest-first, so reusing it made a phase-4
   * "Contract declined" report a stale phase-1 "Not interested". `conflicts`
   * carries the other recorded negatives, so nothing is lost.
   */
  because: string;
  /** Stable id for the machine-readable activity trailer. */
  rule: string;
  /**
   * Negative signals this stage deliberately overrode, e.g. a banked deposit
   * winning over a stale phase-1 "not interested". Surfaced in the tracker and
   * the activity body: silently discarding a recorded negative is how a rep
   * loses trust in the automation.
   */
  conflicts: string[];
}

/** Every explicit negative currently recorded, in phase order. */
function negativeSignals(p: LeadPipeline): string[] {
  const out: string[] = [];
  if (p.contactOutcome === 'not_interested') out.push('Not interested at outreach');
  if (p.meetingAttended === false) out.push('No-show at meeting');
  if (p.stillInterested === false) out.push('Not interested after meeting');
  if (p.contractStatus === 'profile_rejected') out.push('Profile rejected');
  if (p.contractStatus === 'no_longer_interested') out.push('No longer interested');
  if (p.contractSigned === 'no') out.push('Contract declined');
  return out;
}

type StageCore = Omit<DerivedStage, 'conflicts'>;

const NO_SIGNAL: StageCore = {
  stage: 'new',
  phase: 0,
  terminal: false,
  certainty: 'explicit',
  because: 'No pipeline activity recorded',
  rule: 'no_signal',
};

/**
 * Reduce a pipeline to the single stage that best describes it.
 *
 * Scans **deepest phase first**. That ordering is the whole precedence rule: a
 * banked deposit outranks a stale phase-1 "not interested", because the deposit
 * is the newer fact. Conversely a declined signature (phase 4) outranks
 * "still interested" (phase 2) for the same reason.
 *
 * When a positive stage wins over recorded negatives, those negatives are listed
 * in `conflicts` rather than discarded.
 */
export function deriveStage(p: LeadPipeline): DerivedStage {
  const core = deriveStageCore(p);
  // Only a positive outcome can override a negative; a terminal simply *is* the
  // negative, so it has nothing to declare.
  const conflicts = core.terminal ? [] : negativeSignals(p);
  return { ...core, conflicts };
}

function deriveStageCore(p: LeadPipeline): StageCore {
  // ── Phase 5 — Deposit ──
  // `false` is not decisive: "not paid yet" is the default, not a setback.
  if (p.depositPaid === true) {
    return {
      stage: 'deposit_paid', phase: 5, terminal: false, certainty: 'explicit',
      because: 'Deposit paid', rule: 'phase5_deposit_paid',
    };
  }

  // ── Phase 4 — Signature ──
  if (p.contractSigned === 'yes') {
    return {
      stage: 'signed', phase: 4, terminal: false, certainty: 'explicit',
      because: 'Contract signed', rule: 'phase4_signed',
    };
  }
  if (p.contractSigned === 'no') {
    return {
      stage: 'lost', phase: 4, terminal: true, certainty: 'explicit',
      because: 'Contract declined', rule: 'phase4_declined',
    };
  }
  // "Awaiting signature" means the contract is out, which is real phase-4
  // progress — but it is the ABSENCE of a signature, not a new fact about the
  // lead's intent. So it ranks here (deepest-wins, above the phase-1/2/3
  // positives) yet defers to any explicit negative, letting the branches below
  // decide. Both constraints matter:
  //   · deferring keeps {stillInterested:false, contractSigned:'pending'} —
  //     they told us they're out, an old contract still in their inbox — from
  //     reading as a live opportunity;
  //   · ranking it here keeps the derivation monotone. Evaluated any lower, a
  //     lead whose only signal was 'pending' would DROP from contract_sent to
  //     contacted the moment someone recorded the earlier outreach.
  if (p.contractSigned === 'pending' && negativeSignals(p).length === 0) {
    return {
      stage: 'contract_sent', phase: 4, terminal: false, certainty: 'explicit',
      because: 'Awaiting signature', rule: 'phase4_pending',
    };
  }

  // ── Phase 3 — Contract ──
  if (p.contractStatus === 'yes') {
    return {
      stage: 'contract_sent', phase: 3, terminal: false, certainty: 'explicit',
      because: 'Contract sent', rule: 'phase3_contract_sent',
    };
  }
  if (p.contractStatus === 'to_be_sent') {
    return {
      stage: 'contract_pending', phase: 3, terminal: false, certainty: 'explicit',
      because: 'Contract to be sent', rule: 'phase3_contract_to_send',
    };
  }
  if (p.contractStatus === 'profile_rejected') {
    // "We rejected them" is a different fact from "they walked away".
    return {
      stage: 'unqualified', phase: 3, terminal: true, certainty: 'explicit',
      because: 'Profile rejected', rule: 'phase3_profile_rejected',
    };
  }
  if (p.contractStatus === 'no_longer_interested') {
    return {
      stage: 'lost', phase: 3, terminal: true, certainty: 'explicit',
      because: 'No longer interested', rule: 'phase3_no_longer_interested',
    };
  }

  // ── Phase 2 — After the meeting ──
  // Interest is recorded after attendance, so it is checked first: it is the
  // more recent word on intent. This is also why {no-show, still interested}
  // reads as Qualified rather than Lost.
  if (p.stillInterested === false) {
    return {
      stage: 'lost', phase: 2, terminal: true, certainty: 'explicit',
      because: 'Not interested after meeting', rule: 'phase2_not_interested',
    };
  }
  if (p.stillInterested === true) {
    return {
      stage: 'qualified', phase: 2, terminal: false, certainty: 'explicit',
      because: 'Still interested after the meeting', rule: 'phase2_qualified',
    };
  }
  if (p.meetingAttended === true) {
    return {
      stage: 'meeting_held', phase: 2, terminal: false, certainty: 'explicit',
      because: 'Attended the meeting', rule: 'phase2_meeting_held',
    };
  }
  if (p.meetingAttended === false) {
    // A missed meeting with no word on interest yet. Real, but ambiguous — the
    // lead may simply need rescheduling, so this is never auto-written.
    return {
      stage: 'lost', phase: 2, terminal: true, certainty: 'inferred',
      because: 'No-show at meeting', rule: 'phase2_no_show',
    };
  }

  // ── Phase 1 — Outreach ──
  if (p.contactOutcome === 'not_interested') {
    return {
      stage: 'lost', phase: 1, terminal: true, certainty: 'explicit',
      because: 'Not interested', rule: 'phase1_not_interested',
    };
  }
  if (p.contactOutcome === 'meeting_scheduled') {
    return {
      stage: 'meeting_scheduled', phase: 1, terminal: false, certainty: 'explicit',
      because: 'Meeting scheduled', rule: 'phase1_meeting_scheduled',
    };
  }
  if (p.contactOutcome === 'follow_up') {
    // A follow-up is still just "we have spoken" — it must not outrank a booked
    // meeting, which sits above it on the ladder.
    return {
      stage: 'contacted', phase: 1, terminal: false, certainty: 'explicit',
      because: 'Follow-up to be scheduled', rule: 'phase1_follow_up',
    };
  }
  if (p.outreachStatus === 'contacted') {
    return {
      stage: 'contacted', phase: 1, terminal: false, certainty: 'explicit',
      because: 'Contacted', rule: 'phase1_contacted',
    };
  }
  if (p.outreachStatus === 'attempted_no_answer') {
    return {
      stage: 'attempted', phase: 1, terminal: false, certainty: 'explicit',
      because: 'Attempted contact — no answer', rule: 'phase1_attempted',
    };
  }

  return NO_SIGNAL;
}

// ── Status vocabulary resolution ─────────────────────────────────────────────

export interface StatusOption {
  id: string;
  name: string;
  /** The org's own ordering, when configured. Used as a sanity check against
   *  our opinion of which stage is further along. */
  displayOrder?: number;
}

/**
 * Synonyms per canonical stage, MOST SPECIFIC FIRST. Ordering matters: an org
 * that configures both "Negotiation" and "Contract Signed" must get the precise
 * one, so coarse catch-alls always come last.
 *
 * Deliberately absent: bare "open". Every rep reads "Open" as "nobody has
 * touched this yet", so it must never be the target of a forward move.
 */
const STAGE_SYNONYMS: Record<CanonicalStage, string[]> = {
  new: ['new', 'new lead'],
  attempted: ['attempted contact', 'attempted', 'no answer', 'left message', 'tried'],
  contacted: ['contacted', 'reached out', 'in contact', 'follow up', 'follow-up', 'nurturing'],
  meeting_scheduled: ['meeting scheduled', 'meeting booked', 'demo scheduled', 'appointment set'],
  // 'connected' means "we actually got in front of them" — it is claimed by
  // meeting_HELD, not meeting_scheduled. A booked meeting is not a connection;
  // half of them no-show. Attendance is the fact that earns the label.
  meeting_held: ['meeting held', 'meeting done', 'demo done', 'discovery done', 'connected', 'met', 'in progress'],
  // 'contract pending' is listed under BOTH qualified and contract_pending on
  // purpose: met-and-interested and contract-to-be-sent are one status in this
  // vocabulary. Safe because a shared label resolves to the same option, so
  // crossing between the two stages leaves the written string unchanged.
  qualified: ['qualified', 'sql', 'mql', 'interested', 'contract pending'],
  contract_pending: ['contract to send', 'contract to be sent', 'contract pending', 'drafting contract', 'preparing proposal', 'proposal in progress'],
  contract_sent: ['awaiting signature', 'contract sent', 'proposal sent', 'quote sent', 'contract out', 'negotiation', 'proposal'],
  signed: ['contract signed', 'signed', 'closed won', 'won', 'open deal'],
  // Only DISTINCTIVE names here. The generic top-of-ladder labels ('open deal',
  // 'closed won', 'won') deliberately belong to `signed` alone: if both stages
  // listed them, they could resolve to *different* strings in the same
  // vocabulary and paying a deposit would move a lead from "Closed Won"
  // backwards to "Open Deal". With no deposit-specific status configured,
  // `deposit_paid` degrades down the chain to `signed` and lands on whatever
  // that resolved to — the same string, so the status simply holds.
  deposit_paid: ['deposit paid', 'deposit received', 'deposit', 'customer'],
  // Both terminals accept the combined 'lost / not interested' label. Terminals
  // never degrade down the ladder, so without it an org whose only negative
  // status is that combined label would resolve `unqualified` to NOTHING and a
  // rejected profile would silently leave the status untouched.
  unqualified: ['unqualified', 'disqualified', 'not a fit', 'rejected', 'lost / not interested', 'lost or not interested'],
  lost: ['lost', 'closed lost', 'dead', 'not interested', 'no longer interested', 'lost / not interested', 'lost or not interested'],
};

/**
 * Statuses that mean "a human deliberately parked this lead". We never write
 * over one automatically — a compliance or do-not-contact flag outranks any
 * inference we could make. This list is a convenience, not the only defence:
 * an unrecognised status is *also* protected (see `planStatusSync`), so a
 * parking status we have never heard of is safe too.
 */
const STICKY_STATUS_LABELS = [
  'do not contact', 'dnc', 'unsubscribed', 'opted out', 'junk', 'spam',
  'on hold', 'hold', 'nurture', 'recycled', 'bad timing', 'archived',
];

const norm = (s: string) => s.trim().toLowerCase().replace(/[_-]+/g, ' ').replace(/\s+/g, ' ');

/** Downgrade path used when an org has no status for a stage. Never upgrades:
 *  claiming more progress than the vocabulary can express would over-promise. */
const DOWNGRADE_CHAIN: CanonicalStage[] = [
  'deposit_paid', 'signed', 'contract_sent', 'contract_pending',
  'qualified', 'meeting_held', 'meeting_scheduled', 'contacted', 'attempted', 'new',
];

export interface ResolvedStatus {
  /** A name taken verbatim from the org's own status list. */
  name: string;
  /** The option's id, only when it is a real GUID (needed for `leadStatusId`). */
  id?: string;
  match: 'exact' | 'synonym' | 'fallback';
  /** The stage actually represented after any downgrade. */
  canonical: CanonicalStage;
}

const GUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function findOption(options: StatusOption[], label: string): StatusOption | undefined {
  const target = norm(label);
  return options.find((o) => norm(o.name) === target);
}

function resolveExactStage(stage: CanonicalStage, options: StatusOption[]): ResolvedStatus | null {
  const synonyms = STAGE_SYNONYMS[stage] ?? [];
  for (let i = 0; i < synonyms.length; i += 1) {
    const hit = findOption(options, synonyms[i]!);
    if (hit) {
      return {
        name: hit.name,
        id: GUID_RE.test(hit.id) ? hit.id : undefined,
        match: i === 0 ? 'exact' : 'synonym',
        canonical: stage,
      };
    }
  }
  return null;
}

/**
 * Pick a real status string for a stage from the org's configured list.
 * Tries the stage's own synonyms, then walks DOWN the ladder. Terminals never
 * degrade — a lead is lost or it is not. Returns null when nothing fits, and
 * callers then stay silent rather than guessing.
 *
 * `overrides` lets an org pin a stage to a specific status name, which is the
 * escape hatch for a fully renamed or non-English vocabulary.
 */
export function resolveStatus(
  stage: CanonicalStage,
  options: StatusOption[],
  overrides?: Partial<Record<CanonicalStage, string>>,
): ResolvedStatus | null {
  if (options.length === 0) return null;

  const pinned = overrides?.[stage];
  if (pinned) {
    const hit = findOption(options, pinned);
    if (hit) {
      return {
        name: hit.name,
        id: GUID_RE.test(hit.id) ? hit.id : undefined,
        match: 'exact',
        canonical: stage,
      };
    }
  }

  const direct = resolveExactStage(stage, options);
  if (direct) return direct;

  // Terminals have no meaningful weaker form.
  if (STAGE_TIER[stage] === null) return null;

  const start = DOWNGRADE_CHAIN.indexOf(stage);
  if (start === -1) return null;
  for (let i = start + 1; i < DOWNGRADE_CHAIN.length; i += 1) {
    const weaker = DOWNGRADE_CHAIN[i]!;
    // Consult overrides on the way down as well: an org that renamed its whole
    // vocabulary pins several stages, and honouring them only for the derived
    // stage would strand exactly the case overrides exist for.
    const pinnedWeaker = overrides?.[weaker];
    if (pinnedWeaker) {
      const pinnedHit = findOption(options, pinnedWeaker);
      if (pinnedHit) {
        return {
          name: pinnedHit.name,
          id: GUID_RE.test(pinnedHit.id) ? pinnedHit.id : undefined,
          match: 'fallback',
          canonical: weaker,
        };
      }
    }
    const hit = resolveExactStage(weaker, options);
    if (hit) return { ...hit, match: 'fallback', canonical: weaker };
  }
  return null;
}

/** The tier a pipeline currently derives to — the baseline to record when a user
 *  overrides the status by hand. Null for terminal / unrankable stages. */
export function derivedTier(p: LeadPipeline): number | null {
  return STAGE_TIER[deriveStage(p).stage];
}

/** Best-guess canonical stage for an arbitrary status string, or null when the
 *  label is not one we recognise (a custom or parking status). */
export function classifyStatusLabel(label: string): CanonicalStage | null {
  const target = norm(label);
  if (!target) return null;
  const stages = Object.keys(STAGE_SYNONYMS) as CanonicalStage[];
  // Exact synonym hit only — no fuzzy matching. Guessing at an unfamiliar label
  // is exactly how a deliberately parked lead gets clobbered.
  for (const stage of stages) {
    if ((STAGE_SYNONYMS[stage] ?? []).some((s) => norm(s) === target)) return stage;
  }
  return null;
}

export function isStickyStatus(label: string): boolean {
  const target = norm(label);
  return STICKY_STATUS_LABELS.some((s) => norm(s) === target);
}

// ── Planning a sync ──────────────────────────────────────────────────────────

export type SkipReason =
  | 'statuses_loading'
  | 'converted'
  | 'disabled'
  | 'no_signal'
  | 'unresolvable'
  | 'noop';

export type SuggestReason =
  | 'inferred'
  | 'would_regress'
  | 'sticky_status'
  | 'unrecognised_status'
  | 'manual_change'
  | 'vocabulary_mismatch';

export interface StatusSyncActivity {
  subject: string;
  body: string;
}

export type StatusSyncPlan =
  | {
      kind: 'apply';
      from: string;
      to: ResolvedStatus;
      derived: DerivedStage;
      activity: StatusSyncActivity;
    }
  | {
      kind: 'suggest';
      from: string;
      to: ResolvedStatus;
      derived: DerivedStage;
      /** Primary reason — the one to lead with in the UI. */
      reason: SuggestReason;
      /**
       * EVERY reason that applies. A lead can be both manually pinned and
       * wearing an unrecognised status; showing only the first strands the user
       * on "I turned the pin off and nothing happened".
       */
      reasons: SuggestReason[];
    }
  | { kind: 'skip'; reason: SkipReason; derived: DerivedStage };

export interface StatusSyncContext {
  pipeline: LeadPipeline;
  currentStatus: string;
  statusOptions: StatusOption[];
  /** False while `getLeadStatuses()` is still in flight. Both pages hand out
   *  FALLBACK_STATUSES synchronously, so syncing before the real list arrives
   *  could write a status this org does not actually have. */
  statusesLoaded: boolean;
  isConverted?: boolean;
  /** Per-user opt-out. */
  enabled?: boolean;
  /** The status this feature last wrote for the lead. When the live status has
   *  since diverged, a human changed it by hand (possibly on another device) —
   *  so we suggest instead of overwriting their decision. */
  lastAutoStatus?: string;
  /** A status the user picked deliberately. Unlike `lastAutoStatus` divergence,
   *  this also protects a lead auto-sync has never written to. */
  manualStatus?: string;
  /**
   * The pipeline's derived tier at the moment of that pick, or null if it was
   * terminal / unrankable. The hold releases only when the pipeline climbs
   * ABOVE this — not above the picked status itself, which would void the hold
   * immediately: auto-sync had already derived something higher, which is why it
   * wrote in the first place.
   */
  manualHeldTier?: number | null;
  /** Org-level stage → status-name pins. */
  overrides?: Partial<Record<CanonicalStage, string>>;
}

/** Machine-readable trailer so support can answer "why did this org get
 *  Qualified instead of Contract Sent" without a repro. */
function trailer(derived: DerivedStage, to: ResolvedStatus): string {
  return `[auto-status rule=${derived.rule} canonical=${to.canonical} match=${to.match} phase=${derived.phase}]`;
}

function buildActivity(from: string, to: ResolvedStatus, derived: DerivedStage): StatusSyncActivity {
  const lines = [
    `Because you set ${derived.because}${derived.phase ? ` (Phase ${derived.phase})` : ''}.`,
    from ? `Status moved from "${from}" to "${to.name}".` : `Status set to "${to.name}".`,
  ];
  if (to.match === 'fallback') {
    lines.push(`No status matched "${derived.stage}", so the closest earlier stage was used.`);
  }
  if (derived.conflicts.length > 0) {
    lines.push(`Overridden: ${derived.conflicts.join('; ')} — a later positive outcome takes precedence.`);
  }
  lines.push(trailer(derived, to));
  return {
    subject: `Status auto-updated to ${to.name}`,
    body: lines.join('\n'),
  };
}

/**
 * Decide what — if anything — should happen to the lead's status given its
 * pipeline. The disposition ladder is ordered so that every case which could
 * overwrite a human decision degrades to a suggestion instead of a write.
 */
export function planStatusSync(ctx: StatusSyncContext): StatusSyncPlan {
  const derived = deriveStage(ctx.pipeline);

  if (!ctx.statusesLoaded) return { kind: 'skip', reason: 'statuses_loading', derived };
  if (ctx.isConverted) return { kind: 'skip', reason: 'converted', derived };
  if (ctx.enabled === false) return { kind: 'skip', reason: 'disabled', derived };
  if (derived.phase === 0 && derived.stage === 'new') {
    // Nothing recorded — and clearing a pipeline should not drag the status
    // back to New behind the user's back. Drift surfacing handles that case.
    return { kind: 'skip', reason: 'no_signal', derived };
  }

  const to = resolveStatus(derived.stage, ctx.statusOptions, ctx.overrides);
  if (!to) return { kind: 'skip', reason: 'unresolvable', derived };

  const from = ctx.currentStatus ?? '';
  if (norm(to.name) === norm(from)) return { kind: 'skip', reason: 'noop', derived };

  const fromStage = from ? classifyStatusLabel(from) : 'new';
  const fromTier = fromStage ? STAGE_TIER[fromStage] : null;
  const toTier = STAGE_TIER[to.canonical];
  const advancesBeyond = toTier !== null && fromTier !== null && toTier > fromTier;

  // Collect EVERY applicable hold, not just the first, so the UI can explain all
  // of them and the user is never left clicking one release with no effect.
  const reasons: SuggestReason[] = [];

  // A deliberately parked lead is never restatused automatically.
  if (isStickyStatus(from)) reasons.push('sticky_status');

  // An unfamiliar status is treated as deliberate too — blocklists are never
  // complete, so anything we cannot classify gets the same protection.
  if (from && fromStage === null) reasons.push('unrecognised_status');

  // A human chose the current status. Two independent signals, because either
  // alone leaves a hole:
  //   · `manualStatus` — we recorded the pick here. Works even for a lead
  //     auto-sync has never written to.
  //   · `lastAutoStatus` divergence — the live status is not what we last wrote,
  //     so *someone* changed it, including on a device whose localStorage we
  //     cannot see.
  //
  // The hold is RELEASABLE: it lasts only while the pipeline has not moved past
  // the status they chose, so a lead someone once touched by hand does not sit
  // in suggest-mode forever — exactly the leads that get worked the most.
  //
  // A hand-set TERMINAL (Lost / Unqualified) therefore holds until the pipeline
  // records something genuinely deeper than it had when the lead was written
  // off — a banked deposit does outrank a write-off, but merely re-saving the
  // same phases does not. The drift strip offers one-click revival regardless.
  const humanPicked = !!ctx.manualStatus && norm(from) === norm(ctx.manualStatus);
  const divergedFromOurWrite = !!ctx.lastAutoStatus && norm(from) !== norm(ctx.lastAutoStatus);
  if (humanPicked || divergedFromOurWrite) {
    // Release only on genuine NEW progress since the intervention. Measuring
    // against the picked status instead would void every hold on creation, and
    // would make Undo revert for exactly one render.
    const held = ctx.manualHeldTier;
    const releasedByProgress = held !== undefined && held !== null
      && toTier !== null && toTier > held;
    // With no recorded baseline (a status changed on another device), fall back
    // to the coarser check so the hold can still release eventually.
    const releasedWithoutBaseline = held === undefined && advancesBeyond;
    if (!releasedByProgress && !releasedWithoutBaseline) reasons.push('manual_change');
  }

  // A bare no-show is real but ambiguous — never mark a lead dead for one
  // missed call without the user saying so.
  if (derived.certainty === 'inferred') reasons.push('inferred');

  // Forward-only: a lower non-terminal stage is offered, not applied. Terminals
  // are off-ladder, so moving to one is never treated as a regression.
  if (!derived.terminal && toTier !== null && fromTier !== null && toTier < fromTier) {
    reasons.push('would_regress');
  }

  // Sanity-check our tier opinion against the org's own ordering. If they
  // disagree about which of {current, target} is further along, this org's
  // ladder is shaped differently from ours and we should not act on a guess.
  if (!derived.terminal) {
    const fromOpt = findOption(ctx.statusOptions, from);
    const toOpt = findOption(ctx.statusOptions, to.name);
    if (
      fromOpt?.displayOrder !== undefined && toOpt?.displayOrder !== undefined
      && toTier !== null && fromTier !== null && toTier !== fromTier
      && fromOpt.displayOrder !== toOpt.displayOrder
    ) {
      const orgSaysForward = toOpt.displayOrder > fromOpt.displayOrder;
      const weSayForward = toTier > fromTier;
      if (orgSaysForward !== weSayForward) reasons.push('vocabulary_mismatch');
    }
  }

  if (reasons.length > 0) {
    return { kind: 'suggest', from, to, derived, reason: reasons[0]!, reasons };
  }

  return { kind: 'apply', from, to, derived, activity: buildActivity(from, to, derived) };
}

// ── Drift (state-derived, not edit-derived) ──────────────────────────────────

export interface StatusDrift {
  derived: DerivedStage;
  suggested: ResolvedStatus;
  currentStatus: string;
}

/**
 * Whether the lead's status currently disagrees with its pipeline — computed
 * from state alone, so it survives a reload, shows drift a colleague caused,
 * and covers the cases `planStatusSync` deliberately declines to write
 * (regressions, no-shows, cleared pipelines, parked statuses).
 */
export function statusDrift(
  // `enabled` is intentionally absent: drift is a statement about the data, not
  // about the automation, so it stays visible when auto-sync is switched off —
  // that is when it is the user's only route to reconciling the two.
  ctx: Omit<StatusSyncContext, 'lastAutoStatus' | 'enabled'>,
): StatusDrift | null {
  const derived = deriveStage(ctx.pipeline);
  if (!ctx.statusesLoaded || ctx.isConverted) return null;

  const from = ctx.currentStatus ?? '';

  // Pipeline cleared but the status still claims progress.
  if (derived.phase === 0 && derived.stage === 'new') {
    const fromStage = from ? classifyStatusLabel(from) : null;
    const fromTier = fromStage ? STAGE_TIER[fromStage] : null;
    if (fromTier === null || fromTier <= 0) return null;
    const suggested = resolveStatus('new', ctx.statusOptions, ctx.overrides);
    return suggested ? { derived, suggested, currentStatus: from } : null;
  }

  const suggested = resolveStatus(derived.stage, ctx.statusOptions, ctx.overrides);
  if (!suggested) return null;
  if (norm(suggested.name) === norm(from)) return null;
  return { derived, suggested, currentStatus: from };
}

// ── Preview ──────────────────────────────────────────────────────────────────

/**
 * The status a pending pipeline edit would produce, for rendering inline on the
 * control that would cause it ("Yes → Open Deal"). Returns null when the edit
 * would not move the status, so the hint only ever appears when it means
 * something. Uses the same `planStatusSync` decision as the real write, so a
 * preview can never promise an outcome the click would not deliver.
 */
export function previewStatusChange(
  base: LeadPipeline,
  patch: Partial<LeadPipeline>,
  ctx: Omit<StatusSyncContext, 'pipeline'>,
): ResolvedStatus | null {
  const plan = planStatusSync({ ...ctx, pipeline: { ...base, ...patch } });
  return plan.kind === 'apply' ? plan.to : null;
}

/** Short label for the suggestion strip / toast copy. */
export function describeSuggestReason(reason: SuggestReason, from: string): string {
  switch (reason) {
    case 'inferred':
      return 'A missed meeting might just need rescheduling, so this one is your call.';
    case 'would_regress':
      return `This would move the lead backwards from "${from}".`;
    case 'sticky_status':
      return `"${from}" looks deliberate, so it was left alone.`;
    case 'unrecognised_status':
      return `"${from}" isn't a stage this tracker recognises, so it was left alone.`;
    case 'manual_change':
      return `"${from}" was set by hand, so it was left alone.`;
    case 'vocabulary_mismatch':
      return `Your status order disagrees with the tracker about whether this is a step forward.`;
  }
}

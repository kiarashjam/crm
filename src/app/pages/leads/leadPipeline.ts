// Structured 5-phase sales lifecycle for a lead.
//
// The whole progression is serialized to JSON and stored on the lead's
// `pipelineState` field (persisted server-side, so the entire org shares one
// view). This module owns the shape, the labels, and the logic that derives the
// current phase / outcome from the raw state. Nothing here touches the network.
//
//   Phase 1  Outreach     — attempted vs contacted (+ interaction date), and if
//                           contacted: meeting scheduled / follow-up / not interested
//   Phase 2  Meeting       — showed up? still interested?
//   Phase 3  Contract      — yes / to be sent / profile rejected / no longer interested
//   Phase 4  Signature     — signed yes / pending / no
//   Phase 5  Deposit        — paid yes / no  → when paid, the lead becomes a deal

export type OutreachStatus = 'attempted_no_answer' | 'contacted';
export type ContactOutcome = 'meeting_scheduled' | 'follow_up' | 'not_interested';
export type ContractStatus = 'yes' | 'to_be_sent' | 'profile_rejected' | 'no_longer_interested';
export type ContractSigned = 'yes' | 'pending' | 'no';

/**
 * Phase 5, shaped like phase 4 rather than as a boolean.
 *
 * `depositPaid?: boolean` could not tell "not paid yet" apart from "they never
 * paid and they are gone", so phase 5 was the one stage with no way to record a
 * failure at all. Legacy `false` normalises to `pending` — the reading that does
 * not retroactively mark existing leads as lost.
 */
export type DepositStatus = 'paid' | 'pending' | 'not_paid';

/**
 * Why a lead walked away. ONE list, used at both the Meeting and the Contract
 * phase — asked for explicitly so drop-off reasons stay comparable across
 * stages instead of becoming two vocabularies that cannot be reported together.
 */
export type DropoutReason = 'has_kids' | 'not_within_budget' | 'too_soon' | 'other';

export const DROPOUT_REASON_LABELS: Record<DropoutReason, string> = {
  has_kids: 'Has kids / not kid-friendly club',
  not_within_budget: 'Not within budget',
  too_soon: 'Too soon',
  other: 'Other',
};

/** Menu order, so every phase presents the options identically. */
export const DROPOUT_REASONS: DropoutReason[] = [
  'has_kids', 'not_within_budget', 'too_soon', 'other',
];

/**
 * Why WE turned them down — a different question, so a different list.
 *
 * The drop-out reasons above are all the customer's: has kids, not within
 * budget, too soon. None of them is something we would be reporting about our
 * OWN decision to reject an application, and forcing that list onto a rejection
 * would produce a report that reads as customer sentiment while actually
 * recording our admissions policy.
 */
export type RejectionReason =
  | 'does_not_meet_criteria'
  | 'incomplete_application'
  | 'references_or_background'
  | 'no_capacity'
  | 'conduct_or_conflict'
  | 'other';

export const REJECTION_REASON_LABELS: Record<RejectionReason, string> = {
  does_not_meet_criteria: 'Does not meet membership criteria',
  incomplete_application: 'Application incomplete',
  references_or_background: 'References or background check',
  no_capacity: 'No capacity / waiting list',
  conduct_or_conflict: 'Conduct or conflict of interest',
  other: 'Other',
};

export const REJECTION_REASONS: RejectionReason[] = [
  'does_not_meet_criteria', 'incomplete_application', 'references_or_background',
  'no_capacity', 'conduct_or_conflict', 'other',
];

/** Who ended it, and therefore which list of reasons applies. */
export type FailureKind = 'dropout' | 'rejection';

/** A recorded failure: where it happened and whose decision it was. */
export interface FailurePoint {
  /** 1-based phase. */
  phase: number;
  kind: FailureKind;
  /** What was recorded, in words. */
  label: string;
}

export interface LeadPipeline {
  // Phase 1 — Outreach
  outreachStatus?: OutreachStatus;
  outreachDate?: string;     // date of the interaction (YYYY-MM-DD)
  contactOutcome?: ContactOutcome;
  meetingDate?: string;      // scheduled meeting date
  // Phase 2 — After the meeting
  meetingAttended?: boolean;
  stillInterested?: boolean;
  // Phase 3 — Contract
  contractStatus?: ContractStatus;
  contractSentDate?: string;
  // Phase 4 — Signature
  contractSigned?: ContractSigned;
  signatureDate?: string;
  // Phase 5 — Deposit
  depositStatus?: DepositStatus;
  /**
   * Legacy. Read on parse and normalised into `depositStatus`; never written.
   * Kept on the type so old JSON still type-checks where it is read.
   */
  depositPaid?: boolean;
  paymentDate?: string;

  // ── Why they dropped out ───────────────────────────────────────────────────
  /** Selected from the shared list. Mandatory once a drop-out is recorded. */
  dropoutReason?: DropoutReason;
  /** Free text, mandatory when the reason is `other`. */
  dropoutReasonOther?: string;
  /**
   * The 1-based phase the reason was captured at — auto-tagged, never typed, so
   * the report can break drop-off down by the stage it actually happened at.
   *
   * Set once, at the moment the reason is first recorded, and then left alone.
   * A later negative at a deeper phase is bookkeeping on a lead that already
   * left; re-tagging it would move an explanation to a stage that did not
   * produce it and quietly corrupt the very report this field exists to feed.
   */
  dropoutReasonPhase?: number;

  // ── Why we rejected them ───────────────────────────────────────────────────
  /** From the rejection list. Mandatory once a rejection is recorded. */
  rejectionReason?: RejectionReason;
  /** Free text, mandatory when the rejection reason is `other`. */
  rejectionReasonOther?: string;
}

export const PHASE_TITLES = ['Outreach', 'Meeting', 'Contract', 'Signature', 'Deposit'] as const;

export const OUTREACH_LABELS: Record<OutreachStatus, string> = {
  attempted_no_answer: 'Attempted contact — no answer',
  contacted: 'Contacted',
};
export const OUTCOME_LABELS: Record<ContactOutcome, string> = {
  meeting_scheduled: 'Meeting scheduled',
  follow_up: 'To be scheduled / follow-up',
  not_interested: 'Not interested',
};
export const CONTRACT_LABELS: Record<ContractStatus, string> = {
  yes: 'Yes',
  to_be_sent: 'To be sent',
  profile_rejected: 'Profile rejected',
  no_longer_interested: 'No longer interested',
};
export const SIGNED_LABELS: Record<ContractSigned, string> = {
  yes: 'Signed',
  pending: 'Pending',
  no: 'Not signed',
};
export const DEPOSIT_LABELS: Record<DepositStatus, string> = {
  paid: 'Paid',
  pending: 'Not paid yet',
  not_paid: 'Never paid',
};

export function parsePipeline(raw?: string | null): LeadPipeline {
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw) as LeadPipeline;
    if (!parsed || typeof parsed !== 'object') return {};
    return normalise(parsed);
  } catch {
    return {};
  }
}

/**
 * Folds legacy shapes into the current one, at the ONE boundary where raw JSON
 * becomes a pipeline.
 *
 * Doing it here rather than at each reader is the whole point: a codebase that
 * checks `depositPaid === true || depositStatus === 'paid'` in nine places has
 * nine chances to forget one. Everything past this function sees only
 * `depositStatus`.
 *
 * Legacy `false` becomes `pending`, not `not_paid`. It was written when there was
 * no way to say "they never paid", so reading it as a failure now would mark
 * existing leads lost retroactively.
 */
function normalise(p: LeadPipeline): LeadPipeline {
  if (p.depositStatus === undefined && typeof p.depositPaid === 'boolean') {
    return { ...p, depositStatus: p.depositPaid ? 'paid' : 'pending' };
  }
  return p;
}

export function serializePipeline(p: LeadPipeline): string {
  return JSON.stringify(p);
}

/** Whether each of the 5 phases is fully satisfied (index 0 = phase 1). */
export function phaseCompletion(p: LeadPipeline): boolean[] {
  const p1 = p.outreachStatus === 'contacted' && p.contactOutcome === 'meeting_scheduled' && !!p.meetingDate;
  const p2 = p.meetingAttended === true && p.stillInterested === true;
  const p3 = p.contractStatus === 'yes' && !!p.contractSentDate;
  const p4 = p.contractSigned === 'yes' && !!p.signatureDate;
  const p5 = p.depositStatus === 'paid' && !!p.paymentDate;
  return [p1, p2, p3, p4, p5];
}

/** True once every phase is complete — the lead is ready to become a deal. */
export function isPipelineComplete(p: LeadPipeline): boolean {
  return phaseCompletion(p).every(Boolean);
}

/**
 * Drop-out rules in precedence order, with the phase that RECORDED each one.
 *
 * A single table so `lostReason` and `lostPhase` can never disagree about which
 * rule fired — they are two projections of one ordered list.
 */
const LOST_RULES: { phase: number; reason: string; hit: (p: LeadPipeline) => boolean }[] = [
  { phase: 1, reason: 'Not interested', hit: (p) => p.contactOutcome === 'not_interested' },
  // `stillInterested` is recorded after attendance, so it is the more recent
  // word on the lead's intent: a no-show who has since confirmed interest
  // (rescheduled, answered by email) has NOT dropped out. Checking interest
  // first keeps this in agreement with the derived lead status — otherwise the
  // detail page renders "dropped out: No-show" directly above a Qualified badge.
  { phase: 2, reason: 'Not interested after meeting', hit: (p) => p.stillInterested === false },
  { phase: 2, reason: 'No-show at meeting', hit: (p) => p.stillInterested !== true && p.meetingAttended === false },
  { phase: 3, reason: 'Profile rejected', hit: (p) => p.contractStatus === 'profile_rejected' },
  { phase: 3, reason: 'No longer interested', hit: (p) => p.contractStatus === 'no_longer_interested' },
  { phase: 4, reason: 'Contract declined', hit: (p) => p.contractSigned === 'no' },
  // Phase 5 had no failure rule at all, because a boolean could not express one.
  { phase: 5, reason: 'Deposit never paid', hit: (p) => p.depositStatus === 'not_paid' },
];

/**
 * A terminal "lost" reason, if the lead dropped out at any phase. These halt
 * forward progress regardless of which phase they occur in.
 */
export function lostReason(p: LeadPipeline): string | null {
  return LOST_RULES.find((r) => r.hit(p))?.reason ?? null;
}

/**
 * The 1-based phase that RECORDED the drop-out — which is NOT `currentPhase()`.
 *
 * They diverge whenever a later phase records a loss while an earlier one is
 * still incomplete: `{ contractStatus: 'profile_rejected' }` with no meeting
 * logged reports phase 3 here but phase 2 as current. Marking the "stopped"
 * card by current phase would put the rose treatment on Meeting, when the
 * rejection was recorded against Contract.
 */
export function lostPhase(p: LeadPipeline): number | null {
  return LOST_RULES.find((r) => r.hit(p))?.phase ?? null;
}

/**
 * Every way a lead can fail, deepest phase first, with whose decision it was.
 *
 * ONE ordered table, so "which phase failed", "which reason list applies" and
 * "is a reason owed" cannot disagree — they are three projections of this list.
 *
 * Deepest first, matching the stage derivation: the newest recorded fact is the
 * one that describes where the lead actually stopped.
 *
 * A rejection draws on a different vocabulary because it answers a different
 * question. "Profile rejected" is us declining them, and none of the drop-out
 * reasons — has kids, not within budget, too soon — is something we would be
 * reporting about our own admissions decision.
 */
const FAILURE_RULES: (FailurePoint & { hit: (p: LeadPipeline) => boolean })[] = [
  {
    phase: 5, kind: 'dropout', label: 'Deposit never paid',
    hit: (p) => p.depositStatus === 'not_paid',
  },
  {
    phase: 4, kind: 'dropout', label: 'Contract declined',
    hit: (p) => p.contractSigned === 'no',
  },
  {
    phase: 3, kind: 'rejection', label: 'Profile rejected',
    hit: (p) => p.contractStatus === 'profile_rejected',
  },
  {
    phase: 3, kind: 'dropout', label: 'No longer interested',
    hit: (p) => p.contractStatus === 'no_longer_interested',
  },
  {
    phase: 2, kind: 'dropout', label: 'Not interested after the meeting',
    hit: (p) => p.stillInterested === false,
  },
  {
    phase: 1, kind: 'dropout', label: 'Not interested at outreach',
    hit: (p) => p.contactOutcome === 'not_interested',
  },
];

/**
 * The failure this pipeline records, or null while the lead is still live.
 *
 * A bare no-show is deliberately NOT a failure. A missed meeting often just
 * needs rescheduling, and every phase now has an explicit way to say "they are
 * out" — so silence about intent means the lead is still open, not lost.
 */
export function failurePointFor(p: LeadPipeline): FailurePoint | null {
  const hit = FAILURE_RULES.find((r) => r.hit(p));
  return hit ? { phase: hit.phase, kind: hit.kind, label: hit.label } : null;
}

/** The 1-based phase a failure was recorded at, or null. */
export function dropoutPhaseFor(p: LeadPipeline): number | null {
  return failurePointFor(p)?.phase ?? null;
}

/** True when a failure has been recorded, so a reason is owed. */
export function isReasonRequired(p: LeadPipeline): boolean {
  return failurePointFor(p) !== null;
}

/**
 * True when the reason requirement is satisfied — including the free-text box,
 * which is mandatory in its own right once "Other" is chosen. An "Other" with
 * nothing typed is the exact hole this check exists to close.
 *
 * Reads whichever field the failure's own vocabulary lives in, so a rejection
 * cannot be satisfied by a drop-out reason left over from an earlier phase.
 */
export function isReasonComplete(p: LeadPipeline): boolean {
  const failure = failurePointFor(p);
  if (!failure) return true;
  if (failure.kind === 'rejection') {
    if (!p.rejectionReason) return false;
    return p.rejectionReason === 'other' ? !!p.rejectionReasonOther?.trim() : true;
  }
  if (!p.dropoutReason) return false;
  return p.dropoutReason === 'other' ? !!p.dropoutReasonOther?.trim() : true;
}

/** The recorded reason as one readable string, or null if none is on record. */
export function dropoutReasonText(p: LeadPipeline): string | null {
  const failure = failurePointFor(p);
  if (failure?.kind === 'rejection') {
    if (!p.rejectionReason) return null;
    if (p.rejectionReason === 'other') {
      const t = p.rejectionReasonOther?.trim();
      return t ? t : null;
    }
    return REJECTION_REASON_LABELS[p.rejectionReason];
  }
  if (!p.dropoutReason) return null;
  if (p.dropoutReason === 'other') {
    const t = p.dropoutReasonOther?.trim();
    return t ? t : null;
  }
  return DROPOUT_REASON_LABELS[p.dropoutReason];
}

/** 1-based index of the phase currently in progress (the first incomplete one). */
export function currentPhase(p: LeadPipeline): number {
  const done = phaseCompletion(p);
  const idx = done.findIndex((d) => !d);
  return idx === -1 ? 5 : idx + 1;
}

/** True when the lead has no recorded pipeline activity at all. */
export function isPipelineEmpty(p: LeadPipeline): boolean {
  return Object.keys(p).length === 0;
}

export type PipelineBadgeTone = 'lost' | 'complete' | 'active' | 'idle';

/** Compact summary for list cards / chips. */
export function pipelineBadge(p: LeadPipeline): { label: string; tone: PipelineBadgeTone } {
  if (isPipelineEmpty(p)) return { label: 'Not started', tone: 'idle' };
  const lost = lostReason(p);
  if (lost) return { label: lost, tone: 'lost' };
  if (isPipelineComplete(p)) return { label: 'Deal-ready', tone: 'complete' };
  const n = currentPhase(p);
  return { label: `Phase ${n} · ${PHASE_TITLES[n - 1]}`, tone: 'active' };
}


/** `Aug 20`, or null for a missing/unparseable date. */
function shortDate(iso?: string): string | null {
  if (!iso) return null;
  // A bare YYYY-MM-DD parses as UTC midnight, which renders as the PREVIOUS day
  // in any negative-offset timezone. Pinning the time keeps it local.
  const d = new Date(`${iso}T00:00:00`);
  if (Number.isNaN(d.getTime())) return null;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

export interface ReasonByStageRow {
  /** 1-based phase the reason was captured at. */
  phase: number;
  phaseTitle: string;
  /** Whose decision these are — the two are never mixed in one row. */
  kind: FailureKind;
  /** Count per reason, plus how many failures at this phase have no reason yet. */
  counts: Record<string, number>;
  missing: number;
  total: number;
}

/**
 * Failure reasons broken down by the stage they were logged at.
 *
 * Every phase now, not just two. Phase 1 was the largest source of drop-off and
 * used to be invisible here because it never asked for a reason; phase 5 could
 * not record a failure at all.
 *
 * Drop-outs and rejections are reported as SEPARATE rows even at the same phase.
 * Adding "profile rejected" into the same bucket as "not within budget" would
 * produce a chart that reads as customer sentiment while half of it is our own
 * admissions decisions.
 *
 * `missing` is reported rather than hidden: a stage with fifteen failures and
 * eleven reasons is a data-collection problem, and rounding it away would make
 * the chart look complete while the insight is still missing.
 */
export function dropoutReasonBreakdown(pipelines: LeadPipeline[]): ReasonByStageRow[] {
  const key = (phase: number, kind: FailureKind) => `${phase}:${kind}`;
  const rows = new Map<string, ReasonByStageRow>();

  const ensure = (phase: number, kind: FailureKind): ReasonByStageRow => {
    const k = key(phase, kind);
    let row = rows.get(k);
    if (!row) {
      const vocabulary = kind === 'rejection' ? REJECTION_REASONS : DROPOUT_REASONS;
      row = {
        phase,
        phaseTitle: PHASE_TITLES[phase - 1] ?? `Phase ${phase}`,
        kind,
        counts: Object.fromEntries(vocabulary.map((r) => [r, 0])),
        missing: 0,
        total: 0,
      };
      rows.set(k, row);
    }
    return row;
  };

  for (const p of pipelines) {
    const failure = failurePointFor(p);
    if (!failure) continue;

    // Attribute a drop-out to the phase it was TAGGED at when one is recorded, so
    // an explanation given at outreach is not re-attributed to a later phase that
    // merely tidied up the record afterwards. Rows written before tagging existed
    // have no tag, and dropping them would understate every total.
    const phase = failure.kind === 'dropout' ? p.dropoutReasonPhase ?? failure.phase : failure.phase;
    const row = ensure(phase, failure.kind);
    row.total += 1;

    const picked = failure.kind === 'rejection' ? p.rejectionReason : p.dropoutReason;
    if (isReasonComplete(p) && picked) row.counts[picked] = (row.counts[picked] ?? 0) + 1;
    else row.missing += 1;
  }

  // Phase order, then drop-outs before rejections within a phase, so the table
  // reads down the funnel.
  return [...rows.values()].sort((a, b) =>
    a.phase - b.phase || (a.kind === b.kind ? 0 : a.kind === 'dropout' ? -1 : 1));
}

/** One recorded fact inside a phase. */
export interface PhaseStep {
  /** What this field records, e.g. "Meeting date". */
  label: string;
  /** True when it holds the value the phase needs in order to advance. */
  done: boolean;
  /**
   * What is actually on record right now, or null if untouched. Distinct from
   * `done`: "To be sent" is a real answer that does not advance the phase, and
   * a collapsed card needs to show it rather than looking blank.
   */
  value: string | null;
}

/**
 * The individual facts each phase is waiting on, in the order they are asked.
 *
 * This is what lets a folded-away card still say something useful — a row of
 * dots for progress within the phase, and the values already captured. The
 * three-way distinction (done / answered-but-not-advancing / untouched) is the
 * point: a phase sitting on "Profile rejected" is not the same as an empty one,
 * and both are incomplete.
 *
 * INVARIANT, pinned by a test: `phaseSteps(p)[i].every(s => s.done)` equals
 * `phaseCompletion(p)[i]`. The two must never disagree.
 */
export function phaseSteps(p: LeadPipeline): PhaseStep[][] {
  const bool = (v: boolean | undefined, yes: string, no: string) =>
    v === undefined ? null : v ? yes : no;

  return [
    [
      {
        label: 'Contact made',
        done: p.outreachStatus === 'contacted',
        value: p.outreachStatus ? OUTREACH_LABELS[p.outreachStatus] : null,
      },
      {
        label: 'Result of contact',
        done: p.contactOutcome === 'meeting_scheduled',
        value: p.contactOutcome ? OUTCOME_LABELS[p.contactOutcome] : null,
      },
      { label: 'Meeting date', done: !!p.meetingDate, value: shortDate(p.meetingDate) },
    ],
    [
      {
        label: 'Attendance',
        done: p.meetingAttended === true,
        value: bool(p.meetingAttended, 'Attended', 'No-show'),
      },
      {
        label: 'Still interested',
        done: p.stillInterested === true,
        value: bool(p.stillInterested, 'Still interested', 'Not interested'),
      },
    ],
    [
      {
        label: 'Contract status',
        done: p.contractStatus === 'yes',
        value: p.contractStatus ? CONTRACT_LABELS[p.contractStatus] : null,
      },
      { label: 'Sent date', done: !!p.contractSentDate, value: shortDate(p.contractSentDate) },
    ],
    [
      {
        label: 'Signature',
        done: p.contractSigned === 'yes',
        value: p.contractSigned ? SIGNED_LABELS[p.contractSigned] : null,
      },
      { label: 'Signature date', done: !!p.signatureDate, value: shortDate(p.signatureDate) },
    ],
    [
      {
        label: 'Deposit',
        done: p.depositStatus === 'paid',
        value: p.depositStatus ? DEPOSIT_LABELS[p.depositStatus] : null,
      },
      { label: 'Payment date', done: !!p.paymentDate, value: shortDate(p.paymentDate) },
    ],
  ];
}

/**
 * One line per phase describing where it stands — what happened if it is done,
 * or what is still outstanding if it is not.
 *
 * Reads `phaseCompletion()` directly rather than re-deriving done-ness, so a
 * card can never show a completed check beside "Waiting on: signature date".
 */
export function phaseCaptions(p: LeadPipeline): string[] {
  const done = phaseCompletion(p);
  const lostAt = lostPhase(p);
  const reason = lostReason(p);

  const caption = (i: number): string => {
    if (lostAt === i + 1 && reason) {
      // On the card that recorded the drop-out, say WHY if we know, and say the
      // reason is outstanding if we do not — a card reading only "Not interested
      // after meeting" gives no hint that something is still owed.
      if (dropoutPhaseFor(p) === i + 1) {
        const why = dropoutReasonText(p);
        if (why) return `${reason} · ${why}`;
        return `${reason} · waiting on: a reason`;
      }
      return reason;
    }

    switch (i) {
      case 0:
        if (done[0]) {
          const d = shortDate(p.meetingDate);
          return d ? `Contacted · meeting ${d}` : 'Contacted · meeting scheduled';
        }
        if (p.outreachStatus === 'contacted') {
          return p.contactOutcome === 'follow_up'
            ? 'Waiting on: a follow-up date'
            : 'Waiting on: the result of contact';
        }
        if (p.outreachStatus === 'attempted_no_answer') return 'Waiting on: a reply';
        return 'Log your first outreach';
      case 1:
        if (done[1]) return 'Attended · still interested';
        if (p.meetingAttended === undefined) return 'Waiting on: did they show up?';
        return 'Waiting on: are they still interested?';
      case 2: {
        if (done[2]) {
          const d = shortDate(p.contractSentDate);
          return d ? `Contract sent ${d}` : 'Contract sent';
        }
        if (p.contractStatus === 'yes') return 'Waiting on: the date it was sent';
        return 'Waiting on: the contract';
      }
      case 3: {
        if (done[3]) {
          const d = shortDate(p.signatureDate);
          return d ? `Signed ${d}` : 'Signed';
        }
        if (p.contractSigned === 'yes') return 'Waiting on: the signature date';
        return 'Waiting on: their signature';
      }
      default: {
        if (done[4]) {
          const d = shortDate(p.paymentDate);
          return d ? `Deposit paid ${d}` : 'Deposit paid';
        }
        if (p.depositStatus === 'paid') return 'Waiting on: the payment date';
        return 'Waiting on: the deposit';
      }
    }
  };

  return PHASE_TITLES.map((_, i) => caption(i));
}

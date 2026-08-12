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

/** Menu order, so both phases present the options identically. */
export const DROPOUT_REASONS: DropoutReason[] = [
  'has_kids', 'not_within_budget', 'too_soon', 'other',
];

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

export function parsePipeline(raw?: string | null): LeadPipeline {
  if (!raw) return {};
  try {
    const parsed = JSON.parse(raw) as LeadPipeline;
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
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
  const p5 = p.depositPaid === true && !!p.paymentDate;
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
 * The phases at which the customer can tell us they are out, and therefore the
 * phases that ask for a reason. Both use the same question in different words:
 * "still interested?" → No, and "contract status" → No longer interested.
 *
 * `profile_rejected` is deliberately NOT here. That is us declining them, not
 * them declining us, and none of the shared reasons ("has kids", "not within
 * budget", "too soon") is something we would be reporting about our own
 * decision.
 */
export function dropoutPhaseFor(p: LeadPipeline): number | null {
  if (p.stillInterested === false) return 2;
  if (p.contractStatus === 'no_longer_interested') return 3;
  return null;
}

/** True when a drop-out has been recorded, so a reason is owed. */
export function isReasonRequired(p: LeadPipeline): boolean {
  return dropoutPhaseFor(p) !== null;
}

/**
 * True when the reason requirement is satisfied — including the free-text box,
 * which is mandatory in its own right once "Other" is chosen. An "Other" with
 * nothing typed is the exact hole this check exists to close.
 */
export function isReasonComplete(p: LeadPipeline): boolean {
  if (!isReasonRequired(p)) return true;
  if (!p.dropoutReason) return false;
  if (p.dropoutReason === 'other') return !!p.dropoutReasonOther?.trim();
  return true;
}

/** The recorded reason as one readable string, or null if none is on record. */
export function dropoutReasonText(p: LeadPipeline): string | null {
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
  /** Count per reason, plus how many drop-outs at this phase have no reason yet. */
  counts: Record<DropoutReason, number>;
  missing: number;
  total: number;
}

/**
 * Drop-off reasons broken down by the stage they were logged at — the report
 * the phase tag exists to make possible.
 *
 * `missing` is reported rather than hidden: a stage with fifteen drop-outs and
 * eleven reasons is a data-collection problem, and rounding it away would make
 * the chart look complete while the insight is still missing.
 */
export function dropoutReasonBreakdown(pipelines: LeadPipeline[]): ReasonByStageRow[] {
  const empty = (): Record<DropoutReason, number> =>
    ({ has_kids: 0, not_within_budget: 0, too_soon: 0, other: 0 });

  const rows = new Map<number, ReasonByStageRow>();
  for (const phase of [2, 3]) {
    rows.set(phase, {
      phase,
      phaseTitle: PHASE_TITLES[phase - 1] ?? `Phase ${phase}`,
      counts: empty(),
      missing: 0,
      total: 0,
    });
  }

  for (const p of pipelines) {
    const at = dropoutPhaseFor(p);
    if (at === null) continue;
    // Attribute to the tagged phase when one is recorded, else to the phase that
    // currently shows the drop-out. Old rows written before tagging existed have
    // no tag, and dropping them would understate every total.
    const key = p.dropoutReasonPhase ?? at;
    const row = rows.get(key) ?? rows.get(at)!;
    row.total += 1;
    if (isReasonComplete(p) && p.dropoutReason) row.counts[p.dropoutReason] += 1;
    else row.missing += 1;
  }

  return [...rows.values()];
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
      { label: 'Deposit', done: p.depositPaid === true, value: bool(p.depositPaid, 'Paid', 'Not paid') },
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
        if (p.depositPaid === true) return 'Waiting on: the payment date';
        return 'Waiting on: the deposit';
      }
    }
  };

  return PHASE_TITLES.map((_, i) => caption(i));
}

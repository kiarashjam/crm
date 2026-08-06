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
    if (lostAt === i + 1 && reason) return reason;

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

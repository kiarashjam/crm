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
 * A terminal "lost" reason, if the lead dropped out at any phase. These halt
 * forward progress regardless of which phase they occur in.
 */
export function lostReason(p: LeadPipeline): string | null {
  if (p.contactOutcome === 'not_interested') return 'Not interested';
  if (p.meetingAttended === false) return 'No-show at meeting';
  if (p.stillInterested === false) return 'Not interested after meeting';
  if (p.contractStatus === 'profile_rejected') return 'Profile rejected';
  if (p.contractStatus === 'no_longer_interested') return 'No longer interested';
  if (p.contractSigned === 'no') return 'Contract declined';
  return null;
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

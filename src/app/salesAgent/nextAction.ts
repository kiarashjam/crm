// Next action for a Cadence salesperson. No language model.
//
// The five lead phases live in leadPipeline.ts. This module only reads that
// state and names the single next thing a person should do. One suggestion per
// lead. Lost leads with a recorded reason produce nothing: the work is finished.

import {
  PHASE_TITLES,
  currentPhase,
  failurePointFor,
  isPipelineComplete,
  isReasonComplete,
  parsePipeline,
  type LeadPipeline,
} from '@/app/pages/leads/leadPipeline';
import { calendarKey, shiftKey, todayKey } from './calendarDay';

export type SuggestionKind =
  | 'record_reason'
  | 'start_outreach'
  | 'try_again'
  | 'record_outcome'
  | 'book_meeting'
  | 'add_meeting_date'
  | 'hold_meeting'
  | 'prepare_meeting'
  | 'log_attendance'
  | 'reschedule'
  | 'record_interest'
  | 'decide_contract'
  | 'send_contract'
  | 'record_sent_date'
  | 'ask_signature'
  | 'chase_signature'
  | 'record_signature_date'
  | 'ask_deposit'
  | 'record_payment_date'
  | 'convert';

export interface NextAction {
  leadId: string;
  leadName: string;
  phase: number;
  phaseTitle: string;
  kind: SuggestionKind;
  /** What to do, in one sentence. */
  action: string;
  /** Why this is the next step, from the phase rules. */
  because: string;
  /** Higher numbers should be done first. */
  priority: number;
}

export interface SuggestableLead {
  id: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  pipelineState?: string | null;
  isConverted?: boolean;
  assignedToId?: string | null;
  lastContactedAt?: string | null;
}

function actionFor(p: LeadPipeline, now: Date, lastContactedAt?: string | null): Omit<NextAction, 'leadId' | 'leadName'> | null {
  const failure = failurePointFor(p);
  if (failure && !isReasonComplete(p)) {
    const list = failure.kind === 'rejection' ? 'why the profile was rejected' : 'why they dropped out';
    return {
      phase: failure.phase,
      phaseTitle: PHASE_TITLES[failure.phase - 1] ?? 'Outreach',
      kind: 'record_reason',
      action: `Record ${list}.`,
      because: `${failure.label} is already on the lead, and the reason is still missing.`,
      priority: 100,
    };
  }
  if (failure) return null;
  if (isPipelineComplete(p)) {
    return {
      phase: 5,
      phaseTitle: 'Deposit',
      kind: 'convert',
      action: 'Convert this lead into a deal. The deposit is paid.',
      because: 'Outreach, meeting, contract, signature, and deposit are all complete.',
      priority: 90,
    };
  }

  const phase = currentPhase(p);
  const title = PHASE_TITLES[phase - 1] ?? 'Outreach';
  const today = todayKey(now);

  if (phase === 1) {
    if (!p.outreachStatus) {
      return { phase, phaseTitle: title, kind: 'start_outreach', action: 'Make the first call and log whether they answered.', because: 'Outreach has not started.', priority: 70 };
    }
    if (p.outreachStatus === 'attempted_no_answer') {
      const calledToday = calendarKey(lastContactedAt) === today;
      return calledToday
        ? { phase, phaseTitle: title, kind: 'try_again', action: 'Try once more later today, at a different time. They were already called today.', because: 'A no-answer is not a lost lead. A second try belongs in a different part of the day, not on top of work that is already due.', priority: 55 }
        : { phase, phaseTitle: title, kind: 'try_again', action: 'Call again at a different time of day. The last attempt had no answer.', because: 'A no-answer is not a lost lead. The next step is another attempt.', priority: 68 };
    }
    if (!p.contactOutcome) {
      return { phase, phaseTitle: title, kind: 'record_outcome', action: 'Record what they said: meeting, follow-up, or not interested.', because: 'They were contacted, and the outcome is not saved.', priority: 72 };
    }
    if (p.contactOutcome === 'follow_up') {
      return { phase, phaseTitle: title, kind: 'book_meeting', action: 'Book the meeting. They asked for a follow-up.', because: 'A follow-up without a date does not move the lead to Meeting.', priority: 74 };
    }
    if (p.contactOutcome === 'meeting_scheduled' && !p.meetingDate) {
      return { phase, phaseTitle: title, kind: 'add_meeting_date', action: 'Add the meeting date.', because: 'A meeting is scheduled, but the date is missing.', priority: 76 };
    }
  }

  if (phase === 2) {
    const meetingDay = calendarKey(p.meetingDate);
    if (meetingDay && p.meetingAttended === undefined && meetingDay >= today) {
      if (meetingDay === today) {
        return { phase, phaseTitle: title, kind: 'hold_meeting', action: 'The meeting is today. Hold it, then log whether they came.', because: 'A meeting dated today is still ahead of logging the outcome.', priority: 96 };
      }
      const soon = meetingDay === shiftKey(today, 1);
      return {
        phase, phaseTitle: title, kind: 'prepare_meeting',
        action: soon ? `Prepare for tomorrow's meeting on ${meetingDay}.` : `Prepare for the meeting on ${meetingDay}.`,
        because: soon ? 'The meeting is tomorrow.' : 'The meeting is still in the future.',
        priority: soon ? 66 : 40,
      };
    }
    if (p.meetingAttended === undefined) {
      return { phase, phaseTitle: title, kind: 'log_attendance', action: 'Log whether they came to the meeting.', because: 'The meeting date has passed, or no date was saved, and attendance is blank.', priority: 78 };
    }
    if (p.meetingAttended === false) {
      const stillIn = p.stillInterested === true;
      return {
        phase, phaseTitle: title, kind: 'reschedule',
        action: stillIn
          ? 'Book another meeting. They missed the last one and are still interested.'
          : 'Reschedule. A no-show is not a loss until they say they are out.',
        because: stillIn
          ? 'Attendance is still incomplete, and they have said they are interested.'
          : 'They missed the meeting and interest is not recorded.',
        priority: 77,
      };
    }
    if (p.stillInterested === undefined) {
      return { phase, phaseTitle: title, kind: 'record_interest', action: 'Record whether they are still interested after the meeting.', because: 'Attendance is logged and interest is not.', priority: 76 };
    }
  }

  if (phase === 3) {
    if (!p.contractStatus) {
      return { phase, phaseTitle: title, kind: 'decide_contract', action: 'Record the contract decision: send it, reject the profile, or mark them as no longer interested.', because: 'The meeting went well and the contract step is empty.', priority: 80 };
    }
    if (p.contractStatus === 'to_be_sent') {
      return { phase, phaseTitle: title, kind: 'send_contract', action: 'Send the contract.', because: 'The contract is marked to be sent and has not gone out.', priority: 84 };
    }
    if (p.contractStatus === 'yes' && !p.contractSentDate) {
      return { phase, phaseTitle: title, kind: 'record_sent_date', action: 'Record the date the contract was sent.', because: 'The contract is a yes, and the send date is missing.', priority: 82 };
    }
  }

  if (phase === 4) {
    if (!p.contractSigned || p.contractSigned === 'pending') {
      return {
        phase, phaseTitle: title,
        kind: p.contractSigned === 'pending' ? 'chase_signature' : 'ask_signature',
        action: p.contractSigned === 'pending' ? 'Follow up on the signature. It is still pending.' : 'Ask them to sign the contract.',
        because: 'The contract is out and it is not signed.',
        priority: 86,
      };
    }
    if (p.contractSigned === 'yes' && !p.signatureDate) {
      return { phase, phaseTitle: title, kind: 'record_signature_date', action: 'Record the signature date.', because: 'The contract is signed and the date is missing.', priority: 83 };
    }
  }

  if (phase === 5) {
    if (p.depositStatus !== 'paid') {
      return { phase, phaseTitle: title, kind: 'ask_deposit', action: 'Ask for the deposit.', because: 'The contract is signed and the deposit is not paid.', priority: 88 };
    }
    if (!p.paymentDate) {
      return { phase, phaseTitle: title, kind: 'record_payment_date', action: 'Record the payment date.', because: 'The deposit is paid and the date is missing.', priority: 85 };
    }
  }

  return null;
}

/** The one next step for this lead, or null when nothing is owed. */
export function nextActionForLead(lead: SuggestableLead, now: Date = new Date()): NextAction | null {
  if (!lead?.id || lead.isConverted) return null;
  const step = actionFor(parsePipeline(lead.pipelineState), now, lead.lastContactedAt);
  if (!step) return null;
  return { leadId: lead.id, leadName: lead.name?.trim() || 'Untitled lead', ...step };
}

/** The most urgent suggestions, one per lead, highest priority first. */
export function nextActions(leads: SuggestableLead[], now: Date = new Date(), limit = 5): NextAction[] {
  return leads
    .map((lead) => nextActionForLead(lead, now))
    .filter((item): item is NextAction => item !== null)
    .sort((a, b) => b.priority - a.priority || a.leadName.localeCompare(b.leadName))
    .slice(0, limit);
}

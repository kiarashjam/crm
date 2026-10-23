import type { SuggestionKind } from '../nextAction';

/**
 * The operating manual for one next step.
 * The agent quotes this when someone asks how to do the step. It does not invent a different process.
 */
export interface Playbook {
  /** The move, in the order a person should actually do it. */
  steps: string[];
  /** What to say, as a talk track, still filled by the sales writer rather than sent. */
  say: string;
  /** The mistake that loses the record. */
  avoid: string;
}

export const LEAD_PLAYBOOKS: Record<SuggestionKind, Playbook> = {
  record_reason: {
    steps: ['Open the lead.', 'Pick the drop-out or rejection reason from the agreed list.', 'If the reason is Other, write the real reason in the note.', 'Leave the lead. The work is finished once the reason is complete.'],
    say: 'We are closing this out so the reason is on the record. What should I put down?',
    avoid: 'Do not invent a reason to clear the queue. A blank reason is more honest than a guessed one.',
  },
  start_outreach: {
    steps: ['Confirm the name, email, and company are real.', 'Draft the first note from the sales writer.', 'Send it yourself, then log the date you actually sent it.', 'The next suggestion appears only after that date is saved.'],
    say: 'Short first note: who you are, why this person, one question, one time to talk.',
    avoid: 'Do not send a second first note on the same day. One clean outreach, then wait.',
  },
  try_again: {
    steps: ['Read the last outreach date.', 'Change the angle: a different proof, a shorter note, or a call.', 'Log the new attempt.', 'Stop after the pipeline says the attempt budget is spent, and record why.'],
    say: 'One new fact they did not have last time, then the same ask.',
    avoid: 'Do not resend the same paragraph. A repeat with no new fact trains them to ignore you.',
  },
  record_outcome: {
    steps: ['Open the outreach step.', 'Set the outcome to what actually happened.', 'If they dropped or were rejected, the reason step comes next and is mandatory.'],
    say: 'What happened on the last attempt: reply, no reply, wrong person, or not a fit?',
    avoid: 'Do not leave the outcome blank because you plan to remember it. The plan cannot see a memory.',
  },
  book_meeting: {
    steps: ['Offer two specific times.', 'When they accept, save the meeting date on the lead.', 'The phase does not move on a verbal yes. It moves on the saved date.'],
    say: 'Would Tuesday at 10 or Thursday at 15 work for twenty minutes?',
    avoid: 'Do not mark the meeting done because a calendar hold exists somewhere else. The date has to be on the lead.',
  },
  add_meeting_date: {
    steps: ['Find the agreed time in the thread.', 'Save that date on the Meeting phase.', 'Prepare only after the date is on the record.'],
    say: 'I have us down for [date]. I will send a short agenda.',
    avoid: 'Do not type "next week". The plan compares a real date with today.',
  },
  hold_meeting: {
    steps: ['The meeting date is today or in the past and attendance is not logged.', 'Meet, or reschedule if they did not show.', 'Log whether they attended before you write the contract.'],
    say: 'Confirming we are still on for the time already on the lead.',
    avoid: 'Do not skip ahead to a contract because the meeting was "basically fine". Attendance is its own field.',
  },
  prepare_meeting: {
    steps: ['Read the company and the last note.', 'Write a three-line agenda: their situation, the offer, the decision you want.', 'Hold the meeting on the saved date.'],
    say: 'Agenda: where you are now, what we would do, and whether a contract is the next step.',
    avoid: 'Do not walk in with a deck and no question. The meeting exists to learn if they will move.',
  },
  log_attendance: {
    steps: ['Set attended or not attended.', 'If they did not attend, reschedule or record why it stopped.', 'If they attended, the interest decision is the next field.'],
    say: 'Thanks for the time. I have noted that we met.',
    avoid: 'Do not log attended for a no-show. That opens the contract phase on a meeting that never happened.',
  },
  reschedule: {
    steps: ['Clear or replace the meeting date.', 'Offer two new times.', 'Save the new date before you prepare again.'],
    say: 'We missed each other. I can do [time] or [time].',
    avoid: 'Do not keep the old date and also book a new one. One date is the plan.',
  },
  record_interest: {
    steps: ['After a held meeting, set whether they want to go further.', 'Interested moves you to the contract decision.', 'Not interested requires the reason, then the lead goes quiet.'],
    say: 'From today, do you want a contract, or should we stop here?',
    avoid: 'Do not send a contract to someone whose interest is still blank.',
  },
  decide_contract: {
    steps: ['Decide if a contract is the right next paper.', 'Yes moves you to sending it.', 'No requires the reason.'],
    say: 'The next paper is the contract. Shall I send it, or is there a term we still have to settle?',
    avoid: 'Do not send terms you have not agreed. A contract is not a way to start the negotiation.',
  },
  send_contract: {
    steps: ['Draft the contract on the lead.', 'Read it once for the name, the price, and the dates.', 'Send it, then save the sent date.'],
    say: 'The contract is ready. I am sending it for signature today.',
    avoid: 'Do not edit the body after it has been sent. Void it and draft again if the terms change.',
  },
  record_sent_date: {
    steps: ['The contract went out.', 'Save the day it was sent.', 'The signature chase uses that date, not the day you meant to send it.'],
    say: 'Sent today. I will look for the signature.',
    avoid: 'Do not leave the sent date empty after a real send. The chase will start from the wrong day.',
  },
  ask_signature: {
    steps: ['Confirm they opened it.', 'Ask for the signature, with the sent date in the note.', 'When it is signed, save the signature date.'],
    say: 'The contract from [sent date] is ready for your signature. Reply if a line needs to change before you sign.',
    avoid: 'Do not offer new terms in the chase. Changes mean a new draft.',
  },
  chase_signature: {
    steps: ['Count the days since it was sent.', 'One chase, with the date, not a new offer.', 'If they refuse, record the reason instead of chasing forever.'],
    say: 'Checking on the contract sent on [date]. Happy to walk through any line before you sign.',
    avoid: 'Do not send a different price in the chase email. That is a new negotiation, and the open contract would be the wrong paper.',
  },
  record_signature_date: {
    steps: ['Save the day they signed.', 'The deposit ask is the next phase.', 'A signature with no date does not count as done.'],
    say: 'Thank you. I have the signature as of [date]. The deposit is the remaining step.',
    avoid: 'Do not mark signed because they said "we will". The date is the evidence.',
  },
  ask_deposit: {
    steps: ['Tell them the amount and where it goes.', 'When it arrives, set the deposit to paid and save the payment date.', 'Then convert the lead.'],
    say: 'The signature is in. The deposit is the last step before this becomes a deal.',
    avoid: 'Do not convert before the deposit status is paid. The pipeline treats those as different facts.',
  },
  record_payment_date: {
    steps: ['Confirm the money arrived.', 'Set deposit status to paid.', 'Save the payment date, then convert.'],
    say: 'Payment received on [date]. I am converting this lead into a deal.',
    avoid: 'Do not type paid and leave the date empty. The audit reads both.',
  },
  convert: {
    steps: ['Check signature and paid deposit are both saved.', 'Convert the lead to a deal.', 'The lead plan then stops. The deal plan starts at its stage.'],
    say: 'This is complete on the lead. I am opening it as a deal.',
    avoid: 'Do not convert a lead that is still missing a date. The deal would inherit a hole.',
  },
};

export const DEAL_PLAYBOOKS: Record<string, Playbook> = {
  Qualification: {
    steps: ['Name the buyer and the budget.', 'If they are a fit, move the stage to Proposal.', 'If they are not, close it lost and write the reason.'],
    say: 'Before a proposal: who decides, what it is worth, and whether we should write it.',
    avoid: 'Do not write a proposal for a deal that has no buyer.',
  },
  Proposal: {
    steps: ['Send the proposal or follow the one already out.', 'Log the activity on the deal.', 'Move to Negotiation only when they are discussing terms.'],
    say: 'The proposal is the offer. I can walk through it this week.',
    avoid: 'Do not skip to Contract Sent because you attached a PDF. The stage has to move.',
  },
  Negotiation: {
    steps: ['Write down the open terms.', 'Agree them.', 'Send the contract, which is its own stage.'],
    say: 'These are the open points. Once they are agreed I will send the contract.',
    avoid: 'Do not keep negotiating inside a contract that was already sent.',
  },
  'Contract Sent': {
    steps: ['Watch for their signature.', 'Countersign only after they have signed.', 'Move the stage when that is done.'],
    say: 'The contract is with you. Tell me which line to clarify, otherwise the next step is your signature.',
    avoid: 'Do not countersign your own draft before they sign. That paper is not executed.',
  },
  'Contract Signed': {
    steps: ['Confirm both signatures.', 'Move to Closed Won when the commercial close matches the paper.', 'If it falls through, close it lost with a reason.'],
    say: 'Both signatures are on it. I am marking the deal won.',
    avoid: 'Do not leave a signed deal sitting in Contract Signed with no close.',
  },
  'Closed Won': {
    steps: ['The deal is won.', 'Hand the next work to delivery.', 'Do not reopen it to chase a new signature.'],
    say: 'This one is closed won. Nothing further on the sale.',
    avoid: 'Do not keep it in the active queue.',
  },
  'Closed Lost': {
    steps: ['Make sure the lost reason is saved.', 'Stop selling this deal.', 'Use the reason in the weekly pattern, not as a script to re-open them the same week.'],
    say: 'We are closing this lost. The reason on the deal is the one we will learn from.',
    avoid: 'Do not delete the reason to make the report look cleaner.',
  },
};

export function playbookForKind(kind: SuggestionKind): Playbook {
  return LEAD_PLAYBOOKS[kind];
}

export function playbookForStage(stage: string): Playbook | null {
  return DEAL_PLAYBOOKS[stage] ?? null;
}

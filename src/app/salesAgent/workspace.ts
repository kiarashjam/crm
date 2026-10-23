// Next step across the lists a salesperson actually works.
//
// Leads use the five-phase rules in nextAction.ts. Deals, tasks, and sequence
// enrollments use the same idea: one suggestion per record, from the fields
// already saved. No language model, and nothing here writes.

import { duplicateCompanyGroups, duplicateContactGroups } from '@/app/api/dedupe';
import type { SequenceStepType } from '@/app/api/sequences';
import { calendarKey, daysBetween, todayKey } from './calendarDay';
import { nextActionForLead, type NextAction, type SuggestionKind, type SuggestableLead } from './nextAction';
import { dealBriefKind, writerBrief, type WriterBrief } from './writerBrief';

export interface WorkSuggestion {
  key: string;
  href: string;
  name: string;
  /** Which list the record lives on. */
  list: string;
  action: string;
  because: string;
  priority: number;
  /** Present when the step is a message the sales writer can draft. Nothing is sent. */
  brief?: WriterBrief;
  /** Lead suggestion kind, or deal stage name, so the playbook can explain the step. */
  guide?: string;
  /** Pending invitation, when this row can resend it. */
  inviteId?: string;
  /** Address a confirmed send can use. Absent when the record has no email. */
  email?: string | null;
  /** Set when exactly one duplicate group can be merged without guessing which to keep. */
  mergeKind?: 'contacts' | 'companies';
  mergePrimaryId?: string;
  mergeDuplicateIds?: string[];
}

export interface SuggestableDeal {
  id: string;
  name: string;
  stage?: string;
  dealStageName?: string;
  isWon?: boolean | null;
  expectedCloseDateUtc?: string | null;
  closedReason?: string | null;
  assigneeId?: string | null;
  lastActivityAtUtc?: string | null;
  /** Saved amount, when the deal carries one. The judge reads it and can move the row. */
  value?: string | null;
  probability?: number | null;
  companyId?: string | null;
  companyName?: string | null;
  contactId?: string | null;
  contactName?: string | null;
  pipelineId?: string | null;
  /** The next open stage on this deal's own pipeline, when the stage name is not a default. */
  nextStageName?: string | null;
  nextStageId?: string | null;
}

export interface SuggestableTask {
  id: string;
  title: string;
  status: string;
  priority?: string;
  dueDateUtc?: string | null;
  reminderDateUtc?: string | null;
  completed?: boolean;
  leadId?: string | null;
  dealId?: string | null;
  contactId?: string | null;
}

export interface SuggestableEnrollment {
  id: string;
  sequenceId: string;
  targetType: 'lead' | 'contact';
  targetId: string;
  targetName: string;
  status: string;
  currentStep: number;
  nextActionAtUtc?: string | null;
}

export interface SuggestableSequence {
  id: string;
  steps: { order: number; type: SequenceStepType | string; taskTitle?: string; subject?: string }[];
  name?: string;
}

const STALE_DAYS = 7;
const CLOSING_WINDOW_DAYS = 7;

const STAGE_STEPS: Record<string, { action: string; because: string; priority: number }> = {
  Qualification: {
    action: 'Confirm they are a fit, then move the deal to Proposal.',
    because: 'The deal is still in Qualification.',
    priority: 70,
  },
  Proposal: {
    action: 'Send the proposal, or follow up if it is already out.',
    because: 'The deal is in Proposal.',
    priority: 72,
  },
  Negotiation: {
    action: 'Agree the terms and send the contract.',
    because: 'The deal is in Negotiation.',
    priority: 82,
  },
  'Contract Sent': {
    action: 'Follow up on the signature.',
    because: 'The contract is out and the deal is not closed.',
    priority: 86,
  },
  'Contract Signed': {
    action: 'Mark the deal Closed Won once payment is in.',
    because: 'The contract is signed and the deal is still open.',
    priority: 88,
  },
};

/** A near date or a quiet week raises a stage step. It does not replace it. */
function raise(priority: number, closingSoon: boolean, quietFor: number | null): number {
  let next = priority;
  if (closingSoon) next = Math.max(next, 80);
  if (quietFor !== null && quietFor >= STALE_DAYS) next = Math.max(next, Math.min(85, next + 8));
  return next;
}

function evidence(base: string, notes: string[]): string {
  return notes.length === 0 ? base : `${base} ${notes.join(' ')}`;
}

function noteOwner(because: string, assigned: boolean): string {
  return assigned ? because : `${because} Nobody is assigned.`;
}

const LEAD_BRIEF: Partial<Record<SuggestionKind, 'outreach' | 'followup' | 'note' | 'close'>> = {
  start_outreach: 'outreach',
  try_again: 'followup',
  book_meeting: 'followup',
  prepare_meeting: 'note',
  hold_meeting: 'note',
  reschedule: 'followup',
  send_contract: 'close',
  ask_signature: 'close',
  chase_signature: 'followup',
  ask_deposit: 'close',
};

function withBrief(step: WorkSuggestion, kind: 'outreach' | 'followup' | 'note' | 'close' | 'proposal' | null): WorkSuggestion {
  if (!kind) return step;
  return { ...step, brief: writerBrief(kind, step.name, step.action, step.because) };
}

function stageName(deal: SuggestableDeal): string {
  return (deal.dealStageName || deal.stage || '').trim();
}

const STAGE_BY_KEY = new Map(Object.entries(STAGE_STEPS).map(([name, step]) => [name.toLowerCase(), step]));

function namedStage(stage: string): { action: string; because: string; priority: number } | undefined {
  return STAGE_BY_KEY.get(stage.toLowerCase());
}

function dealOutcome(deal: SuggestableDeal): 'won' | 'lost' | 'open' {
  if (deal.isWon === true) return 'won';
  if (deal.isWon === false) return 'lost';
  const stage = stageName(deal).toLowerCase();
  if (stage === 'closed won') return 'won';
  if (stage === 'closed lost') return 'lost';
  return 'open';
}

/** The one next step for this deal, or null when the deal is finished. */
export function nextActionForDeal(deal: SuggestableDeal, now: Date = new Date()): WorkSuggestion | null {
  const outcome = dealOutcome(deal);
  const name = deal.name || 'Untitled deal';
  const base = { key: `deal:${deal.id}`, href: `/deals/${deal.id}`, name, list: 'Deals', guide: stageName(deal) || undefined };

  if (outcome === 'won') return null;
  if (outcome === 'lost') {
    if (deal.closedReason?.trim()) return null;
    return {
      ...base,
      action: 'Record why this deal was lost.',
      because: 'The deal is closed lost, and the reason is still empty.',
      // Same rule as a lead's missing reason: bookkeeping, so below a meeting today.
      priority: 95,
    };
  }

  const today = todayKey(now);
  const close = calendarKey(deal.expectedCloseDateUtc);
  const stage = stageName(deal) || 'Open';
  const touched = calendarKey(deal.lastActivityAtUtc);
  const quietFor = touched ? daysBetween(touched, today) : null;
  const quiet = quietFor !== null && quietFor >= STALE_DAYS;
  const closingSoon = close !== null && daysBetween(today, close) >= 0 && daysBetween(today, close) <= CLOSING_WINDOW_DAYS;
  const owned = (because: string) => noteOwner(because, !!deal.assigneeId?.trim());

  if (close && close < today) {
    return {
      ...base,
      action: 'Update the expected close date, or close the deal.',
      because: owned(`The close date has passed and the deal is still in ${stage}.`),
      priority: 94,
    };
  }

  const named = namedStage(stage);
  if (!named) {
    if (!close) {
      return {
        ...base,
        action: quiet ? 'Add an expected close date and log a touch.' : 'Add an expected close date.',
        because: owned(quiet
          ? `Nothing has been recorded in ${quietFor} days, and there is no close date.`
          : 'An open deal with no date has no next checkpoint.'),
        priority: quiet ? 70 : 65,
      };
    }
    if (quiet) {
      return {
        ...base,
        action: 'Log a touch. Nothing has been recorded in the last 7 days.',
        because: owned(`The last activity was ${quietFor} days ago, and ${stage} has no named next step.`),
        priority: 67,
      };
    }
    const notes = closingSoon ? ['The expected close date is within the next seven days.'] : [];
    const next = deal.nextStageName?.trim();
    return {
      ...base,
      action: next
        ? `Move the deal from ${stage} to ${next}, or record why it is waiting.`
        : 'Move the deal to its next stage, or record why it is waiting.',
      because: owned(evidence(next ? `${stage} is followed by ${next} on its pipeline.` : `${stage} is an open stage with no named next step.`, notes)),
      priority: raise(next ? 68 : 64, closingSoon, quietFor),
    };
  }

  const notes: string[] = [];
  if (closingSoon) notes.push('The expected close date is within the next seven days.');
  if (quiet) notes.push(`Nothing has been recorded in ${quietFor} days.`);
  if (!close) notes.push('It also has no expected close date.');
  return {
    ...base,
    action: named.action,
    because: owned(evidence(named.because, notes)),
    priority: raise(named.priority, closingSoon, quietFor),
  };
}

/** The one next step for this task, or null when it is done or not due. */
export function nextActionForTask(task: SuggestableTask, now: Date = new Date()): WorkSuggestion | null {
  if (task.completed || task.status === 'completed' || task.status === 'cancelled') return null;
  const name = task.title || 'Untitled task';
  const base = { key: `task:${task.id}`, href: `/tasks/${task.id}`, name, list: 'Tasks' };
  const today = todayKey(now);
  const due = calendarKey(task.dueDateUtc);

  if (due && due < today) {
    return { ...base, action: 'Complete this task. It is past its due date.', because: 'The due date is before today and the task is still open.', priority: 93 };
  }
  if (due && due === today) {
    return { ...base, action: 'Do this task today.', because: 'It is due today.', priority: 73 };
  }
  const reminder = calendarKey(task.reminderDateUtc);
  if (reminder && reminder <= today && (!due || due > today)) {
    return { ...base, action: 'This reminder is due.', because: `The reminder date is ${reminder} and the task is still open.`, priority: 60 };
  }
  if (!due && (task.priority === 'high' || task.status === 'in_progress')) {
    return { ...base, action: 'Set a due date so this task cannot stall.', because: 'It is open and has no due date.', priority: 58 };
  }
  return null;
}

function enrollmentHref(enrollment: SuggestableEnrollment): string {
  return enrollment.targetType === 'contact'
    ? `/contacts/${enrollment.targetId}`
    : `/leads/${enrollment.targetId}`;
}

function stepSentence(step: SuggestableSequence['steps'][number] | undefined): string {
  if (!step) return 'Run the next sequence step. It is due.';
  if (step.type === 'email') return step.subject ? `Send the sequence email "${step.subject}".` : 'Send the sequence email.';
  if (step.type === 'call') return step.taskTitle ? `Make the sequence call: ${step.taskTitle}.` : 'Make the sequence call.';
  if (step.type === 'task') return step.taskTitle ? `Complete the sequence task: ${step.taskTitle}.` : 'Complete the sequence task.';
  if (step.type === 'wait') return 'The wait is over. Advance the enrollment.';
  return 'Run the next sequence step. It is due.';
}

/** The one next step for an enrollment whose step is due, or null otherwise. */
export function nextActionForEnrollment(
  enrollment: SuggestableEnrollment,
  sequence: SuggestableSequence | undefined,
  now: Date = new Date(),
): WorkSuggestion | null {
  if (enrollment.status !== 'active') return null;
  const due = calendarKey(enrollment.nextActionAtUtc);
  if (!due || due > todayKey(now)) return null;

  const steps = [...(sequence?.steps ?? [])].sort((a, b) => a.order - b.order);
  const step = steps.find((item) => item.order === enrollment.currentStep) ?? steps[enrollment.currentStep];
  const waiting = 'The scheduled step date has arrived.';

  const action = stepSentence(step);
  const message = step?.type === 'email' || step?.type === 'call' ? 'followup' as const : null;
  return withBrief({
    key: `enrollment:${enrollment.id}`,
    href: enrollmentHref(enrollment),
    name: enrollment.targetName || 'Sequence enrollment',
    list: 'Sequences',
    action,
    because: waiting,
    priority: 71,
  }, message);
}

export interface SuggestableContact {
  id: string;
  name: string;
  email?: string | null;
  phone?: string | null;
  companyId?: string | null;
  isArchived?: boolean;
  doNotContact?: boolean;
  jobTitle?: string | null;
  preferredContactMethod?: string | null;
  companyName?: string | null;
}

export interface SuggestableCompany {
  id: string;
  name: string;
}

export interface SuggestableAutomation {
  id: string;
  name: string;
  enabled: boolean;
  actions?: { id?: string }[];
}

/** A contact who cannot be reached. Archived contacts and do-not-contact stay quiet. */
export function nextActionForContact(contact: SuggestableContact): WorkSuggestion | null {
  if (!contact?.id || contact.isArchived || contact.doNotContact) return null;
  if (contact.email?.trim() || contact.phone?.trim()) return null;
  return {
    key: `contact:${contact.id}`,
    href: `/contacts/${contact.id}`,
    name: contact.name?.trim() || 'Untitled contact',
    list: 'Contacts',
    action: 'Add an email or a phone number.',
    because: [
      'This contact cannot be reached.',
      contact.jobTitle?.trim() ? `Job title: ${contact.jobTitle.trim()}.` : '',
      contact.preferredContactMethod?.trim() ? `Preferred method: ${contact.preferredContactMethod.trim()}.` : '',
    ].filter(Boolean).join(' '),
    priority: 64,
  };
}

/** A company with nobody on file. People already linked produce nothing. */
export function nextActionForCompany(company: SuggestableCompany, contactCount: number): WorkSuggestion | null {
  if (!company?.id || contactCount > 0) return null;
  return {
    key: `company:${company.id}`,
    href: `/companies/${company.id}`,
    name: company.name?.trim() || 'Untitled company',
    list: 'Companies',
    action: 'Add a contact. There is nobody to reach at this company.',
    because: 'The company has no people on file.',
    priority: 52,
  };
}

/** An automation that is switched on and has no action. A switched-off rule is a choice, not work. */
export function nextActionForAutomation(rule: SuggestableAutomation): WorkSuggestion | null {
  if (!rule?.id || !rule.enabled || (rule.actions?.length ?? 0) > 0) return null;
  return {
    key: `automation:${rule.id}`,
    href: '/automations',
    name: rule.name?.trim() || 'Untitled automation',
    list: 'Automations',
    action: 'Add an action, or turn this automation off.',
    because: 'It is switched on and has nothing to do.',
    priority: 56,
  };
}

function leadSuggestion(step: NextAction, lead: SuggestableLead): WorkSuggestion {
  const reachable = !!(lead.email?.trim() || lead.phone?.trim());
  const needsReach = step.kind === 'start_outreach' || step.kind === 'try_again';
  const blocked = !reachable && needsReach;
  const action = blocked
    ? 'Add a phone number or an email. There is no way to reach them.'
    : step.action;
  const context = [
    lead.source?.trim() ? `Source: ${lead.source.trim()}.` : '',
    lead.referredByContactName?.trim() ? `Referred by ${lead.referredByContactName.trim()}.` : '',
    lead.tags && lead.tags.length > 0 ? `Tags: ${lead.tags.slice(0, 3).join(', ')}.` : '',
    typeof lead.leadScore === 'number' ? `Lead score ${lead.leadScore}.` : '',
    lead.description?.trim() ? 'A description is saved on the lead.' : '',
  ].filter(Boolean);
  const because = blocked
    ? 'The next step is a call, and neither a phone number nor an email is saved.'
    : [step.because, ...context].join(' ');
  const canWrite = !blocked && !!lead.email?.trim();
  return withBrief({
    key: `lead:${step.leadId}`,
    href: `/leads/${step.leadId}`,
    name: step.leadName,
    list: step.phaseTitle,
    action,
    because: noteOwner(because, !!lead.assignedToId?.trim()),
    priority: step.priority,
    guide: step.kind,
    email: lead.email,
  }, canWrite ? LEAD_BRIEF[step.kind] ?? null : null);
}

function duplicateSuggestions(
  kind: 'contacts' | 'companies',
  groups: { records: { id: string; name?: string | null; email?: string | null; doNotContact?: boolean | null }[] }[],
): WorkSuggestion[] {
  const people = kind === 'contacts';
  const rows: WorkSuggestion[] = [];
  groups.slice(0, 5).forEach((group, index) => {
    const records = group.records.filter((record) => record.id);
    if (records.length < 2) return;
    const primary = records.find((record) => record.email?.trim() && !record.doNotContact)
      ?? records.find((record) => !record.doNotContact)
      ?? records.find((record) => record.email?.trim())
      ?? records[0];
    const rest = records.filter((record) => record.id !== primary.id);
    const kept = primary.name?.trim() || (people ? 'the contact with an email' : 'the first company');
    rows.push({
      key: index === 0 ? `duplicates:${kind}` : `duplicates:${kind}:${primary.id}`,
      href: '/duplicates',
      name: primary.name?.trim() || (people ? 'Duplicate contacts' : 'Duplicate companies'),
      list: 'Duplicates',
      action: `Merge ${rest.length} duplicate${rest.length === 1 ? '' : 's'} into ${kept}.`,
      because: people
        ? 'They share an email or a name. The record with an email is kept.'
        : 'They share a domain or a name. The first record in the group is kept.',
      priority: 62,
      mergeKind: kind,
      mergePrimaryId: primary.id,
      mergeDuplicateIds: rest.map((record) => record.id),
    });
  });
  return rows;
}

export interface WorkspaceInput {
  leads?: SuggestableLead[];
  deals?: SuggestableDeal[];
  tasks?: SuggestableTask[];
  enrollments?: SuggestableEnrollment[];
  sequences?: SuggestableSequence[];
  contacts?: SuggestableContact[];
  companies?: SuggestableCompany[];
  automations?: SuggestableAutomation[];
}

/** The most urgent suggestions across every working list. One row per record. */
export function collectWork(input: WorkspaceInput, now: Date = new Date(), limit = 5): WorkSuggestion[] {
  const sequences = new Map((input.sequences ?? []).map((sequence) => [sequence.id, sequence]));
  const items: WorkSuggestion[] = [];

  for (const lead of input.leads ?? []) {
    if (!lead?.id) continue;
    const step = nextActionForLead(lead, now);
    if (!step) continue;
    items.push(leadSuggestion(step, lead));
  }

  for (const deal of input.deals ?? []) {
    if (!deal?.id) continue;
    const step = nextActionForDeal(deal, now);
    if (step) items.push(withBrief(step, dealBriefKind(step.action)));
  }
  for (const task of input.tasks ?? []) {
    if (!task?.id) continue;
    const step = nextActionForTask(task, now);
    if (step) items.push(withBrief(step, /^(Complete|Do this task)/.test(step.action) ? 'followup' : null));
  }
  for (const enrollment of input.enrollments ?? []) {
    if (!enrollment?.id || !enrollment.targetId) continue;
    const step = nextActionForEnrollment(enrollment, sequences.get(enrollment.sequenceId), now);
    if (step) items.push(step);
  }

  const contacts = (input.contacts ?? []).filter((contact) => contact?.id);
  const peopleAt = new Map<string, number>();
  for (const contact of contacts) {
    if (!contact.isArchived && contact.companyId) {
      peopleAt.set(contact.companyId, (peopleAt.get(contact.companyId) ?? 0) + 1);
    }
    const step = nextActionForContact(contact);
    if (step) items.push(step);
  }
  for (const company of input.companies ?? []) {
    const step = nextActionForCompany(company, peopleAt.get(company.id) ?? 0);
    if (step) items.push(step);
  }
  for (const rule of input.automations ?? []) {
    const step = nextActionForAutomation(rule);
    if (step) items.push(step);
  }
  const contactGroups = duplicateContactGroups(contacts);
  const companyGroups = duplicateCompanyGroups(input.companies ?? []);
  for (const step of duplicateSuggestions('contacts', contactGroups)) items.push(step);
  for (const step of duplicateSuggestions('companies', companyGroups)) items.push(step);

  const bestByRecord = new Map<string, WorkSuggestion>();
  for (const item of items) {
    const bucket = /^\/(leads|contacts|deals|tasks|companies)\//.test(item.href) ? item.href : item.key;
    const current = bestByRecord.get(bucket);
    const leadWinsTie = item.priority === current?.priority && item.key.startsWith('lead:');
    if (!current || item.priority > current.priority || leadWinsTie) bestByRecord.set(bucket, item);
  }

  return [...bestByRecord.values()]
    .sort((a, b) => b.priority - a.priority || a.name.localeCompare(b.name))
    .slice(0, limit);
}

/** The focus list, plus how many suggestions sit beyond it. */
export function workQueue(input: WorkspaceInput, now: Date = new Date(), limit = 5): { items: WorkSuggestion[]; remaining: number } {
  const all = collectWork(input, now, Number.MAX_SAFE_INTEGER);
  const items = all.slice(0, limit);
  return { items, remaining: all.length - items.length };
}

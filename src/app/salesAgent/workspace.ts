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
}

export interface SuggestableTask {
  id: string;
  title: string;
  status: string;
  priority?: string;
  dueDateUtc?: string | null;
  completed?: boolean;
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

function namedStage(stage: string): { action: string; because: string; priority: number } | undefined {
  const key = stage.toLowerCase();
  return Object.entries(STAGE_STEPS).find(([name]) => name.toLowerCase() === key)?.[1];
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
  const base = { key: `deal:${deal.id}`, href: `/deals/${deal.id}`, name, list: 'Deals' };

  if (outcome === 'won') return null;
  if (outcome === 'lost') {
    if (deal.closedReason?.trim()) return null;
    return {
      ...base,
      action: 'Record why this deal was lost.',
      because: 'The deal is closed lost, and the reason is still empty.',
      priority: 99,
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
    return {
      ...base,
      action: 'Move the deal to its next stage, or record why it is waiting.',
      because: owned(evidence(`${stage} is an open stage with no named next step.`, notes)),
      priority: raise(64, closingSoon, quietFor),
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
    because: 'This contact cannot be reached.',
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
  const because = blocked
    ? 'The next step is a call, and neither a phone number nor an email is saved.'
    : step.because;
  const canWrite = !blocked && !!lead.email?.trim();
  return withBrief({
    key: `lead:${step.leadId}`,
    href: `/leads/${step.leadId}`,
    name: step.leadName,
    list: step.phaseTitle,
    action,
    because: noteOwner(because, !!lead.assignedToId?.trim()),
    priority: step.priority,
  }, canWrite ? LEAD_BRIEF[step.kind] ?? null : null);
}

function duplicateSuggestion(kind: 'contacts' | 'companies', groups: number): WorkSuggestion | null {
  if (groups < 1) return null;
  const people = kind === 'contacts';
  return {
    key: `duplicates:${kind}`,
    href: '/duplicates',
    name: people ? 'Duplicate contacts' : 'Duplicate companies',
    list: 'Duplicates',
    action: `Merge ${groups} duplicate ${people ? 'contact' : 'company'} group${groups === 1 ? '' : 's'}.`,
    because: people ? 'They share an email or a name.' : 'They share a domain or a name.',
    priority: 62,
  };
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
  const contactDupes = duplicateSuggestion('contacts', duplicateContactGroups(contacts).length);
  const companyDupes = duplicateSuggestion('companies', duplicateCompanyGroups(input.companies ?? []).length);
  if (contactDupes) items.push(contactDupes);
  if (companyDupes) items.push(companyDupes);

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

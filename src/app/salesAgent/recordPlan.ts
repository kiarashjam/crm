// The full path for the record that is open, not only the first sentence.
//
// Leads use the five phases. Deals use the stage ladder. Tasks, contacts, and
// companies have a single step, so that step is the whole plan.

import { PHASE_TITLES, currentPhase, failurePointFor, isReasonComplete, parsePipeline } from '@/app/pages/leads/leadPipeline';
import { nextActionForLead, type SuggestableLead } from './nextAction';
import { nextActionForDeal, type SuggestableDeal, type SuggestableCompany, type SuggestableContact, type SuggestableTask, nextActionForCompany, nextActionForContact, nextActionForTask } from './workspace';

export interface PlanLine {
  title: string;
  state: 'done' | 'now' | 'later' | 'stopped';
  detail?: string;
}

export interface RecordPlan {
  name: string;
  lines: PlanLine[];
}

const DEAL_LADDER = ['Qualification', 'Proposal', 'Negotiation', 'Contract Sent', 'Contract Signed', 'Closed Won'] as const;

function sentence(action?: string, because?: string): string | undefined {
  const text = `${action ?? ''} ${because ?? ''}`.replace(/\s+/g, ' ').trim();
  return text || undefined;
}

export function leadPlan(lead: SuggestableLead, now: Date = new Date()): RecordPlan {
  const name = lead.name?.trim() || 'Untitled lead';
  const pipeline = parsePipeline(lead.pipelineState);
  const failure = failurePointFor(pipeline);
  const finished = !!failure && isReasonComplete(pipeline);
  const step = nextActionForLead(lead, now);
  const phase = lead.isConverted ? PHASE_TITLES.length + 1 : (failure?.phase ?? step?.phase ?? currentPhase(pipeline));

  const lines: PlanLine[] = PHASE_TITLES.map((title, index) => {
    const number = index + 1;
    if (lead.isConverted || number < phase) return { title, state: 'done' };
    if (finished && number === failure.phase) {
      return { title, state: 'stopped', detail: 'Finished. The reason is recorded.' };
    }
    if (finished && number > failure.phase) return { title, state: 'later' };
    if (number === phase) {
      return {
        title,
        state: failure && !isReasonComplete(pipeline) ? 'stopped' : 'now',
        detail: sentence(step?.action, step?.because),
      };
    }
    return { title, state: 'later' };
  });

  if (lead.isConverted) {
    lines.push({ title: 'Deal', state: 'done', detail: 'This lead is already a deal.' });
  }
  return { name, lines };
}

function dealStageIndex(name: string): number {
  const needle = name.trim().toLowerCase();
  return DEAL_LADDER.findIndex((stage) => stage.toLowerCase() === needle);
}

export function dealPlan(deal: SuggestableDeal, now: Date = new Date()): RecordPlan {
  const name = deal.name?.trim() || 'Untitled deal';
  const step = nextActionForDeal(deal, now);
  const won = deal.isWon === true || (deal.dealStageName || deal.stage || '').trim().toLowerCase() === 'closed won';
  const lost = (deal.dealStageName || deal.stage || '').trim().toLowerCase() === 'closed lost';
  const index = dealStageIndex(deal.dealStageName || deal.stage || '');

  const lines: PlanLine[] = DEAL_LADDER.map((title, stageIndex) => {
    if (won) return { title, state: 'done' as const };
    if (lost) {
      return {
        title,
        state: 'later' as const,
      };
    }
    if (index === -1) return { title, state: 'later' as const };
    if (stageIndex < index) return { title, state: 'done' as const };
    if (stageIndex === index) return { title, state: 'now' as const, detail: sentence(step?.action, step?.because) };
    return { title, state: 'later' as const };
  });

  if (lost) {
    return {
      name,
      lines: [
        {
          title: 'Closed Lost',
          state: 'stopped',
          detail: deal.closedReason?.trim()
            ? 'Finished. The reason is recorded.'
            : sentence(step?.action, step?.because),
        },
        ...lines.map((line) => ({ ...line, state: 'later' as const, detail: undefined })),
      ],
    };
  }

  if (won) {
    return { name, lines: [...lines, { title: 'Closed', state: 'done', detail: 'This deal is won.' }] };
  }

  if (index === -1) {
    return {
      name,
      lines: [
        { title: (deal.dealStageName || deal.stage || 'Open').trim() || 'Open', state: 'now', detail: sentence(step?.action, step?.because) },
        ...lines,
      ],
    };
  }

  return { name, lines };
}

export function taskPlan(task: SuggestableTask, now: Date = new Date()): RecordPlan {
  const name = task.title?.trim() || 'Untitled task';
  const step = nextActionForTask(task, now);
  if (!step) return { name, lines: [{ title: 'Task', state: 'done', detail: 'This task is finished.' }] };
  return { name, lines: [{ title: 'Task', state: 'now', detail: sentence(step.action, step.because) }] };
}

export function contactPlan(contact: SuggestableContact): RecordPlan {
  const name = contact.name?.trim() || 'Untitled contact';
  const step = nextActionForContact(contact);
  if (contact.isArchived || contact.doNotContact) {
    return { name, lines: [{ title: 'Contact', state: 'stopped', detail: contact.doNotContact ? 'Do not contact.' : 'This contact is archived.' }] };
  }
  if (!step) return { name, lines: [{ title: 'Contact', state: 'done', detail: 'There is a way to reach them.' }] };
  return { name, lines: [{ title: 'Contact', state: 'now', detail: sentence(step.action, step.because) }] };
}

export function companyPlan(company: SuggestableCompany, contactCount: number): RecordPlan {
  const name = company.name?.trim() || 'Untitled company';
  const step = nextActionForCompany(company, contactCount);
  if (!step) return { name, lines: [{ title: 'Company', state: 'done', detail: 'Someone at this company is on file.' }] };
  return { name, lines: [{ title: 'Company', state: 'now', detail: sentence(step.action, step.because) }] };
}

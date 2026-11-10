// The signed-in rep's open work, from GET /api/work-queue: their tasks, leads and deals
// that still owe a step, or with the team switch everyone's. The server decides whose
// work a record is (given to them, or made by them and given to nobody) and leaves out
// what is finished; the assistant's rules decide each record's next step from it.

import type { Deal, Lead, TaskItem } from './types';
import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';
import { getDeals, mapDeal, type DealRaw } from './deals';
import { getLeads, mapLead, type LeadRaw } from './leads';
import { getTasks, isMineInDemo, mapTask, type TaskRaw } from './tasks';
import type { WorkScope } from '@/app/lib/workScope';

export interface WorkQueue {
  scope: WorkScope;
  tasks: TaskItem[];
  leads: Lead[];
  deals: Deal[];
  /** A list held more than was sent: the most pressing came first. */
  truncated: boolean;
}

interface WorkQueueRaw {
  scope?: string;
  tasks?: TaskRaw[];
  leads?: LeadRaw[];
  deals?: DealRaw[];
  truncated?: boolean;
}

const open = (task: TaskItem) => !task.completed && task.status !== 'completed' && task.status !== 'cancelled';
const owesStep = (deal: Deal) => deal.isWon == null || (deal.isWon === false && !deal.closedReason?.trim());

export async function getWorkQueue(scope: WorkScope, req?: RequestOptions): Promise<WorkQueue> {
  if (!servesDemoData()) {
    const raw = await authFetchJson<WorkQueueRaw>(`/api/work-queue?scope=${scope}`, req);
    return {
      scope: raw?.scope === 'team' ? 'team' : 'mine',
      tasks: (raw?.tasks ?? []).map(mapTask),
      leads: (raw?.leads ?? []).map(mapLead),
      deals: (raw?.deals ?? []).map(mapDeal),
      truncated: raw?.truncated === true,
    };
  }
  // Demo mode, from the browser's own lists, by the same rule. The demo user made every
  // record there, so one given to nobody is theirs.
  const [tasks, leads, deals] = await Promise.all([getTasks(), getLeads(), getDeals()]);
  const mine = scope === 'mine';
  return {
    scope,
    tasks: tasks.filter((t) => open(t) && (!mine || isMineInDemo(t))),
    leads: leads.filter((l) => !l.isConverted && (!mine || isMineInDemo({ assigneeId: l.assignedToId }))),
    deals: deals.filter((d) => owesStep(d) && (!mine || isMineInDemo({ assigneeId: d.assigneeId }))),
    truncated: false,
  };
}

import type { DashboardStats } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Get dashboard stats (active leads, active deals, pipeline value, won/lost). */
export async function getDashboardStats(): Promise<DashboardStats> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ activeLeadsCount: number; activeDealsCount: number; pipelineValue: number; dealsWonCount: number; dealsLostCount: number }>('/api/reporting/dashboard');
    return res ?? { activeLeadsCount: 0, activeDealsCount: 0, pipelineValue: 0, dealsWonCount: 0, dealsLostCount: 0 };
  }
  await delay(150);
  return { activeLeadsCount: 0, activeDealsCount: 0, pipelineValue: 0, dealsWonCount: 0, dealsLostCount: 0 };
}

export interface PipelineStageValue {
  stageId: string;
  stageName: string;
  dealCount: number;
  value: number;
}

/** Get pipeline value grouped by stage (open deals only). */
export async function getPipelineValueByStage(): Promise<PipelineStageValue[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<PipelineStageValue[]>('/api/reporting/pipeline-by-stage');
    return Array.isArray(list) ? list : [];
  }
  await delay(150);
  return [];
}

export interface PipelineValueByAssignee {
  assigneeUserId: string;
  assigneeName: string;
  dealCount: number;
  value: number;
}

/** Get pipeline value grouped by assignee (open deals only). */
export async function getPipelineValueByAssignee(): Promise<PipelineValueByAssignee[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<PipelineValueByAssignee[]>('/api/reporting/pipeline-by-assignee');
    return Array.isArray(list) ? list : [];
  }
  await delay(150);
  return [];
}

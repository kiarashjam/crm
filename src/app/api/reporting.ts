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

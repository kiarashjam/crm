// Dashboard figures.
//
// In demo mode these used to return hardcoded zeros, so the Dashboard showed
// "0 leads, $0 pipeline" while the Leads page listed five leads and Reports
// showed a six-figure pipeline. A demo that contradicts itself teaches people not
// to trust the numbers. They are now computed from the same mock records every
// other page reads.

import type { CurrencyTotal, DashboardStats } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';
import { getDeals } from './deals';
import { getLeads } from './leads';
import { currencyOf, tryParseAmount } from '@/app/lib/money';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const EMPTY_STATS: DashboardStats = {
  activeLeadsCount: 0,
  activeDealsCount: 0,
  pipelineValue: 0,
  pipelineCurrency: 'USD',
  pipelineByCurrency: [],
  unreadableValueCount: 0,
  dealsWonCount: 0,
  dealsLostCount: 0,
};

/** An open deal is one that has neither been won nor lost. */
function isOpen(d: { isWon?: boolean }): boolean {
  return d.isWon === null || d.isWon === undefined;
}

/**
 * Totals per currency, largest first, plus how many values could not be read.
 *
 * The same rule as `ReportingService.TotalsByCurrency` on the server: nothing is
 * converted, ties break on the currency code so the dominant currency cannot flip
 * between renders, and an unreadable value is counted as unreadable rather than
 * silently as zero.
 */
function totalsByCurrency(
  deals: { value?: string; currency?: string }[],
): { totals: CurrencyTotal[]; unreadable: number } {
  const sums = new Map<string, { value: number; dealCount: number }>();
  let unreadable = 0;
  for (const d of deals) {
    const amount = tryParseAmount(d.value);
    if (amount === null) {
      unreadable += 1;
      continue;
    }
    const currency = currencyOf(d);
    const prev = sums.get(currency) ?? { value: 0, dealCount: 0 };
    sums.set(currency, { value: prev.value + amount, dealCount: prev.dealCount + 1 });
  }
  const totals = [...sums.entries()]
    .map(([currency, v]) => ({ currency, value: v.value, dealCount: v.dealCount }))
    .sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
  return { totals, unreadable };
}

/** Get dashboard stats (active leads, active deals, pipeline value, won/lost). */
export async function getDashboardStats(): Promise<DashboardStats> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<DashboardStats>('/api/reporting/dashboard');
    if (!res) return EMPTY_STATS;
    // The server is the authority, but a field an older deployment does not send
    // must not reach a headline figure as `undefined`.
    return {
      ...EMPTY_STATS,
      ...res,
      pipelineByCurrency: Array.isArray(res.pipelineByCurrency) ? res.pipelineByCurrency : [],
    };
  }
  await delay(150);
  const [deals, leads] = await Promise.all([getDeals(), getLeads()]);
  const dealList = Array.isArray(deals) ? deals : [];
  const open = dealList.filter(isOpen);
  const { totals, unreadable } = totalsByCurrency(open);
  const dominant = totals[0];
  return {
    activeLeadsCount: (Array.isArray(leads) ? leads : []).filter((l) => !l.isConverted).length,
    activeDealsCount: open.length,
    pipelineValue: dominant?.value ?? 0,
    pipelineCurrency: dominant?.currency ?? 'USD',
    pipelineByCurrency: totals,
    unreadableValueCount: unreadable,
    dealsWonCount: dealList.filter((d) => d.isWon === true).length,
    dealsLostCount: dealList.filter((d) => d.isWon === false).length,
  };
}

export interface PipelineStageValue {
  stageId: string;
  stageName: string;
  /** Part of the grouping, not a display detail: one row per stage per currency. */
  currency: string;
  dealCount: number;
  value: number;
}

/** Get pipeline value grouped by stage and currency (open deals only). */
export async function getPipelineValueByStage(): Promise<PipelineStageValue[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<PipelineStageValue[]>('/api/reporting/pipeline-by-stage');
    return Array.isArray(list) ? list.map((r) => ({ ...r, currency: r.currency || 'USD' })) : [];
  }
  await delay(150);
  const deals = await getDeals();
  const open = (Array.isArray(deals) ? deals : []).filter(isOpen);
  const groups = new Map<string, PipelineStageValue>();
  for (const d of open) {
    const amount = tryParseAmount(d.value);
    if (amount === null) continue;
    const stageName = d.dealStageName || d.stage || 'Unset';
    const currency = currencyOf(d);
    const key = `${stageName}|${currency}`;
    const prev = groups.get(key);
    if (prev) {
      prev.dealCount += 1;
      prev.value += amount;
    } else {
      groups.set(key, { stageId: key, stageName, currency, dealCount: 1, value: amount });
    }
  }
  return [...groups.values()].sort(
    (a, b) => a.stageName.localeCompare(b.stageName) || a.currency.localeCompare(b.currency),
  );
}

export interface PipelineValueByAssignee {
  assigneeUserId: string;
  assigneeName: string;
  currency: string;
  dealCount: number;
  value: number;
}

/** Get pipeline value grouped by assignee and currency (open deals only). */
export async function getPipelineValueByAssignee(): Promise<PipelineValueByAssignee[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<PipelineValueByAssignee[]>('/api/reporting/pipeline-by-assignee');
    return Array.isArray(list) ? list.map((r) => ({ ...r, currency: r.currency || 'USD' })) : [];
  }
  await delay(150);
  const deals = await getDeals();
  const open = (Array.isArray(deals) ? deals : []).filter(isOpen);
  const groups = new Map<string, PipelineValueByAssignee>();
  for (const d of open) {
    const amount = tryParseAmount(d.value);
    if (amount === null) continue;
    const assigneeName = d.assigneeName || 'Unassigned';
    const currency = currencyOf(d);
    const key = `${assigneeName}|${currency}`;
    const prev = groups.get(key);
    if (prev) {
      prev.dealCount += 1;
      prev.value += amount;
    } else {
      groups.set(key, {
        assigneeUserId: d.assigneeId || '',
        assigneeName,
        currency,
        dealCount: 1,
        value: amount,
      });
    }
  }
  return [...groups.values()].sort(
    (a, b) => a.assigneeName.localeCompare(b.assigneeName) || a.currency.localeCompare(b.currency),
  );
}

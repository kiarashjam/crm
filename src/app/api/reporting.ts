// Dashboard figures.
//
// In demo mode these used to return hardcoded zeros, so the Dashboard showed
// "0 leads, $0 pipeline" while the Leads page listed five leads and Reports
// showed a six-figure pipeline. A demo that contradicts itself teaches people not
// to trust the numbers. They are now computed from the same mock records every
// other page reads.

import type { CurrencyTotal, DashboardStats } from './types';
import { servesDemoData, authFetchJson } from './apiClient';
import { getDeals } from './deals';
import { getLeads } from './leads';
import { amountOf, currencyOf, DEFAULT_CURRENCY } from '@/app/lib/money';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const EMPTY_STATS: DashboardStats = {
  activeLeadsCount: 0,
  activeDealsCount: 0,
  pipelineValue: 0,
  pipelineCurrency: DEFAULT_CURRENCY,
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
  deals: { amount?: number | null; currency?: string }[],
): { totals: CurrencyTotal[]; unreadable: number } {
  const sums = new Map<string, { value: number; dealCount: number }>();
  let unreadable = 0;
  for (const d of deals) {
    const amount = amountOf(d);
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
  if (!servesDemoData()) {
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
    pipelineCurrency: dominant?.currency ?? DEFAULT_CURRENCY,
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
  if (!servesDemoData()) {
    const list = await authFetchJson<PipelineStageValue[]>('/api/reporting/pipeline-by-stage');
    return Array.isArray(list) ? list.map((r) => ({ ...r, currency: r.currency || DEFAULT_CURRENCY })) : [];
  }
  await delay(150);
  const deals = await getDeals();
  const open = (Array.isArray(deals) ? deals : []).filter(isOpen);
  const groups = new Map<string, PipelineStageValue>();
  for (const d of open) {
    const amount = amountOf(d);
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
  if (!servesDemoData()) {
    const list = await authFetchJson<PipelineValueByAssignee[]>('/api/reporting/pipeline-by-assignee');
    return Array.isArray(list) ? list.map((r) => ({ ...r, currency: r.currency || DEFAULT_CURRENCY })) : [];
  }
  await delay(150);
  const deals = await getDeals();
  const open = (Array.isArray(deals) ? deals : []).filter(isOpen);
  const groups = new Map<string, PipelineValueByAssignee>();
  for (const d of open) {
    const amount = amountOf(d);
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


// ── The Reports page ─────────────────────────────────────────────────────────
//
// The Reports page used to download every deal, lead and activity and work its
// figures out here, with its own money parser and its own idea of an active
// lead, so it disagreed with the Dashboard and grew slower with every year of
// history. The server now computes them for a period, a scope, a pipeline and a
// currency, and this module only asks. Demo mode has no server, so it computes
// the same shapes from the sample records (reportingDemo.ts).

/** Whose records a report covers. */
export type ReportScope = 'me' | 'team' | 'org';

export interface ReportFilters {
  /** Start of the period, an ISO instant: a midnight in the workspace's time zone. */
  from: string;
  /** End of the period, exclusive. */
  to: string;
  scope: ReportScope;
  /** With scope "team": which team. Omitted means the viewer's own. */
  teamId?: string;
  /** Deals in this pipeline only. Leads and activities belong to no pipeline. */
  pipelineId?: string;
  /** Deals in this currency only. */
  currency?: string;
  /** IANA time zone the charts count days in. Left out, the server uses the workspace's. */
  timeZone?: string;
}

/** Deals in one currency. `unreadable` are counted in `count` but not in `value`. */
export interface MoneyRow { currency: string; count: number; value: number; unreadable: number }
/** A set of deals: the count across currencies, the money per currency. */
export interface DealFigures { count: number; byCurrency: MoneyRow[] }
/** Weighted value of open deals in one currency, from those with a probability. */
export interface WeightedRow { currency: string; value: number; covered: number; missing: number }
export interface StageRow {
  pipelineId: string | null;
  pipelineName: string;
  stageKey: string;
  stageName: string;
  displayOrder: number;
  currency: string;
  count: number;
  value: number;
  weighted: number;
  unreadable: number;
}
export interface OwnerRow {
  ownerId: string | null;
  ownerName: string;
  currency: string;
  openCount: number;
  openValue: number;
  weighted: number;
  wonCount: number;
  wonValue: number;
  lostCount: number;
}
export interface CountRow { key: string; count: number }
/** Leads created in the period with this status today, and how many were converted. */
export interface LeadStatusRow { status: string; count: number; converted: number }
/** One bucket of a chart: `start` is its first day, yyyy-MM-dd in the report's time zone. */
export interface SeriesPoint { start: string; count: number }
export interface LeadFigures {
  /** Not yet converted, now. */
  active: number;
  /** Created in the period. */
  created: number;
  /** Of those, converted since. */
  createdConverted: number;
  /** Converted in the period, whenever created. */
  converted: number;
  convertedToDeal: number;
  scored: number;
  averageScore: number | null;
  byStatus: LeadStatusRow[];
  bySource: CountRow[];
  createdSeries: SeriesPoint[];
}
export interface ActivityFigures { total: number; byType: CountRow[]; series: SeriesPoint[] }
export interface ReportPeriod {
  fromUtc: string;
  toUtc: string;
  scope: ReportScope;
  teamId: string | null;
  teamName: string | null;
  pipelineId: string | null;
  currency: string | null;
  timeZone: string;
  /** How the charts are grouped, chosen by the server from the period's length. */
  bucket: 'day' | 'week' | 'month';
}
export interface ReportSummary {
  period: ReportPeriod;
  /** Every currency the scope's deals are in, whatever the currency filter. */
  currencies: string[];
  /** The filter's currency, or the one holding most of the OPEN pipeline. */
  headlineCurrency: string | null;
  /** Open now, whatever the period. */
  open: DealFigures;
  weighted: WeightedRow[];
  /** Open deals expected to close in the period. */
  closingInPeriod: DealFigures;
  created: DealFigures;
  won: DealFigures;
  lost: DealFigures;
  /** Won ÷ (won + lost) among deals closed in the period; null when none closed. */
  winRate: number | null;
  averageSalesCycleDays: number | null;
  byStage: StageRow[];
  byOwner: OwnerRow[];
  leads: LeadFigures;
  activities: ActivityFigures;
}

function query(params: Record<string, string | number | undefined | null>): string {
  const q = new URLSearchParams();
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null && v !== '') q.set(k, String(v));
  }
  const s = q.toString();
  return s ? `?${s}` : '';
}

/** Every figure on the Reports page for these filters. Throws when the server refuses. */
export async function getReportSummary(filters: ReportFilters): Promise<ReportSummary> {
  if (servesDemoData()) {
    const { demoReportSummary } = await import('./reportingDemo');
    return demoReportSummary(filters);
  }
  const res = await authFetchJson<ReportSummary>(`/api/reporting/summary${query({
    from: filters.from,
    to: filters.to,
    scope: filters.scope,
    teamId: filters.scope === 'team' ? filters.teamId : undefined,
    pipelineId: filters.pipelineId,
    currency: filters.currency,
    tz: filters.timeZone,
  })}`);
  if (!res) throw new Error('The reports could not be loaded.');
  return res;
}

/** Open deals expected to close in one month, in one currency, beside what was won that month. */
export interface ForecastMonthRow {
  /** yyyy-MM */
  month: string;
  currency: string;
  count: number;
  value: number;
  weighted: number;
  covered: number;
  missing: number;
  unreadable: number;
  wonCount: number;
  wonValue: number;
}
export interface Forecast {
  firstMonth: string;
  months: number;
  scope: ReportScope;
  teamId: string | null;
  rows: ForecastMonthRow[];
  /** Open deals past their expected close date. */
  overdue: MoneyRow[];
  /** Open deals with no expected close date, so in no month. */
  noCloseDate: MoneyRow[];
}

/** The forecast by expected close month, from `firstMonth` (yyyy-MM) for `months` months. */
export async function getForecast(
  filters: Pick<ReportFilters, 'scope' | 'teamId' | 'pipelineId' | 'currency'> & { firstMonth: string; months: number },
): Promise<Forecast> {
  if (servesDemoData()) {
    const { demoForecast } = await import('./reportingDemo');
    return demoForecast(filters);
  }
  const res = await authFetchJson<Forecast>(`/api/reporting/forecast${query({
    from: filters.firstMonth,
    months: filters.months,
    scope: filters.scope,
    teamId: filters.scope === 'team' ? filters.teamId : undefined,
    pipelineId: filters.pipelineId,
    currency: filters.currency,
  })}`);
  if (!res) throw new Error('The forecast could not be loaded.');
  return res;
}

/** One line of a saved forecast: an owner's deals expected that month, in one currency, as they stood. */
export interface ForecastSnapshotRow {
  id: string;
  /** "2026-10" */
  month: string;
  currency: string;
  assigneeName: string;
  weightedValue: number;
  openValue: number;
  /** Deals with a probability, and so in the weighted total. */
  covered: number;
  missing: number;
  savedAtUtc: string;
  snapshotId: string;
  /** False for saves made before forecasts went by close date: those counted every open deal. */
  byCloseDate: boolean;
  /** Open deals expected before the month, not in the figures above. */
  overdueCount: number;
  overdueValue: number;
  noCloseDateCount: number;
  unreadableCount: number;
}

/** One save of a month's forecast. Saves are kept; saving again adds another. */
export interface ForecastSave { snapshotId: string; savedAtUtc: string; savedByName: string; byCloseDate: boolean }
/** What an owner won in the month, in one currency. */
export interface ForecastActual { assigneeName: string; currency: string; wonCount: number; wonValue: number }
export interface ForecastSnapshotSet {
  month: string;
  /** Newest first. */
  saves: ForecastSave[];
  /** The save `rows` belong to. */
  snapshotId: string | null;
  rows: ForecastSnapshotRow[];
  actual: ForecastActual[];
}

const EMPTY_SET = (month: string): ForecastSnapshotSet => ({ month, saves: [], snapshotId: null, rows: [], actual: [] });

/**
 * Save a month's forecast on the server, so later edits to deals don't rewrite it.
 * Earlier saves for the month are kept. In an organization only the owner or a
 * manager may: a saved forecast counts private deals. The month is the viewer's
 * own, which can differ from UTC's around midnight on the 1st.
 */
export async function saveForecastSnapshot(year: number, month: number): Promise<ForecastSnapshotSet> {
  const res = await authFetchJson<ForecastSnapshotSet>(
    `/api/reporting/forecast-snapshot${query({ year, month })}`, { method: 'POST' });
  return res ?? EMPTY_SET(`${year}-${String(month).padStart(2, '0')}`);
}

/** A month's saves, the lines of one (the latest unless another is named), and what was won. */
export async function getForecastSnapshots(year: number, month: number, snapshotId?: string): Promise<ForecastSnapshotSet> {
  const res = await authFetchJson<ForecastSnapshotSet>(
    `/api/reporting/forecast-snapshots${query({ year, month, snapshotId })}`);
  return res ?? EMPTY_SET(`${year}-${String(month).padStart(2, '0')}`);
}

/** A sales team in the workspace, for the "My team" scope. */
export interface SalesTeam { id: string; name: string; memberUserIds: string[] }

/** The workspace's sales teams. None in demo mode or a personal workspace. */
export async function getSalesTeams(): Promise<SalesTeam[]> {
  if (servesDemoData()) return [];
  const list = await authFetchJson<SalesTeam[]>('/api/teams');
  return Array.isArray(list) ? list : [];
}

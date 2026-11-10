// The Reports figures for demo mode, which has no server.
//
// The same shapes the server's ReportingService returns, computed from the sample
// records so the demo's Reports page agrees with its Dashboard and Leads pages.
// The rules follow the server's and are tested against the same cases: money per
// currency and never added across currencies; a deal whose value cannot be read
// counted as unreadable, not as zero; an active lead is one not yet converted; the
// app's own "system" activities are not work anyone did. The demo has one user and
// no teams, so "me" is that user's deals and "team" is not offered.

import type { Activity, Deal, Lead } from './types';
import type {
  DealFigures, Forecast, ForecastMonthRow, MoneyRow, OwnerRow, ReportFilters, ReportSummary, SeriesPoint, StageRow, WeightedRow,
} from './reporting';
import { getDeals } from './deals';
import { getLeads } from './leads';
import { getActivities } from './activities';
import { getCurrentUser } from '@/app/lib/auth';
import { activityTime } from '@/app/lib/activityFacts';
import { amountOf, DEFAULT_CURRENCY } from '@/app/lib/money';

export interface DemoRecords {
  deals: Deal[];
  leads: Lead[];
  activities: Activity[];
  /** The demo user, for "me". */
  userId: string | null;
}

/** The currency a deal is counted in: blank means the workspace's, as on the server; the demo's is the default. */
export function currencyKey(currency?: string | null): string {
  const c = (currency ?? '').trim().toUpperCase();
  return c.length === 0 ? DEFAULT_CURRENCY : c;
}

function time(iso?: string | null): number | null {
  if (!iso) return null;
  const t = Date.parse(iso);
  return Number.isNaN(t) ? null : t;
}

function inRange(iso: string | undefined | null, from: number, to: number): boolean {
  const t = time(iso);
  return t !== null && t >= from && t < to;
}

function figures(deals: Deal[]): DealFigures {
  const m = new Map<string, MoneyRow>();
  for (const d of deals) {
    const currency = currencyKey(d.currency);
    const row = m.get(currency) ?? { currency, count: 0, value: 0, unreadable: 0 };
    const amount = amountOf(d);
    row.count += 1;
    if (amount === null) row.unreadable += 1;
    else row.value += amount;
    m.set(currency, row);
  }
  const byCurrency = [...m.values()].sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
  return { count: deals.length, byCurrency };
}

function weightedOf(d: Deal): number {
  const amount = amountOf(d);
  return amount !== null && typeof d.probability === 'number' ? (amount * d.probability) / 100 : 0;
}

function weighted(open: Deal[]): WeightedRow[] {
  const m = new Map<string, WeightedRow>();
  for (const d of open) {
    const currency = currencyKey(d.currency);
    const row = m.get(currency) ?? { currency, value: 0, covered: 0, missing: 0 };
    if (typeof d.probability !== 'number') row.missing += 1;
    else if (amountOf(d) !== null) {
      row.covered += 1;
      row.value += weightedOf(d);
    }
    m.set(currency, row);
  }
  return [...m.values()].sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
}

const isOpen = (d: Deal) => d.isWon === null || d.isWon === undefined;

function localDay(t: number): Date {
  const d = new Date(t);
  return new Date(d.getFullYear(), d.getMonth(), d.getDate());
}

function dayKey(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** An expected close date is a day, stored as its UTC midnight: that day, as yyyy-MM-dd. */
function closeDay(iso?: string | null): string | null {
  const t = time(iso);
  return t === null ? null : new Date(t).toISOString().slice(0, 10);
}

/** The chart grouping the server picks for a period of this length. */
export function bucketFor(from: number, to: number): 'day' | 'week' | 'month' {
  const days = (to - from) / 86_400_000;
  return days <= 62 ? 'day' : days <= 400 ? 'week' : 'month';
}

function bucketStart(day: Date, bucket: 'day' | 'week' | 'month'): Date {
  if (bucket === 'week') return new Date(day.getFullYear(), day.getMonth(), day.getDate() - ((day.getDay() + 6) % 7));
  if (bucket === 'month') return new Date(day.getFullYear(), day.getMonth(), 1);
  return day;
}

function nextBucket(b: Date, bucket: 'day' | 'week' | 'month'): Date {
  if (bucket === 'week') return new Date(b.getFullYear(), b.getMonth(), b.getDate() + 7);
  if (bucket === 'month') return new Date(b.getFullYear(), b.getMonth() + 1, 1);
  return new Date(b.getFullYear(), b.getMonth(), b.getDate() + 1);
}

/** Counts per bucket of the period, in this browser's time zone, empty buckets included. */
export function series(instants: (string | undefined | null)[], from: number, to: number, bucket: 'day' | 'week' | 'month'): SeriesPoint[] {
  const counts = new Map<string, number>();
  for (const iso of instants) {
    const t = time(iso);
    if (t === null || t < from || t >= to) continue;
    const k = dayKey(bucketStart(localDay(t), bucket));
    counts.set(k, (counts.get(k) ?? 0) + 1);
  }
  const out: SeriesPoint[] = [];
  const last = localDay(to - 1);
  for (let b = bucketStart(localDay(from), bucket); b <= last; b = nextBucket(b, bucket)) {
    out.push({ start: dayKey(b), count: counts.get(dayKey(b)) ?? 0 });
  }
  return out;
}

function countBy<T>(rows: T[], key: (r: T) => string): { key: string; count: number }[] {
  const m = new Map<string, number>();
  for (const r of rows) m.set(key(r), (m.get(key(r)) ?? 0) + 1);
  return [...m.entries()].map(([k, count]) => ({ key: k, count }))
    .sort((a, b) => b.count - a.count || a.key.localeCompare(b.key));
}

/** The Reports figures from these records, by the server's rules. */
export function summariseForDemo(records: DemoRecords, filters: ReportFilters): ReportSummary {
  const from = Date.parse(filters.from);
  const to = Date.parse(filters.to);
  const me = records.userId;
  const mine = (owner?: string) => filters.scope !== 'me' || !owner || owner === me;

  const scoped = records.deals.filter((d) => mine(d.assigneeId)
    && (!filters.pipelineId || d.pipelineId === filters.pipelineId));
  const currency = filters.currency ? currencyKey(filters.currency) : null;
  const deals = currency ? scoped.filter((d) => currencyKey(d.currency) === currency) : scoped;
  const open = deals.filter(isOpen);
  const won = deals.filter((d) => d.isWon === true && inRange(d.closedAtUtc, from, to));
  const lost = deals.filter((d) => d.isWon === false && inRange(d.closedAtUtc, from, to));

  const openByCurrency = figures(scoped.filter(isOpen)).byCurrency;
  const allByCurrency = figures(scoped).byCurrency;
  const currencies = [...new Set([...openByCurrency.map((r) => r.currency), ...allByCurrency.map((r) => r.currency)])];
  const headline = currency ?? openByCurrency[0]?.currency ?? currencies[0] ?? null;

  const stages = new Map<string, StageRow>();
  for (const d of open) {
    const c = currencyKey(d.currency);
    const stageName = d.dealStageName || d.stage || 'No stage';
    const k = `${d.dealStageId ?? stageName}|${c}`;
    const amount = amountOf(d);
    const row = stages.get(k) ?? {
      pipelineId: d.pipelineId ?? null, pipelineName: d.pipelineName || 'No pipeline', stageKey: d.dealStageId ?? stageName,
      stageName, displayOrder: Number.MAX_SAFE_INTEGER, currency: c, count: 0, value: 0, weighted: 0, unreadable: 0,
    };
    row.count += 1;
    if (amount === null) row.unreadable += 1;
    else row.value += amount;
    row.weighted += weightedOf(d);
    stages.set(k, row);
  }

  const owners = new Map<string, OwnerRow>();
  const ownerRow = (d: Deal): OwnerRow => {
    const c = currencyKey(d.currency);
    const k = `${d.assigneeId ?? ''}|${c}`;
    const row = owners.get(k) ?? {
      ownerId: d.assigneeId ?? null, ownerName: d.assigneeId ? d.assigneeName || 'Unknown' : 'Unassigned', currency: c,
      openCount: 0, openValue: 0, weighted: 0, wonCount: 0, wonValue: 0, lostCount: 0,
    };
    owners.set(k, row);
    return row;
  };
  for (const d of open) {
    const r = ownerRow(d);
    r.openCount += 1;
    r.openValue += amountOf(d) ?? 0;
    r.weighted += weightedOf(d);
  }
  for (const d of won) {
    const r = ownerRow(d);
    r.wonCount += 1;
    r.wonValue += amountOf(d) ?? 0;
  }
  for (const d of lost) ownerRow(d).lostCount += 1;

  const cycles = won
    .map((d) => (time(d.closedAtUtc) ?? 0) - (time(d.createdAtUtc) ?? Number.NaN))
    .filter((ms) => Number.isFinite(ms) && ms >= 0)
    .map((ms) => ms / 86_400_000);

  const leads = records.leads.filter((l) => mine(l.assignedToId));
  const cohort = leads.filter((l) => inRange(l.createdAtUtc, from, to));
  const converted = leads.filter((l) => l.isConverted && inRange(l.convertedAtUtc, from, to));
  const scored = cohort.filter((l) => typeof l.leadScore === 'number');
  const statuses = new Map<string, { status: string; count: number; converted: number }>();
  for (const l of cohort) {
    const status = (l.status || '').trim() || 'Unknown';
    const row = statuses.get(status) ?? { status, count: 0, converted: 0 };
    row.count += 1;
    if (l.isConverted) row.converted += 1;
    statuses.set(status, row);
  }

  const logged = records.activities
    .filter((a) => (a.type || '').toLowerCase() !== 'system' && inRange(activityTime(a), from, to))
    .filter((a) => filters.scope !== 'me' || !a.createdByUserId || a.createdByUserId === me);
  const bucket = bucketFor(from, to);
  // As on the server: a close date is matched to the period's days on this
  // browser's calendar. Compared with the period's instants, a deal due on the 1st
  // fell outside a month that starts after UTC midnight, as it does west of Greenwich.
  const firstDay = dayKey(localDay(from));
  const lastDay = dayKey(localDay(to - 1));
  const closesInPeriod = (d: Deal) => {
    const day = closeDay(d.expectedCloseDateUtc);
    return day !== null && day >= firstDay && day <= lastDay;
  };

  return {
    period: {
      fromUtc: new Date(from).toISOString(), toUtc: new Date(to).toISOString(), scope: filters.scope === 'team' ? 'org' : filters.scope,
      teamId: null, teamName: null, pipelineId: filters.pipelineId ?? null, currency, timeZone: filters.timeZone ?? 'UTC', bucket,
    },
    currencies,
    headlineCurrency: headline,
    open: figures(open),
    weighted: weighted(open),
    closingInPeriod: figures(open.filter(closesInPeriod)),
    created: figures(deals.filter((d) => inRange(d.createdAtUtc, from, to))),
    won: figures(won),
    lost: figures(lost),
    winRate: won.length + lost.length > 0 ? won.length / (won.length + lost.length) : null,
    averageSalesCycleDays: cycles.length > 0 ? Math.round((cycles.reduce((s, x) => s + x, 0) / cycles.length) * 10) / 10 : null,
    byStage: [...stages.values()].sort((a, b) => a.stageName.localeCompare(b.stageName) || a.currency.localeCompare(b.currency)),
    byOwner: [...owners.values()].sort((a, b) => (b.openValue + b.wonValue) - (a.openValue + a.wonValue) || a.ownerName.localeCompare(b.ownerName)),
    leads: {
      active: leads.filter((l) => !l.isConverted).length,
      created: cohort.length,
      createdConverted: cohort.filter((l) => l.isConverted).length,
      converted: converted.length,
      convertedToDeal: converted.length,
      scored: scored.length,
      averageScore: scored.length > 0
        ? Math.round((scored.reduce((s, l) => s + (l.leadScore ?? 0), 0) / scored.length) * 10) / 10
        : null,
      byStatus: [...statuses.values()].sort((a, b) => b.count - a.count || a.status.localeCompare(b.status)),
      bySource: countBy(cohort, (l) => (l.source || '').trim() || 'Unknown'),
      createdSeries: series(cohort.map((l) => l.createdAtUtc), from, to, bucket),
    },
    activities: {
      total: logged.length,
      byType: countBy(logged, (a) => (a.type || '').toLowerCase()),
      series: series(logged.map((a) => activityTime(a)), from, to, bucket),
    },
  };
}

function monthKey(d: Date): string {
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
}

/** The forecast by expected close month from these deals, by the server's rules. */
export function forecastForDemo(
  records: DemoRecords,
  filters: Pick<ReportFilters, 'scope' | 'pipelineId' | 'currency'> & { firstMonth: string; months: number },
  now = new Date(),
): Forecast {
  const [y, m] = filters.firstMonth.split('-').map(Number);
  const first = Date.UTC(y!, (m ?? 1) - 1, 1);
  const end = Date.UTC(y!, (m ?? 1) - 1 + filters.months, 1);
  const currency = filters.currency ? currencyKey(filters.currency) : null;
  const deals = records.deals.filter((d) => (filters.scope !== 'me' || !d.assigneeId || d.assigneeId === records.userId)
    && (!filters.pipelineId || d.pipelineId === filters.pipelineId)
    && (!currency || currencyKey(d.currency) === currency));
  const open = deals.filter(isOpen);
  const expected = open.filter((d) => inRange(d.expectedCloseDateUtc, first, end));
  const won = deals.filter((d) => d.isWon === true && inRange(d.closedAtUtc, first, end));
  const currencies = [...new Set([...expected, ...won].map((d) => currencyKey(d.currency)))].sort();

  const rows: ForecastMonthRow[] = [];
  for (let i = 0; i < filters.months; i += 1) {
    const month = monthKey(new Date(Date.UTC(y!, (m ?? 1) - 1 + i, 1)));
    for (const c of currencies) {
      const inMonth = expected.filter((d) => currencyKey(d.currency) === c && monthKey(new Date(time(d.expectedCloseDateUtc)!)) === month);
      const wonInMonth = won.filter((d) => currencyKey(d.currency) === c && monthKey(new Date(time(d.closedAtUtc)!)) === month);
      const f = figures(inMonth).byCurrency[0];
      const w = weighted(inMonth)[0];
      rows.push({
        month, currency: c, count: inMonth.length, value: f?.value ?? 0, weighted: Math.round((w?.value ?? 0) * 100) / 100,
        covered: w?.covered ?? 0, missing: w?.missing ?? 0, unreadable: f?.unreadable ?? 0,
        wonCount: wonInMonth.length, wonValue: figures(wonInMonth).byCurrency[0]?.value ?? 0,
      });
    }
  }
  const today = Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate());
  return {
    firstMonth: monthKey(new Date(first)),
    months: filters.months,
    scope: filters.scope === 'team' ? 'org' : filters.scope,
    teamId: null,
    rows,
    overdue: figures(open.filter((d) => { const t = time(d.expectedCloseDateUtc); return t !== null && t < today; })).byCurrency,
    noCloseDate: figures(open.filter((d) => !d.expectedCloseDateUtc)).byCurrency,
  };
}

async function demoRecords(): Promise<DemoRecords> {
  const [deals, leads, activities] = await Promise.all([getDeals(), getLeads(), getActivities()]);
  return {
    deals: Array.isArray(deals) ? deals : [],
    leads: Array.isArray(leads) ? leads : [],
    activities: Array.isArray(activities) ? activities : [],
    userId: getCurrentUser()?.id ?? null,
  };
}

export async function demoReportSummary(filters: ReportFilters): Promise<ReportSummary> {
  return summariseForDemo(await demoRecords(), filters);
}

export async function demoForecast(
  filters: Pick<ReportFilters, 'scope' | 'pipelineId' | 'currency'> & { firstMonth: string; months: number },
): Promise<Forecast> {
  return forecastForDemo(await demoRecords(), filters);
}

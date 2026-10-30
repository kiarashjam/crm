// The Reports page's filters, as pure functions.
//
// A period is chosen by name ("This month", "Last quarter") and turned into two
// instants here, at the workspace's midnights in its own time zone, and the server
// counts between the instants it is given. These used this browser's midnights, so a
// manager away from the club cut its months at their own midnight, and a deal won in
// Zurich at half past midnight on the 1st counted in the month before.

import type { ReportFilters, ReportScope } from '@/app/api/reporting';
import { dayKeyIn, startOfDayIn, zonedParts } from '@/app/lib/zonedTime';

export type PeriodPreset =
  | 'this-month' | 'last-month' | 'this-quarter' | 'last-quarter'
  | 'last-30' | 'last-90' | 'this-year' | 'last-year' | 'custom';

export const PERIOD_LABELS: Record<PeriodPreset, string> = {
  'this-month': 'This month',
  'last-month': 'Last month',
  'this-quarter': 'This quarter',
  'last-quarter': 'Last quarter',
  'last-30': 'Last 30 days',
  'last-90': 'Last 90 days',
  'this-year': 'This year',
  'last-year': 'Last year',
  custom: 'Custom dates',
};

export interface Period {
  /** The workspace's midnight that starts the period. */
  from: Date;
  /** The workspace's midnight after its last day (exclusive). */
  to: Date;
}

/** The period a preset names, between midnights in `timeZone`, as of `now`. */
export function periodFor(preset: Exclude<PeriodPreset, 'custom'>, timeZone: string, now = new Date()): Period {
  const { year: y, month: m, day } = zonedParts(now, timeZone);
  // Months and days out of range roll over: month 0 is last December.
  const at = (year: number, month: number, d: number) => startOfDayIn(year, month, d, timeZone);
  const quarter = Math.floor((m - 1) / 3) * 3 + 1;
  switch (preset) {
    case 'this-month': return { from: at(y, m, 1), to: at(y, m + 1, 1) };
    case 'last-month': return { from: at(y, m - 1, 1), to: at(y, m, 1) };
    case 'this-quarter': return { from: at(y, quarter, 1), to: at(y, quarter + 3, 1) };
    case 'last-quarter': return { from: at(y, quarter - 3, 1), to: at(y, quarter, 1) };
    // The last N days include today.
    case 'last-30': return { from: at(y, m, day - 29), to: at(y, m, day + 1) };
    case 'last-90': return { from: at(y, m, day - 89), to: at(y, m, day + 1) };
    case 'this-year': return { from: at(y, 1, 1), to: at(y + 1, 1, 1) };
    case 'last-year': return { from: at(y - 1, 1, 1), to: at(y, 1, 1) };
  }
}

/** yyyy-MM-dd of the day an instant falls on in `timeZone`, as an <input type="date"> holds it. */
export function dateInputValue(d: Date, timeZone: string): string {
  return dayKeyIn(d, timeZone);
}

/**
 * A custom period from two date inputs, both days included, between midnights in
 * `timeZone`. Null when either is missing or the end is before the start.
 */
export function customPeriod(fromDay: string, toDay: string, timeZone: string): Period | null {
  const parse = (s: string) => {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
    return match ? { y: Number(match[1]), m: Number(match[2]), d: Number(match[3]) } : null;
  };
  const first = parse(fromDay);
  const last = parse(toDay);
  if (!first || !last) return null;
  const from = startOfDayIn(first.y, first.m, first.d, timeZone);
  const to = startOfDayIn(last.y, last.m, last.d + 1, timeZone);
  return to > from ? { from, to } : null;
}

export interface FilterState {
  preset: PeriodPreset;
  customFrom: string;
  customTo: string;
  scope: ReportScope;
  teamId: string;
  pipelineId: string;
  /** Empty means "the headline currency the server picks". */
  currency: string;
}

export const DEFAULT_FILTERS: FilterState = {
  preset: 'this-month',
  customFrom: '',
  customTo: '',
  scope: 'org',
  teamId: '',
  pipelineId: '',
  currency: '',
};

/**
 * The request a filter state makes, or null for a custom period that is not complete
 * yet. The charts' days are counted in the zone the period was cut in.
 */
export function toReportFilters(state: FilterState, timeZone: string, now = new Date()): ReportFilters | null {
  const period = state.preset === 'custom' ? customPeriod(state.customFrom, state.customTo, timeZone) : periodFor(state.preset, timeZone, now);
  if (!period) return null;
  return {
    from: period.from.toISOString(),
    to: period.to.toISOString(),
    scope: state.scope,
    teamId: state.scope === 'team' && state.teamId ? state.teamId : undefined,
    pipelineId: state.pipelineId || undefined,
    currency: state.currency || undefined,
    timeZone,
  };
}

/** yyyy-MM of the month an instant falls in, in `timeZone`. */
export function monthOf(d: Date, timeZone: string): string {
  return dayKeyIn(d, timeZone).slice(0, 7);
}

/** The yyyy-MM `by` months after (or before) another. */
export function addMonths(month: string, by: number): string {
  const [y, m] = month.split('-').map(Number);
  const d = new Date(Date.UTC(y!, m! - 1 + by, 1));
  return `${d.getUTCFullYear()}-${String(d.getUTCMonth() + 1).padStart(2, '0')}`;
}

/** "2026-10" as "Oct 2026" in `locale`. */
export function monthLabel(month: string, locale?: string): string {
  const [y, m] = month.split('-').map(Number);
  if (!y || !m) return month;
  // A calendar month, not an instant: written as it is, wherever the viewer is.
  return new Date(Date.UTC(y, m - 1, 1)).toLocaleDateString(locale, { month: 'short', year: 'numeric', timeZone: 'UTC' });
}

/** A chart bucket's label: the day, or for weeks "w/c" the Monday, or the month. */
export function bucketLabel(start: string, bucket: 'day' | 'week' | 'month', locale?: string): string {
  const [y, m, d] = start.split('-').map(Number);
  if (!y || !m || !d) return start;
  const date = new Date(Date.UTC(y, m - 1, d));
  if (bucket === 'month') return date.toLocaleDateString(locale, { month: 'short', year: '2-digit', timeZone: 'UTC' });
  const day = date.toLocaleDateString(locale, { month: 'short', day: 'numeric', timeZone: 'UTC' });
  return bucket === 'week' ? `w/c ${day}` : day;
}

const FILTERS_KEY = 'cadence.reports.filters';

/** The viewer's last filters in this browser, a convenience only. */
export function loadSavedFilters(): FilterState {
  try {
    const raw = localStorage.getItem(FILTERS_KEY);
    if (!raw) return DEFAULT_FILTERS;
    const saved = JSON.parse(raw) as Partial<FilterState>;
    return {
      ...DEFAULT_FILTERS,
      ...saved,
      scope: saved.scope === 'me' || saved.scope === 'team' || saved.scope === 'org' ? saved.scope : 'org',
      preset: saved.preset && saved.preset in PERIOD_LABELS ? saved.preset : DEFAULT_FILTERS.preset,
    };
  } catch {
    return DEFAULT_FILTERS;
  }
}

export function saveFilters(state: FilterState): void {
  try {
    localStorage.setItem(FILTERS_KEY, JSON.stringify(state));
  } catch {
    // A private window: the filters just are not remembered.
  }
}

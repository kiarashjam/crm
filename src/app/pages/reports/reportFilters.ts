// The Reports page's filters, as pure functions.
//
// A period is chosen by name ("This month", "Last quarter") and turned into two
// instants here, at this browser's own midnights. The server has no organisation
// time zone yet, so it counts between the instants it is given rather than
// guessing where the viewer's month starts.

import type { ReportFilters, ReportScope } from '@/app/api/reporting';

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
  /** Local midnight that starts the period. */
  from: Date;
  /** Local midnight after its last day (exclusive). */
  to: Date;
}

/** The period a preset names, at local midnights, as of `now`. */
export function periodFor(preset: Exclude<PeriodPreset, 'custom'>, now = new Date()): Period {
  const y = now.getFullYear();
  const m = now.getMonth();
  const today = new Date(y, m, now.getDate());
  const tomorrow = new Date(y, m, now.getDate() + 1);
  const quarter = Math.floor(m / 3) * 3;
  switch (preset) {
    case 'this-month': return { from: new Date(y, m, 1), to: new Date(y, m + 1, 1) };
    case 'last-month': return { from: new Date(y, m - 1, 1), to: new Date(y, m, 1) };
    case 'this-quarter': return { from: new Date(y, quarter, 1), to: new Date(y, quarter + 3, 1) };
    case 'last-quarter': return { from: new Date(y, quarter - 3, 1), to: new Date(y, quarter, 1) };
    // The last N days include today.
    case 'last-30': return { from: new Date(y, m, today.getDate() - 29), to: tomorrow };
    case 'last-90': return { from: new Date(y, m, today.getDate() - 89), to: tomorrow };
    case 'this-year': return { from: new Date(y, 0, 1), to: new Date(y + 1, 0, 1) };
    case 'last-year': return { from: new Date(y - 1, 0, 1), to: new Date(y, 0, 1) };
  }
}

/** yyyy-MM-dd of a local date, as an <input type="date"> holds it. */
export function dateInputValue(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/**
 * A custom period from two date inputs, both days included. Null when either is
 * missing or the end is before the start.
 */
export function customPeriod(fromDay: string, toDay: string): Period | null {
  const parse = (s: string) => {
    const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(s);
    return match ? new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3])) : null;
  };
  const from = parse(fromDay);
  const last = parse(toDay);
  if (!from || !last || last < from) return null;
  return { from, to: new Date(last.getFullYear(), last.getMonth(), last.getDate() + 1) };
}

/** The browser's IANA time zone, which the server counts the charts' days in. */
export function browserTimeZone(): string | undefined {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || undefined;
  } catch {
    return undefined;
  }
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

/** The request a filter state makes, or null for a custom period that is not complete yet. */
export function toReportFilters(state: FilterState, now = new Date()): ReportFilters | null {
  const period = state.preset === 'custom' ? customPeriod(state.customFrom, state.customTo) : periodFor(state.preset, now);
  if (!period) return null;
  return {
    from: period.from.toISOString(),
    to: period.to.toISOString(),
    scope: state.scope,
    teamId: state.scope === 'team' && state.teamId ? state.teamId : undefined,
    pipelineId: state.pipelineId || undefined,
    currency: state.currency || undefined,
    timeZone: browserTimeZone(),
  };
}

/** yyyy-MM of the local month a date is in. */
export function monthOf(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}`;
}

/** "2026-10" as "Oct 2026" in the viewer's language. */
export function monthLabel(month: string): string {
  const [y, m] = month.split('-').map(Number);
  if (!y || !m) return month;
  return new Date(y, m - 1, 1).toLocaleDateString(undefined, { month: 'short', year: 'numeric' });
}

/** A chart bucket's label: the day, or for weeks "w/c" the Monday, or the month. */
export function bucketLabel(start: string, bucket: 'day' | 'week' | 'month'): string {
  const [y, m, d] = start.split('-').map(Number);
  if (!y || !m || !d) return start;
  const date = new Date(y, m - 1, d);
  if (bucket === 'month') return date.toLocaleDateString(undefined, { month: 'short', year: '2-digit' });
  const day = date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
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

// The period the site analytics show: a preset (today, the last 7, 30 or 90 days) or two
// days picked, always as Europe/Zurich days, which is how the server counts them. The
// choice lives in the address (?range=7d, ?range=custom&from=…&to=…), so a link or the
// back button brings back the same view.

import { SITE_ANALYTICS_TIME_ZONE } from '@/app/api/siteAnalytics';
import { dayKey, daysBetween, todayKeyIn } from '@/app/lib/zonedTime';

export type RangePreset = 'today' | '7d' | '30d' | '90d' | 'custom';

export const RANGE_PRESETS: { value: RangePreset; label: string }[] = [
  { value: 'today', label: 'Today' },
  { value: '7d', label: '7 days' },
  { value: '30d', label: '30 days' },
  { value: '90d', label: '90 days' },
  { value: 'custom', label: 'Custom' },
];

/** What the server shows when asked for nothing in particular: the last 30 days. */
export const DEFAULT_PRESET: RangePreset = '30d';

const DAYS: Record<Exclude<RangePreset, 'custom'>, number> = { today: 1, '7d': 7, '30d': 30, '90d': 90 };

export interface RangeSelection {
  preset: RangePreset;
  /** yyyy-MM-dd, for a custom range. */
  customFrom: string;
  customTo: string;
}

/** Both days included, yyyy-MM-dd in Europe/Zurich. */
export interface DayRange {
  from: string;
  to: string;
}

const DAY = /^\d{4}-\d{2}-\d{2}$/;

/** A day this many days after (or, negative, before) another. */
export function addDays(key: string, days: number): string {
  return dayKey(Number(key.slice(0, 4)), Number(key.slice(5, 7)), Number(key.slice(8, 10)) + days);
}

/** Today in Zurich. */
export function todayInZurich(now: Date = new Date()): string {
  return todayKeyIn(SITE_ANALYTICS_TIME_ZONE, now);
}

/** The selection the address holds; the last 30 days when it holds none (or nonsense). */
export function readSelection(params: URLSearchParams): RangeSelection {
  const raw = params.get('range');
  const preset = RANGE_PRESETS.some((p) => p.value === raw) ? (raw as RangePreset) : DEFAULT_PRESET;
  return { preset, customFrom: params.get('from') ?? '', customTo: params.get('to') ?? '' };
}

/** The address's parameters for a selection: none for the default. */
export function writeSelection(selection: RangeSelection, params: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(params);
  next.delete('range');
  next.delete('from');
  next.delete('to');
  if (selection.preset !== DEFAULT_PRESET) next.set('range', selection.preset);
  if (selection.preset === 'custom') {
    if (selection.customFrom) next.set('from', selection.customFrom);
    if (selection.customTo) next.set('to', selection.customTo);
  }
  return next;
}

/**
 * The days a selection covers, or null for a custom range still being typed (a day
 * missing, or the end before the start).
 */
export function rangeFor(selection: RangeSelection, now: Date = new Date()): DayRange | null {
  if (selection.preset === 'custom') {
    const { customFrom: from, customTo: to } = selection;
    if (!DAY.test(from) || !DAY.test(to) || daysBetween(from, to) < 0) return null;
    return { from, to };
  }
  const to = todayInZurich(now);
  return { from: addDays(to, 1 - DAYS[selection.preset]), to };
}

/** How many days a range covers, both ends included. */
export function rangeLength(range: DayRange): number {
  return daysBetween(range.from, range.to) + 1;
}

/** What the period before is called: "yesterday", "the previous 30 days". */
export function previousPeriodLabel(days: number): string {
  if (days <= 1) return 'yesterday';
  return `the previous ${days} days`;
}

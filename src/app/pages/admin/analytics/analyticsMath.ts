// The site analytics' arithmetic, as pure functions: changes against the period before, the
// 7-day average, the best day, the sign-up funnel, and the words for sources, events, times
// and durations. The page only draws what these return.

import type {
  SiteAnalyticsDay, SiteAnalyticsEventRow, SiteAnalyticsStep, SiteAnalyticsTotals,
} from '@/app/api/siteAnalytics';

// ── Change against the period before ────────────────────────────────────────

/**
 * How a figure moved: `new` when there was none before and is some now, `none` when there
 * is none either side, else the change (a ratio for counts, percentage points for a rate).
 */
export type Delta =
  | { kind: 'new' }
  | { kind: 'none' }
  | { kind: 'change'; value: number; unit: 'ratio' | 'points' };

/** Below this a change reads as flat (under half a percent, or half a point). */
const FLAT = 0.005;

/** The change of a count: 142 against 103 is +0.379. */
export function countDelta(current: number, previous: number): Delta {
  if (previous === 0) return current > 0 ? { kind: 'new' } : { kind: 'none' };
  return { kind: 'change', value: (current - previous) / previous, unit: 'ratio' };
}

/**
 * The change of a rate (0..1), in points: a bounce rate from 0.50 to 0.42 is −8 points. With
 * no visits before there was no rate before, so any rate now is new.
 */
export function rateDelta(current: number, previous: number, hadBefore: boolean, hasNow: boolean): Delta {
  if (!hadBefore) return hasNow ? { kind: 'new' } : { kind: 'none' };
  if (!hasNow) return { kind: 'none' };
  return { kind: 'change', value: current - previous, unit: 'points' };
}

/** The change of an average, such as the visit's length: new when there were no visits before. */
export function averageDelta(current: number, previous: number, hadBefore: boolean, hasNow: boolean): Delta {
  if (!hadBefore) return hasNow ? { kind: 'new' } : { kind: 'none' };
  if (!hasNow) return { kind: 'none' };
  if (previous === 0) return current > 0 ? { kind: 'new' } : { kind: 'none' };
  return { kind: 'change', value: (current - previous) / previous, unit: 'ratio' };
}

export type Direction = 'up' | 'down' | 'flat';

export function directionOf(delta: Delta): Direction {
  if (delta.kind !== 'change') return 'flat';
  if (delta.value > FLAT) return 'up';
  if (delta.value < -FLAT) return 'down';
  return 'flat';
}

/** Whether the move is good news: more visitors is, a higher bounce rate is not. */
export function sentimentOf(delta: Delta, higherIsBetter = true): 'good' | 'bad' | 'neutral' {
  if (delta.kind === 'new') return 'good';
  const direction = directionOf(delta);
  if (direction === 'flat') return 'neutral';
  return (direction === 'up') === higherIsBetter ? 'good' : 'bad';
}

// ── The KPI cards ───────────────────────────────────────────────────────────

export type KpiKey = 'visitors' | 'sessions' | 'pageViews' | 'signups' | 'bounceRate' | 'avgSessionSeconds';

/** Each card's change against the period before. */
export function kpiDelta(key: KpiKey, totals: SiteAnalyticsTotals, previous: SiteAnalyticsTotals): Delta {
  const hadBefore = previous.sessions > 0;
  const hasNow = totals.sessions > 0;
  switch (key) {
    case 'bounceRate': return rateDelta(totals.bounceRate, previous.bounceRate, hadBefore, hasNow);
    case 'avgSessionSeconds': return averageDelta(totals.avgSessionSeconds, previous.avgSessionSeconds, hadBefore, hasNow);
    default: return countDelta(totals[key], previous[key]);
  }
}

// ── The growth chart ────────────────────────────────────────────────────────

/**
 * The trailing average over `window` days (the day and the days before it). The first days
 * of the period average what there is so far, so the line starts on the first day.
 */
export function movingAverage(values: readonly number[], window = 7): number[] {
  const out: number[] = [];
  let sum = 0;
  for (let i = 0; i < values.length; i++) {
    sum += values[i] ?? 0;
    if (i >= window) sum -= values[i - window] ?? 0;
    out.push(sum / Math.min(i + 1, window));
  }
  return out;
}

export interface GrowthRow extends SiteAnalyticsDay {
  /** Visitors, averaged over the 7 days up to this one. */
  average: number;
}

export function growthRows(daily: readonly SiteAnalyticsDay[]): GrowthRow[] {
  const average = movingAverage(daily.map((d) => d.visitors));
  return daily.map((d, i) => ({ ...d, average: average[i] ?? 0 }));
}

/** The day with the most visitors (the latest of equals), or null while no day had any. */
export function bestDay(daily: readonly SiteAnalyticsDay[]): SiteAnalyticsDay | null {
  let best: SiteAnalyticsDay | null = null;
  for (const d of daily) if (d.visitors > 0 && (!best || d.visitors >= best.visitors)) best = d;
  return best;
}

/** How many days of the period had no visitor. */
export function zeroDays(daily: readonly SiteAnalyticsDay[]): number {
  return daily.filter((d) => d.visitors === 0).length;
}

/**
 * The 7-day average at the start of the period (its first full week, or its first day for a
 * shorter one) and at its end: how the daily rate moved inside the period.
 */
export function averageTrend(rows: readonly GrowthRow[]): { start: number; end: number } | null {
  if (rows.length < 14) return null;
  return { start: rows[6]!.average, end: rows[rows.length - 1]!.average };
}

/** A round top for the y-axis: never below 4, so one visitor is not drawn as a peak. */
export function niceMax(max: number): number {
  if (!Number.isFinite(max) || max <= 4) return 4;
  const magnitude = 10 ** Math.floor(Math.log10(max));
  for (const step of [1, 1.2, 1.5, 2, 2.5, 3, 4, 5, 6, 8, 10]) {
    const top = step * magnitude;
    if (top >= max) return top;
  }
  return 10 * magnitude;
}

// ── The sign-up funnel ──────────────────────────────────────────────────────

export interface FunnelStep {
  key: 'visitors' | 'cta-signup' | 'signup_started' | 'signup_completed';
  label: string;
  value: number;
  /** Share of the step before; null for the first step, or when the step before had none. */
  ofPrevious: number | null;
  /** Share of the visitors; null while there are none. */
  ofVisitors: number | null;
}

const eventCount = (events: readonly SiteAnalyticsEventRow[], name: string): number | undefined =>
  events.find((e) => e.name === name)?.count;

/**
 * Visitors → sign-up button clicks → sign-ups started → sign-ups completed. The last step is
 * the signup_completed event, or the period's sign-ups when the site sent no such event.
 */
export function funnelSteps(totals: SiteAnalyticsTotals, events: readonly SiteAnalyticsEventRow[]): FunnelStep[] {
  const raw: [FunnelStep['key'], string, number][] = [
    ['visitors', 'Visitors', totals.visitors],
    ['cta-signup', 'Clicked a sign-up button', eventCount(events, 'cta-signup') ?? 0],
    ['signup_started', 'Started signing up', eventCount(events, 'signup_started') ?? 0],
    ['signup_completed', 'Signed up', eventCount(events, 'signup_completed') ?? totals.signups],
  ];
  const visitors = totals.visitors;
  return raw.map(([key, label, value], i) => {
    const previous = i > 0 ? raw[i - 1]![2] : null;
    return {
      key,
      label,
      value,
      ofPrevious: previous === null || previous === 0 ? null : value / previous,
      ofVisitors: visitors > 0 ? value / visitors : null,
    };
  });
}

// ── Words ───────────────────────────────────────────────────────────────────

const SOURCE_LABELS: Record<string, string> = {
  google: 'Google',
  bing: 'Bing',
  duckduckgo: 'DuckDuckGo',
  linkedin: 'LinkedIn',
  facebook: 'Facebook',
  instagram: 'Instagram',
  x: 'X (Twitter)',
  ai: 'AI assistants',
  email: 'Email',
  direct: 'Direct',
};

/** A source as people say it: "google" is Google, a host or utm source stays as it is. */
export function sourceLabel(source: string): string {
  const s = (source ?? '').trim();
  if (!s) return 'Unknown';
  return SOURCE_LABELS[s.toLowerCase()] ?? s;
}

const EVENT_LABELS: Record<string, string> = {
  'cta-signup': 'Sign-up button clicked',
  signup_started: 'Sign-up started',
  signup_completed: 'Sign-up completed',
  login: 'Logged in',
  outbound: 'Outbound link followed',
};

/** An event as people say it; an unknown one is tidied: "pricing_toggle" is "Pricing toggle". */
export function eventLabel(name: string): string {
  const known = EVENT_LABELS[name];
  if (known) return known;
  const t = name.replace(/[_-]+/g, ' ').trim();
  return t ? t[0]!.toUpperCase() + t.slice(1) : 'Event';
}

/** What a step of a visit was: the page, or "click cta-signup". */
export function stepText(step: Pick<SiteAnalyticsStep, 'type' | 'path' | 'name'>): string {
  if (step.type === 'page_view') return step.path || '/';
  if (step.type === 'click') return `click ${step.name ?? 'link'}`;
  return step.name ?? 'event';
}

/** A visit's path in one line: "/ → /features/club-crm → click cta-signup → /login". */
export function journeyText(steps: readonly SiteAnalyticsStep[]): string {
  return steps.map(stepText).join(' → ');
}

/** "45s", "2m 05s", "1h 02m". */
export function formatDuration(seconds: number): string {
  const s = Math.max(0, Math.round(Number.isFinite(seconds) ? seconds : 0));
  if (s < 60) return `${s}s`;
  const minutes = Math.floor(s / 60);
  if (minutes < 60) return `${minutes}m ${String(s % 60).padStart(2, '0')}s`;
  return `${Math.floor(minutes / 60)}h ${String(minutes % 60).padStart(2, '0')}m`;
}

/** "just now", "40 s ago", "3 min ago", "2 h ago", "4 d ago". */
export function relativeTime(iso: string, now: number = Date.now()): string {
  const t = Date.parse(iso);
  if (Number.isNaN(t)) return '';
  const seconds = Math.max(0, Math.round((now - t) / 1000));
  if (seconds < 10) return 'just now';
  if (seconds < 60) return `${seconds} s ago`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} min ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} h ago`;
  return `${Math.floor(hours / 24)} d ago`;
}

/** A language code in words, in the reader's language: "de-CH" is "German (Switzerland)". */
export function languageName(code: string, locale = 'en'): string {
  const c = (code ?? '').trim();
  if (!c) return 'Unknown';
  try {
    return new Intl.DisplayNames([locale], { type: 'language' }).of(c) ?? c;
  } catch {
    return c;
  }
}

/** "desktop" is "Desktop". */
export function capitalise(text: string): string {
  const t = (text ?? '').trim();
  return t ? t[0]!.toUpperCase() + t.slice(1) : 'Unknown';
}

/** Rows with nothing in them left out, the largest first. */
export function nonEmpty<T>(rows: readonly T[] | null | undefined, value: (row: T) => number): T[] {
  return (rows ?? []).filter((r) => value(r) > 0).sort((a, b) => value(b) - value(a));
}

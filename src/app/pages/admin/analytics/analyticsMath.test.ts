// The site analytics' arithmetic: changes against the period before, the 7-day average,
// the best day, the funnel's rates, the axis top, and the period's days.

import { describe, expect, it } from 'vitest';
import type { SiteAnalyticsDay, SiteAnalyticsTotals } from '@/app/api/siteAnalytics';
import {
  averageTrend, bestDay, countDelta, directionOf, formatDuration, funnelSteps, growthRows, journeyText, kpiDelta,
  movingAverage, niceMax, rateDelta, relativeTime, sentimentOf, sourceLabel, zeroDays,
} from './analyticsMath';
import { addDays, previousPeriodLabel, rangeFor, rangeLength, readSelection, writeSelection } from './range';

const totals = (over: Partial<SiteAnalyticsTotals> = {}): SiteAnalyticsTotals => ({
  visitors: 0, sessions: 0, pageViews: 0, bounceRate: 0, pagesPerSession: 0, avgSessionSeconds: 0, signups: 0, botHits: 0, ...over,
});
const day = (date: string, visitors: number): SiteAnalyticsDay => ({ date, visitors, sessions: visitors, pageViews: visitors * 2, signups: 0 });

describe('changes against the period before', () => {
  it('is a ratio for counts, "new" from nothing, and nothing from nothing', () => {
    expect(countDelta(142, 103)).toEqual({ kind: 'change', value: (142 - 103) / 103, unit: 'ratio' });
    expect(countDelta(3, 0)).toEqual({ kind: 'new' });
    expect(countDelta(0, 0)).toEqual({ kind: 'none' });
    expect(countDelta(0, 5)).toEqual({ kind: 'change', value: -1, unit: 'ratio' });
  });

  it('is in points for a rate, and new when there were no visits before', () => {
    expect(rateDelta(0.42, 0.5, true, true)).toEqual({ kind: 'change', value: expect.closeTo(-0.08, 5), unit: 'points' });
    expect(rateDelta(0.42, 0, false, true)).toEqual({ kind: 'new' });
    expect(rateDelta(0, 0, false, false)).toEqual({ kind: 'none' });
  });

  it('reads a bounce rate going down as good news, and visitors going down as bad', () => {
    const t = totals({ visitors: 80, sessions: 100, bounceRate: 0.4 });
    const p = totals({ visitors: 100, sessions: 100, bounceRate: 0.5 });
    expect(sentimentOf(kpiDelta('bounceRate', t, p), false)).toBe('good');
    expect(sentimentOf(kpiDelta('visitors', t, p))).toBe('bad');
    expect(directionOf(countDelta(100, 100))).toBe('flat');
    expect(sentimentOf(countDelta(100, 100))).toBe('neutral');
  });
});

describe('the growth chart', () => {
  it('averages the last 7 days, starting from the first day', () => {
    expect(movingAverage([7, 0, 0, 0, 0, 0, 0, 7])).toEqual([7, 3.5, 7 / 3, 1.75, 1.4, 7 / 6, 1, 1]);
    expect(movingAverage([])).toEqual([]);
  });

  it('finds the best day (the latest of equals), and none while nobody came', () => {
    expect(bestDay([day('2026-11-01', 2), day('2026-11-02', 5), day('2026-11-03', 5), day('2026-11-04', 1)])?.date).toBe('2026-11-03');
    expect(bestDay([day('2026-11-01', 0), day('2026-11-02', 0)])).toBeNull();
    expect(zeroDays([day('2026-11-01', 0), day('2026-11-02', 3), day('2026-11-03', 0)])).toBe(2);
  });

  it('says how the 7-day average moved through a period of two weeks or more', () => {
    const days = Array.from({ length: 14 }, (_, i) => day(addDays('2026-11-01', i), i < 7 ? 1 : 3));
    expect(averageTrend(growthRows(days))).toEqual({ start: 1, end: 3 });
    expect(averageTrend(growthRows(days.slice(0, 7)))).toBeNull();
  });

  it('never tops the axis out below 4, and rounds it up nicely', () => {
    expect(niceMax(0)).toBe(4);
    expect(niceMax(1)).toBe(4);
    expect(niceMax(7)).toBe(8);
    expect(niceMax(48)).toBe(50);
    expect(niceMax(130)).toBe(150);
  });
});

describe('the sign-up funnel', () => {
  it('gives each step its share of the one before and of the visitors', () => {
    const steps = funnelSteps(totals({ visitors: 200, signups: 10 }), [
      { name: 'cta-signup', count: 40 }, { name: 'signup_started', count: 20 }, { name: 'signup_completed', count: 10 },
    ]);
    expect(steps.map((s) => [s.key, s.value, s.ofPrevious, s.ofVisitors])).toEqual([
      ['visitors', 200, null, 1],
      ['cta-signup', 40, 0.2, 0.2],
      ['signup_started', 20, 0.5, 0.1],
      ['signup_completed', 10, 0.5, 0.05],
    ]);
  });

  it('counts the period\'s sign-ups when the site sent no signup_completed event, and has no rate after an empty step', () => {
    const steps = funnelSteps(totals({ visitors: 10, signups: 2 }), []);
    expect(steps[3]).toMatchObject({ value: 2, ofPrevious: null, ofVisitors: 0.2 });
    expect(steps[1]).toMatchObject({ value: 0, ofPrevious: 0 });
  });
});

describe('words', () => {
  it('names sources, durations, times and paths', () => {
    expect(sourceLabel('google')).toBe('Google');
    expect(sourceLabel('ai')).toBe('AI assistants');
    expect(sourceLabel('news.ycombinator.com')).toBe('news.ycombinator.com');
    expect(formatDuration(45)).toBe('45s');
    expect(formatDuration(125)).toBe('2m 05s');
    expect(formatDuration(3720)).toBe('1h 02m');
    const now = Date.parse('2026-11-25T10:00:00Z');
    expect(relativeTime('2026-11-25T09:59:55Z', now)).toBe('just now');
    expect(relativeTime('2026-11-25T09:59:20Z', now)).toBe('40 s ago');
    expect(relativeTime('2026-11-25T09:57:00Z', now)).toBe('3 min ago');
    expect(journeyText([
      { at: '', type: 'page_view', path: '/' },
      { at: '', type: 'page_view', path: '/features/club-crm' },
      { at: '', type: 'click', path: '/features/club-crm', name: 'cta-signup' },
      { at: '', type: 'page_view', path: '/login' },
    ])).toBe('/ → /features/club-crm → click cta-signup → /login');
  });
});

describe('the period', () => {
  const now = new Date('2026-11-25T10:00:00Z');

  it('is the last 30 days by default, as Zurich days including today', () => {
    expect(rangeFor(readSelection(new URLSearchParams()), now)).toEqual({ from: '2026-10-27', to: '2026-11-25' });
    expect(rangeFor(readSelection(new URLSearchParams('range=7d')), now)).toEqual({ from: '2026-11-19', to: '2026-11-25' });
    expect(rangeFor(readSelection(new URLSearchParams('range=today')), now)).toEqual({ from: '2026-11-25', to: '2026-11-25' });
    expect(rangeFor(readSelection(new URLSearchParams('range=90d')), now)).toEqual({ from: '2026-08-28', to: '2026-11-25' });
  });

  it('takes Zurich\'s day, not the browser\'s: 23:30 UTC is already tomorrow there', () => {
    expect(rangeFor({ preset: 'today', customFrom: '', customTo: '' }, new Date('2026-11-25T23:30:00Z'))).toEqual({ from: '2026-11-26', to: '2026-11-26' });
  });

  it('is two picked days, or nothing while the end is before the start', () => {
    expect(rangeFor(readSelection(new URLSearchParams('range=custom&from=2026-11-01&to=2026-11-10')), now)).toEqual({ from: '2026-11-01', to: '2026-11-10' });
    expect(rangeFor(readSelection(new URLSearchParams('range=custom&from=2026-11-10&to=2026-11-01')), now)).toBeNull();
    expect(rangeFor(readSelection(new URLSearchParams('range=custom&from=2026-11-10')), now)).toBeNull();
  });

  it('keeps the address clean for the default and says the rest', () => {
    expect(writeSelection({ preset: '30d', customFrom: '', customTo: '' }, new URLSearchParams('range=7d')).toString()).toBe('');
    expect(writeSelection({ preset: 'custom', customFrom: '2026-11-01', customTo: '2026-11-10' }, new URLSearchParams()).toString())
      .toBe('range=custom&from=2026-11-01&to=2026-11-10');
    expect(rangeLength({ from: '2026-11-01', to: '2026-11-10' })).toBe(10);
    expect(previousPeriodLabel(1)).toBe('yesterday');
    expect(previousPeriodLabel(30)).toBe('the previous 30 days');
  });
});

// The demo's Reports figures follow the server's rules (ReportSummaryTests.cs and
// ForecastTests.cs hold the same cases for the server).

import { describe, it, expect, onTestFinished } from 'vitest';
import type { Activity, Deal, Lead } from './types';
import { forecastForDemo, series, summariseForDemo, type DemoRecords } from './reportingDemo';
import type { ReportFilters } from './reporting';

const october: ReportFilters = { from: new Date(2026, 9, 1).toISOString(), to: new Date(2026, 10, 1).toISOString(), scope: 'org' };
const deal = (p: Partial<Deal>): Deal => ({ id: Math.random().toString(36), name: 'D', value: '0', ...p });
const lead = (p: Partial<Lead>): Lead => ({ id: Math.random().toString(36), name: 'L', email: 'l@example.ch', status: 'New', ...p });
const records = (p: Partial<DemoRecords>): DemoRecords => ({ deals: [], leads: [], activities: [], userId: 'me', ...p });
const local = (y: number, m: number, d: number, h = 12) => new Date(y, m - 1, d, h).toISOString();

describe('summariseForDemo', () => {
  it('never adds currencies together, and leads with the open pipeline\'s currency', () => {
    // Years of won EUR deals once hid a live CHF pipeline behind "€0 open".
    const s = summariseForDemo(records({
      deals: [
        deal({ value: '400000', currency: 'EUR', isWon: true, closedAtUtc: local(2025, 3, 1) }),
        deal({ value: '300000', currency: 'chf ', probability: 50 }),
        deal({ value: '1000', currency: 'EUR' }),
        deal({ value: 'to be agreed', currency: 'CHF' }),
      ],
    }), october);

    expect(s.headlineCurrency).toBe('CHF');
    expect(s.open.byCurrency).toEqual([
      { currency: 'CHF', count: 2, value: 300000, unreadable: 1 },
      { currency: 'EUR', count: 1, value: 1000, unreadable: 0 },
    ]);
    expect(s.weighted.find((w) => w.currency === 'CHF')).toEqual({ currency: 'CHF', value: 150000, covered: 1, missing: 1 });
  });

  it('reads European notation the way the server does', () => {
    // The old Reports parser read "€50.000" as fifty.
    const s = summariseForDemo(records({ deals: [deal({ value: '€50.000', currency: 'EUR' })] }), october);
    expect(s.open.byCurrency[0]!.value).toBe(50000);
  });

  it('counts won and lost only in the period they closed in', () => {
    const s = summariseForDemo(records({
      deals: [
        deal({ value: '10000', currency: 'CHF', isWon: true, closedAtUtc: local(2026, 10, 6), createdAtUtc: local(2026, 9, 16) }),
        deal({ value: '30000', currency: 'CHF', isWon: true, closedAtUtc: local(2026, 9, 28) }),
        deal({ value: '5000', currency: 'CHF', isWon: false, closedAtUtc: local(2026, 10, 21) }),
      ],
    }), october);
    expect(s.won.count).toBe(1);
    expect(s.lost.count).toBe(1);
    expect(s.winRate).toBe(0.5);
    expect(s.averageSalesCycleDays).toBe(20);
  });

  it('counts only unconverted leads as active, and the period\'s cohort by status', () => {
    const s = summariseForDemo(records({
      leads: [
        lead({ status: 'New', createdAtUtc: local(2026, 10, 2), leadScore: 40 }),
        lead({ status: 'Won', createdAtUtc: local(2026, 10, 3), isConverted: true, convertedAtUtc: local(2026, 10, 9) }),
        lead({ status: 'New', createdAtUtc: local(2026, 7, 1) }),
      ],
    }), october);
    expect(s.leads.active).toBe(2);
    expect(s.leads.created).toBe(2);
    expect(s.leads.createdConverted).toBe(1);
    expect(s.leads.converted).toBe(1);
    expect(s.leads.averageScore).toBe(40);
    expect(s.leads.byStatus).toEqual([
      { status: 'New', count: 1, converted: 0 },
      { status: 'Won', count: 1, converted: 1 },
    ]);
  });

  it('leaves out the app\'s own system lines and counts activity by local day', () => {
    const activities = [
      { id: 'a', type: 'Call', createdAt: local(2026, 10, 10, 23) },
      { id: 'b', type: 'system', createdAt: local(2026, 10, 10, 9) },
    ] as Activity[];
    const s = summariseForDemo(records({ activities }), october);
    expect(s.activities.total).toBe(1);
    expect(s.activities.byType).toEqual([{ key: 'call', count: 1 }]);
    expect(s.activities.series).toHaveLength(31);
    expect(s.activities.series.find((p) => p.start === '2026-10-10')!.count).toBe(1);
  });

  it('matches deals closing in the period by their day on this browser\'s calendar', () => {
    // Pinned to New York, whose October starts at 04:00 UTC: compared as instants,
    // the deal due on the 1st was left out and the one due on 1 November counted.
    const tz = process.env.TZ;
    process.env.TZ = 'America/New_York';
    onTestFinished(() => {
      if (tz === undefined) delete process.env.TZ;
      else process.env.TZ = tz;
    });
    const s = summariseForDemo(records({
      deals: [
        deal({ value: '100', currency: 'CHF', expectedCloseDateUtc: '2026-10-01T00:00:00.000Z' }),
        deal({ value: '200', currency: 'CHF', expectedCloseDateUtc: '2026-10-31T00:00:00.000Z' }),
        deal({ value: '400', currency: 'CHF', expectedCloseDateUtc: '2026-11-01T00:00:00.000Z' }),
        deal({ value: '800', currency: 'CHF', expectedCloseDateUtc: '2026-09-30T00:00:00.000Z' }),
      ],
    }), { from: new Date(2026, 9, 1).toISOString(), to: new Date(2026, 10, 1).toISOString(), scope: 'org' });

    expect(s.closingInPeriod.byCurrency).toEqual([{ currency: 'CHF', count: 2, value: 300, unreadable: 0 }]);
  });

  it('narrows to the viewer\'s own deals for "me"', () => {
    const s = summariseForDemo(records({
      deals: [deal({ value: '100', assigneeId: 'me' }), deal({ value: '900', assigneeId: 'someone' })],
    }), { ...october, scope: 'me' });
    expect(s.open.count).toBe(1);
  });
});

describe('series', () => {
  it('has a bucket for every week, empty ones included, starting on Mondays', () => {
    const from = new Date(2026, 0, 1).getTime();
    const to = new Date(2026, 3, 1).getTime();
    const points = series([new Date(2026, 1, 4, 10).toISOString()], from, to, 'week');
    expect(points[0]!.start).toBe('2025-12-29');
    expect(points.find((p) => p.start === '2026-02-02')!.count).toBe(1);
    expect(points.reduce((s, p) => s + p.count, 0)).toBe(1);
  });
});

describe('forecastForDemo', () => {
  it('puts each open deal in the month it is expected to close in', () => {
    const f = forecastForDemo(records({
      deals: [
        deal({ value: '10000', currency: 'CHF', probability: 50, expectedCloseDateUtc: '2026-11-20T00:00:00.000Z' }),
        deal({ value: '60000', currency: 'CHF', probability: 60, expectedCloseDateUtc: '2027-03-02T00:00:00.000Z' }),
        deal({ value: '3000', currency: 'CHF' }),
        deal({ value: '8000', currency: 'CHF', expectedCloseDateUtc: '2026-09-01T00:00:00.000Z' }),
      ],
    }), { scope: 'org', firstMonth: '2026-11', months: 3 }, new Date(Date.UTC(2026, 10, 2)));

    expect(f.rows.map((r) => [r.month, r.count, r.weighted])).toEqual([
      ['2026-11', 1, 5000], ['2026-12', 0, 0], ['2027-01', 0, 0],
    ]);
    expect(f.noCloseDate[0]!.count).toBe(1);
    expect(f.overdue[0]!.value).toBe(8000);
  });
});

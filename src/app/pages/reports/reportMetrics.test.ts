// Regression tests for the report figures users reported as wrong.
//
// Each `REGRESSION` case below is a bug that shipped. Each `INVARIANT` is a
// property that, had it been enforced, would have prevented one.

import { describe, it, expect } from 'vitest';
import type { Deal, Lead, Activity } from '@/app/api/types';
import {
  parseMoney, totalsByCurrency, dominantCurrency, dealState, forecast,
  summariseDeals, leadRank, statusFunnel, summariseLeads,
  groupSum, topNWithOther, localDayKey, activityByDay, newLeadsByWeek,
} from './reportMetrics';

const deal = (p: Partial<Deal>): Deal => ({ id: 'd', name: 'D', value: '0', ...p });
/** Last element. `Array.prototype.at` is outside this project's TS lib target. */
const last = <T>(a: T[]): T => a[a.length - 1]!;
const nth = <T>(a: T[], fromEnd: number): T => a[a.length - fromEnd]!;

const lead = (p: Partial<Lead>): Lead => ({ id: 'l', name: 'L', email: 'a@b.c', status: 'New', ...p });

describe('parseMoney', () => {
  it.each([
    ['1000', 1000], ['$1,000', 1000], ['CHF 12 500', 12500], ['', 0],
    [undefined, 0], ['not money', 0], ['-250', -250], ['1000.50', 1000.5],
  ])('%s → %s', (input, expected) => {
    expect(parseMoney(input as string | undefined)).toBe(expected);
  });
});

describe('currency — REGRESSION: mixed currencies were summed and labelled USD', () => {
  const MIXED = [
    deal({ value: '10000', currency: 'CHF' }),
    deal({ value: '5000', currency: 'CHF' }),
    deal({ value: '9000', currency: 'EUR' }),
    deal({ value: '100', currency: 'USD' }),
  ];

  it('totals each currency separately and never adds them together', () => {
    const t = totalsByCurrency(MIXED);
    expect(t).toEqual([
      { currency: 'CHF', value: 15000, count: 2 },
      { currency: 'EUR', value: 9000, count: 1 },
      { currency: 'USD', value: 100, count: 1 },
    ]);
    // The old bug: one figure of 24100 presented as dollars.
    expect(t.some((r) => r.value === 24100)).toBe(false);
  });

  it('reports the dominant currency, and flags that others exist', () => {
    expect(dominantCurrency(MIXED)).toBe('CHF');
    const s = summariseDeals(MIXED);
    expect(s.currency).toBe('CHF');
    expect(s.mixedCurrency).toBe(true);
    expect(s.otherCurrencies.map((c) => c.currency)).toEqual(['EUR', 'USD']);
    // Headline money is CHF-only, so it is a number that actually means something.
    expect(s.openValue).toBe(15000);
  });

  it('treats a missing currency as USD rather than dropping the deal', () => {
    expect(totalsByCurrency([deal({ value: '50' })])).toEqual([
      { currency: 'USD', value: 50, count: 1 },
    ]);
  });

  it('is not fooled by case', () => {
    expect(totalsByCurrency([
      deal({ value: '1', currency: 'chf' }), deal({ value: '2', currency: 'CHF' }),
    ])).toEqual([{ currency: 'CHF', value: 3, count: 2 }]);
  });
});

describe('dealState', () => {
  it.each([
    [deal({ isWon: true }), 'won'],
    [deal({ closedAtUtc: '2026-01-01' }), 'lost'],
    [deal({ closedReasonCategory: 'price' }), 'lost'],
    [deal({}), 'open'],
    // A won deal that also carries a closed date is still won.
    [deal({ isWon: true, closedAtUtc: '2026-01-01' }), 'won'],
  ])('%j → %s', (d, expected) => {
    expect(dealState(d as Deal)).toBe(expected);
  });
});

describe('forecast — REGRESSION: a 20% probability was invented for deals with none', () => {
  it('counts only deals that actually carry a probability', () => {
    const f = forecast([
      deal({ value: '1000', probability: 50 }),
      deal({ value: '1000' }),                    // no probability
      deal({ value: '1000', probability: 100 }),
    ]);
    expect(f.value).toBe(1500);   // 500 + 1000, NOT 1500 + 200
    expect(f.covered).toBe(2);
    expect(f.missing).toBe(1);
  });

  it('reports zero with full disclosure when nobody has set a probability', () => {
    // The old code produced 20% of pipeline here — a confident wrong number.
    // Zero-with-coverage reads as "set your probabilities", which is actionable.
    const f = forecast([deal({ value: '9999' }), deal({ value: '1' })]);
    expect(f.value).toBe(0);
    expect(f.covered).toBe(0);
    expect(f.missing).toBe(2);
  });

  it('ignores closed deals', () => {
    const f = forecast([
      deal({ value: '1000', probability: 50, isWon: true }),
      deal({ value: '1000', probability: 50, closedAtUtc: '2026-01-01' }),
    ]);
    expect(f).toEqual({ value: 0, covered: 0, missing: 0 });
  });
});

describe('summariseDeals', () => {
  it('reports win rate as null rather than 0% before anything closes', () => {
    // 0% and "nothing has closed yet" are different statements. Showing 0%
    // for a healthy new pipeline is the kind of thing that loses trust.
    expect(summariseDeals([deal({ value: '1' })]).winRate).toBeNull();
  });

  it('computes win rate over closed deals only', () => {
    const s = summariseDeals([
      deal({ isWon: true }), deal({ isWon: true }),
      deal({ closedAtUtc: 'x' }), deal({}),
    ]);
    expect(s.winRate).toBeCloseTo(2 / 3);
  });

  it('REGRESSION: average deal size never silently means "open average"', () => {
    // The old tile fell back to the open average when nothing was won, while
    // still being labelled "Avg deal size (won)".
    const noWins = summariseDeals([deal({ value: '5000' }), deal({ value: '3000' })]);
    expect(noWins.avgWonValue).toBeNull();

    const withWins = summariseDeals([
      deal({ value: '5000', isWon: true }), deal({ value: '3000', isWon: true }),
      deal({ value: '999999' }),
    ]);
    expect(withWins.avgWonValue).toBe(4000);
  });

  it('handles no deals at all without dividing by zero', () => {
    const s = summariseDeals([]);
    expect(s.openValue).toBe(0);
    expect(s.winRate).toBeNull();
    expect(s.avgWonValue).toBeNull();
    expect(s.forecast).toEqual({ value: 0, covered: 0, missing: 0 });
  });
});

describe('qualified counting — REGRESSION: the bug that made this number a lie', () => {
  it('does NOT count "Unqualified" as qualified', () => {
    // `status.includes('qualified')` matched "Unqualified". This is the original
    // reported inaccuracy.
    expect(summariseLeads([lead({ status: 'Unqualified' })]).qualifiedOrBeyond).toBe(0);
    expect(leadRank(lead({ status: 'Unqualified' }))).toBe(0);
  });

  it('counts the CURRENT vocabulary, which contains no word "qualified" at all', () => {
    // After the status rename, substring matching found nothing and the figure
    // sat at a permanent zero. These are the labels that actually mean qualified.
    const leads = [
      lead({ status: 'Contract Pending' }),
      lead({ status: 'Awaiting Signature' }),
      lead({ status: 'Signed' }),
      lead({ status: 'Contacted' }),              // not yet qualified
      lead({ status: 'Lost / Not Interested' }),
    ];
    expect(summariseLeads(leads).qualifiedOrBeyond).toBe(3);
  });

  it('still counts an organisation left on the legacy vocabulary', () => {
    expect(summariseLeads([
      lead({ status: 'Qualified' }), lead({ status: 'Open Deal' }),
    ]).qualifiedOrBeyond).toBe(2);
  });

  it('tolerates casing and stray whitespace', () => {
    expect(summariseLeads([lead({ status: '  signed  ' })]).qualifiedOrBeyond).toBe(1);
  });
});

describe('statusFunnel — INVARIANT: a funnel can never widen', () => {
  const MATRIX: Lead[][] = [
    [],
    [lead({ status: 'New' })],
    [lead({ status: 'Lost / Not Interested' })],
    [lead({ status: 'Signed' })],
    [lead({ status: 'New', isConverted: true })],
    [lead({ status: 'Contacted' }), lead({ status: 'Contract Pending' }), lead({ status: 'Signed' })],
    [lead({ status: 'Unqualified' }), lead({ status: 'Awaiting Signature' }), lead({ status: 'New' })],
    Array.from({ length: 20 }, (_, i) => lead({
      status: ['New', 'Attempted Contact', 'Contacted', 'Connected', 'Contract Pending',
        'Awaiting Signature', 'Signed', 'Lost / Not Interested'][i % 8]!,
      isConverted: i % 7 === 0,
    })),
  ];

  it.each(MATRIX.map((l, i) => [i, l]))('case %i never increases step to step', (_i, leads) => {
    const steps = statusFunnel(leads as Lead[]);
    for (let k = 1; k < steps.length; k += 1) {
      expect(steps[k]!.value, `${steps[k]!.name} > ${steps[k - 1]!.name}`)
        .toBeLessThanOrEqual(steps[k - 1]!.value);
    }
  });

  it('starts at the total number of leads', () => {
    const leads = MATRIX[7]!;
    expect(statusFunnel(leads)[0]!.value).toBe(leads.length);
    expect(statusFunnel(leads)[0]!.share).toBe(1);
  });

  it('REGRESSION: a converted lead is never lost from the deepest step', () => {
    // The old funnel took Qualified from the status string and Converted from
    // isConverted, so with Qualified broken it drew 0 qualified then N converted.
    const steps = statusFunnel([lead({ status: 'New', isConverted: true })]);
    expect(steps.map((s) => s.value)).toEqual([1, 1, 1, 1, 1]);
  });

  it('REGRESSION: a lost lead does not count as "Contacted"', () => {
    // "anything not exactly New" counted lost leads as progress.
    expect(statusFunnel([lead({ status: 'Lost / Not Interested' })])[1]!.value).toBe(0);
  });
});

describe('summariseLeads', () => {
  it('averages the score over scored leads only, and says how many', () => {
    const s = summariseLeads([
      lead({ leadScore: 80 }), lead({ leadScore: 40 }), lead({}),
    ]);
    expect(s.avgScore).toBe(60);
    expect(s.scoredCount).toBe(2);
    expect(s.unscoredCount).toBe(1);
  });

  it('reports null, not zero, when nothing is scored', () => {
    const s = summariseLeads([lead({}), lead({})]);
    expect(s.avgScore).toBeNull();
    expect(s.conversionRate).toBe(0);
  });

  it('reports a null conversion rate with no leads at all', () => {
    expect(summariseLeads([]).conversionRate).toBeNull();
  });
});

describe('topNWithOther — REGRESSION: the tail was dropped so pies did not total', () => {
  const rows = groupSum(
    ['a', 'a', 'a', 'b', 'b', 'c', 'd', 'e'].map((name) => ({ name })),
    (r) => r.name, () => 1,
  );

  it('keeps every row accounted for', () => {
    const capped = topNWithOther(rows, 2);
    const before = rows.reduce((s, r) => s + r.value, 0);
    const after = capped.reduce((s, r) => s + r.value, 0);
    expect(after).toBe(before);
    expect(last(capped).name).toBe('Other');
  });

  it('does not add an empty Other row when everything already fits', () => {
    expect(topNWithOther(rows, 99)).toEqual(rows);
    expect(topNWithOther(rows, rows.length).some((r) => r.name === 'Other')).toBe(false);
  });
});

describe('localDayKey — REGRESSION: UTC bucketing vs local labels', () => {
  it('uses the LOCAL calendar day', () => {
    // 23:30 local. toISOString() would report tomorrow anywhere east of UTC,
    // which is how evening activity ended up on the wrong bar.
    const d = new Date(2026, 4, 13, 23, 30);
    expect(localDayKey(d)).toBe('2026-05-13');
    expect(d.getDate()).toBe(13);
  });

  it('zero-pads month and day', () => {
    expect(localDayKey(new Date(2026, 0, 5))).toBe('2026-01-05');
  });
});

describe('activityByDay', () => {
  const now = new Date(2026, 4, 13, 12, 0);
  const act = (d: Date): Activity => ({ id: 'a', type: 'call', createdAt: d.toISOString() });

  it('returns one point per day, oldest first, ending today', () => {
    const s = activityByDay([], 14, now);
    expect(s).toHaveLength(14);
    expect(last(s).name).toBe(now.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }));
  });

  it('buckets a late-evening activity on the day it happened locally', () => {
    const s = activityByDay([act(new Date(2026, 4, 13, 23, 30))], 2, now);
    expect(last(s).value).toBe(1);
    expect(nth(s, 2).value).toBe(0);
  });

  it('ignores unparseable timestamps instead of throwing', () => {
    const s = activityByDay([{ id: 'x', type: 'call', createdAt: 'nonsense' }], 3, now);
    expect(s.reduce((n, p) => n + p.value, 0)).toBe(0);
  });

  it('does not count activity outside the window', () => {
    const s = activityByDay([act(new Date(2026, 3, 1, 10, 0))], 7, now);
    expect(s.reduce((n, p) => n + p.value, 0)).toBe(0);
  });
});

describe('newLeadsByWeek', () => {
  const now = new Date(2026, 4, 13, 12, 0);
  it('returns the requested number of weeks, oldest first', () => {
    expect(newLeadsByWeek([], 8, now)).toHaveLength(8);
  });

  it('counts a lead created today in the last bucket', () => {
    const s = newLeadsByWeek([lead({ createdAtUtc: new Date(now.getTime() - 3600_000).toISOString() })], 8, now);
    expect(last(s).value).toBe(1);
    expect(s.slice(0, -1).every((p) => p.value === 0)).toBe(true);
  });

  it('counts each lead at most once across all buckets', () => {
    const leads = Array.from({ length: 10 }, (_, i) =>
      lead({ createdAtUtc: new Date(now.getTime() - i * 5 * 86_400_000).toISOString() }));
    const total = newLeadsByWeek(leads, 8, now).reduce((n, p) => n + p.value, 0);
    expect(total).toBeLessThanOrEqual(leads.length);
  });

  it('ignores leads with no creation date', () => {
    expect(newLeadsByWeek([lead({})], 4, now).reduce((n, p) => n + p.value, 0)).toBe(0);
  });
});

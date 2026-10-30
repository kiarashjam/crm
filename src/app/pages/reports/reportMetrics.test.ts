// Regression tests for the report figures users reported as wrong.
//
// Each `REGRESSION` case below is a bug that shipped. Each `INVARIANT` is a
// property that, had it been enforced, would have prevented one. The money and
// time-series cases moved to the server with the figures themselves
// (backend/tests/ACI.WebApi.Tests/ReportSummaryTests.cs) and to the demo mirror
// (src/app/api/reportingDemo.test.ts).

import { describe, it, expect } from 'vitest';
import type { Lead } from '@/app/api/types';
import type { LeadStatusRow } from '@/app/api/reporting';
import {
  leadRank, statusFunnel, statusFunnelFromCounts, summariseStatuses, groupSum, topNWithOther, rowFor,
} from './reportMetrics';

/** Last element. `Array.prototype.at` is outside this project's TS lib target. */
const last = <T>(a: T[]): T => a[a.length - 1]!;

const lead = (p: Partial<Lead>): Lead => ({ id: 'l', name: 'L', email: 'a@b.c', status: 'New', ...p });
const rows = (...r: [string, number, number?][]): LeadStatusRow[] =>
  r.map(([status, count, converted]) => ({ status, count, converted: converted ?? 0 }));

describe('qualified counting — REGRESSION: the bug that made this number a lie', () => {
  it('does NOT count "Unqualified" as qualified', () => {
    // `status.includes('qualified')` matched "Unqualified". This is the original
    // reported inaccuracy.
    expect(summariseStatuses(rows(['Unqualified', 1])).qualifiedOrBeyond).toBe(0);
    expect(leadRank(lead({ status: 'Unqualified' }))).toBe(0);
  });

  it('counts the CURRENT vocabulary, which contains no word "qualified" at all', () => {
    // After the status rename, substring matching found nothing and the figure
    // sat at a permanent zero. These are the labels that actually mean qualified.
    expect(summariseStatuses(rows(
      ['Contract Pending', 1], ['Awaiting Signature', 1], ['Signed', 1],
      ['Contacted', 1], ['Lost / Not Interested', 1],
    )).qualifiedOrBeyond).toBe(3);
  });

  it('still counts an organisation left on the legacy vocabulary', () => {
    expect(summariseStatuses(rows(['Qualified', 1], ['Open Deal', 1])).qualifiedOrBeyond).toBe(2);
  });

  it('tolerates casing and stray whitespace', () => {
    expect(summariseStatuses(rows(['  signed  ', 1])).qualifiedOrBeyond).toBe(1);
  });

  it('counts a converted lead as past every step, whatever its status says', () => {
    const s = summariseStatuses(rows(['New', 3, 2]));
    expect(s).toEqual({ total: 3, contactedOrBeyond: 2, qualifiedOrBeyond: 2, converted: 2 });
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

  it('draws the same funnel from the server\'s counts as from the records', () => {
    const leads = MATRIX[7]!;
    const counted = new Map<string, LeadStatusRow>();
    for (const l of leads) {
      const r = counted.get(l.status) ?? { status: l.status, count: 0, converted: 0 };
      r.count += 1;
      if (l.isConverted) r.converted += 1;
      counted.set(l.status, r);
    }
    expect(statusFunnelFromCounts([...counted.values()])).toEqual(statusFunnel(leads));
  });
});

describe('topNWithOther — REGRESSION: the tail was dropped so pies did not total', () => {
  const grouped = groupSum(
    ['a', 'a', 'a', 'b', 'b', 'c', 'd', 'e'].map((name) => ({ name })),
    (r) => r.name, () => 1,
  );

  it('keeps every row accounted for', () => {
    const capped = topNWithOther(grouped, 2);
    const before = grouped.reduce((s, r) => s + r.value, 0);
    const after = capped.reduce((s, r) => s + r.value, 0);
    expect(after).toBe(before);
    expect(last(capped).name).toBe('Other');
  });

  it('does not add an empty Other row when everything already fits', () => {
    expect(topNWithOther(grouped, 99)).toEqual(grouped);
    expect(topNWithOther(grouped, grouped.length).some((r) => r.name === 'Other')).toBe(false);
  });
});

describe('rowFor', () => {
  it('finds a currency\'s row and never borrows another currency\'s', () => {
    const money = [{ currency: 'EUR', count: 2, value: 900, unreadable: 0 }];
    expect(rowFor(money, 'EUR').value).toBe(900);
    expect(rowFor(money, 'CHF')).toEqual({ currency: 'CHF', count: 0, value: 0, unreadable: 0 });
  });
});

import { describe, it, expect } from 'vitest';
import { toCsv } from '@/app/lib/exportCsv';
import type { OwnerRow, ReportSummary } from '@/app/api/reporting';
import { csvFileName, overviewTable, ownerTable } from './reportCsv';

const owner = (p: Partial<OwnerRow>): OwnerRow => ({
  ownerId: null, ownerName: 'Unassigned', currency: 'CHF', openCount: 0, openValue: 0, weighted: 0,
  wonCount: 0, wonValue: 0, lostCount: 0, ...p,
});

describe('report CSV exports', () => {
  it('writes raw numbers, one row per currency, so a spreadsheet can add them up', () => {
    const t = ownerTable([
      owner({ ownerName: 'Anna', currency: 'CHF', openValue: 85500.5, openCount: 2 }),
      owner({ ownerName: 'Anna', currency: 'EUR', openValue: 1000, openCount: 1 }),
    ]);
    const csv = toCsv(t.rows, t.columns).split('\n');
    expect(csv[0]).toBe('Owner,Currency,Open deals,Open value,Weighted,Won in period,Won value,Lost in period');
    expect(csv[1]).toBe('Anna,CHF,2,85500.5,0,0,0,0');
    expect(csv[2]).toBe('Anna,EUR,1,1000,0,0,0,0');
  });

  it('defuses an owner name a spreadsheet would run as a formula', () => {
    // Names reach the CRM from public forms.
    const t = ownerTable([owner({ ownerName: '=HYPERLINK("https://evil.example","x")' })]);
    expect(toCsv(t.rows, t.columns).split('\n')[1]!.startsWith('"\'=HYPERLINK')).toBe(true);
  });

  it('lists every currency of every headline figure', () => {
    const money = (currency: string, value: number) => ({ currency, count: 1, value, unreadable: 0 });
    const summary = {
      open: { count: 2, byCurrency: [money('CHF', 10), money('EUR', 20)] },
      closingInPeriod: { count: 0, byCurrency: [] },
      created: { count: 0, byCurrency: [] },
      won: { count: 1, byCurrency: [money('CHF', 5)] },
      lost: { count: 0, byCurrency: [] },
      weighted: [{ currency: 'CHF', value: 4, covered: 1, missing: 0 }],
    } as unknown as ReportSummary;
    const t = overviewTable(summary);
    expect(t.rows.map((r) => `${r.figure}|${r.currency}|${r.value}`)).toEqual([
      'Open pipeline (now)|CHF|10', 'Open pipeline (now)|EUR|20', 'Won in the period|CHF|5', 'Weighted pipeline|CHF|4',
    ]);
  });

  it('names the file after the table and the period', () => {
    expect(csvFileName('by-owner', '2026-10-01', '2026-10-31')).toBe('cadence-by-owner-2026-10-01-to-2026-10-31.csv');
  });
});

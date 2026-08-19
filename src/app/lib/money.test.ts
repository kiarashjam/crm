// Four places hardcoded USD and summed across currencies. These pin the
// behaviour that replaced them.

import { describe, it, expect } from 'vitest';
import type { Deal } from '@/app/api/types';
import {
  parseAmount, tryParseAmount, currencyOf, formatMoney, sumByCurrency, dominantCurrencyOf, formatDealSum,
} from './money';

const deal = (value: string, currency?: string): Deal =>
  ({ id: 'd', name: 'D', value, ...(currency ? { currency } : {}) });

describe('parseAmount', () => {
  it.each([
    ['1000', 1000], ['CHF 12,500', 12500], ['$1 000', 1000],
    ['', 0], [undefined, 0], ['abc', 0], ['-99', -99], ['12.5', 12.5],
  ])('%s → %s', (input, expected) => {
    expect(parseAmount(input as string | undefined)).toBe(expected);
  });
});

describe('currencyOf', () => {
  it('normalises case and defaults to USD', () => {
    expect(currencyOf({ currency: 'chf' })).toBe('CHF');
    expect(currencyOf({ currency: undefined })).toBe('USD');
  });
});

describe('formatMoney', () => {
  it('uses the currency it is given, not a hardcoded one', () => {
    // The whole point: a CHF amount must not render with a dollar sign.
    const chf = formatMoney(1000, 'CHF');
    expect(chf).toMatch(/CHF|Fr/);
    expect(chf).not.toContain('$');
  });

  it('survives a nonsense currency code instead of throwing', () => {
    // Intl throws on an invalid code. One bad record must not blank a page.
    expect(() => formatMoney(50, 'NOTACURRENCY')).not.toThrow();
    expect(formatMoney(50, 'NOTACURRENCY')).toContain('50');
  });
});

describe('sumByCurrency — REGRESSION: values were added across currencies', () => {
  const MIXED = [deal('10000', 'CHF'), deal('5000', 'CHF'), deal('9000', 'EUR'), deal('100')];

  it('keeps each currency separate, largest first', () => {
    expect(sumByCurrency(MIXED)).toEqual([
      { currency: 'CHF', value: 15000, count: 2 },
      { currency: 'EUR', value: 9000, count: 1 },
      { currency: 'USD', value: 100, count: 1 },
    ]);
  });

  it('never produces the cross-currency total the old helpers did', () => {
    expect(sumByCurrency(MIXED).some((r) => r.value === 24100)).toBe(false);
  });

  it('reports the dominant currency', () => {
    expect(dominantCurrencyOf(MIXED)).toBe('CHF');
    expect(dominantCurrencyOf([])).toBe('USD');
  });
});

describe('formatDealSum', () => {
  it('is just the total when there is one currency', () => {
    const s = formatDealSum([deal('1000', 'CHF'), deal('500', 'CHF')]);
    expect(s).toMatch(/1[,.\s]?500/);
    expect(s).not.toContain('other');
  });

  it('discloses that other currencies were left out rather than adding them', () => {
    // A visibly incomplete total is safer than an invisibly wrong one.
    const s = formatDealSum([deal('10000', 'CHF'), deal('9000', 'EUR')]);
    expect(s).toContain('+1 other');
    expect(s).not.toMatch(/19[,.\s]?000/);
  });

  it('handles an empty column without producing NaN', () => {
    expect(formatDealSum([])).not.toContain('NaN');
  });
});


// These are the SAME cases as MoneyTextTests.cs on the server, which computes the
// same totals from the same free-text column. If the two ever disagree, the
// dashboard and the reports disagree about how much money is in the pipeline.
describe('tryParseAmount — must match the server parser case for case', () => {
  it.each([
    // Shapes that the old strip-everything-but-digits version got wrong.
    ['1.234,56', 1234.56],   // was 1.23456
    ['85.500', 85500],       // was 85.5
    ['1.234.567,89', 1234567.89],
    // Shapes that already worked and must keep working.
    ['CHF 85,500', 85500],
    ['€50,000', 50000],
    ['50 000 EUR', 50000],
    ['GBP 1,200.50', 1200.5],
    ['$1,234.56', 1234.56],
    ['1,234,567.89', 1234567.89],
    ['1234', 1234],
    ['$0', 0],
    // A lone comma is a decimal point only with one or two digits after it.
    ['85,5', 85.5],
    ['85,50', 85.5],
    // A lone dot: grouped thousands only when the integer part is short.
    ['1.234', 1234],
    ['1234.567', 1234.567],
    ['100.00', 100],
    ['1.5', 1.5],
    // Negatives.
    ['-500', -500],
    ['(500)', -500],
    ['-CHF 1,000', -1000],
  ])('reads %s as %s', (input, expected) => {
    expect(tryParseAmount(input)).toBe(expected);
  });

  it('reports null rather than zero when there is no number', () => {
    // The distinction the old version threw away: "worth nothing" and "we could
    // not read this" are different facts, and only one is a data-entry problem.
    for (const bad of [null, undefined, '', '   ', 'TBC', 'to be confirmed', '$', 'CHF', '.', ',']) {
      expect(tryParseAmount(bad), String(bad)).toBeNull();
    }
  });

  it('does not negate on a stray dash', () => {
    expect(tryParseAmount('1000-2000')).toBeGreaterThan(0);
  });

  it('parseAmount still coerces the unreadable to zero, for callers that only add up', () => {
    expect(parseAmount('TBC')).toBe(0);
    expect(parseAmount('1.234,56')).toBe(1234.56);
  });
});

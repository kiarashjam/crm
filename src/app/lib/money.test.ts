// Four places hardcoded USD and summed across currencies. These pin the
// behaviour that replaced them.

import { describe, it, expect } from 'vitest';
import type { Deal } from '@/app/api/types';
import {
  parseAmount, currencyOf, formatMoney, sumByCurrency, dominantCurrencyOf, formatDealSum,
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

// Four places hardcoded USD and summed across currencies, and four parsers read the
// deal's text and disagreed. These pin what replaced them: the stored amount, one
// input parser, and totals kept per currency in the workspace's own format.

import { describe, it, expect } from 'vitest';
import type { Deal } from '@/app/api/types';
import {
  amountInput, currencyOf, currencyOptions, formatDealAmount, formatDealSum, formatMoney, formatTotals,
  parseMoneyInput, regionalOf, sumByCurrency, DEFAULT_CURRENCY,
} from './money';

const deal = (amount: number | null, currency?: string): Pick<Deal, 'amount' | 'currency'> =>
  ({ amount, ...(currency ? { currency } : {}) });

const SWISS = { currency: 'CHF', locale: 'de-CH' };

describe('regionalOf', () => {
  it('is a Swiss club until the workspace says otherwise', () => {
    expect(regionalOf(null)).toEqual({ currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' });
    expect(regionalOf({ currency: 'eur', locale: 'fr-CH', timeZone: 'America/New_York' }))
      .toEqual({ currency: 'EUR', locale: 'fr-CH', timeZone: 'America/New_York' });
  });

  it('falls back on a setting this browser cannot use rather than failing the page', () => {
    expect(regionalOf({ currency: '€', locale: 'xx-YY', timeZone: 'Mars/Olympus' }))
      .toEqual({ currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' });
  });
});

describe('currencyOf', () => {
  it("normalises case, and a deal without one is in the workspace's, not USD", () => {
    expect(currencyOf({ currency: 'chf' })).toBe('CHF');
    expect(currencyOf({ currency: undefined })).toBe(DEFAULT_CURRENCY);
    expect(currencyOf({ currency: undefined }, 'EUR')).toBe('EUR');
  });
});

describe('formatMoney', () => {
  it('writes the amount in the currency it is given, for the locale', () => {
    // The whole point: a CHF amount must not render with a dollar sign.
    const chf = formatMoney(85500, 'CHF', { locale: 'de-CH' });
    expect(chf).toBe(new Intl.NumberFormat('de-CH', { style: 'currency', currency: 'CHF', maximumFractionDigits: 0 }).format(85500));
    expect(chf).not.toContain('$');
  });

  it('shows cents only when there are any, unless asked otherwise', () => {
    expect(formatMoney(99.4, 'CHF', { locale: 'de-CH' })).toContain('99.40');
    expect(formatMoney(100, 'CHF', { locale: 'de-CH' })).not.toContain('.00');
    expect(formatMoney(100, 'CHF', { locale: 'de-CH', cents: 'always' })).toContain('100.00');
    expect(formatMoney(99.4, 'CHF', { locale: 'de-CH', cents: 'never' })).not.toContain('.');
  });

  it('survives a nonsense currency code instead of throwing', () => {
    // Intl throws on an invalid code. One bad record must not blank a page.
    expect(() => formatMoney(50, 'NOTACURRENCY')).not.toThrow();
    expect(formatMoney(50, 'NOTACURRENCY')).toContain('50');
  });
});

describe('formatDealAmount', () => {
  it("writes a deal's stored amount in its own currency", () => {
    expect(formatDealAmount(deal(1200, 'EUR'), SWISS)).toBe(formatMoney(1200, 'EUR', { locale: 'de-CH' }));
  });

  it('says so for a deal saved without a number, rather than inventing zero', () => {
    expect(formatDealAmount(deal(null, 'CHF'), SWISS)).toBe('No amount');
  });
});

describe('sumByCurrency — REGRESSION: values were added across currencies', () => {
  const MIXED = [deal(10000, 'CHF'), deal(5000, 'chf'), deal(9000, 'EUR'), deal(100), deal(null, 'CHF')];

  it('keeps each currency separate, largest first, and leaves out deals with no amount', () => {
    expect(sumByCurrency(MIXED)).toEqual([
      { currency: 'CHF', value: 15100, count: 3 },
      { currency: 'EUR', value: 9000, count: 1 },
    ]);
  });

  it('never produces the cross-currency total the old helpers did', () => {
    expect(sumByCurrency(MIXED).some((r) => r.value === 24100)).toBe(false);
  });

  it('adds to the cent, so line-item style amounts do not drift', () => {
    expect(sumByCurrency([deal(0.1, 'CHF'), deal(0.2, 'CHF')])[0]?.value).toBe(0.3);
  });
});

describe('formatTotals and formatDealSum', () => {
  it('is just the total when there is one currency', () => {
    expect(formatDealSum([deal(1000, 'CHF'), deal(500, 'CHF')], SWISS)).toBe(formatMoney(1500, 'CHF', { locale: 'de-CH' }));
  });

  it('shows every currency on its own rather than adding them', () => {
    const s = formatDealSum([deal(10000, 'CHF'), deal(9000, 'EUR')], SWISS);
    expect(s).toBe(`${formatMoney(10000, 'CHF', { locale: 'de-CH' })} · ${formatMoney(9000, 'EUR', { locale: 'de-CH' })}`);
  });

  it("shows zero in the workspace's currency for nothing, never NaN", () => {
    expect(formatTotals([], { currency: 'EUR', locale: 'de-CH' })).toBe(formatMoney(0, 'EUR', { locale: 'de-CH' }));
    expect(formatDealSum([], SWISS)).not.toContain('NaN');
  });
});

describe('amountInput and currencyOptions', () => {
  it('shows an amount for editing as digits, with cents only when it has them', () => {
    expect(amountInput(85500)).toBe('85500');
    expect(amountInput(99.4)).toBe('99.40');
    expect(amountInput(null)).toBe('');
  });

  it("offers a record's own currency even when the picker's list does not have it", () => {
    expect(currencyOptions('SEK').map((c) => c.code)).toContain('SEK');
    expect(currencyOptions('CHF').filter((c) => c.code === 'CHF')).toHaveLength(1);
  });
});

// These are the SAME cases as MoneyTextTests.cs on the server, which read every deal
// typed before amounts were numbers. What a person types now is read by the same
// rules, so "85'500" means the same in the dialog as it did in the old data.
describe('parseMoneyInput — must match the server parser case for case', () => {
  it.each([
    // Shapes that the old strip-everything-but-digits version got wrong.
    ['1.234,56', 1234.56],   // was 1.23456
    ['85.500', 85500],       // was 85.5
    ['1.234.567,89', 1234567.89],
    // Shapes that already worked and must keep working.
    ['CHF 85,500', 85500],
    ["CHF 85'500", 85500],
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
    // Kept to the cent, as the server stores it.
    ['1234.567', 1234.57],
    ['100.00', 100],
    ['1.5', 1.5],
    // Negatives.
    ['-500', -500],
    ['(500)', -500],
    ['-CHF 1,000', -1000],
  ])('reads %s as %s', (input, expected) => {
    expect(parseMoneyInput(input)).toBe(expected);
  });

  it('reports null rather than zero when there is no number', () => {
    // "Worth nothing" and "could not read this" are different facts, and only one is
    // a typing mistake to point out.
    for (const bad of [null, undefined, '', '   ', 'TBC', 'to be confirmed', '$', 'CHF', '.', ',']) {
      expect(parseMoneyInput(bad), String(bad)).toBeNull();
    }
  });

  it('does not negate on a stray dash', () => {
    expect(parseMoneyInput('1000-2000')).toBeGreaterThan(0);
  });
});

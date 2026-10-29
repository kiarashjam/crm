// The one formatter every club page now goes through.

import { describe, expect, it } from 'vitest';
import { DEFAULT_CLUB_CURRENCY, formatClubMoney, normaliseCurrency } from './clubMoney';

describe('formatClubMoney', () => {
  it('defaults to Swiss francs, never dollars', () => {
    expect(DEFAULT_CLUB_CURRENCY).toBe('CHF');
    const shown = formatClubMoney(1240);
    expect(shown).toMatch(/CHF/);
    expect(shown).not.toContain('$');
  });

  it('keeps the cents: a member owing 320.50 is not shown as 321', () => {
    expect(formatClubMoney(320.5, 'CHF')).toMatch(/320[.,]50/);
    expect(formatClubMoney(-320.5, 'CHF')).toMatch(/320[.,]50/);
  });

  it('shows a balance that rounds to nothing as zero, not as minus zero', () => {
    expect(formatClubMoney(-0.001, 'CHF')).not.toContain('-');
    expect(formatClubMoney(-0.4, 'CHF')).toMatch(/-.*0[.,]40/);
  });

  it('uses the currency it is given', () => {
    expect(formatClubMoney(50, 'EUR')).toContain('€');
  });

  it('survives a code Intl does not know, and a missing number', () => {
    expect(formatClubMoney(12.5, 'XQQ1')).toBe('XQQ1 12.50');
    expect(formatClubMoney(Number.NaN, 'CHF')).toMatch(/0[.,]00/);
  });
});

describe('normaliseCurrency', () => {
  it('upper-cases a stored code and falls back to CHF for anything else', () => {
    expect(normaliseCurrency('eur')).toBe('EUR');
    expect(normaliseCurrency(undefined)).toBe('CHF');
    expect(normaliseCurrency('')).toBe('CHF');
    expect(normaliseCurrency('francs')).toBe('CHF');
  });
});

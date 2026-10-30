// The one formatter every sales page goes through, driven by the workspace's settings.

import { describe, expect, it, vi } from 'vitest';
import { renderHook } from '@testing-library/react';
import { formatMoney } from '@/app/lib/money';

const org = vi.hoisted(() => ({ current: null as null | { currency?: string; locale?: string; timeZone?: string } }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => (org.current ? { currentOrg: org.current } : null),
}));

const { useMoney } = await import('./useMoney');

describe('useMoney', () => {
  it("formats in the workspace's currency and locale, and keeps each deal in its own", () => {
    org.current = { currency: 'EUR', locale: 'fr-CH', timeZone: 'Europe/Paris' };
    const { result } = renderHook(() => useMoney());

    expect(result.current.currency).toBe('EUR');
    expect(result.current.timeZone).toBe('Europe/Paris');
    expect(result.current.format(1200)).toBe(formatMoney(1200, 'EUR', { locale: 'fr-CH' }));
    expect(result.current.formatDeal({ amount: 85500, currency: 'CHF' })).toBe(formatMoney(85500, 'CHF', { locale: 'fr-CH' }));
    // A deal saved without a currency is in the workspace's, not in dollars.
    expect(result.current.formatDeal({ amount: 10 })).toBe(formatMoney(10, 'EUR', { locale: 'fr-CH' }));
  });

  it('totals per currency and never adds francs to euros', () => {
    org.current = { currency: 'CHF', locale: 'de-CH' };
    const { result } = renderHook(() => useMoney());

    const line = result.current.formatDeals([{ amount: 40000, currency: 'CHF' }, { amount: 30000, currency: 'EUR' }]);
    expect(line).toBe(`${formatMoney(40000, 'CHF', { locale: 'de-CH' })} · ${formatMoney(30000, 'EUR', { locale: 'de-CH' })}`);
    expect(line).not.toContain('70');
  });

  it('works in the defaults outside a workspace', () => {
    org.current = null;
    const { result } = renderHook(() => useMoney());

    expect(result.current.currency).toBe('CHF');
    expect(result.current.locale).toBe('de-CH');
    expect(result.current.timeZone).toBe('Europe/Zurich');
  });
});

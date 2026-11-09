// The club currency every club page reads.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { HospitalitySettings } from '@/app/api/hospitalitySettings';

const settings = vi.fn<() => Promise<Partial<HospitalitySettings>>>();
vi.mock('@/app/api/hospitalitySettings', () => ({ getHospitalitySettings: () => settings() }));
const workspace = vi.hoisted(() => ({ current: null as null | { id: string; name: string; locale?: string } }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => (workspace.current ? { currentOrg: workspace.current } : null),
}));

const { useClubMoney, setClubCurrency, resetClubCurrency } = await import('./useClubMoney');

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  settings.mockReset();
  workspace.current = null;
});

describe('useClubMoney', () => {
  it("reads the workspace's currency once, however many pages ask", async () => {
    settings.mockResolvedValue({ currency: 'eur' });
    const first = renderHook(() => useClubMoney());
    const second = renderHook(() => useClubMoney());

    await waitFor(() => expect(first.result.current.currency).toBe('EUR'));
    expect(second.result.current.currency).toBe('EUR');
    // "EUR 12.50" in the workspace's de-CH number format, "€12.50" in some others.
    expect(first.result.current.format(12.5)).toMatch(/€|EUR/);
    expect(settings).toHaveBeenCalledTimes(1);
  });

  it('shows francs while loading, and when the settings cannot be read', async () => {
    settings.mockRejectedValue(new Error('offline'));
    const { result } = renderHook(() => useClubMoney());

    expect(result.current.currency).toBe('CHF');
    await waitFor(() => expect(settings).toHaveBeenCalled());
    expect(result.current.currency).toBe('CHF');
  });

  it('switches open pages over as soon as the settings are saved', async () => {
    settings.mockResolvedValue({});
    const { result } = renderHook(() => useClubMoney());
    await waitFor(() => expect(settings).toHaveBeenCalled());

    act(() => setClubCurrency('GBP'));

    expect(result.current.currency).toBe('GBP');
  });

  it('keeps each workspace apart', async () => {
    settings.mockResolvedValueOnce({ currency: 'EUR' }).mockResolvedValueOnce({ currency: 'CHF' });
    localStorage.setItem('aci_org_id', 'org-paris');
    const paris = renderHook(() => useClubMoney());
    await waitFor(() => expect(paris.result.current.currency).toBe('EUR'));
    paris.unmount();

    localStorage.setItem('aci_org_id', 'org-zurich');
    const zurich = renderHook(() => useClubMoney());
    await waitFor(() => expect(settings).toHaveBeenCalledTimes(2));
    expect(zurich.result.current.currency).toBe('CHF');
  });

  it("writes amounts in the workspace's number format, as the deal pages do", async () => {
    settings.mockResolvedValue({ currency: 'CHF' });
    workspace.current = { id: 'org-1', name: 'Brooklyn Club', locale: 'en-US' };
    const american = renderHook(() => useClubMoney());
    await waitFor(() => expect(settings).toHaveBeenCalled());
    expect(american.result.current.format(1234.5)).toBe(new Intl.NumberFormat('en-US', { style: 'currency', currency: 'CHF' }).format(1234.5));
    american.unmount();

    workspace.current = { id: 'org-1', name: 'Pavillon 46', locale: 'de-CH' };
    const swiss = renderHook(() => useClubMoney());
    expect(swiss.result.current.format(1234.5)).toBe(new Intl.NumberFormat('de-CH', { style: 'currency', currency: 'CHF' }).format(1234.5));
    expect(swiss.result.current.format(1234.5)).not.toBe(american.result.current.format(1234.5));
  });
});

// Ringing up a tab item from the price catalogue.
//
// Every item on a tab was typed in, name and price, so a Negroni cost what whoever took
// the order thought it cost. A menu item or product picked from the catalogue is sent as
// that item, and the server rings it up at the catalogue's price and VAT rate; changing
// its name or price makes it an item typed in, at what was typed.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { OpenTab } from '@/app/api/tabs';
import type { CatalogItem } from '@/app/api/catalog';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: async () => ({}),
}));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [],
}));

const item = (id: string, kind: CatalogItem['kind'], name: string, price: number, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id, kind, name, price, currency: 'CHF', vatRate: 8.1, benefits: [], chargeKind: 'Bar', isActive: true, sortOrder: 0,
  createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', version: 'v1', ...extra,
});
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: async () => [
    item('cat-negroni', 'MenuItem', 'Negroni', 18.5, { category: 'Bar' }),
    item('cat-spritz', 'MenuItem', 'Spritz', 14, { category: 'Bar', isActive: false }),
    item('cat-guest', 'Fee', 'Guest fee', 25),
  ],
}));

const TAB: OpenTab = {
  id: 't1', memberId: 'm1', memberName: 'Jean Dupont', memberTier: 'Gold', venue: 'Bar', items: [], status: 'Open',
  openedAtUtc: new Date().toISOString(), total: 0,
};
const tabs = vi.hoisted(() => ({ get: vi.fn(), add: vi.fn() }));
vi.mock('@/app/api/tabs', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/tabs')>()),
  getTabs: () => tabs.get(),
  addItemToTab: (...a: unknown[]) => tabs.add(...a),
}));

const { default: Tabs } = await import('./Tabs');

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  toast.success.mockReset();
  toast.error.mockReset();
  tabs.get.mockReset().mockResolvedValue([TAB]);
  tabs.add.mockReset().mockResolvedValue(TAB);
});

const openItemEditor = async () => {
  const user = userEvent.setup();
  render(withQueries(<MemoryRouter><Tabs /></MemoryRouter>));
  await user.click((await screen.findAllByRole('button', { name: /Item$/ }))[0]!);
  return user;
};

describe('a tab item from the menu', () => {
  it('is sent as the catalogue item, which the server prices', async () => {
    const user = await openItemEditor();

    await user.click(await screen.findByLabelText('From the menu'));
    // Menu items and products on sale only.
    expect(screen.queryByRole('option', { name: /Spritz/ })).toBeNull();
    expect(screen.queryByRole('option', { name: /Guest fee/ })).toBeNull();
    await user.click(await screen.findByRole('option', { name: /Bar: Negroni/ }));
    expect(screen.getByLabelText('Item name')).toHaveValue('Negroni');
    expect(screen.getByLabelText('Price')).toHaveValue(18.5);
    await user.click(screen.getByRole('button', { name: 'Add' }));

    await vi.waitFor(() => expect(tabs.add).toHaveBeenCalledTimes(1));
    expect(tabs.add.mock.calls[0]![0]).toEqual({ tabId: 't1', name: 'Negroni', quantity: 1, unitPrice: 18.5, catalogItemId: 'cat-negroni' });
  });

  it('becomes an item typed in when its price is changed', async () => {
    const user = await openItemEditor();

    await user.click(await screen.findByLabelText('From the menu'));
    await user.click(await screen.findByRole('option', { name: /Bar: Negroni/ }));
    await user.clear(screen.getByLabelText('Price'));
    await user.type(screen.getByLabelText('Price'), '12');
    await user.click(screen.getByRole('button', { name: 'Add' }));

    await vi.waitFor(() => expect(tabs.add).toHaveBeenCalledTimes(1));
    expect(tabs.add.mock.calls[0]![0]).toMatchObject({ name: 'Negroni', unitPrice: 12, catalogItemId: undefined });
  });
});

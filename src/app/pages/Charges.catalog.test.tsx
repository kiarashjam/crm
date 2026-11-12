// Posting a charge from the price catalogue.
//
// Every charge was typed in: a guest fee cost whatever the front desk typed that day. A
// fee, product, ticket or menu item picked from the catalogue is sent as that item, and
// the server charges its price, kind and VAT rate; the page shows them and does not offer
// to change what the server would not use.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
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

const JEAN: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [JEAN],
}));

const item = (id: string, kind: CatalogItem['kind'], name: string, price: number, extra: Partial<CatalogItem> = {}): CatalogItem => ({
  id, kind, name, price, currency: 'CHF', vatRate: 8.1, benefits: [], chargeKind: 'Other', isActive: true, sortOrder: 0,
  createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', version: 'v1', ...extra,
});
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: async () => [
    item('cat-gold', 'Membership', 'Gold', 299, { cadence: 'Monthly', chargeKind: 'Dues' }),
    item('cat-guest', 'Fee', 'Guest fee', 25),
    item('cat-towel', 'Product', 'Towel', 12, { chargeKind: 'Retail', isActive: false }),
  ],
}));

const charges = vi.hoisted(() => ({ create: vi.fn() }));
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [],
  getPayments: async () => [],
  createCharge: (...a: unknown[]) => charges.create(...a),
}));

const { default: Charges } = await import('./Charges');

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
  charges.create.mockReset().mockResolvedValue({ id: 'c1' });
});

const pickGuestFee = async () => {
  const user = userEvent.setup();
  render(withQueries(<MemoryRouter><Charges /></MemoryRouter>));
  await user.click((await screen.findAllByRole('button', { name: /Post charge/ }))[0]!);
  const dialog = await screen.findByRole('dialog');
  await user.click(within(dialog).getByLabelText('From the price catalogue'));
  // One-off prices on sale: not a tier, nor one taken off sale.
  expect(screen.queryByRole('option', { name: /Gold/ })).toBeNull();
  expect(screen.queryByRole('option', { name: /Towel/ })).toBeNull();
  await user.click(await screen.findByRole('option', { name: /Fee: Guest fee/ }));
  return { user, dialog };
};

describe('a charge from the price catalogue', () => {
  it('is sent as the item, at the price the catalogue shows', async () => {
    const { user, dialog } = await pickGuestFee();

    const amount = within(dialog).getByLabelText(/^Amount/);
    expect(amount).toHaveValue(25);
    // The server charges the catalogue's price: there is nothing to type.
    expect(amount).toBeDisabled();
    expect(within(dialog).getByLabelText('Description')).toHaveValue('Guest fee');
    await user.click(within(dialog).getByRole('button', { name: 'Post charge' }));

    await vi.waitFor(() => expect(charges.create).toHaveBeenCalledTimes(1));
    expect(charges.create.mock.calls[0]![0]).toMatchObject({
      memberId: 'm1', catalogItemId: 'cat-guest', description: 'Guest fee', amount: 25, kind: 'Other',
    });
  });

  it("shows the server's sentence when it refuses the item", async () => {
    charges.create.mockRejectedValue(new Error('Guest fee is no longer on sale, so it cannot be charged.'));
    const { user, dialog } = await pickGuestFee();

    await user.click(within(dialog).getByRole('button', { name: 'Post charge' }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('Guest fee is no longer on sale, so it cannot be charged.'));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

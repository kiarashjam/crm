// Billing a subscription by hand, and starting one from the price catalogue.
//
// The server bills dues in advance, once per calendar period. Pressing Bill on one whose
// next period has not started used to bill it anyway (a cycle ahead, then another), so a
// double press charged a member twice; the server now refuses it as not due, and the page
// asks whether to bill that period early, which it then asks for in so many words. A new
// subscription can start from a tier or dues item, at its price, on a first billing date.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { ApiError } from '@/app/api/apiClient';
import type { Member } from '@/app/api/members';
import type { Subscription } from '@/app/api/subscriptions';
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

const GOLD: CatalogItem = {
  id: 'cat-gold', kind: 'Membership', name: 'Gold', price: 299, currency: 'CHF', vatRate: 0, cadence: 'Monthly',
  initiationFee: 1000, loyaltyMultiplier: 1.5, benefits: [], chargeKind: 'Dues', isActive: true, sortOrder: 2,
  createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: '2026-01-01T00:00:00Z', version: 'v1',
};
const OLD_TIER: CatalogItem = { ...GOLD, id: 'cat-founder', name: 'Founder', isActive: false };
const MENU: CatalogItem = { ...GOLD, id: 'cat-negroni', kind: 'MenuItem', name: 'Negroni', cadence: undefined, price: 18.5 };
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: async () => [GOLD, OLD_TIER, MENU],
}));

const DUES: Subscription = {
  id: 's1', memberId: 'm1', memberName: 'Jean Dupont', name: 'Gold dues', amount: 299, cadence: 'Monthly', status: 'Active',
  nextChargeAtUtc: new Date(Date.now() + 12 * 86_400_000).toISOString(), startedAtUtc: '2026-01-01T00:00:00Z',
};
const subs = vi.hoisted(() => ({ get: vi.fn(), bill: vi.fn(), create: vi.fn() }));
vi.mock('@/app/api/subscriptions', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/subscriptions')>()),
  getSubscriptions: () => subs.get(),
  runBillingCycle: (...a: unknown[]) => subs.bill(...a),
  createSubscription: (...a: unknown[]) => subs.create(...a),
}));

const { default: Subscriptions } = await import('./Subscriptions');

const confirm = vi.fn<(message?: string) => boolean>();
Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const NOT_DUE = "This subscription's next period starts on 22 Nov 2026, so nothing was billed. Bill it early to charge that period now.";

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  toast.success.mockReset();
  toast.error.mockReset();
  confirm.mockReset();
  subs.get.mockReset().mockResolvedValue([DUES]);
  subs.bill.mockReset()
    .mockRejectedValueOnce(new ApiError(NOT_DUE, 409, '/api/subscriptions/s1/bill', true, 'Subscription.NotDue.Conflict'))
    .mockResolvedValue(DUES);
  subs.create.mockReset().mockResolvedValue(DUES);
});

const show = () => render(withQueries(<MemoryRouter><Subscriptions /></MemoryRouter>));

describe('billing a subscription whose period has not started', () => {
  it('asks, and bills the period early only when told to', async () => {
    confirm.mockReturnValue(true);
    const user = userEvent.setup();
    show();

    await user.click((await screen.findAllByRole('button', { name: /Bill$/ }))[0]!);

    await vi.waitFor(() => expect(subs.bill).toHaveBeenCalledTimes(2));
    expect(confirm.mock.calls[0]![0]).toContain(NOT_DUE);
    expect(subs.bill.mock.calls[0]).toEqual(['s1']);
    expect(subs.bill.mock.calls[1]).toEqual(['s1', { early: true }]);
    expect(toast.success).toHaveBeenCalledWith('Next period billed early');
    expect(toast.error).not.toHaveBeenCalled();
  });

  it('bills nothing when the early bill is declined', async () => {
    confirm.mockReturnValue(false);
    const user = userEvent.setup();
    show();

    await user.click((await screen.findAllByRole('button', { name: /Bill$/ }))[0]!);

    await vi.waitFor(() => expect(confirm).toHaveBeenCalledTimes(1));
    expect(subs.bill).toHaveBeenCalledTimes(1);
    expect(toast.success).not.toHaveBeenCalled();
  });
});

describe('a new subscription', () => {
  it('starts from a tier on sale, at its price, on the first billing date given', async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New subscription/ }));
    await user.click(await screen.findByLabelText('Member'));
    await user.click(await screen.findByRole('option', { name: /Jean Dupont/ }));
    await user.click(screen.getByLabelText('From the price catalogue'));
    // Tiers and dues on sale: not one taken off sale, nor a menu item.
    expect(screen.queryByRole('option', { name: /Founder/ })).toBeNull();
    expect(screen.queryByRole('option', { name: /Negroni/ })).toBeNull();
    await user.click(await screen.findByRole('option', { name: /Gold dues/ }));
    expect(screen.getByLabelText('Name')).toHaveValue('Gold dues');
    expect(screen.getByLabelText(/^Amount/)).toHaveValue(299);
    await user.clear(screen.getByLabelText('First billing date'));
    await user.type(screen.getByLabelText('First billing date'), '2026-12-01');
    await user.click(screen.getByRole('button', { name: 'Create subscription' }));

    await vi.waitFor(() => expect(subs.create).toHaveBeenCalledTimes(1));
    expect(subs.create.mock.calls[0]![0]).toMatchObject({
      memberId: 'm1', catalogItemId: 'cat-gold', name: 'Gold dues', amount: 299, cadence: 'Monthly', nextChargeAtUtc: '2026-12-01',
    });
  });

  it("shows the server's sentence when it refuses one", async () => {
    const refusal = 'The Gold tier is no longer offered, so it can\'t be given to a member.';
    subs.create.mockRejectedValue(new ApiError(refusal, 409, '/api/subscriptions', true, 'Catalog.NotOnSale'));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New subscription/ }));
    await user.click(await screen.findByLabelText('Member'));
    await user.click(await screen.findByRole('option', { name: /Jean Dupont/ }));
    await user.click(screen.getByLabelText('From the price catalogue'));
    await user.click(await screen.findByRole('option', { name: /Gold dues/ }));
    await user.click(screen.getByRole('button', { name: 'Create subscription' }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

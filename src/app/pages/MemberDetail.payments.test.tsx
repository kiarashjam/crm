// A member's online payments on their page: the saved card, dues charged to it, a pay link,
// and what Stripe said of each attempt.
//
// The card saved on Stripe's page shows as the member would recognise it and can be removed;
// dues are charged to it only once whoever turns it on confirms the member agreed; a pay
// link is for what they owe or the charges ticked; a declined card says why. Without a Stripe
// key nothing is offered that the server would refuse, and someone who may not take
// payments sees what is there but changes nothing.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { Charge } from '@/app/api/charges';
import type { MemberPaymentProfile, PaymentStatus } from '@/app/api/payments';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/pages/members/MemberConsentPanel', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const demo = vi.hoisted(() => ({ on: false }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => demo.on,
}));
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: async () => ({}),
}));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions },
  };
};

const JEAN: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -339,
};
const DUES: Charge = {
  id: 'c1', memberId: 'm1', memberName: 'Jean Dupont', kind: 'Dues', description: 'Gold dues November',
  amount: 299, paidAmount: 0, status: 'Pending', postedAtUtc: '2026-11-01T00:00:00Z',
};
const BAR: Charge = { ...DUES, id: 'c2', kind: 'Bar', description: 'Bar tab', amount: 40 };
/** Charges a test adds to Jean's account for itself; emptied before each. */
const moreCharges: Charge[] = [];
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [JEAN],
}));
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [DUES, BAR, ...moreCharges],
  getPayments: async () => [],
}));
vi.mock('@/app/api/visits', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/visits')>()), getVisits: async () => [] }));
vi.mock('@/app/api/loyalty', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/loyalty')>()), getLoyaltyLedger: async () => [] }));
vi.mock('@/app/api/giftCards', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/giftCards')>()), getGiftCards: async () => [] }));
vi.mock('@/app/api/communications', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/communications')>()), getCommunications: async () => [] }));
vi.mock('@/app/api/reservations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/reservations')>()),
  getReservations: async () => [],
}));
vi.mock('@/app/api/eventAttendees', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/eventAttendees')>()),
  getMemberRegistrations: async () => [],
}));

const pay = vi.hoisted(() => ({
  status: vi.fn(),
  profile: vi.fn(),
  removeCard: vi.fn(),
  autopay: vi.fn(),
  link: vi.fn(),
  email: vi.fn(),
  charge: vi.fn(),
}));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPaymentStatus: () => pay.status(),
  getMemberPaymentProfile: (...a: unknown[]) => pay.profile(...a),
  removeSavedCard: (...a: unknown[]) => pay.removeCard(...a),
  setAutoPay: (...a: unknown[]) => pay.autopay(...a),
  createPayLink: (...a: unknown[]) => pay.link(...a),
  emailPayLink: (...a: unknown[]) => pay.email(...a),
  chargeSavedCard: (...a: unknown[]) => pay.charge(...a),
}));

const MemberDetail = (await import('./MemberDetail')).default;

const ON: PaymentStatus = { onlinePayments: true, mode: 'live', methods: ['card', 'twint'], webhookConfigured: true, qrBills: true, qrReferenceType: 'QRR', currency: 'CHF' };
const OFF: PaymentStatus = { onlinePayments: false, methods: [], webhookConfigured: false, qrBills: false, currency: 'CHF' };

const WITH_CARD: MemberPaymentProfile = {
  memberId: 'm1', onlinePayments: true,
  card: { type: 'card', brand: 'visa', last4: '4242', expMonth: 4, expYear: 2028, savedAtUtc: '2026-10-03T10:00:00Z' },
  autoPayDues: false, version: 'v1',
  payments: [{
    id: 'op-1', memberId: 'm1', memberName: 'Jean Dupont', provider: 'stripe', purpose: 'autopay', status: 'failed',
    amount: 299, currency: 'CHF', description: 'Gold dues October', failureCode: 'card_declined',
    failureMessage: 'Your card has insufficient funds.', createdAtUtc: '2026-10-01T06:00:00Z',
  }],
};

const confirm = vi.fn(() => true);
Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  moreCharges.length = 0;
  org.current = null;
  demo.on = false;
  confirm.mockClear();
  toast.success.mockReset();
  toast.error.mockReset();
  pay.status.mockReset().mockResolvedValue(ON);
  pay.profile.mockReset().mockResolvedValue(WITH_CARD);
  pay.removeCard.mockReset().mockResolvedValue({ ...WITH_CARD, card: undefined, version: 'v2' });
  pay.autopay.mockReset().mockResolvedValue({ ...WITH_CARD, autoPayDues: true, version: 'v2' });
  pay.link.mockReset().mockResolvedValue({
    url: 'https://club.example.ch/pay/tok-1', purpose: 'charges', amount: 40, currency: 'CHF', expiresAtUtc: '2027-01-09T00:00:00Z',
  });
  pay.email.mockReset();
  pay.charge.mockReset();
});

const show = async () => {
  const user = userEvent.setup();
  render(withQueries(
    <MemoryRouter initialEntries={['/members/m1']}>
      <Routes><Route path="/members/:id" element={<MemberDetail />} /></Routes>
    </MemoryRouter>,
  ));
  await user.click(await screen.findByRole('tab', { name: /House account/ }));
  return { user, card: await screen.findByRole('region', { name: 'Online payments' }) };
};

describe("a member's online payments", () => {
  it('shows the saved card as the member knows it, and why the last charge to it failed', async () => {
    as([PERMISSION.ManageMoney]);
    const { card } = await show();

    expect(await within(card).findByText('Visa ending 4242, expires 04/28')).toBeInTheDocument();
    expect(within(card).getByText('Your card has insufficient funds.')).toBeInTheDocument();
    expect(within(card).getByText('Failed')).toBeInTheDocument();
    expect(pay.profile).toHaveBeenCalledWith('m1', expect.anything());
  });

  it('removes the card once asked to', async () => {
    as([PERMISSION.ManageMoney]);
    const { user, card } = await show();

    await user.click(await within(card).findByRole('button', { name: /Remove card/ }));

    expect(confirm).toHaveBeenCalledWith(expect.stringContaining('Visa ending 4242'));
    await waitFor(() => expect(pay.removeCard).toHaveBeenCalledWith('m1'));
  });

  it("charges dues to the card only once the member's agreement is confirmed, against the version read", async () => {
    as([PERMISSION.ManageMoney]);
    const { user, card } = await show();

    await user.click(await within(card).findByRole('switch', { name: 'Charge dues to the saved card' }));
    expect(pay.autopay).not.toHaveBeenCalled();
    await user.click(await screen.findByRole('button', { name: /Jean Dupont agreed/ }));

    await waitFor(() => expect(pay.autopay).toHaveBeenCalledWith('m1', true, true, 'v1'));
  });

  it('makes a pay link for the charges ticked', async () => {
    as([PERMISSION.ManageMoney]);
    const { user, card } = await show();

    await user.click(await within(card).findByRole('button', { name: /Pay online/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('checkbox', { name: 'Pay Bar tab' }));
    await user.click(within(dialog).getByRole('button', { name: /Make a link to copy/ }));

    await waitFor(() => expect(pay.link).toHaveBeenCalledWith('m1', { purpose: 'charges', chargeIds: ['c2'] }));
    expect(await within(dialog).findByDisplayValue('https://club.example.ch/pay/tok-1')).toBeInTheDocument();
  });

  it("says whose a household member's rolled-up charge is, when ticking what a link is for", async () => {
    // Roll-up puts a spouse's bills on the primary's account, so they are among the primary's
    // open charges; the dialog says whose each is, as the account's charges list does.
    as([PERMISSION.ManageMoney]);
    moreCharges.push({ ...BAR, id: 'c3', kind: 'Spa', description: 'Massage', amount: 120, incurredByMemberId: 'm2', incurredByName: 'Marie Dupont' });
    const { user, card } = await show();

    await user.click(await within(card).findByRole('button', { name: /Pay online/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getAllByText(/^Incurred by /)).toHaveLength(1);
    expect(within(dialog).getByText('Incurred by Marie Dupont')).toBeInTheDocument();
    await user.click(within(dialog).getByRole('checkbox', { name: 'Pay Massage, incurred by Marie Dupont' }));
    await user.click(within(dialog).getByRole('button', { name: /Make a link to copy/ }));

    await waitFor(() => expect(pay.link).toHaveBeenCalledWith('m1', { purpose: 'charges', chargeIds: ['c3'] }));
  });

  it('emails a link for everything owed when nothing is ticked', async () => {
    as([PERMISSION.ManageMoney]);
    pay.email.mockResolvedValue({ url: 'https://club.example.ch/pay/tok-2', purpose: 'account', amount: 339, currency: 'CHF', expiresAtUtc: '2027-01-09T00:00:00Z', emailStatus: 'queued' });
    const { user, card } = await show();

    await user.click(await within(card).findByRole('button', { name: /Pay online/ }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: /Email it to them/ }));

    await waitFor(() => expect(pay.email).toHaveBeenCalledWith('m1', { purpose: 'account' }));
    expect(toast.success).toHaveBeenCalledWith('Link emailed to Jean Dupont');
  });

  it("says why a charge to the card now failed, in Stripe's words", async () => {
    as([PERMISSION.ManageMoney]);
    pay.charge.mockResolvedValue({ ...WITH_CARD.payments[0], id: 'op-2', purpose: 'card', failureMessage: 'Your card was declined.' });
    const { user, card } = await show();

    await user.click(await within(card).findByRole('button', { name: /Charge the card now/ }));

    await waitFor(() => expect(pay.charge).toHaveBeenCalledWith('m1'));
    expect(toast.error).toHaveBeenCalledWith('Your card was declined.');
  });

  it('offers nothing the server would refuse when online payments are not set up', async () => {
    as([PERMISSION.ManageMoney]);
    pay.status.mockResolvedValue(OFF);
    pay.profile.mockResolvedValue({ ...WITH_CARD, onlinePayments: false, card: undefined, payments: [] });
    const { card } = await show();

    expect(await within(card).findByText(/Online payments are not set up/)).toBeInTheDocument();
    expect(within(card).getByRole('link', { name: /Set up Stripe/ })).toHaveAttribute('href', '/hospitality-settings?tab=payments');
    expect(within(card).queryByRole('button', { name: /Pay online/ })).toBeNull();
    expect(within(card).queryByRole('button', { name: /save a card/ })).toBeNull();
  });

  it('says demo data has no online payments, rather than offering to set them up', async () => {
    demo.on = true;
    const { card } = await show();

    expect(await within(card).findByText(/demo data has none/)).toBeInTheDocument();
    expect(within(card).queryByText(/Online payments are not set up/)).toBeNull();
    expect(within(card).queryByRole('button')).toBeNull();
  });

  it('shows someone who may not take payments the card, with nothing to change it', async () => {
    as([PERMISSION.FrontDesk]);
    const { card } = await show();

    expect(await within(card).findByText('Visa ending 4242, expires 04/28')).toBeInTheDocument();
    expect(within(card).queryByRole('button', { name: /Remove card/ })).toBeNull();
    expect(within(card).queryByRole('switch')).toBeNull();
    expect(within(card).queryByRole('button', { name: /Pay online/ })).toBeNull();
  });
});

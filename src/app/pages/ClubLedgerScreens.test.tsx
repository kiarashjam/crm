// The club's money screens on the ledger.
//
// The pages read their lists once and kept them in local state: a failed read left them
// empty, which looks exactly like a club with no charges. Donations were deleted, and a
// gift vanished from the totals. Payments could only be voided whole. And the statement
// was added up in the browser from two lists, leaving out balances brought over,
// corrections and refunds. Now the pages read through the app's cache, a donation is voided
// with a reason, a payment can be refunded in part, and the statement is the server's.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Member } from '@/app/api/members';
import type { AccountStatement, Charge, Payment } from '@/app/api/charges';
import type { Donation } from '@/app/api/donations';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

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

// Against the real API, not the demo's stores.
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: async () => ({}),
}));

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -320.5,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const CHARGE: Charge = {
  id: 'c1', memberId: 'm1', memberName: 'Jean Dupont', kind: 'Dining', description: 'Dinner for two',
  amount: 320.5, paidAmount: 0, status: 'Pending', postedAtUtc: new Date().toISOString(),
};
const PAYMENT: Payment = {
  id: 'p1', memberId: 'm1', memberName: 'Jean Dupont', amount: 100, method: 'Cash',
  receivedAtUtc: new Date().toISOString(), appliedToChargeIds: [],
};
const STATEMENT: AccountStatement = {
  memberId: 'm1', memberName: 'Jean Dupont', currency: 'CHF', fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-12-01T00:00:00Z',
  // Brought forward owing 50; a dinner, a payment, then part of it paid back.
  openingBalance: -50, closingBalance: -295, totalDebits: 345, totalCredits: 100,
  lines: [
    { id: 'c1', atUtc: '2026-11-03T19:00:00Z', entryType: 'Charge', side: 'charge', description: 'Dinner for two', debit: 320, credit: 0, balance: -370 },
    { id: 'p1', atUtc: '2026-11-05T10:00:00Z', entryType: 'Payment', side: 'payment', description: 'Payment (Cash)', debit: 0, credit: 100, balance: -270 },
    { id: 'r1', atUtc: '2026-11-06T10:00:00Z', entryType: 'Refund', side: 'payment', description: 'Refund of payment', debit: 25, credit: 0, balance: -295, reason: 'Overcharged for wine' },
  ],
};
const charges = vi.hoisted(() => ({
  getCharges: vi.fn(),
  getPayments: vi.fn(),
  refundPayment: vi.fn(),
  getMemberStatement: vi.fn(),
}));
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: () => charges.getCharges(),
  getPayments: () => charges.getPayments(),
  refundPayment: (...a: unknown[]) => charges.refundPayment(...a),
  getMemberStatement: (...a: unknown[]) => charges.getMemberStatement(...a),
}));

const DONATION: Donation = {
  id: 'd1', memberId: 'm1', memberName: 'Jean Dupont', cause: 'Scholarship', amount: 500,
  receivedAtUtc: new Date().toISOString(), isAnonymous: false, acknowledged: false,
};
const donations = vi.hoisted(() => ({ getDonations: vi.fn(), voidDonation: vi.fn() }));
vi.mock('@/app/api/donations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/donations')>()),
  getDonations: () => donations.getDonations(),
  voidDonation: (...a: unknown[]) => donations.voidDonation(...a),
}));

const Charges = (await import('./Charges')).default;
const Donations = (await import('./Donations')).default;
const Statements = (await import('./Statements')).default;

const show = (page: React.ReactNode) => render(withQueries(<MemoryRouter>{page}</MemoryRouter>));

// Voids and refunds ask why (and how much) with the browser's prompt, which the test page lacks.
const prompt = vi.fn<(message?: string, value?: string) => string | null>();
Object.defineProperty(window, 'prompt', { value: prompt, configurable: true, writable: true });

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  prompt.mockReset();
  charges.getCharges.mockReset().mockResolvedValue([CHARGE]);
  charges.getPayments.mockReset().mockResolvedValue([PAYMENT]);
  charges.refundPayment.mockReset().mockResolvedValue({ ...PAYMENT, refundedAmount: 25, status: 'PartiallyRefunded' });
  charges.getMemberStatement.mockReset().mockResolvedValue(STATEMENT);
  donations.getDonations.mockReset().mockResolvedValue([DONATION]);
  donations.voidDonation.mockReset().mockResolvedValue({ ...DONATION, status: 'Voided', voidReason: 'Entered twice' });
});

describe('a money screen whose read fails', () => {
  it('says so with a way to try again, instead of showing no charges', async () => {
    as([PERMISSION.ManageMoney]);
    charges.getCharges.mockRejectedValueOnce(new Error('The server is busy.'));
    const user = userEvent.setup();
    show(<Charges />);

    expect(await screen.findByText('House accounts could not be loaded.')).toBeInTheDocument();
    expect(screen.getByText('The server is busy.')).toBeInTheDocument();
    expect(screen.queryByText('No charges yet')).toBeNull();

    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect((await screen.findAllByText('Dinner for two')).length).toBeGreaterThan(0);
    expect(screen.queryByText('House accounts could not be loaded.')).toBeNull();
  });
});

describe('a payment', () => {
  it('can be refunded in part by someone who may void money, with a reason', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    prompt.mockReturnValueOnce('25').mockReturnValueOnce('Overcharged for wine');
    const user = userEvent.setup();
    show(<Charges />);

    await user.click(await screen.findByRole('tab', { name: /Payments/ }));
    await user.click(await screen.findByRole('button', { name: /Refund payment of/ }));

    expect(prompt).toHaveBeenCalledTimes(2);
    await vi.waitFor(() => expect(charges.refundPayment).toHaveBeenCalledWith('p1', 25, 'Overcharged for wine'));
  });

  it('offers no refund to someone who handles money but may not void it', async () => {
    as([PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show(<Charges />);
    await user.click(await screen.findByRole('tab', { name: /Payments/ }));
    expect(await screen.findAllByText(/100/)).not.toHaveLength(0);
    expect(screen.queryByRole('button', { name: /Refund payment of/ })).toBeNull();
  });
});

describe('a payment Stripe took', () => {
  const STRIPE: Payment = { ...PAYMENT, id: 'p2', method: 'Twint', provider: 'stripe', providerPaymentId: 'pi_123' };

  it('says Stripe took it, and how, and is paid back through Stripe', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    charges.getPayments.mockResolvedValue([STRIPE]);
    prompt.mockReturnValueOnce('100').mockReturnValueOnce('Booking cancelled');
    const user = userEvent.setup();
    show(<Charges />);

    await user.click(await screen.findByRole('tab', { name: /Payments/ }));
    expect(await screen.findByText('Stripe')).toBeInTheDocument();
    expect(screen.getByText('TWINT')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Refund payment of/ }));

    expect(prompt.mock.calls[0]![0]).toMatch(/Stripe pays it back/);
    await vi.waitFor(() => expect(charges.refundPayment).toHaveBeenCalledWith('p2', 100, 'Booking cancelled'));
  });

  it('is voided only past a warning that the member is not paid back', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    charges.getPayments.mockResolvedValue([STRIPE]);
    const confirm = vi.fn<(message?: string) => boolean>().mockReturnValue(false);
    Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });
    const user = userEvent.setup();
    show(<Charges />);

    await user.click(await screen.findByRole('tab', { name: /Payments/ }));
    await user.click(await screen.findByRole('button', { name: /Void payment of/ }));

    expect(confirm).toHaveBeenCalledWith(expect.stringMatching(/not paid back/));
    expect(prompt).not.toHaveBeenCalled();
  });
});

describe('a donation', () => {
  it('is voided with a reason and stays on the list, saying why', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    prompt.mockReturnValueOnce('Entered twice');
    const user = userEvent.setup();
    show(<Donations />);

    donations.getDonations.mockResolvedValue([{ ...DONATION, status: 'Voided', voidReason: 'Entered twice' }]);
    await user.click((await screen.findAllByRole('button', { name: /Void donation from Jean Dupont/ }))[0]!);

    await vi.waitFor(() => expect(donations.voidDonation).toHaveBeenCalledWith('d1', 'Entered twice'));
    expect((await screen.findAllByText('Voided: Entered twice')).length).toBeGreaterThan(0);
    // Taken back: nothing is left to thank anyone for, or to void again.
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).toBeNull();
    expect(screen.queryByRole('button', { name: /Void donation/ })).toBeNull();
  });

  it('is not voided without a reason', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    prompt.mockReturnValueOnce('   ');
    const user = userEvent.setup();
    show(<Donations />);
    await user.click((await screen.findAllByRole('button', { name: /Void donation from Jean Dupont/ }))[0]!);
    expect(donations.voidDonation).not.toHaveBeenCalled();
  });
});

describe('the statement', () => {
  it("is the server's, from the ledger: brought forward, each entry with what was owed after it, and refunds", async () => {
    as([PERMISSION.ManageMoney]);
    show(<Statements />);

    const refund = (await screen.findByText('Refund of payment')).closest('tr')!;
    expect(within(refund).getByText(/Refund · Overcharged for wine/)).toBeInTheDocument();
    // What was owed after it, said as owed: the house balance of -295 is 295 owed.
    expect(within(refund).getByText(/295[.,]00/)).toBeInTheDocument();
    const forward = screen.getByText('Balance brought forward').closest('tr')!;
    expect(within(forward).getByText(/50[.,]00/)).toBeInTheDocument();

    // For the member shown and the month on screen.
    const [memberId, fromUtc, toUtc] = charges.getMemberStatement.mock.calls[0]!;
    expect(memberId).toBe('m1');
    expect(Date.parse(toUtc as string)).toBeGreaterThan(Date.parse(fromUtc as string));
  });
});

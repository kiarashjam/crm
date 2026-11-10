// A member's statement as the server's PDF: downloaded, or emailed to them with it attached.
//
// The browser's print view was the only copy of a statement there was: nothing could be
// saved as a file that matched the books, or sent to the member. The server now builds
// the PDF from the ledger, and emails it through the outbox for whoever manages the money.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Member } from '@/app/api/members';
import type { AccountStatement } from '@/app/api/charges';
import type { StatementDelivery } from '@/app/api/billing';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

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
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -299,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const STATEMENT: AccountStatement = {
  memberId: 'm1', memberName: 'Jean Dupont', currency: 'CHF', fromUtc: '2026-11-01T00:00:00Z', toUtc: '2026-12-01T00:00:00Z',
  openingBalance: 0, closingBalance: -299, totalDebits: 299, totalCredits: 0,
  lines: [{ id: 'c1', atUtc: '2026-11-01T03:00:00Z', entryType: 'Charge', side: 'charge', description: 'Gold dues', debit: 299, credit: 0, balance: -299 }],
};
const getMemberStatement = vi.hoisted(() => vi.fn());
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getMemberStatement: (...a: unknown[]) => getMemberStatement(...a),
}));

const billing = vi.hoisted(() => ({ download: vi.fn(), email: vi.fn() }));
vi.mock('@/app/api/billing', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/billing')>()),
  downloadStatementPdf: (...a: unknown[]) => billing.download(...a),
  emailStatement: (...a: unknown[]) => billing.email(...a),
}));

const payments = vi.hoisted(() => ({ status: vi.fn(), email: vi.fn() }));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPaymentStatus: () => payments.status(),
  emailPayLink: (...a: unknown[]) => payments.email(...a),
}));
const NOTHING_SET_UP = { onlinePayments: false, methods: [], webhookConfigured: false, qrBills: false, currency: 'CHF' };

const { default: Statements } = await import('./Statements');

const confirm = vi.fn<(message?: string) => boolean>();
Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });

const delivery = (patch: Partial<StatementDelivery>): StatementDelivery => ({
  id: 'sd1', memberId: 'm1', periodStartUtc: STATEMENT.fromUtc, periodEndUtc: STATEMENT.toUtc, status: 'queued',
  amountDue: 299, currency: 'CHF', ...patch,
});

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  org.current = null;
  toast.success.mockReset();
  toast.error.mockReset();
  confirm.mockReset().mockReturnValue(true);
  getMemberStatement.mockReset().mockResolvedValue(STATEMENT);
  billing.download.mockReset().mockResolvedValue(undefined);
  billing.email.mockReset().mockResolvedValue(delivery({}));
  payments.status.mockReset().mockResolvedValue(NOTHING_SET_UP);
  payments.email.mockReset().mockResolvedValue({
    url: 'https://club.example.ch/pay/t', purpose: 'account', amount: 299, currency: 'CHF', expiresAtUtc: '2027-01-09T00:00:00Z', emailStatus: 'queued',
  });
});

const show = () => render(withQueries(<MemoryRouter><Statements /></MemoryRouter>));

describe("a member's statement as a PDF", () => {
  it('downloads the server-built PDF for the member and the month on screen', async () => {
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(screen.getByRole('button', { name: /Download PDF/ }));

    await vi.waitFor(() => expect(billing.download).toHaveBeenCalledTimes(1));
    // The same member and period the page shows.
    const [memberId, fromUtc, toUtc] = getMemberStatement.mock.calls[0]!;
    expect(billing.download.mock.calls[0]).toEqual([memberId, fromUtc, toUtc]);
    expect(memberId).toBe('m1');
  });

  it("says why when the PDF can't be built", async () => {
    billing.download.mockRejectedValue(new Error('A statement covers at most five years.'));
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(screen.getByRole('button', { name: /Download PDF/ }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('A statement covers at most five years.'));
  });

  it('emails it to the member, for whoever manages the money', async () => {
    as([PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(screen.getByRole('button', { name: /Email statement/ }));

    await vi.waitFor(() => expect(billing.email).toHaveBeenCalledTimes(1));
    expect(confirm.mock.calls[0]![0]).toContain('Jean Dupont');
    expect(billing.email.mock.calls[0]![0]).toBe('m1');
    expect(toast.success).toHaveBeenCalledWith('Statement on its way to jean@example.ch');
  });

  it("says so when the member's email is not sent", async () => {
    as([PERMISSION.ManageMoney]);
    billing.email.mockResolvedValue(delivery({ status: 'suppressed', reason: 'jean@example.ch asked not to get email from the club.' }));
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(screen.getByRole('button', { name: /Email statement/ }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('jean@example.ch asked not to get email from the club.'));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('sends nothing when the email is not confirmed', async () => {
    as([PERMISSION.ManageMoney]);
    confirm.mockReturnValue(false);
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(screen.getByRole('button', { name: /Email statement/ }));

    expect(billing.email).not.toHaveBeenCalled();
  });

  it('offers the download but not the email to someone who does not manage the money', async () => {
    as([PERMISSION.ClubMembers]);
    show();

    await screen.findByText('Gold dues');
    expect(screen.getByRole('button', { name: /Download PDF/ })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Email statement/ })).toBeNull();
  });
});

describe('paying a statement', () => {
  it('sends the member a link to pay what they owe online, when Stripe is set up', async () => {
    as([PERMISSION.ManageMoney]);
    payments.status.mockResolvedValue({ ...NOTHING_SET_UP, onlinePayments: true, methods: ['card', 'twint'], webhookConfigured: true });
    const user = userEvent.setup();
    show();

    await screen.findByText('Gold dues');
    await user.click(await screen.findByRole('button', { name: /Pay online/ }));
    await user.click(await screen.findByRole('button', { name: /Email it to them/ }));

    await vi.waitFor(() => expect(payments.email).toHaveBeenCalledWith('m1', { purpose: 'account' }));
    expect(toast.success).toHaveBeenCalledWith('Link emailed to Jean Dupont');
  });

  it('offers no online payment without Stripe, and says what the QR-bill on the PDF needs', async () => {
    as([PERMISSION.ManageMoney]);
    show();

    await screen.findByText('Gold dues');
    expect(await screen.findByText(/carries no QR-bill yet/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Club settings, Payments' })).toHaveAttribute('href', '/hospitality-settings?tab=payments');
    expect(screen.queryByRole('button', { name: /Pay online/ })).toBeNull();
  });

  it('says the PDF carries a QR-bill matched back by its reference, once the bank details are set', async () => {
    payments.status.mockResolvedValue({ ...NOTHING_SET_UP, qrBills: true, qrReferenceType: 'QRR' });
    show();

    expect(await screen.findByText(/carries a Swiss QR-bill/)).toBeInTheDocument();
  });
});

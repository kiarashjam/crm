// Payments & bank: Stripe's payments across the club, the bank's files and their unmatched
// transfers, and the accounting journal.
//
// A failed online payment stays listed with Stripe's reason, so staff see a declined card.
// A camt file is sent as read; a transfer no reference matched is allocated to the member
// picked, or set aside with a reason, against the version read. The journal's period is
// whole days in the workspace's time zone. Only someone who takes payments imports files or
// exports, and the page says so instead of offering it.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Member } from '@/app/api/members';
import type { BankTransaction, Journal, OnlinePayment, PaymentStatus } from '@/app/api/payments';
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
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions,
      currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich',
    },
  };
};

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const JEAN: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z', duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
const LEA: Member = { ...JEAN, id: 'm2', firstName: 'Léa', lastName: 'Meier', email: 'lea@example.ch' };
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [JEAN, LEA],
}));

const api = vi.hoisted(() => ({
  status: vi.fn(),
  online: vi.fn(),
  imports: vi.fn(),
  transactions: vi.fn(),
  importFile: vi.fn(),
  allocate: vi.fn(),
  ignore: vi.fn(),
  journal: vi.fn(),
  csv: vi.fn(),
}));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPaymentStatus: () => api.status(),
  getOnlinePayments: (...a: unknown[]) => api.online(...a),
  getBankImports: () => api.imports(),
  getBankTransactions: (...a: unknown[]) => api.transactions(...a),
  importBankFile: (...a: unknown[]) => api.importFile(...a),
  allocateBankTransaction: (...a: unknown[]) => api.allocate(...a),
  ignoreBankTransaction: (...a: unknown[]) => api.ignore(...a),
  getJournal: (...a: unknown[]) => api.journal(...a),
  downloadJournalCsv: (...a: unknown[]) => api.csv(...a),
}));

const { default: ClubPayments } = await import('./ClubPayments');

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const prompt = vi.fn<(message?: string) => string | null>();
Object.defineProperty(window, 'prompt', { value: prompt, configurable: true, writable: true });

const ON: PaymentStatus = { onlinePayments: true, mode: 'live', methods: ['card', 'twint'], webhookConfigured: true, qrBills: true, qrReferenceType: 'QRR', currency: 'CHF' };

const DECLINED: OnlinePayment = {
  id: 'op-1', memberId: 'm1', memberName: 'Jean Dupont', provider: 'stripe', purpose: 'autopay', status: 'failed',
  amount: 299, currency: 'CHF', description: 'Gold dues November', methodType: 'card', failureCode: 'card_declined',
  failureMessage: 'Your card has insufficient funds.', createdAtUtc: '2026-11-01T06:00:00Z',
};
const PAID: OnlinePayment = {
  ...DECLINED, id: 'op-2', purpose: 'checkout', status: 'succeeded', methodType: 'twint', failureCode: undefined,
  failureMessage: undefined, ledgerPaymentId: 'p-9', description: 'Account balance',
};

const TRANSFER: BankTransaction = {
  id: 't1', importId: 'i1', bankReference: 'CH44:TX-7', bookedAtUtc: '2026-11-05T00:00:00Z', amount: 120, currency: 'CHF',
  debtorName: 'J. Dupont', debtorIban: 'CH9300762011623852957', message: 'Mitgliederbeitrag', status: 'unmatched', version: 'tv1',
};

const JOURNAL: Journal = {
  fromUtc: '2026-09-30T22:00:00.000Z', toUtc: '2026-10-31T23:00:00.000Z', layout: 'gross', currency: 'CHF',
  rows: [{
    dateUtc: '2026-10-01T06:00:00Z', date: '2026-10-01', document: 'C-1A2B3C4D', debit: '1100', credit: '3400', amount: 299,
    currency: 'CHF', text: 'Gold dues · Jean Dupont', member: 'Jean Dupont', type: 'Charge', entryId: 'e1',
  }],
  accounts: [{ account: '1100', debits: 299, credits: 0, net: 299 }, { account: '3400', debits: 0, credits: 299, net: -299 }],
  mapping: {} as Journal['mapping'],
};

beforeEach(() => {
  localStorage.clear();
  org.current = null;
  toast.success.mockReset();
  toast.error.mockReset();
  prompt.mockReset();
  api.status.mockReset().mockResolvedValue(ON);
  api.online.mockReset().mockResolvedValue([DECLINED, PAID]);
  api.imports.mockReset().mockResolvedValue([]);
  api.transactions.mockReset().mockResolvedValue([TRANSFER]);
  api.importFile.mockReset();
  api.allocate.mockReset().mockResolvedValue({ ...TRANSFER, status: 'allocated', memberId: 'm1', memberName: 'Jean Dupont' });
  api.ignore.mockReset().mockResolvedValue({ ...TRANSFER, status: 'ignored' });
  api.journal.mockReset().mockResolvedValue(JOURNAL);
  api.csv.mockReset().mockResolvedValue(undefined);
});

const show = (tab?: 'bank' | 'export') =>
  render(withQueries(
    <MemoryRouter initialEntries={[tab ? `/club-payments?tab=${tab}` : '/club-payments']}>
      <ClubPayments />
    </MemoryRouter>,
  ));

describe('online payments', () => {
  it("lists them, a declined card with Stripe's reason, and shows those in one state", async () => {
    as([PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Your card has insufficient funds.')).toBeInTheDocument();
    expect(screen.getByText('Dues charged to the saved card')).toBeInTheDocument();
    // The paid one is on the member's account: Stripe confirmed it and the ledger has it.
    expect(screen.getByText("On the member's account")).toBeInTheDocument();
    expect(screen.getByText(/Members can pay online \(Card, TWINT\)/)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Failed' }));
    await waitFor(() => expect(api.online).toHaveBeenLastCalledWith({ status: 'failed' }, expect.anything()));
  });

  it('says when Stripe is not set up, with the way to set it up', async () => {
    as([PERMISSION.ManageMoney]);
    api.status.mockResolvedValue({ ...ON, onlinePayments: false, methods: [], webhookConfigured: false });
    api.online.mockResolvedValue([]);
    show();

    expect(await screen.findByText(/Online payments are not set up/)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Set up Stripe/ })).toHaveAttribute('href', '/hospitality-settings?tab=payments');
    expect(screen.getByText('No online payments yet')).toBeInTheDocument();
  });

  it('warns that without the webhook secret no payment reaches the ledger', async () => {
    as([PERMISSION.ManageMoney]);
    api.status.mockResolvedValue({ ...ON, webhookConfigured: false });
    show();

    expect(await screen.findByText(/never reach the ledger/)).toBeInTheDocument();
  });
});

describe('bank files', () => {
  it("sends the camt file as read, and says what it matched", async () => {
    as([PERMISSION.ManageMoney]);
    api.importFile.mockResolvedValue({
      id: 'i2', fileName: 'camt054.xml', format: 'camt.054', transactions: 3, matched: 2, unmatched: 1, alreadyImported: 0,
      skipped: 0, amountMatched: 598, importedAtUtc: '2026-11-10T09:00:00Z',
    });
    const user = userEvent.setup();
    show('bank');

    await user.upload(await screen.findByLabelText('Bank file'), new File(['<Document>camt</Document>'], 'camt054.xml', { type: 'text/xml' }));

    await waitFor(() => expect(api.importFile).toHaveBeenCalledWith('camt054.xml', '<Document>camt</Document>'));
    expect(await screen.findByRole('status')).toHaveTextContent('2 matched and recorded, 1 to allocate.');
  });

  it("records an unmatched transfer as the payment of the member picked, against the version read", async () => {
    as([PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show('bank');

    expect(api.transactions).toHaveBeenCalledWith('unmatched', expect.anything());
    await user.click(await screen.findByRole('button', { name: /Allocate .* from J\. Dupont/ }));
    const dialog = await screen.findByRole('dialog');
    // The sender's name is searched for first.
    expect(within(dialog).getByLabelText('Find a member')).toHaveValue('J. Dupont');
    await user.click(await within(dialog).findByRole('option', { name: /Jean Dupont/ }));
    await user.click(within(dialog).getByRole('button', { name: /Record as Jean Dupont's payment/ }));

    await waitFor(() => expect(api.allocate).toHaveBeenCalledWith('t1', 'm1', 'tv1'));
  });

  it('sets a transfer aside only with a reason', async () => {
    as([PERMISSION.ManageMoney]);
    prompt.mockReturnValueOnce('   ').mockReturnValueOnce('Rent from the tenant upstairs');
    const user = userEvent.setup();
    show('bank');

    const notMember = await screen.findByRole('button', { name: /Not a member payment/ });
    await user.click(notMember);
    expect(api.ignore).not.toHaveBeenCalled();
    await user.click(notMember);

    await waitFor(() => expect(api.ignore).toHaveBeenCalledWith('t1', 'Rent from the tenant upstairs', 'tv1'));
  });

  it('says transfers have nothing to match by until statements carry a QR-bill', async () => {
    as([PERMISSION.ManageMoney]);
    api.status.mockResolvedValue({ ...ON, qrBills: false });
    show('bank');

    expect(await screen.findByText(/no reference to match/)).toBeInTheDocument();
  });

  it('is not offered to someone who does not take payments, and nothing is asked of the server', async () => {
    as([PERMISSION.FrontDesk]);
    show('bank');

    expect(await screen.findByText(/Only someone who takes payments imports/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Bank file')).toBeNull();
    expect(api.transactions).not.toHaveBeenCalled();
    expect(api.imports).not.toHaveBeenCalled();
  });
});

describe('the accounting export', () => {
  it("previews last month's journal, whole days in the club's time zone, and downloads it", async () => {
    as([PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show('export');

    await user.clear(await screen.findByLabelText('From'));
    await user.type(screen.getByLabelText('From'), '2026-10-01');
    await user.clear(screen.getByLabelText('To (the whole day)'));
    await user.type(screen.getByLabelText('To (the whole day)'), '2026-10-31');
    await user.click(screen.getByRole('button', { name: /Preview/ }));

    // 1 October from midnight in Zurich (summer time) to 1 November's midnight (winter time).
    await waitFor(() => expect(api.journal).toHaveBeenCalledWith('2026-09-30T22:00:00.000Z', '2026-10-31T23:00:00.000Z', 'gross'));
    const preview = await screen.findByRole('region', { name: 'Journal preview' });
    expect(within(preview).getByText('C-1A2B3C4D')).toBeInTheDocument();
    expect(within(preview).getAllByText('3400').length).toBeGreaterThan(0);

    await user.click(screen.getByRole('button', { name: /Download CSV/ }));
    await waitFor(() => expect(api.csv).toHaveBeenCalledWith('2026-09-30T22:00:00.000Z', '2026-10-31T23:00:00.000Z', 'gross'));
  });

  it('is not offered to someone who does not take payments', async () => {
    as([PERMISSION.ClubMembers]);
    show('export');

    expect(await screen.findByText(/Only someone who takes payments exports/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Download CSV/ })).toBeNull();
  });
});

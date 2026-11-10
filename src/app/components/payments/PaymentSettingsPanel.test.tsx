// The payment settings: Stripe, the QR-bill's bank account and the journal's accounts.
//
// Only settings managers see them. A Stripe secret is typed, sent once and never shown again:
// left empty it is kept. Every other field is saved as shown, so the whole form goes with
// each save, including a key's removal; and a save someone else's change overtook is not
// forced through.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import { ApiError } from '@/app/api/apiClient';
import type { AccountingAccounts, PaymentSettings, PaymentSettingsInput, PaymentStatus } from '@/app/api/payments';
import { withQueries } from '@/test/queryWrapper';

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

const demo = vi.hoisted(() => ({ on: false }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => demo.on,
}));

const api = vi.hoisted(() => ({
  settings: vi.fn<() => Promise<PaymentSettings>>(),
  status: vi.fn<() => Promise<PaymentStatus>>(),
  save: vi.fn<(input: PaymentSettingsInput) => Promise<PaymentSettings>>(),
}));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPaymentSettings: () => api.settings(),
  getPaymentStatus: () => api.status(),
  savePaymentSettings: (input: PaymentSettingsInput) => api.save(input),
}));

const { PaymentSettingsPanel } = await import('./PaymentSettingsPanel');

const ACCOUNTS: AccountingAccounts = {
  cash: '1000', bank: '1020', cardClearing: '1090', receivables: '1100', customerPrepayments: '2030', giftCardLiability: '2030',
  vatOwed: '2200', openingBalances: '9100', corrections: '3600', creditCorrections: '3800', giftCardSales: '1000',
  giftCardRedemptions: '3600', giftCardBreakage: '3600',
  revenueByKind: { Dues: '3400', Dining: '3200', Bar: '3200', Event: '3400', Spa: '3400', Booking: '3400', Retail: '3200', Other: '3600' },
  vatCodes: { '8.1': '', '2.6': '', '3.8': '' },
};

const NOT_SET_UP: PaymentSettings = {
  stripeConfigured: false, stripeWebhookSecretSet: false, stripeOfferTwint: false, stripeOfferSepaDebit: false,
  webhookUrl: 'https://api.example.ch/api/public/payments/stripe/0f1e2d3c', webhookEvents: ['checkout.session.completed', 'payment_intent.succeeded'],
  qrBillReady: false, qrBillProblem: "Set the club's IBAN (or QR-IBAN) to print a QR-bill on statements.",
  accounts: ACCOUNTS, currency: 'CHF', version: 'v1',
};

const CONNECTED: PaymentSettings = {
  ...NOT_SET_UP,
  stripeConfigured: true, stripeKeyHint: 'x9Yz', stripeMode: 'test', stripeCheckedAtUtc: '2026-11-10T09:00:00Z',
  iban: 'CH5800791123000889012', creditorName: 'Pavillon 46', creditorPostalCode: '2501', creditorTown: 'Biel', creditorCountry: 'CH',
  qrBillReady: true, qrBillProblem: undefined, version: 'v2',
};

const STATUS: PaymentStatus = { onlinePayments: true, mode: 'live', methods: ['card', 'twint'], webhookConfigured: true, qrBills: false, currency: 'CHF' };

const show = () => render(withQueries(<PaymentSettingsPanel />));

beforeEach(() => {
  localStorage.clear();
  demo.on = false;
  toast.success.mockReset();
  toast.error.mockReset();
  api.settings.mockReset().mockResolvedValue(NOT_SET_UP);
  api.status.mockReset().mockResolvedValue(STATUS);
  api.save.mockReset().mockImplementation(async () => CONNECTED);
});

describe('payment settings for a settings manager', () => {
  it('says what is not set up, and saves the key typed with the whole form, against the version read', async () => {
    as([PERMISSION.ManageSettings]);
    const user = userEvent.setup();
    show();

    expect(await screen.findByText(/Not set up: members cannot pay online/)).toBeInTheDocument();
    expect(screen.getByText(/Set the club's IBAN/)).toBeInTheDocument();
    expect(screen.getByTestId('webhook-url')).toHaveTextContent('https://api.example.ch/api/public/payments/stripe/0f1e2d3c');
    expect(screen.getByText('payment_intent.succeeded')).toBeInTheDocument();

    await user.type(screen.getByLabelText('Stripe secret key'), 'sk_test_51abcdefghijklmnopqrstuvwxyz');
    await user.type(screen.getByLabelText('IBAN'), 'CH58 0079 1123 0008 8901 2');
    await user.type(screen.getByLabelText('Creditor name'), 'Pavillon 46');
    await user.click(screen.getByRole('button', { name: /Save payment settings/ }));

    await waitFor(() => expect(api.save).toHaveBeenCalledTimes(1));
    const sent = api.save.mock.calls[0]![0];
    expect(sent).toEqual(expect.objectContaining({
      stripeSecretKey: 'sk_test_51abcdefghijklmnopqrstuvwxyz', iban: 'CH58 0079 1123 0008 8901 2', creditorName: 'Pavillon 46',
      creditorCountry: 'CH', qrIban: null, version: 'v1', accounts: ACCOUNTS,
    }));
    // The signing secret was not typed: it is left out, so the server keeps what it has.
    expect(sent).not.toHaveProperty('stripeWebhookSecret');
  });

  it("says in test mode that test payments land on members' accounts, and what going live forgets", async () => {
    as([PERMISSION.ManageSettings]);
    api.settings.mockResolvedValue(CONNECTED);
    show();

    expect(await screen.findByText(/test payments are recorded on members' accounts/)).toBeInTheDocument();
    expect(screen.getByText(/Saving a live key forgets the cards and customers saved in test mode/)).toBeInTheDocument();
  });

  it('asks for a restricted key that may also remove saved cards', async () => {
    as([PERMISSION.ManageSettings]);
    show();

    expect(await screen.findByText(/PaymentIntents, PaymentMethods, SetupIntents and Refunds/)).toBeInTheDocument();
  });

  it("warns that payments are never heard of without the webhook secret, and removes the key without dropping the bank's", async () => {
    as([PERMISSION.ManageSettings]);
    api.settings.mockResolvedValue(CONNECTED);
    const confirm = vi.fn(() => true);
    Object.defineProperty(window, 'confirm', { value: confirm, configurable: true, writable: true });
    const user = userEvent.setup();
    show();

    expect(await screen.findByText(/Connected in test mode with the key ending x9Yz/)).toBeInTheDocument();
    expect(screen.getByText(/never reach the ledger/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Remove key/ }));

    await waitFor(() => expect(api.save).toHaveBeenCalledTimes(1));
    expect(api.save.mock.calls[0]![0]).toEqual(expect.objectContaining({
      stripeSecretKey: '', iban: 'CH5800791123000889012', creditorTown: 'Biel', version: 'v2',
    }));
  });

  it("offers TWINT only for a club in CHF", async () => {
    as([PERMISSION.ManageSettings]);
    api.settings.mockResolvedValue({ ...CONNECTED, currency: 'EUR' });
    show();

    expect(await screen.findByRole('switch', { name: 'Offer TWINT' })).toBeDisabled();
    expect(screen.getByRole('switch', { name: 'Offer SEPA direct debit' })).toBeEnabled();
  });

  it("reads the settings again when someone else's save overtook this one, rather than forcing it through", async () => {
    as([PERMISSION.ManageSettings]);
    api.save.mockRejectedValue(new ApiError('Someone else changed the payment settings at the same moment, so nothing was saved. Reload and try again.', 409));
    const user = userEvent.setup();
    show();

    await user.type(await screen.findByLabelText('IBAN'), 'CH5800791123000889012');
    api.settings.mockResolvedValue({ ...NOT_SET_UP, iban: 'CH9300762011623852957', version: 'v3' });
    await user.click(screen.getByRole('button', { name: /Save payment settings/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/Someone else changed/)));
    expect(await screen.findByDisplayValue('CH9300762011623852957')).toBeInTheDocument();
  });
});

describe('payment settings for anyone else', () => {
  it('shows only what is on, and never asks for the settings', async () => {
    as([PERMISSION.ManageMoney]);
    show();

    expect(await screen.findByText(/Members can pay online \(Card, TWINT\)/)).toBeInTheDocument();
    expect(screen.getByText(/Statements carry no QR-bill/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Stripe secret key')).toBeNull();
    expect(api.settings).not.toHaveBeenCalled();
  });

  it('says demo data has no payments', async () => {
    demo.on = true;
    as([PERMISSION.ManageSettings]);
    show();

    expect(await screen.findByText(/demo data has none/)).toBeInTheDocument();
    expect(api.settings).not.toHaveBeenCalled();
  });
});

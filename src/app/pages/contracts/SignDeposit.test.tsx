// The deposit on the signing page, as the signer meets it: said before they sign, offered
// once they have, through Stripe's page or the club's QR-bill, and shown paid once it is.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { PublicContract, PublicDeposit } from '@/app/api/contracts';

const api = {
  contract: null as PublicContract | null,
  checkout: vi.fn(),
};

vi.mock('@/app/api/contracts', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/contracts')>('@/app/api/contracts');
  return {
    ...actual,
    getPublicContract: () => Promise.resolve(api.contract!),
    startDepositCheckout: (...args: unknown[]) => api.checkout(...args),
    publicContractPdfUrl: (t: string) => `/api/public/contracts/${t}/pdf`,
    publicDepositQrBillUrl: (t: string, lang?: string) => `/api/public/contracts/${t}/deposit/qr-bill?lang=${lang ?? ''}`,
  };
});

const SignContract = (await import('../SignContract')).default;
const { browser } = await import('./SignDeposit');

const deposit = (over: Partial<PublicDeposit> = {}): PublicDeposit => ({
  amount: 500,
  currency: 'CHF',
  status: 'awaiting',
  payable: true,
  methods: ['card', 'twint'],
  qrBill: false,
  locale: 'en-US',
  ...over,
});

const signed = (over: Partial<PublicContract> = {}): PublicContract => ({
  status: 'signed_by_client',
  title: 'Membership Agreement',
  body: 'The whole agreement, in full.',
  counterpartyName: 'Anna Muster',
  organizationName: 'Pavillon 46',
  sentAtUtc: '2026-11-01T09:00:00Z',
  clientSignatureName: 'Anna Muster',
  clientSignedAtUtc: '2026-11-02T09:00:00Z',
  canSign: false,
  membershipTier: 'Gold',
  deposit: deposit(),
  ...over,
});

const show = (path = '/sign/tok123') => render(
  <MemoryRouter initialEntries={[path]}>
    <Routes><Route path="/sign/:token" element={<SignContract />} /></Routes>
  </MemoryRouter>,
);

beforeEach(() => {
  api.contract = signed();
  api.checkout.mockReset();
  browser.go = vi.fn();
});

describe('the deposit on the signing page', () => {
  it('is said before signing, and paid only once signed', async () => {
    api.contract = signed({
      status: 'sent', clientSignatureName: undefined, clientSignedAtUtc: undefined, canSign: true,
      deposit: deposit({ payable: false }),
    });
    show();

    expect(await screen.findByText(/a deposit of CHF 500\.00 is payable once you have signed/i)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /pay CHF 500\.00 online/i })).toBeNull();
  });

  it('once signed, Pay opens Stripe’s page for the token', async () => {
    api.checkout.mockResolvedValue({ url: 'https://checkout.stripe.com/c/pay/cs_test_1' });
    show();
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: /pay CHF 500\.00 online/i }));

    await waitFor(() => expect(browser.go).toHaveBeenCalledWith('https://checkout.stripe.com/c/pay/cs_test_1'));
    expect(api.checkout).toHaveBeenCalledWith('tok123');
  });

  it('says why when Stripe’s page cannot be opened, and lets them try again', async () => {
    const { ContractError } = await import('@/app/api/contracts');
    api.checkout.mockRejectedValue(new ContractError('The deposit is paid.'));
    show();
    const user = userEvent.setup();

    await user.click(await screen.findByRole('button', { name: /pay CHF 500\.00 online/i }));

    expect(await screen.findByRole('alert')).toHaveTextContent('The deposit is paid.');
    expect(browser.go).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: /pay CHF 500\.00 online/i })).toBeEnabled();
  });

  it('without online payments offers the QR-bill, with the account and reference to type', async () => {
    api.contract = signed({
      deposit: deposit({
        methods: [], qrBill: true, iban: 'CH44 3199 9123 0008 8901 2', creditor: 'Pavillon 46', reference: '21 00000 00003 13947 14300 09017',
      }),
    });
    show();

    const link = await screen.findByRole('link', { name: /download the qr-bill/i });
    expect(link.getAttribute('href')).toContain('/api/public/contracts/tok123/deposit/qr-bill');
    expect(screen.getByText('CH44 3199 9123 0008 8901 2')).toBeTruthy();
    expect(screen.getByText('21 00000 00003 13947 14300 09017')).toBeTruthy();
    expect(screen.queryByRole('button', { name: /online/i })).toBeNull();
  });

  it('shows Stripe’s reason for a payment that failed', async () => {
    api.contract = signed({ deposit: deposit({ failureMessage: 'Your card was declined.' }) });
    show();

    expect(await screen.findByText(/your last payment did not go through: your card was declined\./i)).toBeTruthy();
  });

  it('back from Stripe, says the payment is being confirmed, then that it is paid', async () => {
    api.contract = signed();
    show('/sign/tok123?deposit=done');
    expect(await screen.findByText(/your payment is being confirmed/i)).toBeTruthy();

    // The webhook has arrived: the page reads the contract again by itself.
    api.contract = signed({ deposit: deposit({ status: 'paid', payable: false, paidAtUtc: '2026-11-02T10:00:00Z' }) });
    expect(await screen.findByText(/your deposit of CHF 500\.00 is paid/i, undefined, { timeout: 5000 })).toBeTruthy();
    expect(screen.queryByRole('button', { name: /online/i })).toBeNull();
  }, 10000);
});

// The contract panel on a deal: the deal's contracts, a draft that takes its fee from the
// deal, the membership tier and deposit chosen on it, and where each contract's deposit went.

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Contract } from '@/app/api/contracts';
import type { CatalogItem } from '@/app/api/catalog';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), warning: vi.fn(), error: vi.fn() } }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/api/apiClient', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/apiClient')>('@/app/api/apiClient');
  return { ...actual, isUsingRealApi: () => true, servesDemoData: () => false };
});

const api = {
  forDeal: [] as Contract[],
  forLead: vi.fn(),
  draft: vi.fn(),
};
vi.mock('@/app/api/contracts', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/contracts')>('@/app/api/contracts');
  return {
    ...actual,
    listContractsForDeal: () => Promise.resolve(api.forDeal),
    listContractsForLead: (...args: unknown[]) => api.forLead(...args),
    createContractDraft: (...args: unknown[]) => api.draft(...args),
  };
});

const tier = (name: string, sortOrder: number): CatalogItem => ({
  id: `tier-${name}`, kind: 'Membership', name, isActive: true, sortOrder, price: 100, currency: 'CHF', benefits: [],
} as unknown as CatalogItem);
vi.mock('@/app/api/catalog', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/catalog')>('@/app/api/catalog');
  return { ...actual, getCatalog: () => Promise.resolve([tier('Silver', 1), tier('Gold', 2)]) };
});

// Whether the signed-in member may take payments (money.manage); everything else they may.
let mayTakePayments = true;
vi.mock('@/app/hooks/useMay', () => ({
  useMay: () => (permission?: string) => permission !== 'money.manage' || mayTakePayments,
}));

const { ContractPanel } = await import('./ContractPanel');

const contract = (over: Partial<Contract>): Contract => ({
  id: 'c1',
  dealId: 'd1',
  leadId: 'l1',
  status: 'countersigned',
  title: 'Membership Agreement — Anna Muster',
  body: 'The whole agreement.',
  counterpartyName: 'Anna Muster',
  counterpartyEmail: 'anna@example.ch',
  createdAtUtc: '2026-11-01T09:00:00Z',
  updatedAtUtc: '2026-11-02T09:00:00Z',
  allowedActions: [],
  unresolvedFields: [],
  events: [],
  ...over,
});

const show = () => render(
  <MemoryRouter><ContractPanel dealId="d1" leadName="Anna Muster" /></MemoryRouter>,
);

beforeEach(() => {
  api.forDeal = [];
  api.forLead.mockReset();
  api.draft.mockReset();
  mayTakePayments = true;
});

describe('the contract panel on a deal', () => {
  it('lists the deal’s contracts, with the tier and the deposit on the member’s account', async () => {
    api.forDeal = [contract({
      membershipTier: 'Gold', depositAmount: 500, depositCurrency: 'CHF',
      deposit: {
        amount: 500, currency: 'CHF', status: 'paid', amountPaid: 500, amountRefunded: 0, provider: 'stripe', methodType: 'twint',
        paidAtUtc: '2026-11-02T10:00:00Z', memberId: 'm1', memberName: 'Anna Muster', ledgerPaymentId: 'p1',
      },
    })];
    show();

    expect(await screen.findByText('Gold membership')).toBeTruthy();
    const line = screen.getByTestId('deposit-line');
    expect(line).toHaveTextContent(/Paid/);
    expect(line).toHaveTextContent(/with TWINT/);
    expect(screen.getByRole('link', { name: /Anna Muster/ }).getAttribute('href')).toBe('/members/m1');
    expect(api.forLead).not.toHaveBeenCalled();
  });

  it('says a paid deposit is kept until the club admits the signer', async () => {
    api.forDeal = [contract({
      status: 'signed_by_client',
      deposit: { amount: 500, currency: 'CHF', status: 'paid', amountPaid: 500, amountRefunded: 0, provider: 'bank', methodType: 'bank' },
    })];
    show();

    expect(await screen.findByText(/kept until the club admits them/i)).toBeTruthy();
    expect(screen.getByTestId('deposit-line')).toHaveTextContent(/by bank transfer/);
  });

  it('drafts on the deal, at the tier chosen, asking the deposit typed, without asking the fee', async () => {
    api.draft.mockResolvedValue(contract({ status: 'draft', membershipTier: 'Gold', depositAmount: 500, depositCurrency: 'CHF' }));
    show();
    const user = userEvent.setup();

    await screen.findByRole('button', { name: /generate the draft contract/i });
    expect(screen.queryByText('Fee')).toBeNull();
    await user.selectOptions(await screen.findByRole('combobox'), 'Gold');
    await user.type(screen.getByPlaceholderText(/e\.g\. 500/), '500');
    await user.click(screen.getByRole('button', { name: /generate the draft contract/i }));

    await waitFor(() => expect(api.draft).toHaveBeenCalledTimes(1));
    const args = api.draft.mock.calls[0]![0] as Record<string, unknown>;
    expect(args).toMatchObject({ dealId: 'd1', membershipTier: 'Gold', depositAmount: 500 });
    expect(args).not.toHaveProperty('leadId');
    expect(Object.keys(args.values as object)).not.toContain('contract.fee');
  });

  it('offers no deposit to someone who may not take payments', async () => {
    mayTakePayments = false;
    show();

    await screen.findByRole('button', { name: /generate the draft contract/i });
    expect(await screen.findByRole('combobox')).toBeTruthy();
    expect(screen.queryByText(/deposit on signing/i)).toBeNull();
  });
});

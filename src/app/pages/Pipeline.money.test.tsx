// The deals board's money: each deal's stored amount in its own currency, totals kept
// per currency and never added across them, and a new deal priced in the workspace's
// currency. The board stripped each value's text to digits and added everything up,
// so CHF 40,000 and EUR 30,000 showed as "CHF 70,000", and the New Deal dialog opened
// with no currency at all.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Deal } from '@/app/api/types';
import { formatMoney } from '@/app/lib/money';

const api = vi.hoisted(() => ({
  getDeals: vi.fn(), updateDeal: vi.fn(), createDeal: vi.fn(), deleteDeal: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getPipelines: vi.fn(), getOrgMembers: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/activities', () => ({ getActivitiesByDeal: async () => [], createActivity: vi.fn() }));
vi.mock('@/app/api/tasks', () => ({ getTasks: async () => [], createTask: vi.fn() }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, DEAL_DUPLICATES: {} }));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org }),
  useOrgOptional: () => ({ currentOrg: org }),
}));

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Pipeline = (await import('./Pipeline')).default;

const deal = (over: Partial<Deal>): Deal => ({ id: Math.random().toString(36).slice(2), name: 'Deal', value: '0', stage: 'Proposal', ...over });
// Intl writes a no-break space after the code; the page's text is read with plain spaces.
const flat = (s: string) => s.replace(/\s+/g, ' ');
const chf = (n: number) => flat(formatMoney(n, 'CHF', { locale: 'de-CH', cents: 'never' }));
const eur = (n: number) => flat(formatMoney(n, 'EUR', { locale: 'de-CH', cents: 'never' }));

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  org.currency = 'CHF';
  api.getContacts.mockResolvedValue([]);
  api.getCompanies.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([]);
  api.getOrgMembers.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([
    deal({ name: 'Fondation Léman', value: '40000', amount: 40000, currency: 'CHF' }),
    deal({ name: 'Salon de Genève', value: '30000', amount: 30000, currency: 'EUR', stage: 'Negotiation' }),
    // Typed before amounts were numbers: its text says nothing to read.
    deal({ name: 'Vague', value: 'to be agreed', amount: null, currency: 'CHF' }),
    deal({ name: 'Signed gala', value: '10000', amount: 10000, currency: 'CHF', stage: 'Closed Won', isWon: true }),
  ]);
});

// The board loads four lists before it draws anything; wait for a deal on it.
const show = async () => {
  render(<MemoryRouter><Pipeline /></MemoryRouter>);
  await screen.findAllByText('Fondation Léman', undefined, { timeout: 5000 });
};

describe('the deals board', () => {
  it('totals the open pipeline per currency and never adds francs to euros', async () => {
    await show();

    // The header badge: open deals only, one figure per currency.
    expect(screen.getByText(`${chf(40000)} · ${eur(30000)}`)).toBeInTheDocument();
    // The tile names the other currency and what was won beside its own figure.
    expect(screen.getByText(new RegExp(`and ${eur(30000)} · ${chf(10000)} won`))).toBeInTheDocument();
    expect(screen.queryByText(new RegExp(chf(70000)))).not.toBeInTheDocument();
    expect(screen.queryByText(new RegExp(chf(80000)))).not.toBeInTheDocument();
  });

  it("shows each deal's own amount in its own currency, and says when there is none", async () => {
    await show();

    expect(screen.getAllByText(eur(30000)).length).toBeGreaterThan(0);
    expect(screen.getByText('No amount')).toBeInTheDocument();
  });

  it("prices a new deal in the workspace's currency and reads the amount as typed", async () => {
    org.currency = 'EUR';
    api.createDeal.mockResolvedValue(deal({ name: 'Brunch', value: '12500', amount: 12500, currency: 'EUR' }));
    await show();

    fireEvent.click(await screen.findByRole('button', { name: /new deal/i }));
    fireEvent.change(await screen.findByLabelText(/deal name/i), { target: { value: 'Brunch' } });
    fireEvent.change(screen.getByLabelText(/^value/i), { target: { value: "12'500" } });
    fireEvent.click(screen.getByRole('button', { name: /create deal/i }));

    await waitFor(() => expect(api.createDeal).toHaveBeenCalled());
    expect(api.createDeal.mock.calls[0]?.[0]).toMatchObject({ name: 'Brunch', amount: 12500, currency: 'EUR' });
  });

  it('refuses an amount with no number in it before asking the server', async () => {
    await show();

    fireEvent.click(await screen.findByRole('button', { name: /new deal/i }));
    fireEvent.change(await screen.findByLabelText(/deal name/i), { target: { value: 'Brunch' } });
    fireEvent.change(screen.getByLabelText(/^value/i), { target: { value: 'to be agreed' } });
    fireEvent.click(screen.getByRole('button', { name: /create deal/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/as a number/)));
    expect(api.createDeal).not.toHaveBeenCalled();
  });
});

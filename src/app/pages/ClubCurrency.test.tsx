// Club amounts in the club's currency, to the cent.
//
// Each club page had its own formatter with `currency: 'USD'`, so a Swiss club's
// printed statement read "$320.50", and the members list also rounded to whole units:
// the same balance read "-$321" there. The currency now comes from the hospitality
// settings (CHF unless the club chose another), through one formatter.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { Charge } from '@/app/api/charges';
import type { HospitalitySettings } from '@/app/api/hospitalitySettings';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const settings = vi.fn<() => Promise<Partial<HospitalitySettings>>>();
vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: () => settings(),
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
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [CHARGE],
  getPayments: async () => [],
}));

const Statements = (await import('./Statements')).default;
const Members = (await import('./Members')).default;

const show = (page: React.ReactNode) => render(<MemoryRouter>{page}</MemoryRouter>);

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
  settings.mockReset().mockResolvedValue({});
});

describe('the printed statement', () => {
  it('is in Swiss francs when the club has not chosen a currency', async () => {
    show(<Statements />);

    expect(await screen.findByText('Dinner for two')).toBeInTheDocument();
    const text = document.body.textContent ?? '';
    expect(text).toMatch(/CHF\s?320[.,]50/);
    expect(text).not.toContain('$');
  });

  it('is in the currency the club chose', async () => {
    settings.mockResolvedValue({ currency: 'EUR' });
    show(<Statements />);

    expect(await screen.findByText('Dinner for two')).toBeInTheDocument();
    await screen.findAllByText(/€\s?320[.,]50/);
    expect(document.body.textContent).not.toContain('$');
  });
});

describe('the members list', () => {
  it('shows the house balance to the cent, like the statement does', async () => {
    show(<Members />);

    expect((await screen.findAllByText(/320[.,]50/)).length).toBeGreaterThan(0);
    const text = document.body.textContent ?? '';
    expect(text).not.toMatch(/321(?![.,]\d)/);
    expect(text).not.toContain('$');
  });
});

// The POS terminal's gift card and points tiles.
//
// Redeeming a gift card or points here used up the card's value (or the points) and
// toasted "Redeemed", but no payment was recorded and no charge or tab was settled:
// a member who paid a 50 dinner with a 50 card found the card empty and the dinner
// still on the statement. Until a redemption settles a bill, both are switched off
// and say why.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -50,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const addLoyaltyEntry = vi.fn();
vi.mock('@/app/api/loyalty', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/loyalty')>()),
  getLoyaltyLedger: async () => [{ id: 'l1', memberId: 'm1', memberName: 'Jean Dupont', kind: 'Earned', reason: 'DiningPurchase', points: 2000, createdAtUtc: '2026-01-01T00:00:00Z' }],
  addLoyaltyEntry: (...args: unknown[]) => addLoyaltyEntry(...args),
}));

const redeemGiftCard = vi.fn();
vi.mock('@/app/api/giftCards', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/giftCards')>()),
  getGiftCards: async () => [],
  redeemGiftCard: (...args: unknown[]) => redeemGiftCard(...args),
}));

const PosTerminal = (await import('./PosTerminal')).default;

beforeEach(() => {
  localStorage.clear();
  addLoyaltyEntry.mockReset();
  redeemGiftCard.mockReset();
});

describe('POS terminal', () => {
  it('does not offer gift cards or points as a way to pay, and says why', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PosTerminal /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));

    const giftCard = screen.getByRole('button', { name: /Redeem gift card/ });
    const points = screen.getByRole('button', { name: /Redeem points/ });
    expect(giftCard).toBeDisabled();
    expect(points).toBeDisabled();
    expect(giftCard).toHaveTextContent(/without paying any bill/);
    expect(points).toHaveTextContent(/without paying any bill/);

    await user.click(giftCard);
    await user.click(points);
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(redeemGiftCard).not.toHaveBeenCalled();
    expect(addLoyaltyEntry).not.toHaveBeenCalled();
  });

  it('still posts charges, opens tabs and checks members in', async () => {
    const user = userEvent.setup();
    render(<MemoryRouter><PosTerminal /></MemoryRouter>);

    await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));

    for (const name of [/Check in/, /Open tab/, /Post charge/]) {
      expect(screen.getByRole('button', { name })).toBeEnabled();
    }
  });
});

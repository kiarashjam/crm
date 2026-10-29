// The POS terminal when the server says no. It used to answer a card it could not
// redeem with a bare 404, which this screen showed as "Redemption failed". It now
// answers with a reason (expired, used up, used at another till at the same moment),
// and the screen dropped that reason on the floor: no message, and nothing redeemed.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const MEMBER = {
  id: 'm-1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2026-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z', duesAmount: 0, duesFrequency: 'Monthly',
  houseAccountBalance: 0,
};
const CARD = {
  id: 'g-1', code: 'GIFT-AAAA-BBBB-CCCC', purchaserName: 'Ana', recipientName: 'Bea', faceValue: 50, remainingValue: 50,
  status: 'Active', issuedAtUtc: '2026-10-01T00:00:00Z',
};

vi.mock('@/app/api/members', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/members')>('@/app/api/members');
  return { ...actual, getMembers: vi.fn(async () => [MEMBER]) };
});
vi.mock('@/app/api/loyalty', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/loyalty')>('@/app/api/loyalty');
  return { ...actual, getLoyaltyLedger: vi.fn(async () => []) };
});
const redeemGiftCard = vi.fn();
vi.mock('@/app/api/giftCards', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/giftCards')>('@/app/api/giftCards');
  return { ...actual, getGiftCards: vi.fn(async () => [CARD]), redeemGiftCard: (...a: unknown[]) => redeemGiftCard(...a) };
});
const createCharge = vi.fn();
vi.mock('@/app/api/charges', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/charges')>('@/app/api/charges');
  return { ...actual, createCharge: (...a: unknown[]) => createCharge(...a) };
});

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const PosTerminal = (await import('./PosTerminal')).default;

async function openFor(action: RegExp) {
  const user = userEvent.setup();
  render(<MemoryRouter><PosTerminal /></MemoryRouter>);
  await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));
  await user.click(screen.getByRole('button', { name: action }));
  return user;
}

beforeEach(() => {
  redeemGiftCard.mockReset();
  createCharge.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the POS terminal when the server refuses', () => {
  it("shows why a gift card could not be redeemed, and a second try is the same redemption", async () => {
    const refusal = 'This gift card was used somewhere else at the same moment, so nothing was redeemed. Check its balance and try again.';
    redeemGiftCard.mockRejectedValue(new Error(refusal));
    const user = await openFor(/Redeem gift card/);

    await user.type(screen.getByLabelText('Gift card code'), CARD.code);
    await user.type(screen.getByLabelText('Amount to apply ($)'), '20');
    await user.click(screen.getByRole('button', { name: 'Redeem' }));

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(toast.success).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Redeem' }));
    await vi.waitFor(() => expect(redeemGiftCard).toHaveBeenCalledTimes(2));
    const [first, second] = redeemGiftCard.mock.calls.map(([input]) => (input as { idempotencyKey?: string }).idempotencyKey);
    expect(first).toBeTruthy();
    expect(second).toBe(first);
  });

  it('shows why a charge could not be posted', async () => {
    createCharge.mockRejectedValue(new Error('That member was not found.'));
    const user = await openFor(/Post charge/);

    await user.type(screen.getByLabelText('Description'), 'Dinner');
    await user.type(screen.getByLabelText('Amount ($)'), '40');
    // The tile and the dialog's button share the name; the dialog's comes last.
    const posts = screen.getAllByRole('button', { name: 'Post charge' });
    await user.click(posts[posts.length - 1]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('That member was not found.'));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

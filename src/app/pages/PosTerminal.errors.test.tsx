// The POS terminal when the server says no. Posting a charge or opening a tab had no
// catch at all, so a refusal (a member who has left, an amount the server will not
// take) vanished: no message, and nothing posted.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const MEMBER = {
  id: 'm-1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2026-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z', duesAmount: 0, duesFrequency: 'Monthly',
  houseAccountBalance: 0,
};

vi.mock('@/app/api/members', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/members')>('@/app/api/members');
  return { ...actual, getMembers: vi.fn(async () => [MEMBER]) };
});
vi.mock('@/app/api/loyalty', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/loyalty')>('@/app/api/loyalty');
  return { ...actual, getLoyaltyLedger: vi.fn(async () => []) };
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

beforeEach(() => {
  localStorage.clear();
  createCharge.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the POS terminal when the server refuses', () => {
  it('shows why a charge could not be posted', async () => {
    createCharge.mockRejectedValue(new Error('That member was not found.'));
    const user = userEvent.setup();
    render(<MemoryRouter><PosTerminal /></MemoryRouter>);
    await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));
    await user.click(screen.getByRole('button', { name: /Post charge/ }));

    await user.type(screen.getByLabelText('Description'), 'Dinner');
    await user.type(screen.getByLabelText(/Amount/), '40');
    // The tile and the dialog's button share the name; the dialog's comes last.
    const posts = screen.getAllByRole('button', { name: 'Post charge' });
    await user.click(posts[posts.length - 1]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('That member was not found.'));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

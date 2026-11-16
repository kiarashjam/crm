// The house-charge screen when the server says no. Opening a tab or checking a member in
// from here once had no catch at all, so a refusal (a member who has left, a venue the server
// does not know) vanished: no message, and nothing done.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';

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
const checkIn = vi.fn();
vi.mock('@/app/api/visits', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/visits')>('@/app/api/visits');
  return { ...actual, checkIn: (...a: unknown[]) => checkIn(...a) };
});
const openTab = vi.fn();
vi.mock('@/app/api/tabs', async () => {
  const actual = await vi.importActual<typeof import('@/app/api/tabs')>('@/app/api/tabs');
  return { ...actual, openTab: (...a: unknown[]) => openTab(...a) };
});

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const PosTerminal = (await import('./PosTerminal')).default;

beforeEach(() => {
  localStorage.clear();
  checkIn.mockReset();
  openTab.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the house-charge screen when the server refuses', () => {
  it('shows why a tab could not be opened', async () => {
    openTab.mockRejectedValue(new Error('That member was not found.'));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><PosTerminal /></MemoryRouter>));
    await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));
    await user.click(screen.getByRole('button', { name: /Open tab/ }));
    // The tile and the dialog's button share the name; the dialog's comes last.
    const opens = screen.getAllByRole('button', { name: 'Open tab' });
    await user.click(opens[opens.length - 1]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('That member was not found.'));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it('shows why a member could not be checked in', async () => {
    checkIn.mockRejectedValue(new Error('This member is lapsed.'));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><PosTerminal /></MemoryRouter>));
    await user.click(await screen.findByRole('button', { name: /Jean Dupont/ }));
    await user.click(screen.getByRole('button', { name: /Check in/ }));
    const checks = screen.getAllByRole('button', { name: 'Check in' });
    await user.click(checks[checks.length - 1]!);

    await vi.waitFor(() => expect(toast.error).toHaveBeenCalledWith('This member is lapsed.'));
    expect(toast.success).not.toHaveBeenCalled();
  });
});

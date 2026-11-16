// A saved view of the members list: its tier and status filters and its tag and field
// conditions, kept under a name and opened again from the Views menu.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import type { SavedView } from '@/app/api/savedViews';
import { withQueries } from '@/test/queryWrapper';

const membersApi = vi.hoisted(() => ({ getMembers: vi.fn() }));
vi.mock('@/app/api/members', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/members')>('@/app/api/members')),
  ...membersApi,
}));
const viewsApi = vi.hoisted(() => ({ getSavedViews: vi.fn(), createSavedView: vi.fn() }));
vi.mock('@/app/api/savedViews', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/savedViews')>('@/app/api/savedViews')),
  ...viewsApi,
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, MEMBER_DUPLICATES: {} }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Members = (await import('./Members')).default;

const member = (id: string, firstName: string, tier: string, status: Member['status']): Member => ({
  id, firstName, lastName: 'Meier', email: `${id}@example.ch`, tier, status,
  joinedAtUtc: '2025-01-15T00:00:00Z', renewsAtUtc: '2027-01-15T00:00:00Z',
  duesAmount: 1200, duesFrequency: 'Annual', houseAccountBalance: 0,
} as Member);

const LAPSED_GOLD_BOARD: SavedView = {
  id: 'v1', list: 'members', name: 'Lapsed gold on the board', shared: true, mine: true, isDefault: false,
  query: { tier: 'Gold', status: 'Lapsed', tag: ['t-board'] }, columns: null, version: 'v1', updatedAtUtc: '2026-11-13T00:00:00Z',
};

beforeEach(() => {
  for (const fn of [...Object.values(membersApi), ...Object.values(viewsApi)]) fn.mockReset();
  membersApi.getMembers.mockResolvedValue([
    member('m1', 'Jean', 'Gold', 'Active'),
    member('m2', 'Anna', 'Silver', 'Lapsed'),
    member('m3', 'Paul', 'Gold', 'Lapsed'),
  ]);
  viewsApi.getSavedViews.mockResolvedValue([LAPSED_GOLD_BOARD]);
});

describe('saved views of the members list', () => {
  it('open with the tier, status and tag conditions they keep', async () => {
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter initialEntries={['/members']}><Members /></MemoryRouter>));
    expect(await screen.findByText('Anna Meier')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /^Lapsed gold on the board/ }));

    // The tag condition is the server's to apply; the tier and status narrow the list here.
    await waitFor(() => expect(membersApi.getMembers).toHaveBeenLastCalledWith(undefined, { tags: ['t-board'] }));
    await waitFor(() => expect(screen.queryByText('Anna Meier')).not.toBeInTheDocument());
    expect(screen.queryByText('Jean Meier')).not.toBeInTheDocument();
    expect(screen.getByText('Paul Meier')).toBeInTheDocument();
  });

  it('save the tier and status chosen with the tag and field conditions', async () => {
    viewsApi.getSavedViews.mockResolvedValue([]);
    viewsApi.createSavedView.mockImplementation(async (input: Record<string, unknown>) => ({ ...LAPSED_GOLD_BOARD, ...input }));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter initialEntries={['/members?tier=Gold&status=Lapsed&tag=t-board']}><Members /></MemoryRouter>));
    expect(await screen.findByText('Paul Meier')).toBeInTheDocument();
    expect(screen.queryByText('Jean Meier')).not.toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /save these filters/i }));
    await user.type(screen.getByLabelText('Name'), 'Lapsed gold');
    await user.click(screen.getByRole('button', { name: /save view/i }));

    await waitFor(() => expect(viewsApi.createSavedView).toHaveBeenCalledWith(expect.objectContaining({
      list: 'members', name: 'Lapsed gold', query: { tier: 'Gold', status: 'Lapsed', tag: ['t-board'] },
    })));
  });
});

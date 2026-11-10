// Editing a member sends an emptied phone or note as null, which takes it off on the
// server. Left out of the request, as it was, a member who asked the club to delete
// their mobile number kept it, and kept getting texts on it, while the page said
// "Member updated".
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Member } from '@/app/api/members';
import { withQueries } from '@/test/queryWrapper';

const membersApi = vi.hoisted(() => ({ getMembers: vi.fn(), createMember: vi.fn(), updateMember: vi.fn(), deleteMember: vi.fn() }));
vi.mock('@/app/api/members', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/members')>('@/app/api/members')),
  ...membersApi,
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, MEMBER_DUPLICATES: {} }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Members = (await import('./Members')).default;

const jean: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', phone: '+41 79 123 45 67',
  tier: 'Gold', status: 'Active', joinedAtUtc: '2025-01-15T00:00:00Z', renewsAtUtc: '2027-01-15T00:00:00Z',
  duesAmount: 1200, duesFrequency: 'Annual', houseAccountBalance: 0, notes: 'Prefers texts',
} as Member;

beforeEach(() => {
  for (const fn of Object.values(membersApi)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  membersApi.getMembers.mockResolvedValue([jean]);
  membersApi.updateMember.mockResolvedValue({ ...jean, phone: undefined, notes: undefined });
});

const openEdit = async () => {
  // Under the app's cache: the tiers on offer are read from the price catalogue.
  render(withQueries(<MemoryRouter><Members /></MemoryRouter>));
  fireEvent.click((await screen.findAllByRole('button', { name: 'Edit member' }))[0]!);
  return screen.findByLabelText('Phone');
};

describe('editing a member', () => {
  it('sends an emptied phone and note as null, so the server takes them off', async () => {
    const phone = await openEdit();

    fireEvent.change(phone, { target: { value: '' } });
    fireEvent.change(screen.getByLabelText('Notes'), { target: { value: '  ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(membersApi.updateMember).toHaveBeenCalledTimes(1));
    expect(membersApi.updateMember.mock.calls[0]).toEqual(['m1', expect.objectContaining({ phone: null, notes: null, firstName: 'Jean' })]);
  });

  it('sends a phone and note that are there as they are', async () => {
    const phone = await openEdit();

    fireEvent.change(phone, { target: { value: '+41 79 765 43 21' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(membersApi.updateMember).toHaveBeenCalledTimes(1));
    expect(membersApi.updateMember.mock.calls[0]![1]).toMatchObject({ phone: '+41 79 765 43 21', notes: 'Prefers texts' });
  });
});

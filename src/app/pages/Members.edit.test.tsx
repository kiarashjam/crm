// Editing a member sends an emptied phone or note as null, which takes it off on the
// server. Left out of the request, as it was, a member who asked the club to delete
// their mobile number kept it, and kept getting texts on it, while the page said
// "Member updated".
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
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

  it('leaves a member’s language alone unless it was changed, and sends it when it was', async () => {
    const phone = await openEdit();
    // Jean has none set: shown as English, the language he is written to in.
    expect(screen.getByLabelText('Writes to them in')).toHaveValue('en');
    fireEvent.change(phone, { target: { value: '+41 79 765 43 21' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(membersApi.updateMember).toHaveBeenCalledTimes(1));
    expect(membersApi.updateMember.mock.calls[0]![1]).not.toHaveProperty('preferredLanguage');

    fireEvent.click((await screen.findAllByRole('button', { name: 'Edit member' }))[0]!);
    fireEvent.change(await screen.findByLabelText('Writes to them in'), { target: { value: 'de' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save changes' }));
    await waitFor(() => expect(membersApi.updateMember).toHaveBeenCalledTimes(2));
    expect(membersApi.updateMember.mock.calls[1]![1]).toMatchObject({ preferredLanguage: 'de' });
  });
});

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

describe('adding a member', () => {
  // The price catalogue here is demo data's: Gold is 299 a month with a 1,000 initiation fee.
  const openAdd = async () => {
    const user = userEvent.setup();
    membersApi.createMember.mockResolvedValue({ ...jean, id: 'm2', firstName: 'Lea' });
    render(withQueries(<MemoryRouter><Members /></MemoryRouter>));
    await user.click(await screen.findByRole('button', { name: /Add member/ }));
    fireEvent.change(screen.getByLabelText('First name'), { target: { value: 'Lea' } });
    fireEvent.change(screen.getByLabelText('Last name'), { target: { value: 'Meier' } });
    fireEvent.change(screen.getByLabelText('Email'), { target: { value: 'lea@example.ch' } });
    await user.click(screen.getByLabelText('Tier'));
    await user.click(await screen.findByRole('option', { name: /^Gold/ }));
    return user;
  };

  it('says the dues start today, not that the member was billed, when only the dues are started', async () => {
    const user = await openAdd();

    await user.click(screen.getByLabelText('Start their dues today'));
    await user.click(screen.getByRole('button', { name: 'Add member' }));

    await waitFor(() => expect(membersApi.createMember).toHaveBeenCalledTimes(1));
    expect(membersApi.createMember.mock.calls[0]![0]).toMatchObject({ tier: 'Gold', startDues: true });
    expect(membersApi.createMember.mock.calls[0]![0].chargeInitiationFee).toBeUndefined();
    // Nothing was charged: the first period is billed by the billing run, or "Bill what is due".
    expect(toast.success).toHaveBeenCalledWith('Member added: their dues start today');
  });

  it('writes to a new member in the workspace’s language unless staff pick another', async () => {
    const user = await openAdd();

    // The workspace works in de-CH (the default), so a new member is written to in German.
    expect(screen.getByLabelText('Writes to them in')).toHaveValue('de');
    await user.selectOptions(screen.getByLabelText('Writes to them in'), 'fr');
    await user.click(screen.getByRole('button', { name: 'Add member' }));

    await waitFor(() => expect(membersApi.createMember).toHaveBeenCalledTimes(1));
    expect(membersApi.createMember.mock.calls[0]![0]).toMatchObject({ preferredLanguage: 'fr' });
  });

  it('says the initiation fee was charged when it was', async () => {
    const user = await openAdd();

    await user.click(screen.getByLabelText('Charge the initiation fee'));
    await user.click(screen.getByRole('button', { name: 'Add member' }));

    await waitFor(() => expect(membersApi.createMember).toHaveBeenCalledTimes(1));
    expect(membersApi.createMember.mock.calls[0]![0]).toMatchObject({ chargeInitiationFee: true });
    expect(toast.success).toHaveBeenCalledWith('Member added and initiation fee charged');
  });
});

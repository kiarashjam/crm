// The contact page's delete. The server refuses to delete a contact the club still keeps as a
// private client (money on their house account, a private event to come) and says why; the page
// used to read the refusal as "not deleted" and say nothing at all.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { PERMISSION } from '@/app/api/organizations';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/components/EmailComposerDialog', () => ({ default: () => null }));
vi.mock('@/app/components/CustomFieldsCard', () => ({ default: () => null }));
vi.mock('@/app/components/AttachmentsCard', () => ({ default: () => null }));
vi.mock('@/app/components/ReferredLeadsCard', () => ({ default: () => null }));
vi.mock('@/app/components/RelationshipsCard', () => ({ default: () => null }));
vi.mock('@/app/components/MembershipLinks', () => ({ PersonMembershipCard: () => null }));
vi.mock('@/app/components/corporate/ClientClubCard', () => ({ ClientClubCard: () => null }));
vi.mock('@/app/components/privacy/PrivacyRequestLink', () => ({ PrivacyRequestLink: () => null }));
vi.mock('@/app/hooks/useModules', () => ({ useModules: () => ({ sales: false, club: true }) }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

const api = vi.hoisted(() => ({
  getContactById: vi.fn(), getContactLookalikes: vi.fn(), getActivitiesByContact: vi.fn(), getTasksByContact: vi.fn(),
  getDealsPaged: vi.fn(), deleteContact: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));

const { default: ContactDetail } = await import('./ContactDetail');

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/contacts/lena']}>
    <Routes>
      <Route path="/contacts/:id" element={<ContactDetail />} />
      <Route path="/contacts" element={<p>The contacts</p>} />
    </Routes>
  </MemoryRouter>,
));

const deleteThem = async () => {
  await userEvent.click(await screen.findByRole('button', { name: 'Delete contact' }));
  await userEvent.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Delete' }));
};

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(toast)]) fn.mockReset();
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1,
      permissions: [PERMISSION.EditRecords, PERMISSION.DeleteRecords], currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich',
    },
  };
  api.getContactById.mockResolvedValue({ id: 'lena', name: 'Lena Graf', email: 'lena@example.ch', createdAtUtc: '2026-01-01T00:00:00Z', version: 'v1' });
  api.getContactLookalikes.mockResolvedValue([]);
  api.getActivitiesByContact.mockResolvedValue([]);
  api.getTasksByContact.mockResolvedValue([]);
});

describe('deleting a contact', () => {
  it("says why a contact the club keeps cannot be deleted, in the server's words, and stays on them", async () => {
    const refusal = 'This contact has a private event at the club still to come, and the club’s records would be left naming nobody, so they can’t be deleted. Merge them into the contact they duplicate instead.';
    api.deleteContact.mockRejectedValue(new Error(refusal));
    show();

    await deleteThem();

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(api.deleteContact).toHaveBeenCalledWith('lena');
    expect(screen.queryByText('The contacts')).not.toBeInTheDocument();
  });

  it('goes back to the contacts once they are deleted', async () => {
    api.deleteContact.mockResolvedValue(true);
    show();

    await deleteThem();

    expect(await screen.findByText('The contacts')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('Contact deleted');
  });
});

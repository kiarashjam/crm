// The team list in Settings names each member's preset, and leaves changing it to the
// Team page. Its old Member / Manager list showed a read-only accountant as "Member",
// and picking a role there reset whatever the owner had set for them.

import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { OrgMemberDto } from '@/app/api/organizations';

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('./RegionalSettingsCard', () => ({ RegionalSettingsCard: () => null }));
vi.mock('./EmailSendingCard', () => ({ EmailSendingCard: () => null }));

const { OrganizationSection } = await import('./OrganizationSection');

const owner: OrgMemberDto = { userId: 'u-owner', name: 'Olga Owner', email: 'olga@example.ch', role: 0, preset: 'owner', permissions: [] };
const accountant: OrgMemberDto = {
  userId: 'u-books', name: 'Bea Books', email: 'bea@example.ch', role: 3, preset: 'read_only_accountant', permissions: ['records.export'], customised: false,
};
const trusted: OrgMemberDto = {
  userId: 'u-anna', name: 'Anna Rep', email: 'anna@example.ch', role: 1, preset: 'sales_rep', permissions: ['records.edit', 'records.delete'], customised: true,
};
// From a server too old to send presets: the role is all there is.
const older: OrgMemberDto = { userId: 'u-marc', name: 'Marc Old', email: 'marc@example.ch', role: 1 };

const show = (members: OrgMemberDto[], onRoleChange = vi.fn()) =>
  render(
    <MemoryRouter>
      <OrganizationSection
        organizations={[{ id: 'org-1', name: 'Pavillon 46', isOwner: true, role: 0 }]}
        currentOrg={{ id: 'org-1', name: 'Pavillon 46', isOwner: true, role: 0 }}
        setCurrentOrg={vi.fn()}
        inviteEmail=""
        setInviteEmail={vi.fn()}
        handleInvite={vi.fn()}
        inviting={false}
        ownerInvites={[]}
        ownerJoinRequests={[]}
        handleAcceptJr={vi.fn()}
        handleRejectJr={vi.fn()}
        actingJrId={null}
        orgMembers={members}
        handleMemberRoleChange={onRoleChange}
        actingMemberId={null}
        removeMemberConfirm={null}
        setRemoveMemberConfirm={vi.fn()}
        handleRemoveMember={vi.fn()}
        currentUser={{ id: 'u-owner' }}
      />
    </MemoryRouter>,
  );

describe('the team list in Settings', () => {
  it('names each member by their preset and sends changes to the Team page', () => {
    show([owner, accountant, trusted]);

    const books = screen.getByText('Bea Books').closest('tr')!;
    expect(within(books).getByText('Read-only accountant')).toBeInTheDocument();
    expect(within(books).queryByRole('combobox')).toBeNull();
    expect(within(books).queryByText('Member')).toBeNull();
    expect(within(books).getByRole('link', { name: 'Change on the Team page' })).toHaveAttribute('href', '/team');

    const anna = screen.getByText('Anna Rep').closest('tr')!;
    expect(within(anna).getByText('Sales rep, changed')).toBeInTheDocument();
  });

  it('keeps the role list for a server that sends no presets', () => {
    show([owner, older]);

    const marc = screen.getByText('Marc Old').closest('tr')!;
    expect(within(marc).getByRole('combobox')).toHaveValue('1');
  });
});

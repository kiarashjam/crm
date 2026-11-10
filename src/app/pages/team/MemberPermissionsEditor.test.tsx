// What a member may do, as the Team page edits it: presets for whoever manages the
// team, single permissions for the owner, and nothing beyond what the editor holds.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { OrgMemberDto, PermissionCatalogue } from '@/app/api/organizations';

const api = vi.hoisted(() => ({ set: vi.fn() }));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  setMemberPermissions: (...a: unknown[]) => api.set(...a),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { MemberPermissionsEditor, roleForPreset } = await import('./MemberPermissionsEditor');

const catalogue: PermissionCatalogue = {
  permissions: [
    { key: 'records.edit', label: 'Create and edit records', description: 'Leads and deals.' },
    { key: 'records.delete', label: 'Delete records', description: 'Delete or merge.' },
    { key: 'money.manage', label: 'Take payments and charge accounts', description: 'Charges.' },
    { key: 'members.manage', label: 'Manage the team', description: 'Members.' },
  ],
  presets: [
    { key: 'owner', label: 'Owner', description: 'Everything.', permissions: ['records.edit', 'records.delete', 'money.manage', 'members.manage'], assignable: false },
    { key: 'sales_rep', label: 'Sales rep', description: 'Works leads and deals.', permissions: ['records.edit'], assignable: true },
    { key: 'finance', label: 'Finance', description: 'Takes payments.', permissions: ['money.manage'], assignable: true },
  ],
};

const anna: OrgMemberDto = { userId: 'u-anna', name: 'Anna Keller', email: 'anna@example.ch', role: 1, preset: 'sales_rep', permissions: ['records.edit'], customised: false };

beforeEach(() => {
  api.set.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('MemberPermissionsEditor', () => {
  it('lets the owner allow one more thing, sending the whole list with the preset', async () => {
    api.set.mockResolvedValue({ ...anna, permissions: ['records.edit', 'records.delete'], customised: true });
    const changed = vi.fn();
    render(<MemberPermissionsEditor organizationId="org-1" member={anna} catalogue={catalogue} mine={new Set()} isOwner onChanged={changed} />);

    expect(screen.getByRole('checkbox', { name: /Create and edit records/ })).toBeChecked();
    await userEvent.setup().click(screen.getByRole('checkbox', { name: /Delete records/ }));

    await waitFor(() => expect(api.set).toHaveBeenCalledWith('org-1', 'u-anna', 'sales_rep', ['records.edit', 'records.delete']));
    expect(changed).toHaveBeenCalledWith(expect.objectContaining({ customised: true }));
    expect(toast.success).toHaveBeenCalledWith('Changed what Anna Keller may do');
  });

  it('shows someone who manages the team what a member may do, without single switches', () => {
    render(
      <MemberPermissionsEditor organizationId="org-1" member={anna} catalogue={catalogue}
        mine={new Set(['records.edit', 'members.manage'])} isOwner={false} onChanged={vi.fn()} />,
    );

    expect(screen.queryByRole('checkbox')).toBeNull();
    expect(screen.getByLabelText('Allowed')).toBeInTheDocument();
    expect(screen.getByText('Only the owner changes single permissions.')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Preset' })).not.toBeDisabled();
  });

  it('leaves alone a member who can do more than the person editing', () => {
    const finance = { ...anna, preset: 'finance', permissions: ['money.manage'] };
    render(
      <MemberPermissionsEditor organizationId="org-1" member={finance} catalogue={catalogue}
        mine={new Set(['records.edit', 'members.manage'])} isOwner={false} onChanged={vi.fn()} />,
    );

    expect(screen.getByRole('combobox', { name: 'Preset' })).toBeDisabled();
    expect(screen.getByText(/They can do more than you/)).toBeInTheDocument();
  });

  it('says why the server refused', async () => {
    api.set.mockRejectedValue(new Error('HTTP 403: You can only give a member what you can do yourself.'));
    render(<MemberPermissionsEditor organizationId="org-1" member={anna} catalogue={catalogue} mine={new Set()} isOwner onChanged={vi.fn()} />);

    await userEvent.setup().click(screen.getByRole('checkbox', { name: /Take payments/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('You can only give a member what you can do yourself.'));
  });

  it('pairs each preset with the role older clients read', () => {
    expect(roleForPreset('manager')).toBe(2);
    expect(roleForPreset('read_only_accountant')).toBe(3);
    expect(roleForPreset('front_desk')).toBe(1);
  });
});

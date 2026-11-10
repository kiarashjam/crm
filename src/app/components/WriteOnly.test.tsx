// A control shows only to members whose permissions let them use it.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PERMISSION } from '@/app/api/organizations';

const org = vi.hoisted(() => ({ current: null as null | { isReadOnly: boolean; currentOrg: unknown } }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current }));

const { WriteOnly } = await import('./WriteOnly');

const as = (permissions: string[] | undefined, role = 1, isReadOnly = false) => {
  org.current = { isReadOnly, currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role, permissions } };
};

describe('WriteOnly', () => {
  it('shows record editing to whoever may edit records, and not to finance or the front desk', () => {
    as([PERMISSION.EditRecords]);
    const { rerender } = render(<WriteOnly><button type="button">Add lead</button></WriteOnly>);
    expect(screen.getByRole('button', { name: 'Add lead' })).toBeInTheDocument();

    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    rerender(<WriteOnly><button type="button">Add lead</button></WriteOnly>);
    expect(screen.queryByRole('button', { name: 'Add lead' })).toBeNull();
  });

  it('hides a delete from a sales rep, and shows it to someone allowed to delete', () => {
    as([PERMISSION.EditRecords]);
    const { rerender } = render(<WriteOnly permission={PERMISSION.DeleteRecords}><button type="button">Delete</button></WriteOnly>);
    expect(screen.queryByRole('button', { name: 'Delete' })).toBeNull();

    as(undefined); // an old Member, from a server that sends no list: they could always delete
    rerender(<WriteOnly permission={PERMISSION.DeleteRecords}><button type="button">Delete</button></WriteOnly>);
    expect(screen.getByRole('button', { name: 'Delete' })).toBeInTheDocument();
  });

  it('takes any one of a list, and nothing from a read-only member', () => {
    as([PERMISSION.FrontDesk]);
    const { rerender } = render(
      <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}><button type="button">Remove guest</button></WriteOnly>,
    );
    expect(screen.getByRole('button', { name: 'Remove guest' })).toBeInTheDocument();

    as([PERMISSION.ExportRecords], 3, true);
    rerender(<WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}><button type="button">Remove guest</button></WriteOnly>);
    expect(screen.queryByRole('button', { name: 'Remove guest' })).toBeNull();
  });

  it('shows everything outside a workspace provider, and in a personal workspace', () => {
    org.current = null;
    const { rerender } = render(<WriteOnly permission={PERMISSION.VoidMoney}><button type="button">Void</button></WriteOnly>);
    expect(screen.getByRole('button', { name: 'Void' })).toBeInTheDocument();

    org.current = { isReadOnly: false, currentOrg: null };
    rerender(<WriteOnly permission={PERMISSION.VoidMoney}><button type="button">Void</button></WriteOnly>);
    expect(screen.getByRole('button', { name: 'Void' })).toBeInTheDocument();
  });
});

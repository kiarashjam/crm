// Importing members.
//
// The page adds a file's rows one by one through the endpoint that adds a member by hand,
// and a member added sets off the workspace's "When a member is added" automations: a club
// moving its list of members over would have welcomed, and made a task for, every one of
// them. Each row now says it comes from an import, and the server sets nothing off for it.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION } from '@/app/api/organizations';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

const created = vi.hoisted(() => ({ calls: [] as Record<string, unknown>[] }));
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  createMember: async (input: Record<string, unknown>) => {
    created.calls.push(input);
    return { id: `m-${created.calls.length}`, ...input };
  },
}));
vi.mock('@/app/api/catalog', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/catalog')>()),
  getCatalog: async () => [],
}));

const MemberImport = (await import('./MemberImport')).default;

const FILE = `firstName,lastName,email,tier,status
Eleanor,Tanaka,eleanor@example.ch,Gold,Active
Felix,Ashford,felix@example.ch,Silver,Active
`;

beforeEach(() => {
  created.calls = [];
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1,
      permissions: [PERMISSION.EditRecords, PERMISSION.ClubMembers],
    },
  };
});

describe('importing members', () => {
  it('marks every row as an import, so no automation welcomes the members already in the club', async () => {
    render(withQueries(<MemoryRouter><MemberImport /></MemoryRouter>));
    fireEvent.change(screen.getByRole('textbox'), { target: { value: FILE } });

    await userEvent.click(await screen.findByRole('button', { name: /^Import 2 members/ }));

    await waitFor(() => expect(created.calls).toHaveLength(2));
    expect(created.calls.map((c) => [c.email, c.origin])).toEqual([
      ['eleanor@example.ch', 'import'],
      ['felix@example.ch', 'import'],
    ]);
    expect(screen.getByText(/do not set off the "When a member is added" automations/)).toBeInTheDocument();
  });
});

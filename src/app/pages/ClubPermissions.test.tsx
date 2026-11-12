// The club pages offer each member only what their permissions let them do.
//
// The server checks every change (money.manage for charges and payments, club.members
// for applications and members, and records.delete on top of a page's own permission
// to delete), but the pages offered Pay, Acknowledge, Approve and Delete to whoever
// opened them: the front desk saw Pay on every charge and got a refusal for it.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Member } from '@/app/api/members';
import type { Charge } from '@/app/api/charges';
import type { Donation } from '@/app/api/donations';
import type { Application } from '@/app/api/applications';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

/** Signed in as a member of the club holding these permissions. */
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions },
  };
};

// Against the real API: demo mode offers everything, as its workspaces are the visitor's own.
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

vi.mock('@/app/api/hospitalitySettings', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/hospitalitySettings')>()),
  getHospitalitySettings: async () => ({}),
}));

const MEMBER: Member = {
  id: 'm1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', tier: 'Gold', status: 'Active',
  joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: -320.5,
};
vi.mock('@/app/api/members', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/members')>()),
  getMembers: async () => [MEMBER],
}));

const CHARGE: Charge = {
  id: 'c1', memberId: 'm1', memberName: 'Jean Dupont', kind: 'Dining', description: 'Dinner for two',
  amount: 320.5, paidAmount: 0, status: 'Pending', postedAtUtc: new Date().toISOString(),
};
vi.mock('@/app/api/charges', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/charges')>()),
  getCharges: async () => [CHARGE],
  getPayments: async () => [],
}));

const DONATION: Donation = {
  id: 'd1', memberId: 'm1', memberName: 'Jean Dupont', cause: 'Scholarship', amount: 500,
  receivedAtUtc: new Date().toISOString(), isAnonymous: false, acknowledged: false,
};
vi.mock('@/app/api/donations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/donations')>()),
  getDonations: async () => [DONATION],
}));

const APPLICATION: Application = {
  id: 'a1', firstName: 'Anne', lastName: 'Muster', email: 'anne@example.ch', requestedTier: 'Gold',
  status: 'Submitted', submittedAtUtc: new Date().toISOString(),
};
vi.mock('@/app/api/applications', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/applications')>()),
  getApplications: async () => [APPLICATION],
}));

const Charges = (await import('./Charges')).default;
const Donations = (await import('./Donations')).default;
const Applications = (await import('./Applications')).default;
const Members = (await import('./Members')).default;
const MemberImport = (await import('./MemberImport')).default;

const show = (page: React.ReactNode) => render(withQueries(<MemoryRouter>{page}</MemoryRouter>));

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
});

describe('charges', () => {
  it('offer the front desk neither Pay nor Void, and finance both', async () => {
    as([PERMISSION.FrontDesk]);
    const { unmount } = show(<Charges />);
    expect((await screen.findAllByText('Dinner for two')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /pay/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /void charge/i })).toBeNull();
    unmount();

    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney, PERMISSION.ExportRecords]);
    show(<Charges />);
    expect((await screen.findAllByRole('button', { name: /pay/i })).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: /void charge/i }).length).toBeGreaterThan(0);
  });

  it('offer no Void to someone allowed to void but not to handle money, as the server refuses them', async () => {
    as([PERMISSION.VoidMoney, PERMISSION.ExportRecords]);
    show(<Charges />);
    expect((await screen.findAllByText('Dinner for two')).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /void charge/i })).toBeNull();
  });
});

describe('donations', () => {
  it('offer Acknowledge to whoever handles money, and not to the front desk', async () => {
    as([PERMISSION.FrontDesk]);
    const { unmount } = show(<Donations />);
    expect((await screen.findAllByText(/Jean Dupont/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Acknowledge' })).toBeNull();
    unmount();

    as([PERMISSION.ManageMoney]);
    show(<Donations />);
    expect((await screen.findAllByRole('button', { name: 'Acknowledge' })).length).toBeGreaterThan(0);
  });

  it('are voided with a reason by someone who may void money, and never deleted', async () => {
    // A donation is money received: it used to be deleted, and vanished from the totals.
    as([PERMISSION.ManageMoney, PERMISSION.DeleteRecords]);
    const { unmount } = show(<Donations />);
    expect((await screen.findAllByRole('button', { name: 'Acknowledge' })).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /delete donation/i })).toBeNull();
    expect(screen.queryByRole('button', { name: /void donation/i })).toBeNull();
    unmount();

    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney]);
    show(<Donations />);
    expect((await screen.findAllByRole('button', { name: /void donation from Jean Dupont/i })).length).toBeGreaterThan(0);
  });
});

describe('applications', () => {
  it('offer the review to whoever manages club members, and not to finance', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney, PERMISSION.ExportRecords]);
    const { unmount } = show(<Applications />);
    expect((await screen.findAllByText(/Anne Muster/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: /approve/i })).toBeNull();
    unmount();

    as([PERMISSION.EditRecords, PERMISSION.ClubMembers]);
    show(<Applications />);
    expect((await screen.findAllByRole('button', { name: /approve/i })).length).toBeGreaterThan(0);
  });
});

describe('members', () => {
  it('offer removal only to someone who manages club members and may delete', async () => {
    // A sales rep the owner let delete: the club's members are still not theirs.
    as([PERMISSION.EditRecords, PERMISSION.DeleteRecords]);
    const { unmount } = show(<Members />);
    expect((await screen.findAllByText(/Dupont/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Remove member' })).toBeNull();
    unmount();

    as([PERMISSION.ClubMembers, PERMISSION.DeleteRecords]);
    show(<Members />);
    expect((await screen.findAllByRole('button', { name: 'Remove member' })).length).toBeGreaterThan(0);
  });
});

describe('importing members', () => {
  it('offers the import to whoever manages club members, and not to finance', async () => {
    as([PERMISSION.ManageMoney, PERMISSION.VoidMoney, PERMISSION.ExportRecords]);
    const { unmount } = show(<MemberImport />);
    expect(await screen.findByRole('button', { name: /sample/i })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Import\b.*member/ })).toBeNull();
    unmount();

    as([PERMISSION.EditRecords, PERMISSION.ClubMembers]);
    show(<MemberImport />);
    expect(await screen.findByRole('button', { name: /^Import\b.*member/ })).toBeInTheDocument();
  });
});

// A read-only member, or one without the permission a page's changes take, is offered
// nothing on the club pages that the server would refuse.
//
// The buttons at the top of each page were gated, but an empty page still offered "Add
// member", "Check in" or "Post charge" in its empty state to whoever opened it, and the
// hospitality settings let anyone type into fields that only settings.manage may save.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ORG_ROLE, PERMISSION, type Permission } from '@/app/api/organizations';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { withQueries } from '@/test/queryWrapper';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));

/** Signed in to the club holding these permissions; with none that change anything, read-only. */
const as = (permissions: Permission[], readOnly = false) => {
  org.current = {
    isReadOnly: readOnly,
    currentOrgId: 'org-1',
    organizations: [],
    hasFetched: true,
    currentOrg: {
      id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false,
      role: readOnly ? ORG_ROLE.Viewer : ORG_ROLE.Member, permissions,
    },
  };
};
const asViewer = () => as([PERMISSION.ExportRecords], true);

vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

// An empty club: every list comes back with nothing in it.
vi.mock('@/app/api/members', async (o) => ({ ...(await o<typeof import('@/app/api/members')>()), getMembers: async () => [] }));
vi.mock('@/app/api/visits', async (o) => ({ ...(await o<typeof import('@/app/api/visits')>()), getVisits: async () => [] }));
vi.mock('@/app/api/charges', async (o) => ({
  ...(await o<typeof import('@/app/api/charges')>()), getCharges: async () => [], getPayments: async () => [],
}));
vi.mock('@/app/api/communications', async (o) => ({ ...(await o<typeof import('@/app/api/communications')>()), getCommunications: async () => [] }));
vi.mock('@/app/api/tabs', async (o) => ({ ...(await o<typeof import('@/app/api/tabs')>()), getTabs: async () => [] }));
vi.mock('@/app/api/giftCards', async (o) => ({
  ...(await o<typeof import('@/app/api/giftCards')>()), getGiftCards: async () => [], getRedemptions: async () => [],
}));
vi.mock('@/app/api/subscriptions', async (o) => ({ ...(await o<typeof import('@/app/api/subscriptions')>()), getSubscriptions: async () => [] }));
vi.mock('@/app/api/donations', async (o) => ({ ...(await o<typeof import('@/app/api/donations')>()), getDonations: async () => [] }));
vi.mock('@/app/api/loyalty', async (o) => ({ ...(await o<typeof import('@/app/api/loyalty')>()), getLoyaltyLedger: async () => [] }));
vi.mock('@/app/api/catalog', async (o) => ({ ...(await o<typeof import('@/app/api/catalog')>()), getCatalog: async () => [] }));
vi.mock('@/app/api/hospitalitySettings', async (o) => ({
  ...(await o<typeof import('@/app/api/hospitalitySettings')>()), getHospitalitySettings: async () => ({}),
}));
vi.mock('@/app/api/billing', async (o) => ({
  ...(await o<typeof import('@/app/api/billing')>()),
  getBillingRuns: async () => [],
  getBillingPreview: async () => ({ subscriptions: 0, periods: 0, amountByCurrency: {} }),
}));

// Campaigns: one being written and one sending, for the actions each offers.
vi.mock('@/app/api/campaigns', async (o) => {
  const actual = await o<typeof import('@/app/api/campaigns')>();
  const draft = {
    id: 'cmp-1', name: 'Founders dinner', channel: 'Email', category: 'Event', purpose: 'marketing', subject: 'Dinner', body: 'Save the date.',
    segment: {}, status: 'Draft', createdAtUtc: '2026-10-01T00:00:00Z', recipientCount: 0, version: 'v1',
  };
  return {
    ...actual,
    getCampaigns: async () => [draft, { ...draft, id: 'cmp-2', name: 'Wine evening', status: 'Sending', version: 'v2' }],
    getCampaignAudience: async () => ({ purpose: 'marketing', matching: 0, withAddress: 0, agreed: 0, willReceive: 0 }),
  };
});

const show = (page: React.ReactNode) => render(withQueries(<MemoryRouter>{page}</MemoryRouter>));

beforeEach(() => {
  localStorage.clear();
  resetClubCurrency();
});

const EMPTY_PAGES = [
  { page: 'Members', load: () => import('./Members'), empty: 'No members yet', action: 'Add member', needs: PERMISSION.ClubMembers },
  { page: 'Visits', load: () => import('./Visits'), empty: 'No check-ins yet', action: 'Check in', needs: PERMISSION.FrontDesk },
  { page: 'Charges', load: () => import('./Charges'), empty: 'No charges yet', action: 'Post charge', needs: PERMISSION.ManageMoney },
  { page: 'Communications', load: () => import('./Communications'), empty: 'No messages yet', action: 'Email a member', needs: PERMISSION.ClubMembers },
  { page: 'Tabs', load: () => import('./Tabs'), empty: 'No open tabs', action: 'Open tab', needs: PERMISSION.FrontDesk },
  { page: 'Gift cards', load: () => import('./GiftCards'), empty: 'No gift cards yet', action: 'Issue gift card', needs: PERMISSION.ManageMoney },
  { page: 'Subscriptions', load: () => import('./Subscriptions'), empty: 'No subscriptions yet', action: 'New subscription', needs: PERMISSION.ManageMoney },
  { page: 'Donations', load: () => import('./Donations'), empty: 'No donations yet', action: 'Record donation', needs: PERMISSION.ManageMoney },
  { page: 'Loyalty', load: () => import('./Loyalty'), empty: 'No ledger entries yet', action: 'Add entry', needs: PERMISSION.ClubMembers },
];

describe.each(EMPTY_PAGES)('$page, with nothing in it yet', ({ load, empty, action, needs }) => {
  const emptyState = () => screen.findByRole('status', { name: empty });

  it('offers a read-only member nothing to add', async () => {
    asViewer();
    const Page = (await load()).default;
    show(<Page />);

    await emptyState();
    expect(screen.queryByRole('button', { name: action })).toBeNull();
  });

  it(`offers it to a member without ${needs} neither`, async () => {
    // Someone who may do something else in the club, just not this.
    const other = ([PERMISSION.ClubMembers, PERMISSION.FrontDesk, PERMISSION.ManageMoney] as Permission[]).filter((p) => p !== needs);
    as([other[0]!]);
    const Page = (await load()).default;
    show(<Page />);

    await emptyState();
    expect(screen.queryByRole('button', { name: action })).toBeNull();
  });

  it(`offers it to a member with ${needs}`, async () => {
    as([needs]);
    const Page = (await load()).default;
    show(<Page />);

    const state = await emptyState();
    expect(state.querySelector('button')?.textContent).toBe(action);
  });
});

describe('Hospitality settings', () => {
  const HospitalitySettings = async () => (await import('./HospitalitySettings')).default;

  it('are shown to whoever may not change them, but not changed: no fields to type into and no Save', async () => {
    as([PERMISSION.FrontDesk]);
    const Page = await HospitalitySettings();
    show(<Page />);

    expect(await screen.findByRole('combobox', { name: 'Club currency' })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /save changes/i })).toBeNull();
  });

  it('can be changed by whoever manages the settings', async () => {
    as([PERMISSION.ManageSettings]);
    const Page = await HospitalitySettings();
    show(<Page />);

    expect(await screen.findByRole('combobox', { name: 'Club currency' })).toBeEnabled();
    expect(screen.getByRole('button', { name: /save changes/i })).toBeInTheDocument();
  });
});

describe('Campaigns', () => {
  const Campaigns = async () => (await import('./Campaigns')).default;

  it.each([
    ['a read-only member', () => asViewer()],
    ['a member without club.members', () => as([PERMISSION.FrontDesk, PERMISSION.ManageMoney])],
  ])('offer %s nothing that sends, stops, edits or deletes one', async (_who, signIn) => {
    signIn();
    const Page = await Campaigns();
    show(<Page />);

    expect(await screen.findByText('Wine evening')).toBeInTheDocument();
    for (const name of [/Send now/, /Send a test to me/, /Stop sending/, /Edit Founders dinner/, /Delete Founders dinner/, /New campaign/]) {
      expect(screen.queryByRole('button', { name })).toBeNull();
    }
  });

  it('offer whoever manages members to send one and stop one that is sending', async () => {
    as([PERMISSION.ClubMembers, PERMISSION.DeleteRecords]);
    const Page = await Campaigns();
    show(<Page />);

    expect(await screen.findByRole('button', { name: /Stop sending/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Send now/ })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Delete Founders dinner/ })).toBeInTheDocument();
  });
});

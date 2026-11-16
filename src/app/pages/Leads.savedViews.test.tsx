// Opening a saved view of the leads list: everything the view keeps (its tag and custom field
// conditions as well as the page's own filters and search) is what the server is asked for.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Lead } from '@/app/api/types';
import type { SavedView } from '@/app/api';
import { withQueries } from '@/test/queryWrapper';

const lead: Lead = { id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified' };
const page = (items: Lead[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false });

const api = vi.hoisted(() => ({
  getLeads: vi.fn(), getLeadsPaged: vi.fn(), getLeadStats: vi.fn(), getLeadById: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getDeals: vi.fn(), getPipelines: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLeadIds: vi.fn(),
  getSavedViews: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/organizations', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/organizations')>('@/app/api/organizations')),
  getOrgMembers: async () => [],
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, LEAD_DUPLICATES: {} }));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org, currentOrgId: org.id, organizations: [org] }),
  useOrgOptional: () => ({ currentOrg: org }),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn(), warning: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Leads = (await import('./Leads')).default;

const GOLD_REFERRALS: SavedView = {
  id: 'v1', list: 'leads', name: 'Gold referrals', shared: false, mine: true, isDefault: false,
  query: { field: ['tier:is:Gold'], tag: ['t-vip'], source: 'Referral', search: 'jean' },
  columns: null, version: 'v1', updatedAtUtc: '2026-11-13T00:00:00Z',
};

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  api.getLeads.mockResolvedValue([lead]);
  api.getLeadsPaged.mockResolvedValue(page([lead]));
  api.getLeadStats.mockResolvedValue(null);
  api.getCompanies.mockResolvedValue([]);
  api.getContacts.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLeadIds.mockResolvedValue([]);
  api.getSavedViews.mockResolvedValue([GOLD_REFERRALS]);
});

const lastQuery = () => api.getLeadsPaged.mock.calls[api.getLeadsPaged.mock.calls.length - 1]![0];

describe('a saved view of the leads list', () => {
  it('asks the server for its tag and field conditions as well as the filters and search it keeps', async () => {
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter initialEntries={['/leads']}><Leads /></MemoryRouter>));
    expect((await screen.findAllByText('Jean Dupont', undefined, { timeout: 5000 })).length).toBeGreaterThan(0);

    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /^Gold referrals/ }));

    const wanted = { source: 'Referral', search: 'jean', flex: { tags: ['t-vip'], fields: ['tier:is:Gold'] } };
    await waitFor(() => expect(lastQuery()).toMatchObject(wanted));
    // And it stays so once the page has written its own filters and search into the address.
    await new Promise((resolve) => setTimeout(resolve, 500));
    expect(lastQuery()).toMatchObject(wanted);
    expect(screen.getByRole('searchbox')).toHaveValue('jean');
  });
});

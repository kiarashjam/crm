// The Leads list when the server does not answer, and when a search is typed over.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Lead } from '@/app/api/types';
import { withQueries } from '@/test/queryWrapper';

const lead: Lead = { id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified' };
const page = (items: Lead[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false });

const api = vi.hoisted(() => ({
  getLeads: vi.fn(), getLeadsPaged: vi.fn(), getLeadStats: vi.fn(), getLeadById: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getDeals: vi.fn(), getPipelines: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLeadIds: vi.fn(),
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
});

const show = () => render(withQueries(<MemoryRouter initialEntries={['/leads']}><Leads /></MemoryRouter>));

describe('the leads list', () => {
  it('says the leads could not be loaded, with a retry, instead of the empty first-day screen', async () => {
    api.getLeadsPaged.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    show();

    expect(await screen.findByText('Leads could not be loaded.', undefined, { timeout: 5000 })).toBeInTheDocument();
    expect(toast.error).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect((await screen.findAllByText('Jean Dupont', undefined, { timeout: 5000 })).length).toBeGreaterThan(0);
  });

  it('shows the list loading, not the first-day screen, for a search typed after a failure', async () => {
    // Nothing was ever read, so the page has no rows to keep on screen. It used to say
    // "Start Building Your Pipeline" to a team with leads while the search was read.
    api.getLeadsPaged.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    show();
    await screen.findByText('Leads could not be loaded.', undefined, { timeout: 5000 });

    api.getLeadsPaged.mockImplementation(() => new Promise(() => {}));
    // Every node the page adds from here on. The first-day screen used to be drawn for one
    // render, before an effect swapped in the skeleton, so a look at the page afterwards
    // saw it only when it happened to land in between.
    const added: string[] = [];
    const watch = new MutationObserver((records) => {
      for (const record of records) for (const node of record.addedNodes) added.push(node.textContent ?? '');
    });
    watch.observe(document.body, { childList: true, subtree: true });
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'jean' } });
    await waitFor(() => expect(api.getLeadsPaged).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'jean' }), expect.anything()));
    await new Promise((resolve) => setTimeout(resolve, 50));
    watch.disconnect();

    expect(added.filter((text) => text.includes('Start Building Your Pipeline'))).toEqual([]);
    expect(screen.queryByText('Start Building Your Pipeline')).toBeNull();
    expect(screen.queryByText('Leads could not be loaded.')).toBeNull();
  });

  it('cancels the read for a search typed over, so it cannot land over the newer one', async () => {
    show();
    await screen.findAllByText('Jean Dupont', undefined, { timeout: 5000 });
    const signals = new Map<string, AbortSignal>();
    api.getLeadsPaged.mockImplementation((q: { search?: string }, req?: { signal?: AbortSignal }) => {
      if (q.search && req?.signal) signals.set(q.search, req.signal);
      return q.search === 'jea' ? new Promise(() => {}) : Promise.resolve(page([lead]));
    });

    const box = screen.getByRole('searchbox');
    fireEvent.change(box, { target: { value: 'jea' } });
    await waitFor(() => expect(signals.has('jea')).toBe(true));
    fireEvent.change(box, { target: { value: 'jean' } });
    await waitFor(() => expect(signals.has('jean')).toBe(true));

    await waitFor(() => expect(signals.get('jea')!.aborted).toBe(true));
    expect(signals.get('jean')!.aborted).toBe(false);
  });
});

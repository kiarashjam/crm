// The sales agent's shared lists: loaded once, kept fresh, never crossed.
//
// The panel used to remount with every page and re-read eight full lists each
// time, and never noticed an edit made beneath it — it went on telling you to
// "make the first call" after you had logged one. Then it re-read all eight after
// every save anywhere, so logging one call downloaded every lead, contact and deal
// again. These pin the cache that replaced both: what it loads, which lists a save
// re-reads, and what it refuses to show.

import type { ReactNode } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(), getDeals: vi.fn(), getTasks: vi.fn(), getContacts: vi.fn(), getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));

import { act, renderHook, waitFor } from '@testing-library/react';
import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { announceDataChanged, resourcesChangedBy } from '@/app/api/dataChanged';
import { refreshAfterWrites } from '@/app/hooks/queries/invalidation';
import { testQueryClient } from '@/test/queryWrapper';
import { FRESH_FOR_MS, SETTLE_MS, useAgentData } from './agentData';

const named = {
  leads: getLeads, deals: getDeals, tasks: getTasks, enrollments: getEnrollments, sequences: getSequences,
  contacts: getContacts, companies: getCompanies, automations: getAutomations,
} as const;
const all = Object.values(named).map((fn) => vi.mocked(fn as (...args: unknown[]) => Promise<unknown>));
const leads = vi.mocked(getLeads as (...args: unknown[]) => Promise<unknown>);
const deals = vi.mocked(getDeals as (...args: unknown[]) => Promise<unknown>);

const reads = () => leads.mock.calls.length;
/** Which lists were read since the counts were last cleared. */
const reread = () => (Object.keys(named) as (keyof typeof named)[]).filter((name) => vi.mocked(named[name]).mock.calls.length > 0);

let client: QueryClient;
let stopRefreshing: () => void;
// The app's cache outlives every page, and so does what refreshes it after saves.
const wrapper = ({ children }: { children: ReactNode }) => <QueryClientProvider client={client}>{children}</QueryClientProvider>;
const panel = (org: string, enabled = true) => renderHook(() => useAgentData(org, enabled), { wrapper });

function quiet() {
  for (const fn of all) fn.mockResolvedValue([]);
}

/** Let the reads answer and the cache tell its watchers. */
async function flush() {
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, 0)); });
}

/** Wait out a burst of saves. */
async function settle() {
  await act(async () => { await new Promise((resolve) => setTimeout(resolve, SETTLE_MS + 20)); });
  await flush();
}

beforeEach(() => {
  client = testQueryClient();
  stopRefreshing = refreshAfterWrites(client);
  for (const fn of all) fn.mockReset();
  quiet();
});

afterEach(() => {
  stopRefreshing();
  client.clear();
  vi.useRealTimers();
});

describe('loading', () => {
  it('reads the lists once, then serves them to every page', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    const first = panel('org-1');
    await waitFor(() => expect(first.result.current.lists?.leads).toHaveLength(1));
    expect(reads()).toBe(1);

    // A navigation: the old page's panel unmounts, the new one mounts.
    first.unmount();
    const second = panel('org-1');
    await flush();
    expect(second.result.current.lists?.leads).toHaveLength(1);
    expect(reads(), 'a navigation within the freshness window must not re-read').toBe(1);
    // ...and it has data on its very first render — no skeleton flash.
    expect(second.result.current.loading).toBe(false);
  });

  it('re-reads behind the scenes once the copy is older than the freshness window', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-10-17T09:00:00Z'));
    const first = panel('org-1');
    await waitFor(() => expect(first.result.current.lists).not.toBeNull());
    first.unmount();
    expect(reads()).toBe(1);

    vi.setSystemTime(Date.now() + FRESH_FOR_MS + 1);
    panel('org-1');
    await waitFor(() => expect(reads()).toBe(2));
  });
});

describe('freshness after a save', () => {
  it('re-reads the lists a save changed, once the burst settles, and only those', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada', pipelineState: null }]);
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.lists).not.toBeNull());
    for (const fn of all) fn.mockClear();

    // The lead page saves the pipeline, then logs an activity on the lead: two writes.
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada', pipelineState: '{"outreachStatus":"attempted_no_answer"}' }]);
    announceDataChanged({ resources: resourcesChangedBy('PUT', '/api/leads/l1/pipeline') });
    announceDataChanged({ resources: resourcesChangedBy('POST', '/api/activities', JSON.stringify({ leadId: 'l1' })) });
    expect(reads(), 'not before the burst settles').toBe(0);

    await settle();
    expect(reads(), 'one re-read for the whole burst').toBe(1);
    expect(result.current.lists?.leads[0]?.pipelineState).toContain('attempted_no_answer');
    // Deals, companies and automations were not touched, so they are not downloaded again.
    expect(reread()).not.toContain('deals');
    expect(reread()).not.toContain('companies');
    expect(reread()).not.toContain('automations');
    // The old lists stayed on screen throughout: a refresh, not a reload.
    expect(result.current.loading).toBe(false);
  });

  it('a task saved re-reads tasks alone', async () => {
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.lists).not.toBeNull());
    for (const fn of all) fn.mockClear();

    announceDataChanged({ resources: resourcesChangedBy('PUT', '/api/tasks/t-1') });
    await settle();

    expect(reread()).toEqual(['tasks']);
  });

  it('a club save re-reads nothing: checking a member in is not sales work', async () => {
    // Each check-in at the front desk used to re-download every lead, deal, task, contact and company.
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.lists).not.toBeNull());
    for (const fn of all) fn.mockClear();

    announceDataChanged({ resources: resourcesChangedBy('POST', '/api/visits') });
    await settle();

    expect(reread()).toEqual([]);
  });

  it('a save during the first read starts it again, so the old rows are not kept', async () => {
    let release!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { release = r; }));
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada after the save' }]);
    const { result } = panel('org-1');
    await flush();

    announceDataChanged({ resources: ['leads'] });
    await settle();
    await act(async () => { release([{ id: 'l1', name: 'Ada before the save' }]); });

    await waitFor(() => expect(result.current.lists?.leads[0]?.name).toBe('Ada after the save'));
    expect(reads()).toBe(2);
  });
});

describe('when no panel is on screen', () => {
  it('a save marks the copy stale instead of re-reading — the next panel re-reads', async () => {
    // On the till every item rung up is a write, and the panel is hidden there.
    const first = panel('org-1');
    await waitFor(() => expect(first.result.current.lists).not.toBeNull());
    expect(reads()).toBe(1);
    first.unmount();

    announceDataChanged({ resources: ['leads'] });
    announceDataChanged({ resources: ['leads'] });
    await settle();
    expect(reads(), 'nobody watching: no re-read').toBe(1);

    panel('org-1');
    await waitFor(() => expect(reads(), 'the next panel finds the copy stale and re-reads').toBe(2));
  });

  it('a hidden panel neither loads nor counts as watching', async () => {
    panel('org-1', false);
    await flush();
    expect(reads()).toBe(0);

    // And a save while only hidden panels exist does not trigger a read either.
    announceDataChanged({ resources: ['leads'] });
    await settle();
    expect(reads()).toBe(0);
  });
});

describe('workspaces never cross', () => {
  it("switching organisation shows nothing of the old organisation's lists", async () => {
    leads.mockResolvedValue([{ id: 'a1', name: 'Org A lead' }]);
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { wrapper, initialProps: { org: 'org-A' } });
    await waitFor(() => expect(result.current.lists?.leads[0]?.name).toBe('Org A lead'));

    let release!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { release = r; }));
    rerender({ org: 'org-B' });
    // B is still loading — and nothing of A's is shown meanwhile.
    expect(result.current.lists).toBeNull();
    expect(result.current.loading).toBe(true);

    await act(async () => { release([{ id: 'b1', name: 'Org B lead' }]); });
    await waitFor(() => expect(result.current.lists?.leads[0]?.name).toBe('Org B lead'));
  });

  it('a slow read for the old organisation cannot land in the new one', async () => {
    let releaseA!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { releaseA = r; }));
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { wrapper, initialProps: { org: 'org-A' } });
    await flush();

    leads.mockResolvedValueOnce([{ id: 'b1', name: 'Org B lead' }]);
    rerender({ org: 'org-B' });
    await waitFor(() => expect(result.current.lists?.leads[0]?.name).toBe('Org B lead'));

    // A's read finally answers. It must be dropped, not shown.
    await act(async () => { releaseA([{ id: 'a1', name: 'Org A lead' }]); });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');
  });

  it('switching while the old organisation is mid-read still loads the new one', async () => {
    leads.mockImplementationOnce(() => new Promise(() => {})); // A never answers
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { wrapper, initialProps: { org: 'org-A' } });
    await flush();

    leads.mockResolvedValueOnce([{ id: 'b1', name: 'Org B lead' }]);
    rerender({ org: 'org-B' });
    await waitFor(() => expect(result.current.lists?.leads[0]?.name).toBe('Org B lead'));
    expect(result.current.loading).toBe(false);
  });
});

describe('failure', () => {
  it('a total failure with nothing loaded says so', async () => {
    for (const fn of all) fn.mockRejectedValue(new Error('offline'));
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.failed).toBe(true));
    expect(result.current.lists).toBeNull();
    expect(result.current.loading).toBe(false);
  });

  it('a total failure on a re-read keeps the lists already shown', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.lists?.leads).toHaveLength(1));

    for (const fn of all) fn.mockRejectedValue(new Error('offline'));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.failed).toBe(true));
    expect(result.current.lists?.leads).toHaveLength(1);
  });

  it('one list failing keeps its last good copy instead of emptying it', async () => {
    deals.mockResolvedValue([{ id: 'd1', name: 'Big deal', dealStageName: 'Proposal' }]);
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.lists?.deals).toHaveLength(1));

    // Deals goes down; everything else answers. Reading that as "no deals need
    // anything" would be a quiet lie.
    deals.mockRejectedValue(new Error('deals offline'));
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.gaps).toEqual(['Deals could not be refreshed, so an earlier copy is used.']));
    expect(result.current.lists?.deals).toHaveLength(1);

    quiet();
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.gaps).toEqual([]));
  });

  it('a list that never loaded is named, not read as empty', async () => {
    deals.mockRejectedValue(new Error('deals offline'));
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.gaps).toEqual(['Deals could not be loaded.']));
    expect(result.current.lists?.deals).toEqual([]);
  });

  it('retry after a failure loads', async () => {
    for (const fn of all) fn.mockRejectedValueOnce(new Error('offline'));
    const { result } = panel('org-1');
    await waitFor(() => expect(result.current.failed).toBe(true));

    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    act(() => result.current.reload());
    await waitFor(() => expect(result.current.lists?.leads).toHaveLength(1));
    expect(result.current.failed).toBe(false);
  });
});

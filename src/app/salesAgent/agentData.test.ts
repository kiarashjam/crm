// The sales agent's shared lists: loaded once, kept fresh, never crossed.
//
// The panel used to remount with every page and re-read eight full lists each
// time, and never noticed an edit made beneath it — it went on telling you to
// "make the first call" after you had logged one. These pin the store that
// replaced that: what it loads, when it re-reads, and what it refuses to show.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(), getDeals: vi.fn(), getTasks: vi.fn(), getContacts: vi.fn(), getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { announceDataChanged } from '@/app/api/dataChanged';
import {
  FRESH_FOR_MS, SETTLE_MS, ensureFresh, reloadAgentData, resetAgentDataForTests, useAgentData, workspaceKey,
} from './agentData';
import { renderHook, act } from '@testing-library/react';

const all = [getLeads, getDeals, getTasks, getEnrollments, getSequences, getContacts, getCompanies, getAutomations]
  .map((fn) => vi.mocked(fn as (...args: unknown[]) => Promise<unknown>));
const leads = vi.mocked(getLeads as () => Promise<unknown>);
const deals = vi.mocked(getDeals as () => Promise<unknown>);

const reads = () => leads.mock.calls.length;

function quiet() {
  for (const fn of all) fn.mockResolvedValue([]);
}

/** Let every pending promise and microtask settle. */
async function flush() {
  await act(async () => {
    for (let i = 0; i < 5; i += 1) await Promise.resolve();
  });
}

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: false, toFake: ['setTimeout', 'clearTimeout', 'Date'] });
  vi.setSystemTime(new Date('2026-10-17T09:00:00Z'));
  resetAgentDataForTests();
  for (const fn of all) fn.mockReset();
  quiet();
});

afterEach(() => {
  resetAgentDataForTests();
  vi.useRealTimers();
});

describe('loading', () => {
  it('reads the lists once, then serves them to every page', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    const first = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(first.result.current.lists?.leads).toHaveLength(1);
    expect(reads()).toBe(1);

    // A navigation: the old page's panel unmounts, the new one mounts.
    first.unmount();
    const second = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(second.result.current.lists?.leads).toHaveLength(1);
    expect(reads(), 'a navigation within the freshness window must not re-read').toBe(1);
    // ...and it has data on its very first render — no skeleton flash.
    expect(second.result.current.loading).toBe(false);
  });

  it('re-reads behind the scenes once the copy is older than the freshness window', async () => {
    renderHook(() => useAgentData('org-1'));
    await flush();
    expect(reads()).toBe(1);

    vi.setSystemTime(Date.now() + FRESH_FOR_MS + 1);
    ensureFresh(workspaceKey('org-1'));
    await flush();
    expect(reads()).toBe(2);
  });
});

describe('freshness after a save', () => {
  it('a save anywhere re-reads the lists, once the burst settles', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada', pipelineState: null }]);
    const { result } = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(reads()).toBe(1);

    // The lead page saves the pipeline, then logs an activity: two writes.
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada', pipelineState: '{"outreachStatus":"attempted_no_answer"}' }]);
    announceDataChanged();
    announceDataChanged();
    expect(reads(), 'not before the burst settles').toBe(1);

    await act(async () => { vi.advanceTimersByTime(SETTLE_MS); });
    await flush();
    expect(reads(), 'one re-read for the whole burst').toBe(2);
    expect(result.current.lists?.leads[0]?.pipelineState).toContain('attempted_no_answer');
    // The old lists stayed on screen throughout: a refresh, not a reload.
    expect(result.current.loading).toBe(false);
  });

  it('a save during a read triggers one more read afterwards', async () => {
    let release!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { release = r; }));
    renderHook(() => useAgentData('org-1'));
    await flush();

    announceDataChanged();
    await act(async () => { vi.advanceTimersByTime(SETTLE_MS); });
    expect(reads(), 'still the first read').toBe(1);

    await act(async () => { release([]); });
    await flush();
    expect(reads(), 'what was being read may already be stale').toBe(2);
  });
});

describe('when no panel is on screen', () => {
  it('a save marks the copy stale instead of re-reading — the next panel re-reads', async () => {
    // On the till every item rung up is a write, and the panel is hidden there.
    // Re-reading eight lists per sale for a panel nobody can see would be waste.
    const first = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(reads()).toBe(1);
    first.unmount();

    announceDataChanged();
    announceDataChanged();
    await act(async () => { vi.advanceTimersByTime(SETTLE_MS * 4); });
    await flush();
    expect(reads(), 'nobody watching: no re-read').toBe(1);

    renderHook(() => useAgentData('org-1'));
    await flush();
    expect(reads(), 'the next panel finds the copy stale and re-reads').toBe(2);
  });

  it('a hidden panel neither loads nor counts as watching', async () => {
    renderHook(() => useAgentData('org-1', false));
    await flush();
    expect(reads()).toBe(0);

    // And a save while only hidden panels exist does not trigger a read either.
    announceDataChanged();
    await act(async () => { vi.advanceTimersByTime(SETTLE_MS); });
    await flush();
    expect(reads()).toBe(0);
  });
});

describe('workspaces never cross', () => {
  it("switching organisation discards the old organisation's lists at once", async () => {
    leads.mockResolvedValue([{ id: 'a1', name: 'Org A lead' }]);
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { initialProps: { org: 'org-A' } });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org A lead');

    let release!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { release = r; }));
    rerender({ org: 'org-B' });
    // B is still loading — and nothing of A's is shown meanwhile.
    expect(result.current.lists).toBeNull();
    expect(result.current.loading).toBe(true);

    await act(async () => { release([{ id: 'b1', name: 'Org B lead' }]); });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');
  });

  it("a slow read for the old organisation cannot land in the new one", async () => {
    let releaseA!: (v: unknown) => void;
    leads.mockImplementationOnce(() => new Promise((r) => { releaseA = r; }));
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { initialProps: { org: 'org-A' } });
    await flush();

    leads.mockResolvedValueOnce([{ id: 'b1', name: 'Org B lead' }]);
    rerender({ org: 'org-B' });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');

    // A's read finally answers. It must be dropped, not shown.
    await act(async () => { releaseA([{ id: 'a1', name: 'Org A lead' }]); });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');

    // ...and dropped for good, not merely unpublished. A late read that wrote
    // into the store without announcing it would surface on the NEXT update —
    // so cause one (a re-read that stays in flight) and look again.
    leads.mockImplementationOnce(() => new Promise(() => {}));
    await act(async () => { reloadAgentData(); });
    expect(result.current.refreshing).toBe(true);
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');
  });

  it('switching while the old organisation is mid-read still loads the new one', async () => {
    // The bug this store nearly shipped with: the new load merged into the old
    // in-flight read, whose results were then discarded — so B never loaded.
    leads.mockImplementationOnce(() => new Promise(() => {})); // A never answers
    const { result, rerender } = renderHook(({ org }) => useAgentData(org), { initialProps: { org: 'org-A' } });
    await flush();

    leads.mockResolvedValueOnce([{ id: 'b1', name: 'Org B lead' }]);
    rerender({ org: 'org-B' });
    await flush();
    expect(result.current.lists?.leads[0]?.name).toBe('Org B lead');
    expect(result.current.loading).toBe(false);
  });
});

describe('failure', () => {
  it('a total failure with nothing loaded says so', async () => {
    for (const fn of all) fn.mockRejectedValue(new Error('offline'));
    const { result } = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(result.current.failed).toBe(true);
    expect(result.current.lists).toBeNull();
    expect(result.current.loading).toBe(false);
  });

  it('a total failure on a re-read keeps the lists already shown', async () => {
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    const { result } = renderHook(() => useAgentData('org-1'));
    await flush();

    for (const fn of all) fn.mockRejectedValue(new Error('offline'));
    reloadAgentData();
    await flush();
    expect(result.current.lists?.leads).toHaveLength(1);
  });

  it('one list failing keeps its last good copy instead of emptying it', async () => {
    deals.mockResolvedValue([{ id: 'd1', name: 'Big deal', dealStageName: 'Proposal' }]);
    const { result } = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(result.current.lists?.deals).toHaveLength(1);

    // Deals goes down; everything else answers. Reading that as "no deals need
    // anything" would be a quiet lie.
    deals.mockRejectedValue(new Error('deals offline'));
    reloadAgentData();
    await flush();
    expect(result.current.lists?.deals).toHaveLength(1);
  });

  it('retry after a failure loads', async () => {
    for (const fn of all) fn.mockRejectedValueOnce(new Error('offline'));
    const { result } = renderHook(() => useAgentData('org-1'));
    await flush();
    expect(result.current.failed).toBe(true);

    quiet();
    leads.mockResolvedValue([{ id: 'l1', name: 'Ada' }]);
    reloadAgentData();
    await flush();
    expect(result.current.failed).toBe(false);
    expect(result.current.lists?.leads).toHaveLength(1);
  });
});

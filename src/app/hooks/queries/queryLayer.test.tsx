// The cache under every page: keyed by workspace, refreshed by what a save changed,
// emptied on a sign-out, and quick to give up on a read nobody needs.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';
import { announceDataChanged } from '@/app/api/dataChanged';
import { ApiError } from '@/app/api/apiClient';
import { AUTH_SESSION_CHANGED } from '@/app/lib/auth';
import { queryKeys, type WorkspaceScope } from './queryKeys';
import { clearOnSessionChange, keepOnlyWorkspace, refreshAfterWrites, SETTLE_MS } from './invalidation';
import { RequestTimeoutError, retryOnce, timed } from './queryClient';
import { keepPreviousInWorkspace } from './records';

const PAVILLON: WorkspaceScope = { orgId: 'pavillon', userId: 'u1' };
const LAC: WorkspaceScope = { orgId: 'lac-leman', userId: 'u1' };

let client: QueryClient;
let stop: () => void;

async function settle() {
  await vi.advanceTimersByTimeAsync(SETTLE_MS);
  await vi.runAllTimersAsync();
}

beforeEach(() => {
  vi.useFakeTimers();
  client = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: Infinity, gcTime: Infinity } } });
  stop = refreshAfterWrites(client);
});

afterEach(() => {
  stop();
  client.clear();
  vi.useRealTimers();
});

describe('after a save', () => {
  it('reads again only the kinds of record it changed', async () => {
    const tasks = client.fetchQuery({ queryKey: queryKeys.tasks.list(PAVILLON), queryFn: async () => ['t'] });
    const leads = client.fetchQuery({ queryKey: queryKeys.leads.all(PAVILLON), queryFn: async () => ['l'] });
    await Promise.all([tasks, leads]);

    announceDataChanged({ resources: ['tasks', 'work-queue'] });
    await settle();

    expect(client.getQueryState(queryKeys.tasks.list(PAVILLON))?.isInvalidated).toBe(true);
    expect(client.getQueryState(queryKeys.leads.all(PAVILLON))?.isInvalidated).toBe(false);
  });

  it('reads a list on screen again, once for a whole burst of saves', async () => {
    const read = vi.fn(async () => ['t']);
    const { QueryObserver } = await import('@tanstack/react-query');
    const observer = new QueryObserver(client, { queryKey: queryKeys.tasks.list(PAVILLON), queryFn: read });
    const unsubscribe = observer.subscribe(() => {});
    await vi.runAllTimersAsync();
    expect(read).toHaveBeenCalledTimes(1);

    // The task, then the activity the page logs about it.
    announceDataChanged({ resources: ['tasks'] });
    announceDataChanged({ resources: ['activities', 'tasks'] });
    expect(read, 'not before the burst settles').toHaveBeenCalledTimes(1);
    await settle();

    expect(read, 'one read for the burst').toHaveBeenCalledTimes(2);
    unsubscribe();
  });

  it('starts again a first read that was on its way when the save landed', async () => {
    // The read began before the save, so it may hold the old rows; TanStack would keep
    // it as fresh, since it had no data to restart from.
    let release!: (rows: string[]) => void;
    const read = vi.fn()
      .mockImplementationOnce(() => new Promise<string[]>((resolve) => { release = resolve; }))
      .mockResolvedValue(['after the save']);
    const { QueryObserver } = await import('@tanstack/react-query');
    const observer = new QueryObserver(client, { queryKey: queryKeys.leads.all(PAVILLON), queryFn: read });
    const unsubscribe = observer.subscribe(() => {});
    await vi.advanceTimersByTimeAsync(0);
    expect(read).toHaveBeenCalledTimes(1);

    announceDataChanged({ resources: ['leads'] });
    await settle();
    release(['before the save']);
    await vi.runAllTimersAsync();

    expect(read).toHaveBeenCalledTimes(2);
    expect(client.getQueryData(queryKeys.leads.all(PAVILLON))).toEqual(['after the save']);
    unsubscribe();
  });

  it('marks everything stale when a save could not say what it changed', async () => {
    await client.fetchQuery({ queryKey: queryKeys.deals.all(PAVILLON), queryFn: async () => [] });
    announceDataChanged();
    await settle();
    expect(client.getQueryState(queryKeys.deals.all(PAVILLON))?.isInvalidated).toBe(true);
  });
});

describe('workspaces and sessions', () => {
  it("drops other workspaces' reads when the tab switches", async () => {
    await client.fetchQuery({ queryKey: queryKeys.leads.all(PAVILLON), queryFn: async () => ['pavillon lead'] });
    await client.fetchQuery({ queryKey: queryKeys.leads.all(LAC), queryFn: async () => ['lac lead'] });

    keepOnlyWorkspace(client, 'lac-leman');

    expect(client.getQueryData(queryKeys.leads.all(PAVILLON))).toBeUndefined();
    expect(client.getQueryData(queryKeys.leads.all(LAC))).toEqual(['lac lead']);
  });

  it('keeps the last page on screen while the next is read, but never across workspaces', () => {
    // After a switch, the old workspace's rows must not stand in for the new one's.
    const keep = keepPreviousInWorkspace<string[]>(queryKeys.contacts.page(LAC, { page: 1 }));
    expect(keep(['lac, page 2'], { queryKey: queryKeys.contacts.page(LAC, { page: 2 }) })).toEqual(['lac, page 2']);
    expect(keep(['pavillon, page 1'], { queryKey: queryKeys.contacts.page(PAVILLON, { page: 1 }) })).toBeUndefined();
    expect(keep(['another account'], { queryKey: queryKeys.contacts.page({ orgId: 'lac-leman', userId: 'u2' }, { page: 1 }) })).toBeUndefined();
  });

  it('keys the same list apart for two workspaces', () => {
    expect(queryKeys.leads.all(PAVILLON)).not.toEqual(queryKeys.leads.all(LAC));
    expect(queryKeys.leads.all({ orgId: 'pavillon', userId: 'u2' })).not.toEqual(queryKeys.leads.all(PAVILLON));
  });

  it('forgets everything on a sign-in or sign-out', async () => {
    const off = clearOnSessionChange(client);
    await client.fetchQuery({ queryKey: queryKeys.leads.all(PAVILLON), queryFn: async () => ['lead'] });

    window.dispatchEvent(new Event(AUTH_SESSION_CHANGED));

    expect(client.getQueryData(queryKeys.leads.all(PAVILLON))).toBeUndefined();
    off();
  });
});

describe('a read', () => {
  it('is cancelled with the query', async () => {
    const controller = new AbortController();
    let seen: AbortSignal | undefined;
    const pending = timed(controller.signal, (signal) => {
      seen = signal;
      return new Promise((_, reject) => signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError'))));
    });
    controller.abort();
    await expect(pending).rejects.toThrow('aborted');
    expect(seen?.aborted).toBe(true);
  });

  it('gives up on a server that does not answer, and says so', async () => {
    const pending = timed(undefined, (signal) =>
      new Promise((_, reject) => signal.addEventListener('abort', () => reject(new DOMException('aborted', 'AbortError')))), 1000);
    const failed = expect(pending).rejects.toBeInstanceOf(RequestTimeoutError);
    await vi.advanceTimersByTimeAsync(1000);
    await failed;
  });

  it('is tried once more after a dropped connection or a server error, never after a refusal', () => {
    expect(retryOnce(0, new TypeError('Failed to fetch'))).toBe(true);
    expect(retryOnce(0, new ApiError('Server error', 500))).toBe(true);
    expect(retryOnce(0, new RequestTimeoutError())).toBe(true);
    expect(retryOnce(1, new ApiError('Server error', 500))).toBe(false);
    expect(retryOnce(0, new ApiError('You have read-only access.', 403))).toBe(false);
    expect(retryOnce(0, new ApiError('Not found', 404, '/api/leads/x', true))).toBe(false);
  });
});

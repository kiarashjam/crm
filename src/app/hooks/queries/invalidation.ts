// What keeps cached reads true after a save.
//
// Every successful write announces which kinds of record it may have changed
// (api/dataChanged). Here those kinds' cached reads are marked stale: the ones on
// screen are read again behind what they show, the rest when next shown. A task saved
// re-reads tasks, not every lead, deal and contact, as the sales agent used to.

import type { Query, QueryClient } from '@tanstack/react-query';
import { onDataChanged } from '@/app/api/dataChanged';
import { AUTH_SESSION_CHANGED } from '@/app/lib/auth';
import { isWorkspaceKey, resourceOfKey } from './queryKeys';

/** A save usually arrives as a burst (the record, then its activity log): one refresh for all of it. */
export const SETTLE_MS = 400;

/** Mark these kinds' cached reads stale, and read again the ones on screen. Null: every kind. */
export async function refreshResources(client: QueryClient, resources: ReadonlySet<string> | null): Promise<void> {
  const predicate = (query: Query) => {
    const resource = resourceOfKey(query.queryKey);
    return resource !== null && (resources === null || resources.has(resource));
  };
  // A first read still on its way began before the save, so it may hold the old rows.
  // TanStack restarts only a read that already has data; this one would be kept as
  // fresh. So it is dropped and started again.
  const firstReads = client.getQueryCache().findAll({ predicate })
    .filter((query) => query.state.fetchStatus === 'fetching' && query.state.data === undefined);
  if (firstReads.length > 0) await client.cancelQueries({ predicate: (query) => firstReads.includes(query) });
  await client.invalidateQueries({ predicate });
}

/**
 * Refresh what each write changed, once its burst settles. Returns the function that
 * stops listening.
 */
export function refreshAfterWrites(client: QueryClient, settleMs = SETTLE_MS): () => void {
  // The kinds the burst so far changed: undefined for none yet, null for every kind
  // (a write that could not say what it changed).
  let pending: Set<string> | null | undefined;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const stop = onDataChanged((change) => {
    if (change.resources === null) {
      pending = null;
    } else if (pending !== null) {
      pending ??= new Set();
      for (const resource of change.resources) pending.add(resource);
    }
    if (timer) clearTimeout(timer);
    timer = setTimeout(() => {
      timer = undefined;
      const resources = pending ?? null;
      pending = undefined;
      void refreshResources(client, resources);
    }, settleMs);
  });
  return () => {
    stop();
    if (timer) clearTimeout(timer);
  };
}

/**
 * Forget every cached read when someone signs in or out, here or in another tab: the
 * next account must not be shown the last one's records, even from the cache.
 */
export function clearOnSessionChange(client: QueryClient): () => void {
  if (typeof window === 'undefined') return () => {};
  const clear = () => client.clear();
  window.addEventListener(AUTH_SESSION_CHANGED, clear);
  return () => window.removeEventListener(AUTH_SESSION_CHANGED, clear);
}

/**
 * Drop the cached reads of every workspace but this one. Called when the tab switches
 * workspace, so going back later reads afresh instead of showing what was cached.
 */
export function keepOnlyWorkspace(client: QueryClient, orgId: string | null): void {
  client.removeQueries({ predicate: (query) => isWorkspaceKey(query.queryKey) && !isWorkspaceKey(query.queryKey, orgId) });
}

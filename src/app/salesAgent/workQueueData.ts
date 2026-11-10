// The rep's own work for the assistant's queue, from the server (GET /api/work-queue).
//
// Kept in the app's query cache, as the agent's lists are (agentData.ts): the panel sits
// in every page's header, so it remounts on each navigation, and re-reading the queue
// each time would be a request per page. It is read once per workspace, account and
// scope, re-read quietly after a save that changes leads, deals or tasks, when the tab
// comes back into view, or when older than FRESH_FOR_MS, and never shown under another
// workspace.

import { useCallback, useMemo } from 'react';
import { useQuery } from '@tanstack/react-query';
import { getWorkQueue, type WorkQueue } from '@/app/api/workQueue';
import type { WorkScope } from '@/app/lib/workScope';
import { getCurrentUser } from '@/app/lib/auth';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { timed } from '@/app/hooks/queries/queryClient';
import { FRESH_FOR_MS, workspaceKey } from './agentData';

export interface WorkQueueSnapshot {
  /** Which workspace, account and scope the queue is for. */
  key: string | null;
  queue: WorkQueue | null;
  /** First read in flight: nothing to show yet. */
  loading: boolean;
  /** The last read failed. An earlier queue, if any, is still shown. */
  failed: boolean;
  /** Read again now, behind whatever is shown: the panel's retry. */
  reload: () => void;
}

/**
 * The rep's open work for this workspace and scope. Off (no request) where the panel
 * does not show a queue: a record page, a hidden panel, a personal workspace.
 */
export function useWorkQueue(orgId: string | null | undefined, scope: WorkScope, enabled: boolean): WorkQueueSnapshot {
  const userId = getCurrentUser()?.id ?? null;
  const query = useQuery({
    queryKey: queryKeys.workQueue({ orgId: orgId ?? null, userId }, scope),
    queryFn: ({ signal }) => timed(signal, (s) => getWorkQueue(scope, { signal: s })),
    enabled,
    staleTime: FRESH_FOR_MS,
    retry: false,
  });
  const { refetch, data, status, isFetching } = query;
  const reload = useCallback(() => { void refetch(); }, [refetch]);
  const key = `${workspaceKey(orgId)}|${scope}`;
  return useMemo(() => ({
    key,
    queue: data ?? null,
    loading: data === undefined && (isFetching || status === 'pending'),
    failed: status === 'error',
    reload,
  }), [key, data, status, isFetching, reload]);
}

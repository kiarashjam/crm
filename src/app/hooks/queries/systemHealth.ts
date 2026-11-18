// Settings, System health: one read of what went wrong, kept fresh while the page is open.

import { useQuery } from '@tanstack/react-query';
import { getSystemHealth } from '@/app/api/systemHealth';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

/** How often the page reads it again while it is open (and the tab is in front). */
export const SYSTEM_HEALTH_REFRESH_MS = 60_000;

export function useSystemHealth(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.systemHealth(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getSystemHealth({ signal: s })),
    refetchInterval: SYSTEM_HEALTH_REFRESH_MS,
    enabled,
  });
}

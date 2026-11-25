// The recap emails, as cached queries keyed by workspace: who receives which (for those who
// manage the settings) and the signed-in person's own. Off outside a workspace, which has none.

import { useQuery } from '@tanstack/react-query';
import { getMyRecaps, getRecapRecipients } from '@/app/api/recaps';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

/** Who receives the workspace's recaps, and how often. Only for someone who manages the settings. */
export function useRecapRecipients(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.recaps.recipients(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getRecapRecipients({ signal: s })),
    enabled: enabled && ws.orgId !== null,
  });
}

/** Which recaps the signed-in person receives here, and whether they may. */
export function useMyRecaps(enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.recaps.mine(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getMyRecaps({ signal: s })),
    enabled: enabled && ws.orgId !== null,
  });
}

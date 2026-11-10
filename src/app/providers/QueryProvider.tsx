import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query';
import { ReactNode, useEffect, useState } from 'react';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { createAppQueryClient } from '@/app/hooks/queries/queryClient';
import { clearOnSessionChange, keepOnlyWorkspace, refreshAfterWrites } from '@/app/hooks/queries/invalidation';

interface QueryProviderProps {
  children: ReactNode;
  /** A client of the caller's own (a test's). By default the app makes one. */
  client?: QueryClient;
}

/**
 * The app's cache of reads. It refreshes what each save changed, and forgets
 * everything when someone signs in or out.
 */
export function QueryProvider({ children, client }: QueryProviderProps) {
  const [queryClient] = useState(() => client ?? createAppQueryClient());

  useEffect(() => refreshAfterWrites(queryClient), [queryClient]);
  useEffect(() => clearOnSessionChange(queryClient), [queryClient]);

  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}

/**
 * Placed inside the workspace provider: when this tab switches workspace, the reads
 * cached for the others are dropped.
 */
export function WorkspaceCacheGuard() {
  const client = useQueryClient();
  const orgId = useOrgOptional()?.currentOrgId ?? null;
  useEffect(() => {
    keepOnlyWorkspace(client, orgId);
  }, [client, orgId]);
  return null;
}

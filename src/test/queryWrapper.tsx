import type { ReactElement } from 'react';
import type { QueryClient } from '@tanstack/react-query';
import { createAppQueryClient } from '@/app/hooks/queries/queryClient';
import { QueryProvider } from '@/app/providers/QueryProvider';

/** A cache for one test: a failed read shows at once instead of being tried again. */
export function testQueryClient(): QueryClient {
  return createAppQueryClient({ queries: { retry: false } });
}

/**
 * A page under the app's real cache: keyed by workspace and refreshed after saves, as
 * in the app. Pass a client to keep one cache across two renders.
 */
export function withQueries(ui: ReactElement, client: QueryClient = testQueryClient()): ReactElement {
  return <QueryProvider client={client}>{ui}</QueryProvider>;
}

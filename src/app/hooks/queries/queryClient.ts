// How the app's cached reads behave: how long a copy is trusted, when a failed read is
// tried again, and how long a read may take.

import { QueryClient, type DefaultOptions } from '@tanstack/react-query';
import { ApiError } from '@/app/api/apiClient';

/** How long a read may take before it is given up and shown as failed. */
export const REQUEST_TIMEOUT_MS = 30_000;

/**
 * How long a copy is trusted before a page that shows it reads it again. A write made
 * here refreshes what it changed at once (invalidation.ts); this is for what others
 * change, which shows on the next visit to the page, or on coming back to the tab.
 */
export const FRESH_FOR_MS = 30_000;

/** The server took too long, so the read was given up. */
export class RequestTimeoutError extends Error {
  constructor() {
    super('The server took too long to answer.');
    this.name = 'RequestTimeoutError';
  }
}

/**
 * One more try for a dropped connection, a timeout or a server error. None for a
 * refusal (any 4xx): asking again for a record that is gone, or one the role may not
 * read, only makes the person wait longer for the same answer.
 */
export function retryOnce(failureCount: number, error: unknown): boolean {
  if (failureCount >= 1) return false;
  return !(error instanceof ApiError && error.status >= 400 && error.status < 500);
}

export function createAppQueryClient(defaults: DefaultOptions = {}): QueryClient {
  return new QueryClient({
    defaultOptions: {
      ...defaults,
      queries: {
        staleTime: FRESH_FOR_MS,
        gcTime: 10 * 60 * 1000,
        retry: retryOnce,
        // Only a copy older than its staleTime is read again on coming back.
        refetchOnWindowFocus: true,
        ...defaults.queries,
      },
      mutations: { retry: 0, ...defaults.mutations },
    },
  });
}

/**
 * Run a read with the query's signal, cut off after `ms`. The signal cancels the
 * request when nobody needs it any more (a search typed over, the page left, the
 * workspace switched). The time limit turns a server that never answers, which kept
 * a page on its loading state until the browser gave up minutes later, into a
 * failure the page can offer to retry.
 */
export async function timed<T>(
  signal: AbortSignal | undefined,
  read: (signal: AbortSignal) => Promise<T>,
  ms = REQUEST_TIMEOUT_MS,
): Promise<T> {
  const controller = new AbortController();
  const follow = () => controller.abort(signal?.reason);
  if (signal?.aborted) follow();
  else signal?.addEventListener('abort', follow, { once: true });
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, ms);
  try {
    return await read(controller.signal);
  } catch (err) {
    if (timedOut) throw new RequestTimeoutError();
    throw err;
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', follow);
  }
}

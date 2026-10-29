// The unsubscribe link at the foot of a workspace's email. Public: the person
// opening it has no account, and the token in the link is the whole permission.

import { getApiBaseUrl } from './apiClient';

export class UnsubscribeError extends Error {
  readonly status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'UnsubscribeError';
    this.status = status;
  }
}

/** Whose email the link stops, and to which address. */
export interface UnsubscribeTarget {
  workspaceName: string;
  email: string;
}

async function call(token: string, method: 'GET' | 'POST'): Promise<UnsubscribeTarget> {
  const base = getApiBaseUrl();
  if (!base) throw new UnsubscribeError('VITE_API_URL is not set', 0);
  let res: Response;
  try {
    res = await fetch(`${base}/api/public/unsubscribe/${encodeURIComponent(token)}`, { method });
  } catch {
    // Offline, DNS, CORS: no answer at all — not the same as a dead link.
    throw new UnsubscribeError('Could not reach the server', 0);
  }
  if (!res.ok) throw new UnsubscribeError('Not available', res.status);
  return res.json() as Promise<UnsubscribeTarget>;
}

/** What the link would stop. Opening it changes nothing: mail scanners open every link. */
export const getUnsubscribe = (token: string) => call(token, 'GET');

/** Stops the workspace emailing this address. */
export const confirmUnsubscribe = (token: string) => call(token, 'POST');

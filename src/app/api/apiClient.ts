/**
 * API client for the Cadence backend. When VITE_API_URL is set, all API modules
 * use this client to call the real backend (never mock/fake). Token is sent
 * when present; 401 from the backend clears session.
 */

import { clearSession, getAuthToken, getCurrentOrganizationId, isDemoMode } from '@/app/lib/auth';
import { markLocalOnly } from './localOnly';
import { announceDataChanged, changesRecords } from './dataChanged';
import { isNotOnServer } from './notOnServer';

export function getApiBaseUrl(): string | undefined {
  const url = import.meta.env.VITE_API_URL;
  return typeof url === 'string' && url.trim() ? url.trim().replace(/\/$/, '') : undefined;
}

/** Token used for API calls; must match lib/auth (setSession after login). */
export function getToken(): string | null {
  return getAuthToken();
}

/** True when backend is configured: frontend always calls real API, never mock. */
export function isUsingRealApi(): boolean {
  return Boolean(getApiBaseUrl());
}

/**
 * True when what the user sees should be this browser's sample data: a build
 * with no backend, or the "Explore the demo" user, who has no account and is
 * offered on every build. Local-only features skip the server for them, and
 * their stores keep the seeded demo rows.
 */
export function servesDemoData(): boolean {
  return !isUsingRealApi() || isDemoMode();
}

export type AuthFetchOptions = RequestInit & { skipAuth?: boolean };

export async function authFetch(path: string, options: AuthFetchOptions = {}): Promise<Response> {
  const base = getApiBaseUrl();
  if (!base) throw new Error('VITE_API_URL is not set');
  const { skipAuth, ...init } = options;
  const headers = new Headers(init.headers);
  if (!skipAuth) {
    const token = getAuthToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const orgId = getCurrentOrganizationId();
    if (orgId) headers.set('X-Organization-Id', orgId);
  }
  if (headers.get('Content-Type') == null && (init.body != null && typeof init.body === 'string')) {
    headers.set('Content-Type', 'application/json');
  }
  const url = path.startsWith('http') ? path : `${base}${path.startsWith('/') ? path : `/${path}`}`;
  // A feature the backend does not serve yet: answer its 404 here instead of
  // sending a request that can only fail (see notOnServer.ts).
  if (isNotOnServer(path)) return new Response(null, { status: 404, statusText: 'Not Found' });
  const res = await fetch(url, { ...init, headers });
  // Anonymous endpoints (e.g. forgot/reset password) must not clear an existing session on 401.
  if (res.status === 401 && !skipAuth) {
    clearSession();
  }
  // Every write the app makes passes through here, so this is where "your copy
  // may be stale" is said once for all of them. Anonymous calls (signing a
  // contract, the public capture form) are not the signed-in user's records.
  if (res.ok && !skipAuth && changesRecords(init.method, path)) {
    announceDataChanged();
  }
  return res;
}

/**
 * Pull a human-readable message out of an error body. The backend returns
 * RFC-9110 problem+json for failures (e.g. the read-only role rejection), which
 * would otherwise surface to the user as a wall of raw JSON.
 */
/** An unsuccessful API response. `status` lets callers branch on the HTTP code
 *  without pattern-matching the human-readable message. */
export class ApiError extends Error {
  readonly status: number;
  /** The request path, when known, so a fallback can name the feature it hit. */
  readonly path?: string;
  /**
   * Whether the server said anything. The backend answers a route it doesn't have
   * with a bare 404, and a record it can't find with a 404 that explains itself,
   * so this is what tells "no such feature" from "no such record".
   */
  readonly hasBody: boolean;

  constructor(message: string, status: number, path?: string, hasBody = false) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.path = path;
    this.hasBody = hasBody;
  }
}

function errorMessageFrom(text: string, res: Response): string {
  if (text) {
    try {
      const problem = JSON.parse(text) as { detail?: string; title?: string };
      const message = problem?.detail || problem?.title;
      if (message) return message;
    } catch {
      // not JSON — fall through and use the raw text
    }
    return text;
  }
  return res.statusText || `HTTP ${res.status}`;
}

export async function authFetchJson<T>(path: string, options: AuthFetchOptions = {}): Promise<T> {
  const res = await authFetch(path, options);
  const text = await res.text();
  if (!res.ok) {
    // The message stays human-readable (problem+json `detail`/`title`) because it
    // surfaces directly in toasts — a Viewer's refusal should read as a sentence.
    // The status rides along as a property instead of being prefixed onto the
    // text, so callers that need to branch on it (notifications skips endpoints
    // that 404) can do so without parsing prose.
    throw new ApiError(errorMessageFrom(text, res), res.status, path, text.length > 0);
  }
  if (!text) return undefined as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new Error('Invalid JSON response');
  }
}

/** Throw an {@link ApiError} for an unsuccessful response from a raw `authFetch`.
 *  Keeps the status, so `apiWithFallback` can tell a missing endpoint from a refusal. */
export async function ensureOk(res: Response): Promise<void> {
  if (res.ok) return;
  let path: string | undefined;
  try {
    path = res.url ? new URL(res.url).pathname : undefined;
  } catch {
    path = undefined;
  }
  const text = await res.text();
  throw new ApiError(errorMessageFrom(text, res), res.status, path, text.length > 0);
}

/**
 * The server answered that this route doesn't exist (or isn't implemented). A 404
 * with a body is the server explaining that a record wasn't found, which is an
 * answer, not a missing feature, and saving locally then would hide it.
 */
function isEndpointMissing(err: unknown): err is ApiError {
  return err instanceof ApiError && !err.hasBody && (err.status === 404 || err.status === 405 || err.status === 501);
}

/**
 * Run a real-API call, falling back to a local implementation when the backend
 * isn't connected or has no endpoint for the feature yet. Used by
 * frontend-built features (automations, custom fields, hospitality, …) so they
 * keep working, backed by this browser's storage, until the backend gains support.
 *
 * Only a missing endpoint falls back. Any other failure — a 403 for a Viewer, a
 * validation error, a record the server can't find, a 500, an unreachable server —
 * is rethrown, because saving locally then would report success for a write the
 * server refused or never saw.
 */
export async function apiWithFallback<T>(real: () => Promise<T>, local: () => Promise<T>): Promise<T> {
  if (servesDemoData()) return local();
  try {
    return await real();
  } catch (err) {
    if (!isEndpointMissing(err)) throw err;
    markLocalOnly(err.path);
    return local();
  }
}


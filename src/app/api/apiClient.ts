/**
 * API client for the Cadence backend. When VITE_API_URL is set, all API modules
 * use this client to call the real backend (never mock/fake). Token is sent
 * when present; 401 from the backend clears session.
 */

import { clearSession, getAuthToken, getCurrentOrganizationId, isDemoMode } from '@/app/lib/auth';
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
  // A feature the backend does not serve yet: answer a read's 404 here instead of
  // sending a request that can only fail (see notOnServer.ts). A write still goes
  // out, so the read-only middleware can refuse a Viewer with 403; answering it
  // here would save the Viewer's change in this browser instead.
  const method = (init.method ?? 'GET').toUpperCase();
  if ((method === 'GET' || method === 'HEAD') && isNotOnServer(path)) {
    return new Response(null, { status: 404, statusText: 'Not Found' });
  }
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
  /**
   * The server's error code (problem+json `errorCode`, e.g. "Contact.DoNotContact"),
   * for a caller whose next step depends on why it was refused, not only that it was.
   */
  readonly code?: string;

  constructor(message: string, status: number, path?: string, hasBody = false, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.path = path;
    this.hasBody = hasBody;
    this.code = code;
  }
}

function errorCodeFrom(text: string): string | undefined {
  try {
    const code = (JSON.parse(text) as { errorCode?: unknown } | null)?.errorCode;
    return typeof code === 'string' && code ? code : undefined;
  } catch {
    return undefined;
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
    throw new ApiError(errorMessageFrom(text, res), res.status, path, text.length > 0, errorCodeFrom(text));
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
  throw new ApiError(errorMessageFrom(text, res), res.status, path, text.length > 0, errorCodeFrom(text));
}

/**
 * The server answered that this route doesn't exist (or isn't implemented). A 404
 * with a body is the server explaining that a record wasn't found, which is an
 * answer, not a missing feature.
 */
function isEndpointMissing(err: unknown): err is ApiError {
  return err instanceof ApiError && !err.hasBody && (err.status === 404 || err.status === 405 || err.status === 501);
}

/** What a person is told when the server has no route for what they asked. */
export const NOT_ON_SERVER_MESSAGE = 'This part of Cadence is not on the server yet. Nothing was kept in this browser instead.';

/**
 * Run a real-API call, or with demo data, this browser's own implementation of it.
 *
 * Demo data lives in this browser and real data on the server, never a mix of the
 * two. This used to fall back to the browser's storage, with real data too, when the
 * server had no route for a feature (a bare 404, 405 or 501), so a member, a charge
 * or a gift card entered then was saved where no teammate and no other device would
 * ever see it, and the save looked like it had worked. Now such a call fails like
 * any other, with a sentence saying nothing was kept instead.
 *
 * Every other failure (a 403 for a Viewer, a validation error, a record the server
 * cannot find, a 500, an unreachable server) is rethrown as it is.
 */
export async function apiWithFallback<T>(real: () => Promise<T>, local: () => Promise<T>): Promise<T> {
  if (servesDemoData()) return local();
  try {
    return await real();
  } catch (err) {
    if (isEndpointMissing(err)) throw new ApiError(NOT_ON_SERVER_MESSAGE, err.status, err.path, false, err.code);
    throw err;
  }
}

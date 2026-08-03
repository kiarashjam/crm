/**
 * API client for the Cadence backend. When VITE_API_URL is set, all API modules
 * use this client to call the real backend (never mock/fake). Token is sent
 * when present; 401 from the backend clears session.
 */

import { clearSession, getAuthToken, getCurrentOrganizationId } from '@/app/lib/auth';

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
  const res = await fetch(url, { ...init, headers });
  // Anonymous endpoints (e.g. forgot/reset password) must not clear an existing session on 401.
  if (res.status === 401 && !skipAuth) {
    clearSession();
  }
  return res;
}

/**
 * Pull a human-readable message out of an error body. The backend returns
 * RFC-9110 problem+json for failures (e.g. the read-only role rejection), which
 * would otherwise surface to the user as a wall of raw JSON.
 */
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
    throw new Error(errorMessageFrom(text, res));
  }
  if (!text) return undefined as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new Error('Invalid JSON response');
  }
}

/**
 * Run a real-API call, falling back to a local implementation when the backend
 * isn't connected OR doesn't implement the endpoint (e.g. 404). Used by
 * frontend-built features (sequences, automations, custom fields, …) so they
 * keep working — backed by local storage — until the backend gains support.
 */
export async function apiWithFallback<T>(real: () => Promise<T>, local: () => Promise<T>): Promise<T> {
  if (!isUsingRealApi()) return local();
  try {
    return await real();
  } catch {
    return local();
  }
}


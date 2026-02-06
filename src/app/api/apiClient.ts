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
  if (res.status === 401) {
    clearSession();
  }
  return res;
}

export async function authFetchJson<T>(path: string, options: AuthFetchOptions = {}): Promise<T> {
  const res = await authFetch(path, options);
  const text = await res.text();
  if (!res.ok) {
    const msg = text || res.statusText || `HTTP ${res.status}`;
    throw new Error(msg);
  }
  if (!text) return undefined as T;
  try {
    return JSON.parse(text) as T;
  } catch {
    throw new Error('Invalid JSON response');
  }
}

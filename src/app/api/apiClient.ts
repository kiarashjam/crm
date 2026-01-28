/**
 * API client for the ACI backend. When VITE_API_URL is set, all API modules
 * use this client to call the real backend (never mock/fake). Token is sent
 * when present; 401 from the backend clears session.
 */

import { clearSession } from '@/app/lib/auth';

const TOKEN_KEY = 'crm_token';

export function getApiBaseUrl(): string | undefined {
  const url = import.meta.env.VITE_API_URL;
  return typeof url === 'string' && url.trim() ? url.trim().replace(/\/$/, '') : undefined;
}

export function getToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function setToken(token: string): void {
  try {
    localStorage.setItem(TOKEN_KEY, token);
  } catch {
    // ignore
  }
}

export function clearToken(): void {
  try {
    localStorage.removeItem(TOKEN_KEY);
  } catch {
    // ignore
  }
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
    const token = getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
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

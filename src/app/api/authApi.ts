/**
 * Auth API: login, register, logout against the real backend.
 */
import { authFetchJson } from './apiClient';
import { setSession, clearSession } from '@/app/lib/auth';
import type { AuthUser } from '@/app/lib/auth';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  name: string;
}

export interface LoginResponse {
  token: string;
  user: AuthUser;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string | null;
}

export async function apiLogin(body: LoginRequest): Promise<LoginResponse> {
  const res = await authFetchJson<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify(body),
    skipAuth: true,
  });
  if (res.requiresTwoFactor) return res;
  if (res.token && res.user) {
    setSession(res.token, res.user);
  }
  return res;
}

export async function apiRegister(body: RegisterRequest): Promise<LoginResponse> {
  const res = await authFetchJson<LoginResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify(body),
    skipAuth: true,
  });
  if (res.token && res.user) {
    setSession(res.token, res.user);
  }
  return res;
}

export function apiLogout(): void {
  clearSession();
}

import { apiGet, apiPost } from './http';

export type AuthUser = { id: string; name: string; email: string };

export type LoginResponse = {
  token: string | null;
  user: AuthUser | null;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string | null;
};

export function login(email: string, password: string) {
  return apiPost<LoginResponse>('/auth/login', { email, password });
}

export function loginWithTwoFactor(twoFactorToken: string, code: string) {
  return apiPost<LoginResponse>('/auth/login/2fa', { twoFactorToken, code });
}

export function register(name: string, email: string, password: string) {
  return apiPost<LoginResponse>('/auth/register', { name, email, password });
}

export function me() {
  return apiGet<AuthUser>('/auth/me');
}

export type TwoFactorSetupResponse = { enabled: boolean; secret: string; otpauthUri: string };

export function twoFactorSetup() {
  return apiPost<TwoFactorSetupResponse>('/auth/2fa/setup');
}

export function twoFactorEnable(code: string) {
  return apiPost<void>('/auth/2fa/enable', { code });
}

export function twoFactorDisable(password: string, code: string) {
  return apiPost<void>('/auth/2fa/disable', { password, code });
}


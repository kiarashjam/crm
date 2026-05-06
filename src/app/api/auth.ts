import { authFetchJson } from './apiClient';

export type AuthUser = { id: string; name: string; email: string };

export type LoginResponse = {
  token: string | null;
  user: AuthUser | null;
  requiresTwoFactor?: boolean;
  twoFactorToken?: string | null;
};

export function login(email: string, password: string) {
  return authFetchJson<LoginResponse>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
    skipAuth: true,
  });
}

export function loginWithTwoFactor(twoFactorToken: string, code: string) {
  return authFetchJson<LoginResponse>('/api/auth/login/2fa', {
    method: 'POST',
    body: JSON.stringify({ twoFactorToken, code }),
    skipAuth: true,
  });
}

export function register(name: string, email: string, password: string) {
  return authFetchJson<LoginResponse>('/api/auth/register', {
    method: 'POST',
    body: JSON.stringify({ name, email, password }),
    skipAuth: true,
  });
}

export function me() {
  return authFetchJson<AuthUser>('/api/auth/me');
}

export type TwoFactorSetupResponse = { enabled: boolean; secret: string; otpauthUri: string };

export function twoFactorSetup() {
  return authFetchJson<TwoFactorSetupResponse>('/api/auth/2fa/setup', { method: 'POST' });
}

export function twoFactorEnable(code: string) {
  return authFetchJson<void>('/api/auth/2fa/enable', {
    method: 'POST',
    body: JSON.stringify({ code }),
  });
}

export function twoFactorDisable(password: string, code: string) {
  return authFetchJson<void>('/api/auth/2fa/disable', {
    method: 'POST',
    body: JSON.stringify({ password, code }),
  });
}

export type ForgotPasswordResponse = { message: string };

export function requestPasswordReset(email: string) {
  return authFetchJson<ForgotPasswordResponse>('/api/auth/forgot-password', {
    method: 'POST',
    body: JSON.stringify({ email }),
    skipAuth: true,
  });
}

export function resetPassword(token: string, password: string) {
  return authFetchJson<void>('/api/auth/reset-password', {
    method: 'POST',
    body: JSON.stringify({ token, password }),
    skipAuth: true,
  });
}

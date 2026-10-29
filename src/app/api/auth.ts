import { authFetchJson, servesDemoData } from './apiClient';
import { replaceToken } from '@/app/lib/auth';

export type AuthUser = { id: string; name: string; email: string; emailVerified?: boolean };

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

/** Confirm the account's address with the token from the confirmation email. Works signed out. */
export function verifyEmail(token: string) {
  return authFetchJson<void>('/api/auth/verify-email', {
    method: 'POST',
    body: JSON.stringify({ token }),
    skipAuth: true,
  });
}

/** Email the signed-in user a new confirmation link. The server allows a few per 15 minutes. */
export function resendVerificationEmail() {
  return authFetchJson<void>('/api/auth/verify-email/resend', { method: 'POST' });
}

/** `secret` and `otpauthUri` are null once 2FA is on: the server never sends the live secret. */
export type TwoFactorSetupResponse = { enabled: boolean; secret: string | null; otpauthUri: string | null };

/** Only for turning 2FA on. To show whether it is on, ask {@link twoFactorStatus}. */
export async function twoFactorSetup(): Promise<TwoFactorSetupResponse> {
  // The demo user has no account to protect, and the server would answer 401, which
  // signs the session out.
  if (servesDemoData()) throw new Error('Two-factor sign-in is for accounts. The demo has none.');
  return authFetchJson<TwoFactorSetupResponse>('/api/auth/2fa/setup', { method: 'POST' });
}

/** Whether 2FA is on. What the Settings page asks every time it opens. */
export async function twoFactorStatus(): Promise<{ enabled: boolean }> {
  if (servesDemoData()) throw new Error('Two-factor sign-in is for accounts. The demo has none.');
  return authFetchJson<{ enabled: boolean }>('/api/auth/2fa/status');
}

/**
 * Turning 2FA on or off signs out every earlier session, this one included. The server
 * answers with a new token, which is stored here so this browser stays signed in.
 */
async function changeTwoFactor(path: string, body: unknown): Promise<void> {
  const res = await authFetchJson<{ token?: string } | undefined>(path, {
    method: 'POST',
    body: JSON.stringify(body),
  });
  if (res?.token) replaceToken(res.token);
}

export function twoFactorEnable(code: string) {
  return changeTwoFactor('/api/auth/2fa/enable', { code });
}

export function twoFactorDisable(password: string, code: string) {
  return changeTwoFactor('/api/auth/2fa/disable', { password, code });
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

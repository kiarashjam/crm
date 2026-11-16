// Signing in without a password: single sign-on with Google or Microsoft, and passkeys.
//
// Single sign-on is a round trip through the API: the browser goes to
// /api/auth/sso/{provider}/start, on to the provider, back to the API, and lands on the
// app's /auth/sso page with a one-time code in the address. The app trades that code for
// a session together with a secret only this tab holds (kept in sessionStorage, sent to the
// API at the start as its SHA-256), so a code copied into another browser is worth nothing.
//
// Passkeys use navigator.credentials directly: the server hands out WebAuthn options as
// JSON (binary fields base64url), the browser's answer goes back the same way, and the
// server verifies it.

import { authFetchJson, getApiBaseUrl, servesDemoData } from './apiClient';
import type { LoginResponse } from './auth';
import { getCurrentOrganizationId, getCurrentUser, setCurrentOrganizationId } from '@/app/lib/auth';
import { asLanguage, type Language } from '@/app/i18n/languages';
import { translate } from '@/app/i18n/current';
import type { MessageKey } from '@/app/i18n/catalog';

export type SsoProvider = { id: string; name: string };
export type SignInOptions = { sso: SsoProvider[]; passkeys: boolean };

/** What the sign-in page can offer besides the password. Nothing in the demo. */
export async function getSignInOptions(): Promise<SignInOptions> {
  if (servesDemoData()) return { sso: [], passkeys: false };
  return authFetchJson<SignInOptions>('/api/auth/sign-in-options', { skipAuth: true });
}

// ── base64url and binary ─────────────────────────────────────────────────────

export function bufferToBase64Url(value: ArrayBuffer | Uint8Array): string {
  const bytes = value instanceof Uint8Array ? value : new Uint8Array(value);
  let text = '';
  for (const byte of bytes) text += String.fromCharCode(byte);
  return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

export function base64UrlToBuffer(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  const text = atob(base64 + '='.repeat((4 - (base64.length % 4)) % 4));
  const bytes = new Uint8Array(text.length);
  for (let i = 0; i < text.length; i += 1) bytes[i] = text.charCodeAt(i);
  return bytes.buffer;
}

// ── single sign-on ───────────────────────────────────────────────────────────

const SSO_KEY = 'cadence.sso';

/**
 * The workspace a single sign-on started from, and who started it: signing in forgets the
 * workspace (setSession), as it must for someone else at the same desk, so the person who
 * left from a workspace that asked for single sign-on would otherwise come back to another.
 */
export type KeptWorkspace = { orgId: string; userId: string };

/**
 * `language` is the one picked on the sign-in page before leaving for the provider (null:
 * "follow the workspace"), absent when none was. The page is gone by the time the person
 * comes back, and the account's own, older language would otherwise win as the session
 * starts, as it would after a password sign-in that did not keep the pick.
 */
type PendingSso = {
  verifier: string; provider: string; returnTo: string | null; kept?: KeptWorkspace | null; language?: Language | null;
};

/** A language kept for the way back: null ("follow the workspace") or one the app has; anything else is none. */
function pickedLanguage(value: unknown): Language | null | undefined {
  if (value === null) return null;
  return typeof value === 'string' ? asLanguage(value) ?? undefined : undefined;
}

/** Only a path inside the app: the address the browser comes back to must not leave it. */
export function safeReturnTo(path: string | null | undefined): string | null {
  if (!path || !path.startsWith('/') || path.startsWith('//') || path.startsWith('/\\')) return null;
  if (path.startsWith('/login') || path.startsWith('/auth/sso')) return null;
  return path;
}

/** The S256 challenge for a verifier (RFC 7636): base64url(SHA-256(verifier)). */
export async function pkceChallenge(verifier: string): Promise<string> {
  const digest = await crypto.subtle.digest('SHA-256', new TextEncoder().encode(verifier));
  return bufferToBase64Url(digest);
}

/**
 * Where the browser goes to sign in with the provider. Keeps this tab's secret for the way
 * back, and the language picked on the sign-in page (`language`, undefined when none was).
 */
export async function ssoStartUrl(provider: string, returnTo?: string | null, language?: Language | null): Promise<string> {
  const base = getApiBaseUrl();
  if (!base) throw new Error(translate('login.sso.error.noServer'));
  const verifier = bufferToBase64Url(crypto.getRandomValues(new Uint8Array(32)));
  const orgId = getCurrentOrganizationId();
  const userId = getCurrentUser()?.id;
  const pending: PendingSso = {
    verifier, provider, returnTo: safeReturnTo(returnTo), kept: orgId && userId ? { orgId, userId } : null,
    ...(language !== undefined ? { language } : {}),
  };
  sessionStorage.setItem(SSO_KEY, JSON.stringify(pending));
  return `${base}/api/auth/sso/${encodeURIComponent(provider)}/start?challenge=${await pkceChallenge(verifier)}`;
}

/** Sends the browser to the provider. */
export async function startSso(provider: string, returnTo?: string | null, language?: Language | null): Promise<void> {
  window.location.assign(await ssoStartUrl(provider, returnTo, language));
}

/** What the API put in the /auth/sso address: a one-time code, or why it failed. */
export function readSsoLanding(hash: string): { code: string | null; error: string | null } {
  const params = new URLSearchParams(hash.replace(/^#/, ''));
  return { code: params.get('code'), error: params.get('error') };
}

/**
 * Trades the one-time code for a session (or the two-factor step), with this tab's secret.
 * The secret is used once: it is forgotten whether or not the server accepts it.
 */
export async function completeSso(code: string): Promise<{
  response: LoginResponse; returnTo: string | null; kept: KeptWorkspace | null; language: Language | null | undefined;
}> {
  let pending: PendingSso | null = null;
  try {
    pending = JSON.parse(sessionStorage.getItem(SSO_KEY) ?? 'null') as PendingSso | null;
  } catch {
    pending = null;
  }
  sessionStorage.removeItem(SSO_KEY);
  if (!pending?.verifier) {
    throw new Error(translate('login.sso.error.otherTab'));
  }
  const response = await authFetchJson<LoginResponse>('/api/auth/sso/complete', {
    method: 'POST',
    body: JSON.stringify({ code, verifier: pending.verifier }),
    skipAuth: true,
  });
  return { response, returnTo: pending.returnTo, kept: pending.kept ?? null, language: pickedLanguage(pending.language) };
}

/**
 * Back in the workspace the single sign-on started from, once signed in, when the person who
 * came back is the one who left it. Called right after setSession, before the workspaces are
 * read again, which keeps this tab's workspace when it is still one of theirs.
 */
export function keepWorkspaceFor(user: { id: string } | null | undefined, kept: KeptWorkspace | null | undefined): void {
  if (kept?.orgId && user?.id && kept.userId === user.id) setCurrentOrganizationId(kept.orgId);
}

/**
 * Why a single sign-on came back without signing in: the reason the API puts in the
 * address, and the catalogue's words for it (the sign-in page's, in its language).
 */
const SSO_ERRORS = {
  cancelled: 'login.sso.error.cancelled',
  expired: 'login.sso.error.expired',
  email_not_verified: 'login.sso.error.email_not_verified',
  invalid_token: 'login.sso.error.invalid_token',
  provider_error: 'login.sso.error.provider_error',
  provider_unavailable: 'login.sso.error.provider_unavailable',
  not_configured: 'login.sso.error.not_configured',
  bad_request: 'login.sso.error.bad_request',
  server_error: 'login.sso.error.server_error',
} as const satisfies Record<string, MessageKey>;

/** The words for the reason the API gave, or a general one for a reason it does not know. */
export function ssoErrorMessage(reason: string | null | undefined): string {
  return translate(reason && Object.prototype.hasOwnProperty.call(SSO_ERRORS, reason)
    ? SSO_ERRORS[reason as keyof typeof SSO_ERRORS]
    : SSO_ERRORS.server_error);
}

export type SsoLink = { id: string; provider: string; email: string; createdAtUtc: string; lastUsedAtUtc: string | null };

export function getSsoLinks(): Promise<SsoLink[]> {
  return authFetchJson<SsoLink[]>('/api/auth/sso/links');
}

export function unlinkSso(id: string): Promise<void> {
  return authFetchJson<void>(`/api/auth/sso/links/${id}`, { method: 'DELETE' });
}

// ── a workspace's requirement ───────────────────────────────────────────────

export type OrganizationSsoMember = {
  userId: string; name: string; email: string; isOwner: boolean; applies: boolean; hasSignedInWithSso: boolean;
};

export type OrganizationSso = {
  required: boolean;
  domains: string[];
  providers: SsoProvider[];
  canChange: boolean;
  members: OrganizationSsoMember[];
};

export function getOrganizationSso(orgId: string): Promise<OrganizationSso> {
  return authFetchJson<OrganizationSso>(`/api/organizations/${orgId}/sso`);
}

export function updateOrganizationSso(orgId: string, change: { required?: boolean; domains?: string[] }): Promise<OrganizationSso> {
  return authFetchJson<OrganizationSso>(`/api/organizations/${orgId}/sso`, { method: 'PUT', body: JSON.stringify(change) });
}

// ── passkeys ─────────────────────────────────────────────────────────────────

export type Passkey = { id: string; name: string; createdAtUtc: string; lastUsedAtUtc: string | null; synced: boolean };

type Ceremony = { ceremonyId: string; options: Record<string, unknown> };
type Descriptor = { type?: string; id: string; transports?: string[] };

/** Whether this browser can use passkeys at all. */
export function passkeysSupported(): boolean {
  return typeof window !== 'undefined'
    && typeof window.PublicKeyCredential !== 'undefined'
    && typeof navigator !== 'undefined'
    && typeof navigator.credentials !== 'undefined'
    && typeof navigator.credentials.create === 'function'
    && typeof navigator.credentials.get === 'function';
}

function descriptors(list: unknown): PublicKeyCredentialDescriptor[] | undefined {
  if (!Array.isArray(list)) return undefined;
  return (list as Descriptor[]).map((d) => ({
    type: 'public-key',
    id: base64UrlToBuffer(d.id),
    ...(d.transports?.length ? { transports: d.transports as AuthenticatorTransport[] } : {}),
  }));
}

/** The server's creation options as the browser wants them: its base64url fields as bytes. */
export function toCreationOptions(json: Record<string, unknown>): PublicKeyCredentialCreationOptions {
  const rp = json.rp as { id?: string; name: string };
  const user = json.user as { id: string; name: string; displayName: string };
  const selection = (json.authenticatorSelection ?? {}) as {
    authenticatorAttachment?: AuthenticatorAttachment; requireResidentKey?: boolean; userVerification?: UserVerificationRequirement;
  };
  return {
    rp: { id: rp.id, name: rp.name },
    user: { id: base64UrlToBuffer(user.id), name: user.name, displayName: user.displayName },
    challenge: base64UrlToBuffer(json.challenge as string),
    pubKeyCredParams: ((json.pubKeyCredParams ?? []) as { alg: number }[]).map((p) => ({ type: 'public-key', alg: p.alg })),
    timeout: typeof json.timeout === 'number' ? json.timeout : undefined,
    excludeCredentials: descriptors(json.excludeCredentials),
    authenticatorSelection: {
      ...(selection.authenticatorAttachment ? { authenticatorAttachment: selection.authenticatorAttachment } : {}),
      requireResidentKey: Boolean(selection.requireResidentKey),
      residentKey: selection.requireResidentKey ? 'required' : 'preferred',
      userVerification: selection.userVerification ?? 'preferred',
    },
    attestation: (json.attestation as AttestationConveyancePreference | undefined) ?? 'none',
  };
}

/** The server's request options as the browser wants them. */
export function toRequestOptions(json: Record<string, unknown>): PublicKeyCredentialRequestOptions {
  return {
    challenge: base64UrlToBuffer(json.challenge as string),
    timeout: typeof json.timeout === 'number' ? json.timeout : undefined,
    rpId: typeof json.rpId === 'string' ? json.rpId : undefined,
    allowCredentials: descriptors(json.allowCredentials) ?? [],
    userVerification: (json.userVerification as UserVerificationRequirement | undefined) ?? 'preferred',
  };
}

/** The browser's answer as the server reads it: its bytes as base64url. */
export function credentialToJson(credential: PublicKeyCredential): Record<string, unknown> {
  const response = credential.response as AuthenticatorAttestationResponse & AuthenticatorAssertionResponse;
  const answer: Record<string, unknown> = { clientDataJSON: bufferToBase64Url(response.clientDataJSON) };
  if ('attestationObject' in response && response.attestationObject) {
    answer.attestationObject = bufferToBase64Url(response.attestationObject);
  } else {
    answer.authenticatorData = bufferToBase64Url(response.authenticatorData);
    answer.signature = bufferToBase64Url(response.signature);
    answer.userHandle = response.userHandle ? bufferToBase64Url(response.userHandle) : null;
  }
  return { id: credential.id, rawId: bufferToBase64Url(credential.rawId), type: credential.type, response: answer };
}

/** What a passkey prompt that did not finish means, in words. */
export function passkeyPromptError(err: unknown): Error {
  const name = err instanceof DOMException || err instanceof Error ? err.name : '';
  if (name === 'NotAllowedError' || name === 'AbortError') return new Error(translate('login.passkeyError.closed'));
  if (name === 'InvalidStateError') return new Error(translate('login.passkeyError.alreadyAdded'));
  if (name === 'SecurityError') return new Error(translate('login.passkeyError.notHere'));
  return err instanceof Error ? err : new Error(translate('login.passkeyError.failed'));
}

async function ask<T extends Credential>(run: () => Promise<Credential | null>): Promise<T> {
  let credential: Credential | null;
  try {
    credential = await run();
  } catch (err) {
    throw passkeyPromptError(err);
  }
  if (!credential) throw new Error(translate('login.passkeyError.noneChosen'));
  return credential as T;
}

export function listPasskeys(): Promise<Passkey[]> {
  return authFetchJson<Passkey[]>('/api/auth/passkeys');
}

/** Adds a passkey on this device. `password` confirms it is the account's owner (not needed for an account without one). */
export async function addPasskey(name: string, password: string): Promise<Passkey> {
  const ceremony = await authFetchJson<Ceremony>('/api/auth/passkeys/registration/options', {
    method: 'POST',
    body: JSON.stringify(password ? { password } : {}),
  });
  const credential = await ask<PublicKeyCredential>(() => navigator.credentials.create({ publicKey: toCreationOptions(ceremony.options) }));
  return authFetchJson<Passkey>('/api/auth/passkeys/registration', {
    method: 'POST',
    body: JSON.stringify({ ceremonyId: ceremony.ceremonyId, name, credential: credentialToJson(credential) }),
  });
}

export function removePasskey(id: string): Promise<void> {
  return authFetchJson<void>(`/api/auth/passkeys/${id}`, { method: 'DELETE' });
}

/** Signs in with any passkey this browser has for the site. */
export async function signInWithPasskey(): Promise<LoginResponse> {
  const ceremony = await authFetchJson<Ceremony>('/api/auth/passkeys/sign-in/options', { method: 'POST', body: '{}', skipAuth: true });
  const credential = await ask<PublicKeyCredential>(() => navigator.credentials.get({ publicKey: toRequestOptions(ceremony.options) }));
  return authFetchJson<LoginResponse>('/api/auth/passkeys/sign-in', {
    method: 'POST',
    body: JSON.stringify({ ceremonyId: ceremony.ceremonyId, credential: credentialToJson(credential) }),
    skipAuth: true,
  });
}

/** Uses a passkey in place of the authenticator code, for the step token sign-in handed out. */
export async function passkeySecondFactor(twoFactorToken: string): Promise<LoginResponse> {
  const ceremony = await authFetchJson<Ceremony>('/api/auth/passkeys/second-factor/options', {
    method: 'POST',
    body: JSON.stringify({ twoFactorToken }),
    skipAuth: true,
  });
  const credential = await ask<PublicKeyCredential>(() => navigator.credentials.get({ publicKey: toRequestOptions(ceremony.options) }));
  return authFetchJson<LoginResponse>('/api/auth/passkeys/second-factor', {
    method: 'POST',
    body: JSON.stringify({ twoFactorToken, ceremonyId: ceremony.ceremonyId, credential: credentialToJson(credential) }),
    skipAuth: true,
  });
}

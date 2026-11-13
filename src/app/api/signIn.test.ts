// The browser's half of single sign-on and passkeys: the PKCE secret kept for the way back,
// and WebAuthn's bytes turned into what the browser wants and back into what the server reads.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import {
  base64UrlToBuffer, bufferToBase64Url, completeSso, credentialToJson, pkceChallenge, readSsoLanding,
  safeReturnTo, ssoErrorMessage, ssoStartUrl, toCreationOptions, toRequestOptions,
} from './signIn';

const BASE = 'http://api.test';

beforeEach(() => {
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
});

afterEach(() => {
  vi.unstubAllEnvs();
});

describe('base64url', () => {
  it('round-trips bytes, including those that need padding and the url-safe characters', () => {
    const bytes = new Uint8Array([0xfb, 0xff, 0xbf, 0x00, 0x01]);
    const text = bufferToBase64Url(bytes);
    expect(text).toBe('-_-_AAE');
    expect(new Uint8Array(base64UrlToBuffer(text))).toEqual(bytes);
  });
});

describe('single sign-on', () => {
  it('computes the S256 challenge as RFC 7636 does', async () => {
    // The specification's own example (appendix B).
    expect(await pkceChallenge('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk')).toBe('E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM');
  });

  it('starts at the API with the hash of a secret it keeps in this tab', async () => {
    const url = new URL(await ssoStartUrl('google', '/members'));
    expect(url.origin + url.pathname).toBe(`${BASE}/api/auth/sso/google/start`);
    const kept = JSON.parse(sessionStorage.getItem('cadence.sso')!) as { verifier: string; returnTo: string };
    expect(kept.verifier).toMatch(/^[A-Za-z0-9_-]{43}$/);
    expect(url.searchParams.get('challenge')).toBe(await pkceChallenge(kept.verifier));
    expect(kept.returnTo).toBe('/members');
  });

  it('comes back only to a path inside the app', () => {
    expect(safeReturnTo('/deals/42')).toBe('/deals/42');
    expect(safeReturnTo('https://evil.example')).toBeNull();
    expect(safeReturnTo('//evil.example/x')).toBeNull();
    expect(safeReturnTo('/\\evil.example')).toBeNull();
    expect(safeReturnTo('/login')).toBeNull();
  });

  it('trades the code with the kept secret once, and forgets the secret', async () => {
    let sent: unknown;
    server.use(http.post(`${BASE}/api/auth/sso/complete`, async ({ request }) => {
      sent = await request.json();
      return HttpResponse.json({ token: 't', user: { id: 'u', name: 'Anna', email: 'anna@pavillon46.ch' }, requiresTwoFactor: false });
    }));
    await ssoStartUrl('microsoft', '/calendar');
    const { verifier } = JSON.parse(sessionStorage.getItem('cadence.sso')!) as { verifier: string };

    const done = await completeSso('one-time-code');
    expect(sent).toEqual({ code: 'one-time-code', verifier });
    expect(done.returnTo).toBe('/calendar');
    expect(sessionStorage.getItem('cadence.sso')).toBeNull();

    // A second tab, or a code pasted elsewhere, holds no secret.
    await expect(completeSso('one-time-code')).rejects.toThrow(/another browser or tab/);
  });

  it('carries the language picked on the sign-in page through the provider, and only one the app has', async () => {
    server.use(http.post(`${BASE}/api/auth/sso/complete`, () =>
      HttpResponse.json({ token: 't', user: { id: 'u', name: 'Anna', email: 'anna@pavillon46.ch' }, requiresTwoFactor: false })));

    await ssoStartUrl('google', null, 'de');
    expect((await completeSso('code')).language).toBe('de');
    // "Follow the workspace" is a choice too.
    await ssoStartUrl('google', null, null);
    expect((await completeSso('code')).language).toBeNull();
    // Nothing picked: nothing carried, and the account's own language stands.
    await ssoStartUrl('google');
    expect(JSON.parse(sessionStorage.getItem('cadence.sso')!)).not.toHaveProperty('language');
    expect((await completeSso('code')).language).toBeUndefined();
    // A value the app has no catalogue for is no choice.
    sessionStorage.setItem('cadence.sso', JSON.stringify({ verifier: 'v', provider: 'google', returnTo: null, language: 'xx' }));
    expect((await completeSso('code')).language).toBeUndefined();
  });

  it('reads the landing address and says each failure in words', () => {
    expect(readSsoLanding('#code=abc')).toEqual({ code: 'abc', error: null });
    expect(readSsoLanding('#error=email_not_verified')).toEqual({ code: null, error: 'email_not_verified' });
    expect(ssoErrorMessage('cancelled')).toMatch(/cancelled/);
    expect(ssoErrorMessage('made-up')).toBe(ssoErrorMessage('server_error'));
  });
});

describe('passkey options and answers', () => {
  const b = (...bytes: number[]) => bufferToBase64Url(new Uint8Array(bytes));

  it('turns the server’s creation options into the browser’s, binary fields as bytes', () => {
    const options = toCreationOptions({
      rp: { id: 'app.cadence.test', name: 'Cadence' },
      user: { id: b(1, 2, 3), name: 'anna@pavillon46.ch', displayName: 'Anna' },
      challenge: b(9, 9, 9),
      pubKeyCredParams: [{ type: 'public-key', alg: -7 }, { type: 'public-key', alg: -257 }],
      timeout: 60000,
      excludeCredentials: [{ type: 'public-key', id: b(4, 5) }],
      authenticatorSelection: { requireResidentKey: true, userVerification: 'required' },
      attestation: 'none',
      status: 'ok',
    });
    expect(new Uint8Array(options.challenge as ArrayBuffer)).toEqual(new Uint8Array([9, 9, 9]));
    expect(new Uint8Array(options.user.id as ArrayBuffer)).toEqual(new Uint8Array([1, 2, 3]));
    expect(options.pubKeyCredParams.map((p) => p.alg)).toEqual([-7, -257]);
    expect(new Uint8Array(options.excludeCredentials![0]!.id as ArrayBuffer)).toEqual(new Uint8Array([4, 5]));
    expect(options.authenticatorSelection).toMatchObject({ residentKey: 'required', requireResidentKey: true, userVerification: 'required' });
  });

  it('turns request options into the browser’s, with the credentials it may use', () => {
    const options = toRequestOptions({ challenge: b(7), allowCredentials: [{ type: 'public-key', id: b(8) }], userVerification: 'preferred', rpId: 'app.cadence.test' });
    expect(new Uint8Array(options.allowCredentials![0]!.id as ArrayBuffer)).toEqual(new Uint8Array([8]));
    expect(options.rpId).toBe('app.cadence.test');
  });

  it('turns the browser’s answers into base64url, attestation and assertion alike', () => {
    const buf = (...bytes: number[]) => new Uint8Array(bytes).buffer;
    const attestation = credentialToJson({
      id: 'cred', rawId: buf(1), type: 'public-key',
      response: { clientDataJSON: buf(2), attestationObject: buf(3) },
    } as unknown as PublicKeyCredential);
    expect(attestation).toEqual({ id: 'cred', rawId: b(1), type: 'public-key', response: { clientDataJSON: b(2), attestationObject: b(3) } });

    const assertion = credentialToJson({
      id: 'cred', rawId: buf(1), type: 'public-key',
      response: { clientDataJSON: buf(2), authenticatorData: buf(4), signature: buf(5), userHandle: buf(6) },
    } as unknown as PublicKeyCredential);
    expect(assertion.response).toEqual({ clientDataJSON: b(2), authenticatorData: b(4), signature: b(5), userHandle: b(6) });
  });
});

// The sign-in page's other ways in: Google and Microsoft when the server offers them, a
// passkey instead of the password, and a passkey in place of the authenticator code.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { getAuthToken, getCurrentOrganizationId } from '@/app/lib/auth';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));
const startSso = vi.hoisted(() => vi.fn());
vi.mock('@/app/api/signIn', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/signIn')>()), startSso }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';
const buf = (...bytes: number[]) => new Uint8Array(bytes).buffer;
const b64 = (...bytes: number[]) => btoa(String.fromCharCode(...bytes)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

/** What a passkey answers navigator.credentials.get with. */
const assertion = {
  id: 'cred-1', rawId: buf(1, 2), type: 'public-key',
  response: { clientDataJSON: buf(3), authenticatorData: buf(4), signature: buf(5), userHandle: buf(6) },
};

function page(state?: unknown) {
  return render(withQueries(
    <MemoryRouter initialEntries={[{ pathname: '/login', state }]}>
      <Routes>
        <Route path="/login" element={<Login />} />
        <Route path="/organizations" element={<p>Workspaces page</p>} />
        <Route path="/dashboard" element={<p>Workspaces page</p>} />
        <Route path="/members" element={<p>Members page</p>} />
      </Routes>
    </MemoryRouter>,
  ));
}

let get: ReturnType<typeof vi.fn>;

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  startSso.mockReset();
  get = vi.fn(async () => assertion);
  vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
  Object.defineProperty(navigator, 'credentials', { configurable: true, value: { get, create: vi.fn() } });
});

afterEach(async () => {
  cleanup();
  vi.unstubAllGlobals();
  vi.unstubAllEnvs();
  await new Promise((resolve) => setTimeout(resolve, 60));
});

describe('other ways to sign in', () => {
  it('offers the providers the server is set up for, and starts there', async () => {
    server.use(http.get(`${BASE}/api/auth/sign-in-options`, () =>
      HttpResponse.json({ sso: [{ id: 'google', name: 'Google' }, { id: 'microsoft', name: 'Microsoft' }], passkeys: false })));
    const user = userEvent.setup();
    page();

    await user.click(await screen.findByRole('button', { name: 'Continue with Microsoft' }));
    expect(startSso).toHaveBeenCalledWith('microsoft', null);
    expect(screen.getByRole('button', { name: 'Continue with Google' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
  });

  it('shows nothing the server does not offer', async () => {
    let asked = false;
    server.use(http.get(`${BASE}/api/auth/sign-in-options`, () => { asked = true; return HttpResponse.json({ sso: [], passkeys: false }); }));
    page();
    await waitFor(() => expect(asked).toBe(true));
    expect(screen.queryByRole('button', { name: /Continue with/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sign in with a passkey' })).not.toBeInTheDocument();
  });

  it('signs in with a passkey instead of the password', async () => {
    let answered: { ceremonyId: string; credential: { rawId: string; response: Record<string, string> } } | undefined;
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })),
      http.post(`${BASE}/api/auth/passkeys/sign-in/options`, () =>
        HttpResponse.json({ ceremonyId: 'ceremony-1', options: { challenge: b64(9, 9), allowCredentials: [], userVerification: 'required' } })),
      http.post(`${BASE}/api/auth/passkeys/sign-in`, async ({ request }) => {
        answered = await request.json() as typeof answered;
        return HttpResponse.json({ token: 'passkey-session', user: { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true }, requiresTwoFactor: false });
      }),
    );
    const user = userEvent.setup();
    page();

    await user.click(await screen.findByRole('button', { name: 'Sign in with a passkey' }));

    expect(await screen.findByText('Workspaces page')).toBeInTheDocument();
    expect(getAuthToken()).toBe('passkey-session');
    const asked = get.mock.calls[0]![0] as { publicKey: PublicKeyCredentialRequestOptions };
    expect(new Uint8Array(asked.publicKey.challenge as ArrayBuffer)).toEqual(new Uint8Array([9, 9]));
    expect(answered?.ceremonyId).toBe('ceremony-1');
    expect(answered?.credential.rawId).toBe(b64(1, 2));
    expect(answered?.credential.response.signature).toBe(b64(5));
  });

  it('says so when the passkey prompt is closed, and stays on the page', async () => {
    get.mockRejectedValueOnce(new DOMException('The operation was cancelled', 'NotAllowedError'));
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })),
      http.post(`${BASE}/api/auth/passkeys/sign-in/options`, () =>
        HttpResponse.json({ ceremonyId: 'ceremony-1', options: { challenge: b64(9), allowCredentials: [] } })),
    );
    const user = userEvent.setup();
    page();

    await user.click(await screen.findByRole('button', { name: 'Sign in with a passkey' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/closed before it finished/);
    expect(getAuthToken()).toBeNull();
  });

  it('after single sign-on with 2FA on, asks for the code, and takes a passkey in its place', async () => {
    let sentToken: string | undefined;
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })),
      http.post(`${BASE}/api/auth/passkeys/second-factor/options`, async ({ request }) => {
        sentToken = ((await request.json()) as { twoFactorToken: string }).twoFactorToken;
        return HttpResponse.json({ ceremonyId: 'ceremony-2', options: { challenge: b64(7), allowCredentials: [{ type: 'public-key', id: b64(1, 2) }] } });
      }),
      http.post(`${BASE}/api/auth/passkeys/second-factor`, () =>
        HttpResponse.json({ token: 'sso-session', user: { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true }, requiresTwoFactor: false })),
    );
    const user = userEvent.setup();
    page({ twoFactorToken: 'step-from-sso', returnTo: '/members' });

    expect(screen.getByRole('heading', { name: 'Two-factor verification' })).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Use a passkey instead' }));

    expect(await screen.findByText('Members page')).toBeInTheDocument();
    expect(sentToken).toBe('step-from-sso');
    expect(getAuthToken()).toBe('sso-session');
    const asked = get.mock.calls[0]![0] as { publicKey: PublicKeyCredentialRequestOptions };
    expect(new Uint8Array(asked.publicKey.allowCredentials![0]!.id as ArrayBuffer)).toEqual(new Uint8Array([1, 2]));
  });

  it('offers no passkey in place of the code where the server takes none', async () => {
    let asked = false;
    server.use(http.get(`${BASE}/api/auth/sign-in-options`, () => { asked = true; return HttpResponse.json({ sso: [], passkeys: false }); }));
    page({ twoFactorToken: 'step-from-sso' });

    expect(screen.getByRole('heading', { name: 'Two-factor verification' })).toBeInTheDocument();
    await waitFor(() => expect(asked).toBe(true));
    expect(screen.queryByRole('button', { name: 'Use a passkey instead' })).not.toBeInTheDocument();
  });

  it('after the code, stays in the workspace the single sign-on started from for the same person', async () => {
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })),
      http.post(`${BASE}/api/auth/passkeys/second-factor/options`, () =>
        HttpResponse.json({ ceremonyId: 'ceremony-3', options: { challenge: b64(7), allowCredentials: [{ type: 'public-key', id: b64(1, 2) }] } })),
      http.post(`${BASE}/api/auth/passkeys/second-factor`, () =>
        HttpResponse.json({ token: 'sso-session', user: { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true }, requiresTwoFactor: false })),
    );
    const user = userEvent.setup();
    page({ twoFactorToken: 'step-from-sso', returnTo: '/members', kept: { orgId: 'pavillon', userId: 'u1' } });

    await user.click(await screen.findByRole('button', { name: 'Use a passkey instead' }));

    expect(await screen.findByText('Members page')).toBeInTheDocument();
    expect(getCurrentOrganizationId()).toBe('pavillon');
  });
});

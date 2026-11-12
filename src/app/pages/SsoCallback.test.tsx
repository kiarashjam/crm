// Where single sign-on lands: the one-time code from the address is traded, with this tab's
// secret, for the session; the two-factor step goes on at the sign-in page; a failure says why.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { getAuthToken } from '@/app/lib/auth';
import { ssoStartUrl } from '@/app/api/signIn';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));

const SsoCallback = (await import('./SsoCallback')).default;

const BASE = 'http://api.test';

function LoginStub() {
  const state = useLocation().state as { twoFactorToken?: string } | null;
  return <p>Sign-in page, step token {state?.twoFactorToken}</p>;
}

function land(hash: string) {
  window.history.replaceState(null, '', `/auth/sso${hash}`);
  return render(withQueries(
    <MemoryRouter initialEntries={['/auth/sso']}>
      <Routes>
        <Route path="/auth/sso" element={<SsoCallback />} />
        <Route path="/members" element={<p>Members page</p>} />
        <Route path="/organizations" element={<p>Workspaces page</p>} />
        <Route path="/login" element={<LoginStub />} />
      </Routes>
    </MemoryRouter>,
  ));
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
});

afterEach(() => {
  vi.unstubAllEnvs();
  window.history.replaceState(null, '', '/');
});

describe('landing from single sign-on', () => {
  it('trades the code for the session and goes back where the person started', async () => {
    let sent: { code: string; verifier: string } | undefined;
    server.use(http.post(`${BASE}/api/auth/sso/complete`, async ({ request }) => {
      sent = await request.json() as { code: string; verifier: string };
      return HttpResponse.json({ token: 'sso-session', user: { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true }, requiresTwoFactor: false });
    }));
    await ssoStartUrl('google', '/members');
    const { verifier } = JSON.parse(sessionStorage.getItem('cadence.sso')!) as { verifier: string };

    land('#code=the-code');

    expect(await screen.findByText('Members page')).toBeInTheDocument();
    expect(sent).toEqual({ code: 'the-code', verifier });
    expect(getAuthToken()).toBe('sso-session');
    // The spent code leaves the address.
    expect(window.location.hash).toBe('');
  });

  it('hands the two-factor step to the sign-in page', async () => {
    server.use(http.post(`${BASE}/api/auth/sso/complete`, () =>
      HttpResponse.json({ token: null, user: null, requiresTwoFactor: true, twoFactorToken: 'step-token' })));
    await ssoStartUrl('google');

    land('#code=the-code');

    expect(await screen.findByText('Sign-in page, step token step-token')).toBeInTheDocument();
    expect(getAuthToken()).toBeNull();
  });

  it('says why when the provider would not vouch for the address, and signs nobody in', async () => {
    land('#error=email_not_verified');

    expect(await screen.findByRole('alert')).toHaveTextContent(/did not confirm that this address is yours/);
    expect(screen.getByRole('link', { name: 'Back to sign in' })).toBeInTheDocument();
    expect(getAuthToken()).toBeNull();
  });

  it('refuses a code opened in a tab that did not start the sign-on', async () => {
    const complete = vi.fn(() => HttpResponse.json({}));
    server.use(http.post(`${BASE}/api/auth/sso/complete`, complete));

    land('#code=pasted-from-elsewhere');

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/another browser or tab/));
    expect(complete).not.toHaveBeenCalled();
  });
});

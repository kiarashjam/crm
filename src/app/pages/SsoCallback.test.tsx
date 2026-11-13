// Where single sign-on lands: the one-time code from the address is traded, with this tab's
// secret, for the session; the two-factor step goes on at the sign-in page; a failure says why.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { getAuthToken, getCurrentOrganizationId } from '@/app/lib/auth';
import { ssoStartUrl } from '@/app/api/signIn';
import { I18nProvider, loadCatalogue, useT } from '@/app/i18n';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));

const SsoCallback = (await import('./SsoCallback')).default;

const BASE = 'http://api.test';

function LoginStub() {
  const state = useLocation().state as { twoFactorToken?: string; language?: string | null } | null;
  return (
    <>
      <p>Sign-in page, step token {state?.twoFactorToken}</p>
      <p data-testid="picked">{String(state?.language)}</p>
    </>
  );
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

  it('stays in the workspace it started from when the person who left comes back', async () => {
    // Anna, signed in with her password, opened Pavillon 46, which asks for single sign-on.
    localStorage.setItem('aci_token', 'password-session');
    localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch' }));
    sessionStorage.setItem('aci_org_id', 'pavillon');
    server.use(http.post(`${BASE}/api/auth/sso/complete`, () =>
      HttpResponse.json({ token: 'sso-session', user: { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true }, requiresTwoFactor: false })));
    await ssoStartUrl('google', '/members');

    land('#code=the-code');

    expect(await screen.findByText('Members page')).toBeInTheDocument();
    expect(getAuthToken()).toBe('sso-session');
    expect(getCurrentOrganizationId()).toBe('pavillon');
  });

  it('does not keep the last person’s workspace for someone else signing in at the same desk', async () => {
    localStorage.setItem('aci_token', 'password-session');
    localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch' }));
    sessionStorage.setItem('aci_org_id', 'pavillon');
    server.use(http.post(`${BASE}/api/auth/sso/complete`, () =>
      HttpResponse.json({ token: 'marcs-session', user: { id: 'u2', name: 'Marc', email: 'marc@example.ch', emailVerified: true }, requiresTwoFactor: false })));
    await ssoStartUrl('google');

    land('#code=the-code');

    expect(await screen.findByText('Workspaces page')).toBeInTheDocument();
    expect(getAuthToken()).toBe('marcs-session');
    expect(getCurrentOrganizationId()).toBeNull();
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

// The sign-in page is gone by the time the provider sends the person back, so the language
// picked there travels with this tab's secret. Without it, the account's own (older) language
// won as the session started and switched this browser too.
describe('landing from single sign-on with a language picked on the sign-in page', () => {
  const ANNA = { id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch', emailVerified: true };
  let saved: unknown[];
  let asked: number;

  /** Where single sign-on lands, saying which language the app is in. */
  function InTheApp() {
    const { language } = useT();
    return <p>Members page <span data-testid="language">{language}</span></p>;
  }

  function landInTheApp(hash: string) {
    window.history.replaceState(null, '', `/auth/sso${hash}`);
    return render(withQueries(
      <MemoryRouter initialEntries={['/auth/sso']}>
        <I18nProvider>
          <Routes>
            <Route path="/auth/sso" element={<SsoCallback />} />
            <Route path="/members" element={<InTheApp />} />
          </Routes>
        </I18nProvider>
      </MemoryRouter>,
    ));
  }

  async function settledLanguage() {
    await waitFor(() => expect(asked).toBeGreaterThan(0));
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    return screen.getByTestId('language').textContent;
  }

  beforeEach(async () => {
    saved = [];
    asked = 0;
    await loadCatalogue('de');
    await loadCatalogue('fr');
    server.use(
      http.post(`${BASE}/api/auth/sso/complete`, () => HttpResponse.json({ token: 'sso-session', user: ANNA, requiresTwoFactor: false })),
      // The account says French.
      http.get(`${BASE}/api/settings/language`, () => {
        asked += 1;
        return HttpResponse.json({ language: 'fr' });
      }),
      http.put(`${BASE}/api/settings/language`, async ({ request }) => {
        const body = await request.json();
        saved.push(body);
        return HttpResponse.json(body);
      }),
    );
  });

  it('keeps it on the account, over the account’s older one', async () => {
    // Picked on the sign-in page while signed out: applied there and kept in this browser.
    localStorage.setItem('cadence.language', 'de');
    await ssoStartUrl('google', '/members', 'de');

    landInTheApp('#code=the-code');

    expect(await screen.findByText('Members page', { exact: false })).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'de' }]));
    expect(await settledLanguage()).toBe('de');
    expect(localStorage.getItem('cadence.language')).toBe('de');
  });

  it('leaves the account’s own language alone when none was picked', async () => {
    await ssoStartUrl('google', '/members');

    landInTheApp('#code=the-code');

    expect(await screen.findByText('Members page', { exact: false })).toBeInTheDocument();
    expect(await settledLanguage()).toBe('fr');
    expect(saved).toEqual([]);
  });

  it('hands it to the sign-in page with the two-factor step', async () => {
    server.use(http.post(`${BASE}/api/auth/sso/complete`, () =>
      HttpResponse.json({ token: null, user: null, requiresTwoFactor: true, twoFactorToken: 'step-token' })));
    await ssoStartUrl('google', null, 'fr');

    land('#code=the-code');

    expect(await screen.findByText('Sign-in page, step token step-token')).toBeInTheDocument();
    expect(screen.getByTestId('picked')).toHaveTextContent('fr');
  });
});

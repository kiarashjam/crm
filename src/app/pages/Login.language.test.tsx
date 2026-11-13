// The language switch on the sign-in page: the page changes language at once, the choice is
// kept in this browser, and once the person signs in it goes onto their account, so the app
// opens in it in every browser they use. A language they did not touch leaves the account's.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { I18nProvider, loadCatalogue, useT } from '@/app/i18n';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));
// Leaving for Google or Microsoft is a page load the test cannot follow: what it hands over is checked.
const startSso = vi.hoisted(() => vi.fn());
vi.mock('@/app/api/signIn', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/signIn')>()), startSso }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';
let saved: unknown[];

/** The form's own submit button comes after the mode toggle of the same name. */
const last = (buttons: HTMLElement[]) => buttons[buttons.length - 1]!;

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  startSso.mockReset();
  saved = [];
  await loadCatalogue('de');
  await loadCatalogue('fr');
  server.use(
    http.post(`${BASE}/api/auth/login`, () => HttpResponse.json({
      token: 'session-token',
      user: { id: 'user-1', name: 'Nora Keller', email: 'nora@example.ch', emailVerified: true },
    })),
    http.get(`${BASE}/api/settings/language`, () => HttpResponse.json({ language: null })),
    http.put(`${BASE}/api/settings/language`, async ({ request }) => {
      const body = await request.json();
      saved.push(body);
      return HttpResponse.json(body);
    }),
  );
});

/** Where sign-in lands, saying which language the app is in. */
function SignedIn() {
  const { language } = useT();
  return <p>Signed in <span data-testid="language">{language}</span></p>;
}

function renderLogin(state?: unknown) {
  return render(withQueries(
    <MemoryRouter initialEntries={[{ pathname: '/login', state }]}>
      <I18nProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/organizations" element={<SignedIn />} />
          <Route path="/dashboard" element={<SignedIn />} />
        </Routes>
      </I18nProvider>
    </MemoryRouter>,
  ));
}

describe('the sign-in page in another language', () => {
  it('switches the page at once and keeps the choice on the account after signing in', async () => {
    const user = userEvent.setup();
    renderLogin();
    expect(screen.getByRole('heading', { name: 'Welcome back' })).toBeInTheDocument();

    await user.selectOptions(screen.getByLabelText('Language'), 'de');
    expect(screen.getByRole('heading', { name: 'Willkommen zurück' })).toBeInTheDocument();
    expect(screen.getByLabelText('Passwort')).toBeInTheDocument();
    expect(localStorage.getItem('cadence.language')).toBe('de');

    await user.type(screen.getByLabelText('E-Mail'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Passwort'), 'correct horse');
    await user.click(last(screen.getAllByRole('button', { name: /Anmelden/ })));

    expect(await screen.findByText('Signed in')).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'de' }]));
  });

  it('keeps the language picked here over the older one the account answers with as the session starts', async () => {
    // The account says French; the person picks German on this page and signs in. The app
    // asks the account for its language as the session starts, before the new choice is
    // saved, and that older answer used to switch the app (and this browser) to French.
    let asked = 0;
    server.use(http.get(`${BASE}/api/settings/language`, () => {
      asked += 1;
      return HttpResponse.json({ language: 'fr' });
    }));
    const user = userEvent.setup();
    renderLogin();
    await user.selectOptions(screen.getByLabelText('Language'), 'de');
    await user.type(screen.getByLabelText('E-Mail'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Passwort'), 'correct horse');
    await user.click(last(screen.getAllByRole('button', { name: /Anmelden/ })));

    expect(await screen.findByText('Signed in', { exact: false })).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'de' }]));
    await waitFor(() => expect(asked).toBeGreaterThan(0));
    // Let the account's answer arrive and be read.
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    expect(screen.getByTestId('language')).toHaveTextContent('de');
    expect(localStorage.getItem('cadence.language')).toBe('de');
  });

  it('leaves the account alone when nobody touched the language', async () => {
    const user = userEvent.setup();
    renderLogin();
    await user.type(screen.getByLabelText('Email'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Password'), 'correct horse');
    await user.click(last(screen.getAllByRole('button', { name: /Sign in/ })));

    expect(await screen.findByText('Signed in')).toBeInTheDocument();
    expect(saved).toEqual([]);
  });

  it('explains a failed sign-in in the language picked', async () => {
    server.use(http.post(`${BASE}/api/auth/login`, () => HttpResponse.json({ title: 'Invalid email or password' }, { status: 401 })));
    const user = userEvent.setup();
    renderLogin();
    await user.selectOptions(screen.getByLabelText('Language'), 'fr');
    await user.type(screen.getByLabelText('E-mail'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Mot de passe'), 'wrong');
    await user.click(last(screen.getAllByRole('button', { name: /Se connecter/ })));
    expect(await screen.findByText('Ces informations ne correspondent pas')).toBeInTheDocument();
  });
});

// Single sign-on and passkeys came with their own ways in, which started the session without
// the language picked here: the account's own, older language then switched the app (and
// this browser) back, as it once did after a password sign-in.
describe('the language picked here, whichever way in the person takes', () => {
  const NORA = { id: 'user-1', name: 'Nora Keller', email: 'nora@example.ch', emailVerified: true };
  const bytes = (...values: number[]) => new Uint8Array(values).buffer;

  /** This browser holds a passkey for the site, as the sign-in tests of passkeys set it up. */
  function offerPasskeys() {
    const assertion = {
      id: 'cred-1', rawId: bytes(1, 2), type: 'public-key',
      response: { clientDataJSON: bytes(3), authenticatorData: bytes(4), signature: bytes(5), userHandle: bytes(6) },
    };
    vi.stubGlobal('PublicKeyCredential', function PublicKeyCredential() {});
    Object.defineProperty(navigator, 'credentials', { configurable: true, value: { get: vi.fn(async () => assertion), create: vi.fn() } });
  }

  /** The account says French, and counts how often it is asked. */
  function accountSaysFrench() {
    const asked = { count: 0 };
    server.use(http.get(`${BASE}/api/settings/language`, () => {
      asked.count += 1;
      return HttpResponse.json({ language: 'fr' });
    }));
    return asked;
  }

  /** Lets the account's answer arrive and be read, then says which language the app is in. */
  async function settledLanguage(asked: { count: number }) {
    await waitFor(() => expect(asked.count).toBeGreaterThan(0));
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    return screen.getByTestId('language').textContent;
  }

  afterEach(async () => {
    // The code boxes keep timers running for 50 ms after each digit (see Login.twoFactor.test).
    cleanup();
    vi.unstubAllGlobals();
    Reflect.deleteProperty(navigator, 'credentials');
    await new Promise((resolve) => setTimeout(resolve, 100));
  });

  it('keeps it after signing in with a passkey', async () => {
    const asked = accountSaysFrench();
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: true })),
      http.post(`${BASE}/api/auth/passkeys/sign-in/options`, () =>
        HttpResponse.json({ ceremonyId: 'ceremony-1', options: { challenge: 'CQk', allowCredentials: [], userVerification: 'required' } })),
      http.post(`${BASE}/api/auth/passkeys/sign-in`, () => HttpResponse.json({ token: 'passkey-session', user: NORA, requiresTwoFactor: false })),
    );
    offerPasskeys();
    const user = userEvent.setup();
    renderLogin();

    await user.selectOptions(screen.getByLabelText('Language'), 'de');
    await user.click(await screen.findByRole('button', { name: /passkey/i }));

    expect(await screen.findByText('Signed in', { exact: false })).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'de' }]));
    expect(await settledLanguage(asked)).toBe('de');
    expect(localStorage.getItem('cadence.language')).toBe('de');
  });

  it('hands it to single sign-on, which carries it through the provider', async () => {
    server.use(http.get(`${BASE}/api/auth/sign-in-options`, () =>
      HttpResponse.json({ sso: [{ id: 'google', name: 'Google' }], passkeys: false })));
    const user = userEvent.setup();
    renderLogin();

    await user.selectOptions(screen.getByLabelText('Language'), 'de');
    await user.click(await screen.findByRole('button', { name: /Google/ }));

    expect(startSso).toHaveBeenCalledWith('google', null, 'de');
  });

  it('keeps one picked before single sign-on once the two-factor code is in', async () => {
    // Picked here and kept in this browser; then off to the provider, which sends the person
    // back here for the code because the account has two-factor sign-in on.
    localStorage.setItem('cadence.language', 'de');
    const asked = accountSaysFrench();
    server.use(http.post(`${BASE}/api/auth/login/2fa`, () => HttpResponse.json({ token: 'sso-session', user: NORA, requiresTwoFactor: false })));
    const user = userEvent.setup();
    renderLogin({ twoFactorToken: 'step-from-sso', language: 'de' });

    expect(screen.getByRole('heading', { name: 'Zwei-Faktor-Bestätigung' })).toBeInTheDocument();
    await user.type(document.querySelector<HTMLInputElement>('input[autocomplete="one-time-code"]')!, '123456');
    await user.click(screen.getByRole('button', { name: 'Bestätigen und anmelden' }));

    expect(await screen.findByText('Signed in', { exact: false })).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'de' }]));
    expect(await settledLanguage(asked)).toBe('de');
    expect(localStorage.getItem('cadence.language')).toBe('de');
  });
});

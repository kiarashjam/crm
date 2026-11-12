// The language switch on the sign-in page: the page changes language at once, the choice is
// kept in this browser, and once the person signs in it goes onto their account, so the app
// opens in it in every browser they use. A language they did not touch leaves the account's.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { I18nProvider, loadCatalogue } from '@/app/i18n';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';
let saved: unknown[];

/** The form's own submit button comes after the mode toggle of the same name. */
const last = (buttons: HTMLElement[]) => buttons[buttons.length - 1]!;

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
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

function renderLogin() {
  return render(withQueries(
    <MemoryRouter initialEntries={['/login']}>
      <I18nProvider>
        <Routes>
          <Route path="/login" element={<Login />} />
          <Route path="/organizations" element={<p>Signed in</p>} />
          <Route path="/dashboard" element={<p>Signed in</p>} />
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

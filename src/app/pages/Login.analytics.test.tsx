// What the sign-in page tells the site analytics: someone opened the sign-up form, or made an
// account. Names of events only; the analytics never hear who. A sign-in is not counted: it is
// not the website's growth, and its moment would match the account's last sign-in.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));
const trackEvent = vi.fn();
vi.mock('@/app/lib/siteAnalytics', () => ({ trackEvent: (...args: unknown[]) => trackEvent(...args) }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';
const session = {
  token: 'session-token',
  user: { id: 'user-1', name: 'Nora Keller', email: 'nora@example.ch', emailVerified: false },
  requiresTwoFactor: false,
};

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  trackEvent.mockClear();
  vi.stubEnv('VITE_API_URL', BASE);
  server.use(
    http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: false })),
    http.post(`${BASE}/api/auth/register`, () => HttpResponse.json(session)),
    http.post(`${BASE}/api/auth/login`, () => HttpResponse.json(session)),
  );
});

const show = () => render(withQueries(
  <MemoryRouter initialEntries={['/login']}>
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/organizations" element={<p>Signed in</p>} />
      <Route path="/dashboard" element={<p>Signed in</p>} />
    </Routes>
  </MemoryRouter>,
));

describe('the sign-in page and the site analytics', () => {
  it('counts the sign-up form opened once, then the account made', async () => {
    const user = userEvent.setup();
    show();
    expect(trackEvent).not.toHaveBeenCalled();

    await user.click(screen.getByRole('tab', { name: 'Create account' }));
    await user.click(screen.getByRole('tab', { name: 'Sign in' }));
    await user.click(screen.getByRole('tab', { name: 'Create account' }));
    expect(trackEvent.mock.calls).toEqual([['signup_started']]);

    await user.type(screen.getByLabelText('Name'), 'Nora Keller');
    await user.type(screen.getByLabelText('Email'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText('Signed in')).toBeInTheDocument();
    expect(trackEvent.mock.calls).toEqual([['signup_started'], ['signup_completed']]);
  });

  it('does not count a sign-in', async () => {
    const user = userEvent.setup();
    show();

    await user.type(screen.getByLabelText('Email'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Password'), 'correct horse battery');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText('Signed in')).toBeInTheDocument();
    expect(trackEvent).not.toHaveBeenCalled();
  });
});

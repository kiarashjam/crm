// The sign-in page and the public site's two ways in: "Start free" (the sign-up form, with the
// launch offer on it while it runs) and "Try the demo" (?demo=1: straight into the sample
// workspace, unless a real account is already signed in).

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { getCurrentUser, setSession } from '@/app/lib/auth';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));
vi.mock('@/app/lib/siteAnalytics', () => ({ trackEvent: vi.fn() }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  server.use(http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: false })));
});

afterEach(() => {
  vi.useRealTimers();
});

const show = (entry: string) => render(withQueries(
  <MemoryRouter initialEntries={[entry]}>
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/organizations" element={<p>Workspaces</p>} />
    </Routes>
  </MemoryRouter>,
));

describe('the sign-up form and the launch offer', () => {
  it('says it is free, with no card, while the offer runs', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-26T10:00:00Z'));
    show('/login?mode=register');
    expect(screen.getByText('Free until 31 December 2026')).toBeInTheDocument();
    expect(screen.getByText(/No credit card required/)).toBeInTheDocument();
  });

  it('says nothing of it once the offer has ended, nor on the sign-in form', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2027-01-01T00:00:00+01:00'));
    show('/login?mode=register');
    expect(screen.queryByText('Free until 31 December 2026')).not.toBeInTheDocument();
  });

  it('is not on the sign-in form', () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date('2026-11-26T10:00:00Z'));
    show('/login');
    expect(screen.queryByText('Free until 31 December 2026')).not.toBeInTheDocument();
  });
});

describe('"Try the demo" from the public site', () => {
  it('opens the sample workspace without a form', async () => {
    show('/login?demo=1');
    expect(await screen.findByText('Workspaces')).toBeInTheDocument();
    expect(getCurrentUser()?.id).toBe('demo');
  });

  it('never replaces a real account that is signed in', async () => {
    setSession('real-token', { id: 'user-1', name: 'Nora Keller', email: 'nora@example.ch' });
    show('/login?demo=1');
    expect(await screen.findByText('Workspaces')).toBeInTheDocument();
    expect(getCurrentUser()?.id).toBe('user-1');
  });
});

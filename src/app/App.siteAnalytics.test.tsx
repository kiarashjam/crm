// Where /admin/analytics sits in the app's routes: behind sign-in, but outside the wrapper
// that sends someone without a workspace to /organizations. The site's analytics are the
// platform's, and a platform admin may have no workspace of their own.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';

// The pages are not under test here, only which one the address reaches.
vi.mock('@/app/pages/admin/analytics/SiteAnalyticsPage', () => ({ default: () => <h1>Site analytics page</h1> }));
vi.mock('@/app/pages/Organizations', () => ({ default: () => <h1>Organizations page</h1> }));
vi.mock('@/app/pages/Dashboard', () => ({ default: () => <h1>Dashboard page</h1> }));
vi.mock('@/app/pages/Login', () => ({ default: () => <h1>Login page</h1> }));
vi.mock('@/app/components/PageHelp', () => ({ default: () => null }));

const { default: App } = await import('./App');

const BASE = 'http://api.test';
let workspacesRead: number;

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  workspacesRead = 0;
  server.use(
    // Signed in, with no workspace at all.
    http.get(`${BASE}/api/organizations`, () => {
      workspacesRead++;
      return HttpResponse.json([]);
    }),
    http.get(`${BASE}/api/invites/pending`, () => HttpResponse.json([])),
    http.get(`${BASE}/api/settings/language`, () => HttpResponse.json({})),
  );
});

afterEach(() => {
  vi.unstubAllEnvs();
  window.history.pushState({}, '', '/');
});

const signIn = () => {
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-admin', name: 'Kia', email: 'kia@cadencee.ai' }));
  localStorage.setItem('aci_token', 'token');
};

const open = (path: string) => {
  window.history.pushState({}, '', path);
  return render(<App />);
};

describe('/admin/analytics', () => {
  it('opens for someone signed in who has no workspace', async () => {
    signIn();
    open('/admin/analytics');

    expect(await screen.findByRole('heading', { name: 'Site analytics page' })).toBeInTheDocument();
    // Still there once the workspaces have been read and found to be none.
    await waitFor(() => expect(workspacesRead).toBeGreaterThan(0));
    await new Promise((r) => setTimeout(r, 100));
    expect(window.location.pathname).toBe('/admin/analytics');
    expect(screen.getByRole('heading', { name: 'Site analytics page' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Organizations page' })).not.toBeInTheDocument();
  });

  it('is not where a workspace page is: those still send someone without one to /organizations', async () => {
    signIn();
    open('/dashboard');

    expect(await screen.findByRole('heading', { name: 'Organizations page' })).toBeInTheDocument();
    await waitFor(() => expect(window.location.pathname).toBe('/organizations'));
  });

  it('asks a visitor who is not signed in to sign in first', async () => {
    open('/admin/analytics');

    expect(await screen.findByRole('heading', { name: 'Login page' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Site analytics page' })).not.toBeInTheDocument();
  });
});

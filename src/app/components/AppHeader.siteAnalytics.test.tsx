// The account menu's "Site analytics": there for CadenceE's platform admins only, as the
// server says (GET /api/platform/me), and never asked for in demo mode.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';

// The header's other parts read their own data and have their own tests.
vi.mock('@/app/components/NotificationBell', () => ({ default: () => null }));
vi.mock('@/app/components/DemoBanner', () => ({ default: () => null }));
vi.mock('@/app/components/ReadOnlyBanner', () => ({ default: () => null }));
vi.mock('@/app/components/EmailVerificationBanner', () => ({ default: () => null }));
vi.mock('@/app/components/LocalOnlyNotice', () => ({ default: () => null }));
vi.mock('@/app/components/LocalDataUploadDialog', () => ({ default: () => null }));
vi.mock('@/app/components/AppSideNav', () => ({ AppSideNav: () => null }));
vi.mock('@/app/salesAgent/NextActions', () => ({ NextActions: () => null }));

const { default: AppHeader } = await import('./AppHeader');

const BASE = 'http://api.test';
let me: () => Response;
let meReads: number;

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Kia Admin', email: 'kia@cadencee.ai' }));
  localStorage.setItem('aci_token', 'token');
  meReads = 0;
  me = () => HttpResponse.json({ isPlatformAdmin: true });
  server.use(http.get(`${BASE}/api/platform/me`, () => {
    meReads++;
    return me();
  }));
});

afterEach(() => vi.unstubAllEnvs());

const show = () => render(withQueries(<MemoryRouter><AppHeader /></MemoryRouter>));

async function openMenu() {
  await userEvent.setup().click(screen.getByRole('button', { name: /Kia Admin/ }));
  await screen.findByRole('menuitem', { name: 'Settings' });
}

describe('the account menu', () => {
  it('offers Site analytics to a platform admin', async () => {
    vi.stubEnv('VITE_API_URL', BASE);
    show();
    await waitFor(() => expect(meReads).toBe(1));
    await openMenu();

    const item = await screen.findByRole('menuitem', { name: 'Site analytics' });
    expect(item).toHaveAttribute('href', '/admin/analytics');
  });

  it('does not offer it to anyone else', async () => {
    vi.stubEnv('VITE_API_URL', BASE);
    me = () => HttpResponse.json({ isPlatformAdmin: false });
    show();
    await waitFor(() => expect(meReads).toBe(1));
    await openMenu();

    expect(screen.queryByRole('menuitem', { name: 'Site analytics' })).not.toBeInTheDocument();
  });

  it('does not offer it when the server refuses to say', async () => {
    vi.stubEnv('VITE_API_URL', BASE);
    me = () => HttpResponse.json({ title: 'Forbidden' }, { status: 403 });
    show();
    await waitFor(() => expect(meReads).toBe(1));
    await openMenu();

    expect(screen.queryByRole('menuitem', { name: 'Site analytics' })).not.toBeInTheDocument();
  });

  it('never asks in demo mode, where nobody is a platform admin', async () => {
    show();
    await openMenu();

    expect(screen.queryByRole('menuitem', { name: 'Site analytics' })).not.toBeInTheDocument();
    expect(meReads).toBe(0);
  });
});

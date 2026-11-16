// Settings > Security: the Google and Microsoft accounts the person signs in with. Each can
// be unlinked, except the last way into an account without a password or passkey, which the
// server refuses with a sentence the card shows.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { SsoLinksCard } = await import('./SsoLinksCard');

const BASE = 'http://api.test';
const google = { id: 'link-1', provider: 'google', email: 'anna@pavillon46.ch', createdAtUtc: '2026-10-01T09:00:00Z', lastUsedAtUtc: null };

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Anna', email: 'anna@pavillon46.ch' }));
  toast.success.mockReset();
  toast.error.mockReset();
  server.use(http.get(`${BASE}/api/auth/sign-in-options`, () =>
    HttpResponse.json({ sso: [{ id: 'google', name: 'Google' }, { id: 'microsoft', name: 'Microsoft' }], passkeys: true })));
});

afterEach(() => vi.unstubAllEnvs());

describe('the Google and Microsoft sign-in card', () => {
  it('lists the linked accounts and unlinks one', async () => {
    let linked = [google];
    server.use(
      http.get(`${BASE}/api/auth/sso/links`, () => HttpResponse.json(linked)),
      http.delete(`${BASE}/api/auth/sso/links/:id`, () => { linked = []; return new HttpResponse(null, { status: 204 }); }),
    );
    const user = userEvent.setup();
    render(withQueries(<SsoLinksCard />));

    expect(await screen.findByText('anna@pavillon46.ch')).toBeInTheDocument();
    expect(screen.getByText('Google')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Unlink' }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Google no longer signs you in.'));
    expect(await screen.findByText(/None yet\. To add one, sign out and choose “Continue with Google” or “Continue with Microsoft”/)).toBeInTheDocument();
  });

  it('says why the last way in cannot be unlinked, and keeps it', async () => {
    server.use(
      http.get(`${BASE}/api/auth/sso/links`, () => HttpResponse.json([google])),
      http.delete(`${BASE}/api/auth/sso/links/:id`, () => HttpResponse.json(
        { detail: 'This is your only way to sign in. Set a password or add a passkey first.', errorCode: 'Sso.LastWayIn' },
        { status: 400 },
      )),
    );
    const user = userEvent.setup();
    render(withQueries(<SsoLinksCard />));

    await user.click(await screen.findByRole('button', { name: 'Unlink' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('This is your only way to sign in. Set a password or add a passkey first.'));
    expect(screen.getByText('anna@pavillon46.ch')).toBeInTheDocument();
  });

  it('is not shown where the server offers no single sign-on and nothing was ever linked', async () => {
    let asked = false;
    server.use(
      http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: false })),
      http.get(`${BASE}/api/auth/sso/links`, () => { asked = true; return HttpResponse.json([]); }),
    );
    const { container } = render(withQueries(<SsoLinksCard />));

    await waitFor(() => expect(asked).toBe(true));
    await waitFor(() => expect(container).toBeEmptyDOMElement());
  });
});

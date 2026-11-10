// The two-factor step of signing in, against the server as it now answers.
//
// The server used to refuse the step token login hands out (about 600 characters,
// against a limit of 500), so nobody with 2FA on could finish signing in and this half
// of the page never ran against a real server. Now that it does, it has to start the
// session the way a password sign-in does: the workspace this browser last used is
// forgotten and every cached read is dropped, or the account signing in would open
// on the last one's workspace and be shown the last one's records from the cache.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { testQueryClient, withQueries } from '@/test/queryWrapper';
import { queryKeys } from '@/app/hooks/queries/queryKeys';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), message: vi.fn() } }));

const Login = (await import('./Login')).default;

const BASE = 'http://api.test';
// As long as the token the server hands out: its claims carry their full type names.
const STEP_TOKEN = `eyJ.${'x'.repeat(593)}.sig`;
const BEFORE = { orgId: 'org-before', userId: 'user-before' };

let sentToStep: unknown;

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  sentToStep = undefined;
  // What the last session left in this browser and this tab.
  localStorage.setItem('aci_org_id', BEFORE.orgId);
  sessionStorage.setItem('aci_org_id', BEFORE.orgId);
  server.use(
    http.post(`${BASE}/api/auth/login`, () =>
      HttpResponse.json({ token: null, user: null, requiresTwoFactor: true, twoFactorToken: STEP_TOKEN })),
    http.post(`${BASE}/api/auth/login/2fa`, async ({ request }) => {
      sentToStep = await request.json();
      return HttpResponse.json({
        token: 'session-token',
        user: { id: 'user-after', name: 'Nora Keller', email: 'nora@example.ch', emailVerified: true },
        requiresTwoFactor: false,
      });
    }),
  );
});

describe('signing in with 2FA on', () => {
  it('sends the step token whole, then starts the session as a password sign-in does', async () => {
    const client = testQueryClient();
    client.setQueryData(queryKeys.leads.all(BEFORE), [{ id: 'lead-1', name: 'Ada Lovelace' }]);
    const user = userEvent.setup();
    render(withQueries(
      <MemoryRouter initialEntries={['/login']}>
        <Routes>
          <Route path="/login" element={<Login />} />
          {/* Where it lands is the app's choice; that it leaves the sign-in page is what counts. */}
          <Route path="/dashboard" element={<p>Signed in</p>} />
          <Route path="/organizations" element={<p>Signed in</p>} />
        </Routes>
      </MemoryRouter>,
      client,
    ));

    await user.type(screen.getByLabelText('Email'), 'nora@example.ch');
    await user.type(screen.getByLabelText('Password'), 'correct horse');
    await user.click(screen.getByRole('button', { name: /sign in/i }));

    expect(await screen.findByText('Two-factor verification')).toBeInTheDocument();
    // Nothing is signed in until the code is checked.
    expect(localStorage.getItem('aci_token')).toBeNull();

    await user.type(document.querySelector<HTMLInputElement>('input[autocomplete="one-time-code"]')!, '123456');
    await user.click(screen.getByRole('button', { name: /verify & sign in/i }));

    expect(await screen.findByText('Signed in')).toBeInTheDocument();
    expect(sentToStep).toEqual({ twoFactorToken: STEP_TOKEN, code: '123456' });
    expect(STEP_TOKEN.length).toBeGreaterThan(500);
    expect(localStorage.getItem('aci_token')).toBe('session-token');
    expect(JSON.parse(localStorage.getItem('aci_user')!)).toMatchObject({ id: 'user-after' });
    // The last session's workspace is forgotten, in this tab and for new tabs ...
    expect(sessionStorage.getItem('aci_org_id')).toBeNull();
    expect(localStorage.getItem('aci_org_id')).toBeNull();
    // ... and so are its cached records.
    await waitFor(() => expect(client.getQueryData(queryKeys.leads.all(BEFORE))).toBeUndefined());
  });
});

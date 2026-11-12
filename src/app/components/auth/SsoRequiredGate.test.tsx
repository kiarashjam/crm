// A workspace that requires single sign-on refuses every request from a session that began
// with a password or a passkey. In its place the app says why and offers the providers; and
// when the owner turns the requirement on while the workspace is open, the first refusal
// brings that up instead of a page of failed requests.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { authFetchJson } from '@/app/api/apiClient';
import type { Organization } from '@/app/api/organizations';

const startSso = vi.hoisted(() => vi.fn(async () => {}));
vi.mock('@/app/api/signIn', async (importOriginal) => ({ ...(await importOriginal<typeof import('@/app/api/signIn')>()), startSso }));

const { OrgProvider } = await import('@/app/contexts/OrgContext');
const { WorkspaceOutlet } = await import('@/app/components/WorkspaceOutlet');

const BASE = 'http://api.test';

const pavillon = (over: Partial<Organization> = {}): Organization => ({
  id: 'pavillon', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, ssoRequired: false, ssoSignInNeeded: false, ...over,
});

/** A page of the workspace: it reads the contacts when asked. */
function ContactsPage() {
  return (
    <button type="button" onClick={() => void authFetchJson('/api/contacts').catch(() => undefined)}>
      Load contacts
    </button>
  );
}

function app() {
  return withQueries(
    <MemoryRouter initialEntries={['/contacts']}>
      <OrgProvider>
        <Routes>
          <Route element={<WorkspaceOutlet />}>
            <Route path="/contacts" element={<ContactsPage />} />
          </Route>
          <Route path="/organizations" element={<p>Workspaces page</p>} />
        </Routes>
      </OrgProvider>
    </MemoryRouter>,
  );
}

let workspaceReads = 0;

function serveWorkspaces(...lists: Organization[][]) {
  workspaceReads = 0;
  server.use(
    http.get(`${BASE}/api/organizations`, () => {
      const list = lists[Math.min(workspaceReads, lists.length - 1)]!;
      workspaceReads += 1;
      return HttpResponse.json(list);
    }),
    http.get(`${BASE}/api/invites/pending`, () => HttpResponse.json([])),
  );
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'password-session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-anna', name: 'Anna', email: 'anna@pavillon46.ch' }));
  localStorage.setItem('aci_org_id', 'pavillon');
  startSso.mockClear();
  server.use(http.get(`${BASE}/api/auth/sign-in-options`, () =>
    HttpResponse.json({ sso: [{ id: 'google', name: 'Google' }, { id: 'microsoft', name: 'Microsoft' }], passkeys: true })));
});

afterEach(() => vi.unstubAllEnvs());

describe('a workspace that asks for single sign-on', () => {
  it('says why in place of its pages, offers the providers, and comes back to the same page', async () => {
    serveWorkspaces([pavillon({ ssoRequired: true, ssoSignInNeeded: true })]);
    const user = userEvent.setup();
    render(app());

    expect(await screen.findByRole('heading', { name: 'Pavillon 46 asks you to sign in with single sign-on' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Load contacts' })).not.toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Continue with Microsoft' }));
    expect(startSso).toHaveBeenCalledWith('microsoft', '/contacts');
    expect(screen.getByRole('link', { name: 'Choose another workspace' })).toHaveAttribute('href', '/organizations');
  });

  it('says so when the server offers no provider, rather than a button that cannot work', async () => {
    serveWorkspaces([pavillon({ ssoRequired: true, ssoSignInNeeded: true })]);
    server.use(http.get(`${BASE}/api/auth/sign-in-options`, () => HttpResponse.json({ sso: [], passkeys: false })));
    render(app());

    expect(await screen.findByRole('status')).toHaveTextContent(/offers no single sign-on at the moment/);
    expect(screen.queryByRole('button', { name: /Continue with/ })).not.toBeInTheDocument();
  });

  it('turned on while the workspace is open, shows up at the first refusal, reading the workspaces once', async () => {
    // The owner turns the requirement on after this tab read the workspaces.
    serveWorkspaces([pavillon()], [pavillon({ ssoRequired: true, ssoSignInNeeded: true })]);
    server.use(http.get(`${BASE}/api/contacts`, () => HttpResponse.json(
      { title: 'Single sign-on required', detail: 'Pavillon 46 asks you to sign in with Google or Microsoft to work here.', errorCode: 'Sso.Required', organizationId: 'pavillon' },
      { status: 403 },
    )));
    const user = userEvent.setup();
    render(app());

    const load = await screen.findByRole('button', { name: 'Load contacts' });
    expect(workspaceReads).toBe(1);
    // Two pages' worth of requests refused at once read the workspaces once.
    await user.click(load);
    await user.click(load);

    expect(await screen.findByRole('heading', { name: 'Pavillon 46 asks you to sign in with single sign-on' })).toBeInTheDocument();
    await waitFor(() => expect(workspaceReads).toBe(2));
  });

  it('leaves a workspace that admits the session alone, whatever else is refused', async () => {
    serveWorkspaces([pavillon()]);
    server.use(http.get(`${BASE}/api/contacts`, () =>
      HttpResponse.json({ detail: 'You need permission to see contacts.', errorCode: 'Permission.Required' }, { status: 403 })));
    const user = userEvent.setup();
    render(app());

    await user.click(await screen.findByRole('button', { name: 'Load contacts' }));
    await new Promise((resolve) => setTimeout(resolve, 50));
    expect(workspaceReads).toBe(1);
    expect(screen.getByRole('button', { name: 'Load contacts' })).toBeInTheDocument();
  });
});

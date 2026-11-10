// The workspace provider keeps each browser tab in its own workspace.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';

vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  listMyOrganizations: vi.fn(async () => [
    { id: 'pavillon', name: 'Pavillon 46', ownerUserId: 'u1', isOwner: true, role: 0 },
    { id: 'lac-leman', name: 'Lac Léman SA', ownerUserId: 'u1', isOwner: true, role: 0 },
  ]),
  listMyPendingInvites: vi.fn(async () => []),
}));

import { OrgProvider, useOrg } from './OrgContext';
import { AUTH_SESSION_CHANGED, getCurrentOrganizationId } from '@/app/lib/auth';

function CurrentWorkspace() {
  const { currentOrg } = useOrg();
  return <p>{currentOrg?.name ?? 'none'}</p>;
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', 'http://api.test');
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Owner', email: 'owner@example.ch' }));
});

afterEach(() => vi.unstubAllEnvs());

describe('a tab with two workspaces open in the browser', () => {
  it('keeps the workspace it opened in when another tab switches', async () => {
    // The tab opens in the workspace last picked.
    localStorage.setItem('aci_org_id', 'pavillon');
    render(<OrgProvider><CurrentWorkspace /></OrgProvider>);
    expect(await screen.findByText('Pavillon 46')).toBeInTheDocument();

    // Another tab picks Lac Léman, and something makes this tab read the workspaces again.
    localStorage.setItem('aci_org_id', 'lac-leman');
    await act(async () => { window.dispatchEvent(new Event(AUTH_SESSION_CHANGED)); });

    await waitFor(() => expect(screen.getByText('Pavillon 46')).toBeInTheDocument());
    expect(getCurrentOrganizationId()).toBe('pavillon');
  });

  it('keeps the workspace it takes on a sign-in in another tab, whatever that tab picks next', async () => {
    // This tab was opened signed out, so it had no workspace of its own. Another tab
    // signs in, and this one takes the workspace that tab picked. It must keep it: a
    // tab still reading the shared copy sent its next request to whichever workspace
    // the other tab switched to, under this tab's header.
    localStorage.removeItem('aci_token');
    render(<OrgProvider><CurrentWorkspace /></OrgProvider>);
    expect(await screen.findByText('none')).toBeInTheDocument();

    localStorage.setItem('aci_token', 'token');
    localStorage.setItem('aci_org_id', 'lac-leman');
    await act(async () => { window.dispatchEvent(new Event(AUTH_SESSION_CHANGED)); });
    expect(await screen.findByText('Lac Léman SA')).toBeInTheDocument();

    // The other tab switches to Pavillon 46.
    localStorage.setItem('aci_org_id', 'pavillon');

    expect(getCurrentOrganizationId()).toBe('lac-leman');
  });

  it('opens a new tab in its own workspace once it has the focus again', async () => {
    localStorage.setItem('aci_org_id', 'pavillon');
    render(<OrgProvider><CurrentWorkspace /></OrgProvider>);
    await screen.findByText('Pavillon 46');

    localStorage.setItem('aci_org_id', 'lac-leman');
    act(() => { window.dispatchEvent(new Event('focus')); });

    expect(localStorage.getItem('aci_org_id')).toBe('pavillon');
  });
});

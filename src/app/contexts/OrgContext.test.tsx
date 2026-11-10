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

  it('opens a new tab in its own workspace once it has the focus again', async () => {
    localStorage.setItem('aci_org_id', 'pavillon');
    render(<OrgProvider><CurrentWorkspace /></OrgProvider>);
    await screen.findByText('Pavillon 46');

    localStorage.setItem('aci_org_id', 'lac-leman');
    act(() => { window.dispatchEvent(new Event('focus')); });

    expect(localStorage.getItem('aci_org_id')).toBe('pavillon');
  });
});

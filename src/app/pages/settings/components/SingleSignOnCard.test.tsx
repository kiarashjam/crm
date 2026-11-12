// Settings > Team: the workspace's single sign-on requirement. The owner turns it on and
// lists the workspace's email domains; managers see it but cannot change it; a server that
// offers no provider cannot have it turned on.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import type { Organization } from '@/app/api/organizations';
import type { OrganizationSso } from '@/app/api/signIn';

const state = vi.hoisted(() => ({ org: null as Organization | null, refreshOrgs: vi.fn(async () => {}) }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: state.org, currentOrgId: state.org?.id ?? null, refreshOrgs: state.refreshOrgs }),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { SingleSignOnCard } = await import('./SingleSignOnCard');

const BASE = 'http://api.test';
const GOOGLE = { id: 'google', name: 'Google' };

const owner = (over: Partial<Organization> = {}): Organization => ({
  id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: true, role: 0, ...over,
});

function policy(over: Partial<OrganizationSso> = {}): OrganizationSso {
  return {
    required: false,
    domains: [],
    providers: [GOOGLE],
    canChange: true,
    members: [
      { userId: 'u-owner', name: 'Owner', email: 'owner@pavillon46.ch', isOwner: true, applies: false, hasSignedInWithSso: false },
      { userId: 'u-anna', name: 'Anna Keller', email: 'anna@pavillon46.ch', isOwner: false, applies: true, hasSignedInWithSso: true },
      { userId: 'u-marc', name: 'Marc Favre', email: 'marc@pavillon46.ch', isOwner: false, applies: true, hasSignedInWithSso: false },
    ],
    ...over,
  };
}

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-owner', name: 'Owner', email: 'owner@pavillon46.ch' }));
  state.org = owner();
  state.refreshOrgs.mockClear();
  toast.success.mockReset();
  toast.error.mockReset();
});

describe('the single sign-on requirement', () => {
  it('lets the owner require it for the workspace’s domains, and says whom it holds', async () => {
    let current = policy();
    let sent: unknown;
    server.use(
      http.get(`${BASE}/api/organizations/org-1/sso`, () => HttpResponse.json(current)),
      http.put(`${BASE}/api/organizations/org-1/sso`, async ({ request }) => {
        sent = await request.json();
        current = policy({ required: true, domains: ['pavillon46.ch'] });
        return HttpResponse.json(current);
      }),
    );
    const user = userEvent.setup();
    render(withQueries(<SingleSignOnCard />));

    const toggle = await screen.findByRole('checkbox', { name: /Require single sign-on/ });
    expect(toggle).not.toBeChecked();
    await user.type(screen.getByLabelText("The workspace's email domains"), 'Pavillon46.ch');
    await user.click(toggle);

    await waitFor(() => expect(sent).toEqual({ required: true, domains: ['Pavillon46.ch'] }));
    expect(await screen.findByRole('checkbox', { name: /Require single sign-on/ })).toBeChecked();
    expect(toast.success).toHaveBeenCalledWith('Single sign-on is now required here.');
    // The workspace list says whom it refuses now, so it is read again.
    expect(state.refreshOrgs).toHaveBeenCalled();
    expect(screen.getByText(/Required for 2 members; 1 of them has signed in with Google or Microsoft before\./)).toBeInTheDocument();
    const held = screen.getByRole('list', { name: 'Members it holds' });
    expect(within(held).getByText('Anna Keller')).toBeInTheDocument();
    expect(within(held).getByText('Signed in with SSO')).toBeInTheDocument();
    expect(within(held).getByText('Not yet')).toBeInTheDocument();
    expect(within(held).queryByText('Owner')).not.toBeInTheDocument();
  });

  it('shows a manager the setting without a way to change it', async () => {
    state.org = owner({ isOwner: false, role: 1, permissions: ['settings.manage'] });
    server.use(http.get(`${BASE}/api/organizations/org-1/sso`, () =>
      HttpResponse.json(policy({ required: true, domains: ['pavillon46.ch'], canChange: false }))));
    render(withQueries(<SingleSignOnCard />));

    expect(await screen.findByRole('checkbox', { name: /Require single sign-on/ })).toBeDisabled();
    expect(screen.getByLabelText("The workspace's email domains")).toBeDisabled();
    expect(screen.getByLabelText("The workspace's email domains")).toHaveValue('pavillon46.ch');
    expect(screen.queryByRole('button', { name: 'Save' })).not.toBeInTheDocument();
    expect(screen.getByText("Only the workspace's owner can change this.")).toBeInTheDocument();
  });

  it('cannot be turned on while the server offers no provider, and says so', async () => {
    server.use(http.get(`${BASE}/api/organizations/org-1/sso`, () => HttpResponse.json(policy({ providers: [] }))));
    render(withQueries(<SingleSignOnCard />));

    expect(await screen.findByRole('checkbox', { name: /Require single sign-on/ })).toBeDisabled();
    expect(screen.getByRole('status')).toHaveTextContent(/offers no single sign-on yet/);
  });

  it('is not shown to a member who can see neither the settings nor the team', () => {
    state.org = owner({ isOwner: false, role: 1, permissions: ['records.edit'] });
    const { container } = render(withQueries(<SingleSignOnCard />));
    expect(container).toBeEmptyDOMElement();
  });
});

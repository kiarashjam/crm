// Settings > Welcome & portal: the club's member portal. It starts off; whoever may manage the
// settings turns it on (with the version they read); the page says plainly when the workspace
// also hands members to its own portal; members are invited all at once or one by one and
// signed out by whoever runs the club's members; someone without those permissions sees it all
// without the switches.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import type { Organization } from '@/app/api/organizations';
import type { MemberPortalSettings, MemberPortalUse } from '@/app/api/memberPortal';

const state = vi.hoisted(() => ({ org: null as Organization | null }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: state.org, currentOrgId: state.org?.id ?? null }),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const { MemberPortalSettingsCard, inviteAllSummary } = await import('./MemberPortalSettingsCard');

const BASE = 'http://api.test';

const owner = (over: Partial<Organization> = {}): Organization => ({
  id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: true, role: 0, ...over,
});

function settings(over: Partial<MemberPortalSettings> = {}): MemberPortalSettings {
  return {
    enabled: false,
    portalUrl: 'https://app.example.ch/member-portal/abc',
    memberCancelHours: 24,
    handsOverToOwnPortal: false,
    version: 'v1',
    ...over,
  };
}

function use(over: Partial<MemberPortalUse> = {}): MemberPortalUse {
  return {
    memberId: 'm-anna',
    name: 'Anna Keller',
    email: 'anna@example.ch',
    status: 'Active',
    invitationsSent: 0,
    activeSessions: 0,
    canInvite: true,
    fromHandoff: false,
    ...over,
  };
}

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-owner', name: 'Owner', email: 'owner@pavillon46.ch' }));
  state.org = owner();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubGlobal('confirm', vi.fn(() => true));
});

describe('the member portal in Settings', () => {
  it('starts off, and is turned on with the version that was read', async () => {
    let current = settings();
    let sent: unknown;
    server.use(
      http.get(`${BASE}/api/member-portal/settings`, () => HttpResponse.json(current)),
      http.put(`${BASE}/api/member-portal/settings`, async ({ request }) => {
        sent = await request.json();
        current = settings({ enabled: true, version: 'v2' });
        return HttpResponse.json(current);
      }),
      http.get(`${BASE}/api/member-portal/members`, () => HttpResponse.json([use()])),
    );
    const user = userEvent.setup();
    render(withQueries(<MemberPortalSettingsCard />));

    const toggle = await screen.findByRole('switch', { name: 'Members can sign in to the member portal' });
    expect(toggle).toHaveAttribute('aria-checked', 'false');
    expect(screen.getByText('Off')).toBeInTheDocument();
    expect(screen.getByText('Members can be invited once the portal is on.')).toBeInTheDocument();
    await user.click(toggle);

    await waitFor(() => expect(sent).toEqual({ enabled: true, version: 'v1' }));
    expect(await screen.findByText('On')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('The member portal is on.');
    expect(screen.getByDisplayValue('https://app.example.ch/member-portal/abc')).toBeInTheDocument();
  });

  it('says plainly that members would have two places to sign in when the workspace also hands them to its own portal', async () => {
    server.use(
      http.get(`${BASE}/api/member-portal/settings`, () => HttpResponse.json(settings({ enabled: true, handsOverToOwnPortal: true }))),
      http.get(`${BASE}/api/member-portal/members`, () => HttpResponse.json([])),
    );
    render(withQueries(<MemberPortalSettingsCard />));

    const warning = await screen.findByRole('status');
    expect(warning).toHaveTextContent('Your members have two places to sign in.');
    expect(warning).toHaveTextContent('This workspace also hands new members to your own portal');
  });

  it('invites every member, says who was left out and that more remain, and invites or signs out one member', async () => {
    let people = [use(), use({ memberId: 'm-ben', name: 'Ben Keller', email: 'ben@example.ch', invitedAtUtc: '2026-11-01T10:00:00Z', invitationsSent: 1, activeSessions: 2 })];
    const posted: string[] = [];
    server.use(
      http.get(`${BASE}/api/member-portal/settings`, () => HttpResponse.json(settings({ enabled: true }))),
      http.get(`${BASE}/api/member-portal/members`, () => HttpResponse.json(people)),
      http.post(`${BASE}/api/member-portal/invite-all`, () => {
        posted.push('all');
        return HttpResponse.json({ invited: 200, alreadyInvited: 3, noEmail: 2, refused: 1, remaining: 40 });
      }),
      http.post(`${BASE}/api/members/m-anna/portal/invite`, () => {
        posted.push('anna');
        people = people.map((p) => (p.memberId === 'm-anna' ? { ...p, invitedAtUtc: '2026-11-13T09:00:00Z', invitationsSent: 1 } : p));
        return HttpResponse.json(people[0]);
      }),
      http.post(`${BASE}/api/members/m-ben/portal/revoke`, () => {
        posted.push('ben');
        people = people.map((p) => (p.memberId === 'm-ben' ? { ...p, activeSessions: 0 } : p));
        return HttpResponse.json(people[1]);
      }),
    );
    const user = userEvent.setup();
    render(withQueries(<MemberPortalSettingsCard />));

    expect(await screen.findByText('0 of 2 have signed in; 1 invited.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Invite every member' }));
    expect(await screen.findByText(/Invited 200 members\. Left out: 3 invited before, 2 without an email address, 1 whose address bounced or unsubscribed\. 40 more to invite/)).toBeInTheDocument();

    const anna = screen.getByText('Anna Keller').closest('tr')!;
    await user.click(within(anna).getByRole('button', { name: 'Invite' }));
    expect(await within(anna).findByRole('button', { name: 'Invite again' })).toBeInTheDocument();

    const ben = screen.getByText('Ben Keller').closest('tr')!;
    expect(within(ben).getByText('2 devices')).toBeInTheDocument();
    await user.click(within(ben).getByRole('button', { name: 'Sign out' }));
    await waitFor(() => expect(within(ben).queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument());
    expect(posted).toEqual(['all', 'anna', 'ben']);
  });

  it('shows someone without the permissions the portal as it is, without the switches', async () => {
    state.org = owner({ isOwner: false, role: 2, permissions: ['records.edit'] });
    server.use(
      http.get(`${BASE}/api/member-portal/settings`, () => HttpResponse.json(settings({ enabled: true }))),
      http.get(`${BASE}/api/member-portal/members`, () => HttpResponse.json([use({ activeSessions: 1 })])),
    );
    render(withQueries(<MemberPortalSettingsCard />));

    expect(await screen.findByRole('switch', { name: 'Members can sign in to the member portal' })).toBeDisabled();
    expect(screen.getByText(/take the Manage settings permission/)).toBeInTheDocument();
    expect(await screen.findByText('Anna Keller')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Invite every member' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Sign out' })).not.toBeInTheDocument();
  });

  it('says a change someone else made meanwhile, and shows the settings as they are now', async () => {
    let reads = 0;
    server.use(
      http.get(`${BASE}/api/member-portal/settings`, () => {
        reads += 1;
        return HttpResponse.json(settings({ enabled: reads > 1, version: `v${reads}` }));
      }),
      http.put(`${BASE}/api/member-portal/settings`, () =>
        HttpResponse.json({ detail: 'Someone else changed it.', errorCode: 'Portal.Settings.Changed.Conflict' }, { status: 409 })),
      http.get(`${BASE}/api/member-portal/members`, () => HttpResponse.json([])),
    );
    const user = userEvent.setup();
    render(withQueries(<MemberPortalSettingsCard />));

    await user.click(await screen.findByRole('switch', { name: 'Members can sign in to the member portal' }));
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/Someone else changed these settings meanwhile/)));
    expect(await screen.findByText('On')).toBeInTheDocument();
  });
});

describe('inviteAllSummary', () => {
  it('says only what happened', () => {
    expect(inviteAllSummary({ invited: 1, alreadyInvited: 0, noEmail: 0, refused: 0, remaining: 0 })).toBe('Invited 1 member.');
  });
});

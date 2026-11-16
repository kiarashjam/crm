// The links between the club and the sales side, shown both ways.
//
// A member's page names the lead and contact they came from and their household; a lead's
// or a contact's page names the member they became and their applications, and offers to
// start an application for someone who is not a member. A failed read says so with a retry.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { PERMISSION, type Permission } from '@/app/api/organizations';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-a',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-a', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions },
  };
};

const { MemberLinksCard, PersonMembershipCard } = await import('./MembershipLinks');

const BASE = 'http://api.test';
const HOUSEHOLD = {
  id: 'h1', name: 'Meier', createdAtUtc: '', primaryMemberId: 'm-peter', primaryName: 'Peter Meier', members: 2,
  people: [
    { linkId: 'l1', memberId: 'm-peter', name: 'Peter Meier', role: 'Primary', isPrimary: true, rollUpBilling: false },
    { linkId: 'l2', memberId: 'm-anna', name: 'Anna Meier', role: 'Spouse', isPrimary: false, rollUpBilling: true },
  ],
};

let leadMembership: () => Response;
let started: string[];

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  as([PERMISSION.ClubMembers]);
  started = [];
  leadMembership = () => HttpResponse.json({ members: [], applications: [], household: null });
  server.use(
    http.get(`${BASE}/api/members/m-anna/links`, () => HttpResponse.json({
      memberId: 'm-anna', leadId: 'l-anna', leadName: 'Anna Meier', leadStatus: 'Converted',
      contactId: 'c-anna', contactName: 'Anna Meier', contactEmail: 'anna@example.ch',
      applications: [{ id: 'a1', firstName: 'Anna', lastName: 'Meier', email: 'anna@example.ch', requestedTier: 'Gold', status: 'Approved', submittedAtUtc: '' }],
      household: HOUSEHOLD,
    })),
    http.get(`${BASE}/api/leads/l1/membership`, () => leadMembership()),
    http.post(`${BASE}/api/applications/from-lead/l1`, ({ request }) => {
      started.push(new URL(request.url).pathname);
      return HttpResponse.json({ id: 'a2', firstName: 'Theo', lastName: 'Kim', email: 'theo@example.ch', requestedTier: 'Gold', status: 'Submitted', submittedAtUtc: '' });
    }),
  );
});
afterEach(() => vi.unstubAllEnvs());

describe('on a member’s page, joined through a deal', () => {
  it('links the deal, and lists the contract with its deposit on their account', async () => {
    server.use(http.get(`${BASE}/api/members/m-lena/links`, () => HttpResponse.json({
      memberId: 'm-lena', leadId: 'l-lena', leadName: 'Lena Weber', contactId: 'c-lena', contactName: 'Lena Weber',
      dealId: 'd-lena', dealName: 'Gold membership Lena',
      applications: [],
      contracts: [{
        id: 'k1', title: 'Membership Agreement — Lena Weber', status: 'countersigned', membershipTier: 'Gold', dealId: 'd-lena',
        deposit: { amount: 500, currency: 'CHF', status: 'paid', amountPaid: 500, amountRefunded: 0, provider: 'stripe', methodType: 'card', memberId: 'm-lena', ledgerPaymentId: 'p1' },
      }],
      household: null,
    })));
    render(withQueries(<MemoryRouter><MemberLinksCard memberId="m-lena" /></MemoryRouter>));

    expect((await screen.findByRole('link', { name: /Gold membership Lena/ })).getAttribute('href')).toBe('/deals/d-lena');
    expect(screen.getByRole('link', { name: /Membership Agreement — Lena Weber/ }).getAttribute('href')).toBe('/deals/d-lena');
    expect(screen.getByText(/Fully executed · Gold/)).toBeInTheDocument();
    expect(screen.getByTestId('deposit-line')).toHaveTextContent(/Paid/);
    expect(screen.getByTestId('deposit-line')).toHaveTextContent(/On the account of/);
  });
});

describe('on a deal’s page', () => {
  it('names the member the deal brought in', async () => {
    server.use(http.get(`${BASE}/api/deals/d-lena/membership`, () => HttpResponse.json({
      contactId: 'c-lena',
      members: [{ id: 'm-lena', name: 'Lena Weber', email: 'lena@example.ch', tier: 'Gold', status: 'Active', joinedAtUtc: '2026-11-02T10:00:00Z' }],
      applications: [], household: null,
    })));
    render(withQueries(<MemoryRouter><PersonMembershipCard kind="deal" id="d-lena" personName="Lena Weber" contactId="c-lena" /></MemoryRouter>));

    expect((await screen.findByRole('link', { name: /Lena Weber/ })).getAttribute('href')).toBe('/members/m-lena');
    expect(screen.getByText(/Gold member · Active/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /start a membership application/i })).toBeNull();
  });
});

describe('on a member’s page', () => {
  it('names their lead and contact, how they joined, and that their bills go to the primary', async () => {
    render(withQueries(<MemoryRouter><MemberLinksCard memberId="m-anna" /></MemoryRouter>));

    expect((await screen.findAllByRole('link', { name: /Anna Meier/ })).length).toBe(2);
    const links = screen.getAllByRole('link').map((a) => a.getAttribute('href'));
    expect(links).toEqual(expect.arrayContaining(['/leads/l-anna', '/contacts/c-anna', '/households', '/applications']));
    expect(screen.getByText("Their charges, tabs and dues go on Peter Meier's account.")).toBeInTheDocument();
  });
});

describe('on a lead’s page', () => {
  it('starts an application for someone who is not a member, and offers none once they are', async () => {
    const user = userEvent.setup();
    const { unmount } = render(withQueries(<MemoryRouter><PersonMembershipCard kind="lead" id="l1" personName="Theo Kim" /></MemoryRouter>));

    expect(await screen.findByText('Theo is not a member.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Start a membership application' }));
    await waitFor(() => expect(started).toEqual(['/api/applications/from-lead/l1']));
    unmount();

    leadMembership = () => HttpResponse.json({
      members: [{ id: 'm-theo', name: 'Theo Kim', email: 'theo@example.ch', tier: 'Gold', status: 'Active', joinedAtUtc: '' }],
      applications: [], household: null,
    });
    render(withQueries(<MemoryRouter><PersonMembershipCard kind="lead" id="l1" personName="Theo Kim" /></MemoryRouter>));
    expect(await screen.findByRole('link', { name: /Theo Kim/ })).toHaveAttribute('href', '/members/m-theo');
    expect(screen.queryByRole('button', { name: 'Start a membership application' })).toBeNull();
  });

  it('says a failed read failed, with a way to try again', async () => {
    let fail = true;
    leadMembership = () => (fail
      ? HttpResponse.json({ title: 'Server Error', status: 500, detail: 'Busy' }, { status: 500 })
      : HttpResponse.json({ members: [], applications: [], household: null }));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter><PersonMembershipCard kind="lead" id="l1" personName="Theo Kim" /></MemoryRouter>));

    expect(await screen.findByText(/could not be loaded/)).toBeInTheDocument();
    fail = false;
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Theo is not a member.')).toBeInTheDocument();
  });

  it('offers no application to someone who may not manage club members', async () => {
    as([PERMISSION.EditRecords]);
    render(withQueries(<MemoryRouter><PersonMembershipCard kind="lead" id="l1" personName="Theo Kim" /></MemoryRouter>));
    expect(await screen.findByText('Theo is not a member.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Start a membership application' })).toBeNull();
  });
});

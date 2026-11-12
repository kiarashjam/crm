// Applications, against the server.
//
// Approval was three requests from the browser (read the list, add a Pending member, mark
// the application approved): a retry or two reviewers at once made two members, and the
// member was never billed. The page now asks the server to approve, once, with the tier and
// household chosen; a waitlisted application can be approved or reopened, a rejected one
// reopened first; and someone who handles money finishes the dues and fee a reviewer
// without money.manage left behind.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { resetLocalOnly } from '@/app/api/localOnly';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { Application } from '@/app/api/applications';
import type { Household } from '@/app/api/households';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
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

const Applications = (await import('./Applications')).default;

const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const BASE = 'http://api.test';
const now = new Date().toISOString();
const CLAIRE: Application = {
  id: 'a1', firstName: 'Claire', lastName: 'Martin', email: 'claire@example.ch', requestedTier: 'Gold', status: 'Submitted',
  source: 'form', submittedAtUtc: now, leadId: 'l1', leadName: 'Claire Martin', version: 'av1',
};
const MEIER: Household = {
  id: 'h1', name: 'Meier', createdAtUtc: now, primaryMemberId: 'm-peter', primaryName: 'Peter Meier', members: 1, version: 'hv1',
  people: [{ linkId: 'l-peter', memberId: 'm-peter', name: 'Peter Meier', role: 'Primary', isPrimary: true, rollUpBilling: false }],
};

let applications: Application[];
let sent: { method: string; path: string; body: unknown }[];
let approval: () => Response;

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  resetClubCurrency();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  applications = [CLAIRE];
  sent = [];
  approval = () => {
    applications = [{ ...CLAIRE, status: 'Approved', memberId: 'm-claire', memberName: 'Claire Martin', version: 'av2' }];
    return HttpResponse.json({
      application: applications[0], memberId: 'm-claire', memberName: 'Claire Martin', memberCreated: true, approved: true,
      duesStarted: true, feeCharged: true, joinedHousehold: true, welcomeQueued: true, moneyPending: false, notes: [],
    });
  };
  const record = async (request: Request, respond: () => Response) => {
    const text = await request.text();
    sent.push({ method: request.method, path: new URL(request.url).pathname, body: text ? JSON.parse(text) : null });
    return respond();
  };
  server.use(
    http.get(`${BASE}/api/applications`, () => HttpResponse.json(applications)),
    http.get(`${BASE}/api/households`, () => HttpResponse.json([MEIER])),
    http.get(`${BASE}/api/catalog`, () => HttpResponse.json([
      { id: 't1', kind: 'Membership', name: 'Gold', price: 299, cadence: 'Monthly', isActive: true, sortOrder: 1, currency: 'CHF', benefits: [] },
      { id: 't2', kind: 'Membership', name: 'Platinum', price: 599, cadence: 'Monthly', isActive: true, sortOrder: 2, currency: 'CHF', benefits: [] },
    ])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({ currency: 'CHF' })),
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
    http.get(`${BASE}/api/applications/form`, () => HttpResponse.json({ form: null })),
    http.post(`${BASE}/api/applications/:id/approve`, ({ request }) => record(request, approval)),
    http.post(`${BASE}/api/applications/:id/status`, ({ request }) => record(request, () => HttpResponse.json({ ...CLAIRE, status: 'Submitted' }))),
    http.put(`${BASE}/api/applications/form`, ({ request }) => record(request, () => HttpResponse.json({
      id: 'f1', publicToken: 'tok', publicPath: '/apply/tok', isActive: true, consentText: 'Yes, email me.', createdAtUtc: now,
    }))),
  );
});
afterEach(() => vi.unstubAllEnvs());

const show = () => render(withQueries(<MemoryRouter><Applications /></MemoryRouter>));

describe('approving an application', () => {
  it('asks the server once, with the household chosen and the version read, and then links to the member', async () => {
    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show();

    // Where it came from: the public form, and the lead it was linked to.
    expect(await screen.findByText('Public form')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Lead: Claire Martin' })).toHaveAttribute('href', '/leads/l1');

    await user.click(screen.getByRole('button', { name: 'Approve Claire Martin' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('combobox', { name: 'Household to join' }));
    await user.click(await screen.findByRole('option', { name: 'The Meier household' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('switch', { name: /Bill their charges/ }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Approve and admit' }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({
      method: 'POST', path: '/api/applications/a1/approve',
      body: { householdId: 'h1', householdRole: 'Spouse', rollUpBilling: true, version: 'av1' },
    });
    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Claire Martin is now a member', expect.anything()));
    expect(await screen.findByRole('link', { name: /Member: Claire Martin/ })).toHaveAttribute('href', '/members/m-claire');
    expect(screen.queryByRole('button', { name: 'Approve Claire Martin' })).toBeNull();
  });

  it('asks for a tier when the applicant chose none on the public form, instead of breaking the dialog', async () => {
    // "Not sure yet" on the public form leaves the application without a tier. The dialog
    // offered that empty tier as a choice, which the select refuses to draw, and the page broke.
    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    applications = [{ ...CLAIRE, requestedTier: '' as Application['requestedTier'] }];
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Requested: no tier chosen')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Approve Claire Martin' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByRole('button', { name: 'Approve and admit' })).toBeDisabled();
    await user.click(within(dialog).getByRole('combobox', { name: 'Tier to admit them at' }));
    expect(screen.queryByRole('option', { name: '' })).toBeNull();
    await user.click(await screen.findByRole('option', { name: 'Gold' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Approve and admit' }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'POST', path: '/api/applications/a1/approve', body: { tier: 'Gold', version: 'av1' } });
  });

  it('offers a reviewer who cannot charge accounts no roll-up, and tells them the money is left for finance', async () => {
    as([PERMISSION.ClubMembers]);
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Approve Claire Martin' }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByText(/left for someone who handles the club’s money/)).toBeInTheDocument();
    await user.click(within(dialog).getByRole('combobox', { name: 'Household to join' }));
    await user.click(await screen.findByRole('option', { name: 'The Meier household' }));
    expect(within(screen.getByRole('dialog')).queryByRole('switch')).toBeNull();
  });

  it('lets a waitlisted application be approved or reopened, and a rejected one only reopened', async () => {
    as([PERMISSION.ClubMembers]);
    applications = [
      { ...CLAIRE, id: 'w1', firstName: 'Wanda', lastName: 'Wait', status: 'Waitlisted', version: 'wv1', leadId: undefined, source: 'staff' },
      { ...CLAIRE, id: 'r1', firstName: 'Rita', lastName: 'Rejected', status: 'Rejected', version: 'rv1', leadId: undefined, source: 'staff' },
    ];
    const user = userEvent.setup();
    show();

    expect(await screen.findByRole('button', { name: 'Approve Wanda Wait' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: "Reopen Wanda Wait's application" })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Approve Rita Rejected' })).toBeNull();

    await user.click(screen.getByRole('button', { name: "Reopen Rita Rejected's application" }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'POST', path: '/api/applications/r1/status', body: { status: 'Submitted', version: 'rv1' } });
  });

  it('lets someone who handles money set up the dues and fee a reviewer left, and no one else', async () => {
    applications = [{ ...CLAIRE, status: 'Approved', memberId: 'm-claire', memberName: 'Claire Martin', moneyPending: true }];
    as([PERMISSION.ClubMembers]);
    const { unmount } = show();
    expect(await screen.findByText('Dues and fee not set up yet')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Set up dues and fee' })).toBeNull();
    unmount();

    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show();
    await user.click(await screen.findByRole('button', { name: 'Set up dues and fee' }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'POST', path: '/api/applications/a1/approve', body: {} });
  });

  it('says a refusal in the server’s words, and keeps the dialog open', async () => {
    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    const refusal = 'Gold is not on sale. Pick a tier the club offers.';
    approval = () => HttpResponse.json({ title: 'Bad Request', status: 400, detail: refusal }, { status: 400 });
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Approve Claire Martin' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'Approve and admit' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(refusal));
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });
});

describe('the public application form', () => {
  it('is opened by whoever manages the settings, and not offered to others', async () => {
    as([PERMISSION.ClubMembers]);
    const { unmount } = show();
    expect((await screen.findAllByText(/Claire Martin/)).length).toBeGreaterThan(0);
    expect(screen.queryByRole('button', { name: 'Open the form' })).toBeNull();
    unmount();

    as([PERMISSION.ClubMembers, PERMISSION.ManageSettings]);
    const user = userEvent.setup();
    show();
    await user.click(await screen.findByRole('button', { name: 'Open the form' }));

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'PUT', path: '/api/applications/form', body: { isActive: true } });
  });
});

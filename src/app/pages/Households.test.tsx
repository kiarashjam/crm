// Households, against the server.
//
// Roll-up billing was a switch that saved a label nothing read, so the page said charges
// stayed on each member's own account. The server now bills a member whose bills roll up
// to the household's primary, so the page says whose account they go on, lets someone who
// handles money switch it, changes roles and the primary in place, and sends the version
// it read the household at, so a change made from a stale page is refused, not applied.

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

const Households = (await import('./Households')).default;

// Radix Select asks the browser about pointer capture and scrolling, which happy-dom lacks.
const proto = Element.prototype as unknown as Record<string, unknown>;
proto.hasPointerCapture ??= () => false;
proto.releasePointerCapture ??= () => undefined;
proto.scrollIntoView ??= () => undefined;

const BASE = 'http://api.test';
const MEIER: Household = {
  id: 'h1', name: 'Meier', createdAtUtc: '2026-01-01T00:00:00Z', primaryLinkId: 'l-peter', primaryMemberId: 'm-peter',
  primaryName: 'Peter Meier', members: 3, currency: 'CHF', version: 'v1',
  people: [
    { linkId: 'l-peter', memberId: 'm-peter', name: 'Peter Meier', role: 'Primary', isPrimary: true, rollUpBilling: false, tier: 'Gold', duesAmount: 299, duesFrequency: 'Monthly', houseBalance: -180 },
    { linkId: 'l-anna', memberId: 'm-anna', name: 'Anna Meier', role: 'Spouse', isPrimary: false, rollUpBilling: true, tier: 'Gold', duesAmount: 299, duesFrequency: 'Monthly', houseBalance: 0 },
    { linkId: 'l-kit', memberId: 'm-kit', name: 'Kit Meier', role: 'Child', isPrimary: false, rollUpBilling: false, tier: 'Bronze', duesAmount: 49, duesFrequency: 'Monthly', houseBalance: -40 },
  ],
};

let households: Household[];
let householdReads: number;
let sent: { method: string; path: string; body: unknown }[];
let answer: () => Response;

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
  households = [MEIER];
  householdReads = 0;
  sent = [];
  answer = () => HttpResponse.json(MEIER);
  const record = async (request: Request) => {
    const text = await request.text();
    sent.push({ method: request.method, path: new URL(request.url).pathname, body: text ? JSON.parse(text) : null });
    return answer();
  };
  server.use(
    http.get(`${BASE}/api/households`, () => {
      householdReads++;
      return HttpResponse.json(households);
    }),
    http.get(`${BASE}/api/members`, () => HttpResponse.json([])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({ currency: 'CHF' })),
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
    http.get(`${BASE}/api/duplicates/not-same`, () => HttpResponse.json([])),
    http.put(`${BASE}/api/households/:id/members/:linkId`, ({ request }) => record(request)),
    http.post(`${BASE}/api/households/:id/primary`, ({ request }) => record(request)),
  );
});
afterEach(() => vi.unstubAllEnvs());

const show = () => render(withQueries(<MemoryRouter><Households /></MemoryRouter>));

describe('households', () => {
  it("says whose account a member's bills go on, and lets someone who handles money switch it, from the version it read", async () => {
    as([PERMISSION.ClubMembers, PERMISSION.ManageMoney]);
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Bills go to Peter Meier')).toBeInTheDocument();
    expect(screen.getByText('Billed on their own account')).toBeInTheDocument();
    const toggle = screen.getByRole('switch', { name: "Bill Anna Meier's charges to Peter Meier" });
    expect(toggle).toBeChecked();

    await user.click(toggle);

    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'PUT', path: '/api/households/h1/members/l-anna', body: { rollUpBilling: false, version: 'v1' } });
  });

  it('offers no roll-up switch to someone who does not handle money, but still says where the bills go', async () => {
    as([PERMISSION.ClubMembers]);
    show();

    expect(await screen.findByText('Bills go to Peter Meier')).toBeInTheDocument();
    expect(screen.queryByRole('switch')).toBeNull();
    // The role and the primary are theirs to change.
    expect(screen.getByRole('button', { name: 'Make Anna Meier the primary' })).toBeInTheDocument();
  });

  it('changes a role in place and makes someone else the primary', async () => {
    as([PERMISSION.ClubMembers]);
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('combobox', { name: "Kit Meier's role in the household" }));
    await user.click(await screen.findByRole('option', { name: 'Dependent' }));
    await waitFor(() => expect(sent).toHaveLength(1));
    expect(sent[0]).toEqual({ method: 'PUT', path: '/api/households/h1/members/l-kit', body: { role: 'Dependent', version: 'v1' } });

    await user.click(screen.getByRole('button', { name: 'Make Anna Meier the primary' }));
    await waitFor(() => expect(sent).toHaveLength(2));
    expect(sent[1]).toEqual({ method: 'POST', path: '/api/households/h1/primary', body: { linkId: 'l-anna', version: 'v1' } });
  });

  it("says a change from a stale page was refused, and shows the household as it is now", async () => {
    as([PERMISSION.ClubMembers]);
    const changed = 'Someone changed this household first. Reload it and make your change again.';
    answer = () => HttpResponse.json({ title: 'Conflict', status: 409, detail: changed }, { status: 409 });
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Make Kit Meier the primary' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(changed));
    await waitFor(() => expect(householdReads).toBeGreaterThan(1));
  });

  it('never offers to take the primary out of the household', async () => {
    as([PERMISSION.ClubMembers, PERMISSION.DeleteRecords]);
    show();

    const card = (await screen.findByText('The Meier household')).closest('article')!;
    expect(within(card).queryByRole('button', { name: 'Take Peter Meier out of the household' })).toBeNull();
    expect(within(card).getByRole('button', { name: 'Take Anna Meier out of the household' })).toBeInTheDocument();
  });
});

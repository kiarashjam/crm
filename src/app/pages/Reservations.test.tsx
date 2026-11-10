// The bookings page, against the real server.
//
// It used to read its lists once into local state, so a failed read looked like a club
// with no bookings; it sent the member as free text, so a booking was found later by
// matching an email; it checked no clash at all; and it offered every status from every
// status. The server keeps the rules now: the page sends the member's id, says the clash
// the server names, and offers a deposit's money options only to those who handle money.

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
import type { Reservation, Resource } from '@/app/api/reservations';

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

const Reservations = (await import('./Reservations')).default;

const BASE = 'http://api.test';
const COURT: Resource = {
  id: 'r-court', name: 'Tennis Court', kind: 'Court', capacity: 4, bookingMode: 'Exclusive',
  depositAmount: 0, noShowFee: 0, active: true, version: 'rv1',
};
const PRIVATE_DINING: Resource = {
  id: 'r-pdr', name: 'Private Dining Room', kind: 'Dining', capacity: 16, bookingMode: 'Exclusive',
  depositAmount: 200, noShowFee: 0, active: true, version: 'rv2',
};
const MEMBER = {
  id: '5b0e0c4a-0000-4000-8000-000000000009', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch',
  tier: 'Gold', status: 'Active', joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};
const BOOKED: Reservation = {
  id: 'b-1', resourceId: COURT.id, resourceName: COURT.name, memberId: MEMBER.id, memberName: 'Jean Dupont',
  partySize: 2, startAtUtc: new Date(Date.now() + 86_400_000).toISOString(), endAtUtc: new Date(Date.now() + 90_000_000).toISOString(),
  status: 'Confirmed', version: 'bv1',
};

let resources: Resource[];
let bookings: Reservation[];
let failReads: number;
let posted: Record<string, unknown> | null;
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
  as([PERMISSION.FrontDesk, PERMISSION.ClubMembers]);
  resources = [COURT, PRIVATE_DINING];
  bookings = [];
  failReads = 0;
  posted = null;
  answer = () => HttpResponse.json({ ...BOOKED, id: 'b-new' });
  server.use(
    http.get(`${BASE}/api/reservations`, () => (failReads-- > 0
      ? HttpResponse.json({ title: 'Server Error', status: 500, detail: 'The server is busy.' }, { status: 500 })
      : HttpResponse.json(bookings))),
    http.get(`${BASE}/api/resources`, () => HttpResponse.json(resources)),
    http.get(`${BASE}/api/members`, () => HttpResponse.json([MEMBER])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({ currency: 'CHF' })),
    // The regional settings, for how dates and money are written.
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
    http.post(`${BASE}/api/reservations`, async ({ request }) => {
      posted = (await request.json()) as Record<string, unknown>;
      return answer();
    }),
  );
});
afterEach(() => vi.unstubAllEnvs());

const show = () => render(withQueries(<MemoryRouter><Reservations /></MemoryRouter>));

describe('the bookings page', () => {
  it('says a failed read failed, with a way to try again, instead of showing no bookings', async () => {
    failReads = 1;
    bookings = [BOOKED];
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Reservations could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText('No bookings yet')).toBeNull();

    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect((await screen.findAllByText('Jean Dupont')).length).toBeGreaterThan(0);
  });

  it("books for a member by id: their name and email are the record's", async () => {
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New booking/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Book' }));

    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted).toMatchObject({ resourceId: COURT.id, memberId: MEMBER.id, partySize: 2, status: 'Confirmed' });
    expect(posted).not.toHaveProperty('memberName');
    expect(posted).not.toHaveProperty('memberEmail');
  });

  it('says which booking is in the way when the server refuses a clash', async () => {
    const clash = 'Tennis Court is already booked on Sat 14 Nov from 14:00 to 15:00 by Jean Dupont (Confirmed), so it can\'t take this booking.';
    answer = () => HttpResponse.json({ title: 'Conflict', status: 409, detail: clash }, { status: 409 });
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New booking/ }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Book' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(clash));
    // The dialog stays open with what was entered, to pick another time.
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it("offers the front desk only to charge a deposit to the member's account", async () => {
    resources = [PRIVATE_DINING, COURT];
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New booking/ }));
    const deposit = await screen.findByRole('radiogroup', { name: 'Deposit' });

    expect(within(deposit).getAllByRole('radio').map((b) => b.textContent)).toEqual(['Charge to house account']);
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Book' }));
    await waitFor(() => expect(posted).toMatchObject({ resourceId: PRIVATE_DINING.id, deposit: 'charge' }));
  });

  it('offers whoever handles money to record a deposit paid, with how, or to waive it with a reason', async () => {
    as([PERMISSION.FrontDesk, PERMISSION.ManageMoney]);
    resources = [PRIVATE_DINING, COURT];
    answer = () => HttpResponse.json({ ...BOOKED, id: 'b-new', depositAmount: 200, depositState: 'paid' });
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /New booking/ }));
    const deposit = await screen.findByRole('radiogroup', { name: 'Deposit' });
    expect(within(deposit).getAllByRole('radio').map((b) => b.textContent)).toEqual(['Charge to house account', 'Paid now', 'Waive']);

    await user.click(within(deposit).getByRole('radio', { name: 'Paid now' }));
    await user.selectOptions(screen.getByLabelText('Paid by'), 'Cash');
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Book' }));

    await waitFor(() => expect(posted).toMatchObject({ deposit: 'paid', depositMethod: 'Cash' }));
    expect(posted).not.toHaveProperty('depositReason');
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Booked · deposit .*200.* paid$/));
  });

  it('offers to add the usual spaces to a club with none, to whoever runs the members', async () => {
    resources = [];
    let added = false;
    server.use(http.post(`${BASE}/api/resources/defaults`, () => {
      added = true;
      resources = [COURT];
      return HttpResponse.json(resources);
    }));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Add the usual spaces' }));

    await waitFor(() => expect(added).toBe(true));
    expect(await screen.findByText(/one booking at a time/)).toBeInTheDocument();
  });

  it('does not offer the front desk to manage the resources', async () => {
    as([PERMISSION.FrontDesk]);
    resources = [];
    show();

    expect(await screen.findByText(/Nothing can be booked yet/)).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Add the usual spaces' })).toBeNull();
    expect(screen.queryByRole('button', { name: /Resources/ })).toBeNull();
  });
});

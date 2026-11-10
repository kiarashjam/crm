// The events list, against the real server.
//
// It read the list once into local state, so a failed read looked like a club with no
// events, and its RSVP button added a seat with nobody's name on it. Now a failed read says
// so with a way to try again, and registering puts a member on the list by id.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { resetLocalOnly } from '@/app/api/localOnly';
import { resetClubCurrency } from '@/app/hooks/useClubMoney';
import type { ClubEvent } from '@/app/api/events';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Events = (await import('./Events')).default;

const BASE = 'http://api.test';
const EVENT: ClubEvent = {
  id: '7f3c2a10-0000-4000-8000-000000000001', name: 'Wine night',
  startAtUtc: new Date(Date.now() + 7 * 86_400_000).toISOString(), endAtUtc: new Date(Date.now() + 7 * 86_400_000 + 3 * 3_600_000).toISOString(),
  capacity: 40, registeredCount: 12, waitlistCount: 0, seatsLeft: 28, memberPrice: 35, guestPrice: 55,
  visibility: 'MembersAndGuests', status: 'Published', coverEmoji: '🍷', version: 'v1',
};
const MEMBER = {
  id: '5b0e0c4a-0000-4000-8000-000000000009', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch',
  tier: 'Gold', status: 'Active', joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};

let failReads: number;
let requests: string[];

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
  failReads = 0;
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
  server.use(
    http.get(`${BASE}/api/events`, () => (failReads-- > 0
      ? HttpResponse.json({ title: 'Server Error', status: 500, detail: 'The server is busy.' }, { status: 500 })
      : HttpResponse.json([EVENT]))),
    http.get(`${BASE}/api/members`, () => HttpResponse.json([MEMBER])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({ currency: 'CHF' })),
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
  );
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

const show = () => render(withQueries(<MemoryRouter><Events /></MemoryRouter>));

describe('the events list', () => {
  it('says a failed read failed, with a way to try again, instead of showing no events', async () => {
    failReads = 1;
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Events could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText(/No events yet/)).toBeNull();

    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect((await screen.findAllByText('Wine night')).length).toBeGreaterThan(0);
  });

  it('registers a member by id from the list, and says the seat and the ticket the server gave', async () => {
    let body: unknown;
    server.use(http.post(`${BASE}/api/events/${EVENT.id}/attendees`, async ({ request }) => {
      body = await request.json();
      return HttpResponse.json({
        id: 'a-1', eventId: EVENT.id, memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member', status: 'Reserved',
        chargeState: 'charged', ticketPrice: 35, rsvpAtUtc: new Date().toISOString(),
      });
    }));
    const user = userEvent.setup();
    show();

    await user.click((await screen.findAllByRole('button', { name: 'Register' }))[0]!);
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Register' }));

    await waitFor(() => expect(body).toEqual({ memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member' }));
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/^Jean Dupont has a seat · ticket .*35.* on their house account$/));
    // The old RSVP raised the event's count with no name on it.
    expect(requests).not.toContain(`POST /api/events/${EVENT.id}/rsvp`);
  });

  it('says the server refused a delete, and why', async () => {
    const why = "This event has tickets on members' accounts, so it can't be deleted. Cancel it instead: the tickets are taken back and everyone on the list is told.";
    server.use(http.delete(`${BASE}/api/events/${EVENT.id}`, () => HttpResponse.json(
      { title: 'Conflict', status: 409, detail: why }, { status: 409 },
    )));
    Object.defineProperty(window, 'confirm', { value: () => true, configurable: true, writable: true });
    const user = userEvent.setup();
    show();

    await user.click((await screen.findAllByRole('button', { name: 'Delete Wine night' }))[0]!);

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(why));
  });
});

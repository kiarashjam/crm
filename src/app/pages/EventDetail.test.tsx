// The event page's list and door, against the real server.
//
// It used to read and write only this browser's copy, so a door host on another device opened
// the event and found nobody to check in; then the page decided seats, counts and points
// itself. The server keeps all of it now: the page asks, and says what the server did.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { resetLocalOnly } from '@/app/api/localOnly';
import type { ClubEvent } from '@/app/api/events';
import type { EventAttendee } from '@/app/api/eventAttendees';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const EventDetail = (await import('./EventDetail')).default;

const BASE = 'http://api.test';
const EVENT_ID = '7f3c2a10-0000-4000-8000-000000000001';
const EVENT: ClubEvent = {
  id: EVENT_ID, name: 'Wine night', startAtUtc: '2026-11-06T18:00:00Z', endAtUtc: '2026-11-06T21:00:00Z',
  capacity: 40, registeredCount: 2, waitlistCount: 0, memberPrice: 35, guestPrice: 55,
  visibility: 'MembersAndGuests', status: 'Published', coverEmoji: '🍷', version: 'v1',
};
const ENTERED_ON_THE_LAPTOP: EventAttendee = {
  id: 'a-1', eventId: EVENT_ID, memberName: 'Claire Martin', ticketTier: 'Guest', status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
};
const KEPT_IN_THIS_BROWSER: EventAttendee = {
  id: 'att-1730000000000-ab12cd', eventId: EVENT_ID, memberName: 'Amélie Roux', ticketTier: 'Guest', status: 'Reserved', rsvpAtUtc: '2026-10-02T18:00:00Z',
};
const MEMBER = {
  id: '5b0e0c4a-0000-4000-8000-000000000009', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch',
  tier: 'Gold', status: 'Active', joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};

let onServer: EventAttendee[];
let event: ClubEvent;
let requests: string[];

// Cancelling asks why with the browser's prompt, and a no-show asks to confirm.
const prompt = vi.fn<(message?: string, value?: string) => string | null>();
Object.defineProperty(window, 'prompt', { value: prompt, configurable: true, writable: true });
Object.defineProperty(window, 'confirm', { value: () => true, configurable: true, writable: true });

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  toast.success.mockReset();
  toast.error.mockReset();
  prompt.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  onServer = [ENTERED_ON_THE_LAPTOP];
  event = EVENT;
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
  server.use(
    http.get(`${BASE}/api/events/${EVENT_ID}`, () => HttpResponse.json(event)),
    http.get(`${BASE}/api/members`, () => HttpResponse.json([MEMBER])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({})),
    // The regional settings, for how dates and money are written.
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
    http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json(onServer)),
    http.post(`${BASE}/api/events/${EVENT_ID}/attendees/import`, async ({ request }) => {
      const body = (await request.json()) as { memberName: string };
      const row = { ...KEPT_IN_THIS_BROWSER, id: 'server-2', memberName: body.memberName };
      onServer = [...onServer, row];
      return HttpResponse.json(row);
    }),
  );
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

const show = () => render(withQueries(
  <MemoryRouter initialEntries={[`/events/${EVENT_ID}`]}>
    <Routes>
      <Route path="/events/:id" element={<EventDetail />} />
    </Routes>
  </MemoryRouter>,
));

describe('event attendees', () => {
  it('shows the list the rest of the team entered, from the server', async () => {
    show();

    expect(await screen.findByText('Claire Martin')).toBeInTheDocument();
    expect(screen.queryByText(/saved only in this browser/)).toBeNull();
    // The event itself is read on its own, not picked out of the whole list.
    expect(requests).toContain(`GET /api/events/${EVENT_ID}`);
    expect(requests).not.toContain('GET /api/events');
  });

  it('offers to upload what this browser kept before, and then shows it from the server', async () => {
    localStorage.setItem('crm.mock.eventAttendees.v1:user:pat:org:org-a', JSON.stringify([KEPT_IN_THIS_BROWSER]));
    const user = userEvent.setup();
    show();

    expect(await screen.findByText(/saved only in this browser/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Upload to the server/ }));

    await waitFor(() => expect(screen.queryByText(/saved only in this browser/)).toBeNull());
    expect(await screen.findByText('Amélie Roux')).toBeInTheDocument();
    expect(onServer.map((a) => a.memberName)).toEqual(['Claire Martin', 'Amélie Roux']);
  });

  it('says the points the server gave with a check-in, and gives none itself', async () => {
    const reserved: EventAttendee = {
      id: 'a-9', eventId: EVENT_ID, memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member',
      status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
    };
    onServer = [reserved];
    let sent: unknown;
    server.use(http.put(`${BASE}/api/events/${EVENT_ID}/attendees/a-9`, async ({ request }) => {
      sent = await request.json();
      onServer = [{ ...reserved, status: 'CheckedIn', checkedInAtUtc: '2026-11-06T18:01:00Z', pointsAwarded: 75 }];
      return HttpResponse.json(onServer[0]);
    }));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /Check in/ }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('Checked in · +75 loyalty points'));
    expect(sent).toEqual({ status: 'CheckedIn' });
    // The page used to post the points to the loyalty ledger itself, once per press.
    expect(requests.filter((r) => r.includes('/api/loyalty'))).toEqual([]);
    expect(await screen.findByRole('tab', { name: /Came \(1\)/ })).toBeInTheDocument();
  });

  it('gives no points when another door host already checked the member in, and reads the list again', async () => {
    // This screen still shows Jean as expected; on the server he was checked in a minute ago.
    const reserved: EventAttendee = {
      id: 'a-9', eventId: EVENT_ID, memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member',
      status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
    };
    const checkedIn: EventAttendee = { ...reserved, status: 'CheckedIn', checkedInAtUtc: '2026-11-06T18:01:00Z' };
    let reads = 0;
    server.use(
      http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json([reads++ === 0 ? reserved : checkedIn])),
      http.put(`${BASE}/api/events/${EVENT_ID}/attendees/a-9`, () => HttpResponse.json(
        { title: 'Conflict', status: 409, detail: 'This person is already checked in' }, { status: 409 },
      )),
    );
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /Check in/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('This person is already checked in'));
    expect(toast.success).not.toHaveBeenCalled();
    // The list is read again, so the screen stops offering the check-in.
    await waitFor(() => expect(screen.queryByRole('button', { name: /Check in/ })).toBeNull());
    expect(screen.getByRole('tab', { name: /Came \(1\)/ })).toBeInTheDocument();
  });

  it('says seats counted with no name on this list may be in another browser, and lets them go', async () => {
    // Before the move, the events manager's laptop kept the names and raised the server's
    // count; until that list is uploaded, the door host sees the count and no names.
    event = { ...EVENT, registeredCount: 41, unnamedSeats: 40 };
    let patch: unknown;
    server.use(http.put(`${BASE}/api/events/${EVENT_ID}`, async ({ request }) => {
      patch = await request.json();
      event = { ...EVENT, registeredCount: 1, unnamedSeats: 0, version: 'v2' };
      return HttpResponse.json(event);
    }));
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Claire Martin')).toBeInTheDocument();
    expect(screen.getByText(/40 seats were counted with no name on this list/)).toHaveTextContent(/another browser/);

    await user.click(screen.getByRole('button', { name: /Release them/ }));

    // From the copy on screen: an edit made since is refused rather than overwritten.
    await waitFor(() => expect(patch).toEqual({ unnamedSeats: 0, unnamedWaitlist: 0, version: 'v1' }));
    await waitFor(() => expect(screen.queryByText(/counted with no name/)).toBeNull());
  });

  it('registers a member by id, and says when the server put them on the waitlist', async () => {
    let body: unknown;
    server.use(http.post(`${BASE}/api/events/${EVENT_ID}/attendees`, async ({ request }) => {
      body = await request.json();
      return HttpResponse.json({
        id: 'a-5', eventId: EVENT_ID, memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member',
        status: 'Waitlisted', waitlistPosition: 2, chargeState: 'waiting', ticketPrice: 35, rsvpAtUtc: '2026-11-01T10:00:00Z',
      });
    }));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /Add attendee/ }));
    await user.click(await screen.findByRole('button', { name: 'Register' }));

    await waitFor(() => expect(body).toEqual({ memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member' }));
    expect(toast.success).toHaveBeenCalledWith('Jean Dupont is on the waitlist (#2): no ticket until a seat comes free');
  });

  it("says why the server refused a registration, in the server's words", async () => {
    server.use(http.post(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json(
      { title: 'Conflict', status: 409, detail: "Jean Dupont is already on this event's list." }, { status: 409 },
    )));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /Add attendee/ }));
    await user.click(await screen.findByRole('button', { name: 'Register' }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith("Jean Dupont is already on this event's list."));
  });

  it('cancels with the reason given, and says a charged ticket comes off the account', async () => {
    onServer = [{ ...ENTERED_ON_THE_LAPTOP, chargeState: 'charged', ticketPrice: 55, hostName: 'Jean Dupont', chargeId: 'c-1' }];
    prompt.mockReturnValueOnce('Ill');
    let sent: unknown;
    server.use(http.put(`${BASE}/api/events/${EVENT_ID}/attendees/a-1`, async ({ request }) => {
      sent = await request.json();
      return HttpResponse.json({ ...onServer[0], status: 'Cancelled', chargeState: 'reversed' });
    }));
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Cancel Claire Martin' }));

    await waitFor(() => expect(sent).toEqual({ status: 'Cancelled', reason: 'Ill' }));
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/ticket is taken off the account/));
  });

  it('says an event that does not exist is not found', async () => {
    server.use(http.get(`${BASE}/api/events/${EVENT_ID}`, () => HttpResponse.json(
      { title: 'Not Found', status: 404, detail: 'The event was not found.' }, { status: 404 },
    )));
    show();

    expect(await screen.findByText('Event not found')).toBeInTheDocument();
  });
});

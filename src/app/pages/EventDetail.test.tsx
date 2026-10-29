// The event page's attendee list, against the real server.
//
// It used to read and write only this browser's copy, so a door host on another
// device opened the event and found nobody to check in.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { resetLocalOnly } from '@/app/api/localOnly';
import type { ClubEvent } from '@/app/api/events';
import type { EventAttendee } from '@/app/api/eventAttendees';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
const toast = { success: vi.fn(), error: vi.fn() };
vi.mock('sonner', () => ({ toast }));

const EventDetail = (await import('./EventDetail')).default;

const BASE = 'http://api.test';
const EVENT_ID = '7f3c2a10-0000-4000-8000-000000000001';
const EVENT: ClubEvent = {
  id: EVENT_ID, name: 'Wine night', startAtUtc: '2026-11-06T18:00:00Z', endAtUtc: '2026-11-06T21:00:00Z',
  capacity: 40, registeredCount: 2, waitlistCount: 0, memberPrice: 35, guestPrice: 55,
  visibility: 'MembersAndGuests', status: 'Published', coverEmoji: '🍷',
};
const ENTERED_ON_THE_LAPTOP: EventAttendee = {
  id: 'a-1', eventId: EVENT_ID, memberName: 'Claire Martin', ticketTier: 'Guest', status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
};
const KEPT_IN_THIS_BROWSER: EventAttendee = {
  id: 'att-1730000000000-ab12cd', eventId: EVENT_ID, memberName: 'Amélie Roux', ticketTier: 'Guest', status: 'Reserved', rsvpAtUtc: '2026-10-02T18:00:00Z',
};

let onServer: EventAttendee[];

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  onServer = [ENTERED_ON_THE_LAPTOP];
  server.use(
    http.get(`${BASE}/api/events`, () => HttpResponse.json([EVENT])),
    http.get(`${BASE}/api/members`, () => HttpResponse.json([])),
    http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({})),
    http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json(onServer)),
    http.post(`${BASE}/api/events/${EVENT_ID}/attendees/import`, async ({ request }) => {
      const body = (await request.json()) as { memberName: string };
      const row = { ...KEPT_IN_THIS_BROWSER, id: 'server-2', memberName: body.memberName };
      onServer = [...onServer, row];
      return HttpResponse.json(row);
    }),
  );
});
afterEach(() => vi.unstubAllEnvs());

const MEMBER = {
  id: '5b0e0c4a-0000-4000-8000-000000000009', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch',
  tier: 'Gold', status: 'Active', joinedAtUtc: '2024-01-01T00:00:00Z', renewsAtUtc: '2027-01-01T00:00:00Z',
  duesAmount: 299, duesFrequency: 'Monthly', houseAccountBalance: 0,
};

const show = () => render(
  <MemoryRouter initialEntries={[`/events/${EVENT_ID}`]}>
    <Routes>
      <Route path="/events/:id" element={<EventDetail />} />
    </Routes>
  </MemoryRouter>,
);

describe('event attendees', () => {
  it('shows the list the rest of the team entered, from the server', async () => {
    show();

    expect(await screen.findByText('Claire Martin')).toBeInTheDocument();
    expect(screen.queryByText(/saved only in this browser/)).toBeNull();
  });

  it('offers to upload what this browser kept before, and then shows it from the server', async () => {
    localStorage.setItem('crm.mock.eventAttendees.v1:user:pat:org:org-a', JSON.stringify([KEPT_IN_THIS_BROWSER]));
    const user = userEvent.setup();
    show();

    expect(await screen.findByText(/saved only in this browser/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Upload to the server/ }));

    await waitFor(() => expect(screen.queryByText(/saved only in this browser/)).toBeNull());
    expect(screen.getByText('Amélie Roux')).toBeInTheDocument();
    expect(onServer.map((a) => a.memberName)).toEqual(['Claire Martin', 'Amélie Roux']);
  });

  it('gives no points when another door host already checked the member in', async () => {
    // This screen still shows Jean as reserved; on the server he was checked in a minute ago.
    const reserved: EventAttendee = {
      id: 'a-9', eventId: EVENT_ID, memberId: MEMBER.id, memberName: 'Jean Dupont', ticketTier: 'Member',
      status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
    };
    const checkedIn: EventAttendee = { ...reserved, status: 'CheckedIn', checkedInAtUtc: '2026-11-06T18:01:00Z' };
    let reads = 0;
    const pointsGiven: unknown[] = [];
    server.use(
      http.get(`${BASE}/api/members`, () => HttpResponse.json([MEMBER])),
      http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json([reads++ === 0 ? reserved : checkedIn])),
      http.put(`${BASE}/api/events/${EVENT_ID}/attendees/a-9`, () => HttpResponse.json(
        { title: 'Conflict', status: 409, detail: 'This person is already checked in' }, { status: 409 },
      )),
      http.post(`${BASE}/api/loyalty/ledger`, async ({ request }) => {
        pointsGiven.push(await request.json());
        return HttpResponse.json({});
      }),
    );
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: /Check in/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('This person is already checked in'));
    expect(pointsGiven).toEqual([]);
    expect(toast.success).not.toHaveBeenCalledWith(expect.stringMatching(/loyalty points/));
    // The list is read again, so the screen stops offering the check-in.
    await waitFor(() => expect(screen.queryByRole('button', { name: /Check in/ })).toBeNull());
    expect(screen.getByText(/^Checked in .*\d/)).toBeInTheDocument();
  });

  it('does not say where seats without a name came from, since another browser may still hold them', async () => {
    // Before the move, the events manager's laptop kept the names and raised the server's
    // count; until that list is uploaded, the door host sees the count and no names.
    server.use(http.get(`${BASE}/api/events`, () => HttpResponse.json([{ ...EVENT, registeredCount: 41 }])));
    show();

    expect(await screen.findByText('Claire Martin')).toBeInTheDocument();
    expect(screen.queryByText(/RSVPs? from the events list with no name/)).toBeNull();
    expect(screen.getByText(/40 seats counted with no name on this list/)).toHaveTextContent(/another browser/);
  });
});

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
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

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
});

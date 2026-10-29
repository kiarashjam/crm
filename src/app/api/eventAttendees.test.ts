// Event attendees on the server.
//
// The list was kept only in each browser, even against the real server, so the door
// host's iPad never saw the list the events manager entered on a laptop. These pin
// that the list now goes to the server, that the counts are left to it, and that a
// list a browser kept before the move can be uploaded once.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { resetLocalOnly } from './localOnly';
import type { ClubEvent } from './events';
import {
  addEventAttendee, getEventAttendees, localAttendeesToUpload, setAttendeeStatus, uploadLocalAttendees,
  type EventAttendee,
} from './eventAttendees';

const BASE = 'http://api.test';
const EVENT_ID = '7f3c2a10-0000-4000-8000-000000000001';
const EVENT: ClubEvent = {
  id: EVENT_ID, name: 'Wine night', startAtUtc: '2026-11-06T18:00:00Z', endAtUtc: '2026-11-06T21:00:00Z',
  capacity: 40, registeredCount: 1, waitlistCount: 0, memberPrice: 35, guestPrice: 55,
  visibility: 'MembersAndGuests', status: 'Published', coverEmoji: '🍷',
};
const ON_SERVER: EventAttendee = {
  id: 'a-1', eventId: EVENT_ID, memberName: 'Claire Martin', ticketTier: 'Guest', status: 'Reserved', rsvpAtUtc: '2026-10-01T18:00:00Z',
};
/** Where this browser kept the list before, for this user and workspace. */
const LOCAL_KEY = 'crm.mock.eventAttendees.v1:user:pat:org:org-a';

let requests: string[];

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

describe('event attendees', () => {
  it('reads the list the whole team sees from the server, not from this browser', async () => {
    server.use(http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => HttpResponse.json([ON_SERVER])));

    expect(await getEventAttendees(EVENT_ID)).toEqual([ON_SERVER]);
    expect(Object.keys(localStorage).some((k) => k.startsWith('crm.mock.eventAttendees'))).toBe(false);
  });

  it('adds someone on the server and leaves the counts to it', async () => {
    let body: unknown;
    server.use(http.post(`${BASE}/api/events/${EVENT_ID}/attendees`, async ({ request }) => {
      body = await request.json();
      return HttpResponse.json({ ...ON_SERVER, id: 'a-2', status: 'Waitlisted' });
    }));

    const added = await addEventAttendee(EVENT, { memberName: 'Bruno', ticketTier: 'Guest' });

    expect(added.status).toBe('Waitlisted');
    expect(body).toEqual({ memberName: 'Bruno', ticketTier: 'Guest' });
    // The page used to PUT "the count I last read, plus one" to the event.
    expect(requests).toEqual([`POST /api/events/${EVENT_ID}/attendees`]);
  });

  it('uploads a list this browser kept before the move, once, as it was', async () => {
    const kept: EventAttendee = {
      id: 'att-1730000000000-ab12cd', eventId: EVENT_ID, memberName: 'Amélie Roux', ticketTier: 'Guest',
      status: 'CheckedIn', rsvpAtUtc: '2026-10-01T18:00:00Z', checkedInAtUtc: '2026-10-03T19:05:00Z',
    };
    const otherEvent = { ...kept, id: 'att-2', eventId: 'another-event' };
    localStorage.setItem(LOCAL_KEY, JSON.stringify([kept, otherEvent]));
    const sent: unknown[] = [];
    server.use(http.post(`${BASE}/api/events/${EVENT_ID}/attendees/import`, async ({ request }) => {
      sent.push(await request.json());
      return HttpResponse.json({ ...kept, id: 'server-1' });
    }));

    const rows = localAttendeesToUpload(EVENT_ID);
    expect(rows.map((r) => r.id)).toEqual([kept.id]);

    expect(await uploadLocalAttendees(EVENT_ID, rows)).toEqual({ uploaded: 1, failed: 0 });
    expect(sent).toEqual([expect.objectContaining({ localId: kept.id, status: 'CheckedIn', checkedInAtUtc: kept.checkedInAtUtc })]);
    expect(localAttendeesToUpload(EVENT_ID)).toEqual([]);
    // The other event's row stays, and the original list is kept aside.
    expect(JSON.parse(localStorage.getItem(LOCAL_KEY)!)).toEqual([otherEvent]);
    expect(JSON.parse(localStorage.getItem(`${LOCAL_KEY}:upload-backup`)!)).toHaveLength(2);
  });

  it('keeps a row in the browser when the server does not take it', async () => {
    const kept: EventAttendee = { ...ON_SERVER, id: 'att-local-1' };
    localStorage.setItem(LOCAL_KEY, JSON.stringify([kept]));
    server.use(http.post(`${BASE}/api/events/${EVENT_ID}/attendees/import`, () =>
      HttpResponse.json({ title: 'Server Error', status: 500 }, { status: 500 })));

    expect(await uploadLocalAttendees(EVENT_ID, [kept])).toEqual({ uploaded: 0, failed: 1 });
    expect(localAttendeesToUpload(EVENT_ID)).toHaveLength(1);
  });

  it('on a server without the route, keeps the list here and does not offer an upload', async () => {
    const kept: EventAttendee = { ...ON_SERVER, id: 'att-local-1' };
    localStorage.setItem(LOCAL_KEY, JSON.stringify([kept]));
    server.use(http.get(`${BASE}/api/events/${EVENT_ID}/attendees`, () => new HttpResponse(null, { status: 404 })));

    expect(await getEventAttendees(EVENT_ID)).toEqual([kept]);
    expect(localAttendeesToUpload(EVENT_ID)).toEqual([]);
  });

  it('on a server without the route, still refuses to check someone in twice', async () => {
    // The page gives a member event points for each check-in that succeeds.
    const kept: EventAttendee = { ...ON_SERVER, id: 'att-local-1', status: 'CheckedIn', checkedInAtUtc: '2026-11-06T18:01:00Z' };
    localStorage.setItem(LOCAL_KEY, JSON.stringify([kept]));
    server.use(http.put(`${BASE}/api/events/${EVENT_ID}/attendees/att-local-1`, () => new HttpResponse(null, { status: 404 })));

    await expect(setAttendeeStatus(EVENT, { ...kept, status: 'Reserved' }, 'CheckedIn')).rejects.toThrow(/already checked in/);
    expect(JSON.parse(localStorage.getItem(LOCAL_KEY)!)[0].checkedInAtUtc).toBe(kept.checkedInAtUtc);
  });
});

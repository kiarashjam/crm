// Who is coming to an event, and the door.
//
// The list used to be kept only in this browser, even against the real server (see
// `scopedStorageKey`): the events manager registered forty members on a laptop and the door
// host, on an iPad, saw nobody to check in. Then the rules ran in the page. The server keeps
// all of it now, at /api/events/{id}/attendees: it decides a seat or the waitlist against the
// capacity (two people taking the last seat get one seat and one place in line), posts each
// ticket to a house account through the ledger, gives the next in line a seat when someone
// cancels and emails them, gives a member's check-in points once, and posts no-show fees.
//
// The browser store stays for the demo. A server too old to have the route answers with an
// error, as any call does: the list is never kept in this browser instead. Lists a browser
// kept before the move can be uploaded once from the event page (`uploadLocalAttendees`).

import { apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { isLocalOnly } from './localOnly';
import { createMockStore, mockId, scopedStorageKey } from './mockStore';
import { updateEvent, type ClubEvent } from './events';

export type AttendeeStatus = 'Reserved' | 'CheckedIn' | 'Cancelled' | 'Waitlisted' | 'NoShow';
export type TicketTier = 'Member' | 'Guest';

/**
 * A ticket on the ledger: free; charged (on a house account); reversed (taken back by a
 * cancellation); waiting (charged when a seat comes); to-collect (a guest nobody brings, so no
 * house account to put it on); not-charged (registered before tickets were charged); none.
 */
export type ChargeState = 'free' | 'charged' | 'reversed' | 'waiting' | 'to-collect' | 'not-charged' | 'none';

export interface EventAttendee {
  id: string;
  eventId: string;
  memberId?: string;
  memberName: string;
  email?: string;
  ticketTier: TicketTier;
  status: AttendeeStatus;
  rsvpAtUtc: string;
  checkedInAtUtc?: string;
  /** For a guest: the member who brings them, whose account the guest ticket goes on. */
  hostMemberId?: string;
  hostName?: string;
  source?: 'Registered' | 'WalkIn' | 'Moved' | 'Uploaded';
  walkIn?: boolean;
  /** Their place in line, 1 first; only while on the waitlist. */
  waitlistPosition?: number;
  promotedAtUtc?: string;
  checkedInByName?: string;
  noShowAtUtc?: string;
  cancelledAtUtc?: string;
  cancelReason?: string;
  ticketPrice?: number;
  currency?: string;
  chargeState?: ChargeState;
  chargeId?: string;
  noShowFee?: number;
  noShowChargeId?: string;
  noShowFeeState?: 'charged' | 'reversed';
  pointsAwarded?: number;
  /** On a member's own list of registrations. */
  eventName?: string;
  eventStartAtUtc?: string;
}

export interface AttendeeInput {
  memberId?: string;
  memberName: string;
  email?: string;
  ticketTier: TicketTier;
  /** For a guest: the member who brings them. */
  hostMemberId?: string;
}

const STORAGE_KEY = 'crm.mock.eventAttendees.v1';
const day = 86_400_000;

const seed: EventAttendee[] = [
  {
    id: 'att-seed-1',
    eventId: 'evt-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    email: 'amelia.hartwell@example.com',
    ticketTier: 'Member',
    status: 'Reserved',
    rsvpAtUtc: new Date(Date.now() - 4 * day).toISOString(),
  },
  {
    id: 'att-seed-2',
    eventId: 'evt-seed-1',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    email: 'daniel.okafor@example.com',
    ticketTier: 'Member',
    status: 'Reserved',
    rsvpAtUtc: new Date(Date.now() - 3 * day).toISOString(),
  },
  {
    id: 'att-seed-3',
    eventId: 'evt-seed-1',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    email: 'priya.raman@example.com',
    ticketTier: 'Member',
    status: 'Reserved',
    rsvpAtUtc: new Date(Date.now() - 2 * day).toISOString(),
  },
  {
    id: 'att-seed-4',
    eventId: 'evt-seed-2',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    email: 'amelia.hartwell@example.com',
    ticketTier: 'Member',
    status: 'Reserved',
    rsvpAtUtc: new Date(Date.now() - 10 * day).toISOString(),
  },
  {
    id: 'att-seed-5',
    eventId: 'evt-seed-2',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    email: 'jordan.patel@example.com',
    ticketTier: 'Member',
    status: 'Waitlisted',
    rsvpAtUtc: new Date(Date.now() - 5 * day).toISOString(),
  },
  {
    id: 'att-seed-6',
    eventId: 'evt-seed-5',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    ticketTier: 'Member',
    status: 'CheckedIn',
    rsvpAtUtc: new Date(Date.now() - 12 * day).toISOString(),
    checkedInAtUtc: new Date(Date.now() - 6 * day).toISOString(),
  },
];

const attendeeStore = createMockStore<EventAttendee>({
  storageKey: STORAGE_KEY,
  seed,
  idOf: (a) => a.id,
});

const isRegistered = (status: AttendeeStatus) => status === 'Reserved' || status === 'CheckedIn';

function localList(eventId: string): EventAttendee[] {
  return attendeeStore
    .list()
    .filter((a) => a.eventId === eventId)
    .sort((a, b) => Date.parse(a.rsvpAtUtc) - Date.parse(b.rsvpAtUtc));
}

/** The demo's way of keeping the event's counts, as the page did before the server kept them. */
async function moveLocalCounts(event: ClubEvent, from: AttendeeStatus | null, to: AttendeeStatus | null): Promise<void> {
  const registered = event.registeredCount + (to && isRegistered(to) ? 1 : 0) - (from && isRegistered(from) ? 1 : 0);
  const waitlist = event.waitlistCount + (to === 'Waitlisted' ? 1 : 0) - (from === 'Waitlisted' ? 1 : 0);
  const patch: { registeredCount?: number; waitlistCount?: number } = {};
  if (registered !== event.registeredCount) patch.registeredCount = Math.max(0, registered);
  if (waitlist !== event.waitlistCount) patch.waitlistCount = Math.max(0, waitlist);
  if (Object.keys(patch).length) await updateEvent(event.id, patch);
}

export async function getEventAttendees(eventId: string, req?: RequestOptions): Promise<EventAttendee[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<EventAttendee[]>(`/api/events/${eventId}/attendees`, { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => localList(eventId),
  );
}

/** A member's registrations across events: on their own ticket, or bringing a guest. */
export async function getMemberRegistrations(memberId: string, req?: RequestOptions): Promise<EventAttendee[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<EventAttendee[]>(`/api/events/registrations?memberId=${encodeURIComponent(memberId)}`, { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => attendeeStore.list().filter((a) => a.memberId === memberId || a.hostMemberId === memberId),
  );
}

/**
 * Puts a member or a guest on the list. The server gives a seat while there is one, else the
 * waitlist, and posts the ticket of a seat with a price.
 */
export async function addEventAttendee(event: ClubEvent, input: AttendeeInput): Promise<EventAttendee> {
  return apiWithFallback(
    () => authFetchJson<EventAttendee>(`/api/events/${event.id}/attendees`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
    async () => {
      // A capacity of 0 is no limit, as on the server.
      const reserved = localList(event.id).filter((a) => isRegistered(a.status)).length;
      const status: AttendeeStatus = event.capacity > 0 && reserved >= event.capacity ? 'Waitlisted' : 'Reserved';
      const row = attendeeStore.add({
        id: mockId('att'),
        eventId: event.id,
        ...input,
        status,
        rsvpAtUtc: new Date().toISOString(),
      });
      await moveLocalCounts(event, null, status);
      return row;
    },
  );
}

/** Someone at the door with no registration: checked in at once on a free seat, refused when the event is full. */
export async function addWalkIn(event: ClubEvent, input: AttendeeInput): Promise<EventAttendee> {
  return apiWithFallback(
    () => authFetchJson<EventAttendee>(`/api/events/${event.id}/attendees/walk-in`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
    async () => {
      const reserved = localList(event.id).filter((a) => isRegistered(a.status)).length;
      if (event.capacity > 0 && reserved >= event.capacity) throw new Error(`The event is full: all ${event.capacity} seats are taken.`);
      const now = new Date().toISOString();
      const row = attendeeStore.add({
        id: mockId('att'),
        eventId: event.id,
        ...input,
        status: 'CheckedIn',
        source: 'WalkIn',
        walkIn: true,
        rsvpAtUtc: now,
        checkedInAtUtc: now,
      });
      await moveLocalCounts(event, null, 'CheckedIn');
      return row;
    },
  );
}

/**
 * Check in, cancel, take off the waitlist (Reserved) or mark a no-show. The server refuses a
 * seat while the event is full and a check-in that already happened; a cancellation gives
 * the seat to the first in line, who is emailed.
 */
export async function setAttendeeStatus(
  event: ClubEvent,
  attendee: EventAttendee,
  status: AttendeeStatus,
  reason?: string,
): Promise<EventAttendee | null> {
  return apiWithFallback(
    () => authFetchJson<EventAttendee>(`/api/events/${event.id}/attendees/${attendee.id}`, {
      method: 'PUT',
      body: JSON.stringify({ status, reason }),
    }),
    async () => {
      // As on the server: a check-in is refused when it already happened.
      if (status === 'CheckedIn' && attendeeStore.byId(attendee.id)?.status === 'CheckedIn') {
        throw new Error('This person is already checked in.');
      }
      const row = attendeeStore.update(attendee.id, {
        status,
        checkedInAtUtc: status === 'CheckedIn' ? new Date().toISOString() : undefined,
      });
      await moveLocalCounts(event, attendee.status, status);
      return row;
    },
  );
}

export async function removeEventAttendee(event: ClubEvent, attendee: EventAttendee): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/events/${event.id}/attendees/${attendee.id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      const removed = attendeeStore.remove(attendee.id);
      if (removed) await moveLocalCounts(event, attendee.status, null);
      return removed;
    },
  );
}

/** Where a browser's list is copied before an upload removes anything from it. */
export const ATTENDEE_UPLOAD_BACKUP_SUFFIX = ':upload-backup';

/**
 * Attendees this browser kept for the event before the list moved to the server.
 * Empty with demo data, and while the server has no attendee route, since the
 * browser's list is then still the one in use.
 */
export function localAttendeesToUpload(eventId: string): EventAttendee[] {
  if (servesDemoData() || isLocalOnly(['events/attendees'])) return [];
  return localList(eventId);
}

export interface AttendeeUploadResult {
  uploaded: number;
  failed: number;
}

/**
 * Sends the browser's list to the server as it is: statuses and check-in times kept, nothing
 * charged, and each one taking one of the event's unnamed seats, since the browser counted it
 * on the server when it was entered. Each row leaves this browser only once the server has
 * it; a copy of the whole list is kept under a backup key first.
 */
export async function uploadLocalAttendees(eventId: string, rows: EventAttendee[]): Promise<AttendeeUploadResult> {
  const key = scopedStorageKey(STORAGE_KEY);
  try {
    const backup = key + ATTENDEE_UPLOAD_BACKUP_SUFFIX;
    const current = localStorage.getItem(key);
    if (current && !localStorage.getItem(backup)) localStorage.setItem(backup, current);
  } catch {
    // Without storage there is nothing to back up, or to lose.
  }
  let uploaded = 0;
  let failed = 0;
  for (const row of rows) {
    try {
      await authFetchJson<EventAttendee>(`/api/events/${eventId}/attendees/import`, {
        method: 'POST',
        body: JSON.stringify({
          localId: row.id,
          memberId: row.memberId,
          memberName: row.memberName,
          email: row.email,
          ticketTier: row.ticketTier,
          status: row.status,
          rsvpAtUtc: row.rsvpAtUtc,
          checkedInAtUtc: row.checkedInAtUtc,
        }),
      });
      attendeeStore.remove(row.id);
      uploaded++;
    } catch {
      failed++;
    }
  }
  return { uploaded, failed };
}

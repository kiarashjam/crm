// Who is coming to an event.
//
// The list used to be kept only in this browser, even against the real server (see
// `scopedStorageKey`): the events manager registered forty members on a laptop and
// the door host, on an iPad, saw nobody to check in. It lives on the server now, at
// /api/events/{id}/attendees, which also keeps the event's registered and waitlist
// counts and decides reserved or waitlisted against the capacity.
//
// The browser store stays for the demo, and for a server too old to have the route
// (`apiWithFallback` then saves here and the page says so). Lists a browser kept
// before the move can be uploaded once from the event page (`uploadLocalAttendees`).

import { apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData } from './apiClient';
import { isLocalOnly } from './localOnly';
import { createMockStore, mockId, scopedStorageKey } from './mockStore';
import { updateEvent, type ClubEvent } from './events';

export type AttendeeStatus = 'Reserved' | 'CheckedIn' | 'Cancelled' | 'Waitlisted';
export type TicketTier = 'Member' | 'Guest';

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
}

export interface AttendeeInput {
  memberId?: string;
  memberName: string;
  email?: string;
  ticketTier: TicketTier;
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

/** The browser's way of keeping the event's counts, as the page did before the server kept them. */
async function moveLocalCounts(event: ClubEvent, from: AttendeeStatus | null, to: AttendeeStatus | null): Promise<void> {
  const registered = event.registeredCount + (to && isRegistered(to) ? 1 : 0) - (from && isRegistered(from) ? 1 : 0);
  const waitlist = event.waitlistCount + (to === 'Waitlisted' ? 1 : 0) - (from === 'Waitlisted' ? 1 : 0);
  const patch: { registeredCount?: number; waitlistCount?: number } = {};
  if (registered !== event.registeredCount) patch.registeredCount = Math.max(0, registered);
  if (waitlist !== event.waitlistCount) patch.waitlistCount = Math.max(0, waitlist);
  if (Object.keys(patch).length) await updateEvent(event.id, patch);
}

export async function getEventAttendees(eventId: string): Promise<EventAttendee[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<EventAttendee[]>(`/api/events/${eventId}/attendees`);
      return Array.isArray(res) ? res : [];
    },
    async () => localList(eventId),
  );
}

/** Adds someone to the list. The server puts them on the waitlist when the event is full. */
export async function addEventAttendee(event: ClubEvent, input: AttendeeInput): Promise<EventAttendee> {
  return apiWithFallback(
    () => authFetchJson<EventAttendee>(`/api/events/${event.id}/attendees`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),
    async () => {
      // A capacity of 0 is no limit, as on the server; this used to waitlist everyone.
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

/**
 * Check in, cancel, or move off the waitlist. The server refuses a promotion while the
 * event is full, and a check-in of someone already checked in.
 */
export async function setAttendeeStatus(
  event: ClubEvent,
  attendee: EventAttendee,
  status: AttendeeStatus,
): Promise<EventAttendee | null> {
  return apiWithFallback(
    () => authFetchJson<EventAttendee>(`/api/events/${event.id}/attendees/${attendee.id}`, {
      method: 'PUT',
      body: JSON.stringify({ status }),
    }),
    async () => {
      // As on the server: a check-in is refused when it already happened, so the page
      // does not give the member their event points twice.
      if (status === 'CheckedIn' && attendeeStore.byId(attendee.id)?.status === 'CheckedIn') {
        throw new Error('This person is already checked in');
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
 * Sends the browser's list to the server as it is: statuses and check-in times kept,
 * and not counted again, because the browser already added each one to the event's
 * counts on the server when it was entered. Each row leaves this browser only once
 * the server has it; a copy of the whole list is kept under a backup key first.
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

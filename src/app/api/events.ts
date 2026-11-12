// Events & ticketing.
//
// An `Event` is a scheduled occurrence (dinner, mixer, class, party) members can register
// for. The server keeps who is coming (see eventAttendees.ts): the capacity and the
// waitlist, each ticket on a house account through the ledger, and the door. The counts
// on an event are the server's, worked out from its list. Demo mode keeps a mock store so
// the screens work offline.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type EventStatus = 'Draft' | 'Published' | 'Cancelled' | 'Completed';
export type EventVisibility = 'MembersOnly' | 'MembersAndGuests' | 'Public';

export interface ClubEvent {
  id: string;
  name: string;
  description?: string;
  location?: string;
  startAtUtc: string;
  endAtUtc: string;
  capacity: number;
  registeredCount: number;
  waitlistCount: number;
  memberPrice: number;
  guestPrice: number;
  visibility: EventVisibility;
  status: EventStatus;
  coverEmoji: string;
  /** The server's: what its prices are in. */
  currency?: string;
  /** Posted to a member's account when they are marked a no-show; 0 is none. */
  noShowFee?: number;
  checkedInCount?: number;
  noShowCount?: number;
  walkInCount?: number;
  /** Seats the old events list counted for RSVPs with no name: they take seats until released. */
  unnamedSeats?: number;
  unnamedWaitlist?: number;
  /** Seats still free; left out when there is no limit. */
  seatsLeft?: number;
  /** What the tickets on house accounts come to, posted and not taken back. */
  ticketRevenue?: number;
  /** The version to edit the details from; a registration does not change it. */
  version?: string;
}

export const EVENT_STATUSES: EventStatus[] = ['Draft', 'Published', 'Cancelled', 'Completed'];
export const EVENT_VISIBILITIES: EventVisibility[] = ['MembersOnly', 'MembersAndGuests', 'Public'];

export const VISIBILITY_LABELS: Record<EventVisibility, string> = {
  MembersOnly: 'Members only',
  MembersAndGuests: 'Members + guests',
  Public: 'Public',
};

const day = 86_400_000;
const hour = 3_600_000;

const seed: ClubEvent[] = [
  {
    id: 'evt-seed-1',
    name: 'Wine Down Wednesday',
    description: 'A curated tasting of small-production wines from the Loire Valley with the sommelier.',
    location: 'Library Bar',
    startAtUtc: new Date(Date.now() + 3 * day + 19 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 3 * day + 22 * hour).toISOString(),
    capacity: 40,
    registeredCount: 28,
    waitlistCount: 0,
    memberPrice: 35,
    guestPrice: 55,
    visibility: 'MembersAndGuests',
    status: 'Published',
    coverEmoji: '🍷',
  },
  {
    id: 'evt-seed-2',
    name: 'Chef Series: Tokyo Nights',
    description: 'Six-course tasting menu by guest chef Sho Yamamoto. Pairings available.',
    location: 'Private Dining Room',
    startAtUtc: new Date(Date.now() + 9 * day + 20 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 9 * day + 23 * hour).toISOString(),
    capacity: 24,
    registeredCount: 24,
    waitlistCount: 7,
    memberPrice: 185,
    guestPrice: 245,
    visibility: 'MembersOnly',
    status: 'Published',
    coverEmoji: '🍣',
  },
  {
    id: 'evt-seed-3',
    name: 'Rooftop Yoga & Mimosas',
    description: 'Vinyasa flow followed by brunch on the rooftop.',
    location: 'Rooftop Terrace',
    startAtUtc: new Date(Date.now() + 6 * day + 9 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 6 * day + 11 * hour).toISOString(),
    capacity: 30,
    registeredCount: 12,
    waitlistCount: 0,
    memberPrice: 25,
    guestPrice: 40,
    visibility: 'MembersAndGuests',
    status: 'Published',
    coverEmoji: '🧘',
  },
  {
    id: 'evt-seed-4',
    name: 'Founders Anniversary Gala',
    description: 'Black-tie evening celebrating five years of the club. Dinner, dancing, surprises.',
    location: 'Grand Ballroom',
    startAtUtc: new Date(Date.now() + 28 * day + 19 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 28 * day + 26 * hour).toISOString(),
    capacity: 200,
    registeredCount: 0,
    waitlistCount: 0,
    memberPrice: 0,
    guestPrice: 175,
    visibility: 'MembersAndGuests',
    status: 'Draft',
    coverEmoji: '🎉',
  },
  {
    id: 'evt-seed-5',
    name: 'Cigar & Single Malt Night',
    description: 'Paired tasting featuring Highland whiskies and curated cigars.',
    location: 'Cigar Lounge',
    startAtUtc: new Date(Date.now() - 6 * day + 20 * hour).toISOString(),
    endAtUtc: new Date(Date.now() - 6 * day + 23 * hour).toISOString(),
    capacity: 20,
    registeredCount: 20,
    waitlistCount: 3,
    memberPrice: 95,
    guestPrice: 135,
    visibility: 'MembersOnly',
    status: 'Completed',
    coverEmoji: '🥃',
  },
];

const eventStore = createMockStore<ClubEvent>({
  storageKey: 'crm.mock.events.v1',
  seed,
  idOf: (e) => e.id,
});

export async function getEvents(req?: RequestOptions): Promise<ClubEvent[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<ClubEvent[]>('/api/events', { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...eventStore.list()].sort(
        (a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc),
      );
    },
  );
}

/** One event, as the server keeps it now. Null when it is not there. */
export async function getEvent(id: string, req?: RequestOptions): Promise<ClubEvent | null> {
  return apiWithFallback(
    () => authFetchJson<ClubEvent>(`/api/events/${id}`, { signal: req?.signal }),
    async () => {
      await delay(80);
      return eventStore.byId(id) ?? null;
    },
  );
}

/**
 * The fields an event is made or edited with. Text sent empty clears it on the server; the
 * counts are the server's own, and unnamed seats can only be lowered (released).
 */
export type EventInput = Omit<ClubEvent, 'id' | 'registeredCount' | 'waitlistCount' | 'version' | 'currency' | 'checkedInCount'
  | 'noShowCount' | 'walkInCount' | 'seatsLeft' | 'ticketRevenue'> & {
  registeredCount?: number;
  waitlistCount?: number;
  /** The version the edit was made from: an edit from an older copy is refused (409). */
  version?: string;
};

export async function createEvent(input: EventInput): Promise<ClubEvent | null> {
  return apiWithFallback(
    () => authFetchJson<ClubEvent>('/api/events', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return eventStore.add({
        id: mockId('evt'),
        registeredCount: 0,
        waitlistCount: 0,
        ...input,
      });
    },
  );
}

export async function updateEvent(id: string, patch: Partial<EventInput>): Promise<ClubEvent | null> {
  return apiWithFallback(
    () => authFetchJson<ClubEvent>(`/api/events/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      return eventStore.update(id, patch);
    },
  );
}

export async function deleteEvent(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/events/${id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(100);
      return eventStore.remove(id);
    },
  );
}

export interface EventStats {
  upcoming: number;
  draftCount: number;
  totalRsvps: number;
  totalWaitlist: number;
  projectedRevenue: number;
}

export function computeEventStats(events: ClubEvent[]): EventStats {
  const now = Date.now();
  let upcoming = 0;
  let draftCount = 0;
  let totalRsvps = 0;
  let totalWaitlist = 0;
  let projectedRevenue = 0;
  for (const e of events) {
    if (e.status === 'Draft') draftCount++;
    if (e.status === 'Published' && Date.parse(e.startAtUtc) > now) upcoming++;
    totalRsvps += e.registeredCount;
    totalWaitlist += e.waitlistCount;
    // The server says what the tickets on house accounts come to; the demo's events, which
    // have no tickets, are estimated from the seats taken.
    if (e.ticketRevenue !== undefined) projectedRevenue += e.ticketRevenue;
    else if (e.status === 'Published' || e.status === 'Completed') projectedRevenue += e.registeredCount * e.memberPrice;
  }
  return {
    upcoming,
    draftCount,
    totalRsvps,
    totalWaitlist,
    projectedRevenue: Math.round(projectedRevenue * 100) / 100,
  };
}

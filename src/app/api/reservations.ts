// Reservations / bookings.
//
// Members book a resource (dining room, meeting room, spa, court) for a time. The server keeps
// the rules: a resource booked one at a time (Exclusive) takes one booking at a time, a shared
// one takes bookings until the people in them reach its capacity, and a clash is refused with
// a 409 that names the booking in the way. A booking is for a member by id (their name and
// email come from their record) or for a guest by name. A resource can ask a deposit, charged
// to the member's house account, paid at the desk or waived with a reason; it is given back
// when the booking is kept or cancelled and kept on a no-show, which also posts the resource's
// no-show fee. Demo mode keeps mock stores so the screens work offline.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type ResourceKind = 'Dining' | 'Meeting' | 'Spa' | 'Court' | 'Studio' | 'Lounge';
export type BookingMode = 'Exclusive' | 'Shared';
export type ReservationStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'CheckedIn' | 'Completed' | 'NoShow';

/**
 * none (no deposit asked); charged (on the member's account, not yet paid); paid; waived;
 * released (given back: the booking was kept or cancelled); kept (the member did not come).
 */
export type DepositState = 'none' | 'charged' | 'paid' | 'waived' | 'released' | 'kept';

export interface Resource {
  id: string;
  name: string;
  kind: ResourceKind;
  capacity: number;
  description?: string;
  /** Exclusive: one booking at a time. Shared: bookings up to the capacity in people. */
  bookingMode?: BookingMode;
  /** Asked of a member when they book; 0 is none. */
  depositAmount?: number;
  /** Posted when a booking is marked a no-show; 0 is none. */
  noShowFee?: number;
  currency?: string;
  /** Whether it takes bookings. One with bookings is made not bookable rather than deleted. */
  active?: boolean;
  sortOrder?: number;
  /** The version to edit the settings from; a booking does not change it. */
  version?: string;
}

export interface Reservation {
  id: string;
  resourceId: string;
  resourceName: string;
  /** The member it is for; none for a guest booked by name. */
  memberId?: string;
  memberName: string;
  memberEmail?: string;
  partySize: number;
  startAtUtc: string;
  endAtUtc: string;
  status: ReservationStatus;
  notes?: string;
  currency?: string;
  depositAmount?: number;
  depositState?: DepositState;
  depositChargeId?: string;
  depositWaivedReason?: string;
  noShowFee?: number;
  noShowChargeId?: string;
  noShowFeeState?: 'charged' | 'reversed';
  createdByName?: string;
  version?: string;
}

export const RESERVATION_STATUSES: ReservationStatus[] = [
  'Pending',
  'Confirmed',
  'Cancelled',
  'CheckedIn',
  'Completed',
  'NoShow',
];

export const RESOURCE_KINDS: ResourceKind[] = ['Dining', 'Meeting', 'Spa', 'Court', 'Studio', 'Lounge'];
export const BOOKING_MODES: BookingMode[] = ['Exclusive', 'Shared'];

/** Where a booking can go from where it is, as the server allows it. */
export const NEXT_STATUSES: Record<ReservationStatus, ReservationStatus[]> = {
  Pending: ['Confirmed', 'CheckedIn', 'Completed', 'Cancelled', 'NoShow'],
  Confirmed: ['Pending', 'CheckedIn', 'Completed', 'Cancelled', 'NoShow'],
  CheckedIn: ['Confirmed', 'Completed'],
  NoShow: ['CheckedIn'],
  Completed: [],
  Cancelled: [],
};

const day = 86_400_000;
const hour = 3_600_000;

const resourceSeed: Resource[] = [
  { id: 'res-dr', name: 'Main Dining Room', kind: 'Dining', capacity: 80, description: 'Open kitchen, seasonal tasting menu.', bookingMode: 'Shared', depositAmount: 0, noShowFee: 0, active: true },
  { id: 'res-pdr', name: 'Private Dining Room', kind: 'Dining', capacity: 16, description: 'Bookable for 8–16; chef can customize menu.', bookingMode: 'Exclusive', depositAmount: 200, noShowFee: 0, active: true },
  { id: 'res-ca', name: 'Conference Room A', kind: 'Meeting', capacity: 12, description: 'AV, whiteboard, video conference.', bookingMode: 'Exclusive', depositAmount: 0, noShowFee: 0, active: true },
  { id: 'res-cb', name: 'Conference Room B', kind: 'Meeting', capacity: 6, description: 'Quiet huddle room with display.', bookingMode: 'Exclusive', depositAmount: 0, noShowFee: 0, active: true },
  { id: 'res-spa', name: 'Spa Treatment Room', kind: 'Spa', capacity: 1, description: '60 / 90 minute massage, facial, or sauna.', bookingMode: 'Exclusive', depositAmount: 0, noShowFee: 50, active: true },
  { id: 'res-court', name: 'Tennis Court', kind: 'Court', capacity: 4, description: 'Hard court; book in 60 minute blocks.', bookingMode: 'Exclusive', depositAmount: 0, noShowFee: 0, active: true },
  { id: 'res-studio', name: 'Fitness Studio', kind: 'Studio', capacity: 20, description: 'Yoga, pilates, group classes.', bookingMode: 'Shared', depositAmount: 0, noShowFee: 0, active: true },
  { id: 'res-rooftop', name: 'Rooftop Lounge', kind: 'Lounge', capacity: 40, description: 'Open Apr–Oct; sunset views.', bookingMode: 'Shared', depositAmount: 0, noShowFee: 0, active: true },
];

const resourceStore = createMockStore<Resource>({
  storageKey: 'crm.mock.resources.v1',
  seed: resourceSeed,
  idOf: (r) => r.id,
});

const reservationSeed: Reservation[] = [
  {
    id: 'rsv-seed-1',
    resourceId: 'res-dr',
    resourceName: 'Main Dining Room',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    memberEmail: 'amelia.hartwell@example.com',
    partySize: 4,
    startAtUtc: new Date(Date.now() + 1 * day + 20 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 1 * day + 22 * hour).toISOString(),
    status: 'Confirmed',
    notes: 'Anniversary — bring out the cake at 21:30.',
  },
  {
    id: 'rsv-seed-2',
    resourceId: 'res-ca',
    resourceName: 'Conference Room A',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    memberEmail: 'daniel.okafor@example.com',
    partySize: 8,
    startAtUtc: new Date(Date.now() + 0 * day + 14 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 0 * day + 16 * hour).toISOString(),
    status: 'CheckedIn',
  },
  {
    id: 'rsv-seed-3',
    resourceId: 'res-spa',
    resourceName: 'Spa Treatment Room',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    memberEmail: 'priya.raman@example.com',
    partySize: 1,
    startAtUtc: new Date(Date.now() + 2 * day + 11 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 2 * day + 12 * hour).toISOString(),
    status: 'Confirmed',
    notes: '90 minute deep tissue.',
  },
  {
    id: 'rsv-seed-4',
    resourceId: 'res-court',
    resourceName: 'Tennis Court',
    memberName: 'Jordan Patel',
    partySize: 2,
    startAtUtc: new Date(Date.now() - 1 * day + 8 * hour).toISOString(),
    endAtUtc: new Date(Date.now() - 1 * day + 9 * hour).toISOString(),
    status: 'Completed',
  },
  {
    id: 'rsv-seed-5',
    resourceId: 'res-pdr',
    resourceName: 'Private Dining Room',
    memberName: 'Marcus Lindgren',
    memberEmail: 'marcus.lindgren@example.com',
    partySize: 12,
    startAtUtc: new Date(Date.now() + 5 * day + 19 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 5 * day + 22 * hour).toISOString(),
    status: 'Pending',
    notes: 'Awaiting deposit confirmation.',
  },
  {
    id: 'rsv-seed-6',
    resourceId: 'res-studio',
    resourceName: 'Fitness Studio',
    memberName: 'Sofia Reyes',
    partySize: 1,
    startAtUtc: new Date(Date.now() + 0 * day + 18 * hour).toISOString(),
    endAtUtc: new Date(Date.now() + 0 * day + 19 * hour).toISOString(),
    status: 'Cancelled',
  },
];

const reservationStore = createMockStore<Reservation>({
  storageKey: 'crm.mock.reservations.v1',
  seed: reservationSeed,
  idOf: (r) => r.id,
});

// ── resources ────────────────────────────────────────────────────────────────

export async function getResources(req?: RequestOptions): Promise<Resource[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Resource[]>('/api/resources', { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...resourceStore.list()].sort((a, b) => a.name.localeCompare(b.name));
    },
  );
}

export type ResourceInput = Omit<Resource, 'id' | 'currency' | 'version'>;

export async function createResource(input: ResourceInput): Promise<Resource> {
  return apiWithFallback(
    () => authFetchJson<Resource>('/api/resources', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(100);
      return resourceStore.add({ id: mockId('res'), active: true, ...input });
    },
  );
}

/** Changes the fields sent; `version` is the copy the edit was made from (an older one is refused). */
export async function updateResource(id: string, patch: Partial<ResourceInput> & { version?: string }): Promise<Resource | null> {
  return apiWithFallback(
    () => authFetchJson<Resource>(`/api/resources/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(100);
      const { version: _version, ...rest } = patch;
      return resourceStore.update(id, rest);
    },
  );
}

export async function deleteResource(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/resources/${id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => resourceStore.remove(id),
  );
}

/** The usual spaces of a club, for a workspace that has none yet. */
export async function addUsualResources(): Promise<Resource[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Resource[]>('/api/resources/defaults', { method: 'POST' });
      return Array.isArray(res) ? res : [];
    },
    async () => getResources(),
  );
}

// ── bookings ─────────────────────────────────────────────────────────────────

/** Bookings, soonest first: the club's, or one member's. */
export async function getReservations(req?: RequestOptions & { memberId?: string }): Promise<Reservation[]> {
  const query = req?.memberId ? `?memberId=${encodeURIComponent(req.memberId)}` : '';
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Reservation[]>(`/api/reservations${query}`, { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...reservationStore.list()]
        .filter((r) => !req?.memberId || r.memberId === req.memberId)
        .sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc));
    },
  );
}

export interface ReservationInput {
  resourceId: string;
  /** The member it is for; leave it out and give `memberName` for a guest. */
  memberId?: string;
  memberName?: string;
  memberEmail?: string;
  partySize: number;
  startAtUtc: string;
  endAtUtc: string;
  status: ReservationStatus;
  notes?: string;
  /**
   * When the resource asks a deposit: charged to the member's house account (the default),
   * paid at the desk (with how), or waived (with why). The last two take money.manage.
   */
  deposit?: 'charge' | 'paid' | 'waive';
  depositMethod?: 'CardOnFile' | 'Cash' | 'Check' | 'Wire';
  depositReference?: string;
  depositReason?: string;
}

export async function createReservation(input: ReservationInput): Promise<Reservation | null> {
  return apiWithFallback(
    () => authFetchJson<Reservation>('/api/reservations', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      const resource = resourceStore.byId(input.resourceId);
      return reservationStore.add({
        id: mockId('rsv'),
        resourceName: resource?.name ?? 'Unknown resource',
        resourceId: input.resourceId,
        memberId: input.memberId,
        memberName: input.memberName ?? '',
        memberEmail: input.memberEmail,
        partySize: input.partySize,
        startAtUtc: input.startAtUtc,
        endAtUtc: input.endAtUtc,
        status: input.status,
        notes: input.notes,
      });
    },
  );
}

/** The fields of a booking to change; one left out is kept and empty notes clear them. */
export type ReservationPatch = Partial<Omit<ReservationInput, 'deposit' | 'depositMethod' | 'depositReference' | 'depositReason'>> & {
  /** Makes the booking a guest's (no member), keeping the name and email sent. */
  guest?: boolean;
  /** The copy the edit was made from: an edit from an older one is refused (409). */
  version?: string;
};

export async function updateReservation(id: string, patch: ReservationPatch): Promise<Reservation | null> {
  return apiWithFallback(
    () => authFetchJson<Reservation>(`/api/reservations/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      const { version: _version, guest, ...rest } = patch;
      const next: Partial<Reservation> = { ...rest, memberName: rest.memberName ?? undefined };
      if (guest) next.memberId = undefined;
      if (patch.resourceId) {
        const resource = resourceStore.byId(patch.resourceId);
        if (resource) next.resourceName = resource.name;
      }
      if (next.memberName === undefined) delete next.memberName;
      return reservationStore.update(id, next);
    },
  );
}

export async function deleteReservation(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/reservations/${id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(100);
      return reservationStore.remove(id);
    },
  );
}

export interface ReservationStats {
  today: number;
  upcomingWeek: number;
  pending: number;
  totalGuestsToday: number;
}

export function computeReservationStats(reservations: Reservation[]): ReservationStats {
  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const endOfToday = startOfToday + day;
  const endOfWeek = startOfToday + 7 * day;
  let today = 0;
  let upcomingWeek = 0;
  let pending = 0;
  let totalGuestsToday = 0;
  for (const r of reservations) {
    const start = Date.parse(r.startAtUtc);
    if (start >= startOfToday && start < endOfToday) {
      today++;
      if (r.status !== 'Cancelled' && r.status !== 'NoShow') {
        totalGuestsToday += r.partySize;
      }
    }
    if (start >= startOfToday && start < endOfWeek && r.status !== 'Cancelled') {
      upcomingWeek++;
    }
    if (r.status === 'Pending') pending++;
  }
  return { today, upcomingWeek, pending, totalGuestsToday };
}

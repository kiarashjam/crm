// Reservations / bookings.
//
// Members book a resource (dining room, conference room, spa, court) for a
// time window. The catalog of resources is a small fixed list seeded into the
// mock store — in a real deployment it lives on the backend with capacity
// rules and access tiers. Reservations are simple party-size + time records.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type ResourceKind = 'Dining' | 'Meeting' | 'Spa' | 'Court' | 'Studio' | 'Lounge';
export type ReservationStatus = 'Pending' | 'Confirmed' | 'Cancelled' | 'CheckedIn' | 'Completed' | 'NoShow';

export interface Resource {
  id: string;
  name: string;
  kind: ResourceKind;
  capacity: number;
  description?: string;
}

export interface Reservation {
  id: string;
  resourceId: string;
  resourceName: string;
  memberName: string;
  memberEmail?: string;
  partySize: number;
  startAtUtc: string;
  endAtUtc: string;
  status: ReservationStatus;
  notes?: string;
}

export const RESERVATION_STATUSES: ReservationStatus[] = [
  'Pending',
  'Confirmed',
  'Cancelled',
  'CheckedIn',
  'Completed',
  'NoShow',
];

const day = 86_400_000;
const hour = 3_600_000;

const resourceSeed: Resource[] = [
  { id: 'res-dr', name: 'Main Dining Room', kind: 'Dining', capacity: 80, description: 'Open kitchen, seasonal tasting menu.' },
  { id: 'res-pdr', name: 'Private Dining Room', kind: 'Dining', capacity: 16, description: 'Bookable for 8–16; chef can customize menu.' },
  { id: 'res-ca', name: 'Conference Room A', kind: 'Meeting', capacity: 12, description: 'AV, whiteboard, video conference.' },
  { id: 'res-cb', name: 'Conference Room B', kind: 'Meeting', capacity: 6, description: 'Quiet huddle room with display.' },
  { id: 'res-spa', name: 'Spa Treatment Room', kind: 'Spa', capacity: 1, description: '60 / 90 minute massage, facial, or sauna.' },
  { id: 'res-court', name: 'Tennis Court', kind: 'Court', capacity: 4, description: 'Hard court; book in 60 minute blocks.' },
  { id: 'res-studio', name: 'Fitness Studio', kind: 'Studio', capacity: 20, description: 'Yoga, pilates, group classes.' },
  { id: 'res-rooftop', name: 'Rooftop Lounge', kind: 'Lounge', capacity: 40, description: 'Open Apr–Oct; sunset views.' },
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

export async function getResources(): Promise<Resource[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Resource[]>('/api/resources');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...resourceStore.list()].sort((a, b) => a.name.localeCompare(b.name));
    },
  );
}

export async function getReservations(): Promise<Reservation[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Reservation[]>('/api/reservations');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...reservationStore.list()].sort(
        (a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc),
      );
    },
  );
}

export type ReservationInput = Omit<Reservation, 'id' | 'resourceName'>;

export async function createReservation(input: ReservationInput): Promise<Reservation | null> {
  return apiWithFallback(
    () => authFetchJson<Reservation>('/api/reservations', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      const resource = resourceStore.byId(input.resourceId);
      return reservationStore.add({
        id: mockId('rsv'),
        resourceName: resource?.name ?? 'Unknown resource',
        ...input,
      });
    },
  );
}

export async function updateReservation(
  id: string,
  patch: Partial<ReservationInput>,
): Promise<Reservation | null> {
  return apiWithFallback(
    () => authFetchJson<Reservation>(`/api/reservations/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      const next: Partial<Reservation> = { ...patch };
      if (patch.resourceId) {
        const resource = resourceStore.byId(patch.resourceId);
        if (resource) next.resourceName = resource.name;
      }
      return reservationStore.update(id, next);
    },
  );
}

export async function deleteReservation(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/reservations/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
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

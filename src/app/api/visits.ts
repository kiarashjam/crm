// Visits / check-ins.
//
// Every time a member enters the club (or attends an event), staff logs a
// visit. A visit can earn loyalty points automatically: the server works them out from
// the club's settings (the venue's points times the member's tier multiplier) and writes
// them with the visit. This used to be worked out here, from a table of our own, and
// written to the loyalty ledger in a second request that could fail on its own.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { addLoyaltyEntry, TIER_MULTIPLIERS } from './loyalty';
import { getMembers, type MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type VisitVenue = 'Dining' | 'Bar' | 'Spa' | 'Gym' | 'Coworking' | 'Event' | 'Lounge' | 'Rooftop';

export interface Visit {
  id: string;
  memberId: string;
  memberName: string;
  memberTier: MemberTier;
  venue: VisitVenue;
  visitedAtUtc: string;
  guestCount: number;
  pointsAwarded: number;
  notes?: string;
}

export const VISIT_VENUES: VisitVenue[] = [
  'Dining',
  'Bar',
  'Spa',
  'Gym',
  'Coworking',
  'Event',
  'Lounge',
  'Rooftop',
];

const day = 86_400_000;
const hour = 3_600_000;

const seed: Visit[] = [
  {
    id: 'vis-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    memberTier: 'Platinum',
    venue: 'Dining',
    visitedAtUtc: new Date(Date.now() - 3 * hour).toISOString(),
    guestCount: 1,
    pointsAwarded: 100,
  },
  {
    id: 'vis-seed-2',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    memberTier: 'Gold',
    venue: 'Coworking',
    visitedAtUtc: new Date(Date.now() - 5 * hour).toISOString(),
    guestCount: 0,
    pointsAwarded: 50,
  },
  {
    id: 'vis-seed-3',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    memberTier: 'Silver',
    venue: 'Spa',
    visitedAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    guestCount: 1,
    pointsAwarded: 75,
  },
  {
    id: 'vis-seed-4',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    memberTier: 'Silver',
    venue: 'Gym',
    visitedAtUtc: new Date(Date.now() - 8 * hour).toISOString(),
    guestCount: 0,
    pointsAwarded: 25,
  },
  {
    id: 'vis-seed-5',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    memberTier: 'Platinum',
    venue: 'Bar',
    visitedAtUtc: new Date(Date.now() - 2 * day).toISOString(),
    guestCount: 2,
    pointsAwarded: 100,
  },
  {
    id: 'vis-seed-6',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    memberTier: 'Gold',
    venue: 'Dining',
    visitedAtUtc: new Date(Date.now() - 4 * day).toISOString(),
    guestCount: 3,
    pointsAwarded: 75,
  },
  {
    id: 'vis-seed-7',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    memberTier: 'Silver',
    venue: 'Rooftop',
    visitedAtUtc: new Date(Date.now() - 6 * day).toISOString(),
    guestCount: 0,
    pointsAwarded: 50,
  },
];

const visitStore = createMockStore<Visit>({
  storageKey: 'crm.mock.visits.v1',
  seed,
  idOf: (v) => v.id,
});

const BASE_POINTS_BY_VENUE: Record<VisitVenue, number> = {
  Dining: 50,
  Bar: 40,
  Spa: 60,
  Gym: 20,
  Coworking: 30,
  Event: 50,
  Lounge: 25,
  Rooftop: 30,
};

export async function getVisits(req?: RequestOptions): Promise<Visit[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Visit[]>('/api/visits', { signal: req?.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...visitStore.list()].sort(
        (a, b) => Date.parse(b.visitedAtUtc) - Date.parse(a.visitedAtUtc),
      );
    },
  );
}

export interface VisitInput {
  memberId: string;
  venue: VisitVenue;
  guestCount?: number;
  notes?: string;
  awardPoints?: boolean;
}

export async function checkIn(input: VisitInput): Promise<Visit | null> {
  // The server works out the points and writes them with the visit, in one request.
  if (!servesDemoData()) {
    return authFetchJson<Visit>('/api/visits', {
      method: 'POST',
      body: JSON.stringify({
        memberId: input.memberId,
        venue: input.venue,
        guestCount: input.guestCount ?? 0,
        notes: input.notes,
        awardPoints: input.awardPoints !== false,
      }),
    });
  }
  const members = await getMembers();
  const member = members.find((m) => m.id === input.memberId);
  if (!member) return null;
  const guestCount = input.guestCount ?? 0;
  const basePoints = BASE_POINTS_BY_VENUE[input.venue];
  const points = input.awardPoints === false
    ? 0
    : Math.floor(basePoints * (TIER_MULTIPLIERS[member.tier] ?? 1));

  await delay(140);
  const created = visitStore.add({
    id: mockId('vis'),
    memberId: member.id,
    memberName: `${member.firstName} ${member.lastName}`,
    memberTier: member.tier,
    venue: input.venue,
    visitedAtUtc: new Date().toISOString(),
    guestCount,
    pointsAwarded: points,
    notes: input.notes,
  });
  if (created && points > 0) {
    await addLoyaltyEntry({
      memberId: member.id,
      memberName: `${member.firstName} ${member.lastName}`,
      kind: 'Earned',
      reason: 'Visit',
      points,
      note: `${input.venue} check-in`,
    });
  }
  return created;
}

export async function deleteVisit(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/visits/${id}`, { method: 'DELETE' });
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(100);
      return visitStore.remove(id);
    },
  );
}

export interface VisitStats {
  today: number;
  thisWeek: number;
  pointsAwardedToday: number;
  topVenue: VisitVenue | null;
  uniqueMembersToday: number;
}

export function computeVisitStats(visits: Visit[]): VisitStats {
  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
  const weekAgo = startOfToday - 7 * day;

  let today = 0;
  let thisWeek = 0;
  let pointsAwardedToday = 0;
  const venueCounts = new Map<VisitVenue, number>();
  const membersToday = new Set<string>();

  for (const v of visits) {
    const visitedAt = Date.parse(v.visitedAtUtc);
    if (visitedAt >= startOfToday) {
      today++;
      pointsAwardedToday += v.pointsAwarded;
      membersToday.add(v.memberId);
    }
    if (visitedAt >= weekAgo) {
      thisWeek++;
      venueCounts.set(v.venue, (venueCounts.get(v.venue) ?? 0) + 1);
    }
  }

  let topVenue: VisitVenue | null = null;
  let topCount = 0;
  for (const [venue, count] of venueCounts) {
    if (count > topCount) {
      topCount = count;
      topVenue = venue;
    }
  }

  return {
    today,
    thisWeek,
    pointsAwardedToday,
    topVenue,
    uniqueMembersToday: membersToday.size,
  };
}

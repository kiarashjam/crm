// Loyalty & rewards.
//
// A points ledger tied to "touchpoints" (visits, purchases, RSVPs) — earned
// entries add to a member's balance, redemption entries subtract. The current
// balance is derived from the ledger so the books always reconcile. Each tier
// earns at a configurable multiplier.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import type { MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type LoyaltyEntryKind = 'Earned' | 'Redeemed' | 'Adjustment' | 'Expired';

export type LoyaltyReason =
  | 'Visit'
  | 'DiningPurchase'
  | 'BarPurchase'
  | 'EventAttendance'
  | 'Referral'
  | 'Anniversary'
  | 'ManualAdjustment'
  | 'PointsRedemption'
  | 'HouseCreditRedemption'
  | 'GiftCardRedemption';

export interface LoyaltyEntry {
  id: string;
  memberId: string;
  memberName: string;
  kind: LoyaltyEntryKind;
  reason: LoyaltyReason;
  points: number;
  note?: string;
  createdAtUtc: string;
}

export const LOYALTY_REASONS: LoyaltyReason[] = [
  'Visit',
  'DiningPurchase',
  'BarPurchase',
  'EventAttendance',
  'Referral',
  'Anniversary',
  'ManualAdjustment',
  'PointsRedemption',
  'HouseCreditRedemption',
  'GiftCardRedemption',
];

export const REASON_LABELS: Record<LoyaltyReason, string> = {
  Visit: 'Visit / check-in',
  DiningPurchase: 'Dining purchase',
  BarPurchase: 'Bar purchase',
  EventAttendance: 'Event attendance',
  Referral: 'Member referral',
  Anniversary: 'Membership anniversary',
  ManualAdjustment: 'Manual adjustment',
  PointsRedemption: 'Points redemption',
  HouseCreditRedemption: 'House credit',
  GiftCardRedemption: 'Gift card',
};

export const TIER_MULTIPLIERS: Record<MemberTier, number> = {
  Bronze: 1.0,
  Silver: 1.25,
  Gold: 1.5,
  Platinum: 2.0,
};

// 100 points = $1; redemption rate visible to staff/members.
export const POINTS_PER_DOLLAR = 100;

const day = 86_400_000;

const seed: LoyaltyEntry[] = [
  {
    id: 'loy-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    kind: 'Earned',
    reason: 'DiningPurchase',
    points: 480,
    note: 'Tasting menu for 2 — Platinum 2× multiplier.',
    createdAtUtc: new Date(Date.now() - 2 * day).toISOString(),
  },
  {
    id: 'loy-seed-2',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    kind: 'Earned',
    reason: 'EventAttendance',
    points: 200,
    note: 'Cigar & Single Malt Night.',
    createdAtUtc: new Date(Date.now() - 9 * day).toISOString(),
  },
  {
    id: 'loy-seed-3',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    kind: 'Earned',
    reason: 'Visit',
    points: 75,
    createdAtUtc: new Date(Date.now() - 1 * day).toISOString(),
  },
  {
    id: 'loy-seed-4',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    kind: 'Redeemed',
    reason: 'HouseCreditRedemption',
    points: -500,
    note: '$5 applied to bar tab.',
    createdAtUtc: new Date(Date.now() - 5 * day).toISOString(),
  },
  {
    id: 'loy-seed-5',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    kind: 'Earned',
    reason: 'Referral',
    points: 1000,
    note: 'Referred Marcus Lindgren — bonus on application.',
    createdAtUtc: new Date(Date.now() - 6 * day).toISOString(),
  },
  {
    id: 'loy-seed-6',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    kind: 'Earned',
    reason: 'DiningPurchase',
    points: 110,
    createdAtUtc: new Date(Date.now() - 3 * day).toISOString(),
  },
  {
    id: 'loy-seed-7',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    kind: 'Redeemed',
    reason: 'GiftCardRedemption',
    points: -2000,
    note: '$20 gift card to guest.',
    createdAtUtc: new Date(Date.now() - 14 * day).toISOString(),
  },
  {
    id: 'loy-seed-8',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    kind: 'Earned',
    reason: 'Anniversary',
    points: 500,
    note: '1-year membership bonus.',
    createdAtUtc: new Date(Date.now() - 30 * day).toISOString(),
  },
];

const ledgerStore = createMockStore<LoyaltyEntry>({
  storageKey: 'crm.mock.loyalty.v1',
  seed,
  idOf: (l) => l.id,
});

export async function getLoyaltyLedger(): Promise<LoyaltyEntry[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<LoyaltyEntry[]>('/api/loyalty/ledger');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...ledgerStore.list()].sort(
        (a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc),
      );
    },
  );
}

export interface LoyaltyEntryInput {
  memberId: string;
  memberName: string;
  kind: LoyaltyEntryKind;
  reason: LoyaltyReason;
  points: number;
  note?: string;
}

export async function addLoyaltyEntry(input: LoyaltyEntryInput): Promise<LoyaltyEntry | null> {
  return apiWithFallback(
    () =>
      authFetchJson<LoyaltyEntry>('/api/loyalty/ledger', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(140);
      return ledgerStore.add({
        id: mockId('loy'),
        createdAtUtc: new Date().toISOString(),
        ...input,
      });
    },
  );
}

export async function deleteLoyaltyEntry(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/loyalty/ledger/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return ledgerStore.remove(id);
    },
  );
}

export interface MemberBalance {
  memberId: string;
  memberName: string;
  points: number;
  entries: number;
}

export function computeMemberBalances(entries: LoyaltyEntry[]): MemberBalance[] {
  const map = new Map<string, MemberBalance>();
  for (const e of entries) {
    const existing = map.get(e.memberId);
    if (existing) {
      existing.points += e.points;
      existing.entries += 1;
    } else {
      map.set(e.memberId, {
        memberId: e.memberId,
        memberName: e.memberName,
        points: e.points,
        entries: 1,
      });
    }
  }
  return [...map.values()].sort((a, b) => b.points - a.points);
}

export interface LoyaltyStats {
  totalIssued: number;
  totalRedeemed: number;
  outstanding: number;
  outstandingDollarValue: number;
  activeMembers: number;
}

export function computeLoyaltyStats(entries: LoyaltyEntry[]): LoyaltyStats {
  let issued = 0;
  let redeemed = 0;
  const members = new Set<string>();
  for (const e of entries) {
    members.add(e.memberId);
    if (e.points > 0) issued += e.points;
    else redeemed += -e.points;
  }
  const outstanding = issued - redeemed;
  return {
    totalIssued: issued,
    totalRedeemed: redeemed,
    outstanding,
    outstandingDollarValue: Math.round((outstanding / POINTS_PER_DOLLAR) * 100) / 100,
    activeMembers: members.size,
  };
}

export function pointsForPurchase(amountDollars: number, tier: MemberTier): number {
  const base = Math.floor(amountDollars * 10); // 10 base points per $1
  return Math.floor(base * TIER_MULTIPLIERS[tier]);
}

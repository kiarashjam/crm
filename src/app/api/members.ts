// Membership management.
//
// A `Member` represents a recurring-billing customer (club / hospitality model)
// — distinct from a Contact, which is the generic CRM person record. Members
// belong to a tier, pay periodic dues, and have a renewal date.
// Real backend support is optional: the demo store keeps everything working
// offline so the UI is fully exercisable without server changes.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type MemberTier = 'Bronze' | 'Silver' | 'Gold' | 'Platinum';
export type MemberStatus = 'Pending' | 'Active' | 'Lapsed' | 'Cancelled';
export type DuesFrequency = 'Monthly' | 'Quarterly' | 'Annual';

export interface Member {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phone?: string;
  tier: MemberTier;
  status: MemberStatus;
  joinedAtUtc: string;
  renewsAtUtc: string;
  duesAmount: number;
  duesFrequency: DuesFrequency;
  houseAccountBalance: number;
  notes?: string;
}

export const MEMBER_TIERS: MemberTier[] = ['Bronze', 'Silver', 'Gold', 'Platinum'];
export const MEMBER_STATUSES: MemberStatus[] = ['Pending', 'Active', 'Lapsed', 'Cancelled'];
export const DUES_FREQUENCIES: DuesFrequency[] = ['Monthly', 'Quarterly', 'Annual'];

export const TIER_DUES: Record<MemberTier, number> = {
  Bronze: 49,
  Silver: 129,
  Gold: 299,
  Platinum: 599,
};

const day = 86_400_000;
const seed: Member[] = [
  {
    id: 'mem-seed-1',
    firstName: 'Amelia',
    lastName: 'Hartwell',
    email: 'amelia.hartwell@example.com',
    phone: '+1 (415) 555-0142',
    tier: 'Platinum',
    status: 'Active',
    joinedAtUtc: new Date(Date.now() - 420 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() + 45 * day).toISOString(),
    duesAmount: 599,
    duesFrequency: 'Monthly',
    houseAccountBalance: 320.5,
    notes: 'Founding member; prefers dining room booth 7.',
  },
  {
    id: 'mem-seed-2',
    firstName: 'Daniel',
    lastName: 'Okafor',
    email: 'daniel.okafor@example.com',
    tier: 'Gold',
    status: 'Active',
    joinedAtUtc: new Date(Date.now() - 200 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() + 15 * day).toISOString(),
    duesAmount: 299,
    duesFrequency: 'Monthly',
    houseAccountBalance: 84,
  },
  {
    id: 'mem-seed-3',
    firstName: 'Priya',
    lastName: 'Raman',
    email: 'priya.raman@example.com',
    phone: '+1 (212) 555-0199',
    tier: 'Silver',
    status: 'Active',
    joinedAtUtc: new Date(Date.now() - 120 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() + 75 * day).toISOString(),
    duesAmount: 129,
    duesFrequency: 'Monthly',
    houseAccountBalance: 0,
  },
  {
    id: 'mem-seed-4',
    firstName: 'Marcus',
    lastName: 'Lindgren',
    email: 'marcus.lindgren@example.com',
    tier: 'Bronze',
    status: 'Pending',
    joinedAtUtc: new Date(Date.now() - 6 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() + 359 * day).toISOString(),
    duesAmount: 49,
    duesFrequency: 'Monthly',
    houseAccountBalance: 0,
    notes: 'Application submitted; awaiting committee approval.',
  },
  {
    id: 'mem-seed-5',
    firstName: 'Sofia',
    lastName: 'Reyes',
    email: 'sofia.reyes@example.com',
    tier: 'Gold',
    status: 'Lapsed',
    joinedAtUtc: new Date(Date.now() - 700 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() - 18 * day).toISOString(),
    duesAmount: 299,
    duesFrequency: 'Monthly',
    houseAccountBalance: -45,
    notes: 'Card declined last cycle; outreach scheduled.',
  },
  {
    id: 'mem-seed-6',
    firstName: 'Jordan',
    lastName: 'Patel',
    email: 'jordan.patel@example.com',
    tier: 'Silver',
    status: 'Active',
    joinedAtUtc: new Date(Date.now() - 88 * day).toISOString(),
    renewsAtUtc: new Date(Date.now() + 277 * day).toISOString(),
    duesAmount: 1290,
    duesFrequency: 'Annual',
    houseAccountBalance: 12.75,
  },
];

const memberStore = createMockStore<Member>({
  storageKey: 'crm.mock.members.v1',
  seed,
  idOf: (m) => m.id,
});

export async function getMembers(): Promise<Member[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Member[]>('/api/members');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...memberStore.list()].sort((a, b) => a.lastName.localeCompare(b.lastName));
    },
  );
}

export type MemberInput = Omit<Member, 'id' | 'houseAccountBalance'> & {
  houseAccountBalance?: number;
};

export async function createMember(input: MemberInput): Promise<Member | null> {
  return apiWithFallback(
    () => authFetchJson<Member>('/api/members', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return memberStore.add({
        id: mockId('mem'),
        houseAccountBalance: 0,
        ...input,
      });
    },
  );
}

export async function updateMember(id: string, patch: Partial<MemberInput>): Promise<Member | null> {
  return apiWithFallback(
    () => authFetchJson<Member>(`/api/members/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      return memberStore.update(id, patch);
    },
  );
}

export async function deleteMember(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/members/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return memberStore.remove(id);
    },
  );
}

export interface MemberStats {
  total: number;
  active: number;
  pending: number;
  lapsed: number;
  monthlyRecurringRevenue: number;
  totalHouseAccountBalance: number;
  renewingSoon: number;
}

export function computeMemberStats(members: Member[]): MemberStats {
  const soonThreshold = Date.now() + 30 * day;
  let mrr = 0;
  let house = 0;
  let active = 0;
  let pending = 0;
  let lapsed = 0;
  let renewingSoon = 0;
  for (const m of members) {
    if (m.status === 'Active') active++;
    else if (m.status === 'Pending') pending++;
    else if (m.status === 'Lapsed') lapsed++;
    house += m.houseAccountBalance;
    if (m.status === 'Active') {
      const monthly =
        m.duesFrequency === 'Monthly'
          ? m.duesAmount
          : m.duesFrequency === 'Quarterly'
            ? m.duesAmount / 3
            : m.duesAmount / 12;
      mrr += monthly;
    }
    if (m.status === 'Active' && Date.parse(m.renewsAtUtc) <= soonThreshold) {
      renewingSoon++;
    }
  }
  return {
    total: members.length,
    active,
    pending,
    lapsed,
    monthlyRecurringRevenue: Math.round(mrr * 100) / 100,
    totalHouseAccountBalance: Math.round(house * 100) / 100,
    renewingSoon,
  };
}

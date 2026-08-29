// Households / family billing.
//
// A household groups a primary member with sub-members (spouse, partner,
// dependents). All charges roll up to the primary's house account, even
// when the sub-member is the one swiping their card at the bar.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getMembers, type Member } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type HouseholdRole = 'Primary' | 'Spouse' | 'Partner' | 'Dependent' | 'Other';

export interface Household {
  id: string;
  name: string;
  primaryMemberId: string;
  createdAtUtc: string;
  notes?: string;
}

export interface HouseholdMember {
  id: string;
  householdId: string;
  memberId: string;
  role: HouseholdRole;
  rollUpBilling: boolean;
}

export const HOUSEHOLD_ROLES: HouseholdRole[] = [
  'Primary',
  'Spouse',
  'Partner',
  'Dependent',
  'Other',
];

const day = 86_400_000;

const householdSeed: Household[] = [
  {
    id: 'hh-seed-1',
    name: 'Hartwell',
    primaryMemberId: 'mem-seed-1',
    createdAtUtc: new Date(Date.now() - 400 * day).toISOString(),
    notes: 'Anniversary March 14. Charges roll up to Amelia.',
  },
  {
    id: 'hh-seed-2',
    name: 'Okafor',
    primaryMemberId: 'mem-seed-2',
    createdAtUtc: new Date(Date.now() - 180 * day).toISOString(),
  },
];

const householdStore = createMockStore<Household>({
  storageKey: 'crm.mock.households.v1',
  seed: householdSeed,
  idOf: (h) => h.id,
});

const householdMemberSeed: HouseholdMember[] = [
  {
    id: 'hm-seed-1',
    householdId: 'hh-seed-1',
    memberId: 'mem-seed-1',
    role: 'Primary',
    rollUpBilling: true,
  },
  {
    id: 'hm-seed-2',
    householdId: 'hh-seed-2',
    memberId: 'mem-seed-2',
    role: 'Primary',
    rollUpBilling: true,
  },
  {
    id: 'hm-seed-3',
    householdId: 'hh-seed-2',
    memberId: 'mem-seed-6',
    role: 'Spouse',
    rollUpBilling: true,
  },
];

const householdMemberStore = createMockStore<HouseholdMember>({
  storageKey: 'crm.mock.householdMembers.v1',
  seed: householdMemberSeed,
  idOf: (h) => h.id,
});

export async function getHouseholds(): Promise<Household[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Household[]>('/api/households');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(100);
      return [...householdStore.list()].sort((a, b) => a.name.localeCompare(b.name));
    },
  );
}

export async function getHouseholdMembers(): Promise<HouseholdMember[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<HouseholdMember[]>('/api/household-members');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...householdMemberStore.list()];
    },
  );
}

export interface HouseholdInput {
  name: string;
  primaryMemberId: string;
  notes?: string;
}

export async function createHousehold(input: HouseholdInput): Promise<Household | null> {
  const created = await apiWithFallback(
    () => authFetchJson<Household>('/api/households', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return householdStore.add({
        id: mockId('hh'),
        createdAtUtc: new Date().toISOString(),
        ...input,
      });
    },
  );
  if (created) {
    householdMemberStore.add({
      id: mockId('hm'),
      householdId: created.id,
      memberId: input.primaryMemberId,
      role: 'Primary',
      rollUpBilling: true,
    });
  }
  return created;
}

export async function updateHousehold(
  id: string,
  patch: Partial<Household>,
): Promise<Household | null> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      return householdStore.update(id, patch);
    },
  );
}

export async function deleteHousehold(id: string): Promise<boolean> {
  const ok = await apiWithFallback(
    async () => {
      const res = await authFetch(`/api/households/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      // Cascade — drop all links too.
      for (const link of householdMemberStore.list().filter((h) => h.householdId === id)) {
        householdMemberStore.remove(link.id);
      }
      return householdStore.remove(id);
    },
  );
  return ok;
}

export interface HouseholdMemberInput {
  householdId: string;
  memberId: string;
  role: HouseholdRole;
  rollUpBilling?: boolean;
}

export async function addMemberToHousehold(
  input: HouseholdMemberInput,
): Promise<HouseholdMember | null> {
  return apiWithFallback(
    () =>
      authFetchJson<HouseholdMember>('/api/household-members', {
        method: 'POST',
        body: JSON.stringify(input),
      }),
    async () => {
      await delay(140);
      return householdMemberStore.add({
        id: mockId('hm'),
        householdId: input.householdId,
        memberId: input.memberId,
        role: input.role,
        rollUpBilling: input.rollUpBilling ?? true,
      });
    },
  );
}

export async function removeMemberFromHousehold(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/household-members/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return householdMemberStore.remove(id);
    },
  );
}

export interface HouseholdRollup {
  household: Household;
  members: { link: HouseholdMember; member: Member | undefined }[];
  totalDuesMonthly: number;
  totalHouseBalance: number;
}

export async function getHouseholdRollups(): Promise<HouseholdRollup[]> {
  const [households, links, members] = await Promise.all([
    getHouseholds(),
    getHouseholdMembers(),
    getMembers(),
  ]);
  return households.map((h) => {
    const myLinks = links.filter((l) => l.householdId === h.id);
    const memberRows = myLinks.map((l) => ({
      link: l,
      member: members.find((m) => m.id === l.memberId),
    }));
    let totalDuesMonthly = 0;
    let totalHouseBalance = 0;
    for (const row of memberRows) {
      if (!row.member) continue;
      const monthly =
        row.member.duesFrequency === 'Monthly'
          ? row.member.duesAmount
          : row.member.duesFrequency === 'Quarterly'
            ? row.member.duesAmount / 3
            : row.member.duesAmount / 12;
      totalDuesMonthly += monthly;
      totalHouseBalance += row.member.houseAccountBalance;
    }
    return {
      household: h,
      members: memberRows,
      totalDuesMonthly: Math.round(totalDuesMonthly * 100) / 100,
      totalHouseBalance: Math.round(totalHouseBalance * 100) / 100,
    };
  });
}

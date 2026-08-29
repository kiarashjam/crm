// Donations.
//
// Optional contributions members make to causes the club supports — staff
// pantry, scholarship fund, community grants. Each donation is a one-time
// charge that posts to the member's house account.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type DonationCause =
  | 'StaffSupport'
  | 'Scholarship'
  | 'CommunityGrants'
  | 'Sustainability'
  | 'CulinaryEducation'
  | 'Arts'
  | 'Other';

export interface Donation {
  id: string;
  memberId: string;
  memberName: string;
  cause: DonationCause;
  amount: number;
  receivedAtUtc: string;
  isAnonymous: boolean;
  message?: string;
  acknowledged: boolean;
}

export const DONATION_CAUSES: DonationCause[] = [
  'StaffSupport',
  'Scholarship',
  'CommunityGrants',
  'Sustainability',
  'CulinaryEducation',
  'Arts',
  'Other',
];

export const CAUSE_LABELS: Record<DonationCause, string> = {
  StaffSupport: 'Staff support fund',
  Scholarship: 'Member scholarship',
  CommunityGrants: 'Community grants',
  Sustainability: 'Sustainability initiative',
  CulinaryEducation: 'Culinary education',
  Arts: 'Arts programming',
  Other: 'Other',
};

const day = 86_400_000;

const seed: Donation[] = [
  {
    id: 'dn-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    cause: 'StaffSupport',
    amount: 1000,
    receivedAtUtc: new Date(Date.now() - 14 * day).toISOString(),
    isAnonymous: false,
    message: 'Thank you to the team for the holiday season.',
    acknowledged: true,
  },
  {
    id: 'dn-seed-2',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    cause: 'Scholarship',
    amount: 500,
    receivedAtUtc: new Date(Date.now() - 30 * day).toISOString(),
    isAnonymous: false,
    acknowledged: true,
  },
  {
    id: 'dn-seed-3',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    cause: 'CulinaryEducation',
    amount: 250,
    receivedAtUtc: new Date(Date.now() - 6 * day).toISOString(),
    isAnonymous: true,
    acknowledged: false,
  },
  {
    id: 'dn-seed-4',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    cause: 'Sustainability',
    amount: 2500,
    receivedAtUtc: new Date(Date.now() - 65 * day).toISOString(),
    isAnonymous: false,
    message: 'Earmarked for the rooftop garden expansion.',
    acknowledged: true,
  },
];

const donationStore = createMockStore<Donation>({
  storageKey: 'crm.mock.donations.v1',
  seed,
  idOf: (d) => d.id,
});

export async function getDonations(): Promise<Donation[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Donation[]>('/api/donations');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...donationStore.list()].sort(
        (a, b) => Date.parse(b.receivedAtUtc) - Date.parse(a.receivedAtUtc),
      );
    },
  );
}

export interface DonationInput {
  memberId: string;
  memberName: string;
  cause: DonationCause;
  amount: number;
  isAnonymous?: boolean;
  message?: string;
}

export async function recordDonation(input: DonationInput): Promise<Donation | null> {
  return apiWithFallback(
    () => authFetchJson<Donation>('/api/donations', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return donationStore.add({
        id: mockId('dn'),
        receivedAtUtc: new Date().toISOString(),
        isAnonymous: input.isAnonymous ?? false,
        acknowledged: false,
        ...input,
      });
    },
  );
}

export async function acknowledgeDonation(id: string): Promise<Donation | null> {
  return apiWithFallback(
    () => authFetchJson<Donation>(`/api/donations/${id}/acknowledge`, { method: 'POST' }),
    async () => {
      await delay(100);
      return donationStore.update(id, { acknowledged: true });
    },
  );
}

export async function deleteDonation(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/donations/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return donationStore.remove(id);
    },
  );
}

export interface DonationStats {
  totalRaised: number;
  donorCount: number;
  awaitingAcknowledgement: number;
  topCause: { cause: DonationCause; amount: number } | null;
}

export function computeDonationStats(donations: Donation[]): DonationStats {
  let totalRaised = 0;
  let awaitingAcknowledgement = 0;
  const donors = new Set<string>();
  const causeTotals = new Map<DonationCause, number>();
  for (const d of donations) {
    totalRaised += d.amount;
    donors.add(d.memberId);
    if (!d.acknowledged) awaitingAcknowledgement++;
    causeTotals.set(d.cause, (causeTotals.get(d.cause) ?? 0) + d.amount);
  }
  let topCause: { cause: DonationCause; amount: number } | null = null;
  for (const [cause, amount] of causeTotals) {
    if (!topCause || amount > topCause.amount) topCause = { cause, amount };
  }
  return {
    totalRaised: Math.round(totalRaised * 100) / 100,
    donorCount: donors.size,
    awaitingAcknowledgement,
    topCause,
  };
}

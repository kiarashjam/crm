// Donations.
//
// Optional contributions members make to causes the club supports — staff
// pantry, scholarship fund, community grants. A donation is money received for a
// cause, kept on the server's ledger: it is voided with a reason, never deleted.

import { apiWithFallback, authFetchJson, type RequestOptions } from './apiClient';
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
  /** Set when someone allowed to void money took it back: it stays listed, and counts for nothing. */
  status?: 'Voided';
  voidReason?: string;
  voidedAtUtc?: string;
  voidedByName?: string;
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

export async function getDonations(req?: RequestOptions): Promise<Donation[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Donation[]>('/api/donations', { signal: req?.signal });
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

/**
 * Void a donation with a reason: one entered twice, or one that bounced. It stays on the
 * record, saying why, and leaves the giving totals. Takes money.void.
 */
export async function voidDonation(id: string, reason: string): Promise<Donation | null> {
  return apiWithFallback(
    () => authFetchJson<Donation>(`/api/donations/${id}/void`, { method: 'POST', body: JSON.stringify({ reason }) }),
    async () => {
      await delay(100);
      return donationStore.update(id, { status: 'Voided', voidReason: reason, voidedAtUtc: new Date().toISOString() });
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
    // A voided donation stays on the list but was never money the club received.
    if (d.status === 'Voided') continue;
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

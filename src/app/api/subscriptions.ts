// Subscriptions / recurring billing.
//
// A subscription is a recurring charge schedule — typically a member's dues
// but also extras like locker rental, fitness add-ons, parking. When a cycle
// comes due, the billing run drops a charge onto the member's house account.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { createCharge } from './charges';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type SubscriptionCadence = 'Monthly' | 'Quarterly' | 'SemiAnnual' | 'Annual';
export type SubscriptionStatus = 'Active' | 'Paused' | 'Cancelled' | 'PastDue';

export interface Subscription {
  id: string;
  memberId: string;
  memberName: string;
  name: string;
  amount: number;
  cadence: SubscriptionCadence;
  status: SubscriptionStatus;
  nextChargeAtUtc: string;
  startedAtUtc: string;
  lastChargedAtUtc?: string;
  notes?: string;
}

export const SUBSCRIPTION_CADENCES: SubscriptionCadence[] = [
  'Monthly',
  'Quarterly',
  'SemiAnnual',
  'Annual',
];
export const SUBSCRIPTION_STATUSES: SubscriptionStatus[] = [
  'Active',
  'Paused',
  'Cancelled',
  'PastDue',
];

export const CADENCE_DAYS: Record<SubscriptionCadence, number> = {
  Monthly: 30,
  Quarterly: 91,
  SemiAnnual: 182,
  Annual: 365,
};

const day = 86_400_000;

const seed: Subscription[] = [
  {
    id: 'sub-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    name: 'Platinum dues',
    amount: 599,
    cadence: 'Monthly',
    status: 'Active',
    nextChargeAtUtc: new Date(Date.now() + 2 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 400 * day).toISOString(),
    lastChargedAtUtc: new Date(Date.now() - 28 * day).toISOString(),
  },
  {
    id: 'sub-seed-2',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    name: 'Locker rental',
    amount: 35,
    cadence: 'Monthly',
    status: 'Active',
    nextChargeAtUtc: new Date(Date.now() + 12 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 200 * day).toISOString(),
    lastChargedAtUtc: new Date(Date.now() - 18 * day).toISOString(),
  },
  {
    id: 'sub-seed-3',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    name: 'Gold dues',
    amount: 299,
    cadence: 'Monthly',
    status: 'Active',
    nextChargeAtUtc: new Date(Date.now() + 15 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 180 * day).toISOString(),
    lastChargedAtUtc: new Date(Date.now() - 15 * day).toISOString(),
  },
  {
    id: 'sub-seed-4',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    name: 'Silver dues',
    amount: 129,
    cadence: 'Monthly',
    status: 'Active',
    nextChargeAtUtc: new Date(Date.now() + 8 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 110 * day).toISOString(),
    lastChargedAtUtc: new Date(Date.now() - 22 * day).toISOString(),
  },
  {
    id: 'sub-seed-5',
    memberId: 'mem-seed-5',
    memberName: 'Sofia Reyes',
    name: 'Gold dues',
    amount: 299,
    cadence: 'Monthly',
    status: 'PastDue',
    nextChargeAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 700 * day).toISOString(),
    lastChargedAtUtc: new Date(Date.now() - 45 * day).toISOString(),
    notes: 'Card declined; retry scheduled.',
  },
  {
    id: 'sub-seed-6',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    name: 'Silver annual dues',
    amount: 1290,
    cadence: 'Annual',
    status: 'Active',
    nextChargeAtUtc: new Date(Date.now() + 277 * day).toISOString(),
    startedAtUtc: new Date(Date.now() - 88 * day).toISOString(),
  },
];

const subStore = createMockStore<Subscription>({
  storageKey: 'crm.mock.subscriptions.v1',
  seed,
  idOf: (s) => s.id,
});

export async function getSubscriptions(): Promise<Subscription[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Subscription[]>('/api/subscriptions');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...subStore.list()].sort(
        (a, b) => Date.parse(a.nextChargeAtUtc) - Date.parse(b.nextChargeAtUtc),
      );
    },
  );
}

export interface SubscriptionInput {
  memberId: string;
  memberName: string;
  name: string;
  amount: number;
  cadence: SubscriptionCadence;
  nextChargeAtUtc?: string;
  notes?: string;
}

export async function createSubscription(input: SubscriptionInput): Promise<Subscription | null> {
  return apiWithFallback(
    () => authFetchJson<Subscription>('/api/subscriptions', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      return subStore.add({
        id: mockId('sub'),
        startedAtUtc: new Date().toISOString(),
        status: 'Active',
        nextChargeAtUtc: input.nextChargeAtUtc ?? new Date(Date.now() + CADENCE_DAYS[input.cadence] * day).toISOString(),
        ...input,
      });
    },
  );
}

export async function updateSubscription(
  id: string,
  patch: Partial<Subscription>,
): Promise<Subscription | null> {
  return apiWithFallback(
    () => authFetchJson<Subscription>(`/api/subscriptions/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(120);
      return subStore.update(id, patch);
    },
  );
}

export async function deleteSubscription(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/subscriptions/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return subStore.remove(id);
    },
  );
}

/**
 * Manually run the billing cycle for a subscription: drop a charge on the
 * member's account and advance `nextChargeAtUtc`. Real billing runs would
 * happen as a scheduled job; this is the staff-triggered version.
 */
export async function runBillingCycle(id: string): Promise<Subscription | null> {
  const sub = subStore.byId(id);
  if (!sub) return null;
  if (sub.status === 'Cancelled' || sub.status === 'Paused') return sub;

  await createCharge({
    memberId: sub.memberId,
    memberName: sub.memberName,
    kind: 'Dues',
    description: sub.name,
    amount: sub.amount,
    dueAtUtc: new Date(Date.now() + 14 * day).toISOString(),
  });

  const nextChargeAtUtc = new Date(
    Date.parse(sub.nextChargeAtUtc) + CADENCE_DAYS[sub.cadence] * day,
  ).toISOString();
  return subStore.update(id, {
    lastChargedAtUtc: new Date().toISOString(),
    nextChargeAtUtc,
    status: 'Active',
  });
}

/**
 * Convenience: run billing cycles for all subscriptions whose next-charge
 * date is in the past. Returns the number of charges created.
 */
export async function runDueBillingCycles(): Promise<number> {
  const now = Date.now();
  const due = subStore
    .list()
    .filter(
      (s) =>
        (s.status === 'Active' || s.status === 'PastDue') &&
        Date.parse(s.nextChargeAtUtc) <= now,
    );
  for (const s of due) {
    await runBillingCycle(s.id);
  }
  return due.length;
}

export interface SubscriptionStats {
  activeCount: number;
  mrr: number;
  pastDueCount: number;
  dueSoonCount: number;
  cancelledCount: number;
}

export function computeSubscriptionStats(subs: Subscription[]): SubscriptionStats {
  const soon = Date.now() + 14 * day;
  let activeCount = 0;
  let mrr = 0;
  let pastDueCount = 0;
  let dueSoonCount = 0;
  let cancelledCount = 0;
  for (const s of subs) {
    if (s.status === 'Active') activeCount++;
    if (s.status === 'PastDue') pastDueCount++;
    if (s.status === 'Cancelled') cancelledCount++;
    if (s.status === 'Active' || s.status === 'PastDue') {
      const monthly =
        s.cadence === 'Monthly'
          ? s.amount
          : s.cadence === 'Quarterly'
            ? s.amount / 3
            : s.cadence === 'SemiAnnual'
              ? s.amount / 6
              : s.amount / 12;
      mrr += monthly;
      if (Date.parse(s.nextChargeAtUtc) <= soon) dueSoonCount++;
    }
  }
  return {
    activeCount,
    mrr: Math.round(mrr * 100) / 100,
    pastDueCount,
    dueSoonCount,
    cancelledCount,
  };
}


// Subscriptions / recurring billing.
//
// A subscription is a recurring charge schedule — typically a member's dues
// but also extras like locker rental, fitness add-ons, parking. Dues are billed in
// advance, once per calendar period (a month, a quarter, half a year or a year from the
// day it started): the server's billing run posts each period's charge onto the member's
// house account, by itself every day when the club turns automatic billing on, or when
// someone asks (api/billing).

import { ApiError, apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
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
  /** The tier or dues item of the price catalogue it was started from. */
  catalogItemId?: string;
  /** The server's version of the subscription as it was read; a change is sent with this (If-Match). */
  version?: string;
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

/** The calendar months in each cadence's period. */
export const CADENCE_MONTHS: Record<SubscriptionCadence, number> = {
  Monthly: 1,
  Quarterly: 3,
  SemiAnnual: 6,
  Annual: 12,
};

const day = 86_400_000;

/**
 * The start of the period after the one starting at `iso`: the same day of the month,
 * or the month's last day when it is shorter (the 31st of January, then the 28th of
 * February). As the server reckons it; it used to be thirty days on, whatever the month.
 */
export function nextPeriodStart(iso: string, cadence: SubscriptionCadence, anchorDay?: number): string {
  const from = new Date(iso);
  const anchor = anchorDay ?? from.getUTCDate();
  const months = from.getUTCMonth() + (CADENCE_MONTHS[cadence] ?? 1);
  const lastDay = new Date(Date.UTC(from.getUTCFullYear(), months + 1, 0)).getUTCDate();
  return new Date(Date.UTC(from.getUTCFullYear(), months, Math.min(anchor, lastDay))).toISOString();
}

/** Today at midnight UTC: the day a subscription started today first bills. */
function todayUtc(): string {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate())).toISOString();
}

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

export async function getSubscriptions(req?: RequestOptions): Promise<Subscription[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Subscription[]>('/api/subscriptions', { signal: req?.signal });
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
  /** The first period's start, billed on that day; left out, today. */
  nextChargeAtUtc?: string;
  notes?: string;
  /** Started from a tier or dues item of the catalogue: its name, price and cadence unless these are sent. */
  catalogItemId?: string;
}

export async function createSubscription(input: SubscriptionInput): Promise<Subscription | null> {
  return apiWithFallback(
    () => authFetchJson<Subscription>('/api/subscriptions', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      // Billed in advance, as the server does: the first period starts today unless told otherwise.
      return subStore.add({
        id: mockId('sub'),
        startedAtUtc: new Date().toISOString(),
        status: 'Active',
        ...input,
        nextChargeAtUtc: input.nextChargeAtUtc ? new Date(input.nextChargeAtUtc).toISOString() : todayUtc(),
      });
    },
  );
}

export async function updateSubscription(
  id: string,
  patch: Partial<Subscription>,
  version?: string,
): Promise<Subscription | null> {
  return apiWithFallback(
    // Checked against the copy the change was made from: one paused or cancelled by someone
    // else meanwhile is refused (409), not changed back.
    () => authFetchJson<Subscription>(`/api/subscriptions/${id}`, { method: 'PUT', headers: version ? { 'If-Match': `"${version}"` } : undefined, body: JSON.stringify(patch) }),
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
      await ensureOk(res);
      return true;
    },
    async () => {
      await delay(100);
      return subStore.remove(id);
    },
  );
}

/** The server's code for billing a period that has not started: billing it early is asked for apart. */
export const NOT_DUE_CODE = 'Subscription.NotDue.Conflict';

/** Whether a refusal says the next period has not started, so it can be billed early instead. */
export function isNotDue(err: unknown): err is ApiError {
  return err instanceof ApiError && err.code === NOT_DUE_CODE;
}

/**
 * Bills a subscription's period by hand: the server posts the charge and moves the next
 * date on, once per period. A period that has not started is refused (see `isNotDue`)
 * unless `early` asks to bill it now, one period ahead at most.
 */
export async function runBillingCycle(id: string, options: { early?: boolean } = {}): Promise<Subscription | null> {
  // The server posts the charge, moves the balance and the next date itself.
  if (!servesDemoData()) {
    return authFetchJson<Subscription>(`/api/subscriptions/${id}/bill`, {
      method: 'POST',
      ...(options.early ? { body: JSON.stringify({ early: true }) } : {}),
    });
  }
  const sub = subStore.byId(id);
  if (!sub) return null;
  if (sub.status === 'Cancelled' || sub.status === 'Paused') return sub;
  if (!options.early && Date.parse(sub.nextChargeAtUtc) > Date.now()) {
    const starts = new Date(sub.nextChargeAtUtc).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    throw new ApiError(
      `This subscription's next period starts on ${starts}, so nothing was billed. Bill it early to charge that period now.`,
      409, `/api/subscriptions/${id}/bill`, true, NOT_DUE_CODE);
  }

  await createCharge({
    memberId: sub.memberId,
    memberName: sub.memberName,
    kind: 'Dues',
    description: sub.name,
    amount: sub.amount,
    dueAtUtc: new Date(Date.now() + 14 * day).toISOString(),
  });

  const nextChargeAtUtc = nextPeriodStart(sub.nextChargeAtUtc, sub.cadence, new Date(sub.startedAtUtc).getUTCDate());
  return subStore.update(id, {
    lastChargedAtUtc: new Date().toISOString(),
    nextChargeAtUtc,
    status: 'Active',
  });
}

/**
 * Demo data's billing run (api/billing's runBillingNow): bills each subscription whose
 * period has started. Returns the number of charges created. Against the server, the
 * server's own run bills, once per period, in one request.
 */
export async function runDueBillingCycles(): Promise<number> {
  const now = Date.now();
  // Through the API: the browser's own list is empty when they live on the server, so
  // this used to bill nothing and report "Ran 0 due cycles".
  const due = (await getSubscriptions())
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


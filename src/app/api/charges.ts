// House-account charges and payments.
//
// Every member has an internal ledger that records charges (dining bills,
// event tickets, dues, spa, etc.) and payments. The member's `houseAccountBalance`
// stays in sync with the running ledger total.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getMembers, updateMember, type Member } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type ChargeKind = 'Dues' | 'Dining' | 'Bar' | 'Event' | 'Spa' | 'Booking' | 'Retail' | 'Other';
export type ChargeStatus = 'Pending' | 'Paid' | 'PartiallyPaid' | 'Overdue' | 'Voided';

export interface Charge {
  id: string;
  memberId: string;
  memberName: string;
  kind: ChargeKind;
  description: string;
  amount: number;
  paidAmount: number;
  status: ChargeStatus;
  postedAtUtc: string;
  dueAtUtc?: string;
  paidAtUtc?: string;
  reference?: string;
}

export interface Payment {
  id: string;
  memberId: string;
  memberName: string;
  amount: number;
  method: 'CardOnFile' | 'Cash' | 'Check' | 'Wire' | 'AppliedCredit';
  reference?: string;
  receivedAtUtc: string;
  appliedToChargeIds: string[];
}

export const CHARGE_KINDS: ChargeKind[] = [
  'Dues',
  'Dining',
  'Bar',
  'Event',
  'Spa',
  'Booking',
  'Retail',
  'Other',
];
export const CHARGE_STATUSES: ChargeStatus[] = [
  'Pending',
  'Paid',
  'PartiallyPaid',
  'Overdue',
  'Voided',
];
export const PAYMENT_METHODS: Payment['method'][] = [
  'CardOnFile',
  'Cash',
  'Check',
  'Wire',
  'AppliedCredit',
];

export const PAYMENT_METHOD_LABELS: Record<Payment['method'], string> = {
  CardOnFile: 'Card on file',
  Cash: 'Cash',
  Check: 'Check',
  Wire: 'Wire transfer',
  AppliedCredit: 'House credit',
};

const day = 86_400_000;

const chargeSeed: Charge[] = [
  {
    id: 'chg-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    kind: 'Dining',
    description: 'Chef tasting menu × 2',
    amount: 380,
    paidAmount: 0,
    status: 'Pending',
    postedAtUtc: new Date(Date.now() - 2 * day).toISOString(),
    dueAtUtc: new Date(Date.now() + 12 * day).toISOString(),
    reference: 'BILL-2026-0481',
  },
  {
    id: 'chg-seed-2',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    kind: 'Dues',
    description: 'Platinum monthly dues',
    amount: 599,
    paidAmount: 599,
    status: 'Paid',
    postedAtUtc: new Date(Date.now() - 28 * day).toISOString(),
    paidAtUtc: new Date(Date.now() - 28 * day).toISOString(),
    reference: 'DUES-2026-04',
  },
  {
    id: 'chg-seed-3',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    kind: 'Bar',
    description: 'Library Bar tab',
    amount: 124,
    paidAmount: 50,
    status: 'PartiallyPaid',
    postedAtUtc: new Date(Date.now() - 5 * day).toISOString(),
    dueAtUtc: new Date(Date.now() + 10 * day).toISOString(),
  },
  {
    id: 'chg-seed-4',
    memberId: 'mem-seed-5',
    memberName: 'Sofia Reyes',
    kind: 'Dues',
    description: 'Gold monthly dues — failed',
    amount: 299,
    paidAmount: 0,
    status: 'Overdue',
    postedAtUtc: new Date(Date.now() - 45 * day).toISOString(),
    dueAtUtc: new Date(Date.now() - 18 * day).toISOString(),
    reference: 'DUES-2026-03',
  },
  {
    id: 'chg-seed-5',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    kind: 'Event',
    description: 'Wine Down Wednesday × 2 tickets',
    amount: 70,
    paidAmount: 70,
    status: 'Paid',
    postedAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    paidAtUtc: new Date(Date.now() - 1 * day).toISOString(),
  },
  {
    id: 'chg-seed-6',
    memberId: 'mem-seed-6',
    memberName: 'Jordan Patel',
    kind: 'Spa',
    description: 'Deep-tissue 90 min',
    amount: 215,
    paidAmount: 0,
    status: 'Pending',
    postedAtUtc: new Date(Date.now() - 3 * day).toISOString(),
    dueAtUtc: new Date(Date.now() + 11 * day).toISOString(),
  },
];

const paymentSeed: Payment[] = [
  {
    id: 'pay-seed-1',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    amount: 599,
    method: 'CardOnFile',
    receivedAtUtc: new Date(Date.now() - 28 * day).toISOString(),
    appliedToChargeIds: ['chg-seed-2'],
    reference: 'auth_2N4xF1...A2',
  },
  {
    id: 'pay-seed-2',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    amount: 50,
    method: 'AppliedCredit',
    receivedAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    appliedToChargeIds: ['chg-seed-3'],
  },
  {
    id: 'pay-seed-3',
    memberId: 'mem-seed-3',
    memberName: 'Priya Raman',
    amount: 70,
    method: 'CardOnFile',
    receivedAtUtc: new Date(Date.now() - 1 * day).toISOString(),
    appliedToChargeIds: ['chg-seed-5'],
  },
];

const chargeStore = createMockStore<Charge>({
  storageKey: 'crm.mock.charges.v1',
  seed: chargeSeed,
  idOf: (c) => c.id,
});

const paymentStore = createMockStore<Payment>({
  storageKey: 'crm.mock.payments.v1',
  seed: paymentSeed,
  idOf: (p) => p.id,
});

export async function getCharges(): Promise<Charge[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Charge[]>('/api/charges');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return [...chargeStore.list()].sort(
        (a, b) => Date.parse(b.postedAtUtc) - Date.parse(a.postedAtUtc),
      );
    },
  );
}

export async function getPayments(): Promise<Payment[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Payment[]>('/api/payments');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      return [...paymentStore.list()].sort(
        (a, b) => Date.parse(b.receivedAtUtc) - Date.parse(a.receivedAtUtc),
      );
    },
  );
}

export interface ChargeInput {
  memberId: string;
  memberName: string;
  kind: ChargeKind;
  description: string;
  amount: number;
  dueAtUtc?: string;
  reference?: string;
}

export async function createCharge(input: ChargeInput): Promise<Charge | null> {
  const created = await apiWithFallback(
    () => authFetchJson<Charge>('/api/charges', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(140);
      return chargeStore.add({
        id: mockId('chg'),
        paidAmount: 0,
        status: 'Pending',
        postedAtUtc: new Date().toISOString(),
        ...input,
      });
    },
  );
  if (created) await recomputeMemberBalance(input.memberId);
  return created;
}

export async function deleteCharge(id: string): Promise<boolean> {
  const existing = chargeStore.byId(id);
  const ok = await apiWithFallback(
    async () => {
      const res = await authFetch(`/api/charges/${id}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return true;
    },
    async () => {
      await delay(100);
      return chargeStore.remove(id);
    },
  );
  if (ok && existing) await recomputeMemberBalance(existing.memberId);
  return ok;
}

export interface PaymentInput {
  memberId: string;
  memberName: string;
  amount: number;
  method: Payment['method'];
  reference?: string;
  chargeId?: string;
}

export async function recordPayment(input: PaymentInput): Promise<Payment | null> {
  const created = await apiWithFallback(
    () => authFetchJson<Payment>('/api/payments', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(160);
      const payment = paymentStore.add({
        id: mockId('pay'),
        memberId: input.memberId,
        memberName: input.memberName,
        amount: input.amount,
        method: input.method,
        reference: input.reference,
        receivedAtUtc: new Date().toISOString(),
        appliedToChargeIds: input.chargeId ? [input.chargeId] : [],
      });
      if (input.chargeId) applyPaymentToCharge(input.chargeId, input.amount);
      return payment;
    },
  );
  if (created) await recomputeMemberBalance(input.memberId);
  return created;
}

function applyPaymentToCharge(chargeId: string, amount: number): void {
  const charge = chargeStore.byId(chargeId);
  if (!charge) return;
  const newPaid = Math.min(charge.amount, charge.paidAmount + amount);
  const status: ChargeStatus =
    newPaid >= charge.amount ? 'Paid' : newPaid > 0 ? 'PartiallyPaid' : charge.status;
  const patch: Partial<Charge> = { paidAmount: newPaid, status };
  if (status === 'Paid') patch.paidAtUtc = new Date().toISOString();
  chargeStore.update(chargeId, patch);
}

async function recomputeMemberBalance(memberId: string): Promise<void> {
  const member = (await getMembers()).find((m: Member) => m.id === memberId);
  if (!member) return;
  const charges = chargeStore.list().filter((c) => c.memberId === memberId);
  const outstanding = charges.reduce((sum, c) => {
    if (c.status === 'Voided') return sum;
    return sum + (c.amount - c.paidAmount);
  }, 0);
  // House balance convention: positive = credit, negative = amount owed.
  const newBalance = -Math.round(outstanding * 100) / 100;
  await updateMember(memberId, { houseAccountBalance: newBalance });
}

export interface ChargeStats {
  outstanding: number;
  overdue: number;
  paidThisMonth: number;
  pendingCount: number;
}

export function computeChargeStats(charges: Charge[]): ChargeStats {
  const monthAgo = Date.now() - 30 * day;
  let outstanding = 0;
  let overdue = 0;
  let paidThisMonth = 0;
  let pendingCount = 0;
  for (const c of charges) {
    if (c.status === 'Voided') continue;
    const remaining = c.amount - c.paidAmount;
    if (remaining > 0) outstanding += remaining;
    if (c.status === 'Overdue') overdue += remaining;
    if (c.status === 'PartiallyPaid' || c.status === 'Pending') pendingCount++;
    if (c.status === 'Paid' && c.paidAtUtc && Date.parse(c.paidAtUtc) >= monthAgo) {
      paidThisMonth += c.amount;
    }
  }
  return {
    outstanding: Math.round(outstanding * 100) / 100,
    overdue: Math.round(overdue * 100) / 100,
    paidThisMonth: Math.round(paidThisMonth * 100) / 100,
    pendingCount,
  };
}

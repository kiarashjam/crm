// House-account charges and payments.
//
// Every member has a house account on the server's money ledger: charges (dining bills,
// event tickets, dues, spa, etc.), payments, and the entries that correct them. Entries
// are only ever added: a void or a refund is an entry of its own, and the member's
// `houseAccountBalance` is what the entries add up to (negative when they owe).

import { apiWithFallback, authFetchJson, type RequestOptions } from './apiClient';
import { buildStatement } from '@/app/lib/statement';
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
  /**
   * Charge, or a balance brought over (Opening) or set by hand (Adjustment): what was
   * owed before the ledger is paid like any charge.
   */
  entryType?: 'Charge' | 'Opening' | 'Adjustment';
  kind: ChargeKind;
  description: string;
  amount: number;
  paidAmount: number;
  status: ChargeStatus;
  postedAtUtc: string;
  dueAtUtc?: string;
  paidAtUtc?: string;
  reference?: string;
  /** Set when an owner or manager voided it: the charge stays, and why it was voided with it. */
  voidReason?: string;
  voidedAtUtc?: string;
  voidedByName?: string;
  /**
   * The household member who ran it up, when their bills roll up to this account's
   * holder (the household's primary); left out for the account holder's own charges.
   */
  incurredByMemberId?: string;
  incurredByName?: string;
}

export interface Payment {
  id: string;
  memberId: string;
  memberName: string;
  amount: number;
  method: 'CardOnFile' | 'Card' | 'Twint' | 'DirectDebit' | 'Cash' | 'Check' | 'Wire' | 'AppliedCredit';
  reference?: string;
  receivedAtUtc: string;
  /**
   * Who took the money, when it came from outside: stripe (an online payment, with Stripe's
   * payment id) or bank (a transfer from the bank's file, with the bank's entry reference).
   */
  provider?: string;
  providerPaymentId?: string;
  chargeId?: string;
  appliedToChargeIds: string[];
  /**
   * A voided payment stays listed but pays nothing: its charge owes again what it paid.
   * A refunded one gave part or all of the money back.
   */
  status?: 'Voided' | 'Refunded' | 'PartiallyRefunded';
  refundedAmount?: number;
  voidReason?: string;
  voidedAtUtc?: string;
  voidedByName?: string;
}

/** One entry on a member's statement, with what they owed after it. */
/**
 * Whether a charge is a sale: something the club sold. A balance brought over from before
 * the ledger (Opening) or set by hand (Adjustment) is owed like a charge, but paying it
 * is not revenue.
 */
export function isSale(c: Pick<Charge, 'entryType'>): boolean {
  return (c.entryType ?? 'Charge') === 'Charge';
}

export interface StatementLine {
  id: string;
  atUtc: string;
  entryType: string;
  side: 'charge' | 'payment';
  description: string;
  /** What it added to what was owed. */
  debit: number;
  /** What it took off. */
  credit: number;
  /** The house balance after it: negative while the member owes. */
  balance: number;
  kind?: string;
  method?: string;
  reference?: string;
  reason?: string;
  /** Who in the household ran it up, on a charge rolled up to this account. */
  incurredByMemberId?: string;
  incurredByName?: string;
}

/** A member's entries for [fromUtc, toUtc), from the ledger. Balances are negative while the member owes. */
export interface AccountStatement {
  memberId: string;
  memberName: string;
  currency: string;
  fromUtc: string;
  toUtc: string;
  openingBalance: number;
  closingBalance: number;
  totalDebits: number;
  totalCredits: number;
  lines: StatementLine[];
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
  'Card',
  'Twint',
  'DirectDebit',
  'Cash',
  'Check',
  'Wire',
  'AppliedCredit',
];

export const PAYMENT_METHOD_LABELS: Record<Payment['method'], string> = {
  CardOnFile: 'Card on file',
  Card: 'Card',
  Twint: 'TWINT',
  DirectDebit: 'Direct debit',
  Cash: 'Cash',
  Check: 'Check',
  Wire: 'Wire transfer',
  AppliedCredit: 'House credit',
};

/** Who took a payment from outside the club, as the payments list says it. */
export const PAYMENT_PROVIDER_LABELS: Record<string, string> = {
  stripe: 'Stripe',
  bank: 'Bank file',
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

export async function getCharges(req?: RequestOptions): Promise<Charge[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Charge[]>('/api/charges', { signal: req?.signal });
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

export async function getPayments(req?: RequestOptions): Promise<Payment[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Payment[]>('/api/payments', { signal: req?.signal });
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
  /**
   * A fee, product, ticket or menu item from the price catalogue: the server charges its
   * price, as its charge kind and with its VAT rate, whatever amount or kind is sent.
   */
  catalogItemId?: string;
}

/**
 * Posts a charge. `idempotencyKey` makes a retry safe: the server gives the first charge back
 * for a second request with the same key (a confirm pressed twice, a request sent again after
 * a dropped connection) instead of charging the member twice.
 */
export async function createCharge(input: ChargeInput, options: { idempotencyKey?: string } = {}): Promise<Charge | null> {
  // The server moves the member's balance itself; the demo does it below.
  return apiWithFallback(
    () => authFetchJson<Charge>('/api/charges', {
      method: 'POST',
      body: JSON.stringify(input),
      headers: options.idempotencyKey ? { 'Idempotency-Key': options.idempotencyKey } : undefined,
    }),
    async () => {
      await delay(140);
      const charge = chargeStore.add({
        id: mockId('chg'),
        paidAmount: 0,
        status: 'Pending',
        postedAtUtc: new Date().toISOString(),
        ...input,
      });
      await recomputeMemberBalance(input.memberId);
      return charge;
    },
  );
}

/**
 * Void a charge, with a reason. Charges are never deleted: a voided one stays on the
 * account, saying who voided it and why, and no longer counts towards the balance.
 * On the server only an owner or manager may, and not while payments stand against it.
 */
export async function voidCharge(id: string, reason: string): Promise<Charge | null> {
  return apiWithFallback(
    () => authFetchJson<Charge>(`/api/charges/${id}/void`, { method: 'POST', body: JSON.stringify({ reason }) }),
    async () => {
      await delay(100);
      const existing = chargeStore.byId(id);
      if (!existing) return null;
      const voided = chargeStore.update(id, { status: 'Voided', voidReason: reason, voidedAtUtc: new Date().toISOString() });
      await recomputeMemberBalance(existing.memberId);
      return voided;
    },
  );
}

/** Void a payment, with a reason: its charge owes again what the payment paid. */
export async function voidPayment(id: string, reason: string): Promise<Payment | null> {
  return apiWithFallback(
    () => authFetchJson<Payment>(`/api/payments/${id}/void`, { method: 'POST', body: JSON.stringify({ reason }) }),
    async () => {
      await delay(100);
      const existing = paymentStore.byId(id);
      if (!existing) return null;
      for (const chargeId of existing.appliedToChargeIds) {
        const charge = chargeStore.byId(chargeId);
        if (!charge || charge.status === 'Voided') continue;
        const paidAmount = Math.max(0, charge.paidAmount - existing.amount);
        chargeStore.update(chargeId, { paidAmount, status: paidAmount > 0 ? 'PartiallyPaid' : 'Pending', paidAtUtc: undefined });
      }
      const voided = paymentStore.update(id, { status: 'Voided', voidReason: reason, voidedAtUtc: new Date().toISOString() });
      await recomputeMemberBalance(existing.memberId);
      return voided;
    },
  );
}

/**
 * A member's statement for a period, worked out by the server from the ledger: the
 * opening balance, every entry (charges, payments, credits, voids, refunds) with the
 * balance after it, and the closing balance.
 */
export async function getMemberStatement(memberId: string, fromUtc: string, toUtc: string, req?: RequestOptions): Promise<AccountStatement> {
  return apiWithFallback(
    () => authFetchJson<AccountStatement>(
      `/api/members/${memberId}/statement?from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}`,
      { signal: req?.signal },
    ),
    async () => {
      // The demo's own charges and payments, in the server's shape.
      await delay(80);
      const charges = chargeStore.list().filter((c) => c.memberId === memberId);
      const payments = paymentStore.list().filter((p) => p.memberId === memberId);
      const start = Date.parse(fromUtc);
      const end = Date.parse(toUtc);
      const statement = buildStatement(charges, payments, start, end);
      const entries = [
        ...statement.charges.filter((c) => c.status !== 'Voided').map((c) => ({
          id: c.id, atUtc: c.postedAtUtc, entryType: 'Charge', side: 'charge' as const, description: c.description,
          debit: c.amount, credit: 0, kind: c.kind, reference: c.reference,
        })),
        ...statement.payments.map((p) => ({
          id: p.id, atUtc: p.receivedAtUtc, entryType: 'Payment', side: 'payment' as const,
          description: `Payment (${PAYMENT_METHOD_LABELS[p.method]})`, debit: 0, credit: p.amount, method: p.method, reference: p.reference,
        })),
      ].sort((a, b) => Date.parse(a.atUtc) - Date.parse(b.atUtc));
      let running = -statement.openingBalance;
      const lines = entries.map((line) => {
        running = Math.round((running + line.credit - line.debit) * 100) / 100;
        return { ...line, balance: running };
      });
      return {
        memberId, memberName: charges[0]?.memberName ?? payments[0]?.memberName ?? '', currency: '',
        fromUtc, toUtc, openingBalance: -statement.openingBalance, closingBalance: -statement.closingBalance,
        totalDebits: statement.periodCharges, totalCredits: statement.periodPayments, lines,
      };
    },
  );
}

/**
 * Pay money back to the member, part or all of a payment, with a reason. What the payment
 * left unused goes back first; beyond that its charges owe again. Takes money.void.
 */
export async function refundPayment(id: string, amount: number, reason: string): Promise<Payment | null> {
  return apiWithFallback(
    () => authFetchJson<Payment>(`/api/payments/${id}/refund`, { method: 'POST', body: JSON.stringify({ amount, reason }) }),
    async () => {
      await delay(100);
      const existing = paymentStore.byId(id);
      if (!existing) return null;
      const refundedAmount = Math.round(((existing.refundedAmount ?? 0) + amount) * 100) / 100;
      return paymentStore.update(id, { refundedAmount, status: refundedAmount >= existing.amount ? 'Refunded' : 'PartiallyRefunded' });
    },
  );
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
  // The server settles the charge and moves the balance itself; the demo does it below.
  return apiWithFallback(
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
      await recomputeMemberBalance(input.memberId);
      return payment;
    },
  );
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

/**
 * This browser's demo ledger only. On the server the server keeps the balance: this
 * sums the browser's own charges, which are none there, and used to write back zero.
 */
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
    // An overdue charge is still open: the server now reports late ones as Overdue.
    if (c.status === 'PartiallyPaid' || c.status === 'Pending' || c.status === 'Overdue') pendingCount++;
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

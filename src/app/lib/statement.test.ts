import { describe, expect, it } from 'vitest';
import type { Charge, Payment } from '@/app/api/charges';
import { buildStatement } from './statement';

const charge = (over: Partial<Charge>): Charge => ({
  id: Math.random().toString(36).slice(2),
  memberId: 'm1',
  description: 'Dues',
  category: 'Dues',
  amount: 299,
  paidAmount: 0,
  status: 'Open',
  postedAtUtc: '2026-09-25T10:00:00Z',
  ...over,
} as Charge);

const payment = (over: Partial<Payment>): Payment => ({
  id: Math.random().toString(36).slice(2),
  memberId: 'm1',
  amount: 299,
  method: 'Card',
  receivedAtUtc: '2026-10-05T10:00:00Z',
  ...over,
} as Payment);

const sept = [Date.parse('2026-09-01T00:00:00Z'), Date.parse('2026-10-01T00:00:00Z')] as const;
const oct = [Date.parse('2026-10-01T00:00:00Z'), Date.parse('2026-11-01T00:00:00Z')] as const;

describe('buildStatement', () => {
  it('carries a charge paid the next month over without counting the payment twice', () => {
    const charges = [charge({ paidAmount: 299, status: 'Paid' })];
    const payments = [payment({})];

    const september = buildStatement(charges, payments, ...sept);
    const october = buildStatement(charges, payments, ...oct);

    expect(september.closingBalance).toBe(299);
    expect(october.openingBalance).toBe(299);
    expect(october.periodPayments).toBe(299);
    expect(october.closingBalance).toBe(0);
  });

  it('leaves voided charges out of every total', () => {
    const s = buildStatement([charge({ status: 'Voided', postedAtUtc: '2026-10-02T00:00:00Z' })], [], ...oct);
    expect(s.periodCharges).toBe(0);
    expect(s.closingBalance).toBe(0);
    expect(s.charges).toHaveLength(1);
  });

  it('leaves a voided payment off the statement: the member owes what it would have paid', () => {
    const charges = [charge({ postedAtUtc: '2026-10-02T00:00:00Z' })];
    const bounced = payment({ status: 'Voided', voidReason: 'Cheque bounced' });

    const s = buildStatement(charges, [bounced], ...oct);
    const november = buildStatement(charges, [bounced], Date.parse('2026-11-01T00:00:00Z'), Date.parse('2026-12-01T00:00:00Z'));

    expect(s.payments).toHaveLength(0);
    expect(s.periodPayments).toBe(0);
    expect(s.closingBalance).toBe(299);
    expect(november.openingBalance).toBe(299);
  });

  it('adds up cents exactly', () => {
    const tenCents = Array.from({ length: 3 }, () => charge({ amount: 0.1, postedAtUtc: '2026-10-02T00:00:00Z' }));
    expect(buildStatement(tenCents, [], ...oct).periodCharges).toBe(0.3);
  });
});

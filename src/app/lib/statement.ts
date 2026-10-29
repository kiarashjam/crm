import type { Charge, Payment } from '@/app/api/charges';

export interface Statement {
  charges: Charge[];
  payments: Payment[];
  openingBalance: number;
  periodCharges: number;
  periodPayments: number;
  closingBalance: number;
}

// Summed in cents, so a long ledger of 0.10s does not end up 0.30000000000000004 off.
const cents = (n: number) => Math.round(n * 100);

/**
 * One member's statement for [start, end), built from the ledger as it stood then:
 * opening = charges posted before the period − payments received before it.
 *
 * The opening balance used to subtract each earlier charge's paidAmount as it is
 * today, and then subtract this period's payments again. A charge posted in
 * September and paid in October was taken off twice, so the October statement
 * showed the member a credit they did not have, and September's closing did not
 * match October's opening.
 */
export function buildStatement(charges: Charge[], payments: Payment[], start: number, end: number): Statement {
  const live = charges.filter((c) => c.status !== 'Voided');
  const before = (iso: string) => Date.parse(iso) < start;
  const within = (iso: string) => {
    const t = Date.parse(iso);
    return t >= start && t < end;
  };

  const opening =
    live.filter((c) => before(c.postedAtUtc)).reduce((s, c) => s + cents(c.amount), 0) -
    payments.filter((p) => before(p.receivedAtUtc)).reduce((s, p) => s + cents(p.amount), 0);
  const periodChargeList = charges.filter((c) => within(c.postedAtUtc));
  const periodPaymentList = payments.filter((p) => within(p.receivedAtUtc));
  const charged = periodChargeList.filter((c) => c.status !== 'Voided').reduce((s, c) => s + cents(c.amount), 0);
  const paid = periodPaymentList.reduce((s, p) => s + cents(p.amount), 0);

  return {
    charges: periodChargeList,
    payments: periodPaymentList,
    openingBalance: opening / 100,
    periodCharges: charged / 100,
    periodPayments: paid / 100,
    closingBalance: (opening + charged - paid) / 100,
  };
}

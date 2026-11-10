// How the money screens name what Stripe and the bank's files say.

import type { BankImport, BankTransactionStatus, OnlinePayment, OnlinePaymentStatus } from '@/app/api/payments';

/** Stripe's names for how a member pays, as a member would say them. */
export const METHOD_LABEL: Record<string, string> = {
  card: 'Card',
  twint: 'TWINT',
  sepa_debit: 'SEPA direct debit',
};

export const ONLINE_STATUS_LABEL: Record<OnlinePaymentStatus, string> = {
  open: 'Waiting for the member',
  processing: 'Processing',
  succeeded: 'Paid',
  failed: 'Failed',
  canceled: 'Cancelled',
  expired: 'Expired',
};

export const ONLINE_STATUS_BADGE: Record<OnlinePaymentStatus, string> = {
  open: 'bg-slate-100 text-slate-700 ring-1 ring-slate-200',
  processing: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  succeeded: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  failed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  canceled: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
  expired: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

/** What an online payment was for. */
export const PURPOSE_LABEL: Record<OnlinePayment['purpose'], string> = {
  checkout: 'Pay link',
  autopay: 'Dues charged to the saved card',
  card: 'Saved card, charged by staff',
  setup: 'Saving a card',
};

export const BANK_STATUS_LABEL: Record<BankTransactionStatus, string> = {
  matched: 'Matched by reference',
  unmatched: 'Unmatched',
  allocated: 'Allocated by hand',
  ignored: 'Not a member payment',
};

export const BANK_STATUS_BADGE: Record<BankTransactionStatus, string> = {
  matched: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  unmatched: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  allocated: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  ignored: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

/** A saved card as a member would recognise it: "Visa ending 4242, expires 04/28". */
export function describeCard(card: { type: string; brand?: string; last4?: string; expMonth?: number; expYear?: number }): string {
  const name = card.type === 'card'
    ? (card.brand ? card.brand.charAt(0).toUpperCase() + card.brand.slice(1) : 'Card')
    : METHOD_LABEL[card.type] ?? card.type;
  const ending = card.last4 ? ` ending ${card.last4}` : '';
  const expiry = card.expMonth && card.expYear ? `, expires ${String(card.expMonth).padStart(2, '0')}/${String(card.expYear).slice(-2)}` : '';
  return `${name}${ending}${expiry}`;
}

/** What a bank file's import did, as one sentence. */
export function importSummary(i: Pick<BankImport, 'matched' | 'unmatched' | 'alreadyImported' | 'skipped'>): string {
  const parts = [`${i.matched} matched and recorded`, `${i.unmatched} to allocate`];
  if (i.alreadyImported) parts.push(`${i.alreadyImported} already imported`);
  if (i.skipped) parts.push(`${i.skipped} skipped (debits, reversals, not yet booked)`);
  return `${parts.join(', ')}.`;
}

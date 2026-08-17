// Money formatting and summing, in one place.
//
// Four separate places hardcoded `currency: 'USD'` in an Intl.NumberFormat, so an
// organisation billing in CHF saw dollar signs on the pipeline board, the deal
// line items and the reports. Worse, the sum helpers added every deal's value
// together regardless of currency, producing a headline figure that was not a
// quantity of anything.
//
// There is no exchange rate anywhere in this system, so nothing here converts.
// When a set of deals spans currencies the dominant one is shown and the
// existence of the others is disclosed, because a visibly incomplete total is
// safer than an invisibly wrong one.

import type { Deal } from '@/app/api/types';

/** Digits out of a free-text money field. Never NaN. */
export function parseAmount(v?: string | null): number {
  if (!v) return 0;
  const n = parseFloat(String(v).replace(/[^0-9.-]/g, ''));
  return Number.isFinite(n) ? n : 0;
}

export const DEFAULT_CURRENCY = 'USD';

/** Normalised currency code for a deal. */
export function currencyOf(d: Pick<Deal, 'currency'>): string {
  return (d.currency || DEFAULT_CURRENCY).toUpperCase();
}

/**
 * Format an amount in a given currency.
 *
 * An unrecognised code must not throw — Intl rejects anything that is not a
 * valid ISO 4217 code, and a single bad record should not blank a whole page.
 */
export function formatMoney(amount: number, currency = DEFAULT_CURRENCY): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
    }).format(amount);
  } catch {
    return `${currency} ${Math.round(amount).toLocaleString()}`;
  }
}

export interface CurrencySum {
  currency: string;
  value: number;
  count: number;
}

/** Deal values totalled per currency, largest total first. */
export function sumByCurrency(deals: Pick<Deal, 'value' | 'currency'>[]): CurrencySum[] {
  const m = new Map<string, CurrencySum>();
  for (const d of deals) {
    const currency = currencyOf(d);
    const e = m.get(currency) ?? { currency, value: 0, count: 0 };
    e.value += parseAmount(d.value);
    e.count += 1;
    m.set(currency, e);
  }
  return [...m.values()].sort((a, b) => b.value - a.value);
}

/** The currency most of the money is in. */
export function dominantCurrencyOf(deals: Pick<Deal, 'value' | 'currency'>[]): string {
  return sumByCurrency(deals)[0]?.currency ?? DEFAULT_CURRENCY;
}

/**
 * A single display string for a set of deals.
 *
 * With one currency this is just the total. With several it is the dominant
 * total plus a count of the currencies left out — never a silent addition across
 * them, which is what the old helpers did.
 */
export function formatDealSum(deals: Pick<Deal, 'value' | 'currency'>[]): string {
  const totals = sumByCurrency(deals);
  if (totals.length === 0) return formatMoney(0);
  const [main, ...rest] = totals;
  const head = formatMoney(main!.value, main!.currency);
  return rest.length === 0 ? head : `${head} +${rest.length} other`;
}

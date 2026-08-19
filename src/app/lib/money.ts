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

/**
 * An amount out of a free-text money field, or null when there is no number in it.
 *
 * Deal values are typed by hand, so they arrive in whichever notation the writer
 * uses. The first version of this stripped everything but digits, dots and
 * minus — which read "1.234,56" as 1.23456 and "85.500" as 85.5, understating a
 * European-formatted deal by three orders of magnitude.
 *
 * Where both separators appear the LAST one is the decimal point, which settles
 * "1,234.56" and "1.234,56" without knowing the writer's locale. A lone comma is
 * a decimal point only with one or two digits after it, so "85,500" stays
 * eighty-five thousand. A lone dot with exactly three digits after and at most
 * three before is grouped thousands, so "85.500" is also eighty-five thousand
 * while "1234.567" keeps its decimal.
 *
 * Mirrors `MoneyText.TryParseAmount` on the server, which computes the same
 * totals from the same column. The two are tested against the same cases.
 */
export function tryParseAmount(v?: string | null): number | null {
  if (v === null || v === undefined) return null;
  const raw = String(v);
  const trimmed = raw.trim();
  if (trimmed.length === 0) return null;
  // Only a leading minus, or accounting parentheses, is a negative. Matching '-'
  // anywhere would turn a range like "1000-2000" into a negative amount.
  const negative = trimmed.startsWith('-') || (trimmed.startsWith('(') && trimmed.endsWith(')'));

  const kept = raw.replace(/[^0-9.,]/g, '');
  if (!/[0-9]/.test(kept)) return null;

  const lastDot = kept.lastIndexOf('.');
  const lastComma = kept.lastIndexOf(',');
  let normalised: string;
  if (lastDot >= 0 && lastComma >= 0) {
    const at = Math.max(lastDot, lastComma);
    const whole = kept.slice(0, at).replace(/[.,]/g, '');
    const frac = kept.slice(at + 1).replace(/[.,]/g, '');
    normalised = frac.length === 0 ? whole : `${whole}.${frac}`;
  } else if (lastComma >= 0) {
    const frac = kept.slice(lastComma + 1);
    normalised = frac.length === 1 || frac.length === 2
      ? `${kept.slice(0, lastComma).replace(/,/g, '')}.${frac}`
      : kept.replace(/,/g, '');
  } else if (lastDot >= 0) {
    const frac = kept.slice(lastDot + 1);
    const intPart = kept.slice(0, lastDot);
    const groupedThousands = frac.length === 3
      && kept.indexOf('.') === lastDot
      && intPart.length > 0 && intPart.length <= 3;
    normalised = groupedThousands
      ? kept.replace(/\./g, '')
      : `${intPart.replace(/\./g, '')}.${frac}`;
  } else {
    normalised = kept;
  }

  const n = Number(normalised);
  if (!Number.isFinite(n)) return null;
  return negative ? -n : n;
}

/**
 * The same amount, with an unreadable value treated as zero.
 *
 * Kept for the many places that only need a number to add up. Anywhere the
 * difference between "worth nothing" and "we could not read it" matters — a
 * headline total, a forecast — use `tryParseAmount` and report the gap.
 */
export function parseAmount(v?: string | null): number {
  return tryParseAmount(v) ?? 0;
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

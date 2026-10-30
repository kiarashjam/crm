// Money: reading what someone types into an amount field, writing an amount in a
// workspace's currency and locale, and adding deals up per currency.
//
// A deal's value used to be free text, and the board, the Reports page, the dashboard
// and the company totals each read it with a parser of their own. They disagreed:
// "1.234,56" was 1234.56 on one page and 1.23 on another, and the board added francs
// and euros into one "CHF" figure. The server now keeps every deal's amount as a
// number to the cent (Deal.amount) in an ISO currency, and every page here reads that
// number. Text is read in one place only: what a person types (parseMoneyInput).
//
// The workspace says what it works in (its currency, locale and time zone, set by an
// owner or manager in Settings), and useMoney formats through it. There is no
// exchange rate anywhere, so nothing here converts: amounts in different currencies
// are totalled apart and shown apart.

import type { Deal } from '@/app/api/types';

export const DEFAULT_CURRENCY = 'CHF';
export const DEFAULT_LOCALE = 'de-CH';
export const DEFAULT_TIME_ZONE = 'Europe/Zurich';

/** The currencies the deal and settings pickers offer. A record in another code is still shown in it. */
export const CURRENCIES: { code: string; name: string }[] = [
  { code: 'CHF', name: 'Swiss franc' },
  { code: 'EUR', name: 'Euro' },
  { code: 'USD', name: 'US dollar' },
  { code: 'GBP', name: 'British pound' },
  { code: 'CAD', name: 'Canadian dollar' },
  { code: 'AUD', name: 'Australian dollar' },
  { code: 'JPY', name: 'Japanese yen' },
];

/** The picker's currencies, with `current` added when a record is in one the list does not have. */
export function currencyOptions(current?: string | null): { code: string; name: string }[] {
  const code = normaliseCurrency(current, '');
  return code && !CURRENCIES.some((c) => c.code === code) ? [...CURRENCIES, { code, name: code }] : CURRENCIES;
}

/** What a workspace works in. */
export interface Regional {
  /** ISO 4217 code new deals are priced in and club amounts are shown in. */
  currency: string;
  /** BCP 47 name amounts and dates are written for ("de-CH"). */
  locale: string;
  /** IANA zone the workspace's days and months begin in ("Europe/Zurich"). */
  timeZone: string;
}

/** A currency code, upper-cased, or `fallback` when the text is not three letters. */
export function normaliseCurrency(code?: string | null, fallback: string = DEFAULT_CURRENCY): string {
  const c = String(code ?? '').trim().toUpperCase();
  return /^[A-Z]{3}$/.test(c) ? c : fallback;
}

function knownLocale(locale?: string | null): string | null {
  if (!locale) return null;
  try {
    return Intl.NumberFormat.supportedLocalesOf([locale]).length > 0 ? locale : null;
  } catch {
    return null;
  }
}

function knownZone(zone?: string | null): string | null {
  if (!zone) return null;
  try {
    new Intl.DateTimeFormat('en', { timeZone: zone });
    return zone;
  } catch {
    return null;
  }
}

/**
 * A workspace's settings, with the defaults (CHF, de-CH, Europe/Zurich) for anything it
 * does not say, or says in a way this browser cannot use: one bad setting must not
 * stop a page from rendering.
 */
export function regionalOf(org?: { currency?: string | null; locale?: string | null; timeZone?: string | null } | null): Regional {
  return {
    currency: normaliseCurrency(org?.currency),
    locale: knownLocale(org?.locale) ?? DEFAULT_LOCALE,
    timeZone: knownZone(org?.timeZone) ?? DEFAULT_TIME_ZONE,
  };
}

/**
 * Cents on an amount: 'auto' shows them only when there are any ("CHF 85’500" but
 * "CHF 99.40"), 'always' as the currency has them (statements, tabs), 'never' for
 * headline figures that count up and would flicker through decimals on the way.
 */
export type Cents = 'auto' | 'always' | 'never';

/**
 * An amount in a currency, written for a locale.
 *
 * A value that rounds to zero shows as zero rather than "-CHF 0", and a code Intl
 * does not know falls back to "XYZ 12.50" instead of throwing, so one bad record
 * cannot blank a page.
 */
export function formatMoney(
  amount: number,
  currency: string = DEFAULT_CURRENCY,
  options: { locale?: string; cents?: Cents } = {},
): string {
  const value = Number.isFinite(amount) ? amount : 0;
  const shown = Math.abs(value) < 0.005 ? 0 : value;
  const cents = options.cents ?? 'auto';
  const whole = cents === 'never' || (cents === 'auto' && Math.round(shown * 100) % 100 === 0);
  try {
    return new Intl.NumberFormat(options.locale || DEFAULT_LOCALE, {
      style: 'currency',
      currency,
      ...(whole ? { minimumFractionDigits: 0, maximumFractionDigits: 0 } : {}),
    }).format(shown);
  } catch {
    return `${currency} ${whole ? Math.round(shown) : shown.toFixed(2)}`;
  }
}

/** An amount to the cent, as the server stores it. */
export function roundCents(amount: number): number {
  return Math.round(amount * 100) / 100;
}

/**
 * What someone typed into an amount field, as a number to the cent, or null when
 * there is no number in it.
 *
 * People type amounts in whichever notation they use: "85,500", "CHF 85'500",
 * "1.234,56", "$1,234.56". Where both separators appear the LAST one is the decimal
 * point, which settles "1,234.56" and "1.234,56" without knowing the writer's locale.
 * A lone comma is a decimal point only with one or two digits after it, so "85,500"
 * stays eighty-five thousand. A lone dot with exactly three digits after and at most
 * three before is grouped thousands, so "85.500" is also eighty-five thousand while
 * "1234.567" keeps its decimal.
 *
 * The same rules as `MoneyText.TryParseAmount` on the server, which read the deals
 * typed before amounts were numbers, and tested against the same cases.
 */
export function parseMoneyInput(v?: string | null): number | null {
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
  return roundCents(negative ? -n : n);
}

/** An amount as an amount field shows it for editing: "85500", "99.40", or empty for none. */
export function amountInput(amount?: number | null): string {
  if (amount === null || amount === undefined || !Number.isFinite(amount)) return '';
  return Number.isInteger(amount) ? String(amount) : amount.toFixed(2);
}

/** What an amount field says when what was typed holds no number. */
export const AMOUNT_HINT = "Enter the amount as a number, such as 85500 or 85'500.50.";

/** A deal's amount as a number, or null for one saved without a readable number. */
export function amountOf(deal: Pick<Deal, 'amount'>): number | null {
  return typeof deal.amount === 'number' && Number.isFinite(deal.amount) ? deal.amount : null;
}

/** A deal's currency: its own code, or the workspace's when it has none. */
export function currencyOf(deal: { currency?: string | null }, fallback: string = DEFAULT_CURRENCY): string {
  return normaliseCurrency(deal.currency, fallback);
}

/** A deal's amount in its own currency, or "No amount" for one saved without a readable number. */
export function formatDealAmount(deal: Pick<Deal, 'amount' | 'currency'>, regional: Pick<Regional, 'currency' | 'locale'>): string {
  if (deal.amount === null || deal.amount === undefined) return 'No amount';
  return formatMoney(deal.amount, currencyOf(deal, regional.currency), { locale: regional.locale });
}

export interface CurrencySum {
  currency: string;
  value: number;
  /** Deals with an amount, the ones in `value`. */
  count: number;
}

/**
 * Deal amounts totalled per currency, largest first, ties by code so the order cannot
 * flip between renders. A deal with no amount adds nothing and is not counted; the
 * caller can tell how many there were from the deals it passed.
 */
export function sumByCurrency(deals: Pick<Deal, 'amount' | 'currency'>[], fallback: string = DEFAULT_CURRENCY): CurrencySum[] {
  const m = new Map<string, CurrencySum>();
  for (const d of deals) {
    if (d.amount === null || d.amount === undefined || !Number.isFinite(d.amount)) continue;
    const currency = currencyOf(d, fallback);
    const e = m.get(currency) ?? { currency, value: 0, count: 0 };
    e.value = roundCents(e.value + d.amount);
    e.count += 1;
    m.set(currency, e);
  }
  return [...m.values()].sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
}

/**
 * Totals per currency as one line, each in its own currency: "CHF 40’000 · EUR 30’000".
 * Nothing at all is zero in the workspace's currency.
 */
export function formatTotals(totals: Pick<CurrencySum, 'currency' | 'value'>[], regional: Pick<Regional, 'currency' | 'locale'>, cents: Cents = 'auto'): string {
  if (totals.length === 0) return formatMoney(0, regional.currency, { locale: regional.locale, cents });
  return totals.map((t) => formatMoney(t.value, t.currency, { locale: regional.locale, cents })).join(' · ');
}

/** A set of deals' amounts, totalled per currency, as one line. */
export function formatDealSum(deals: Pick<Deal, 'amount' | 'currency'>[], regional: Pick<Regional, 'currency' | 'locale'>): string {
  return formatTotals(sumByCurrency(deals, regional.currency), regional);
}

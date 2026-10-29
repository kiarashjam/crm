// Club amounts, in the club's own currency and to the cent.
//
// Every club page had its own formatter with `currency: 'USD'` written into it, so a
// Swiss club's printed statements, tab totals, gift cards and dues all showed dollar
// signs. Some of them also rounded to whole units: a member owing 320.50 read "$321"
// on the members list and "$320.50" on the statement, and a balance of -0.40 showed as
// a red "-$0". The currency is now one setting (Hospitality settings), and every club
// page formats through here, with the currency's own minor units.
//
// There is no exchange rate anywhere: choosing another currency relabels the amounts,
// it does not convert them.

export const DEFAULT_CLUB_CURRENCY = 'CHF';

/** The currencies the settings page offers. */
export const CLUB_CURRENCIES: { code: string; name: string }[] = [
  { code: 'CHF', name: 'Swiss franc' },
  { code: 'EUR', name: 'Euro' },
  { code: 'GBP', name: 'British pound' },
  { code: 'USD', name: 'US dollar' },
];

/** A stored currency code, upper-cased, or the default when it is not a code at all. */
export function normaliseCurrency(code?: string | null): string {
  const c = String(code ?? '').trim().toUpperCase();
  return /^[A-Z]{3}$/.test(c) ? c : DEFAULT_CLUB_CURRENCY;
}

/**
 * An amount in the club currency, with the currency's own decimals (two for CHF).
 *
 * A value that rounds to zero shows as zero rather than "-CHF 0.00", and a code Intl
 * does not know falls back to "XYZ 12.50" instead of throwing, so one bad setting
 * cannot blank a page.
 */
export function formatClubMoney(amount: number, currency: string = DEFAULT_CLUB_CURRENCY): string {
  const value = Number.isFinite(amount) ? amount : 0;
  const shown = Math.abs(value) < 0.005 ? 0 : value;
  try {
    return new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(shown);
  } catch {
    return `${currency} ${shown.toFixed(2)}`;
  }
}

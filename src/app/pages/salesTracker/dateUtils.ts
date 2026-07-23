// Excel uses dd.mm.yyyy strings — mirror that format so what a user types
// in the app can round-trip back through import/export unchanged.

const DDMMYYYY = /^(\d{2})\.(\d{2})\.(\d{4})$/;

export function parseDdMmYyyy(value: string | undefined | null): Date | null {
  if (!value) return null;
  const m = DDMMYYYY.exec(value.trim());
  if (!m) return null;
  const day = Number(m[1]);
  const month = Number(m[2]);
  const year = Number(m[3]);
  if (month < 1 || month > 12 || day < 1 || day > 31) return null;
  const d = new Date(Date.UTC(year, month - 1, day));
  return Number.isNaN(d.getTime()) ? null : d;
}

export function formatDdMmYyyy(date: Date | null | undefined): string {
  if (!date || Number.isNaN(date.getTime())) return '';
  const day = String(date.getUTCDate()).padStart(2, '0');
  const month = String(date.getUTCMonth() + 1).padStart(2, '0');
  const year = date.getUTCFullYear();
  return `${day}.${month}.${year}`;
}

/** Convert an <input type="date"> yyyy-mm-dd value to dd.mm.yyyy (returns '' for empty). */
export function isoToDdMmYyyy(iso: string): string {
  if (!iso) return '';
  const [year, month, day] = iso.split('-');
  if (!year || !month || !day) return '';
  return `${day}.${month}.${year}`;
}

/** Convert a dd.mm.yyyy value to yyyy-mm-dd for <input type="date">. */
export function ddMmYyyyToIso(value: string): string {
  const m = DDMMYYYY.exec((value ?? '').trim());
  if (!m) return '';
  return `${m[3]}-${m[2]}-${m[1]}`;
}

/** Excel's `WEEKNUM(date, 2)`: weeks start on Monday and week 1 is the week
 *  containing January 1 (NOT the ISO 8601 Thursday rule). This matches the
 *  source workbook's `P4 = WEEKNUM(F4, 2)` and the weekly-meetings table's
 *  `WEEKNUM(TODAY(), 2) - N` labels. */
export function excelWeekNumber(date: Date): number {
  const year = date.getUTCFullYear();
  const jan1 = new Date(Date.UTC(year, 0, 1));
  const daysFromJan1 = Math.floor((date.getTime() - jan1.getTime()) / 86400000);
  // JS getUTCDay(): Sun=0..Sat=6. Convert to Monday=0..Sunday=6.
  const jan1WeekdayMondayStart = (jan1.getUTCDay() + 6) % 7;
  return Math.floor((daysFromJan1 + jan1WeekdayMondayStart) / 7) + 1;
}

export function daysBetween(from: Date, to: Date): number {
  const ms = to.getTime() - from.getTime();
  return Math.floor(ms / 86400000);
}

/** Today at 00:00 UTC so day-count math is stable across timezones. */
export function todayUtc(): Date {
  const now = new Date();
  return new Date(Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate()));
}

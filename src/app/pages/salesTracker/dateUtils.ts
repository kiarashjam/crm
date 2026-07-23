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

/** ISO week number (WEEKNUM(date, 2) — Monday start). */
export function isoWeekNumber(date: Date): number {
  const d = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth(), date.getUTCDate()));
  const dayNum = d.getUTCDay() || 7;
  d.setUTCDate(d.getUTCDate() + 4 - dayNum);
  const yearStart = new Date(Date.UTC(d.getUTCFullYear(), 0, 1));
  return Math.ceil(((d.getTime() - yearStart.getTime()) / 86400000 + 1) / 7);
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

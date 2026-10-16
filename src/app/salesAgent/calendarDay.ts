// Calendar days for the sales agent.
//
// A bare "2026-10-16" is a day, not a moment. Parsing it as UTC midnight would
// show the previous day anywhere west of Greenwich. A real timestamp uses the
// local calendar day of that instant, which is how the task list and the
// pipeline board already read due dates.

const DATE_ONLY = /^(\d{4}-\d{2}-\d{2})$/;

/** The calendar day a saved date refers to, as YYYY-MM-DD, or null. */
export function calendarKey(iso: string | null | undefined): string | null {
  if (!iso) return null;
  const trimmed = iso.trim();
  const dateOnly = DATE_ONLY.exec(trimmed);
  if (dateOnly) return dateOnly[1];
  const parsed = new Date(trimmed);
  if (Number.isNaN(parsed.getTime())) return null;
  const month = String(parsed.getMonth() + 1).padStart(2, '0');
  const day = String(parsed.getDate()).padStart(2, '0');
  return `${parsed.getFullYear()}-${month}-${day}`;
}

/** Today's calendar day in the running environment. */
export function todayKey(now: Date): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

/** Whole days from `earlier` to `later`. Positive when later is after earlier. */
export function daysBetween(earlier: string, later: string): number {
  const [y1, m1, d1] = earlier.split('-').map(Number);
  const [y2, m2, d2] = later.split('-').map(Number);
  const start = Date.UTC(y1, m1 - 1, d1);
  const end = Date.UTC(y2, m2 - 1, d2);
  return Math.round((end - start) / 86_400_000);
}

/** A calendar day shifted by whole days, still YYYY-MM-DD. */
export function shiftKey(key: string, days: number): string {
  const [year, month, day] = key.split('-').map(Number);
  const shifted = new Date(Date.UTC(year, month - 1, day + days));
  const m = String(shifted.getUTCMonth() + 1).padStart(2, '0');
  const d = String(shifted.getUTCDate()).padStart(2, '0');
  return `${shifted.getUTCFullYear()}-${m}-${d}`;
}

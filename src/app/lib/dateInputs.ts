// A datetime-local input shows and returns local wall-clock time ("2026-10-28T15:00");
// the API stores and returns UTC ("2026-10-28T14:00:00Z"). Slicing the UTC string
// straight into the input showed UTC as if it were local, and saving it back moved
// the time by the user's offset on every edit.

const pad = (n: number) => String(n).padStart(2, '0');

/** An API date, as the local "YYYY-MM-DDTHH:mm" a datetime-local input expects. */
export function toLocalInputValue(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** A datetime-local input's value (local time), as the UTC ISO string the API takes. */
export function fromLocalInputValue(value?: string | null): string | undefined {
  if (!value) return undefined;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString();
}

/**
 * A date input's day ("2026-12-31"), as the UTC instant that day ends where the user is.
 *
 * A due date or an expiry names a whole day. Sent as `new Date(day)`, it was that
 * day's first moment in UTC, and the server now compares it with the clock: a gift
 * card "expiring 31 Dec" was refused all through 31 Dec, and a charge due today read
 * as overdue the moment it was posted.
 */
export function endOfLocalDay(value?: string | null): string | undefined {
  const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value ?? '');
  if (!m) return undefined;
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]), 23, 59, 59, 999);
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString();
}

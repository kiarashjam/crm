// The journal's period: whole days in the workspace's time zone, as the instants the server
// takes ([from, to)), and the month a bookkeeper most often exports, the last one.

import { startOfDayIn } from '@/app/lib/zonedTime';

/** The month before the one `todayKey` (YYYY-MM-DD) is in, as its first and last days. */
export function lastMonth(todayKey: string): { from: string; to: string } {
  const [y, m] = todayKey.split('-').map(Number) as [number, number];
  const first = new Date(Date.UTC(y, m - 2, 1));
  const last = new Date(Date.UTC(y, m - 1, 0));
  const key = (d: Date) => d.toISOString().slice(0, 10);
  return { from: key(first), to: key(last) };
}

/** The instants a period of whole days covers in `timeZone`: from the first day's start to the day after the last. */
export function periodIn(from: string, to: string, timeZone: string): { fromUtc: string; toUtc: string } | null {
  const a = /^(\d{4})-(\d{2})-(\d{2})$/.exec(from);
  const b = /^(\d{4})-(\d{2})-(\d{2})$/.exec(to);
  if (!a || !b) return null;
  const start = startOfDayIn(Number(a[1]), Number(a[2]), Number(a[3]), timeZone);
  const end = startOfDayIn(Number(b[1]), Number(b[2]), Number(b[3]) + 1, timeZone);
  return end > start ? { fromUtc: start.toISOString(), toUtc: end.toISOString() } : null;
}

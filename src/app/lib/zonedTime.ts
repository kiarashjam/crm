// Days and months on a workspace's calendar.
//
// The browser's clock and zone are the viewer's, not the workspace's. A manager in
// New York looking at a Zurich club's figures got the club's months cut at New York's
// midnight, and a deal won in Zurich at half past midnight on the 1st counted in the
// month before. These read an instant on the calendar of an IANA zone (the
// workspace's, from useRegional) with Intl, which every browser has.

const formatters = new Map<string, Intl.DateTimeFormat>();

function partsFormatter(timeZone: string): Intl.DateTimeFormat {
  let f = formatters.get(timeZone);
  if (!f) {
    f = new Intl.DateTimeFormat('en-US', {
      timeZone,
      hourCycle: 'h23',
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
    formatters.set(timeZone, f);
  }
  return f;
}

export interface ZonedParts {
  year: number;
  /** 1 to 12. */
  month: number;
  day: number;
  hour: number;
  minute: number;
  second: number;
}

/** The wall-clock date and time an instant is in `timeZone`. */
export function zonedParts(instant: Date, timeZone: string): ZonedParts {
  const parts: Record<string, number> = {};
  for (const p of partsFormatter(timeZone).formatToParts(instant)) {
    if (p.type !== 'literal') parts[p.type] = Number(p.value);
  }
  return {
    year: parts.year ?? 1970,
    month: parts.month ?? 1,
    day: parts.day ?? 1,
    hour: parts.hour ?? 0,
    minute: parts.minute ?? 0,
    second: parts.second ?? 0,
  };
}

const pad = (n: number) => String(n).padStart(2, '0');

/** "YYYY-MM-DD" of a calendar day. */
export function dayKey(year: number, month: number, day: number): string {
  const d = new Date(Date.UTC(year, month - 1, day));
  return `${d.getUTCFullYear()}-${pad(d.getUTCMonth() + 1)}-${pad(d.getUTCDate())}`;
}

/** The "YYYY-MM-DD" an instant falls on in `timeZone`. */
export function dayKeyIn(instant: Date | string, timeZone: string): string {
  const p = zonedParts(typeof instant === 'string' ? new Date(instant) : instant, timeZone);
  return dayKey(p.year, p.month, p.day);
}

/** Today, as "YYYY-MM-DD", in `timeZone`. */
export function todayKeyIn(timeZone: string, now: Date = new Date()): string {
  return dayKeyIn(now, timeZone);
}

/**
 * The instant a calendar day begins in `timeZone`. The day may be given out of range
 * (day 0, month 13) and is rolled over the way Date.UTC rolls it.
 */
export function startOfDayIn(year: number, month: number, day: number, timeZone: string): Date {
  const target = Date.UTC(year, month - 1, day);
  let t = target;
  // Midnight there is UTC midnight less the zone's offset; the offset is read at the
  // guess and read again, for the days the clocks change.
  for (let i = 0; i < 3; i++) {
    const p = zonedParts(new Date(t), timeZone);
    const wall = Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second);
    const off = wall - target;
    if (off === 0) break;
    t -= off;
  }
  return new Date(t);
}

/**
 * The calendar day of a date-only value, such as an expected close date, which is
 * stored as the UTC midnight of the day that was picked. Read in a zone west of UTC
 * it would land on the day before. Empty for text that is no date at all, rather than
 * an error that would blank the page showing it.
 */
export function dateOnlyKey(iso: string): string {
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(iso);
  if (m) return `${m[1]}-${m[2]}-${m[3]}`;
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : dayKeyIn(d, 'UTC');
}

const utcOf = (key: string) => Date.UTC(Number(key.slice(0, 4)), Number(key.slice(5, 7)) - 1, Number(key.slice(8, 10)));

/** Whole calendar days from one "YYYY-MM-DD" to another: 1 from today to tomorrow. */
export function daysBetween(fromKey: string, toKey: string): number {
  return Math.round((utcOf(toKey) - utcOf(fromKey)) / 86_400_000);
}

/** The day of the week of a "YYYY-MM-DD", 0 for Sunday. */
export function weekdayOf(key: string): number {
  return new Date(utcOf(key)).getUTCDay();
}

/**
 * A "YYYY-MM-DD", or a date-only value, written as the calendar day it is, in the
 * viewer's language unless `locale` says otherwise.
 */
export function formatDay(keyOrIso: string, options: Intl.DateTimeFormatOptions, locale?: string): string {
  return new Date(utcOf(dateOnlyKey(keyOrIso))).toLocaleDateString(locale, { ...options, timeZone: 'UTC' });
}

/**
 * The instant a wall-clock time on a calendar day is in `timeZone`: "2026-11-20" at "18:30"
 * in Europe/Zurich is 17:30 UTC. A time the clocks skip or pass twice, on the nights they
 * change, comes out an hour to one side. Null for a day or time that cannot be read.
 */
export function instantIn(dayKeyValue: string, time: string, timeZone: string): Date | null {
  const d = /^(\d{4})-(\d{2})-(\d{2})$/.exec(dayKeyValue);
  const t = /^(\d{1,2}):(\d{2})$/.exec(time);
  if (!d || !t) return null;
  const hour = Number(t[1]);
  const minute = Number(t[2]);
  if (hour > 23 || minute > 59) return null;
  const target = Date.UTC(Number(d[1]), Number(d[2]) - 1, Number(d[3]), hour, minute);
  const offsetAt = (instant: number) => {
    const p = zonedParts(new Date(instant), timeZone);
    return Date.UTC(p.year, p.month - 1, p.day, p.hour, p.minute, p.second) - instant;
  };
  // The zone's offset at the guess, and again at the answer, where the clocks changed between.
  const first = offsetAt(target);
  const second = offsetAt(target - first);
  return new Date(target - second);
}

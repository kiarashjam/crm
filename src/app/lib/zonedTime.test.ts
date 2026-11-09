// Days and months on the workspace's calendar, whatever zone the browser is in.

import { describe, expect, it } from 'vitest';
import { dateOnlyKey, dayKey, dayKeyIn, daysBetween, formatDay, startOfDayIn, todayKeyIn, weekdayOf, zonedParts } from './zonedTime';

describe('the day an instant falls on', () => {
  const late = new Date('2026-10-31T23:30:00Z');

  it('is the zone\'s, not this browser\'s', () => {
    expect(dayKeyIn(late, 'Europe/Zurich')).toBe('2026-11-01');
    expect(dayKeyIn(late, 'America/New_York')).toBe('2026-10-31');
    expect(dayKeyIn('2026-10-31T23:30:00Z', 'UTC')).toBe('2026-10-31');
    expect(todayKeyIn('Europe/Zurich', late)).toBe('2026-11-01');
    expect(zonedParts(late, 'Europe/Zurich')).toMatchObject({ year: 2026, month: 11, day: 1, hour: 0, minute: 30 });
  });

  it('rolls a day given out of range over, as Date.UTC does', () => {
    expect(dayKey(2026, 13, 1)).toBe('2027-01-01');
    expect(dayKey(2026, 3, 0)).toBe('2026-02-28');
  });
});

describe('startOfDayIn', () => {
  it("is the zone's midnight, in summer and in winter time", () => {
    expect(startOfDayIn(2026, 10, 1, 'Europe/Zurich').toISOString()).toBe('2026-09-30T22:00:00.000Z');
    expect(startOfDayIn(2026, 11, 1, 'Europe/Zurich').toISOString()).toBe('2026-10-31T23:00:00.000Z');
    expect(startOfDayIn(2026, 11, 1, 'America/New_York').toISOString()).toBe('2026-11-01T04:00:00.000Z');
    expect(startOfDayIn(2026, 11, 2, 'America/New_York').toISOString()).toBe('2026-11-02T05:00:00.000Z');
  });

  it('rolls months over, so month 0 is last December', () => {
    expect(startOfDayIn(2026, 0, 1, 'Europe/Zurich').toISOString()).toBe('2025-11-30T23:00:00.000Z');
  });
});

describe('dateOnlyKey', () => {
  it('keeps the day a close date was picked for, read in a zone west of UTC', () => {
    // Stored as the UTC midnight of the day picked; New York would read the day before.
    expect(dateOnlyKey('2026-11-01T00:00:00Z')).toBe('2026-11-01');
    expect(dayKeyIn('2026-11-01T00:00:00Z', 'America/New_York')).toBe('2026-10-31');
  });
});

describe('counting calendar days', () => {
  it('counts whole days between two days, across a change of the clocks', () => {
    expect(daysBetween('2026-10-31', '2026-11-01')).toBe(1);
    expect(daysBetween('2026-11-01', '2026-10-31')).toBe(-1);
    // Zurich puts its clocks back on 25 October; the days still count as days.
    expect(daysBetween('2026-10-24', '2026-10-26')).toBe(2);
    expect(daysBetween('2026-12-31', '2027-01-01')).toBe(1);
  });

  it('names the day of the week of a day, Sunday first', () => {
    expect(weekdayOf('2026-11-01')).toBe(0);
    expect(weekdayOf('2026-11-07')).toBe(6);
  });
});

describe('formatDay', () => {
  it('writes a close date as the day that was picked, in a zone west of UTC too', () => {
    // A close date is stored as the UTC midnight of its day. Written in New York's
    // zone, as toLocaleDateString did, it showed the day before.
    expect(formatDay('2026-11-01T00:00:00Z', { day: 'numeric', month: 'short', year: 'numeric' }, 'en-GB')).toBe('1 Nov 2026');
    expect(formatDay('2026-11-01', { month: 'long', year: 'numeric' }, 'en-GB')).toBe('November 2026');
  });
});

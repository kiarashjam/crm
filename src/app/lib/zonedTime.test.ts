// Days and months on the workspace's calendar, whatever zone the browser is in.

import { describe, expect, it } from 'vitest';
import { dateOnlyKey, dayKey, dayKeyIn, startOfDayIn, todayKeyIn, zonedParts } from './zonedTime';

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

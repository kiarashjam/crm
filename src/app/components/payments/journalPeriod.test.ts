// The journal's period: whole days in the workspace's time zone, so a booking at 00:30 on the
// first of the month in Zurich is in that month, not the one before.

import { describe, expect, it } from 'vitest';
import { lastMonth, periodIn } from './journalPeriod';

describe('the journal period', () => {
  it('starts as last month, across a year end too', () => {
    expect(lastMonth('2026-11-10')).toEqual({ from: '2026-10-01', to: '2026-10-31' });
    expect(lastMonth('2026-01-03')).toEqual({ from: '2025-12-01', to: '2025-12-31' });
    expect(lastMonth('2028-03-01')).toEqual({ from: '2028-02-01', to: '2028-02-29' });
  });

  it("covers whole days in the club's time zone, through a change of the clocks", () => {
    expect(periodIn('2026-10-01', '2026-10-31', 'Europe/Zurich')).toEqual({
      fromUtc: '2026-09-30T22:00:00.000Z',
      toUtc: '2026-10-31T23:00:00.000Z',
    });
    expect(periodIn('2026-11-10', '2026-11-10', 'UTC')).toEqual({ fromUtc: '2026-11-10T00:00:00.000Z', toUtc: '2026-11-11T00:00:00.000Z' });
  });

  it('is no period when it ends before it starts, or a day is not a date', () => {
    expect(periodIn('2026-10-31', '2026-10-01', 'Europe/Zurich')).toBeNull();
    expect(periodIn('', '2026-10-01', 'Europe/Zurich')).toBeNull();
  });
});

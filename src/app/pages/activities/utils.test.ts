// The headings the Activities page lists activities under, on the workspace's calendar.

import { describe, expect, it } from 'vitest';
import { getDateGroup } from './utils';

describe("an activity's heading", () => {
  // Half past midnight on Sunday 1 November in Zurich; still Saturday 31 October in
  // UTC and in New York.
  const now = new Date('2026-10-31T23:30:00Z');

  it("is the day it happened at the club, not in this browser", () => {
    // A call at 00:10 Zurich time on the 1st. In a browser west of the club it was
    // listed under yesterday, while the reports counted it on the 1st.
    const call = '2026-10-31T23:10:00Z';
    expect(getDateGroup(call, 'Europe/Zurich', now)).toBe('Today');
    // Late on the 31st in Zurich is yesterday there.
    expect(getDateGroup('2026-10-31T21:00:00Z', 'Europe/Zurich', now)).toBe('Yesterday');
    // The same instants in New York are both still on the 31st.
    expect(getDateGroup(call, 'America/New_York', now)).toBe('Today');
    expect(getDateGroup('2026-10-31T21:00:00Z', 'America/New_York', now)).toBe('Today');
  });

  it('counts this week and last week from Sunday, as the page did', () => {
    // Wednesday 4 November in Zurich.
    const wednesday = new Date('2026-11-04T10:00:00Z');
    expect(getDateGroup('2026-11-03T10:00:00Z', 'Europe/Zurich', wednesday)).toBe('Yesterday');
    expect(getDateGroup('2026-11-01T10:00:00Z', 'Europe/Zurich', wednesday)).toBe('This Week');
    expect(getDateGroup('2026-10-31T10:00:00Z', 'Europe/Zurich', wednesday)).toBe('Last Week');
    expect(getDateGroup('2026-10-25T10:00:00Z', 'Europe/Zurich', wednesday)).toBe('Last Week');
    expect(getDateGroup('2026-10-24T10:00:00Z', 'Europe/Zurich', wednesday)).toMatch(/October 2026/);
  });

  it("names an older month by the club's calendar", () => {
    // 23:30 UTC on 30 September is already 1 October in Zurich.
    expect(getDateGroup('2026-09-30T23:30:00Z', 'Europe/Zurich', now)).toMatch(/October 2026/);
    expect(getDateGroup('2026-09-30T23:30:00Z', 'America/New_York', now)).toMatch(/September 2026/);
  });
});

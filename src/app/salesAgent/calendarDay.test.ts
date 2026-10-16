import { describe, expect, it } from 'vitest';
import { calendarKey } from './calendarDay';

describe('calendarKey', () => {
  it('keeps a bare date on that day', () => {
    expect(calendarKey('2026-10-16')).toBe('2026-10-16');
  });

  it('reads a timestamp as the local day, matching the task list', () => {
    const stamp = '2026-10-16T00:00:00.000Z';
    const instant = new Date(stamp);
    const month = String(instant.getMonth() + 1).padStart(2, '0');
    const day = String(instant.getDate()).padStart(2, '0');
    expect(calendarKey(stamp)).toBe(`${instant.getFullYear()}-${month}-${day}`);
  });

  it('returns null for a blank or unreadable value', () => {
    expect(calendarKey('')).toBeNull();
    expect(calendarKey('not-a-date')).toBeNull();
  });
});

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fromLocalInputValue, toLocalInputValue } from './dateInputs';

describe('date inputs', () => {
  beforeEach(() => vi.stubEnv('TZ', 'Europe/Zurich'));
  afterEach(() => vi.unstubAllEnvs());

  it('round-trips without moving the time', () => {
    const stored = '2026-10-28T14:00:00.000Z';
    expect(fromLocalInputValue(toLocalInputValue(stored))).toBe(stored);
  });

  it('shows the local time, not the UTC one', () => {
    const d = new Date('2026-10-28T14:00:00Z');
    const local = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    expect(toLocalInputValue('2026-10-28T14:00:00Z').slice(11)).toBe(local);
  });

  it('treats empty and invalid values as no date', () => {
    expect(toLocalInputValue(null)).toBe('');
    expect(toLocalInputValue('not a date')).toBe('');
    expect(fromLocalInputValue('')).toBeUndefined();
  });
});

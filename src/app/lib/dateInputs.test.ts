import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { endOfLocalDay, fromLocalInputValue, toLocalInputValue } from './dateInputs';
import { isGiftCardExpired } from '@/app/api/giftCards';

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

  it('takes a chosen day to its end where the user is, so the whole day counts', () => {
    expect(endOfLocalDay('2026-12-31')).toBe('2026-12-31T22:59:59.999Z');
    expect(endOfLocalDay('')).toBeUndefined();
    expect(endOfLocalDay('31.12.2026')).toBeUndefined();
  });

  it('keeps a gift card usable on the day it expires', () => {
    const elevenInZurich = Date.parse('2026-12-31T10:00:00Z');
    expect(isGiftCardExpired({ expiresAtUtc: endOfLocalDay('2026-12-31') }, elevenInZurich)).toBe(false);
    // What the form used to send: the first moment of the day, in UTC.
    expect(isGiftCardExpired({ expiresAtUtc: new Date('2026-12-31').toISOString() }, elevenInZurich)).toBe(true);
    expect(isGiftCardExpired({ expiresAtUtc: endOfLocalDay('2026-12-31') }, Date.parse('2027-01-01T00:00:00Z'))).toBe(true);
  });
});

import { describe, expect, it } from 'vitest';
import type { NewsletterAudience } from '@/app/api/leadNewsletter';
import { leftOutCounts, leftOutTotal, reasonInfo } from './audience';

const counted = (over: Partial<NewsletterAudience>): NewsletterAudience => ({
  matching: 10, willReceive: 5, noEmail: 1, doNotContact: 0, suppressed: 1, duplicates: 1,
  recipients: [], leftOut: [], maxRecipients: 2000, ...over,
});

describe('who a newsletter leaves out', () => {
  it('counts those with a meeting already booked, by name, among the others', () => {
    const audience = counted({ alreadyBooked: 2 });
    expect(leftOutCounts(audience)).toEqual([
      { reason: 'no_email', count: 1 },
      { reason: 'suppressed', count: 1 },
      { reason: 'already_booked', count: 2 },
      { reason: 'duplicate', count: 1 },
    ]);
    expect(leftOutTotal(audience)).toBe(5);
    expect(reasonInfo('already_booked').label).toBe('Already booked');
  });

  it('reads a server that does not count them yet as none', () => {
    const audience = counted({});
    expect(leftOutCounts(audience).map((r) => r.reason)).not.toContain('already_booked');
    expect(leftOutTotal(audience)).toBe(3);
  });
});

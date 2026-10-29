// The Charges page figures, now that the server reports a late charge as Overdue.

import { describe, it, expect } from 'vitest';
import { computeChargeStats, type Charge } from './charges';

const charge = (over: Partial<Charge>): Charge => ({
  id: Math.random().toString(36).slice(2),
  memberId: 'm1',
  memberName: 'Jean Dupont',
  kind: 'Dues',
  description: 'Dues',
  amount: 100,
  paidAmount: 0,
  status: 'Pending',
  postedAtUtc: new Date().toISOString(),
  ...over,
});

describe('computeChargeStats', () => {
  it('counts an overdue charge as owed, as overdue, and as open', () => {
    const stats = computeChargeStats([
      charge({ status: 'Overdue', amount: 600, paidAmount: 100 }),
      charge({ status: 'Pending', amount: 50 }),
      charge({ status: 'Voided', amount: 999 }),
    ]);

    expect(stats.outstanding).toBe(550);
    expect(stats.overdue).toBe(500);
    expect(stats.pendingCount).toBe(2);
  });
});

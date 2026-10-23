import { describe, expect, it } from 'vitest';
import { coverageFactLines, coverageSteps } from './coverage';

const now = new Date(2026, 9, 16);

describe('coverageSteps', () => {
  it('names a lapsed member, an overdue charge, and a no-show', () => {
    const items = coverageSteps({
      now,
      members: [{ id: 'm1', firstName: 'Ada', lastName: 'Lovelace', status: 'Lapsed' }],
      charges: [{ id: 'c1', memberName: 'Ada Lovelace', status: 'Overdue', dueAtUtc: '2026-10-01' }],
      reservations: [{ id: 'r1', memberName: 'Ada Lovelace', resourceName: 'Dining', status: 'NoShow', startAtUtc: '2026-10-15T18:00:00Z' }],
    });
    expect(items.map((item) => item.list).sort()).toEqual(['Charges', 'Members', 'Reservations']);
    expect(items.find((item) => item.list === 'Reservations')?.action).toMatch(/no-show/i);
  });

  it('ignores a demo webhook and a finished contract', () => {
    const items = coverageSteps({
      now,
      webhook: { hasApiKey: false, webhookUrl: 'demo://webhook/leads' },
      contracts: [{ id: 'k1', title: 'Done', status: 'countersigned', leadId: 'l1' }],
    });
    expect(items).toEqual([]);
  });

  it('flags an empty household and a negative loyalty balance', () => {
    const items = coverageSteps({
      now,
      households: [{ id: 'h1', name: 'The Lovelaces' }, { id: 'h2', name: 'Full house' }],
      householdMemberIds: ['h2'],
      loyalty: [{ memberId: 'm1', memberName: 'Ada Lovelace', points: -20 }],
    });
    expect(items.map((item) => item.name).sort()).toEqual(['Ada Lovelace', 'The Lovelaces']);
  });

  it('asks for a task after a recent call that has no open task', () => {
    const items = coverageSteps({
      now,
      activities: [{ id: 'a1', type: 'call', leadId: 'l1', leadName: 'Ada Lovelace', createdAt: '2026-10-16T09:00:00' }],
      openTasks: [],
      audits: [{ id: 'au1', action: 'deleted', entityLabel: 'Old lead', entityType: 'lead', createdAtUtc: '2026-10-15T09:00:00' }],
    });
    expect(items.find((item) => item.list === 'Activities')?.action).toMatch(/no open task/i);
    expect(items.find((item) => item.list === 'Audit')?.name).toBe('Old lead');
  });

  it('turns pipeline totals and copy history into sentences', () => {
    const lines = coverageFactLines({
      pipelineLines: ['Proposal: 12000 USD across 2 deals.'],
      copyCount: 3,
      requiredFieldCount: 1,
    });
    expect(lines.join(' ')).toContain('Proposal');
    expect(lines.join(' ')).toContain('3 drafts');
    expect(lines.join(' ')).toContain('required');
  });
});

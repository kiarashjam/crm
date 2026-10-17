import { describe, expect, it } from 'vitest';
import { dealPlan, leadPlan } from './recordPlan';

const now = new Date('2026-10-16T12:00:00');

describe('leadPlan', () => {
  it('shows the first call as now and the later phases still ahead', () => {
    const plan = leadPlan({ id: 'l1', name: 'Ada' }, now);
    expect(plan.lines.map((line) => line.state)).toEqual(['now', 'later', 'later', 'later', 'later']);
    expect(plan.lines[0]?.detail).toMatch(/first call/);
    expect(plan.lines[1]?.title).toBe('Meeting');
  });

  it('stops a lost lead once the reason is saved', () => {
    const plan = leadPlan({
      id: 'l1',
      name: 'Ada',
      pipelineState: JSON.stringify({
        outreachStatus: 'contacted',
        contactOutcome: 'not_interested',
        dropoutReason: 'not_within_budget',
        dropoutReasonPhase: 1,
      }),
    }, now);
    expect(plan.lines[0]?.state).toBe('stopped');
    expect(plan.lines[0]?.detail).toMatch(/reason is recorded/);
  });
});

describe('dealPlan', () => {
  it('marks earlier stages done and names the step for the open stage', () => {
    const plan = dealPlan({ id: 'd1', name: 'Villa', stage: 'Proposal', expectedCloseDateUtc: '2026-12-01' }, now);
    expect(plan.lines.map((line) => [line.title, line.state])).toEqual([
      ['Qualification', 'done'],
      ['Proposal', 'now'],
      ['Negotiation', 'later'],
      ['Contract Sent', 'later'],
      ['Contract Signed', 'later'],
      ['Closed Won', 'later'],
    ]);
    expect(plan.lines[1]?.detail).toMatch(/proposal/i);
  });

  it('says a won deal is finished', () => {
    const plan = dealPlan({ id: 'd1', name: 'Villa', isWon: true }, now);
    expect(plan.lines.every((line) => line.state === 'done')).toBe(true);
    expect(plan.lines[plan.lines.length - 1]?.detail).toMatch(/won/);
  });
});

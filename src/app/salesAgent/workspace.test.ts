import { describe, expect, it } from 'vitest';
import {
  collectWork,
  nextActionForDeal,
  nextActionForEnrollment,
  nextActionForTask,
} from './workspace';

const now = new Date(2026, 9, 16);

describe('nextActionForDeal', () => {
  it('asks for the lost reason, then stops once it is saved', () => {
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', isWon: false }, now)?.action).toMatch(/why this deal was lost/);
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', stage: 'Closed Lost', closedReason: 'Budget' }, now)).toBeNull();
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', isWon: true }, now)).toBeNull();
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', stage: 'closed won' }, now)).toBeNull();
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', stage: 'proposal', expectedCloseDateUtc: '2026-12-01' }, now)?.action).toMatch(/proposal/i);
  });

  it('puts a passed close date ahead of the stage step', () => {
    const action = nextActionForDeal({
      id: 'd1',
      name: 'Villa',
      stage: 'Proposal',
      expectedCloseDateUtc: '2026-10-01',
    }, now);
    expect(action?.priority).toBe(94);
  });

  it('follows a named stage, and asks for a date when the stage has none', () => {
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', dealStageName: 'Contract Sent', expectedCloseDateUtc: '2026-12-01' }, now)?.action).toMatch(/signature/);
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', stage: 'Qualification' }, now)?.action).toMatch(/fit/);
    expect(nextActionForDeal({ id: 'd1', name: 'Villa', stage: 'Custom hold' }, now)?.action).toMatch(/expected close date/);
  });

  it('keeps the stage step when the close date is this week', () => {
    const action = nextActionForDeal({
      id: 'd1',
      name: 'Villa',
      stage: 'Proposal',
      expectedCloseDateUtc: '2026-10-20T00:00:00.000Z',
    }, now);
    expect(action?.action).toMatch(/proposal/i);
    expect(action?.priority).toBe(80);
    expect(action?.because).toMatch(/seven days/);
  });

  it('says nothing for an enrollment that has no step date', () => {
    expect(nextActionForEnrollment({
      id: 'e1', sequenceId: 's1', targetType: 'lead', targetId: 'l1', targetName: 'Ada',
      status: 'active', currentStep: 1,
    }, { id: 's1', steps: [] }, now)).toBeNull();
  });

  it('asks for a touch when the deal has been quiet for a week and the stage step is weaker', () => {
    const action = nextActionForDeal({
      id: 'd1',
      name: 'Villa',
      stage: 'Custom hold',
      expectedCloseDateUtc: '2026-12-01',
      lastActivityAtUtc: '2026-10-01',
    }, now);
    expect(action?.action).toMatch(/Log a touch/);
  });
});

describe('nextActionForTask', () => {
  it('flags overdue and due-today work, and ignores finished tasks', () => {
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'todo', dueDateUtc: '2026-10-01' }, now)?.priority).toBe(93);
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'todo', dueDateUtc: '2026-10-16' }, now)?.action).toMatch(/today/);
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'completed', dueDateUtc: '2026-10-01' }, now)).toBeNull();
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'todo', dueDateUtc: '2026-10-20' }, now)).toBeNull();
  });

  it('asks for a date on a high-priority task that has none', () => {
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'todo', priority: 'high' }, now)?.action).toMatch(/due date/);
    expect(nextActionForTask({ id: 't1', title: 'Call', status: 'todo', priority: 'low' }, now)).toBeNull();
  });
});

describe('nextActionForEnrollment', () => {
  const sequence = {
    id: 's1',
    steps: [{ order: 1, type: 'call' as const, taskTitle: 'Qualify the lead' }],
  };

  it('names the due step and skips a future one', () => {
    const due = nextActionForEnrollment({
      id: 'e1', sequenceId: 's1', targetType: 'lead', targetId: 'l1', targetName: 'Ada',
      status: 'active', currentStep: 1, nextActionAtUtc: '2026-10-16',
    }, sequence, now);
    expect(due?.action).toMatch(/Qualify the lead/);
    expect(due?.href).toBe('/leads/l1');

    expect(nextActionForEnrollment({
      id: 'e1', sequenceId: 's1', targetType: 'contact', targetId: 'c1', targetName: 'Ada',
      status: 'active', currentStep: 1, nextActionAtUtc: '2026-10-20',
    }, sequence, now)).toBeNull();
    expect(nextActionForEnrollment({
      id: 'e1', sequenceId: 's1', targetType: 'lead', targetId: 'l1', targetName: 'Ada',
      status: 'paused', currentStep: 1,
    }, sequence, now)).toBeNull();
  });
});

describe('collectWork', () => {
  it('ranks a missing lead reason above an overdue task and a stage step', () => {
    const ranked = collectWork({
      leads: [{
        id: 'l1',
        name: 'Ada',
        pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }),
      }],
      deals: [{ id: 'd1', name: 'Villa', stage: 'Proposal', expectedCloseDateUtc: '2026-12-01' }],
      tasks: [{ id: 't1', title: 'Send deck', status: 'todo', dueDateUtc: '2026-10-01' }],
    }, now, 5);
    expect(ranked.map((item) => item.key)).toEqual(['lead:l1', 'task:t1', 'deal:d1']);
  });

  it('keeps the stronger step when a sequence and a lead are the same person', () => {
    const ranked = collectWork({
      leads: [{ id: 'l1', name: 'Ada' }],
      enrollments: [{
        id: 'e1', sequenceId: 's1', targetType: 'lead', targetId: 'l1', targetName: 'Ada',
        status: 'active', currentStep: 0, nextActionAtUtc: '2026-10-16',
      }],
      sequences: [{ id: 's1', steps: [{ order: 0, type: 'email', subject: 'Hello' }] }],
    }, now);
    expect(ranked.map((item) => item.href)).toEqual(['/leads/l1']);
    expect(ranked[0]?.key).toBe('enrollment:e1');
  });

  it('covers people, duplicates, and an empty automation without burying the pipeline', () => {
    const ranked = collectWork({
      leads: [{ id: 'l1', name: 'Ada', email: 'ada@example.com' }],
      contacts: [
        { id: 'c1', name: 'No phone', email: '' },
        { id: 'c2', name: 'Bea', email: 'bea@example.com', doNotContact: true },
        { id: 'c3', name: 'Bea Two', email: 'bea@example.com' },
      ],
      companies: [{ id: 'co1', name: 'Empty Co' }],
      automations: [
        { id: 'a1', name: 'Welcome', enabled: true, actions: [] },
        { id: 'a2', name: 'Off', enabled: false, actions: [] },
      ],
    }, now, 10);
    const keys = ranked.map((item) => item.key);
    expect(keys[0]).toBe('lead:l1');
    expect(keys).toContain('contact:c1');
    expect(keys).not.toContain('contact:c2');
    expect(keys).toContain('duplicates:contacts');
    expect(keys).toContain('company:co1');
    expect(keys).toContain('automation:a1');
    expect(ranked.find((item) => item.key === 'lead:l1')?.action).toMatch(/first call/);
    expect(ranked.find((item) => item.key === 'lead:l1')?.brief?.copyTypeId).toBe('sales-email');
  });

  it('asks for a way to reach a lead before calling, and does not draft a message', () => {
    const ranked = collectWork({ leads: [{ id: 'l1', name: 'Ada' }] }, now);
    expect(ranked[0]?.action).toMatch(/no way to reach/);
    expect(ranked[0]?.brief).toBeUndefined();
  });

  it('offers a proposal draft and no draft for a missing loss reason', () => {
    const proposal = collectWork({
      deals: [{ id: 'd1', name: 'Villa', stage: 'Proposal', expectedCloseDateUtc: '2026-12-01', assigneeId: 'u1' }],
    }, now);
    expect(proposal[0]?.brief?.goal).toBe('Follow up after demo');
    const lost = collectWork({ deals: [{ id: 'd1', name: 'Villa', isWon: false }] }, now);
    expect(lost[0]?.brief).toBeUndefined();
  });
});

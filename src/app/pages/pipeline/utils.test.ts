import { describe, it, expect } from 'vitest';
import type { Deal, Pipeline } from '@/app/api/types';
import { dealOutcome, getStageList } from './utils';

const pipeline: Pipeline = {
  id: 'p1',
  organizationId: 'o1',
  name: 'Membership',
  displayOrder: 0,
  dealStages: [
    { id: 's3', pipelineId: 'p1', name: 'Signed', displayOrder: 2, isWon: true, isLost: false },
    { id: 's1', pipelineId: 'p1', name: 'Enquiry', displayOrder: 0, isWon: false, isLost: false },
    { id: 's4', pipelineId: 'p1', name: 'Declined', displayOrder: 3, isWon: false, isLost: true },
    { id: 's2', pipelineId: 'p1', name: 'Tour', displayOrder: 1, isWon: false, isLost: false },
  ],
};

const deal = (over: Partial<Deal>): Deal => ({ id: 'd', name: 'Deal', value: '100', ...over });

describe('getStageList', () => {
  it("uses the pipeline's own stages in order, with their won and lost settings", () => {
    const stages = getStageList([pipeline], 'p1');
    expect(stages.map((s) => s.name)).toEqual(['Enquiry', 'Tour', 'Signed', 'Declined']);
    expect(stages.find((s) => s.name === 'Signed')?.isWon).toBe(true);
    expect(stages.find((s) => s.name === 'Declined')?.isLost).toBe(true);
  });

  it('falls back to the standard stages when a pipeline has none', () => {
    const stages = getStageList([], null);
    expect(stages.find((s) => s.name === 'Closed Won')?.isWon).toBe(true);
    expect(stages.find((s) => s.name === 'Closed Lost')?.isLost).toBe(true);
  });
});

describe('dealOutcome', () => {
  const stages = getStageList([pipeline], 'p1');

  it('reads won and lost from the stage settings, whatever the stage is called', () => {
    expect(dealOutcome(deal({ dealStageId: 's3' }), stages)).toBe('won');
    expect(dealOutcome(deal({ dealStageId: 's4' }), stages)).toBe('lost');
    expect(dealOutcome(deal({ dealStageId: 's2' }), stages)).toBe('open');
  });

  it("puts the deal's own outcome first", () => {
    expect(dealOutcome(deal({ dealStageId: 's2', isWon: true }), stages)).toBe('won');
    expect(dealOutcome(deal({ dealStageId: 's3', isWon: false }), stages)).toBe('lost');
  });

  it('matches a deal saved before stages had ids by its stage name', () => {
    expect(dealOutcome(deal({ stage: 'Closed Won' }), getStageList([], null))).toBe('won');
  });
});

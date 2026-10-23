import { describe, expect, it } from 'vitest';
import type { WorkSuggestion } from '../workspace';
import { weigh } from './judge';

const secondCall: WorkSuggestion = {
  key: 'lead:l1',
  href: '/leads/l1',
  name: 'Ada',
  list: 'Outreach',
  action: 'Try once more later today, at a different time. They were already called today.',
  because: 'A no-answer is not a lost lead.',
  priority: 55,
};

describe('weigh', () => {
  it('holds a same-day second call until 15:00', () => {
    const morning = weigh(secondCall, [], [], new Date(2026, 5, 1, 10, 0, 0));
    const afternoon = weigh(secondCall, [], [], new Date(2026, 5, 1, 15, 0, 0));
    expect(morning.score).toBe(43);
    expect(afternoon.score).toBe(59);
  });

  it('trims a large deal when the saved probability is under 40 percent', () => {
    const deal: WorkSuggestion = {
      ...secondCall,
      key: 'deal:d1',
      href: '/deals/d1',
      name: 'Pavilion',
      action: 'Send the proposal.',
      because: 'The deal is in Proposal.',
      priority: 72,
    };
    const now = new Date(2026, 5, 1, 10, 0, 0);
    const thin = weigh(deal, [], [{ id: 'd1', name: 'Pavilion', value: '48000', probability: 20 }], now);
    const likely = weigh(deal, [], [{ id: 'd1', name: 'Pavilion', value: '48000', probability: 80 }], now);
    expect(thin.weights.find((weight) => weight.name === 'Larger deal')?.points).toBe(14);
    expect(likely.weights.find((weight) => weight.name === 'Larger deal')?.points).toBe(22);
    expect(likely.score).toBeGreaterThan(thin.score);
  });

  it('adds weight only after 14 days of silence already written on the row', () => {
    const quiet = (days: number): WorkSuggestion => ({
      ...secondCall,
      key: 'deal:d2',
      href: '/deals/d2',
      action: 'Log a touch.',
      because: `Nothing has been recorded in ${days} days.`,
      priority: 67,
    });
    const now = new Date(2026, 5, 1, 10, 0, 0);
    expect(weigh(quiet(16), [], [], now).weights.some((weight) => weight.name === 'Long silence')).toBe(true);
    expect(weigh(quiet(7), [], [], now).weights.some((weight) => weight.name === 'Long silence')).toBe(false);
  });
});

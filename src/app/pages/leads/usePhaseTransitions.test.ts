// The rules for "a phase just completed". Written as statements about behaviour,
// because each one guards against a specific way the animation goes wrong.

import { describe, it, expect } from 'vitest';
import { deepestNewlyCompleted } from './usePhaseTransitions';

const F = false;
const T = true;

describe('deepestNewlyCompleted', () => {
  it('says nothing on the first observation', () => {
    // A lead opened at phase 4 already has completed phases. Replaying that
    // history as an entrance animation is noise, not progress.
    expect(deepestNewlyCompleted(null, [T, T, T, F, F])).toBeNull();
  });

  it('reports a phase that just flipped to complete', () => {
    expect(deepestNewlyCompleted([F, F, F, F, F], [T, F, F, F, F])).toBe(0);
    expect(deepestNewlyCompleted([T, F, F, F, F], [T, T, F, F, F])).toBe(1);
  });

  it('stays silent when nothing changed', () => {
    // Unrelated re-renders (a parent updating, a date field edited) must not
    // trigger the animation.
    expect(deepestNewlyCompleted([T, T, F, F, F], [T, T, F, F, F])).toBeNull();
  });

  it('stays silent when a phase is UN-completed', () => {
    // Correcting a mistake is not an achievement.
    expect(deepestNewlyCompleted([T, T, F, F, F], [T, F, F, F, F])).toBeNull();
  });

  it('reports the deepest phase when several complete at once', () => {
    // One animation on the furthest point reached, not a burst of five.
    expect(deepestNewlyCompleted([F, F, F, F, F], [T, T, T, F, F])).toBe(2);
  });

  it('reports the completion even when another phase regressed in the same update', () => {
    // Mixed edit: phase 2 un-ticked, phase 4 ticked. Forward progress wins.
    expect(deepestNewlyCompleted([T, T, F, F, F], [T, F, F, T, F])).toBe(3);
  });

  it('handles the final phase', () => {
    expect(deepestNewlyCompleted([T, T, T, T, F], [T, T, T, T, T])).toBe(4);
  });

  it('handles an empty vector without throwing', () => {
    expect(deepestNewlyCompleted([], [])).toBeNull();
  });
});

// Detects the moment a pipeline phase *becomes* complete, as opposed to merely
// being complete.
//
// This is the hard part of animating the tracker, and it is easy to get wrong in
// three specific ways:
//
//   1. Driving the animation from the click handler. Every edit round-trips to
//      the server and the component re-renders from new props, so a click-driven
//      animation plays before the change is real — and never plays at all when a
//      change arrives from anywhere else.
//   2. Playing on first mount. A lead opened at phase 4 has three completed
//      phases; replaying their history as an entrance animation is noise.
//   3. Celebrating a correction. Un-ticking a phase moves `done[i]` true → false;
//      that is not progress and must stay silent.
//
// So this hook compares the CURRENT completion vector against the previous one
// and reports only false → true edges, after the first render.

import { useEffect, useRef, useState } from 'react';

export interface PhaseTransitionState {
  /**
   * Index (0-based) of the phase that just flipped to complete, or null.
   * Auto-clears after `holdMs` so the animation is one-shot rather than a
   * state the component sits in.
   */
  justCompleted: number | null;
  /**
   * Increments on every reported transition. Use as a React `key` so that
   * completing the same phase twice (tick → untick → tick) replays the
   * animation instead of being deduped away.
   */
  nonce: number;
}

export interface UsePhaseTransitionsOptions {
  /**
   * When false, transitions are swallowed but the baseline still advances — so
   * re-enabling does not dump a backlog of animations for changes the user
   * never saw. Pass `!disabled` here.
   */
  enabled?: boolean;
  /** How long `justCompleted` stays set. Should exceed the animation duration. */
  holdMs?: number;
}

const DEFAULT_HOLD_MS = 1200;

export function usePhaseTransitions(
  done: readonly boolean[],
  { enabled = true, holdMs = DEFAULT_HOLD_MS }: UsePhaseTransitionsOptions = {},
): PhaseTransitionState {
  // `null` marks "not yet seen", which is what makes the first render silent.
  const previous = useRef<readonly boolean[] | null>(null);
  const [state, setState] = useState<PhaseTransitionState>({ justCompleted: null, nonce: 0 });
  const timer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Serialised so the effect compares by VALUE. `done` is rebuilt on every
  // render by phaseCompletion(), so a reference dep would fire constantly.
  const signature = done.join(',');

  useEffect(() => {
    const prev = previous.current;
    previous.current = done.slice();

    // First render establishes the baseline and reports nothing.
    if (prev === null) return;
    if (!enabled) return;

    // Report the DEEPEST newly-completed phase. Completing several at once (a
    // bulk edit, or a server response carrying more than one change) should be
    // one animation on the furthest point reached, not a burst of five.
    let found = -1;
    for (let i = 0; i < done.length; i += 1) {
      if (done[i] && !prev[i]) found = i;
    }
    if (found === -1) return;

    setState((s) => ({ justCompleted: found, nonce: s.nonce + 1 }));

    if (timer.current) clearTimeout(timer.current);
    timer.current = setTimeout(() => {
      setState((s) => ({ ...s, justCompleted: null }));
      timer.current = null;
    }, holdMs);
    // `signature` is the value-equality dep; `done` itself is intentionally absent.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [signature, enabled, holdMs]);

  // Clear the pending timeout if the component unmounts mid-animation.
  useEffect(() => () => {
    if (timer.current) clearTimeout(timer.current);
  }, []);

  return state;
}

/**
 * Pure core of the hook, exported for testing and reuse: the index of the
 * deepest phase that flipped false → true, or null.
 *
 * `prev === null` means "first observation" and always yields null.
 */
export function deepestNewlyCompleted(
  prev: readonly boolean[] | null,
  next: readonly boolean[],
): number | null {
  if (prev === null) return null;
  let found = -1;
  for (let i = 0; i < next.length; i += 1) {
    if (next[i] && !prev[i]) found = i;
  }
  return found === -1 ? null : found;
}

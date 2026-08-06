// Which phase cards are unfolded.
//
// Five phases fully expanded is a wall of forms, and four of them are asking
// about things that either already happened or have not happened yet. The one
// that matters is the phase you are on — so that is the one that is open, and
// the rest fold down to a summary line you can open whenever you want.
//
// The whole difficulty is that "open the current phase" is a moving target, and
// it must not fight the user. The rule:
//
//   · A card with no explicit choice on it follows the focus.
//   · A card the user opened or closed by hand keeps that choice.
//   · When the focus MOVES, the two cards involved in the hand-off — the one
//     being left and the one being entered — drop their manual choice and
//     return to following. Anything the user opened elsewhere stays open.
//
// Without that last clause, finishing a phase would leave the finished card
// pinned open forever, because opening it once counted as a manual choice.

import { useCallback, useMemo, useState } from 'react';

export interface PhaseDisclosure {
  isOpen: (i: number) => boolean;
  toggle: (i: number) => void;
  /** Open a specific card without touching the others (used by the step rail). */
  reveal: (i: number) => void;
  setAll: (open: boolean) => void;
  openCount: number;
  allOpen: boolean;
}

/**
 * @param focus  Index of the phase that should be open by default, or null for
 *               none — which is the right answer for a finished pipeline, where
 *               every card is a summary and the convert action is the point.
 * @param count  How many phases there are.
 */
export function usePhaseDisclosure(focus: number | null, count: number): PhaseDisclosure {
  const [overrides, setOverrides] = useState<Record<number, boolean>>({});
  const [seenFocus, setSeenFocus] = useState<number | null>(focus);

  // Adjusting state during render rather than in an effect: React re-runs the
  // component before committing, so the corrected value is what paints. An
  // effect would paint the stale layout first and the card would visibly
  // flick shut and open again.
  if (seenFocus !== focus) {
    setSeenFocus(focus);
    setOverrides((o) => {
      const next = { ...o };
      if (seenFocus !== null) delete next[seenFocus];
      if (focus !== null) delete next[focus];
      return next;
    });
  }

  const isOpen = useCallback(
    (i: number) => overrides[i] ?? i === focus,
    [overrides, focus],
  );

  const toggle = useCallback(
    (i: number) => setOverrides((o) => ({ ...o, [i]: !(o[i] ?? i === focus) })),
    [focus],
  );

  const reveal = useCallback((i: number) => setOverrides((o) => ({ ...o, [i]: true })), []);

  const setAll = useCallback(
    (open: boolean) => {
      const next: Record<number, boolean> = {};
      for (let i = 0; i < count; i += 1) next[i] = open;
      setOverrides(next);
    },
    [count],
  );

  const openCount = useMemo(() => {
    let n = 0;
    for (let i = 0; i < count; i += 1) if (overrides[i] ?? i === focus) n += 1;
    return n;
  }, [overrides, focus, count]);

  return { isOpen, toggle, reveal, setAll, openCount, allOpen: openCount === count };
}

/**
 * Which card should be open by default, given where the pipeline stands.
 *
 * Not simply `currentPhase`: a lead that dropped out at Contract while Meeting
 * is still blank should open Contract, because that is where the story ended —
 * and a finished pipeline should open nothing at all.
 */
export function defaultFocus(args: {
  currentPhase: number;
  lostPhase: number | null;
  complete: boolean;
}): number | null {
  if (args.complete) return null;
  if (args.lostPhase !== null) return args.lostPhase - 1;
  return args.currentPhase - 1;
}

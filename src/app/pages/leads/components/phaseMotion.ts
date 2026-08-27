// Every timing number for the lead lifecycle tracker, in one place.
//
// The choreography is a downward relay: when a phase completes, an emerald bar
// pours down its left edge, and while it is still pouring the next card's indigo
// tick grows down its own edge 12px below. Card N and card N+1 are adjacent
// siblings, so the whole gesture happens inside that gap and reads as one
// movement — "that one's banked, you're on this one now" — without the eye
// leaving the local area, and without depending on the header being on screen.
//
// Total elapsed: 480ms. Every animated property is transform or opacity, so
// nothing triggers layout. Nothing loops, nothing overshoots past its resting
// value, and nothing moves under the cursor.

import { useMemo } from 'react';

// `as const` is load-bearing: without it TS widens these to `number[]` and
// Framer's `Easing` type rejects them.
const EASE_OUT_QUINT = [0.22, 1, 0.36, 1] as const;
const EASE_OUT_EXPO = [0.16, 1, 0.3, 1] as const;

// `times` is the one place the const assertion has to be undone: Framer types it
// as a mutable `number[]`, and a `readonly` tuple is not assignable to that.
const BADGE_TIMES: number[] = [0, 0.45, 1];

export const INSTANT = { duration: 0 } as const;

export const PHASE_MOTION = {
  /** Phase icon fading out of the badge. */
  glyphOut: { duration: 0.12, ease: 'linear' },
  /** Check fading in. Overlaps the fade-out so the badge is never empty. */
  glyphIn: { duration: 0.18, ease: EASE_OUT_QUINT, delay: 0.09 },
  /** One compress-and-return on the badge — the whole celebration budget. */
  badge: { duration: 0.26, ease: EASE_OUT_QUINT, times: BADGE_TIMES },
  /** THE SEAL: emerald bar pours down the completed card's left edge. */
  seal: { duration: 0.3, ease: EASE_OUT_EXPO },
  /** Header rail connector filling. Peripheral, so it trails the seal. */
  connector: { duration: 0.3, ease: EASE_OUT_EXPO, delay: 0.12 },
  /** THE LANDING: next card's tick grows while the seal is still pouring. */
  accent: { duration: 0.28, ease: EASE_OUT_EXPO, delay: 0.2 },
  /** Outgoing tick fading under the seal. scaleY is held so it never retracts. */
  tickOut: { duration: 0.16, ease: 'linear' },
  /** Convert panel, when the fifth phase is the one that completed. */
  convert: { duration: 0.24, ease: EASE_OUT_EXPO, delay: 0.2 },
  /**
   * A card folding open or shut. The one place a height IS animated — a
   * disclosure has no honest transform equivalent — so it is kept short, and
   * the box is only clipped while it is actually moving.
   */
  disclosure: { duration: 0.24, ease: EASE_OUT_EXPO },
  /** Chevron rotating. Slightly quicker than the fold so it leads it. */
  chevron: { duration: 0.2, ease: EASE_OUT_QUINT },

  /* ── The presence layer ─────────────────────────────────────────────────
     Everything below animates STATE CHANGES the tracker previously cut on:
     the current pip moving, a chip being chosen, a caption being replaced.
     Springs where a thing MOVES (physicality reads as responsiveness), tweens
     where a thing is REPLACED (a swap should be quiet, not bouncy). */

  /** The "you are here" ring gliding along the rail to the new current pip. */
  railCursor: { type: 'spring', stiffness: 480, damping: 38, mass: 0.7 },
  /** A check springing in the moment a choice or date lands. */
  chipCheck: { type: 'spring', stiffness: 640, damping: 30, mass: 0.6 },
  /** Neighbouring chips re-settling around the landed check's width. */
  chipLayout: { duration: 0.18, ease: EASE_OUT_QUINT },
  /** Caption / pill / count text replaced with continuity instead of a cut. */
  swap: { duration: 0.18, ease: EASE_OUT_QUINT },
  /**
   * One pip of the full-completion cascade: when the fifth phase lands, the
   * rail pops left to right, one small crest per pip. The per-pip delay is
   * added at the call site; this is the shape of each pop.
   */
  cascadePop: { duration: 0.3, ease: EASE_OUT_QUINT, times: BADGE_TIMES },
} as const;

export type PhaseMotionTokens = typeof PHASE_MOTION;

const ALL_INSTANT = Object.fromEntries(
  Object.keys(PHASE_MOTION).map((k) => [k, INSTANT]),
) as unknown as PhaseMotionTokens;

/**
 * The token set, already branched on the motion preference.
 *
 * Returning the whole set pre-branched means no element has its own `if (reduce)`
 * to forget. Reduced motion is `duration: 0`, not a short duration: Framer writes
 * the target on the next frame with no interpolation at all.
 */
export function usePhaseMotion(reduce: boolean): PhaseMotionTokens {
  return useMemo(() => (reduce ? ALL_INSTANT : PHASE_MOTION), [reduce]);
}

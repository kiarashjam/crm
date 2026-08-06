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

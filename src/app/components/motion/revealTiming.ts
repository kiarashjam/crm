// Timing and geometry for page-entry animations.
//
// Pulled out of the components because the two ways an entry animation goes
// wrong are both invisible in a screenshot:
//
//  · An unbounded stagger. 40ms per item looks lovely for six cards and means
//    the two-hundredth lead appears eight seconds after the first. The delay has
//    to be capped, not just small.
//  · Replaying. Framer runs `initial → animate` on MOUNT, and a filtered list
//    unmounts and remounts its rows on every keystroke. Without a guard, typing
//    in the Leads search box re-cascades the whole page. So the reveal is a
//    WINDOW that opens once when the content first appears and then shuts;
//    anything mounting afterwards is placed, not animated.

export interface StaggerOpts {
  /** Seconds between consecutive items. */
  step?: number;
  /** How many items still get a delay. Beyond this they all share the last one. */
  cap?: number;
  /** Seconds before the first item moves. */
  base?: number;
}

const DEFAULTS: Required<StaggerOpts> = { step: 0.045, cap: 12, base: 0 };

/**
 * Delay for the item at `index`, in seconds.
 *
 * Capped rather than merely small: past `cap` every remaining item shares the
 * same delay, so a long list finishes arriving in a bounded time no matter how
 * long it is. A negative or non-finite index is treated as the first item rather
 * than producing a negative delay, which Framer would reject.
 */
export function staggerDelay(index: number, opts: StaggerOpts = {}): number {
  const { step, cap, base } = { ...DEFAULTS, ...opts };
  if (!Number.isFinite(index) || index <= 0) return base;
  return base + Math.min(index, cap) * step;
}

/**
 * How long after the content appears that new children still animate in.
 *
 * Must outlast the longest possible cascade — the capped stagger plus the slowest
 * variant, about 1.09s. At 900ms an item mounting at 890ms started a 550ms entry
 * while its neighbour at 910ms snapped straight in, which is visible right at the
 * boundary. Still far shorter than the gap before anyone types in a filter, so
 * the anti-replay guard is unaffected. `pageEnter.test.ts` pins the relationship
 * rather than the number.
 */
export const REVEAL_WINDOW_MS = 1400;

/**
 * True while the reveal window is still open.
 *
 * `openedAt` is null until the page has something to show — a page that is still
 * loading has not started revealing anything, so nothing should animate yet.
 */
export function isRevealing(
  openedAt: number | null,
  now: number,
  windowMs: number = REVEAL_WINDOW_MS,
): boolean {
  if (openedAt === null) return false;
  return now - openedAt < windowMs;
}

export type RevealVariant =
  /** Rises into place. The default, for sections and blocks. */
  | 'up'
  /** Scales up from slightly small. For stat tiles and cards — the "pop". */
  | 'pop'
  /** Opacity only. For anything where movement would fight a sticky position. */
  | 'fade'
  /** Slides in from the left. For toolbars and filter rows. */
  | 'slide';

export interface RevealMotion {
  initial: Record<string, number>;
  animate: Record<string, number>;
  duration: number;
  /** Springs feel right for a pop; a tween is steadier for large blocks. */
  spring: boolean;
}

/**
 * The motion for one variant.
 *
 * Only `opacity`, `y`, `x` and `scale` — all compositor properties. Animating
 * height or width here would reflow the page on every frame and, worse, move the
 * content under a reader's cursor while they are already looking at it.
 */
export function revealMotion(variant: RevealVariant): RevealMotion {
  switch (variant) {
    case 'pop':
      return { initial: { opacity: 0, scale: 0.94, y: 8 }, animate: { opacity: 1, scale: 1, y: 0 }, duration: 0.5, spring: true };
    case 'fade':
      return { initial: { opacity: 0 }, animate: { opacity: 1 }, duration: 0.4, spring: false };
    case 'slide':
      return { initial: { opacity: 0, x: -14 }, animate: { opacity: 1, x: 0 }, duration: 0.45, spring: false };
    case 'up':
    default:
      return { initial: { opacity: 0, y: 18 }, animate: { opacity: 1, y: 0 }, duration: 0.55, spring: false };
  }
}

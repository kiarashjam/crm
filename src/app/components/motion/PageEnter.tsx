// Orchestrated page-entry animation: content arrives in a cascade rather than
// appearing all at once.
//
// Two pieces. `<PageEnter>` opens a reveal window the moment the page has
// something real to show, and `<Reveal>` animates one block — but only if it
// mounted while that window was open. Each Reveal decides once, at mount, and
// remembers; that is what stops a filtered list from re-cascading the entire page
// on every keystroke, because those rows remount long after the window shut.
//
// Reduced motion turns the whole thing off rather than shortening it. An entry
// animation is decoration; there is nothing here a person loses by not seeing it.

import {
  createContext, memo, useContext, useMemo, useRef, type ReactNode,
} from 'react';
import { motion } from 'motion/react';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import {
  REVEAL_WINDOW_MS, isRevealing, revealMotion, staggerDelay,
  type RevealVariant, type StaggerOpts,
} from './pageEnter';

interface EnterContext {
  /** Whether a child mounting right now should animate. */
  revealing: () => boolean;
  reduceMotion: boolean;
}

// Defaults for a `<Reveal>` used outside a provider: render, do not animate.
// Failing closed matters — an un-provided Reveal that started at opacity 0 with
// nothing to trigger it would leave the content permanently invisible.
const Ctx = createContext<EnterContext>({ revealing: () => false, reduceMotion: true });

export function PageEnter({
  children,
  /**
   * Flips to true when the real content is about to render. The window opens on
   * the first true and never reopens, so a later refetch does not replay the
   * page. Defaults to true for pages that have nothing to wait for.
   */
  ready = true,
}: {
  children: ReactNode;
  ready?: boolean;
}) {
  const reduceMotion = useMotionPreference();
  const openedAt = useRef<number | null>(null);
  const sawReady = useRef(false);

  // The window opens TWICE, and both openings matter.
  //
  // Once at mount, so content with no data dependency — a page header, a toolbar
  // — animates the instant you arrive rather than waiting on a request it does
  // not need. And again when `ready` first turns true, so the rows that WERE
  // waiting on that request cascade as they appear. With only the first opening,
  // any fetch slower than the window left the header animating and the content
  // snapping in behind it; with only the second, the header sat still until the
  // data came back.
  //
  // Re-opening cannot resurrect the replay bug: it happens exactly once, at the
  // loading-to-loaded transition, long before anyone has typed in a filter.
  if (openedAt.current === null) openedAt.current = Date.now();
  if (ready && !sawReady.current) {
    sawReady.current = true;
    openedAt.current = Date.now();
  }

  const value = useMemo<EnterContext>(() => ({
    revealing: () => isRevealing(openedAt.current, Date.now(), REVEAL_WINDOW_MS),
    reduceMotion,
  }), [reduceMotion]);

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

interface RevealProps {
  children: ReactNode;
  /** Position in the cascade. Delays are capped, so a large index is safe. */
  index?: number;
  variant?: RevealVariant;
  className?: string;
  stagger?: StaggerOpts;
  /** Rendered element. `section`/`li` keep the surrounding markup meaningful. */
  as?: 'div' | 'section' | 'li' | 'nav' | 'article';
  /**
   * Passed through, because a landmark that loses its name is an accessibility
   * regression hiding inside a cosmetic change — a `<nav>` wrapped in a Reveal
   * that silently dropped `aria-label` is exactly how that happens.
   */
  'aria-label'?: string;
  id?: string;
  /**
   * Interactive props, so a clickable card can BE the revealed element instead of
   * gaining a wrapper. That matters twice over: a wrapper becomes the grid item
   * and stops the card stretching, and wrapping a 400-line card body would mean
   * re-indenting all of it for no behavioural gain.
   */
  role?: string;
  tabIndex?: number;
  onClick?: (e: React.MouseEvent<HTMLElement>) => void;
  onKeyDown?: (e: React.KeyboardEvent<HTMLElement>) => void;
}

function RevealImpl({
  children, index = 0, variant = 'up', className, stagger, as = 'div',
  'aria-label': ariaLabel, id, role, tabIndex, onClick, onKeyDown,
}: RevealProps) {
  const { revealing, reduceMotion } = useContext(Ctx);
  // Frozen at mount. Reading it live would mean a re-render after the window
  // shuts could yank a mid-flight element back to its initial state.
  const animateOnMount = useRef<boolean | null>(null);
  if (animateOnMount.current === null) {
    animateOnMount.current = !reduceMotion && revealing();
  }

  const Tag = motion[as];
  const passthrough = {
    className, id, 'aria-label': ariaLabel, role, tabIndex, onClick, onKeyDown,
  };

  if (!animateOnMount.current) {
    // `initial={false}` rather than a zero-duration animation: no frame is ever
    // rendered at opacity 0, so nothing flickers.
    return (
      <Tag {...passthrough} initial={false}>{children}</Tag>
    );
  }

  const m = revealMotion(variant);
  const delay = staggerDelay(index, stagger);

  return (
    <Tag
      {...passthrough}
      initial={m.initial}
      animate={m.animate}
      transition={m.spring
        ? { type: 'spring', stiffness: 320, damping: 24, delay }
        : { duration: m.duration, delay, ease: [0.16, 1, 0.3, 1] }}
    >
      {children}
    </Tag>
  );
}

/**
 * Memoised because these wrap the heaviest blocks on the page — chart panels,
 * lead cards — and the pages they live on re-render on every keystroke.
 */
export const Reveal = memo(RevealImpl);

export default PageEnter;

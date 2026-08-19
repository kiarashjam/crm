// The transition from picking a workspace to being inside it.
//
// It is a shared-element flight, not a fade: the overlay renders a plate carrying
// the SAME `layoutId` as the tile that was clicked, so Framer measures both boxes
// and interpolates the real geometry. The card the user pointed at is literally
// the thing that grows to fill the screen — which is what makes it read as
// "entering this workspace" rather than "a loading screen happened".
//
// Navigation is tied to the progress rail finishing rather than to a bare timer,
// so the animation is never cut off mid-flight. But rAF is suspended in a
// background tab, and an animation that never ticks would never complete, leaving
// someone who tabbed away staring at a stuck overlay — so a timeout backstops it
// and a ref makes sure only the first of the two wins.

import { useEffect, useRef } from 'react';
import { motion } from 'motion/react';
import { ArrowRight, Loader2 } from 'lucide-react';
import type { Organization } from '@/app/api/organizations';
import { hueFor, monogram } from './launcher';
import { plateLayoutId } from './WorkspaceCard';

/** How long the whole sequence takes, in seconds, before navigation. */
const FLIGHT = 1.3;
const REDUCED_FLIGHT = 0.18;
/**
 * How long the tile takes to grow into the screen.
 *
 * Measured, not guessed. A stiff spring finished the expansion in ~400ms of a
 * 1.15s sequence, which put the one moment worth watching in the first third and
 * left the rest of the time staring at a still frame. Sized to fill most of the
 * sequence, with just enough bounce to feel physical rather than mechanical.
 */
const PLATE = { type: 'spring', duration: 0.95, bounce: 0.14 } as const;

interface Props {
  org: Organization;
  /** Where the launch is heading, shown so the destination is never a surprise. */
  destinationLabel: string;
  reduceMotion: boolean;
  onComplete: () => void;
}

export function LaunchSequence({ org, destinationLabel, reduceMotion, onComplete }: Props) {
  const hue = hueFor(org.id);
  const from = `hsl(${hue} 82% 58%)`;
  const to = `hsl(${(hue + 42) % 360} 78% 48%)`;
  const duration = reduceMotion ? REDUCED_FLIGHT : FLIGHT;

  const fired = useRef(false);
  const finish = () => {
    if (fired.current) return;
    fired.current = true;
    onComplete();
  };

  useEffect(() => {
    // Backstop only. The rail normally gets there first.
    const t = window.setTimeout(finish, duration * 1000 + 700);
    return () => window.clearTimeout(t);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- one timer per launch
  }, []);

  return (
    <motion.div
      className="fixed inset-0 z-[60] flex items-center justify-center overflow-hidden bg-[#07080f]"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      transition={{ duration: reduceMotion ? 0.1 : 0.25 }}
      role="status"
      aria-live="polite"
      data-testid="launch-sequence"
    >
      {/* Colour wash in the workspace's own hue, so the screen itself changes
          identity as you cross into it. */}
      <motion.div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{ background: `radial-gradient(60% 55% at 50% 45%, ${from}22, transparent 70%)` }}
        initial={{ opacity: 0, scale: 0.85 }}
        animate={{ opacity: 1, scale: 1 }}
        transition={{ duration, ease: 'easeOut' }}
      />

      {/* The tile itself, arriving from wherever it was in the grid. */}
      <motion.div
        layoutId={plateLayoutId(org.id)}
        className="absolute inset-0 bg-[#0d0f18]"
        style={{ borderRadius: 0 }}
        transition={reduceMotion ? { duration: REDUCED_FLIGHT } : PLATE}
      />

      <div className="relative flex flex-col items-center px-6 text-center">
        {/* Rings pushing outward from the monogram. Purely decorative, and the
            only part that is genuinely dropped under reduced motion. */}
        {!reduceMotion && [0, 1, 2].map((i) => (
          <motion.span
            key={i}
            aria-hidden
            className="absolute top-1/2 left-1/2 -mt-24 -ml-24 h-48 w-48 rounded-full border-2"
            style={{ borderColor: `${from}99` }}
            initial={{ scale: 0.45, opacity: 0 }}
            animate={{ scale: 2.8, opacity: [0, 0.7, 0] }}
            // Delayed past the expansion: rings pushing out of a card that is
            // itself still growing just read as noise.
            transition={{ duration: 1.7, delay: 0.35 + i * 0.3, ease: 'easeOut', repeat: Infinity }}
          />
        ))}

        <motion.div
          className="relative flex h-24 w-24 items-center justify-center rounded-[28px] text-3xl font-black tracking-tight text-white"
          style={{ background: `linear-gradient(140deg, ${from}, ${to})`, boxShadow: `0 30px 80px -20px ${from}` }}
          initial={{ scale: 0.6, opacity: 0, rotate: reduceMotion ? 0 : -8 }}
          animate={{ scale: 1, opacity: 1, rotate: 0 }}
          transition={reduceMotion
            ? { duration: REDUCED_FLIGHT }
            : { type: 'spring', stiffness: 180, damping: 18, delay: 0.18 }}
        >
          {monogram(org.name)}
        </motion.div>

        <motion.p
          className="mt-8 text-[11px] font-semibold uppercase tracking-[0.35em] text-white/45"
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.35, delay: reduceMotion ? 0 : 0.26 }}
        >
          Entering workspace
        </motion.p>

        <motion.h2
          className="mt-3 max-w-2xl text-3xl font-bold text-white sm:text-4xl"
          initial={{ opacity: 0, y: 14, letterSpacing: reduceMotion ? '0em' : '0.12em' }}
          animate={{ opacity: 1, y: 0, letterSpacing: '-0.01em' }}
          transition={{ duration: reduceMotion ? REDUCED_FLIGHT : 0.8, delay: reduceMotion ? 0 : 0.3, ease: [0.16, 1, 0.3, 1] }}
        >
          {org.name}
        </motion.h2>

        {/* Progress rail. Its completion is what triggers navigation. */}
        <div className="mt-9 h-[3px] w-56 overflow-hidden rounded-full bg-white/10">
          <motion.div
            className="h-full w-full origin-left rounded-full"
            style={{ background: `linear-gradient(90deg, ${from}, ${to})` }}
            initial={{ scaleX: 0 }}
            animate={{ scaleX: 1 }}
            // Near-linear on purpose: it is telling you how long is left, and an
            // eased bar that sits at 90% for half a second reads as stuck.
            transition={{ duration, ease: reduceMotion ? 'linear' : [0.5, 0.1, 0.4, 1] }}
            onAnimationComplete={finish}
          />
        </div>

        <motion.p
          className="mt-5 inline-flex items-center gap-2 text-xs text-white/40"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          transition={{ duration: 0.3, delay: reduceMotion ? 0 : 0.4 }}
        >
          <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />
          Taking you to {destinationLabel}
          <ArrowRight className="h-3.5 w-3.5" aria-hidden />
        </motion.p>
      </div>
    </motion.div>
  );
}

export default LaunchSequence;

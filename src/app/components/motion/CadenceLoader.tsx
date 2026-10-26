// The app's loading marks, in one place.
//
// `CadenceLoader` is the full mark for a page or a panel: the Cadence tile
// breathing in the middle, a gradient arc turning round it, four coloured dots
// orbiting the other way, and a soft glow behind. `Spinner` is the same arc on its
// own, for inline use. Both are decoration: they are hidden from assistive
// technology (whoever renders them says "Loading"), and with reduced motion they
// hold still rather than disappear, so the shape still says "working on it".

import { motion } from 'motion/react';
import { Sparkles } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const SIZES = {
  sm: { box: 40, tile: 18, icon: 10, ring: 3, dot: 4 },
  md: { box: 64, tile: 28, icon: 14, ring: 4, dot: 6 },
  lg: { box: 96, tile: 42, icon: 20, ring: 5, dot: 8 },
} as const;

export type LoaderSize = keyof typeof SIZES;

const DOTS = ['#f97316', '#ec4899', '#8b5cf6', '#0ea5e9'];

/** The colours of the turning arc: fading in from nothing, round to violet. */
const ARC = 'conic-gradient(from 0deg, transparent 0deg, transparent 60deg, #f97316 150deg, #ec4899 240deg, #8b5cf6 330deg, transparent 360deg)';

/** A ring of the given thickness cut out of a filled circle. */
function ringMask(thickness: number): string {
  return `radial-gradient(farthest-side, transparent calc(100% - ${thickness}px), #000 calc(100% - ${thickness - 1}px))`;
}

export function CadenceLoader({ size = 'md', className }: { size?: LoaderSize; className?: string }) {
  const reduce = useMotionPreference();
  const s = SIZES[size];
  const mask = ringMask(s.ring);
  return (
    <span
      aria-hidden
      className={cn('relative inline-flex shrink-0 items-center justify-center', className)}
      style={{ width: s.box, height: s.box }}
    >
      {/* A soft glow that breathes behind everything. */}
      <motion.span
        className="absolute inset-[14%] rounded-full bg-gradient-to-br from-orange-400/45 via-pink-400/35 to-violet-400/45 blur-xl"
        animate={reduce ? undefined : { scale: [0.9, 1.15, 0.9], opacity: [0.55, 1, 0.55] }}
        transition={{ duration: 2.2, repeat: Infinity, ease: 'easeInOut' }}
      />
      {/* The track, then the arc turning round it. */}
      <span className="absolute inset-0 rounded-full bg-slate-400/20" style={{ WebkitMask: mask, mask }} />
      <motion.span
        className="absolute inset-0 rounded-full"
        style={{ backgroundImage: ARC, WebkitMask: mask, mask }}
        animate={reduce ? undefined : { rotate: 360 }}
        transition={{ duration: 1.1, repeat: Infinity, ease: 'linear' }}
      />
      {/* Four dots orbiting the other way, each pulsing in turn. */}
      <motion.span
        className="absolute inset-[17%]"
        animate={reduce ? undefined : { rotate: -360 }}
        transition={{ duration: 2.8, repeat: Infinity, ease: 'linear' }}
      >
        {DOTS.map((colour, i) => {
          const angle = (i / DOTS.length) * Math.PI * 2;
          return (
            <motion.span
              key={colour}
              className="absolute rounded-full"
              style={{
                width: s.dot,
                height: s.dot,
                background: colour,
                boxShadow: `0 0 ${s.dot * 1.5}px ${colour}`,
                left: `calc(50% + ${(Math.cos(angle) * 50).toFixed(2)}% - ${s.dot / 2}px)`,
                top: `calc(50% + ${(Math.sin(angle) * 50).toFixed(2)}% - ${s.dot / 2}px)`,
              }}
              animate={reduce ? undefined : { scale: [0.7, 1.35, 0.7], opacity: [0.6, 1, 0.6] }}
              transition={{ duration: 1.4, repeat: Infinity, ease: 'easeInOut', delay: i * 0.35 }}
            />
          );
        })}
      </motion.span>
      {/* The Cadence tile, breathing. */}
      <motion.span
        className="relative flex items-center justify-center overflow-hidden rounded-[30%] bg-gradient-to-br from-orange-500 via-pink-500 to-fuchsia-500 text-white shadow-lg shadow-pink-500/30"
        style={{ width: s.tile, height: s.tile }}
        animate={reduce ? undefined : { scale: [1, 1.08, 1], rotate: [0, 6, -6, 0] }}
        transition={{ duration: 1.8, repeat: Infinity, ease: 'easeInOut' }}
      >
        <span className="absolute inset-0 bg-gradient-to-b from-white/35 to-transparent" />
        <Sparkles className="relative" style={{ width: s.icon, height: s.icon }} />
      </motion.span>
    </span>
  );
}

/** Just the turning gradient arc, for a spinner inside a button, a row or a panel. */
export function Spinner({ size = 20, thickness, className }: { size?: number; thickness?: number; className?: string }) {
  const reduce = useMotionPreference();
  const mask = ringMask(thickness ?? Math.max(2, Math.round(size / 8)));
  return (
    <span aria-hidden className={cn('relative inline-block shrink-0', className)} style={{ width: size, height: size }}>
      <span className="absolute inset-0 rounded-full bg-slate-400/20" style={{ WebkitMask: mask, mask }} />
      <motion.span
        className="absolute inset-0 rounded-full"
        style={{ backgroundImage: ARC, WebkitMask: mask, mask }}
        animate={reduce ? undefined : { rotate: 360 }}
        transition={{ duration: 0.9, repeat: Infinity, ease: 'linear' }}
      />
    </span>
  );
}

export default CadenceLoader;

// A skeleton block with a light sweeping across it.
//
// Self-contained on purpose. `src/styles/animations.css` declares `@keyframes
// shimmer` TWICE, at lines 88 and 757, with opposite directions — so anything
// depending on that name gets whichever the cascade happens to pick. This drives
// the sweep from Framer instead, which also means one honest switch for reduced
// motion rather than a media query that the in-app toggle cannot reach.

import { motion } from 'motion/react';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

export function Shimmer({
  className,
  /** Staggers the sweep between neighbouring blocks so it reads as one wave. */
  delay = 0,
  style,
}: {
  className?: string;
  delay?: number;
  /** For sizes that are data rather than design — a bar-chart silhouette. */
  style?: React.CSSProperties;
}) {
  const reduceMotion = useMotionPreference();
  return (
    <div
      aria-hidden
      className={cn('relative overflow-hidden rounded-xl bg-gradient-to-r from-slate-200/90 via-slate-100 to-slate-200/90', className)}
      style={style}
    >
      {!reduceMotion && (
        <>
          {/* A faint wash of the brand colours drifting through, then the light. */}
          <motion.div
            className="absolute inset-y-0 w-2/3 bg-gradient-to-r from-transparent via-fuchsia-200/40 to-transparent"
            initial={{ x: '-160%' }}
            animate={{ x: '260%' }}
            transition={{ duration: 1.8, delay, repeat: Infinity, ease: 'easeInOut' }}
          />
          <motion.div
            className="absolute inset-y-0 w-1/2 -skew-x-12 bg-gradient-to-r from-transparent via-white/80 to-transparent"
            initial={{ x: '-150%' }}
            animate={{ x: '250%' }}
            transition={{ duration: 1.35, delay: delay + 0.1, repeat: Infinity, ease: 'linear' }}
          />
        </>
      )}
    </div>
  );
}

export default Shimmer;

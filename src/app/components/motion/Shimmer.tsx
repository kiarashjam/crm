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
      className={cn('relative overflow-hidden rounded-xl bg-slate-200', className)}
      style={style}
    >
      {!reduceMotion && (
        <motion.div
          className="absolute inset-y-0 w-1/2 bg-gradient-to-r from-transparent via-white/75 to-transparent"
          initial={{ x: '-150%' }}
          animate={{ x: '250%' }}
          transition={{ duration: 1.35, delay, repeat: Infinity, ease: 'linear' }}
        />
      )}
    </div>
  );
}

export default Shimmer;

import { motion } from 'motion/react';
import { useLocation } from 'react-router-dom';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

/**
 * A line of colour that sweeps across the top of the screen each time you move to
 * another page, so the change of page reads as a movement rather than a jump.
 * Keyed on the path, so every navigation plays it once. Purely decoration, and
 * nothing at all with reduced motion.
 */
export function RouteProgress() {
  const { pathname } = useLocation();
  const reduce = useMotionPreference();
  if (reduce) return null;
  return (
    <motion.div
      key={pathname}
      aria-hidden
      className="pointer-events-none fixed inset-x-0 top-0 z-[90] h-[3px]"
      initial={{ opacity: 1 }}
      animate={{ opacity: [1, 1, 0] }}
      transition={{ duration: 0.95, times: [0, 0.7, 1] }}
    >
      <motion.div
        className="h-full origin-left rounded-r-full bg-gradient-to-r from-orange-500 via-pink-500 to-violet-500 shadow-[0_0_12px_rgba(236,72,153,0.7)]"
        initial={{ scaleX: 0 }}
        animate={{ scaleX: 1 }}
        transition={{ duration: 0.7, ease: [0.16, 1, 0.3, 1] }}
      />
    </motion.div>
  );
}

export default RouteProgress;

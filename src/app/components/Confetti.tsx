import { useEffect, useMemo, useState } from 'react';
import { motion, PresenceContext } from 'motion/react';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const COLOURS = ['#f97316', '#f59e0b', '#ec4899', '#8b5cf6', '#0ea5e9', '#10b981', '#facc15'];

interface Piece {
  id: number;
  x: number;
  y: number;
  rotate: number;
  colour: string;
  size: number;
  round: boolean;
  delay: number;
}

function pieces(count: number, spread: number, seed: number): Piece[] {
  // A small deterministic scatter: the same burst every time for the same seed,
  // so it reads as designed rather than noisy.
  let state = seed * 9301 + 49297;
  const next = () => {
    state = (state * 9301 + 49297) % 233280;
    return state / 233280;
  };
  return Array.from({ length: count }, (_, id) => {
    const angle = (id / count) * Math.PI * 2 + next() * 0.6;
    const distance = spread * (0.55 + next() * 0.6);
    return {
      id,
      x: Math.cos(angle) * distance,
      y: Math.sin(angle) * distance - spread * 0.35,
      rotate: (next() - 0.5) * 540,
      colour: COLOURS[id % COLOURS.length] ?? '#f97316',
      size: 5 + Math.round(next() * 4),
      round: next() > 0.55,
      delay: next() * 0.08,
    };
  });
}

/**
 * A short burst of confetti from the middle of its parent, played each time
 * `fire` changes to a new truthy value. Purely decoration: hidden from assistive
 * technology, ignores the pointer, and does nothing at all when reduced motion is
 * asked for. The parent needs `position: relative`.
 */
export function Confetti({ fire, count = 18, spread = 56 }: { fire: number | boolean | null | undefined; count?: number; spread?: number }) {
  const reduce = useMotionPreference();
  const [burst, setBurst] = useState(0);

  useEffect(() => {
    if (!fire || reduce) return undefined;
    setBurst((current) => current + 1);
    const handle = window.setTimeout(() => setBurst(0), 1100);
    return () => window.clearTimeout(handle);
  }, [fire, reduce]);

  const scatter = useMemo(() => pieces(count, spread, burst || 1), [count, spread, burst]);
  if (!burst || reduce) return null;

  // The burst can sit inside a list kept by `AnimatePresence initial={false}`,
  // which tells everything mounted under it to skip its entry animation. A fresh,
  // empty presence context gives the pieces their flight back.
  return (
    <PresenceContext.Provider value={null}>
      <span aria-hidden className="pointer-events-none absolute left-1/2 top-1/2 z-30 h-0 w-0">
        {scatter.map((piece) => (
          <motion.span
            key={`${burst}-${piece.id}`}
            className="absolute block"
            style={{
              width: piece.size,
              height: piece.round ? piece.size : piece.size * 0.45,
              background: piece.colour,
              borderRadius: piece.round ? 999 : 2,
            }}
            initial={{ x: 0, y: 0, opacity: 1, scale: 0.4, rotate: 0 }}
            animate={{ x: piece.x, y: [0, piece.y, piece.y + 26], opacity: [1, 1, 0], scale: 1, rotate: piece.rotate }}
            transition={{ duration: 0.95, ease: [0.2, 0.8, 0.3, 1], delay: piece.delay }}
          />
        ))}
      </span>
    </PresenceContext.Provider>
  );
}

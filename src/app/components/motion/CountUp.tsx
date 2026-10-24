import { useEffect, useRef, useState } from 'react';
import { animate } from 'motion/react';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const defaultFormat = (n: number) => Math.round(n).toLocaleString();

/**
 * A number that counts up to its value, and glides to each new value after that.
 *
 * Screen readers get the final number only; the moving one is hidden from them,
 * so nobody hears "3… 7… 12". With reduced motion it is just the number.
 */
export function CountUp({
  value,
  format = defaultFormat,
  duration = 0.9,
}: {
  value: number;
  format?: (n: number) => string;
  duration?: number;
}) {
  const reduce = useMotionPreference();
  const [shown, setShown] = useState(0);
  const current = useRef(0);

  useEffect(() => {
    if (reduce) return undefined;
    const controls = animate(current.current, value, {
      duration,
      ease: [0.16, 1, 0.3, 1],
      onUpdate: (n) => {
        current.current = n;
        setShown(n);
      },
    });
    return () => controls.stop();
  }, [value, duration, reduce]);

  // Exactly the value at rest, whatever rounding the tween ended on.
  const display = reduce || shown === value ? format(value) : format(shown);
  return (
    <>
      <span aria-hidden>{display}</span>
      <span className="sr-only">{format(value)}</span>
    </>
  );
}

import { useReducedMotion } from 'motion/react';
import { useThemeOptional } from '@/app/contexts/ThemeContext';

/**
 * True when the user wants less motion, from EITHER source.
 *
 * This app has two independent switches and they need different mechanisms:
 *
 *   · the OS `prefers-reduced-motion` media query, and
 *   · an in-app "Reduce motion" toggle (Settings → Appearance) which puts a
 *     `.reduce-motion` class on the root.
 *
 * The class defeats CSS transitions only. Framer Motion animates in JS and sails
 * straight through it — and Tailwind's `motion-reduce:` variant is itself a media
 * query, so it is equally blind to the class. A user who ticked the in-app box
 * with no OS preference set would otherwise get the full choreography.
 *
 * `useThemeOptional` rather than `useTheme` so components remain renderable in
 * tests that do not wrap in ThemeProvider.
 */
export function useMotionPreference(): boolean {
  // Returns `boolean | null` — null before the media query resolves.
  const os = useReducedMotion();
  const theme = useThemeOptional();
  return !!os || !!theme?.appearance.reduceMotion;
}

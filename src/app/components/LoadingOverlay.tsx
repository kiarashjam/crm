import { createContext, useContext, useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { CadenceLoader } from '@/app/components/motion/CadenceLoader';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

interface LoadingRegistry {
  claim: (id: string) => void;
  release: (id: string) => void;
}

const LoadingContext = createContext<LoadingRegistry | null>(null);

export function PageLoadingProvider({ children }: { children: ReactNode }) {
  const [ids, setIds] = useState<string[]>([]);
  const reduceMotion = useMotionPreference();

  const registry = useMemo<LoadingRegistry>(() => ({
    claim: (id) => setIds((current) => (current.includes(id) ? current : [...current, id])),
    release: (id) => setIds((current) => current.filter((item) => item !== id)),
  }), []);

  return (
    <LoadingContext.Provider value={registry}>
      {children}
      <LoadingOverlay open={ids.length > 0} reduceMotion={reduceMotion} />
    </LoadingContext.Provider>
  );
}

/** Hold the shared overlay while `active` is true. Safe to call from many pages at once. */
export function usePageLoading(active: boolean) {
  const registry = useContext(LoadingContext);
  const id = useId();

  useEffect(() => {
    if (!registry || !active) return undefined;
    registry.claim(id);
    return () => registry.release(id);
  }, [active, id, registry]);
}

const EASE = [0.16, 1, 0.3, 1] as const;

/** What the card says under "Loading", one after another, while you wait. */
const HINTS = ['Getting things ready', 'Fetching the latest', 'Almost there'];

/**
 * The page is loading: a light frosted veil over it rather than a dark sheet, a
 * progress line along the top, and a glass card with the Cadence mark. Nothing at
 * all shows for a load that finishes in the first tenth of a second, so a quick
 * page never flashes a veil; a slow one gets the whole thing. It leaves by
 * blurring away. With reduced motion it simply appears and goes.
 */
function LoadingOverlay({ open, reduceMotion }: { open: boolean; reduceMotion: boolean }) {
  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[80] flex items-center justify-center px-6"
          initial={{ opacity: reduceMotion ? 1 : 0 }}
          animate={{ opacity: 1, transition: { duration: reduceMotion ? 0 : 0.25, delay: reduceMotion ? 0 : 0.1 } }}
          exit={{ opacity: 0, transition: { duration: reduceMotion ? 0 : 0.3, ease: EASE } }}
        >
          <div aria-hidden className="absolute inset-0 bg-white/60 backdrop-blur-[3px] dark:bg-slate-950/60" />
          {!reduceMotion && (
            <div aria-hidden className="pointer-events-none absolute inset-0 overflow-hidden">
              <motion.span
                className="absolute -left-24 top-1/4 h-80 w-80 rounded-full bg-orange-300/30 blur-3xl"
                animate={{ x: [0, 60, 0], y: [0, -30, 0] }}
                transition={{ duration: 6, repeat: Infinity, ease: 'easeInOut' }}
              />
              <motion.span
                className="absolute -right-24 bottom-1/4 h-80 w-80 rounded-full bg-violet-300/30 blur-3xl"
                animate={{ x: [0, -60, 0], y: [0, 30, 0] }}
                transition={{ duration: 7, repeat: Infinity, ease: 'easeInOut' }}
              />
            </div>
          )}
          <ProgressLine still={reduceMotion} />

          <motion.div
            role="status"
            aria-live="polite"
            aria-label="Loading"
            initial={reduceMotion ? false : { opacity: 0, y: 14, scale: 0.92, filter: 'blur(8px)' }}
            animate={{ opacity: 1, y: 0, scale: 1, filter: 'blur(0px)', transition: { duration: reduceMotion ? 0 : 0.45, ease: EASE, delay: reduceMotion ? 0 : 0.12 } }}
            exit={reduceMotion ? { opacity: 0 } : { opacity: 0, y: -10, scale: 0.96, filter: 'blur(6px)', transition: { duration: 0.25, ease: EASE } }}
            className="relative flex w-[min(100%,18rem)] flex-col items-center overflow-hidden rounded-3xl border border-white/80 bg-white/85 px-8 pb-6 pt-8 shadow-2xl shadow-fuchsia-500/15 backdrop-blur-xl dark:border-slate-700 dark:bg-slate-900/85"
          >
            {/* A line of the brand colours, flowing along the card's top edge. */}
            <span
              aria-hidden
              className="absolute inset-x-0 top-0 h-1 bg-gradient-to-r from-orange-500 via-pink-500 to-violet-500 bg-[length:200%_100%] motion-safe:animate-[shimmer_2.4s_linear_infinite]"
            />
            <CadenceLoader size="lg" />
            <p className="mt-5 bg-gradient-to-r from-slate-800 via-fuchsia-600 to-slate-800 bg-[length:200%_100%] bg-clip-text text-base font-semibold tracking-wide text-transparent motion-safe:animate-[shimmer_3s_linear_infinite] dark:from-white dark:via-fuchsia-300 dark:to-white">
              Loading
            </p>
            <Hint still={reduceMotion} />
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

/** A thin line of colour sweeping along the top of the screen while something loads. */
function ProgressLine({ still }: { still: boolean }) {
  return (
    <div aria-hidden className="absolute inset-x-0 top-0 h-[3px] overflow-hidden bg-fuchsia-100/60 dark:bg-fuchsia-950/40">
      {still ? (
        <div className="h-full w-full bg-gradient-to-r from-orange-500 via-pink-500 to-violet-500 opacity-60" />
      ) : (
        <motion.div
          className="h-full w-2/5 rounded-full bg-gradient-to-r from-orange-500 via-pink-500 to-violet-500 shadow-[0_0_12px_rgba(236,72,153,0.7)]"
          initial={{ x: '-110%' }}
          animate={{ x: '260%' }}
          transition={{ duration: 1.3, repeat: Infinity, ease: [0.45, 0, 0.25, 1] }}
        />
      )}
    </div>
  );
}

/** One line under "Loading" that changes every so often, so a wait never feels stuck. */
function Hint({ still }: { still: boolean }) {
  const [index, setIndex] = useState(0);
  useEffect(() => {
    if (still) return undefined;
    const handle = window.setInterval(() => setIndex((i) => (i + 1) % HINTS.length), 1600);
    return () => window.clearInterval(handle);
  }, [still]);
  return (
    <span className="relative mt-1 flex h-5 w-full items-center justify-center overflow-hidden text-xs text-slate-500 dark:text-slate-400">
      <AnimatePresence mode="wait" initial={false}>
        <motion.span
          key={index}
          initial={still ? false : { opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          exit={{ opacity: 0, y: -8 }}
          transition={{ duration: 0.25, ease: EASE }}
        >
          {HINTS[index]}…
        </motion.span>
      </AnimatePresence>
    </span>
  );
}

import { createContext, useContext, useEffect, useId, useMemo, useState, type ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
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

function LoadingOverlay({ open, reduceMotion }: { open: boolean; reduceMotion: boolean }) {
  return (
    <AnimatePresence>
      {open && (
        <motion.div
          className="fixed inset-0 z-[80] flex items-center justify-center bg-slate-950/50 px-6"
          initial={reduceMotion ? { opacity: 1 } : { opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0 }}
          transition={{ duration: reduceMotion ? 0.01 : 0.22, ease: [0.16, 1, 0.3, 1] }}
        >
          <motion.div
            role="status"
            aria-live="polite"
            aria-label="Loading"
            initial={reduceMotion ? { opacity: 1 } : { opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            exit={reduceMotion ? { opacity: 0 } : { opacity: 0, y: 8 }}
            transition={{ duration: reduceMotion ? 0.01 : 0.28, ease: [0.16, 1, 0.3, 1] }}
            className="flex w-[min(100%,16rem)] flex-col items-center rounded-3xl border border-white/70 bg-white px-8 py-7 shadow-2xl shadow-slate-950/20 dark:border-slate-700 dark:bg-slate-900"
          >
            <Spinner still={reduceMotion} />
            <p className="mt-4 text-sm font-medium tracking-wide text-slate-700 dark:text-slate-200">Loading</p>
          </motion.div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}

function Spinner({ still }: { still: boolean }) {
  return (
    <svg viewBox="0 0 72 72" className="h-16 w-16" aria-hidden="true">
      <circle cx="36" cy="36" r="28" fill="none" className="stroke-slate-200 dark:stroke-slate-700" strokeWidth="3" />
      <g className={still ? undefined : 'cadence-loader-spin'}>
        <circle
          cx="36"
          cy="36"
          r="28"
          fill="none"
          stroke="var(--brand-primary)"
          strokeWidth="3"
          strokeLinecap="round"
          strokeDasharray="64 120"
        />
      </g>
      <g className={still ? undefined : 'cadence-loader-spin-reverse'}>
        <circle
          cx="36"
          cy="36"
          r="18"
          fill="none"
          stroke="var(--brand-primary)"
          strokeOpacity="0.55"
          strokeWidth="2.5"
          strokeLinecap="round"
          strokeDasharray="28 90"
        />
      </g>
      <circle cx="36" cy="36" r="3.5" fill="var(--brand-primary)" />
    </svg>
  );
}

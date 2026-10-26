// What a page shows while its data is on the way.
//
// Every block is shaped like the real thing it stands in for — the compact page
// header with its row of figures, list cards, board columns, a record's two
// columns — so the page does not jump when the content lands, and the blocks rise
// in one after another with a colourful light sweeping across them. With reduced
// motion they simply sit there.

import { motion } from 'motion/react';
import { Shimmer } from '@/app/components/motion/Shimmer';
import { CadenceLoader } from '@/app/components/motion/CadenceLoader';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';

const EASE = [0.16, 1, 0.3, 1] as const;

/** A block that rises into place after the ones before it. */
function Rise({ index = 0, className, children }: { index?: number; className?: string; children: React.ReactNode }) {
  const reduce = useMotionPreference();
  return (
    <motion.div
      className={className}
      initial={reduce ? false : { opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: EASE, delay: reduce ? 0 : Math.min(index, 10) * 0.05 }}
    >
      {children}
    </motion.div>
  );
}

/** The shared page header, as a skeleton: icon or avatar, title, and its figures. */
export function HeroSkeleton({ stats = 4, back = false }: { stats?: number; back?: boolean }) {
  return (
    <Rise className="mb-4 overflow-hidden rounded-2xl border border-slate-200/80 bg-gradient-to-br from-white via-slate-50 to-white p-4 shadow-sm sm:p-5">
      {back && <Shimmer className="mb-3 h-3 w-24 rounded-md" />}
      <div className="flex items-center gap-3.5">
        <Shimmer className="h-12 w-12 shrink-0 rounded-2xl" />
        <div className="min-w-0 flex-1 space-y-2">
          <Shimmer className="h-6 w-48 max-w-full rounded-lg" delay={0.05} />
          <Shimmer className="h-3.5 w-72 max-w-full rounded-md opacity-70" delay={0.1} />
        </div>
        <Shimmer className="hidden h-9 w-28 rounded-xl sm:block" delay={0.15} />
      </div>
      {stats > 0 && (
        <div className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-[repeat(auto-fit,minmax(150px,1fr))]">
          {Array.from({ length: stats }).map((_, i) => (
            <div key={i} className="flex items-center gap-2.5 rounded-xl bg-white/85 px-2.5 py-2 ring-1 ring-slate-200/70">
              <Shimmer className="h-9 w-9 shrink-0 rounded-xl" delay={0.1 + i * 0.06} />
              <div className="flex-1 space-y-1.5">
                <Shimmer className="h-4 w-14 rounded-md" delay={0.12 + i * 0.06} />
                <Shimmer className="h-2.5 w-20 rounded-sm opacity-70" delay={0.14 + i * 0.06} />
              </div>
            </div>
          ))}
        </div>
      )}
    </Rise>
  );
}

/** A whole page: the app header's bar, the page header, and a list. */
export function PageSkeleton() {
  return (
    <div className="flex min-h-screen flex-col bg-gradient-subtle">
      <header className="w-full border-b border-slate-200/60 bg-white/80 px-[var(--page-padding)] py-4 backdrop-blur-sm">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <Shimmer className="h-10 w-10 rounded-xl" />
            <Shimmer className="h-6 w-32 rounded-lg" />
          </div>
          <div className="flex items-center gap-3">
            <Shimmer className="h-8 w-8 rounded-full" />
            <Shimmer className="h-10 w-10 rounded-full" />
          </div>
        </div>
      </header>
      <main className="w-full flex-1 px-[var(--page-padding)] py-[var(--main-block-padding-y)]">
        <HeroSkeleton />
        <ContentSkeleton rows={5} />
      </main>
    </div>
  );
}

/** A list of cards under a page's header. */
export function ContentSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-3" aria-busy="true">
      <p className="sr-only" role="status">Loading…</p>
      {Array.from({ length: rows }).map((_, i) => (
        <Rise
          key={i}
          index={i}
          className="flex items-center gap-4 rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm"
        >
          <Shimmer className="h-11 w-11 shrink-0 rounded-xl" delay={i * 0.08} />
          <div className="min-w-0 flex-1 space-y-2">
            <Shimmer className="h-4 rounded-md" delay={i * 0.08 + 0.04} style={{ width: `${55 + ((i * 17) % 30)}%` }} />
            <Shimmer className="h-3 rounded-md opacity-70" delay={i * 0.08 + 0.08} style={{ width: `${30 + ((i * 11) % 25)}%` }} />
          </div>
          <Shimmer className="hidden h-6 w-16 rounded-full sm:block" delay={i * 0.08 + 0.1} />
        </Rise>
      ))}
    </div>
  );
}

/** Board columns with a few cards in each, for the deals and tasks boards. */
export function KanbanSkeleton({ columns = 4 }: { columns?: number }) {
  return (
    <div className="flex gap-4 overflow-hidden" aria-busy="true">
      <p className="sr-only" role="status">Loading…</p>
      {Array.from({ length: columns }).map((_, col) => (
        <Rise key={col} index={col} className="min-w-[260px] flex-1 rounded-2xl border border-slate-200/70 bg-white/60 p-4">
          <div className="mb-4 flex items-center justify-between">
            <Shimmer className="h-5 w-24 rounded-md" delay={col * 0.1} />
            <Shimmer className="h-6 w-6 rounded-full" delay={col * 0.1} />
          </div>
          <div className="space-y-3">
            {[0, 1, 2].map((card) => (
              <div key={card} className="rounded-xl border border-slate-200/80 bg-white p-4 shadow-sm">
                <Shimmer className="mb-2 h-4 w-full rounded-md" delay={col * 0.1 + card * 0.06} />
                <Shimmer className="mb-3 h-3 w-2/3 rounded-sm opacity-70" delay={col * 0.1 + card * 0.06 + 0.04} />
                <div className="flex items-center justify-between">
                  <Shimmer className="h-5 w-16 rounded-full" delay={col * 0.1 + card * 0.06 + 0.08} />
                  <Shimmer className="h-6 w-6 rounded-full" delay={col * 0.1 + card * 0.06 + 0.08} />
                </div>
              </div>
            ))}
          </div>
        </Rise>
      ))}
    </div>
  );
}

/** A record page: its header with a back link, then the main column and the side column. */
export function RecordSkeleton({ className }: { className?: string }) {
  return (
    <div className={cn('w-full', className)} aria-busy="true">
      <p className="sr-only" role="status">Loading…</p>
      <HeroSkeleton back stats={4} />
      <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
        <div className="min-w-0 space-y-5">
          {[0, 1].map((card) => (
            <Rise key={card} index={card + 1} className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm">
              <div className="mb-4 flex items-center gap-2">
                <Shimmer className="h-6 w-6 rounded-lg" delay={card * 0.1} />
                <Shimmer className="h-3 w-24 rounded-md" delay={card * 0.1} />
              </div>
              <div className="space-y-2.5">
                {[0, 1, 2].map((row) => (
                  <div key={row} className="flex items-center gap-3">
                    <Shimmer className="h-9 w-9 shrink-0 rounded-lg" delay={card * 0.1 + row * 0.06} />
                    <div className="flex-1 space-y-1.5">
                      <Shimmer className="h-3.5 rounded-md" delay={card * 0.1 + row * 0.06 + 0.04} style={{ width: `${70 - row * 12}%` }} />
                      <Shimmer className="h-2.5 w-1/3 rounded-sm opacity-70" delay={card * 0.1 + row * 0.06 + 0.08} />
                    </div>
                  </div>
                ))}
              </div>
            </Rise>
          ))}
        </div>
        <div className="space-y-5">
          {[0, 1].map((card) => (
            <Rise key={card} index={card + 3} className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm">
              <div className="mb-4 flex items-center gap-2">
                <Shimmer className="h-6 w-6 rounded-lg" delay={0.2 + card * 0.1} />
                <Shimmer className="h-3 w-20 rounded-md" delay={0.2 + card * 0.1} />
              </div>
              <div className="grid grid-cols-2 gap-3">
                {[0, 1, 2, 3].map((cell) => (
                  <div key={cell} className="space-y-1.5">
                    <Shimmer className="h-2.5 w-12 rounded-sm opacity-70" delay={0.2 + card * 0.1 + cell * 0.05} />
                    <Shimmer className="h-3.5 w-20 rounded-md" delay={0.24 + card * 0.1 + cell * 0.05} />
                  </div>
                ))}
              </div>
            </Rise>
          ))}
        </div>
      </div>
    </div>
  );
}

/** The Cadence mark with a line of text under it, centred in the space it is given. */
export function CenteredLoader({ text = 'Loading...' }: { text?: string }) {
  return (
    <div className="flex min-h-[400px] flex-col items-center justify-center" role="status" aria-live="polite">
      <CadenceLoader size="lg" />
      <p className="mt-5 bg-gradient-to-r from-slate-500 via-fuchsia-500 to-slate-500 bg-[length:200%_100%] bg-clip-text text-sm font-semibold text-transparent motion-safe:animate-[shimmer_3s_linear_infinite]">
        {text}
      </p>
    </div>
  );
}

export default PageSkeleton;

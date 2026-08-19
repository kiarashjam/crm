// What Reports shows while it is fetching.
//
// It replaces a single centred spinner. A spinner tells you nothing except that
// something is happening; a skeleton shaped like the page tells you what is
// coming, and because the blocks are the same size as the real ones the content
// does not jump when it lands.
//
// The shapes here mirror Reports.tsx deliberately: hero, jump bar, a six-KPI
// overview, two wide panels, a four-KPI row, three panels, then a chart. If that
// layout changes and this does not, the cost is a small shift on load — visible,
// but never wrong data. That is the right way round for a loading state.

import { BarChart3 } from 'lucide-react';
import { Shimmer } from '@/app/components/motion/Shimmer';
import { Reveal } from '@/app/components/motion/PageEnter';

/** A fixed silhouette. Random heights would redraw differently on every render. */
const BAR_HEIGHTS = [46, 72, 38, 88, 60, 96, 54, 78, 42, 68, 84, 50, 74, 62];

/** One panel outline: title, subtitle, and a few bars. */
function PanelBox({ bars = 5, delay = 0 }: { bars?: number; delay?: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4">
      <Shimmer className="h-4 w-32" delay={delay} />
      <Shimmer className="mt-2 h-3 w-24 opacity-70" delay={delay + 0.05} />
      <div className="mt-4 space-y-2.5">
        {Array.from({ length: bars }, (_, i) => (
          <div key={i} className="flex items-center gap-3">
            <Shimmer className="h-3 w-20 shrink-0" delay={delay + i * 0.06} />
            {/* Varying widths, so it reads as data rather than a template. */}
            <Shimmer className="h-3 flex-1" delay={delay + i * 0.06} />
            <Shimmer className="h-3 w-9 shrink-0" delay={delay + i * 0.06} />
          </div>
        ))}
      </div>
    </div>
  );
}

function KpiBox({ delay = 0 }: { delay?: number }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4">
      <div className="flex items-start justify-between gap-2">
        <Shimmer className="h-3 w-20" delay={delay} />
        <Shimmer className="h-7 w-7 rounded-lg" delay={delay} />
      </div>
      <Shimmer className="mt-3 h-7 w-24" delay={delay + 0.06} />
      <Shimmer className="mt-2 h-2.5 w-16 opacity-70" delay={delay + 0.12} />
    </div>
  );
}

function Head({ delay = 0 }: { delay?: number }) {
  return (
    <div className="mb-3 flex items-center gap-3">
      <Shimmer className="h-9 w-9 rounded-xl" delay={delay} />
      <div>
        <Shimmer className="h-2.5 w-14" delay={delay} />
        <Shimmer className="mt-1.5 h-4 w-52" delay={delay + 0.05} />
      </div>
    </div>
  );
}

export function ReportsSkeleton() {
  return (
    <main
      className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
      aria-busy="true"
    >
      {/* Announced once, rather than leaving a screen reader with thirty
          unlabelled boxes. The shapes themselves are all aria-hidden. */}
      <p className="sr-only" role="status">Loading your reports…</p>

      {/* Hero. The real one is a live component, so this only stands in for the
          block it occupies. */}
      <Reveal variant="fade">
        <div className="flex items-center gap-4">
          <div className="flex h-14 w-14 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-indigo-500 to-violet-500 opacity-40">
            <BarChart3 className="h-7 w-7 text-white" aria-hidden />
          </div>
          <div>
            <Shimmer className="h-7 w-40" />
            <Shimmer className="mt-2 h-3.5 w-80 max-w-[70vw] opacity-70" delay={0.08} />
          </div>
        </div>
      </Reveal>

      {/* Jump bar */}
      <div className="mt-5 flex gap-1.5 rounded-2xl border border-slate-200 bg-white/85 px-2 py-2">
        {[56, 44, 42, 60, 50].map((w, i) => (
          <Shimmer key={i} className="h-6 shrink-0 rounded-xl" delay={i * 0.07} style={{ width: w }} />
        ))}
      </div>

      <div className="mt-8">
        <Head />
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 xl:grid-cols-6">
          {Array.from({ length: 6 }, (_, i) => <KpiBox key={i} delay={i * 0.08} />)}
        </div>
      </div>

      <div className="mt-10">
        <Head delay={0.1} />
        <div className="grid gap-3 lg:grid-cols-2">
          <PanelBox bars={6} delay={0.1} />
          <PanelBox bars={5} delay={0.16} />
        </div>
      </div>

      <div className="mt-10">
        <Head delay={0.2} />
        <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
          {Array.from({ length: 4 }, (_, i) => <KpiBox key={i} delay={0.2 + i * 0.08} />)}
        </div>
        <div className="mt-3 grid gap-3 lg:grid-cols-3">
          <PanelBox bars={5} delay={0.24} />
          <PanelBox bars={6} delay={0.3} />
          <PanelBox bars={4} delay={0.36} />
        </div>
      </div>

      <div className="mt-10">
        <Head delay={0.3} />
        <div className="rounded-2xl border border-slate-200 bg-white p-4">
          <Shimmer className="h-4 w-40" delay={0.3} />
          {/* A bar-chart silhouette rather than one grey slab. */}
          <div className="mt-5 flex h-[180px] items-end gap-2.5">
            {BAR_HEIGHTS.map((h, i) => (
              <Shimmer
                key={i}
                className="flex-1 rounded-t-md"
                delay={i * 0.05}
                style={{ height: `${h}%` }}
              />
            ))}
          </div>
        </div>
      </div>
    </main>
  );
}

export default ReportsSkeleton;

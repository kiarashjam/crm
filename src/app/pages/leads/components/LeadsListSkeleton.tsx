// What the Leads gallery shows while it is fetching.
//
// It replaces a generic six-row `ContentSkeleton`, which was the same shape on
// every page and so told you nothing about this one. These blocks are laid out
// like real gallery cards — tinted top band, avatar, name and badges, the phase
// bar, the four numbers, footer — in the same grid, so the wait previews the page
// instead of merely occupying it, and the content does not jump when it lands.

import { Shimmer } from '@/app/components/motion/Shimmer';

/** Slightly different widths and band colours per card, so it reads as data. */
const CARDS = [
  { name: 148, badges: [72, 58], band: 'from-sky-50' },
  { name: 116, badges: [64, 82], band: 'from-violet-50' },
  { name: 172, badges: [78, 60], band: 'from-amber-50' },
  { name: 132, badges: [70, 74], band: 'from-pink-50' },
  { name: 160, badges: [66, 88], band: 'from-teal-50' },
  { name: 124, badges: [80, 56], band: 'from-emerald-50' },
];

export function LeadsListSkeleton() {
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3" aria-busy="true">
      <p className="sr-only" role="status">Loading your leads…</p>
      {CARDS.map((card, i) => (
        <div
          key={i}
          aria-hidden
          className="relative flex flex-col overflow-hidden rounded-3xl border border-slate-200/80 bg-white shadow-sm"
        >
          <div className={`flex items-start gap-3 bg-gradient-to-br ${card.band} to-white px-4 pb-3 pt-4`}>
            <Shimmer className="h-12 w-12 shrink-0 rounded-2xl" delay={i * 0.08} />
            <div className="min-w-0 flex-1">
              <Shimmer className="h-4" delay={i * 0.08} style={{ width: card.name }} />
              <div className="mt-3 flex flex-wrap gap-1.5">
                {card.badges.map((w, k) => (
                  <Shimmer key={k} className="h-5 rounded-full" delay={i * 0.08 + k * 0.05} style={{ width: w }} />
                ))}
              </div>
            </div>
          </div>

          <div className="space-y-3 px-4 py-3">
            <Shimmer className="h-3 w-28" delay={i * 0.08} />
            <div className="grid grid-cols-5 gap-1">
              {[0, 1, 2, 3, 4].map((k) => (
                <Shimmer key={k} className="h-2 rounded-full" delay={i * 0.08 + k * 0.04} />
              ))}
            </div>
            <div className="grid grid-cols-4 gap-1.5">
              {[0, 1, 2, 3].map((k) => (
                <Shimmer key={k} className="h-12 rounded-xl opacity-70" delay={i * 0.08 + k * 0.05} />
              ))}
            </div>
          </div>

          <div className="mt-auto flex items-center justify-between gap-3 border-t border-slate-100 bg-slate-50/60 px-3 py-2.5">
            <Shimmer className="h-7 w-28 rounded-full opacity-70" delay={i * 0.08} />
            <div className="flex gap-1.5">
              <Shimmer className="h-8 w-16 rounded-full" delay={i * 0.08 + 0.04} />
              <Shimmer className="h-8 w-14 rounded-full" delay={i * 0.08 + 0.08} />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

export default LeadsListSkeleton;

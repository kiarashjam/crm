// What the Leads list shows while it is fetching.
//
// It replaces a generic six-row `ContentSkeleton`, which was the same shape on
// every page and so told you nothing about this one. These blocks are laid out
// like real lead cards — status rail, avatar, name and email, badge row, footer —
// so the wait previews the page instead of merely occupying it, and the content
// does not jump when it lands.

import { Shimmer } from '@/app/components/motion/Shimmer';

/** Slightly different name and badge widths per row, so it reads as data. */
const ROWS = [
  { name: 148, email: 196, badges: [72, 58, 90] },
  { name: 116, email: 168, badges: [64, 82] },
  { name: 172, email: 184, badges: [78, 60, 68] },
  { name: 132, email: 208, badges: [70, 74] },
  { name: 160, email: 176, badges: [66, 88, 62] },
  { name: 124, email: 192, badges: [80, 56] },
];

export function LeadsListSkeleton() {
  return (
    <div className="space-y-3" aria-busy="true">
      <p className="sr-only" role="status">Loading your leads…</p>
      {ROWS.map((row, i) => (
        <div
          key={i}
          aria-hidden
          className="relative overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-[0_2px_3px_rgba(15,23,42,0.04),0_8px_16px_-6px_rgba(15,23,42,0.10)]"
        >
          {/* The status rail every real card carries down its left edge. */}
          <Shimmer className="absolute top-0 bottom-0 left-0 w-2 rounded-none rounded-r-full" delay={i * 0.08} />

          <div className="flex items-start gap-4 px-5 py-4 pl-7">
            <Shimmer className="h-12 w-12 shrink-0 rounded-xl" delay={i * 0.08} />
            <div className="min-w-0 flex-1">
              <Shimmer className="h-4" delay={i * 0.08} style={{ width: row.name }} />
              <Shimmer className="mt-2 h-3 opacity-70" delay={i * 0.08 + 0.05} style={{ width: row.email }} />
              <div className="mt-3 flex flex-wrap gap-2">
                {row.badges.map((w, k) => (
                  <Shimmer key={k} className="h-5 rounded-full" delay={i * 0.08 + k * 0.05} style={{ width: w }} />
                ))}
              </div>
            </div>
            <div className="hidden shrink-0 items-center gap-2 sm:flex">
              <Shimmer className="h-8 w-16 rounded-lg" delay={i * 0.08} />
              <Shimmer className="h-8 w-8 rounded-lg" delay={i * 0.08 + 0.04} />
            </div>
          </div>

          <div className="flex items-center justify-between gap-3 border-t border-slate-100 px-5 py-2.5 pl-7">
            <Shimmer className="h-2.5 w-28 opacity-60" delay={i * 0.08} />
            <Shimmer className="h-2.5 w-20 opacity-60" delay={i * 0.08 + 0.04} />
          </div>
        </div>
      ))}
    </div>
  );
}

export default LeadsListSkeleton;

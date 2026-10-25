import type { ReactNode } from 'react';
import { motion } from 'motion/react';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import type { HeroTone } from './PageHero';

const EASE = [0.16, 1, 0.3, 1] as const;

// Static class strings, because Tailwind only ships the classes it can read.
const ICON: Record<HeroTone, string> = {
  orange: 'from-orange-400 to-pink-500',
  amber: 'from-amber-400 to-orange-500',
  rose: 'from-rose-400 to-pink-500',
  pink: 'from-pink-500 to-fuchsia-600',
  fuchsia: 'from-fuchsia-500 to-purple-600',
  violet: 'from-violet-500 to-fuchsia-500',
  indigo: 'from-indigo-500 to-violet-500',
  blue: 'from-blue-500 to-indigo-500',
  sky: 'from-sky-400 to-blue-600',
  cyan: 'from-cyan-400 to-sky-600',
  teal: 'from-teal-400 to-cyan-600',
  emerald: 'from-emerald-500 to-teal-500',
  slate: 'from-slate-400 to-slate-600',
};

const AVATAR: Record<HeroTone, string> = {
  orange: 'from-orange-500 to-pink-500 shadow-orange-500/30',
  amber: 'from-amber-400 to-orange-500 shadow-amber-500/30',
  rose: 'from-rose-500 to-pink-500 shadow-rose-500/30',
  pink: 'from-pink-500 to-fuchsia-600 shadow-pink-500/30',
  fuchsia: 'from-fuchsia-500 to-purple-600 shadow-fuchsia-500/30',
  violet: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30',
  indigo: 'from-indigo-500 to-violet-500 shadow-indigo-500/30',
  blue: 'from-blue-500 to-indigo-500 shadow-blue-500/30',
  sky: 'from-sky-400 to-blue-600 shadow-sky-500/30',
  cyan: 'from-cyan-400 to-sky-600 shadow-cyan-500/30',
  teal: 'from-teal-400 to-cyan-600 shadow-teal-500/30',
  emerald: 'from-emerald-500 to-teal-500 shadow-emerald-500/30',
  slate: 'from-slate-500 to-slate-700 shadow-slate-500/30',
};

/** Up to two initials, whatever spacing or punctuation the name carries. */
export function initialsOf(name: string | null | undefined): string {
  const words = (name ?? '').split(/[\s\-_.]+/).filter(Boolean);
  if (words.length === 0) return '?';
  const first = words[0]?.charAt(0) ?? '';
  const last = words.length > 1 ? words[words.length - 1]?.charAt(0) ?? '' : '';
  return (first + last).toUpperCase();
}

/** A record's initials in a colour tile, for a detail page's header. */
export function RecordAvatar({ name, tone = 'orange' }: { name: string | null | undefined; tone?: HeroTone }) {
  return (
    <div
      className={cn(
        'flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br text-base font-bold text-white shadow-lg ring-4 ring-white/80',
        'transition-transform duration-300 hover:-rotate-6 hover:scale-105 motion-reduce:transition-none',
        AVATAR[tone],
      )}
      aria-hidden
    >
      {initialsOf(name)}
    </div>
  );
}

/**
 * A section of a detail page: a small coloured icon, a title and a count, an
 * optional action on the right, and the content. The same card on every record
 * page, rising into place in turn as the page opens.
 */
export function RecordCard({
  title, icon, tone = 'slate', count, action, index = 0, className, bodyClassName, children, id,
}: {
  title: string;
  icon: ReactNode;
  tone?: HeroTone;
  count?: number;
  action?: ReactNode;
  /** Order on the page, for the entrance. */
  index?: number;
  className?: string;
  bodyClassName?: string;
  children: ReactNode;
  id?: string;
}) {
  const reduce = useMotionPreference();
  const headingId = id ? `${id}-heading` : undefined;
  return (
    <motion.section
      id={id}
      aria-labelledby={headingId}
      initial={reduce ? false : { opacity: 0, y: 12 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.4, ease: EASE, delay: reduce ? 0 : 0.05 + Math.min(index, 8) * 0.05 }}
      className={cn('overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm transition-shadow hover:shadow-md dark:border-slate-700 dark:bg-slate-800', className)}
    >
      <div className="flex items-center justify-between gap-2 px-4 pb-1 pt-3">
        <h2 id={headingId} className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-slate-500 dark:text-slate-400">
          <span className={cn('flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-sm', ICON[tone])} aria-hidden>
            {icon}
          </span>
          {title}
          {count !== undefined && (
            <span className="rounded-full bg-slate-100 px-1.5 py-px text-[10px] font-bold tabular-nums text-slate-600 dark:bg-slate-700 dark:text-slate-300">{count}</span>
          )}
        </h2>
        {action}
      </div>
      <div className={cn('px-4 pb-4 pt-2', bodyClassName)}>{children}</div>
    </motion.section>
  );
}

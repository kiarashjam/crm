import type { ElementType, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowLeft, Sparkles } from 'lucide-react';
import { CountUp } from '@/app/components/motion/CountUp';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { NextActionsSlot } from '@/app/salesAgent/NextActionsSlot';

export type StatTone =
  | 'slate' | 'indigo' | 'violet' | 'blue' | 'sky' | 'cyan' | 'teal' | 'emerald'
  | 'amber' | 'orange' | 'rose' | 'pink' | 'fuchsia';

/** A page's own colour: its icon, the band's wash and the lights drifting in it. */
export type HeroTone = StatTone;

export interface PageStat {
  label: string;
  /** A number counts up to itself; a string is shown as it is. */
  value: string | number;
  icon: ElementType;
  tone?: StatTone;
  /** What is behind the number, after the label: "6 active", "this month". */
  hint?: string;
  /** How a numeric value reads, e.g. as money. Grouped whole number by default. */
  format?: (n: number) => string;
  onClick?: () => void;
  /** For a figure that toggles a filter: whether that filter is on. */
  pressed?: boolean;
  /** The accessible name of a clickable figure. */
  actionLabel?: string;
  /** A share from 0 to 1, drawn as a thin bar along the tile's foot. */
  progress?: number;
}

interface PageHeroProps {
  icon: ElementType;
  /** In place of the icon tile: a record's avatar, say, on a detail page. */
  avatar?: ReactNode;
  tone?: HeroTone;
  /** Overrides the icon tile's gradient. Kept for callers that already pass one. */
  iconGradient?: string;
  title: ReactNode;
  subtitle?: ReactNode;
  /** Small chips beside the title: a status, a tag, a live count. */
  badges?: ReactNode;
  /** Right-aligned actions. `heroSecondary` / `heroPrimary` style them to match. */
  actions?: ReactNode;
  /** The page's key numbers, inside the band rather than as a row of cards under it. */
  stats?: PageStat[];
  /** Where this page sits, e.g. back to the list it came from. */
  back?: { to: string; label: string };
  /** A last row inside the band: a notice, a tab strip, a filter summary. */
  children?: ReactNode;
  /** Leave out the agent's next-step slot, for a page that places it itself. */
  noAgentSlot?: boolean;
}

interface HeroStyle {
  icon: string;
  wash: string;
  border: string;
  lights: [string, string, string];
  primary: string;
}

/** The colours that turn slowly round the band's edge, and the band's own glow. */
const RING: Record<HeroTone, { colours: string; via: string; halo: string; glow: string }> = {
  orange: { colours: '#f97316,#ec4899,#f59e0b,#fb7185,#f97316', via: 'via-orange-600', halo: 'bg-orange-400/50', glow: 'shadow-orange-500/15' },
  amber: { colours: '#f59e0b,#f97316,#facc15,#fb923c,#f59e0b', via: 'via-amber-600', halo: 'bg-amber-400/50', glow: 'shadow-amber-500/15' },
  rose: { colours: '#f43f5e,#ec4899,#fb923c,#f472b6,#f43f5e', via: 'via-rose-600', halo: 'bg-rose-400/50', glow: 'shadow-rose-500/15' },
  pink: { colours: '#ec4899,#d946ef,#f43f5e,#a855f7,#ec4899', via: 'via-pink-600', halo: 'bg-pink-400/50', glow: 'shadow-pink-500/15' },
  fuchsia: { colours: '#d946ef,#a855f7,#ec4899,#8b5cf6,#d946ef', via: 'via-fuchsia-600', halo: 'bg-fuchsia-400/50', glow: 'shadow-fuchsia-500/15' },
  violet: { colours: '#8b5cf6,#d946ef,#6366f1,#ec4899,#8b5cf6', via: 'via-violet-600', halo: 'bg-violet-400/50', glow: 'shadow-violet-500/15' },
  indigo: { colours: '#6366f1,#8b5cf6,#0ea5e9,#a855f7,#6366f1', via: 'via-indigo-600', halo: 'bg-indigo-400/50', glow: 'shadow-indigo-500/15' },
  blue: { colours: '#3b82f6,#6366f1,#0ea5e9,#8b5cf6,#3b82f6', via: 'via-blue-600', halo: 'bg-blue-400/50', glow: 'shadow-blue-500/15' },
  sky: { colours: '#0ea5e9,#3b82f6,#06b6d4,#6366f1,#0ea5e9', via: 'via-sky-600', halo: 'bg-sky-400/50', glow: 'shadow-sky-500/15' },
  cyan: { colours: '#06b6d4,#0ea5e9,#14b8a6,#3b82f6,#06b6d4', via: 'via-cyan-600', halo: 'bg-cyan-400/50', glow: 'shadow-cyan-500/15' },
  teal: { colours: '#14b8a6,#06b6d4,#10b981,#0ea5e9,#14b8a6', via: 'via-teal-600', halo: 'bg-teal-400/50', glow: 'shadow-teal-500/15' },
  emerald: { colours: '#10b981,#14b8a6,#84cc16,#06b6d4,#10b981', via: 'via-emerald-600', halo: 'bg-emerald-400/50', glow: 'shadow-emerald-500/15' },
  slate: { colours: '#64748b,#6366f1,#0ea5e9,#94a3b8,#64748b', via: 'via-slate-500', halo: 'bg-slate-400/50', glow: 'shadow-slate-500/15' },
};

// Static class strings, because Tailwind only ships the classes it can read.
const HERO: Record<HeroTone, HeroStyle> = {
  orange: { icon: 'from-orange-500 via-amber-500 to-pink-500 shadow-orange-500/30', wash: 'from-orange-50 via-white to-pink-50', border: 'border-orange-100', lights: ['bg-orange-300/40', 'bg-pink-300/30', 'bg-amber-200/40'], primary: 'from-orange-500 to-pink-500 shadow-pink-500/25' },
  amber: { icon: 'from-amber-400 to-orange-500 shadow-amber-500/30', wash: 'from-amber-50 via-white to-orange-50', border: 'border-amber-100', lights: ['bg-amber-300/40', 'bg-orange-300/30', 'bg-yellow-200/40'], primary: 'from-amber-500 to-orange-500 shadow-orange-500/25' },
  rose: { icon: 'from-rose-500 to-pink-500 shadow-rose-500/30', wash: 'from-rose-50 via-white to-pink-50', border: 'border-rose-100', lights: ['bg-rose-300/40', 'bg-pink-300/30', 'bg-orange-200/40'], primary: 'from-rose-500 to-pink-500 shadow-rose-500/25' },
  pink: { icon: 'from-pink-500 to-fuchsia-600 shadow-pink-500/30', wash: 'from-pink-50 via-white to-fuchsia-50', border: 'border-pink-100', lights: ['bg-pink-300/40', 'bg-fuchsia-300/30', 'bg-rose-200/40'], primary: 'from-pink-500 to-fuchsia-600 shadow-pink-500/25' },
  fuchsia: { icon: 'from-fuchsia-500 to-purple-600 shadow-fuchsia-500/30', wash: 'from-fuchsia-50 via-white to-purple-50', border: 'border-fuchsia-100', lights: ['bg-fuchsia-300/40', 'bg-purple-300/30', 'bg-pink-200/40'], primary: 'from-fuchsia-500 to-purple-600 shadow-fuchsia-500/25' },
  violet: { icon: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30', wash: 'from-violet-50 via-white to-fuchsia-50', border: 'border-violet-100', lights: ['bg-violet-300/40', 'bg-fuchsia-300/30', 'bg-indigo-200/40'], primary: 'from-violet-500 to-fuchsia-500 shadow-violet-500/25' },
  indigo: { icon: 'from-indigo-500 to-violet-500 shadow-indigo-500/30', wash: 'from-indigo-50 via-white to-violet-50', border: 'border-indigo-100', lights: ['bg-indigo-300/40', 'bg-violet-300/30', 'bg-sky-200/40'], primary: 'from-indigo-500 to-violet-500 shadow-indigo-500/25' },
  blue: { icon: 'from-blue-500 to-indigo-500 shadow-blue-500/30', wash: 'from-blue-50 via-white to-indigo-50', border: 'border-blue-100', lights: ['bg-blue-300/40', 'bg-indigo-300/30', 'bg-sky-200/40'], primary: 'from-blue-500 to-indigo-500 shadow-blue-500/25' },
  sky: { icon: 'from-sky-400 to-blue-600 shadow-sky-500/30', wash: 'from-sky-50 via-white to-blue-50', border: 'border-sky-100', lights: ['bg-sky-300/40', 'bg-blue-300/30', 'bg-cyan-200/40'], primary: 'from-sky-500 to-blue-600 shadow-sky-500/25' },
  cyan: { icon: 'from-cyan-400 to-sky-600 shadow-cyan-500/30', wash: 'from-cyan-50 via-white to-sky-50', border: 'border-cyan-100', lights: ['bg-cyan-300/40', 'bg-sky-300/30', 'bg-teal-200/40'], primary: 'from-cyan-500 to-sky-600 shadow-cyan-500/25' },
  teal: { icon: 'from-teal-400 to-cyan-600 shadow-teal-500/30', wash: 'from-teal-50 via-white to-cyan-50', border: 'border-teal-100', lights: ['bg-teal-300/40', 'bg-cyan-300/30', 'bg-emerald-200/40'], primary: 'from-teal-500 to-cyan-600 shadow-teal-500/25' },
  emerald: { icon: 'from-emerald-500 to-teal-500 shadow-emerald-500/30', wash: 'from-emerald-50 via-white to-teal-50', border: 'border-emerald-100', lights: ['bg-emerald-300/40', 'bg-teal-300/30', 'bg-lime-200/40'], primary: 'from-emerald-500 to-teal-500 shadow-emerald-500/25' },
  slate: { icon: 'from-slate-600 to-slate-800 shadow-slate-500/30', wash: 'from-slate-50 via-white to-slate-100', border: 'border-slate-200', lights: ['bg-slate-300/40', 'bg-indigo-200/30', 'bg-sky-200/30'], primary: 'from-slate-700 to-slate-900 shadow-slate-500/25' },
};

const STAT: Record<StatTone, { chip: string; bar: string; on: string; hover: string }> = {
  slate: { chip: 'from-slate-400 to-slate-600 shadow-slate-500/30', bar: 'from-slate-400 to-slate-600', on: 'bg-slate-50 ring-2 ring-slate-400', hover: 'hover:ring-slate-300' },
  indigo: { chip: 'from-indigo-500 to-violet-500 shadow-indigo-500/30', bar: 'from-indigo-500 to-violet-500', on: 'bg-indigo-50 ring-2 ring-indigo-400', hover: 'hover:ring-indigo-300' },
  violet: { chip: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30', bar: 'from-violet-500 to-fuchsia-500', on: 'bg-violet-50 ring-2 ring-violet-400', hover: 'hover:ring-violet-300' },
  blue: { chip: 'from-blue-500 to-indigo-500 shadow-blue-500/30', bar: 'from-blue-500 to-indigo-500', on: 'bg-blue-50 ring-2 ring-blue-400', hover: 'hover:ring-blue-300' },
  sky: { chip: 'from-sky-400 to-blue-500 shadow-sky-500/30', bar: 'from-sky-400 to-blue-500', on: 'bg-sky-50 ring-2 ring-sky-400', hover: 'hover:ring-sky-300' },
  cyan: { chip: 'from-cyan-400 to-sky-500 shadow-cyan-500/30', bar: 'from-cyan-400 to-sky-500', on: 'bg-cyan-50 ring-2 ring-cyan-400', hover: 'hover:ring-cyan-300' },
  teal: { chip: 'from-teal-400 to-cyan-500 shadow-teal-500/30', bar: 'from-teal-400 to-cyan-500', on: 'bg-teal-50 ring-2 ring-teal-400', hover: 'hover:ring-teal-300' },
  emerald: { chip: 'from-emerald-400 to-teal-500 shadow-emerald-500/30', bar: 'from-emerald-400 to-teal-500', on: 'bg-emerald-50 ring-2 ring-emerald-400', hover: 'hover:ring-emerald-300' },
  amber: { chip: 'from-amber-400 to-orange-500 shadow-amber-500/30', bar: 'from-amber-400 to-orange-500', on: 'bg-amber-50 ring-2 ring-amber-400', hover: 'hover:ring-amber-300' },
  orange: { chip: 'from-orange-400 to-pink-500 shadow-orange-500/30', bar: 'from-orange-400 to-pink-500', on: 'bg-orange-50 ring-2 ring-orange-400', hover: 'hover:ring-orange-300' },
  rose: { chip: 'from-rose-400 to-pink-500 shadow-rose-500/30', bar: 'from-rose-400 to-pink-500', on: 'bg-rose-50 ring-2 ring-rose-400', hover: 'hover:ring-rose-300' },
  pink: { chip: 'from-pink-400 to-fuchsia-500 shadow-pink-500/30', bar: 'from-pink-400 to-fuchsia-500', on: 'bg-pink-50 ring-2 ring-pink-400', hover: 'hover:ring-pink-300' },
  fuchsia: { chip: 'from-fuchsia-500 to-purple-500 shadow-fuchsia-500/30', bar: 'from-fuchsia-500 to-purple-500', on: 'bg-fuchsia-50 ring-2 ring-fuchsia-400', hover: 'hover:ring-fuchsia-300' },
};

const EASE = [0.16, 1, 0.3, 1] as const;

/** A plain button in the band: white on the wash, the page's colour when hovered. */
export const heroSecondary =
  'h-9 gap-2 rounded-xl border-slate-200/80 bg-white/80 px-3 text-slate-700 shadow-sm backdrop-blur hover:border-slate-300 hover:bg-white hover:text-slate-900';

/** The band's main action, in the page's colour. */
export function heroPrimary(tone: HeroTone = 'orange'): string {
  return cn('h-9 gap-2 rounded-xl bg-gradient-to-r px-4 font-semibold text-white shadow-md hover:opacity-95', HERO[tone].primary);
}

const LIGHT_SPOTS = [
  { at: '-right-16 -top-24 h-64 w-64', drift: { x: [0, -24, 0], y: [0, 16, 0] }, duration: 14 },
  { at: 'right-1/3 -bottom-28 h-56 w-56', drift: { x: [0, 28, 0], y: [0, -12, 0] }, duration: 17 },
  { at: '-left-20 -top-20 h-52 w-52', drift: { x: [0, 18, 0], y: [0, 20, 0] }, duration: 19 },
];

function Stat({ stat, index, reduce }: { stat: PageStat; index: number; reduce: boolean }) {
  const t = STAT[stat.tone ?? 'slate'];
  const Icon = stat.icon;
  const value = typeof stat.value === 'number'
    ? <CountUp value={stat.value} format={stat.format} />
    : stat.value;
  const share = stat.progress === undefined ? null : Math.max(0, Math.min(1, stat.progress));
  const body = (
    <>
      {/* A shine that crosses the tile under the pointer. */}
      {!reduce && (
        <span
          aria-hidden
          className="pointer-events-none absolute inset-y-0 -left-1/2 w-1/3 -skew-x-12 bg-gradient-to-r from-transparent via-white/80 to-transparent transition-transform duration-700 ease-out group-hover/stat:translate-x-[420%]"
        />
      )}
      <span
        className={cn(
          'relative flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br text-white shadow-md transition-transform duration-300 group-hover/stat:-rotate-6 group-hover/stat:scale-110 motion-reduce:transition-none',
          t.chip,
        )}
        aria-hidden
      >
        <span className="absolute inset-0 rounded-xl bg-gradient-to-b from-white/30 to-transparent" />
        <Icon className="relative h-4 w-4" />
      </span>
      <span className="relative min-w-0 flex-1">
        <span className="block truncate text-lg font-bold leading-tight tabular-nums text-slate-900">{value}</span>
        <span className="block truncate text-[11px] font-medium text-slate-500">
          {stat.label}
          {stat.hint && <span className="font-normal text-slate-500"> · {stat.hint}</span>}
        </span>
      </span>
      {stat.pressed && (
        <span className="relative shrink-0 rounded-full bg-slate-900 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">On</span>
      )}
      {share !== null ? (
        <span aria-hidden className="absolute inset-x-2.5 bottom-1 h-1 overflow-hidden rounded-full bg-slate-100">
          <motion.span
            className={cn('block h-full rounded-full bg-gradient-to-r', t.bar)}
            initial={reduce ? false : { width: 0 }}
            animate={{ width: `${share * 100}%` }}
            transition={{ duration: reduce ? 0 : 0.9, ease: EASE, delay: reduce ? 0 : 0.3 + index * 0.05 }}
          />
        </span>
      ) : (
        <span
          aria-hidden
          className={cn(
            'absolute inset-x-3 bottom-0 h-0.5 origin-left rounded-full bg-gradient-to-r transition-transform duration-300 ease-out motion-reduce:transition-none',
            t.bar,
            stat.pressed ? 'scale-x-100' : 'scale-x-0 group-hover/stat:scale-x-100',
          )}
        />
      )}
    </>
  );
  const className = cn(
    'group/stat relative flex h-full w-full min-w-0 items-center gap-2.5 overflow-hidden rounded-xl bg-white/85 px-2.5 py-2 text-left ring-1 ring-slate-200/70 backdrop-blur',
    share !== null && 'pb-3',
    'transition-[translate,box-shadow] duration-200 hover:-translate-y-0.5 hover:shadow-md motion-reduce:transition-none motion-reduce:hover:translate-y-0',
    t.hover,
    stat.pressed && t.on,
  );
  return (
    <motion.li
      className="min-w-0"
      initial={reduce ? false : { opacity: 0, y: 8, scale: 0.97 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{ duration: 0.4, ease: EASE, delay: reduce ? 0 : 0.12 + index * 0.06 }}
    >
      {stat.onClick ? (
        <button
          type="button"
          onClick={stat.onClick}
          aria-pressed={stat.pressed}
          aria-label={stat.actionLabel}
          className={cn(className, 'cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]')}
        >
          {body}
        </button>
      ) : (
        <div className={className}>{body}</div>
      )}
    </motion.li>
  );
}

/**
 * The header every page shares: one compact, bright band with the page's icon,
 * title and actions, and its key numbers inside it rather than as a row of cards
 * underneath, so the page's own content is on screen straight away. Each page has
 * its own colour; the motion (its colours turning round the edge, drifting light,
 * a shine as the page opens, a glowing icon, a shimmering title, numbers that
 * count up) is the same everywhere and switches off with reduced motion.
 */
export default function PageHero({
  icon: Icon, avatar, tone = 'orange', iconGradient, title, subtitle, badges, actions, stats, back, children, noAgentSlot = false,
}: PageHeroProps) {
  const reduce = useMotionPreference();
  const t = HERO[tone];
  const ring = RING[tone];
  return (
    <>
      <motion.div
        className={cn('relative mb-4 overflow-hidden rounded-2xl p-[1.5px] shadow-lg', ring.glow)}
        initial={reduce ? false : { opacity: 0, y: -10, scale: 0.99 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        transition={{ duration: 0.5, ease: EASE }}
      >
        {/* The edge: the page's colours turning slowly round the band. */}
        <motion.span
          aria-hidden
          className="pointer-events-none absolute left-1/2 top-1/2 aspect-square w-[150%] -translate-x-1/2 -translate-y-1/2 opacity-60"
          style={{ backgroundImage: `conic-gradient(from 0deg, ${ring.colours})` }}
          animate={reduce ? undefined : { rotate: 360 }}
          transition={{ duration: 18, repeat: Infinity, ease: 'linear' }}
        />
        <div className={cn('relative overflow-hidden rounded-[15px] bg-gradient-to-br', t.wash)}>
          <div aria-hidden className="pointer-events-none absolute inset-0">
            {LIGHT_SPOTS.map((spot, i) => (
              <motion.span
                key={spot.at}
                className={cn('absolute rounded-full blur-3xl', spot.at, t.lights[i])}
                animate={reduce ? undefined : spot.drift}
                transition={{ duration: spot.duration, repeat: Infinity, ease: 'easeInOut' }}
              />
            ))}
            <span className="absolute inset-0 opacity-60 [background-image:radial-gradient(circle,rgb(15_23_42/0.05)_1px,transparent_1px)] [background-size:18px_18px]" />
            {/* One shine across the band as the page opens. */}
            {!reduce && (
              <motion.span
                className="absolute inset-y-0 left-0 w-1/4 -skew-x-12 bg-gradient-to-r from-transparent via-white/70 to-transparent"
                initial={{ x: '-150%' }}
                animate={{ x: '520%' }}
                transition={{ duration: 1.6, ease: 'easeInOut', delay: 0.35 }}
              />
            )}
          </div>

          <div className="relative px-4 py-4 sm:px-5">
            {back && (
              <Link
                to={back.to}
                className="group/back mb-1 inline-flex min-h-6 items-center gap-1 text-xs font-medium text-slate-500 transition-colors hover:text-slate-900"
              >
                <ArrowLeft className="h-3.5 w-3.5 transition-transform group-hover/back:-translate-x-0.5" aria-hidden />
                {back.label}
              </Link>
            )}

            <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
              <div className="flex min-w-0 items-center gap-3.5">
                <div className="relative shrink-0">
                  {/* A soft glow that breathes behind the icon. */}
                  <motion.span
                    aria-hidden
                    className={cn('absolute -inset-1.5 rounded-[20px] blur-md', ring.halo)}
                    initial={false}
                    animate={reduce ? { opacity: 0.6 } : { opacity: [0.35, 0.8, 0.35], scale: [0.95, 1.05, 0.95] }}
                    transition={{ duration: 3.2, repeat: reduce ? 0 : Infinity, ease: 'easeInOut' }}
                  />
                  {avatar ?? (
                    <motion.div
                      className={cn('relative flex h-12 w-12 items-center justify-center overflow-hidden rounded-2xl bg-gradient-to-br shadow-lg', iconGradient ?? t.icon)}
                      animate={reduce ? undefined : { y: [0, -3, 0] }}
                      transition={{ duration: 4, repeat: Infinity, ease: 'easeInOut' }}
                      whileHover={reduce ? undefined : { rotate: [0, -8, 8, 0], transition: { duration: 0.5 } }}
                    >
                      <span aria-hidden className="absolute inset-0 bg-gradient-to-b from-white/35 via-white/5 to-transparent" />
                      <Icon className="relative h-6 w-6 text-white drop-shadow-sm" aria-hidden />
                    </motion.div>
                  )}
                  {!avatar && (
                    <motion.span
                      aria-hidden
                      className="absolute -right-1 -top-1 text-yellow-300 drop-shadow"
                      initial={false}
                      animate={reduce ? { opacity: 0 } : { opacity: [0, 1, 0], scale: [0.5, 1, 0.5], rotate: [0, 90, 180] }}
                      transition={{ duration: 2.6, repeat: reduce ? 0 : Infinity, repeatDelay: 1.4, ease: 'easeInOut' }}
                    >
                      <Sparkles className="h-3.5 w-3.5" />
                    </motion.span>
                  )}
                </div>
                <div className="min-w-0">
                  <div className="flex flex-wrap items-center gap-2">
                    <h1
                      className={cn(
                        'bg-gradient-to-r from-slate-900 to-slate-900 bg-[length:200%_100%] bg-clip-text text-xl font-bold tracking-tight text-transparent sm:text-2xl',
                        'motion-safe:animate-[shimmer_10s_linear_infinite]',
                        ring.via,
                      )}
                    >
                      {title}
                    </h1>
                    {badges}
                  </div>
                  {subtitle && <p className="mt-0.5 line-clamp-2 text-sm text-slate-500 sm:line-clamp-1">{subtitle}</p>}
                </div>
              </div>
              {actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}
            </div>

            {stats && stats.length > 0 && (
              <ul className="mt-3 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-[repeat(auto-fit,minmax(150px,1fr))]">
                {stats.map((stat, i) => <Stat key={stat.label} stat={stat} index={i} reduce={reduce} />)}
              </ul>
            )}

            {children && <div className="mt-3">{children}</div>}
          </div>
        </div>
      </motion.div>

      {/* The sales agent's next step sits right after the page title. */}
      {!noAgentSlot && <NextActionsSlot />}
    </>
  );
}

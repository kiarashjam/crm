import type { ElementType, ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowLeft } from 'lucide-react';
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

const STAT: Record<StatTone, { icon: string; on: string; hover: string }> = {
  slate: { icon: 'bg-slate-100 text-slate-600', on: 'bg-slate-50 ring-2 ring-slate-400', hover: 'hover:ring-slate-300' },
  indigo: { icon: 'bg-indigo-100 text-indigo-600', on: 'bg-indigo-50 ring-2 ring-indigo-400', hover: 'hover:ring-indigo-300' },
  violet: { icon: 'bg-violet-100 text-violet-600', on: 'bg-violet-50 ring-2 ring-violet-400', hover: 'hover:ring-violet-300' },
  blue: { icon: 'bg-blue-100 text-blue-600', on: 'bg-blue-50 ring-2 ring-blue-400', hover: 'hover:ring-blue-300' },
  sky: { icon: 'bg-sky-100 text-sky-600', on: 'bg-sky-50 ring-2 ring-sky-400', hover: 'hover:ring-sky-300' },
  cyan: { icon: 'bg-cyan-100 text-cyan-600', on: 'bg-cyan-50 ring-2 ring-cyan-400', hover: 'hover:ring-cyan-300' },
  teal: { icon: 'bg-teal-100 text-teal-600', on: 'bg-teal-50 ring-2 ring-teal-400', hover: 'hover:ring-teal-300' },
  emerald: { icon: 'bg-emerald-100 text-emerald-600', on: 'bg-emerald-50 ring-2 ring-emerald-400', hover: 'hover:ring-emerald-300' },
  amber: { icon: 'bg-amber-100 text-amber-600', on: 'bg-amber-50 ring-2 ring-amber-400', hover: 'hover:ring-amber-300' },
  orange: { icon: 'bg-orange-100 text-orange-600', on: 'bg-orange-50 ring-2 ring-orange-400', hover: 'hover:ring-orange-300' },
  rose: { icon: 'bg-rose-100 text-rose-600', on: 'bg-rose-50 ring-2 ring-rose-400', hover: 'hover:ring-rose-300' },
  pink: { icon: 'bg-pink-100 text-pink-600', on: 'bg-pink-50 ring-2 ring-pink-400', hover: 'hover:ring-pink-300' },
  fuchsia: { icon: 'bg-fuchsia-100 text-fuchsia-600', on: 'bg-fuchsia-50 ring-2 ring-fuchsia-400', hover: 'hover:ring-fuchsia-300' },
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
  const body = (
    <>
      <span
        className={cn(
          'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl transition-transform duration-300 group-hover/stat:-rotate-6 group-hover/stat:scale-110 motion-reduce:transition-none',
          t.icon,
        )}
        aria-hidden
      >
        <Icon className="h-4 w-4" />
      </span>
      <span className="min-w-0 flex-1">
        <span className="block truncate text-lg font-bold leading-tight tabular-nums text-slate-900">{value}</span>
        <span className="block truncate text-[11px] font-medium text-slate-500">
          {stat.label}
          {stat.hint && <span className="font-normal text-slate-400"> · {stat.hint}</span>}
        </span>
      </span>
      {stat.pressed && (
        <span className="shrink-0 rounded-full bg-slate-900 px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-wide text-white">On</span>
      )}
    </>
  );
  const className = cn(
    'group/stat flex h-full w-full min-w-0 items-center gap-2.5 rounded-xl bg-white/85 px-2.5 py-2 text-left ring-1 ring-slate-200/70 backdrop-blur',
    'transition-[translate,box-shadow] duration-200 hover:-translate-y-0.5 hover:shadow-md motion-reduce:transition-none motion-reduce:hover:translate-y-0',
    t.hover,
    stat.pressed && t.on,
  );
  return (
    <motion.li
      className="min-w-0"
      initial={reduce ? false : { opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.35, ease: EASE, delay: reduce ? 0 : 0.08 + index * 0.05 }}
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
 * its own colour; the motion (soft drifting light, a bobbing icon, numbers that
 * count up) is the same everywhere and switches off with reduced motion.
 */
export default function PageHero({
  icon: Icon, avatar, tone = 'orange', iconGradient, title, subtitle, badges, actions, stats, back, children, noAgentSlot = false,
}: PageHeroProps) {
  const reduce = useMotionPreference();
  const t = HERO[tone];
  return (
    <>
      <motion.div
        className={cn('relative mb-4 overflow-hidden rounded-2xl border bg-gradient-to-br shadow-sm', t.wash, t.border)}
        initial={reduce ? false : { opacity: 0, y: -8 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.4, ease: EASE }}
      >
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
        </div>

        <div className="relative px-4 py-4 sm:px-5">
          {back && (
            <Link
              to={back.to}
              className="group/back mb-2 inline-flex items-center gap-1 text-xs font-medium text-slate-500 transition-colors hover:text-slate-900"
            >
              <ArrowLeft className="h-3.5 w-3.5 transition-transform group-hover/back:-translate-x-0.5" aria-hidden />
              {back.label}
            </Link>
          )}

          <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
            <div className="flex min-w-0 items-center gap-3.5">
              {avatar ?? (
                <motion.div
                  className={cn('flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br shadow-lg', iconGradient ?? t.icon)}
                  animate={reduce ? undefined : { y: [0, -3, 0] }}
                  transition={{ duration: 4, repeat: Infinity, ease: 'easeInOut' }}
                  whileHover={reduce ? undefined : { rotate: [0, -8, 8, 0], transition: { duration: 0.5 } }}
                >
                  <Icon className="h-6 w-6 text-white" aria-hidden />
                </motion.div>
              )}
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  <h1 className="text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">{title}</h1>
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
      </motion.div>

      {/* The sales agent's next step sits right after the page title. */}
      {!noAgentSlot && <NextActionsSlot />}
    </>
  );
}

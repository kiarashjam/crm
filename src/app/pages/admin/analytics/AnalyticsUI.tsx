// The pieces every section of the site analytics is built from: a ranked bar list, the
// change badge, a sparkline, an empty note, an inline error with its retry, and skeletons.
// Cards, pills and section headings are the app's own (Settings and Reports).

import { useId, type ReactNode } from 'react';
import { AlertTriangle, ArrowDownRight, ArrowUpRight, Minus, Sparkles } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { RetryButton } from '@/app/components/LoadFailed';
import { useFormat } from '@/app/i18n';
import { directionOf, sentimentOf, type Delta } from './analyticsMath';

// ── Change badge ────────────────────────────────────────────────────────────

const SENTIMENT = {
  good: 'bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/25',
  bad: 'bg-rose-50 text-rose-700 ring-rose-200 dark:bg-rose-500/10 dark:text-rose-300 dark:ring-rose-500/25',
  neutral: 'bg-slate-100 text-slate-600 ring-slate-200 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10',
  new: 'bg-sky-50 text-sky-700 ring-sky-200 dark:bg-sky-500/10 dark:text-sky-300 dark:ring-sky-500/25',
} as const;

/** The change written out: "+38%", "−8 pts", "0%". */
export function useDeltaText(): (delta: Delta) => string {
  const fmt = useFormat();
  return (delta) => {
    if (delta.kind === 'new') return 'New';
    if (delta.kind === 'none') return '—';
    if (delta.unit === 'points') {
      const points = Math.round(delta.value * 100);
      return `${points > 0 ? '+' : points < 0 ? '−' : ''}${fmt.number(Math.abs(points))} pts`;
    }
    const text = fmt.number(Math.abs(delta.value), { style: 'percent', maximumFractionDigits: Math.abs(delta.value) >= 0.1 ? 0 : 1 });
    const direction = directionOf(delta);
    return `${direction === 'up' ? '+' : direction === 'down' ? '−' : ''}${text}`;
  };
}

/**
 * A figure's change against the period before: green when it is good news, red when it is
 * not (a bounce rate going up is red), "New" when there was nothing before. The arrow and
 * the words say it too, for anyone who cannot tell the colours apart.
 */
export function DeltaBadge({ delta, higherIsBetter = true, previousLabel, className }: {
  delta: Delta;
  higherIsBetter?: boolean;
  /** "the previous 30 days", for the words a screen reader hears. */
  previousLabel: string;
  className?: string;
}) {
  const text = useDeltaText()(delta);
  const direction = directionOf(delta);
  const tone = delta.kind === 'new' ? 'new' : sentimentOf(delta, higherIsBetter);
  const Icon = delta.kind === 'new' ? Sparkles : direction === 'up' ? ArrowUpRight : direction === 'down' ? ArrowDownRight : Minus;
  const spoken = delta.kind === 'new'
    ? `new: none in ${previousLabel}`
    : delta.kind === 'none'
      ? `none in ${previousLabel} either`
      : direction === 'flat'
        ? `about the same as ${previousLabel}`
        : `${direction === 'up' ? 'up' : 'down'} ${text.replace(/^[+−]/, '')} on ${previousLabel}`;
  return (
    <span
      title={`Compared with ${previousLabel}`}
      className={cn('inline-flex items-center gap-0.5 whitespace-nowrap rounded-full px-1.5 py-0.5 text-[11px] font-semibold ring-1', SENTIMENT[tone], className)}
    >
      {delta.kind !== 'none' && <Icon className="h-3 w-3" aria-hidden />}
      <span aria-hidden>{text}</span>
      <span className="sr-only">{spoken}</span>
    </span>
  );
}

// ── Sparkline ───────────────────────────────────────────────────────────────

/** A tiny line of the period's days; flat on the baseline while every day is zero. Decorative. */
export function Sparkline({ values, color, className }: { values: readonly number[]; color: string; className?: string }) {
  const gradient = useId();
  const width = 100;
  const height = 28;
  const pad = 2;
  const max = Math.max(...values, 0);
  const n = values.length;
  const points = values.map((v, i) => {
    const x = n <= 1 ? width / 2 : (i / (n - 1)) * width;
    const y = max > 0 ? height - pad - (v / max) * (height - pad * 2) : height - pad;
    return [x, y] as const;
  });
  if (points.length === 1) points.unshift([0, points[0]![1]]);
  const line = points.map(([x, y]) => `${x.toFixed(2)},${y.toFixed(2)}`).join(' ');
  const area = `0,${height} ${line} ${width},${height}`;
  const empty = max === 0;
  return (
    <svg viewBox={`0 0 ${width} ${height}`} preserveAspectRatio="none" aria-hidden className={cn('h-7 w-full overflow-visible', className)}>
      <defs>
        <linearGradient id={gradient} x1="0" y1="0" x2="0" y2="1">
          <stop offset="0%" stopColor={color} stopOpacity={0.25} />
          <stop offset="100%" stopColor={color} stopOpacity={0} />
        </linearGradient>
      </defs>
      {!empty && <polygon points={area} fill={`url(#${gradient})`} />}
      <polyline
        points={line}
        fill="none"
        stroke={empty ? 'currentColor' : color}
        strokeOpacity={empty ? 0.25 : 1}
        strokeWidth={1.75}
        strokeLinejoin="round"
        strokeLinecap="round"
        vectorEffect="non-scaling-stroke"
      />
    </svg>
  );
}

// ── Ranked bar list ─────────────────────────────────────────────────────────

export interface BarRow {
  key: string;
  label: ReactNode;
  /** The full text, on hover, for a label cut short. */
  title?: string;
  icon?: ReactNode;
  value: number;
  /** A second figure after the first: "9 visitors". */
  secondary?: ReactNode;
}

const BAR_TONE = {
  orange: 'bg-orange-100 dark:bg-orange-500/15',
  indigo: 'bg-indigo-100 dark:bg-indigo-500/15',
  violet: 'bg-violet-100 dark:bg-violet-500/15',
  sky: 'bg-sky-100 dark:bg-sky-500/15',
  emerald: 'bg-emerald-100 dark:bg-emerald-500/15',
  teal: 'bg-teal-100 dark:bg-teal-500/15',
} as const;
export type BarTone = keyof typeof BAR_TONE;

/**
 * Rows ranked by their figure, each with a soft bar behind it as long as its share of the
 * largest: easy to compare, and readable with one visit as with a thousand.
 */
export function BarRows({ rows, label, tone = 'orange', valueLabel, max, limit = 8, mono = false }: {
  rows: readonly BarRow[];
  /** The list's accessible name. */
  label: string;
  tone?: BarTone;
  /** What the figure counts, for screen readers: "visits". */
  valueLabel: string;
  /** The bars' 100%; the largest row by default. */
  max?: number;
  limit?: number;
  /** Paths read better in a fixed-width face. */
  mono?: boolean;
}) {
  const fmt = useFormat();
  const shown = rows.slice(0, limit);
  const top = max ?? Math.max(...shown.map((r) => r.value), 1);
  const rest = rows.length - shown.length;
  return (
    <>
      <ul aria-label={label} className="space-y-1">
        {shown.map((r) => (
          <li key={r.key} className="relative flex min-h-8 items-center gap-2 overflow-hidden rounded-lg px-2 py-1">
            <span
              aria-hidden
              className={cn('absolute inset-y-0 left-0 rounded-lg transition-[width] duration-500 motion-reduce:transition-none', BAR_TONE[tone])}
              style={{ width: `${top > 0 ? Math.max((r.value / top) * 100, r.value > 0 ? 3 : 0) : 0}%` }}
            />
            {r.icon && <span className="relative flex shrink-0 items-center">{r.icon}</span>}
            <span title={r.title} className={cn('relative min-w-0 flex-1 truncate text-sm text-slate-700 dark:text-slate-200', mono && 'font-mono text-[12.5px]')}>
              {r.label}
            </span>
            <span className="relative flex shrink-0 items-baseline gap-2 text-sm">
              <span className="font-semibold tabular-nums text-slate-900 dark:text-white">
                {fmt.number(r.value)}<span className="sr-only"> {valueLabel}</span>
              </span>
              {r.secondary && <span className="w-[4.5rem] text-right text-xs tabular-nums text-slate-500">{r.secondary}</span>}
            </span>
          </li>
        ))}
      </ul>
      {rest > 0 && <p className="mt-2 px-2 text-xs text-slate-500">and {fmt.number(rest)} more</p>}
    </>
  );
}

// ── Empty, error, loading ───────────────────────────────────────────────────

/** A section with nothing to show yet, said kindly. */
export function EmptyNote({ children, icon, className }: { children: ReactNode; icon?: ReactNode; className?: string }) {
  return (
    <div className={cn('flex min-h-[96px] flex-col items-center justify-center gap-2 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-4 py-5 text-center dark:border-white/10 dark:bg-white/[0.03]', className)}>
      {icon && <span aria-hidden className="text-slate-400">{icon}</span>}
      <p className="max-w-sm text-sm text-slate-500">{children}</p>
    </div>
  );
}

/** A read of one section that failed, with its retry: the rest of the page stays. */
export function InlineError({ what, error, onRetry, retrying }: { what: string; error: unknown; onRetry: () => void; retrying: boolean }) {
  const reason = error instanceof Error && error.message && error.message !== 'Failed to fetch' ? error.message : 'The server did not answer.';
  return (
    <div role="alert" className="flex flex-wrap items-center gap-3 rounded-xl border border-amber-200 bg-amber-50 px-3 py-2.5 text-sm text-amber-900 dark:border-amber-500/30 dark:bg-amber-500/10 dark:text-amber-100">
      <AlertTriangle className="h-4 w-4 shrink-0" aria-hidden />
      <p className="min-w-0 flex-1 basis-48">
        <strong className="font-semibold">{what} could not be loaded.</strong> {reason}
      </p>
      <RetryButton onRetry={onRetry} retrying={retrying} />
    </div>
  );
}

/** A grey block the shape of what is coming. */
export function Bone({ className }: { className?: string }) {
  return <div aria-hidden className={cn('animate-pulse rounded-lg bg-slate-100 motion-reduce:animate-none dark:bg-white/5', className)} />;
}

/** Rows of bones, for a list still loading. */
export function ListBones({ rows = 4 }: { rows?: number }) {
  return (
    <div className="space-y-2" aria-hidden>
      {Array.from({ length: rows }, (_, i) => <Bone key={i} className={cn('h-8', i % 2 ? 'w-4/5' : 'w-full')} />)}
    </div>
  );
}

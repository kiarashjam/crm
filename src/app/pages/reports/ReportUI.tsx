// The shared visual vocabulary for Reports.
//
// The page used to be two halves that had each grown their own KPI tile and
// chart card, with different radii, borders, label sizes and colour logic. That
// is most of why it read as bolted together rather than designed. One set of
// primitives here, used by every section, is the fix.

import type { ReactNode } from 'react';
import { cn } from '@/app/components/ui/utils';

/** Section accent, so each area of the page is identifiable at a glance. */
export type Accent = 'indigo' | 'teal' | 'violet' | 'amber' | 'rose' | 'sky' | 'emerald';

const ACCENT_TEXT: Record<Accent, string> = {
  indigo: 'text-indigo-600', teal: 'text-teal-600', violet: 'text-violet-600',
  amber: 'text-amber-600', rose: 'text-rose-600', sky: 'text-sky-600',
  emerald: 'text-emerald-600',
};
const ACCENT_SOFT: Record<Accent, string> = {
  indigo: 'bg-indigo-50 text-indigo-600', teal: 'bg-teal-50 text-teal-600',
  violet: 'bg-violet-50 text-violet-600', amber: 'bg-amber-50 text-amber-600',
  rose: 'bg-rose-50 text-rose-600', sky: 'bg-sky-50 text-sky-600',
  emerald: 'bg-emerald-50 text-emerald-600',
};
const ACCENT_BAR: Record<Accent, string> = {
  indigo: 'from-indigo-500 to-violet-500', teal: 'from-teal-500 to-emerald-500',
  violet: 'from-violet-500 to-fuchsia-500', amber: 'from-amber-500 to-orange-500',
  rose: 'from-rose-500 to-pink-500', sky: 'from-sky-500 to-cyan-500',
  emerald: 'from-emerald-500 to-teal-500',
};

/** Chart series colours, in the order charts should consume them. */
export const SERIES = ['#6366f1', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#ec4899', '#84cc16'];

// ── Section heading ──────────────────────────────────────────────────────────

export function SectionHead({
  id, eyebrow, title, blurb, accent = 'indigo', icon: Icon, right,
}: {
  id?: string;
  eyebrow: string;
  title: string;
  blurb?: string;
  accent?: Accent;
  icon?: React.ElementType;
  right?: ReactNode;
}) {
  return (
    <div id={id} className="mb-4 scroll-mt-24">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            {Icon && (
              <span className={cn('flex h-7 w-7 items-center justify-center rounded-lg', ACCENT_SOFT[accent])}>
                <Icon className="h-4 w-4" />
              </span>
            )}
            <span className={cn('text-[11px] font-bold uppercase tracking-[0.14em]', ACCENT_TEXT[accent])}>
              {eyebrow}
            </span>
          </div>
          <h2 className="mt-1.5 text-xl font-bold tracking-[-0.02em] text-slate-900 sm:text-2xl">{title}</h2>
          {blurb && <p className="mt-1 max-w-3xl text-[13px] leading-relaxed text-slate-500">{blurb}</p>}
        </div>
        {right}
      </div>
      <div className={cn('mt-3 h-[3px] w-16 rounded-full bg-gradient-to-r', ACCENT_BAR[accent])} />
    </div>
  );
}

// ── KPI tile ─────────────────────────────────────────────────────────────────

export function Kpi({
  label, value, sub, icon: Icon, accent = 'indigo', warn,
}: {
  label: string;
  /** Already formatted. Pass an em dash for "we do not know", never a zero. */
  value: string;
  sub?: string;
  icon?: React.ElementType;
  accent?: Accent;
  /** Marks a figure whose input data is incomplete, so it reads as a gap. */
  warn?: boolean;
}) {
  return (
    <div className={cn(
      'rounded-2xl border bg-white p-4 shadow-[0_1px_2px_rgba(15,23,42,0.04)]',
      'transition-colors duration-200',
      warn ? 'border-amber-200' : 'border-slate-200',
    )}>
      <div className="flex items-start justify-between gap-2">
        <span className="text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-400">{label}</span>
        {Icon && (
          <span className={cn('flex h-7 w-7 shrink-0 items-center justify-center rounded-lg', ACCENT_SOFT[accent])}>
            <Icon className="h-3.5 w-3.5" />
          </span>
        )}
      </div>
      <p className="mt-1.5 text-2xl font-bold tabular-nums tracking-[-0.02em] text-slate-900">{value}</p>
      {sub && (
        <p className={cn('mt-0.5 text-[11px]', warn ? 'font-medium text-amber-700' : 'text-slate-400')}>
          {sub}
        </p>
      )}
    </div>
  );
}

// ── Chart / content panel ────────────────────────────────────────────────────

export function Panel({
  title, subtitle, hint, children, className,
}: {
  title: string;
  subtitle?: string;
  /** How the number is derived. Trust is repaired by showing the arithmetic. */
  hint?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={cn(
      'rounded-2xl border border-slate-200 bg-white p-4 shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:p-5',
      className,
    )}>
      <div className="mb-3">
        <h3 className="text-sm font-bold text-slate-800">{title}</h3>
        {subtitle && <p className="mt-0.5 text-[11px] text-slate-400">{subtitle}</p>}
      </div>
      {children}
      {hint && (
        <p className="mt-3 border-t border-slate-100 pt-2 text-[10.5px] leading-relaxed text-slate-400">
          {hint}
        </p>
      )}
    </section>
  );
}

/** Shown in a panel instead of an axis-less empty chart. */
export function NoData({ children = 'Nothing recorded yet.' }: { children?: ReactNode }) {
  return (
    <div className="flex min-h-[120px] items-center justify-center rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-4 text-center">
      <p className="text-xs text-slate-400">{children}</p>
    </div>
  );
}

// ── Horizontal bar list ──────────────────────────────────────────────────────

/**
 * A ranked breakdown as labelled bars rather than a pie.
 *
 * Pie charts of six-plus categories are close to unreadable, and the old page
 * used them for exactly that. Sorted bars with the number printed are easier to
 * compare and cannot mislead about proportion.
 */
export function BarList({
  rows, total, accent = 'indigo', format,
}: {
  rows: { name: string; value: number }[];
  /** Denominator for the bar widths. Defaults to the largest row. */
  total?: number;
  accent?: Accent;
  format?: (v: number) => string;
}) {
  if (rows.length === 0) return <NoData />;
  const max = total ?? Math.max(...rows.map((r) => r.value), 1);
  const fmt = format ?? ((v: number) => String(v));
  return (
    <ul className="space-y-2">
      {rows.map((r) => (
        <li key={r.name}>
          <div className="flex items-baseline justify-between gap-3">
            <span className="truncate text-xs font-medium text-slate-600">{r.name}</span>
            <span className="shrink-0 text-xs font-bold tabular-nums text-slate-800">{fmt(r.value)}</span>
          </div>
          <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-slate-100">
            <div
              className={cn('h-full rounded-full bg-gradient-to-r', ACCENT_BAR[accent])}
              style={{ width: `${max > 0 ? Math.max((r.value / max) * 100, r.value > 0 ? 2 : 0) : 0}%` }}
            />
          </div>
        </li>
      ))}
    </ul>
  );
}

// ── Funnel ───────────────────────────────────────────────────────────────────

/** Steps drawn as nested bars, with the drop from the previous step named. */
export function Funnel({
  steps, accent = 'teal',
}: {
  steps: { name: string; value: number; share: number }[];
  accent?: Accent;
}) {
  const top = steps[0]?.value ?? 0;
  if (top === 0) return <NoData>No leads yet, so there is no funnel to draw.</NoData>;
  return (
    <ul className="space-y-2.5">
      {steps.map((s, i) => {
        const prev = i > 0 ? steps[i - 1]!.value : null;
        const dropped = prev !== null ? prev - s.value : 0;
        return (
          <li key={s.name}>
            <div className="flex items-baseline justify-between gap-3">
              <span className="text-xs font-semibold text-slate-700">{s.name}</span>
              <span className="shrink-0 text-xs tabular-nums text-slate-500">
                <b className="text-slate-900">{s.value}</b>
                <span className="ml-1.5 text-slate-400">{Math.round(s.share * 100)}%</span>
              </span>
            </div>
            <div className="mt-1 h-2.5 overflow-hidden rounded-full bg-slate-100">
              <div
                className={cn('h-full rounded-full bg-gradient-to-r', ACCENT_BAR[accent])}
                style={{ width: `${(s.value / top) * 100}%` }}
              />
            </div>
            {prev !== null && dropped > 0 && (
              <p className="mt-1 text-[10.5px] text-rose-500">
                −{dropped} did not reach this step
              </p>
            )}
          </li>
        );
      })}
    </ul>
  );
}

// The six headline figures: each with its value, its change against the period before and
// a sparkline of the period's days.

import type { ComponentType } from 'react';
import { Clock, Eye, LogOut, MousePointerClick, UserPlus, Users } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useFormat } from '@/app/i18n';
import type { SiteAnalyticsDay, SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { formatDuration, kpiDelta, type KpiKey } from './analyticsMath';
import { Bone, DeltaBadge, Sparkline } from './AnalyticsUI';

interface KpiDef {
  key: KpiKey;
  label: string;
  icon: ComponentType<{ className?: string }>;
  tile: string;
  /** The sparkline's colour, light and dark. */
  line: [string, string];
  /** The day's figure the sparkline draws; none for a rate the days do not carry. */
  daily?: (d: SiteAnalyticsDay) => number;
  higherIsBetter?: boolean;
  hint: string;
}

const KPIS: KpiDef[] = [
  {
    key: 'visitors', label: 'Visitors', icon: Users, line: ['#ea580c', '#fb923c'], daily: (d) => d.visitors,
    tile: 'bg-orange-50 text-orange-600 dark:bg-orange-500/10 dark:text-orange-300', hint: 'different people, counted once a day',
  },
  {
    key: 'sessions', label: 'Visits', icon: MousePointerClick, line: ['#db2777', '#f472b6'], daily: (d) => d.sessions,
    tile: 'bg-pink-50 text-pink-600 dark:bg-pink-500/10 dark:text-pink-300', hint: 'sessions on the site',
  },
  {
    key: 'pageViews', label: 'Page views', icon: Eye, line: ['#6366f1', '#818cf8'], daily: (d) => d.pageViews,
    tile: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-300', hint: 'pages opened',
  },
  {
    key: 'signups', label: 'Sign-ups', icon: UserPlus, line: ['#059669', '#34d399'], daily: (d) => d.signups,
    tile: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300', hint: 'new CadenceE accounts',
  },
  {
    key: 'bounceRate', label: 'Bounce rate', icon: LogOut, line: ['#0284c7', '#38bdf8'], higherIsBetter: false,
    tile: 'bg-sky-50 text-sky-600 dark:bg-sky-500/10 dark:text-sky-300', hint: 'left after one page',
  },
  {
    key: 'avgSessionSeconds', label: 'Avg. visit', icon: Clock, line: ['#7c3aed', '#a78bfa'],
    tile: 'bg-violet-50 text-violet-600 dark:bg-violet-500/10 dark:text-violet-300', hint: 'time on the site per visit',
  },
];

/** For a rate the days do not carry: two bars, the period before and this one. */
function CompareBars({ previous, current, color }: { previous: number; current: number; color: string }) {
  const max = Math.max(previous, current, 0);
  const width = (v: number) => `${max > 0 ? Math.max((v / max) * 100, v > 0 ? 4 : 0) : 0}%`;
  return (
    <div aria-hidden className="flex h-7 flex-col justify-center gap-1">
      <span className="h-1.5 rounded-full bg-slate-200 dark:bg-white/15" style={{ width: width(previous) }} />
      <span className="h-1.5 rounded-full" style={{ width: width(current), backgroundColor: color }} />
    </div>
  );
}

export function KpiGrid({ summary, previousLabel, dark }: { summary: SiteAnalyticsSummary; previousLabel: string; dark: boolean }) {
  const fmt = useFormat();
  const { totals, previous, daily } = summary;
  const valueOf = (key: KpiKey, t: typeof totals): string => {
    if (key === 'bounceRate') return t.sessions > 0 ? fmt.number(t.bounceRate, { style: 'percent', maximumFractionDigits: 0 }) : '—';
    if (key === 'avgSessionSeconds') return t.sessions > 0 ? formatDuration(t.avgSessionSeconds) : '—';
    return fmt.number(t[key]);
  };
  return (
    <ul aria-label="Key figures" className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
      {KPIS.map((k) => {
        const Icon = k.icon;
        const color = dark ? k.line[1] : k.line[0];
        const delta = kpiDelta(k.key, totals, previous);
        return (
          <li
            key={k.key}
            className="flex min-w-0 flex-col rounded-2xl border border-slate-200 bg-white p-3.5 shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:p-4"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="truncate text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">{k.label}</span>
              <span aria-hidden className={cn('flex h-7 w-7 shrink-0 items-center justify-center rounded-lg', k.tile)}>
                <Icon className="h-3.5 w-3.5" />
              </span>
            </div>
            <div className="mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1">
              <p className="text-2xl font-bold tracking-[-0.02em] text-slate-900 dark:text-white" data-testid={`kpi-${k.key}`}>
                {valueOf(k.key, totals)}
              </p>
              <DeltaBadge delta={delta} higherIsBetter={k.higherIsBetter ?? true} previousLabel={previousLabel} />
            </div>
            <p className="mt-0.5 truncate text-[11px] text-slate-500" title={k.hint}>{k.hint}</p>
            <div className="mt-2 text-slate-400">
              {k.daily
                ? <Sparkline values={daily.map(k.daily)} color={color} />
                : <CompareBars previous={k.key === 'bounceRate' ? previous.bounceRate : previous.avgSessionSeconds} current={k.key === 'bounceRate' ? totals.bounceRate : totals.avgSessionSeconds} color={color} />}
            </div>
          </li>
        );
      })}
    </ul>
  );
}

export function KpiGridSkeleton() {
  return (
    <div aria-hidden className="grid grid-cols-2 gap-3 md:grid-cols-3 xl:grid-cols-6">
      {KPIS.map((k) => (
        <div key={k.key} className="rounded-2xl border border-slate-200 bg-white p-4">
          <Bone className="h-3 w-20" />
          <Bone className="mt-3 h-7 w-16" />
          <Bone className="mt-3 h-7 w-full" />
        </div>
      ))}
    </div>
  );
}

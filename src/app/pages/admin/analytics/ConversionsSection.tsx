// What they do: the events the site sent, and the sign-up funnel from visitor to account,
// with the share that reached each step.

import { Filter, Zap } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useFormat } from '@/app/i18n';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import type { SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { eventLabel, funnelSteps, nonEmpty } from './analyticsMath';
import { BarRows, EmptyNote } from './AnalyticsUI';

const STEP_BAR = ['bg-orange-500', 'bg-orange-400', 'bg-amber-400', 'bg-emerald-500'];

export function Funnel({ summary }: { summary: SiteAnalyticsSummary }) {
  const fmt = useFormat();
  const steps = funnelSteps(summary.totals, summary.events);
  const pct = (v: number | null) => (v === null ? '—' : fmt.number(v, { style: 'percent', maximumFractionDigits: v > 0 && v < 0.1 ? 1 : 0 }));
  const last = steps[steps.length - 1]!;
  if (summary.totals.visitors === 0) {
    return <EmptyNote>No visitors yet, so no funnel to draw. Share your link to get the first visitors!</EmptyNote>;
  }
  return (
    <div>
      <ol aria-label="Sign-up funnel" className="space-y-3">
        {steps.map((s, i) => (
          <li key={s.key} data-testid={`funnel-${s.key}`}>
            <div className="flex items-baseline justify-between gap-3">
              <span className="min-w-0 text-sm font-medium text-slate-800 dark:text-slate-100">
                <span aria-hidden className="mr-1.5 inline-flex h-5 w-5 items-center justify-center rounded-full bg-slate-100 text-[10px] font-bold text-slate-500 dark:bg-white/10 dark:text-slate-300">{i + 1}</span>
                {s.label}
              </span>
              <strong className="shrink-0 text-sm font-semibold tabular-nums text-slate-900 dark:text-white">{fmt.number(s.value)}</strong>
            </div>
            <div className="mt-1.5 h-2.5 overflow-hidden rounded-full bg-slate-100 dark:bg-white/10" aria-hidden>
              <div
                className={cn('h-full rounded-full transition-[width] duration-500 motion-reduce:transition-none', STEP_BAR[i])}
                style={{ width: `${Math.min(100, Math.max((s.ofVisitors ?? 0) * 100, s.value > 0 ? 2 : 0))}%` }}
              />
            </div>
            {i > 0 && (
              <p className="mt-1 text-xs text-slate-500">
                <span className="font-semibold text-slate-700 dark:text-slate-200" data-testid={`funnel-${s.key}-step`}>{pct(s.ofPrevious)}</span> of the step before
                {' · '}<span data-testid={`funnel-${s.key}-visitors`}>{pct(s.ofVisitors)}</span> of visitors
              </p>
            )}
          </li>
        ))}
      </ol>
      <p className="mt-4 rounded-xl bg-emerald-50 px-3 py-2 text-sm text-emerald-800 dark:bg-emerald-500/10 dark:text-emerald-200" data-testid="funnel-overall">
        <strong className="font-semibold">{pct(last.ofVisitors)}</strong> of visitors signed up
        {' '}({fmt.number(last.value)} of {fmt.number(summary.totals.visitors)}).
      </p>
    </div>
  );
}

export function ConversionsSection({ summary }: { summary: SiteAnalyticsSummary }) {
  const events = nonEmpty(summary.events, (e) => e.count);
  return (
    <div className="grid gap-4 lg:grid-cols-2 [&>*]:min-w-0">
      <SettingsCard icon={Filter} tone="emerald" title="Sign-up funnel" description="From a visit to a CadenceE account, step by step.">
        <Funnel summary={summary} />
      </SettingsCard>
      <SettingsCard icon={Zap} tone="pink" title="Events" description="Clicks and actions the site recorded.">
        {events.length === 0 ? (
          <EmptyNote>No events yet in this period. Sign-up clicks, logins and outbound links show up here.</EmptyNote>
        ) : (
          <BarRows
            label="Events"
            tone="emerald"
            valueLabel="times"
            limit={10}
            rows={events.map((e) => ({
              key: e.name,
              label: <>{eventLabel(e.name)} <span className="ml-1 font-mono text-[11px] text-slate-400">{e.name}</span></>,
              title: e.name,
              value: e.count,
            }))}
          />
        )}
      </SettingsCard>
    </div>
  );
}

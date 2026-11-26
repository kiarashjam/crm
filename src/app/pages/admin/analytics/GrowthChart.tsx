// Growth: daily visitors (an area) and page views (a line) on one count axis, with the
// visitors' 7-day average drawn through them, and the headline beside it: how visitors moved
// against the period before, and the best day. Built for a young site: the axis never tops
// out below 4, so one visitor is not drawn as a mountain, and a day with nobody sits on the
// baseline rather than breaking the line. Its summary is written out for screen readers,
// and the days can be read as a table.

import { useId, useMemo, useState } from 'react';
import { Table2, TrendingDown, TrendingUp, Trophy } from 'lucide-react';
import {
  Area, CartesianGrid, ComposedChart, Line, ResponsiveContainer, Tooltip, XAxis, YAxis, type TooltipProps,
} from 'recharts';
import { cn } from '@/app/components/ui/utils';
import { useFormat } from '@/app/i18n';
import { formatDay } from '@/app/lib/zonedTime';
import type { SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import {
  averageTrend, bestDay, countDelta, directionOf, growthRows, niceMax, zeroDays, type GrowthRow,
} from './analyticsMath';
import { useDeltaText } from './AnalyticsUI';

/** Validated for both themes (dataviz palette check: orange and indigo, one axis). */
const COLOURS = {
  light: { visitors: '#ea580c', pageViews: '#6366f1', average: '#9a3412', grid: '#eef2f7', tick: '#64748b' },
  dark: { visitors: '#ea580c', pageViews: '#818cf8', average: '#fdba74', grid: 'rgba(255,255,255,0.08)', tick: '#94a3b8' },
};

function ChartTooltip({ active, payload, locale, numbers }: TooltipProps<number, string> & { locale: string; numbers: (n: number, o?: Intl.NumberFormatOptions) => string }) {
  const row = payload?.[0]?.payload as GrowthRow | undefined;
  if (!active || !row) return null;
  return (
    <div className="rounded-xl border border-slate-200 bg-white px-3 py-2 text-xs shadow-lg dark:border-white/10 dark:bg-slate-800">
      <p className="font-semibold text-slate-900 dark:text-white">{formatDay(row.date, { weekday: 'short', day: 'numeric', month: 'short' }, locale)}</p>
      <dl className="mt-1 grid grid-cols-[auto_auto] gap-x-3 gap-y-0.5 text-slate-600 dark:text-slate-300">
        <dt>Visitors</dt><dd className="text-right font-semibold tabular-nums text-slate-900 dark:text-white">{numbers(row.visitors)}</dd>
        <dt>Page views</dt><dd className="text-right font-semibold tabular-nums text-slate-900 dark:text-white">{numbers(row.pageViews)}</dd>
        <dt>7-day average</dt><dd className="text-right tabular-nums">{numbers(row.average, { maximumFractionDigits: 1 })}</dd>
        {row.signups > 0 && (<><dt>Sign-ups</dt><dd className="text-right tabular-nums">{numbers(row.signups)}</dd></>)}
      </dl>
    </div>
  );
}

function Swatch({ color, kind, label }: { color: string; kind: 'area' | 'line' | 'dashed'; label: string }) {
  return (
    <li className="flex items-center gap-1.5">
      {kind === 'dashed'
        ? <span aria-hidden className="inline-block w-4 border-t-2 border-dashed" style={{ borderColor: color }} />
        : <span aria-hidden className={cn('inline-block w-4 rounded-full', kind === 'area' ? 'h-2.5 opacity-80' : 'h-[3px]')} style={{ backgroundColor: color }} />}
      {label}
    </li>
  );
}

export function GrowthChart({ summary, previousLabel, dark }: { summary: SiteAnalyticsSummary; previousLabel: string; dark: boolean }) {
  const fmt = useFormat();
  const deltaText = useDeltaText();
  const summaryId = useId();
  const gradientId = `growth-${useId().replace(/:/g, '')}`;
  const [asTable, setAsTable] = useState(false);
  const c = dark ? COLOURS.dark : COLOURS.light;

  const rows = useMemo(() => growthRows(summary.daily), [summary.daily]);
  const best = useMemo(() => bestDay(summary.daily), [summary.daily]);
  const trend = useMemo(() => {
    const t = averageTrend(rows);
    return t && (t.start > 0 || t.end > 0) ? t : null;
  }, [rows]);
  const empty = zeroDays(summary.daily);
  const delta = countDelta(summary.totals.visitors, summary.previous.visitors);
  const direction = directionOf(delta);
  const top = niceMax(Math.max(0, ...rows.map((r) => Math.max(r.visitors, r.pageViews))));
  const day = (key: string, options: Intl.DateTimeFormatOptions = { day: 'numeric', month: 'short' }) => formatDay(key, options, fmt.dateLocale);
  const one = (n: number) => fmt.number(n, { maximumFractionDigits: 1 });
  const plural = (n: number, word: string) => `${fmt.number(n)} ${word}${n === 1 ? '' : 's'}`;

  const headline = delta.kind === 'new'
    ? { big: 'New', text: `${plural(summary.totals.visitors, 'visitor')}, none in ${previousLabel}` }
    : delta.kind === 'none'
      ? { big: '0', text: 'visitors so far: share your link to get the first ones' }
      : { big: deltaText(delta), text: `visitors vs ${previousLabel}` };
  const headlineTone = delta.kind === 'new' || direction === 'up'
    ? 'text-emerald-700 dark:text-emerald-300'
    : direction === 'down' ? 'text-rose-700 dark:text-rose-300' : 'text-slate-700 dark:text-slate-200';
  const HeadlineIcon = direction === 'down' ? TrendingDown : TrendingUp;

  const spoken = [
    `${summary.from === summary.to ? `On ${day(summary.from)}` : `From ${day(summary.from)} to ${day(summary.to)}`}: ${plural(summary.totals.visitors, 'visitor')} and ${plural(summary.totals.pageViews, 'page view')}.`,
    delta.kind === 'change' ? `Visitors ${direction === 'flat' ? 'about the same as' : `${direction} ${deltaText(delta).replace(/^[+−]/, '')} on`} ${previousLabel}.` : '',
    delta.kind === 'new' ? `None in ${previousLabel}.` : '',
    best ? `Best day: ${day(best.date, { weekday: 'long', day: 'numeric', month: 'long' })}, with ${plural(best.visitors, 'visitor')}.` : 'No day had a visitor yet.',
    trend ? `The 7-day average went from ${one(trend.start)} to ${one(trend.end)} visitors a day.` : '',
    summary.daily.length > 1 ? `${plural(empty, 'day')} without a visitor.` : '',
  ].filter(Boolean).join(' ');

  return (
    <div className="flex h-full flex-col gap-4">
      <div className="flex flex-wrap items-start gap-x-6 gap-y-3">
        <div className="flex min-w-0 items-center gap-3">
          <span aria-hidden className={cn('flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl ring-1', delta.kind === 'new' || direction === 'up' ? 'bg-emerald-50 text-emerald-600 ring-emerald-100 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/20' : direction === 'down' ? 'bg-rose-50 text-rose-600 ring-rose-100 dark:bg-rose-500/10 dark:text-rose-300 dark:ring-rose-500/20' : 'bg-slate-100 text-slate-500 ring-slate-200 dark:bg-white/5 dark:ring-white/10')}>
            <HeadlineIcon className="h-5 w-5" />
          </span>
          <p className="min-w-0" data-testid="growth-callout">
            <span className={cn('block text-2xl font-bold tracking-[-0.02em]', headlineTone)}>{headline.big}</span>
            <span className="block text-xs text-slate-500">{headline.text}</span>
          </p>
        </div>
        {best && (
          <p className="flex min-w-0 items-center gap-2 text-sm text-slate-600 dark:text-slate-300" data-testid="best-day">
            <Trophy className="h-4 w-4 shrink-0 text-amber-500" aria-hidden />
            <span>
              Best day: <strong className="font-semibold text-slate-900 dark:text-white">{day(best.date, { weekday: 'short', day: 'numeric', month: 'short' })}</strong>
              {' · '}{plural(best.visitors, 'visitor')}
            </span>
          </p>
        )}
        {trend && (
          <p className="text-sm text-slate-600 dark:text-slate-300">
            7-day average <strong className="font-semibold tabular-nums text-slate-900 dark:text-white">{one(trend.start)} → {one(trend.end)}</strong> a day
          </p>
        )}
      </div>

      <p id={summaryId} className="sr-only" data-testid="growth-summary">{spoken}</p>

      {summary.daily.length <= 1 ? (
        <div className="flex min-h-[200px] flex-1 flex-col items-center justify-center gap-1 rounded-xl border border-dashed border-slate-200 bg-slate-50/60 px-4 text-center dark:border-white/10 dark:bg-white/[0.03]">
          <p className="text-3xl font-bold text-slate-900 dark:text-white">{fmt.number(summary.totals.visitors)}</p>
          <p className="text-sm text-slate-500">{summary.totals.visitors === 1 ? 'visitor' : 'visitors'} today, {plural(summary.totals.pageViews, 'page view')}</p>
          <p className="mt-2 text-xs text-slate-400">Pick 7 days or more to see the trend.</p>
        </div>
      ) : (
        <>
          <div className="flex flex-wrap items-center justify-between gap-2">
            <ul aria-label="Legend" className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-600 dark:text-slate-300">
              <Swatch color={c.visitors} kind="area" label="Visitors" />
              <Swatch color={c.pageViews} kind="line" label="Page views" />
              <Swatch color={c.average} kind="dashed" label="Visitors, 7-day average" />
            </ul>
            <button
              type="button"
              onClick={() => setAsTable((v) => !v)}
              aria-pressed={asTable}
              className="inline-flex h-7 items-center gap-1 rounded-lg border border-slate-200 bg-white px-2 text-[11px] font-semibold text-slate-600 hover:bg-slate-50 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:border-white/10 dark:bg-transparent dark:text-slate-300"
            >
              <Table2 className="h-3 w-3" aria-hidden />
              {asTable ? 'Show chart' : 'Show table'}
            </button>
          </div>
          {asTable ? (
            <div className="max-h-[260px] overflow-auto rounded-xl border border-slate-200 dark:border-white/10">
              <table className="w-full text-left text-sm">
                <caption className="sr-only">Visitors and page views per day</caption>
                <thead className="sticky top-0 bg-slate-50 text-xs text-slate-500 dark:bg-slate-800">
                  <tr>
                    <th scope="col" className="px-3 py-2 font-medium">Day</th>
                    <th scope="col" className="px-3 py-2 text-right font-medium">Visitors</th>
                    <th scope="col" className="px-3 py-2 text-right font-medium">Page views</th>
                    <th scope="col" className="px-3 py-2 text-right font-medium">7-day avg.</th>
                    <th scope="col" className="px-3 py-2 text-right font-medium">Sign-ups</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100 tabular-nums dark:divide-white/5">
                  {[...rows].reverse().map((r) => (
                    <tr key={r.date}>
                      <th scope="row" className="px-3 py-1.5 font-normal text-slate-700 dark:text-slate-200">{day(r.date, { weekday: 'short', day: 'numeric', month: 'short' })}</th>
                      <td className="px-3 py-1.5 text-right">{fmt.number(r.visitors)}</td>
                      <td className="px-3 py-1.5 text-right">{fmt.number(r.pageViews)}</td>
                      <td className="px-3 py-1.5 text-right text-slate-500">{one(r.average)}</td>
                      <td className="px-3 py-1.5 text-right">{fmt.number(r.signups)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div role="img" aria-label="Daily visitors and page views" aria-describedby={summaryId} className="h-[240px] w-full sm:h-[260px]">
              <ResponsiveContainer width="100%" height="100%">
                <ComposedChart data={rows} margin={{ top: 8, right: 8, bottom: 0, left: -16 }}>
                  <defs>
                    <linearGradient id={gradientId} x1="0" y1="0" x2="0" y2="1">
                      <stop offset="0%" stopColor={c.visitors} stopOpacity={dark ? 0.35 : 0.22} />
                      <stop offset="100%" stopColor={c.visitors} stopOpacity={0.02} />
                    </linearGradient>
                  </defs>
                  <CartesianGrid vertical={false} stroke={c.grid} />
                  <XAxis
                    dataKey="date"
                    tickFormatter={(d: string) => day(d)}
                    tick={{ fontSize: 11, fill: c.tick }}
                    tickLine={false}
                    axisLine={false}
                    minTickGap={28}
                    tickMargin={8}
                  />
                  <YAxis
                    allowDecimals={false}
                    domain={[0, top]}
                    tickCount={5}
                    tick={{ fontSize: 11, fill: c.tick }}
                    tickFormatter={(v: number) => fmt.number(v)}
                    tickLine={false}
                    axisLine={false}
                    width={44}
                  />
                  <Tooltip
                    cursor={{ stroke: c.tick, strokeOpacity: 0.3 }}
                    content={<ChartTooltip locale={fmt.dateLocale} numbers={fmt.number} />}
                  />
                  <Area
                    type="monotone"
                    dataKey="visitors"
                    name="Visitors"
                    stroke={c.visitors}
                    strokeWidth={2}
                    fill={`url(#${gradientId})`}
                    dot={rows.length <= 14 ? { r: 2.5, strokeWidth: 0, fill: c.visitors } : false}
                    activeDot={{ r: 4.5, strokeWidth: 2, stroke: dark ? '#1e293b' : '#ffffff' }}
                    isAnimationActive={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="pageViews"
                    name="Page views"
                    stroke={c.pageViews}
                    strokeWidth={2}
                    dot={false}
                    activeDot={{ r: 4, strokeWidth: 2, stroke: dark ? '#1e293b' : '#ffffff' }}
                    isAnimationActive={false}
                  />
                  <Line
                    type="monotone"
                    dataKey="average"
                    name="7-day average"
                    stroke={c.average}
                    strokeWidth={2}
                    strokeDasharray="5 4"
                    dot={false}
                    activeDot={false}
                    isAnimationActive={false}
                  />
                </ComposedChart>
              </ResponsiveContainer>
            </div>
          )}
        </>
      )}
    </div>
  );
}

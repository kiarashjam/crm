import { useEffect, useMemo, useRef, useState } from 'react';
import {
  BarChart3, TrendingUp, Target, DollarSign, Percent, Users, UserCheck,
  Gauge, Sparkles, Activity as ActivityIcon, Kanban, Filter, AlertTriangle, Info, CalendarClock, Timer,
} from 'lucide-react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  AreaChart, Area,
} from 'recharts';
import AppHeader from '@/app/components/AppHeader';
import { PageEnter, Reveal } from '@/app/components/motion/PageEnter';
import ReportsSkeleton from './reports/ReportsSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { cn } from '@/app/components/ui/utils';
import { getLeads, getPipelines } from '@/app/api';
import type { Lead, Pipeline } from '@/app/api/types';
import {
  getForecast, getReportSummary, getSalesTeams,
  type Forecast, type ReportSummary, type SalesTeam,
} from '@/app/api/reporting';
import { SalesTrackerReport } from './reports/SalesTrackerReport';
import { ConversionReportPanel } from './reports/ConversionReportPanel';
import { SectionHead, Kpi, Panel, NoData, BarList, Funnel, CsvButton } from './reports/ReportUI';
import { statusFunnelFromCounts, summariseStatuses, groupSum, topNWithOther, rowFor } from './reports/reportMetrics';
import {
  PERIOD_LABELS, bucketLabel, dateInputValue, loadSavedFilters, monthLabel, monthOf, periodFor, saveFilters,
  toReportFilters, type FilterState, type PeriodPreset,
} from './reports/reportFilters';
import {
  countTable, csvFileName, forecastTable, leadStatusTable, overviewTable, ownerTable, seriesTable, stageTable, type CsvTable,
} from './reports/reportCsv';
import { downloadCsv } from '@/app/lib/exportCsv';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { servesDemoData } from '@/app/api/apiClient';
import { isOrgAdmin } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { SavedForecastPanel } from './reports/SavedForecastPanel';
import { useMoney } from '@/app/hooks/useMoney';

const pct = (v: number | null) => (v === null ? '—' : `${Math.round(v * 100)}%`);
const plural = (n: number, one: string, many = `${one}s`) => `${n} ${n === 1 ? one : many}`;

/** `cold_call` → `Cold call`. Sources are stored as raw enum-ish strings. */
function humanise(s: string): string {
  const t = s.replace(/[_-]+/g, ' ').trim();
  return t.length === 0 ? 'Unknown' : t[0]!.toUpperCase() + t.slice(1);
}

const SECTIONS = [
  { id: 'overview', label: 'Overview' },
  { id: 'pipeline', label: 'Deals' },
  { id: 'forecast', label: 'Forecast' },
  { id: 'leads', label: 'Leads' },
  { id: 'lifecycle', label: 'Lifecycle' },
  { id: 'activity', label: 'Activity' },
] as const;

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

/** Last day of a period ending at the exclusive instant `to`, as yyyy-MM-dd in `timeZone`. */
function lastDayOf(toIso: string, timeZone: string): string {
  return dateInputValue(new Date(Date.parse(toIso) - 1), timeZone);
}

export default function Reports() {
  const [filters, setFilters] = useState<FilterState>(() => loadSavedFilters());
  const [summary, setSummary] = useState<ReportSummary | null>(null);
  const [forecast, setForecast] = useState<Forecast | null>(null);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [forecastError, setForecastError] = useState(false);
  const [pipelines, setPipelines] = useState<Pipeline[]>([]);
  const [teams, setTeams] = useState<SalesTeam[]>([]);
  usePageLoading(loading);
  // Every figure is written in the workspace's format, and its periods and months
  // begin at the workspace's midnight, not at this browser's.
  const regional = useMoney();
  const { timeZone, locale } = regional;
  const money = (n: number, currency: string) => regional.format(n, currency, 'never');
  const orgs = useOrgOptional();
  const org = orgs?.currentOrg ?? null;
  // Saved forecasts live on the server and, in an organization, are a manager's: they
  // count private deals. A personal workspace's are its owner's. Wait for the
  // organizations to load, or everyone looks personal for a moment.
  const canSaveForecast = !servesDemoData() && Boolean(orgs?.hasFetched) && (org === null || isOrgAdmin(org));

  const request = useMemo(() => toReportFilters(filters, timeZone), [filters, timeZone]);
  const latest = useRef(0);

  useEffect(() => { saveFilters(filters); }, [filters]);

  useEffect(() => {
    // The filters are remembered per browser, not per workspace. A pipeline or team
    // remembered from another workspace (or since deleted) is not this one's: the
    // server refused every request for it, and the picker, which lists only this
    // workspace's, could not show what was being asked for. Without a list there
    // is no picker at all, so nothing is narrowed by what cannot be seen.
    getPipelines()
      .then((list) => {
        setPipelines(list);
        setFilters((f) => (f.pipelineId && !list.some((p) => p.id === f.pipelineId) ? { ...f, pipelineId: '' } : f));
      })
      .catch(() => {
        setPipelines([]);
        setFilters((f) => (f.pipelineId ? { ...f, pipelineId: '' } : f));
      });
    const noTeams = () => setFilters((f) => (f.scope === 'team' || f.teamId ? { ...f, scope: f.scope === 'team' ? 'org' : f.scope, teamId: '' } : f));
    getSalesTeams()
      .then((list) => {
        setTeams(list);
        // "My team" remembered from a workspace that has teams, in one that has none.
        if (list.length === 0) noTeams();
        else setFilters((f) => (f.teamId && !list.some((t) => t.id === f.teamId) ? { ...f, teamId: '' } : f));
      })
      .catch(() => {
        // A personal workspace has no teams, and the server says so with a refusal.
        setTeams([]);
        noTeams();
      });
  }, []);

  useEffect(() => {
    // A custom period still being typed asks for nothing, and must not leave the
    // page on its skeleton.
    if (!request) {
      setLoading(false);
      return;
    }
    const ticket = ++latest.current;
    setRefreshing(true);
    setError(null);
    setForecastError(false);
    const scope = { scope: request.scope, teamId: request.teamId, pipelineId: request.pipelineId, currency: request.currency };
    Promise.allSettled([
      getReportSummary(request),
      getForecast({ ...scope, firstMonth: monthOf(new Date(), timeZone), months: 6 }),
    ]).then(([s, f]) => {
      // A slower answer to an older filter must not overwrite a newer one.
      if (ticket !== latest.current) return;
      if (s.status === 'fulfilled') setSummary(s.value);
      // Previously a failure rendered zeros, which is indistinguishable from "you
      // have no data" — and is exactly how a page earns a reputation for being wrong.
      else setError(reason(s.reason, 'These figures could not be loaded.'));
      if (f.status === 'fulfilled') setForecast(f.value);
      else setForecastError(true);
    }).finally(() => {
      if (ticket !== latest.current) return;
      setLoading(false);
      setRefreshing(false);
    });
  }, [request, timeZone]);

  const set = (patch: Partial<FilterState>) => setFilters((f) => ({ ...f, ...patch }));

  const cur = summary?.headlineCurrency ?? null;
  const period = summary?.period ?? null;
  const firstDay = period ? dateInputValue(new Date(period.fromUtc), timeZone) : '';
  const lastDay = period ? lastDayOf(period.toUtc, timeZone) : '';
  const periodText = period
    ? `${new Date(period.fromUtc).toLocaleDateString(locale, { day: 'numeric', month: 'short', timeZone })} – ${new Date(Date.parse(period.toUtc) - 1).toLocaleDateString(locale, { day: 'numeric', month: 'short', year: 'numeric', timeZone })}`
    : '';
  const exportTable = <T,>(t: CsvTable<T>) => downloadCsv(csvFileName(t.name, firstDay, lastDay), t.rows, t.columns);
  // Leads and activities have no pipeline or currency, so those filters cannot narrow
  // them; with either picked, the sections that ignore it say so.
  const dealsOnlyNote = filters.pipelineId || filters.currency
    ? ' The pipeline and currency picked above narrow deals only, so this is not narrowed by them.'
    : '';

  const d = useMemo(() => {
    if (!summary) return null;
    const open = rowFor(summary.open.byCurrency, cur);
    const won = rowFor(summary.won.byCurrency, cur);
    const weighted = summary.weighted.find((w) => w.currency === cur) ?? { currency: cur ?? regional.currency, value: 0, covered: 0, missing: 0 };
    const closing = rowFor(summary.closingInPeriod.byCurrency, cur);
    const readableWon = won.count - won.unreadable;
    return {
      open, won, weighted, closing,
      avgWon: readableWon > 0 ? won.value / readableWon : null,
      others: summary.open.byCurrency.filter((r) => r.currency !== cur),
      unreadableOpen: summary.open.byCurrency.reduce((s, r) => s + r.unreadable, 0),
      missingProbability: summary.weighted.reduce((s, w) => s + w.missing, 0),
    };
  }, [summary, cur, regional.currency]);

  const multiplePipelines = new Set(summary?.byStage.map((r) => r.pipelineName) ?? []).size > 1;
  const byStage = useMemo(() => (summary && cur
    ? topNWithOther(summary.byStage.filter((r) => r.currency === cur).map((r) => ({
      name: multiplePipelines ? `${r.pipelineName} · ${r.stageName}` : r.stageName, value: r.value, count: r.count,
    })), 8)
    : []), [summary, cur, multiplePipelines]);
  const owners = useMemo(() => (summary && cur ? summary.byOwner.filter((r) => r.currency === cur) : []), [summary, cur]);
  const statuses = useMemo(() => (summary ? summariseStatuses(summary.leads.byStatus) : null), [summary]);
  const funnel = useMemo(() => (summary ? statusFunnelFromCounts(summary.leads.byStatus) : []), [summary]);
  const byStatus = useMemo(() => (summary
    ? topNWithOther(summary.leads.byStatus.map((r) => ({ name: r.status, value: r.count, count: r.count })), 8)
    : []), [summary]);
  const bySource = useMemo(() => (summary
    ? topNWithOther(groupSum(summary.leads.bySource, (r) => humanise(r.key), (r) => r.count), 6)
    : []), [summary]);
  const bucket = period?.bucket ?? 'day';
  const leadSeries = useMemo(() => (summary?.leads.createdSeries ?? []).map((p) => ({ name: bucketLabel(p.start, bucket, locale), value: p.count })), [summary, bucket, locale]);
  const activitySeries = useMemo(() => (summary?.activities.series ?? []).map((p) => ({ name: bucketLabel(p.start, bucket, locale), value: p.count })), [summary, bucket, locale]);
  const forecastRows = useMemo(() => (forecast && cur ? forecast.rows.filter((r) => r.currency === cur) : []), [forecast, cur]);

  /**
   * Gaps in the underlying data, stated openly at the top of the page.
   *
   * This is the deliberate answer to "the reports are inaccurate". Most of that
   * feeling came from figures that silently substituted a guess for a missing
   * input. Naming the gap turns an untrustworthy number into a to-do.
   */
  const gaps = useMemo(() => {
    if (!summary || !d) return [];
    const out: string[] = [];
    if (!filters.currency && summary.currencies.length > 1 && cur) {
      out.push(
        `Deals exist in ${summary.currencies.length} currencies. Money figures below are `
        + `${cur} only — ${summary.currencies.filter((c) => c !== cur).join(', ')} ${summary.currencies.length === 2 ? 'is' : 'are'} listed separately, `
        + 'never converted or added in. Pick a currency above to see its figures.',
      );
    }
    if (d.missingProbability > 0) {
      out.push(
        `${plural(d.missingProbability, 'open deal')} ${d.missingProbability === 1 ? 'has' : 'have'} no win probability set, `
        + `so ${d.missingProbability === 1 ? 'it is' : 'they are'} left out of the weighted forecast.`,
      );
    }
    if (d.unreadableOpen > 0) {
      out.push(`${plural(d.unreadableOpen, 'open deal')} ${d.unreadableOpen === 1 ? 'has' : 'have'} a value that is not a number, so ${d.unreadableOpen === 1 ? 'it is' : 'they are'} counted but not added in.`);
    }
    const unscored = summary.leads.created - summary.leads.scored;
    if (unscored > 0 && summary.leads.created > 0) {
      out.push(`${unscored} of ${summary.leads.created} leads created in the period have no score, so the average covers ${summary.leads.scored}.`);
    }
    return out;
  }, [summary, d, cur, filters.currency]);

  // A spinner on an empty page says only "something is happening". A skeleton
  // shaped like the report says what is coming, and because its blocks are the
  // size of the real ones, nothing jumps when the figures land.
  if (loading && request) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <ReportsSkeleton />
      </div>
    );
  }

  const presetPeriod = filters.preset === 'custom' ? null : periodFor(filters.preset, timeZone);

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageEnter>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={BarChart3}
            tone="indigo"
            title="Reports"
            subtitle={periodText
              ? `Pipeline, forecast, leads and activity for ${periodText} — computed on the server from your own records.`
              : 'Pipeline, forecast, leads and activity — computed on the server from your own records.'}
            badges={summary && !error && (
              <>
                <span className="inline-flex items-center gap-1 rounded-full bg-emerald-100 px-2 py-0.5 text-[11px] font-semibold text-emerald-700">
                  <span aria-hidden className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                  {refreshing ? 'Updating…' : 'Live'}
                </span>
                {cur && (
                  <span className="inline-flex items-center rounded-full bg-indigo-100 px-2 py-0.5 text-[11px] font-semibold text-indigo-700">
                    {cur}
                  </span>
                )}
              </>
            )}
            stats={summary && !error ? [
              { label: 'Open deals', value: summary.open.count, icon: Kanban, tone: 'sky', hint: `${summary.won.count} won in the period` },
              { label: 'New leads', value: summary.leads.created, icon: Users, tone: 'violet', hint: `${summary.leads.converted} converted in the period` },
              { label: 'Activities', value: summary.activities.total, icon: ActivityIcon, tone: 'rose', hint: 'logged in the period' },
            ] : undefined}
          />

          {/* Filters */}
          <Reveal as="section" variant="fade" index={1} aria-label="Report filters"
            className="mt-5 flex flex-wrap items-end gap-3 rounded-2xl border border-slate-200 bg-white px-4 py-3">
            <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
              Period
              <select
                value={filters.preset}
                onChange={(e) => {
                  const preset = e.target.value as PeriodPreset;
                  if (preset === 'custom' && presetPeriod) {
                    // Start the custom dates from the period being looked at.
                    set({ preset, customFrom: dateInputValue(presetPeriod.from, timeZone), customTo: dateInputValue(new Date(presetPeriod.to.getTime() - 1), timeZone) });
                  } else set({ preset });
                }}
                className="h-9 rounded-xl border border-slate-200 bg-white px-2 text-sm font-normal normal-case tracking-normal text-slate-800"
              >
                {(Object.keys(PERIOD_LABELS) as PeriodPreset[]).map((p) => <option key={p} value={p}>{PERIOD_LABELS[p]}</option>)}
              </select>
            </label>
            {filters.preset === 'custom' && (
              <>
                <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                  From
                  <input type="date" value={filters.customFrom} onChange={(e) => set({ customFrom: e.target.value })}
                    className="h-9 rounded-xl border border-slate-200 px-2 text-sm font-normal text-slate-800" />
                </label>
                <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                  To
                  <input type="date" value={filters.customTo} onChange={(e) => set({ customTo: e.target.value })}
                    className="h-9 rounded-xl border border-slate-200 px-2 text-sm font-normal text-slate-800" />
                </label>
              </>
            )}
            <div className="flex flex-col gap-1">
              <span className="text-[11px] font-semibold uppercase tracking-wide text-slate-500">Whose</span>
              <div role="group" aria-label="Whose records" className="flex h-9 overflow-hidden rounded-xl border border-slate-200">
                {([['me', 'Mine'], ...(teams.length > 0 ? [['team', 'My team']] : []), ['org', 'Everyone']] as [FilterState['scope'], string][]).map(([value, label]) => (
                  <button key={value} type="button" aria-pressed={filters.scope === value}
                    onClick={() => set({ scope: value })}
                    className={cn('px-3 text-sm', filters.scope === value ? 'bg-slate-900 text-white' : 'bg-white text-slate-600 hover:bg-slate-50')}>
                    {label}
                  </button>
                ))}
              </div>
            </div>
            {filters.scope === 'team' && teams.length > 0 && (
              <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                Team
                <select value={filters.teamId} onChange={(e) => set({ teamId: e.target.value })}
                  className="h-9 rounded-xl border border-slate-200 bg-white px-2 text-sm font-normal normal-case tracking-normal text-slate-800">
                  <option value="">My own team</option>
                  {teams.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              </label>
            )}
            {pipelines.length > 0 && (
              <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                Pipeline
                <select value={filters.pipelineId} onChange={(e) => set({ pipelineId: e.target.value })}
                  className="h-9 rounded-xl border border-slate-200 bg-white px-2 text-sm font-normal normal-case tracking-normal text-slate-800">
                  <option value="">All pipelines</option>
                  {pipelines.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </label>
            )}
            {summary && summary.currencies.length > 0 && (
              <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
                Currency
                <select value={filters.currency} onChange={(e) => set({ currency: e.target.value })}
                  className="h-9 rounded-xl border border-slate-200 bg-white px-2 text-sm font-normal normal-case tracking-normal text-slate-800">
                  <option value="">{summary.currencies.length > 1 ? 'Main currency' : summary.currencies[0]}</option>
                  {summary.currencies.length > 1 && summary.currencies.map((c) => <option key={c} value={c}>{c} only</option>)}
                  {filters.currency && !summary.currencies.includes(filters.currency) && <option value={filters.currency}>{filters.currency} only</option>}
                </select>
              </label>
            )}
            {summary && (
              <div className="ml-auto self-center">
                <CsvButton what="the overview figures" onClick={() => exportTable(overviewTable(summary))} />
              </div>
            )}
            {!request && <p className="w-full text-[12px] text-amber-700">Pick a start and an end date, the end on or after the start.</p>}
          </Reveal>

          {error && (
            <div className="mt-5 flex items-start gap-2 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                <strong>These figures could not be loaded.</strong> {error}
                {summary ? ' What you see below is for the previous filters.' : ' Reload the page rather than reading an empty report as real.'}
              </span>
            </div>
          )}

          {/* Jump bar. The page is long by nature; this keeps it navigable
              instead of an undifferentiated scroll. */}
          <Reveal
            as="nav"
            variant="fade"
            index={1}
            aria-label="Report sections"
            className="sticky top-[52px] z-20 -mx-1 mt-4 flex gap-1.5 overflow-x-auto rounded-2xl border border-slate-200 bg-white/85 px-2 py-2 backdrop-blur-sm"
          >
            {SECTIONS.map((s) => (
              <a
                key={s.id}
                href={`#${s.id}`}
                className="shrink-0 rounded-xl px-3 py-1.5 text-xs font-semibold text-slate-600 transition-colors hover:bg-slate-100 hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400"
              >
                {s.label}
              </a>
            ))}
          </Reveal>

          {gaps.length > 0 && (
            <div className="mt-4 rounded-2xl border border-amber-200 bg-amber-50/70 p-4">
              <div className="flex items-center gap-2">
                <Info className="h-4 w-4 shrink-0 text-amber-600" />
                <h2 className="text-sm font-bold text-amber-900">Worth knowing about this data</h2>
              </div>
              <ul className="mt-2 space-y-1.5">
                {gaps.map((g) => (
                  <li key={g} className="flex gap-2 text-[12.5px] leading-relaxed text-amber-900/90">
                    <span aria-hidden className="mt-[7px] h-1 w-1 shrink-0 rounded-full bg-amber-500" />
                    <span>{g}</span>
                  </li>
                ))}
              </ul>
              <p className="mt-2.5 text-[11px] text-amber-700">
                Nothing here is estimated or filled in on your behalf. Where an input is missing the
                figure says so rather than guessing.
              </p>
            </div>
          )}

          {summary && d && statuses && (
            <>
              {/* ── Overview ─────────────────────────────────────────────── */}
              <Reveal as="section" index={2} className="mt-8">
                <SectionHead
                  id="overview"
                  eyebrow="Overview"
                  title="Where the business stands"
                  blurb={`Open pipeline is as of now; won, lost and win rate are for ${periodText}. Money is in ${cur ?? 'no currency yet'}.`}
                  accent="indigo"
                  icon={Gauge}
                />
                <div className="grid grid-cols-2 gap-3 lg:grid-cols-4 xl:grid-cols-8">
                  <Kpi index={1} label="Open pipeline" value={cur ? money(d.open.value, cur) : '—'}
                    sub={plural(d.open.count, 'open deal')} icon={DollarSign} accent="indigo" warn={d.open.unreadable > 0} />
                  <Kpi index={2} label="Weighted forecast"
                    value={cur && d.weighted.covered > 0 ? money(d.weighted.value, cur) : '—'}
                    sub={d.weighted.covered > 0 ? `from ${plural(d.weighted.covered, 'deal')} with a probability` : 'no probabilities set'}
                    icon={TrendingUp} accent="emerald" warn={d.weighted.missing > 0} />
                  {/* The count is of the deals the value adds up: beside a CHF total, a
                      count of every currency's wins read as CHF from more deals. */}
                  <Kpi index={3} label="Won" value={cur ? money(d.won.value, cur) : '—'}
                    sub={summary.won.count > d.won.count
                      ? `${plural(d.won.count, 'deal')} in ${cur}, ${summary.won.count - d.won.count} more in other currencies`
                      : `${plural(d.won.count, 'deal')} in the period`}
                    icon={Target} accent="teal" />
                  <Kpi index={4} label="Win rate" value={pct(summary.winRate)}
                    sub={summary.winRate === null ? 'nothing closed in the period' : `${summary.won.count} won · ${summary.lost.count} lost`}
                    icon={Percent} accent="teal" />
                  <Kpi index={5} label="Avg won deal"
                    value={d.avgWon === null || !cur ? '—' : money(d.avgWon, cur)}
                    sub={d.avgWon === null ? 'none won in the period' : `across ${plural(d.won.count - d.won.unreadable, 'won deal')} in ${cur}`}
                    icon={Target} accent="amber" />
                  <Kpi index={6} label="Closing in period" value={cur ? money(d.closing.value, cur) : '—'}
                    sub={`${plural(d.closing.count, 'open deal')} expected`} icon={CalendarClock} accent="sky" />
                  <Kpi index={7} label="Sales cycle"
                    value={summary.averageSalesCycleDays === null ? '—' : `${Math.round(summary.averageSalesCycleDays)} days`}
                    sub={summary.averageSalesCycleDays === null ? 'none won in the period' : 'average, creation to win'}
                    icon={Timer} accent="violet" />
                  <Kpi index={8} label="Lead conversion"
                    value={pct(summary.leads.created > 0 ? summary.leads.createdConverted / summary.leads.created : null)}
                    sub={`${summary.leads.createdConverted} of ${summary.leads.created} new leads`} icon={UserCheck} accent="violet" />
                </div>

                {canSaveForecast && <SavedForecastPanel />}

                {d.others.length > 0 && (
                  <div className="mt-3 rounded-2xl border border-slate-200 bg-white p-4">
                    <h3 className="text-sm font-bold text-slate-800">Open pipeline in other currencies</h3>
                    <p className="mt-0.5 text-[11px] text-slate-500">
                      Listed rather than converted, because there is no exchange rate in the system to convert them with.
                    </p>
                    <div className="mt-3">
                      <BarList
                        rows={d.others.map((c) => ({ name: `${c.currency} · ${plural(c.count, 'deal')}`, value: c.value }))}
                        accent="sky"
                        format={(v) => Math.round(v).toLocaleString()}
                      />
                    </div>
                  </div>
                )}
              </Reveal>

              {/* ── Deals ────────────────────────────────────────────────── */}
              <Reveal as="section" index={3} className="mt-10">
                <SectionHead
                  id="pipeline"
                  eyebrow="Deals"
                  title="Open pipeline, by stage and owner"
                  blurb={`Open deals in ${cur ?? '—'}, in pipeline order. Won and lost are for the period.`}
                  accent="sky"
                  icon={Kanban}
                />
                <div className="grid gap-3 lg:grid-cols-2">
                  <Panel index={1} title="Value by stage" subtitle={plural(d.open.count, 'open deal')}
                    hint="Sum of deal value per stage, open deals only, in the currency shown. The CSV has every currency."
                    actions={<CsvButton what="pipeline by stage" disabled={summary.byStage.length === 0} onClick={() => exportTable(stageTable(summary.byStage))} />}>
                    <BarList rows={byStage} accent="sky" format={(v) => (cur ? money(v, cur) : String(v))} />
                  </Panel>
                  <Panel index={2} title="By owner" subtitle="Who is carrying the pipeline, and what they closed"
                    hint="Deals with no assignee are grouped as Unassigned rather than dropped. Won and lost are for the period."
                    actions={<CsvButton what="the owners table" disabled={summary.byOwner.length === 0} onClick={() => exportTable(ownerTable(summary.byOwner))} />}>
                    {owners.length === 0 ? <NoData>No deals in {cur ?? 'this currency'}.</NoData> : (
                      <div className="overflow-x-auto">
                        <table className="w-full text-left text-sm">
                          <thead>
                            <tr className="text-[11px] uppercase tracking-wide text-slate-500">
                              <th className="py-1.5 pr-3 font-semibold">Owner</th>
                              <th className="py-1.5 pr-3 text-right font-semibold">Open</th>
                              <th className="py-1.5 pr-3 text-right font-semibold">Weighted</th>
                              <th className="py-1.5 pr-3 text-right font-semibold">Won</th>
                              <th className="py-1.5 text-right font-semibold">Lost</th>
                            </tr>
                          </thead>
                          <tbody>
                            {owners.map((o) => (
                              <tr key={`${o.ownerId ?? 'none'}|${o.currency}`} className="border-t border-slate-100">
                                <td className="py-1.5 pr-3 text-slate-700">{o.ownerName}</td>
                                <td className="py-1.5 pr-3 text-right text-slate-900">{money(o.openValue, o.currency)}<span className="text-slate-400"> · {o.openCount}</span></td>
                                <td className="py-1.5 pr-3 text-right text-slate-600">{money(o.weighted, o.currency)}</td>
                                <td className="py-1.5 pr-3 text-right text-emerald-700">{money(o.wonValue, o.currency)}<span className="text-slate-400"> · {o.wonCount}</span></td>
                                <td className="py-1.5 text-right text-slate-500">{o.lostCount}</td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    )}
                  </Panel>
                </div>
              </Reveal>

              {/* ── Forecast ─────────────────────────────────────────────── */}
              <Reveal as="section" index={4} className="mt-10">
                <SectionHead
                  id="forecast"
                  eyebrow="Forecast"
                  title="What is expected to close, month by month"
                  blurb="Open deals by the month of their expected close date, weighted by probability, beside what was won that month."
                  accent="emerald"
                  icon={TrendingUp}
                />
                <Panel index={1} title="Forecast by close month" subtitle={`Next six months, in ${cur ?? '—'}`}
                  hint="A deal counts in the month its expected close date falls in, also once that date has passed. Below the table are the open deals past their close date (this month's are in its row as well, earlier months' in none) and the deals with no close date, which are in no month."
                  actions={forecast && <CsvButton what="the forecast" disabled={forecast.rows.length === 0} onClick={() => exportTable(forecastTable(forecast.rows))} />}>
                  {forecastError && <p className="text-sm text-rose-700">The forecast could not be loaded.</p>}
                  {forecast && !forecastError && (forecastRows.length === 0 ? (
                    <NoData>No open deal in {cur ?? 'this currency'} is expected to close in the next six months.</NoData>
                  ) : (
                    <div className="overflow-x-auto">
                      <table className="w-full text-left text-sm">
                        <thead>
                          <tr className="text-[11px] uppercase tracking-wide text-slate-500">
                            <th className="py-1.5 pr-3 font-semibold">Month</th>
                            <th className="py-1.5 pr-3 text-right font-semibold">Expected</th>
                            <th className="py-1.5 pr-3 text-right font-semibold">Weighted</th>
                            <th className="py-1.5 pr-3 text-right font-semibold">No probability</th>
                            <th className="py-1.5 text-right font-semibold">Won</th>
                          </tr>
                        </thead>
                        <tbody>
                          {forecastRows.map((r) => (
                            <tr key={r.month} className="border-t border-slate-100">
                              <td className="py-1.5 pr-3 text-slate-700">{monthLabel(r.month, locale)}</td>
                              <td className="py-1.5 pr-3 text-right text-slate-900">{money(r.value, r.currency)}<span className="text-slate-400"> · {r.count}</span></td>
                              <td className="py-1.5 pr-3 text-right font-medium text-slate-900">{money(r.weighted, r.currency)}</td>
                              <td className="py-1.5 pr-3 text-right text-slate-500">{r.missing}</td>
                              <td className="py-1.5 text-right text-emerald-700">{r.wonCount > 0 ? `${money(r.wonValue, r.currency)} · ${r.wonCount}` : '—'}</td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ))}
                  {forecast && cur && (() => {
                    const overdue = rowFor(forecast.overdue, cur);
                    const undated = rowFor(forecast.noCloseDate, cur);
                    if (overdue.count === 0 && undated.count === 0) return null;
                    return (
                      <p className="mt-3 text-[12px] text-slate-600">
                        {overdue.count > 0 && <>{plural(overdue.count, 'open deal')} past {overdue.count === 1 ? 'its' : 'their'} close date ({money(overdue.value, cur)}). </>}
                        {undated.count > 0 && <>{plural(undated.count, 'open deal')} with no close date ({money(undated.value, cur)}).</>}
                      </p>
                    );
                  })()}
                </Panel>
              </Reveal>

              {/* ── Leads ────────────────────────────────────────────────── */}
              <Reveal as="section" index={5} className="mt-10">
                <SectionHead
                  id="leads"
                  eyebrow="Leads"
                  title="The leads that came in, and where they are now"
                  blurb={`Leads created in the period, by their status today.${dealsOnlyNote} For how far leads actually got, see Lifecycle below.`}
                  accent="violet"
                  icon={Filter}
                />
                <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
                  <Kpi index={7} label="Active leads" value={String(summary.leads.active)}
                    sub="not converted, as of now" icon={Users} accent="sky" />
                  <Kpi index={8} label="New in period" value={String(summary.leads.created)}
                    sub={`${statuses.qualifiedOrBeyond} qualified or beyond`} icon={Sparkles} accent="emerald" />
                  <Kpi index={9} label="Converted in period" value={String(summary.leads.converted)}
                    sub={`${summary.leads.convertedToDeal} became deals`} icon={UserCheck} accent="violet" />
                  <Kpi index={10} label="Avg lead score"
                    value={summary.leads.averageScore === null ? '—' : String(Math.round(summary.leads.averageScore))}
                    sub={summary.leads.averageScore === null ? 'no new leads scored' : `across ${summary.leads.scored} scored`}
                    icon={Gauge} accent="amber" warn={summary.leads.created > summary.leads.scored} />
                </div>

                <div className="mt-3 grid gap-3 lg:grid-cols-3">
                  <Panel index={3} title="Status funnel" subtitle="Current status, narrowing by definition"
                    hint="Each step counts leads at that stage or beyond, so a later step can never exceed an earlier one. Status has no memory: a lead contacted and later marked lost counts only in the first step.">
                    <Funnel steps={funnel} accent="teal" />
                  </Panel>
                  <Panel index={4} title="By status" subtitle="Every new lead, one bucket each"
                    hint="Bars are scaled to the largest bucket; the printed number is the true count. Anything past the top eight is grouped as Other."
                    actions={<CsvButton what="leads by status" disabled={summary.leads.byStatus.length === 0} onClick={() => exportTable(leadStatusTable(summary.leads.byStatus))} />}>
                    <BarList rows={byStatus} accent="violet" />
                  </Panel>
                  <Panel index={5} title="By source" subtitle="Where they came from"
                    hint="Leads with no source recorded appear as Unknown, so the breakdown still totals correctly."
                    actions={<CsvButton what="leads by source" disabled={summary.leads.bySource.length === 0} onClick={() => exportTable(countTable('leads-by-source', 'Source', summary.leads.bySource))} />}>
                    <BarList rows={bySource} accent="amber" />
                  </Panel>
                </div>

                <div className="mt-3">
                  <Panel index={6} title="New leads" subtitle={`Per ${bucket}, ${periodText}`}
                    hint="Counted by each lead's creation date, on your own calendar."
                    actions={<CsvButton what="new leads over time" onClick={() => exportTable(seriesTable('new-leads', 'New leads', summary.leads.createdSeries))} />}>
                    {leadSeries.every((w) => w.value === 0) ? (
                      <NoData>No leads created in the period.</NoData>
                    ) : (
                      <div className="h-[200px]">
                        <ResponsiveContainer width="100%" height="100%">
                          <BarChart data={leadSeries} margin={{ top: 4, right: 8, bottom: 0, left: -18 }}>
                            <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" vertical={false} />
                            <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#94a3b8' }} tickLine={false} axisLine={false} />
                            <YAxis allowDecimals={false} tick={{ fontSize: 10, fill: '#94a3b8' }} tickLine={false} axisLine={false} />
                            <Tooltip contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                            <Bar dataKey="value" name="New leads" fill="#8b5cf6" radius={[5, 5, 0, 0]} maxBarSize={38} />
                          </BarChart>
                        </ResponsiveContainer>
                      </div>
                    )}
                  </Panel>
                </div>
              </Reveal>
            </>
          )}

          {/* ── Lifecycle (sales tracker) ────────────────────────────── */}
          <Reveal as="section" index={6} className="mt-10">
            <SectionHead
              id="lifecycle"
              eyebrow="Lifecycle"
              title="How far leads actually got"
              blurb="Built from the five-phase pipeline on each lead, which keeps its history — so unlike the status funnel above, a lead that dropped out still counts at every stage it reached. It covers every lead, whatever the filters."
              accent="emerald"
              icon={TrendingUp}
            />
            <LifecycleOnRequest />
          </Reveal>

          {/* ── Activity ─────────────────────────────────────────────── */}
          {summary && (
            <Reveal as="section" index={7} className="mt-10">
              <SectionHead
                id="activity"
                eyebrow="Activity"
                title="What the team has been doing"
                blurb={`Calls, meetings, emails and notes logged against any record in the period.${dealsOnlyNote}`}
                accent="rose"
                icon={ActivityIcon}
              />
              <div className="grid gap-3 lg:grid-cols-3">
                <Panel index={7} className="lg:col-span-2" title="Activity" subtitle={`${plural(summary.activities.total, 'activity', 'activities')} logged, per ${bucket}`}
                  hint="Counted by the day the activity was logged on your own calendar, so evening work appears on the day it happened. The app's own system updates are not counted."
                  actions={<CsvButton what="activity over time" onClick={() => exportTable(seriesTable('activity', 'Activities', summary.activities.series))} />}>
                  {activitySeries.every((x) => x.value === 0) ? (
                    <NoData>No activity logged in the period.</NoData>
                  ) : (
                    <div className="h-[220px]">
                      <ResponsiveContainer width="100%" height="100%">
                        <AreaChart data={activitySeries} margin={{ top: 4, right: 8, bottom: 0, left: -18 }}>
                          <defs>
                            <linearGradient id="actFill" x1="0" y1="0" x2="0" y2="1">
                              <stop offset="0%" stopColor="#f43f5e" stopOpacity={0.28} />
                              <stop offset="100%" stopColor="#f43f5e" stopOpacity={0.02} />
                            </linearGradient>
                          </defs>
                          <CartesianGrid strokeDasharray="3 3" stroke="#eef2f7" vertical={false} />
                          <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#94a3b8' }} tickLine={false} axisLine={false} />
                          <YAxis allowDecimals={false} tick={{ fontSize: 10, fill: '#94a3b8' }} tickLine={false} axisLine={false} />
                          <Tooltip contentStyle={{ borderRadius: 12, border: '1px solid #e2e8f0', fontSize: 12 }} />
                          <Area type="monotone" dataKey="value" name="Activities" stroke="#f43f5e" strokeWidth={2} fill="url(#actFill)" />
                        </AreaChart>
                      </ResponsiveContainer>
                    </div>
                  )}
                </Panel>
                <Panel index={8} title="By type" subtitle="What kind of work"
                  actions={<CsvButton what="activity by type" disabled={summary.activities.byType.length === 0} onClick={() => exportTable(countTable('activity-by-type', 'Type', summary.activities.byType))} />}>
                  <BarList rows={summary.activities.byType.map((t) => ({ name: humanise(t.key), value: t.count }))} accent="rose" />
                </Panel>
              </div>
            </Reveal>
          )}

          <ConversionReportPanel />

          <p className={cn(
            'mt-8 rounded-2xl border border-slate-200 bg-white/70 px-4 py-3',
            'text-[11.5px] leading-relaxed text-slate-500',
          )}>
            <strong className="text-slate-700">How to read these numbers.</strong> Every figure is
            computed on the server from your leads, deals and activities when the page loads, for the
            period and people chosen above — there is no overnight job and no cached copy to go stale.
            The pipeline and currency narrow the deals, the only records that have them; leads and
            activity are not narrowed by them. The lifecycle report and the lead conversion table are the exceptions: they
            cover every lead, whatever the filters. Private deals you may not see are left out. Where an input is missing, the
            figure shows an em dash and says what is missing rather than substituting a default. Money is
            never converted between currencies.
          </p>
        </main>
      </PageEnter>
    </div>
  );
}

/**
 * The lifecycle report is still worked out in the browser, from every lead's
 * pipeline record, so it loads when asked for rather than with the page: the rest
 * of the page no longer downloads whole tables.
 */
function LifecycleOnRequest() {
  const [leads, setLeads] = useState<Lead[] | null>(null);
  const [state, setState] = useState<'idle' | 'loading' | 'failed'>('idle');
  const load = () => {
    setState('loading');
    getLeads()
      .then((l) => { setLeads(Array.isArray(l) ? l : []); setState('idle'); })
      .catch(() => setState('failed'));
  };
  if (leads) return <SalesTrackerReport leads={leads} />;
  return (
    <div className="rounded-2xl border border-dashed border-slate-200 bg-white/70 px-4 py-6 text-center">
      <p className="text-[12.5px] text-slate-600">
        This report reads every lead&apos;s pipeline in your browser, so it is loaded on request.
      </p>
      <button type="button" onClick={load} disabled={state === 'loading'}
        className="mt-3 h-9 rounded-xl bg-slate-900 px-3 text-sm font-medium text-white disabled:opacity-60">
        {state === 'loading' ? 'Loading…' : 'Show the lifecycle report'}
      </button>
      {state === 'failed' && <p className="mt-2 text-[12px] text-rose-700">The leads could not be loaded. Try again.</p>}
    </div>
  );
}

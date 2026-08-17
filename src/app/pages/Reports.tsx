import { useEffect, useMemo, useState } from 'react';
import {
  BarChart3, TrendingUp, Target, DollarSign, Percent, Loader2, Users, UserCheck,
  Gauge, Sparkles, Activity as ActivityIcon, Kanban, Filter, AlertTriangle, Info,
} from 'lucide-react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip,
  AreaChart, Area,
} from 'recharts';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
import { cn } from '@/app/components/ui/utils';
import { getDeals, getLeads, getActivities } from '@/app/api';
import type { Deal, Lead, Activity } from '@/app/api/types';
import { SalesTrackerReport } from './reports/SalesTrackerReport';
import { SectionHead, Kpi, Panel, NoData, BarList, Funnel } from './reports/ReportUI';
import {
  summariseDeals, summariseLeads, statusFunnel, groupSum, topNWithOther,
  activityByDay, newLeadsByWeek, dealState, parseMoney,
} from './reports/reportMetrics';

/**
 * Money in a specific currency. The old page hardcoded USD, so an organisation
 * billing in CHF saw dollar signs on every figure.
 */
function money(n: number, currency: string): string {
  try {
    return new Intl.NumberFormat(undefined, {
      style: 'currency', currency, maximumFractionDigits: 0,
    }).format(n);
  } catch {
    // An unrecognised currency code must not blank the whole page.
    return `${currency} ${Math.round(n).toLocaleString()}`;
  }
}
const pct = (v: number | null) => (v === null ? '—' : `${Math.round(v * 100)}%`);

/** `cold_call` → `Cold call`. Sources are stored as raw enum-ish strings. */
function humanise(s: string): string {
  const t = s.replace(/[_-]+/g, ' ').trim();
  return t.length === 0 ? 'Unknown' : t[0]!.toUpperCase() + t.slice(1);
}

const SECTIONS = [
  { id: 'overview', label: 'Overview' },
  { id: 'pipeline', label: 'Deals' },
  { id: 'leads', label: 'Leads' },
  { id: 'lifecycle', label: 'Lifecycle' },
  { id: 'activity', label: 'Activity' },
] as const;

export default function Reports() {
  const [deals, setDeals] = useState<Deal[]>([]);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [loading, setLoading] = useState(true);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    Promise.all([getDeals(), getLeads(), getActivities()])
      .then(([d, l, a]) => {
        setDeals(Array.isArray(d) ? d : []);
        setLeads(Array.isArray(l) ? l : []);
        setActivities(Array.isArray(a) ? a : []);
      })
      // Previously this swallowed the error and rendered zeros, which is
      // indistinguishable from "you have no data" — and is exactly how a page
      // earns a reputation for being wrong.
      .catch(() => setFailed(true))
      .finally(() => setLoading(false));
  }, []);

  const d = useMemo(() => summariseDeals(deals), [deals]);
  const l = useMemo(() => summariseLeads(leads), [leads]);
  const funnel = useMemo(() => statusFunnel(leads), [leads]);
  const byStage = useMemo(
    () => topNWithOther(groupSum(
      deals.filter((x) => dealState(x) === 'open' && (x.currency || 'USD').toUpperCase() === d.currency),
      (x) => x.dealStageName || x.stage || 'Unstaged', (x) => parseMoney(x.value),
    ), 7),
    [deals, d.currency],
  );
  const byOwner = useMemo(
    () => topNWithOther(groupSum(
      deals.filter((x) => dealState(x) === 'open' && (x.currency || 'USD').toUpperCase() === d.currency),
      (x) => x.assigneeName || 'Unassigned', (x) => parseMoney(x.value),
    ), 6),
    [deals, d.currency],
  );
  const byStatus = useMemo(
    () => topNWithOther(groupSum(leads, (x) => x.status || 'Unknown', () => 1), 8),
    [leads],
  );
  const bySource = useMemo(
    () => topNWithOther(groupSum(leads, (x) => humanise(x.source || 'Unknown'), () => 1), 6),
    [leads],
  );
  const days = useMemo(() => activityByDay(activities, 14), [activities]);
  const weeks = useMemo(() => newLeadsByWeek(leads, 8), [leads]);

  /**
   * Gaps in the underlying data, stated openly at the top of the page.
   *
   * This is the deliberate answer to "the reports are inaccurate". Most of that
   * feeling came from figures that silently substituted a guess for a missing
   * input. Naming the gap turns an untrustworthy number into a to-do.
   */
  const gaps = useMemo(() => {
    const out: string[] = [];
    if (d.mixedCurrency) {
      out.push(
        `Deals exist in ${d.otherCurrencies.length + 1} currencies. Money figures below are `
        + `${d.currency} only — ${d.otherCurrencies.map((c) => c.currency).join(', ')} are listed separately, `
        + 'never converted or added in.',
      );
    }
    if (d.forecast.missing > 0) {
      out.push(
        `${d.forecast.missing} open deal${d.forecast.missing === 1 ? ' has' : 's have'} no win probability set, `
        + `so ${d.forecast.missing === 1 ? 'it is' : 'they are'} excluded from the weighted forecast.`,
      );
    }
    if (l.unscoredCount > 0 && l.total > 0) {
      out.push(`${l.unscoredCount} of ${l.total} leads have no score, so the average covers ${l.scoredCount}.`);
    }
    return out;
  }, [d, l]);

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <div className="flex flex-1 items-center justify-center">
          <Loader2 className="h-6 w-6 animate-spin text-indigo-500" />
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={BarChart3}
            iconGradient="from-indigo-500 to-violet-500"
            title="Reports"
            subtitle="Pipeline, leads, lifecycle and activity — every figure computed from your own records."
          />

          {failed && (
            <div className="mt-5 flex items-start gap-2 rounded-2xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
              <span>
                <strong>These figures could not be loaded.</strong> What you see below is not your
                data — reload the page rather than reading zeros as real.
              </span>
            </div>
          )}

          {/* Jump bar. The page is long by nature; this keeps it navigable
              instead of an undifferentiated scroll. */}
          <nav
            aria-label="Report sections"
            className="sticky top-[52px] z-20 -mx-1 mt-5 flex gap-1.5 overflow-x-auto rounded-2xl border border-slate-200 bg-white/85 px-2 py-2 backdrop-blur-sm"
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
          </nav>

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

          {/* ── Overview ─────────────────────────────────────────────── */}
          <div className="mt-8">
            <SectionHead
              id="overview"
              eyebrow="Overview"
              title="Where the business stands"
              blurb="The six figures worth checking daily. Money is shown in your dominant currency."
              accent="indigo"
              icon={Gauge}
            />
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-3 xl:grid-cols-6">
              <Kpi label="Open pipeline" value={money(d.openValue, d.currency)}
                sub={`${d.openCount} open deal${d.openCount === 1 ? '' : 's'}`} icon={DollarSign} accent="indigo" />
              <Kpi label="Weighted forecast"
                value={d.forecast.covered > 0 ? money(d.forecast.value, d.currency) : '—'}
                sub={d.forecast.covered > 0
                  ? `from ${d.forecast.covered} deal${d.forecast.covered === 1 ? '' : 's'} with a probability`
                  : 'no probabilities set'}
                icon={TrendingUp} accent="emerald" warn={d.forecast.missing > 0} />
              <Kpi label="Win rate" value={pct(d.winRate)}
                sub={d.winRate === null ? 'nothing closed yet' : `${d.wonCount} won · ${d.lostCount} lost`}
                icon={Percent} accent="teal" />
              <Kpi label="Avg won deal"
                value={d.avgWonValue === null ? '—' : money(d.avgWonValue, d.currency)}
                sub={d.avgWonValue === null ? 'no deals won yet' : `across ${d.wonCount} won`}
                icon={Target} accent="amber" />
              <Kpi label="Total leads" value={String(l.total)}
                sub={`${l.qualifiedOrBeyond} qualified or beyond`} icon={Users} accent="sky" />
              <Kpi label="Lead conversion" value={pct(l.conversionRate)}
                sub={`${l.converted} became deals`} icon={UserCheck} accent="violet" />
            </div>

            {d.mixedCurrency && (
              <div className="mt-3 rounded-2xl border border-slate-200 bg-white p-4">
                <h3 className="text-sm font-bold text-slate-800">Other currencies</h3>
                <p className="mt-0.5 text-[11px] text-slate-400">
                  Totals across all deal states. Listed rather than converted, because there is no
                  exchange rate in the system to convert them with.
                </p>
                <div className="mt-3">
                  <BarList
                    rows={d.otherCurrencies.map((c) => ({ name: `${c.currency} · ${c.count} deals`, value: c.value }))}
                    accent="sky"
                    format={(v) => Math.round(v).toLocaleString()}
                  />
                </div>
              </div>
            )}
          </div>

          {/* ── Deals ────────────────────────────────────────────────── */}
          <div className="mt-10">
            <SectionHead
              id="pipeline"
              eyebrow="Deals"
              title="Open pipeline, by stage and owner"
              blurb={`Open deals in ${d.currency}. Won and lost deals are excluded — this is what is still live.`}
              accent="sky"
              icon={Kanban}
            />
            <div className="grid gap-3 lg:grid-cols-2">
              <Panel title="Value by stage" subtitle={`${d.openCount} open deals`}
                hint="Sum of deal value per stage, open deals only, in the dominant currency.">
                <BarList rows={byStage} accent="sky" format={(v) => money(v, d.currency)} />
              </Panel>
              <Panel title="Value by owner" subtitle="Who is carrying the pipeline"
                hint="Deals with no assignee are grouped as Unassigned rather than dropped.">
                <BarList rows={byOwner} accent="indigo" format={(v) => money(v, d.currency)} />
              </Panel>
            </div>
          </div>

          {/* ── Leads ────────────────────────────────────────────────── */}
          <div className="mt-10">
            <SectionHead
              id="leads"
              eyebrow="Leads"
              title="Where your leads are"
              blurb="Status is a snapshot of right now. For how far leads actually got, see Lifecycle below."
              accent="violet"
              icon={Filter}
            />
            <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
              <Kpi label="Contacted or beyond" value={String(l.contactedOrBeyond)}
                sub={`of ${l.total} leads`} icon={UserCheck} accent="teal" />
              <Kpi label="Qualified or beyond" value={String(l.qualifiedOrBeyond)}
                sub="met and interested onwards" icon={Target} accent="violet" />
              <Kpi label="Avg lead score"
                value={l.avgScore === null ? '—' : String(Math.round(l.avgScore))}
                sub={l.avgScore === null ? 'no leads scored' : `across ${l.scoredCount} scored`}
                icon={Gauge} accent="amber" warn={l.unscoredCount > 0} />
              <Kpi label="New (8 weeks)" value={String(weeks.reduce((s, w) => s + w.value, 0))}
                sub="created recently" icon={Sparkles} accent="emerald" />
            </div>

            <div className="mt-3 grid gap-3 lg:grid-cols-3">
              <Panel title="Status funnel" subtitle="Current status, narrowing by definition"
                hint="Each step counts leads at that stage or beyond, so a later step can never exceed an earlier one. Status has no memory: a lead contacted and later marked lost counts only in the first step.">
                <Funnel steps={funnel} accent="teal" />
              </Panel>
              <Panel title="By status" subtitle="Every lead, one bucket each"
                hint="Bars are scaled to the largest bucket so they can be compared; the printed number is the true count. Counts sum to your total lead count, with anything past the top eight grouped as Other.">
                <BarList rows={byStatus} accent="violet" />
              </Panel>
              <Panel title="By source" subtitle="Where they came from"
                hint="Bars are scaled to the largest source. Leads with no source recorded appear as Unknown, so the breakdown still totals correctly.">
                <BarList rows={bySource} accent="amber" />
              </Panel>
            </div>

            <div className="mt-3">
              <Panel title="New leads per week" subtitle="Last 8 weeks, most recent on the right"
                hint="Rolling 7-day windows counted back from today, using each lead's creation date.">
                {weeks.every((w) => w.value === 0) ? (
                  <NoData>No leads created in the last 8 weeks.</NoData>
                ) : (
                  <div className="h-[200px]">
                    <ResponsiveContainer width="100%" height="100%">
                      <BarChart data={weeks} margin={{ top: 4, right: 8, bottom: 0, left: -18 }}>
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
          </div>

          {/* ── Lifecycle (sales tracker) ────────────────────────────── */}
          <div className="mt-10">
            <SectionHead
              id="lifecycle"
              eyebrow="Lifecycle"
              title="How far leads actually got"
              blurb="Built from the five-phase pipeline on each lead, which keeps its history — so unlike the status funnel above, a lead that dropped out still counts at every stage it reached."
              accent="emerald"
              icon={TrendingUp}
            />
            <SalesTrackerReport leads={leads} />
          </div>

          {/* ── Activity ─────────────────────────────────────────────── */}
          <div className="mt-10">
            <SectionHead
              id="activity"
              eyebrow="Activity"
              title="What the team has been doing"
              blurb="Calls, meetings, emails and notes logged against any record."
              accent="rose"
              icon={ActivityIcon}
            />
            <Panel title="Activity, last 14 days" subtitle={`${activities.length} logged in total`}
              hint="Counted by the local calendar day the activity was logged, so evening work appears on the day it happened.">
              {days.every((x) => x.value === 0) ? (
                <NoData>No activity logged in the last 14 days.</NoData>
              ) : (
                <div className="h-[220px]">
                  <ResponsiveContainer width="100%" height="100%">
                    <AreaChart data={days} margin={{ top: 4, right: 8, bottom: 0, left: -18 }}>
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
          </div>

          <p className={cn(
            'mt-8 rounded-2xl border border-slate-200 bg-white/70 px-4 py-3',
            'text-[11.5px] leading-relaxed text-slate-500',
          )}>
            <strong className="text-slate-700">How to read these numbers.</strong> Every figure is
            computed live from your leads, deals and activities when the page loads — there is no
            overnight job and no cached copy to go stale. Where an input is missing, the figure shows
            an em dash and says what is missing rather than substituting a default. Money is never
            converted between currencies.
          </p>
        </main>
      </PageTransition>
    </div>
  );
}

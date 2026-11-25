// The site analytics, for a platform admin: the header with the period and the Live pill,
// the headline figures, growth, live visitors, sources, pages, conversions, journeys and
// audience, and the privacy note. One read of the period's summary feeds every section but
// Live (read every 15 seconds) and Journeys (read per day).

import { useMemo, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  BarChart3, Check, Compass, Copy, ExternalLink, FileText, MousePointerClick, Route, ShieldCheck, TrendingUp, Users,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { LoadFailed } from '@/app/components/LoadFailed';
import { ApiError } from '@/app/api/apiClient';
import { useThemeOptional } from '@/app/contexts/ThemeContext';
import { useFormat } from '@/app/i18n';
import { formatDay } from '@/app/lib/zonedTime';
import { SectionHead } from '@/app/pages/reports/ReportUI';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import { useSiteAnalyticsLive, useSiteAnalyticsSummary } from '@/app/hooks/queries/siteAnalytics';
import type { SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { AudienceSection } from './AudienceSection';
import { Bone } from './AnalyticsUI';
import { ConversionsSection } from './ConversionsSection';
import { GrowthChart } from './GrowthChart';
import { JourneysSection } from './JourneysSection';
import { KpiGrid, KpiGridSkeleton } from './KpiGrid';
import { LiveNow, LivePill } from './LiveNow';
import { NotAvailable } from './NotAvailable';
import { PagesSection } from './PagesSection';
import { RangePicker } from './RangePicker';
import { SourcesSection } from './SourcesSection';
import { previousPeriodLabel, rangeFor, rangeLength, readSelection, todayInZurich, writeSelection, type RangeSelection } from './range';

export const SITE_URL = 'https://www.cadencee.ai';

const isEmpty = (s: SiteAnalyticsSummary) => s.totals.visitors === 0 && s.totals.sessions === 0 && s.totals.pageViews === 0;

/** Nobody came yet: said once at the top, with the link to share. */
function NoVisitsYet() {
  const [copied, setCopied] = useState(false);
  const copy = async () => {
    try {
      await navigator.clipboard.writeText(SITE_URL);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 2000);
    } catch {
      setCopied(false);
    }
  };
  return (
    <div role="status" className="flex flex-wrap items-center gap-3 rounded-2xl border border-orange-200 bg-gradient-to-r from-orange-50 to-pink-50 px-4 py-3 dark:border-orange-500/25 dark:from-orange-500/10 dark:to-pink-500/10">
      <span aria-hidden className="flex h-9 w-9 shrink-0 items-center justify-center rounded-xl bg-white text-orange-600 shadow-sm dark:bg-white/10 dark:text-orange-300">
        <Users className="h-4 w-4" />
      </span>
      <p className="min-w-0 flex-1 basis-60 text-sm text-orange-900 dark:text-orange-100">
        <strong className="font-semibold">No visits yet in this period. Share your link to get the first visitors!</strong>
        <span className="block text-xs text-orange-800/80 dark:text-orange-200/80">Every visit to {SITE_URL.replace('https://', '')} shows up here within seconds.</span>
      </p>
      <button
        type="button"
        onClick={() => void copy()}
        className="inline-flex h-9 items-center gap-1.5 rounded-xl bg-white px-3 text-sm font-semibold text-orange-700 shadow-sm ring-1 ring-orange-200 hover:bg-orange-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:bg-white/10 dark:text-orange-200 dark:ring-orange-500/30"
      >
        {copied ? <Check className="h-4 w-4" aria-hidden /> : <Copy className="h-4 w-4" aria-hidden />}
        {copied ? 'Copied' : 'Copy link'}
      </button>
    </div>
  );
}

export function SiteAnalyticsDashboard() {
  const fmt = useFormat();
  const dark = useThemeOptional()?.resolvedTheme === 'dark';
  const [params, setParams] = useSearchParams();
  const selection = useMemo(() => readSelection(params), [params]);
  const range = useMemo(() => rangeFor(selection), [selection]);
  const summaryQuery = useSiteAnalyticsSummary(range ?? {}, range !== null);
  const live = useSiteAnalyticsLive();

  const choose = (next: RangeSelection) => setParams((p) => writeSelection(next, p), { replace: true });

  const summary = summaryQuery.data;
  // The period asked for; the server's own, while a period still being picked asks for none.
  const days = range ? rangeLength(range) : summary ? summary.daily.length || rangeLength({ from: summary.from, to: summary.to }) : 30;
  const previousLabel = previousPeriodLabel(days);
  const shownFrom = range?.from ?? summary?.from;
  const shownTo = range?.to ?? summary?.to;
  const period = shownFrom && shownTo
    ? shownFrom === shownTo
      ? formatDay(shownFrom, { weekday: 'long', day: 'numeric', month: 'long', year: 'numeric' }, fmt.dateLocale)
      : `${formatDay(shownFrom, { day: 'numeric', month: 'short' }, fmt.dateLocale)} – ${formatDay(shownTo, { day: 'numeric', month: 'short', year: 'numeric' }, fmt.dateLocale)}`
    : '';
  // The last period's figures stay on screen, dimmed, while the new one's are read.
  const stale = summaryQuery.isPlaceholderData;
  const dim = cn('transition-opacity duration-200', stale && 'opacity-60');
  const bots = summary?.totals.botHits ?? 0;

  // Access taken away while the page was open: the server refuses, and the page says so.
  if (!summary && summaryQuery.error instanceof ApiError && summaryQuery.error.status === 403) return <NotAvailable />;

  // What stands where the growth chart goes while there is nothing to draw: a period still
  // being picked, the first read on its way, or a read that failed.
  let instead = null;
  if (range === null) {
    instead = (
      <p className="flex min-h-[200px] items-center justify-center rounded-2xl border border-dashed border-slate-200 bg-white/70 px-4 py-6 text-center text-sm text-slate-500 dark:border-white/10 dark:bg-white/[0.03]">
        Pick a start and an end day to see that period.
      </p>
    );
  } else if (!summary && summaryQuery.isPending) {
    instead = <Bone className="h-[420px] w-full rounded-2xl" />;
  } else if (!summary) {
    instead = <LoadFailed what="The site's figures" error={summaryQuery.error} onRetry={() => void summaryQuery.refetch()} retrying={summaryQuery.isFetching} />;
  }

  return (
    <div className="space-y-6">
      <header className="space-y-4">
        <div className="flex flex-wrap items-center gap-3">
          <span aria-hidden className="flex h-11 w-11 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-md shadow-orange-500/25">
            <BarChart3 className="h-5 w-5" />
          </span>
          <div className="min-w-0 flex-1 basis-56">
            <h1 className="text-xl font-bold tracking-tight text-slate-900 sm:text-2xl">
              Site analytics
              <span className="font-normal text-slate-400"> · </span>
              <a href={SITE_URL} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 font-semibold text-slate-600 underline-offset-4 hover:text-orange-600 hover:underline dark:text-slate-300">
                www.cadencee.ai<ExternalLink className="h-3.5 w-3.5" aria-hidden />
              </a>
            </h1>
            <p className="text-sm text-slate-500">Who visits, where they come from and what they do.</p>
          </div>
          <LivePill live={live} />
        </div>
        <div className="flex flex-wrap items-center justify-between gap-x-4 gap-y-2">
          <RangePicker selection={selection} onChange={choose} />
          {period && (
            <p className="text-xs text-slate-500" aria-live="polite" data-testid="period">
              <span className="font-medium text-slate-700 dark:text-slate-200">{period}</span>
              {' · vs '}{previousLabel}
              {summaryQuery.isFetching && summary ? ' · updating…' : ''}
            </p>
          )}
        </div>
      </header>

      {summary && summaryQuery.isError && (
        <LoadFailed stale what="The site's figures" error={summaryQuery.error} onRetry={() => void summaryQuery.refetch()} retrying={summaryQuery.isFetching} />
      )}

      {/* Live keeps its place whatever the figures are doing: it is read on its own. */}
      <section aria-labelledby="sa-overview" className="space-y-4">
        <h2 id="sa-overview" className="sr-only">Overview</h2>
        {summary && isEmpty(summary) && <NoVisitsYet />}
        {summary
          ? <div className={dim} aria-busy={stale || undefined}><KpiGrid summary={summary} previousLabel={previousLabel} dark={dark} /></div>
          : range !== null && summaryQuery.isPending && <KpiGridSkeleton />}
        <div className="grid gap-4 lg:grid-cols-3 [&>*]:min-w-0">
          <div className="lg:col-span-2">
            {summary ? (
              <SettingsCard icon={TrendingUp} tone="orange" title="Growth" description="Daily visitors and page views, with the 7-day average." className={cn('h-full', dim)}>
                <GrowthChart summary={summary} previousLabel={previousLabel} dark={dark} />
              </SettingsCard>
            ) : instead}
          </div>
          <LiveNow live={live} />
        </div>
      </section>

      {summary && (
        <div className={cn('space-y-10', dim)} aria-busy={stale || undefined}>
          <section aria-labelledby="sa-sources">
            <SectionHead id="sa-sources" eyebrow="Acquisition" title="Where they come from" accent="amber" icon={Compass}
              blurb="Search engines, social networks, AI assistants, email and links on other sites." />
            <SourcesSection summary={summary} />
          </section>

          <section aria-labelledby="sa-pages">
            <SectionHead id="sa-pages" eyebrow="Content" title="What they look at" accent="indigo" icon={FileText} />
            <PagesSection summary={summary} />
          </section>

          <section aria-labelledby="sa-actions">
            <SectionHead id="sa-actions" eyebrow="Behaviour" title="What they do" accent="emerald" icon={MousePointerClick}
              blurb="Sign-up clicks, sign-ups and other events, and how many visitors make it to an account." />
            <ConversionsSection summary={summary} />
          </section>

          <section aria-labelledby="sa-journeys">
            <SectionHead id="sa-journeys" eyebrow="Visits" title="Journeys" accent="sky" icon={Route}
              blurb="Pick a day to follow each visit from its first page to its last." />
            <JourneysSection defaultDay={range?.to ?? (summary.to || todayInZurich())} />
          </section>

          <section aria-labelledby="sa-audience">
            <SectionHead id="sa-audience" eyebrow="Audience" title="Who they are" accent="violet" icon={Users} />
            <AudienceSection summary={summary} dark={dark} />
          </section>
        </div>
      )}

      <footer className="flex items-start gap-2.5 rounded-2xl border border-slate-200 bg-white/70 px-4 py-3 text-xs leading-relaxed text-slate-500 dark:border-white/10 dark:bg-white/[0.03]">
        <ShieldCheck className="mt-px h-4 w-4 shrink-0 text-emerald-600 dark:text-emerald-400" aria-hidden />
        <p data-testid="privacy-note">
          Cookieless, privacy-friendly analytics. Visitors are counted per day with a hash that changes daily; IPs are never stored.
          {' '}Bots excluded ({fmt.number(bots)} bot {bots === 1 ? 'hit' : 'hits'}). Days and times are in Zurich time.
        </p>
      </footer>
    </div>
  );
}

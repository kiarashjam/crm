// Journeys: one day's visits, newest first. Each row says where the visit came from, on
// what, for how long and how many pages, and whether it ended in a sign-up; opened, it
// shows the path it took, step by step.

import { useId, useState } from 'react';
import {
  BadgeCheck, ChevronDown, ChevronLeft, ChevronRight, Eye, Languages, MousePointerClick, Route, UserRound, Zap,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { DateInput } from '@/app/components/DateInput';
import { useFormat } from '@/app/i18n';
import { formatDay } from '@/app/lib/zonedTime';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import {
  SITE_ANALYTICS_TIME_ZONE, type SiteAnalyticsAudience, type SiteAnalyticsSession, type SiteAnalyticsStep,
} from '@/app/api/siteAnalytics';
import { SITE_ANALYTICS_SESSIONS_LIMIT, useSiteAnalyticsSessions } from '@/app/hooks/queries/siteAnalytics';
import { capitalise, eventLabel, formatDuration, journeyText, languageName, sourceLabel, stepText } from './analyticsMath';
import { EmptyNote, InlineError, ListBones } from './AnalyticsUI';
import { SourceIcon, browserIcon, deviceIcon } from './AnalyticsIcons';
import { addDays, todayInZurich } from './range';

const STEP_LOOK = {
  page_view: { icon: Eye, dot: 'bg-indigo-500', tile: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-300' },
  click: { icon: MousePointerClick, dot: 'bg-orange-500', tile: 'bg-orange-50 text-orange-600 dark:bg-orange-500/10 dark:text-orange-300' },
  event: { icon: Zap, dot: 'bg-emerald-500', tile: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300' },
} as const;

/** "+1:05" after the visit began. */
function offset(fromIso: string, atIso: string): string {
  const s = Math.max(0, Math.round((Date.parse(atIso) - Date.parse(fromIso)) / 1000));
  if (Number.isNaN(s)) return '';
  const m = Math.floor(s / 60);
  return m >= 60 ? `+${Math.floor(m / 60)}:${String(m % 60).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}` : `+${m}:${String(s % 60).padStart(2, '0')}`;
}

function Step({ step, startedAt }: { step: SiteAnalyticsStep; startedAt: string }) {
  const look = STEP_LOOK[step.type] ?? STEP_LOOK.event;
  const Icon = look.icon;
  const text = step.type === 'event' ? eventLabel(step.name ?? 'event') : stepText(step);
  return (
    <li className="relative flex items-start gap-3 pb-3 last:pb-0">
      <span aria-hidden className={cn('relative z-10 mt-0.5 flex h-6 w-6 shrink-0 items-center justify-center rounded-full ring-4 ring-white dark:ring-slate-800', look.tile)}>
        <Icon className="h-3 w-3" />
      </span>
      <div className="min-w-0 flex-1">
        <p className={cn('break-words text-sm text-slate-800 dark:text-slate-100', step.type === 'page_view' && 'font-mono text-[12.5px]')}>
          {text}
          {step.type !== 'page_view' && step.path && <span className="text-slate-500"> on <span className="font-mono text-[12px]">{step.path}</span></span>}
        </p>
      </div>
      <time dateTime={step.at} className="shrink-0 text-xs tabular-nums text-slate-400">{offset(startedAt, step.at)}</time>
    </li>
  );
}

function SessionRow({ s }: { s: SiteAnalyticsSession }) {
  const fmt = useFormat();
  const [open, setOpen] = useState(false);
  const panel = useId();
  const Device = deviceIcon(s.device);
  const Browser = browserIcon(s.browser);
  const time = fmt.custom(s.startedAt, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23', timeZone: SITE_ANALYTICS_TIME_ZONE });
  const path = journeyText(s.steps);
  const pages = `${fmt.number(s.pageViews)} ${s.pageViews === 1 ? 'page' : 'pages'}`;
  const from = sourceLabel(s.source);
  return (
    <li className="border-b border-slate-100 last:border-b-0 dark:border-white/5">
      <button
        type="button"
        aria-expanded={open}
        aria-controls={panel}
        onClick={() => setOpen((v) => !v)}
        className="flex w-full items-start gap-3 rounded-xl px-2 py-2.5 text-left transition-colors hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:hover:bg-white/5"
      >
        <span className="w-11 shrink-0 pt-1 text-xs font-semibold tabular-nums text-slate-500">{time}</span>
        <SourceIcon source={s.source} />
        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
            <span className="font-medium text-slate-900 dark:text-white">{from}</span>
            {s.campaign && <span className="rounded-full bg-amber-50 px-1.5 py-0.5 text-[10.5px] font-semibold text-amber-800 dark:bg-amber-500/10 dark:text-amber-200">{s.campaign}</span>}
            {s.converted && (
              <span className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-1.5 py-0.5 text-[10.5px] font-semibold text-emerald-700 ring-1 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/25">
                <BadgeCheck className="h-3 w-3" aria-hidden />Converted
              </span>
            )}
            {s.signedIn && (
              <span className="inline-flex items-center gap-1 rounded-full bg-sky-50 px-1.5 py-0.5 text-[10.5px] font-semibold text-sky-700 dark:bg-sky-500/10 dark:text-sky-300">
                <UserRound className="h-3 w-3" aria-hidden />Signed in
              </span>
            )}
          </span>
          <span className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
            <span className="inline-flex items-center gap-1" title={capitalise(s.device)}><Device className="h-3.5 w-3.5" aria-hidden />{capitalise(s.device)}</span>
            <span className="inline-flex items-center gap-1" title={s.browser}><Browser className="h-3.5 w-3.5" aria-hidden />{s.browser || 'Unknown'}</span>
            <span className="inline-flex items-center gap-1" title={languageName(s.language, fmt.dateLocale)}><Languages className="h-3.5 w-3.5" aria-hidden />{s.language || '—'}</span>
            <span className="tabular-nums">{formatDuration(s.durationSeconds)}</span>
            <span className="tabular-nums">{pages}</span>
            {s.referrerHost && <span className="truncate">via {s.referrerHost}</span>}
          </span>
          {!open && path && <span className="mt-1 block truncate font-mono text-[11.5px] text-slate-400" title={path}>{path}</span>}
        </span>
        <ChevronDown className={cn('mt-1 h-4 w-4 shrink-0 text-slate-400 transition-transform motion-reduce:transition-none', open && 'rotate-180')} aria-hidden />
        <span className="sr-only">{open ? 'Hide the path' : 'Show the path'}</span>
      </button>
      {open && (
        <div id={panel} className="px-2 pb-3 pl-[3.75rem] sm:pl-[4.75rem]">
          {s.steps.length === 0 ? (
            <p className="text-sm text-slate-500">No steps were recorded for this visit.</p>
          ) : (
            <ol aria-label={`Path of the visit at ${time}`} className="relative before:absolute before:bottom-3 before:left-3 before:top-3 before:w-px before:bg-slate-200 dark:before:bg-white/10">
              {s.steps.map((step, i) => <Step key={`${step.at}-${i}`} step={step} startedAt={s.startedAt} />)}
            </ol>
          )}
        </div>
      )}
    </li>
  );
}

export function JourneysSection({ defaultDay, audience }: { defaultDay: string; audience: SiteAnalyticsAudience }) {
  const fmt = useFormat();
  const [picked, setPicked] = useState<{ day: string; forDefault: string } | null>(null);
  // A new period starts the list on its last day again.
  const day = picked && picked.forDefault === defaultDay ? picked.day : defaultDay;
  const setDay = (next: string) => setPicked({ day: next, forDefault: defaultDay });
  const today = todayInZurich();
  const sessions = useSiteAnalyticsSessions(day, audience);
  const rows = sessions.data ?? [];
  const label = formatDay(day, { weekday: 'long', day: 'numeric', month: 'long' }, fmt.dateLocale);

  const picker = (
    <div className="flex items-center gap-1" role="group" aria-label="Day">
      <button
        type="button"
        onClick={() => setDay(addDays(day, -1))}
        aria-label="The day before"
        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:border-white/10 dark:bg-transparent dark:text-slate-300"
      >
        <ChevronLeft className="h-4 w-4" aria-hidden />
      </button>
      <div className="w-[9.5rem]">
        <DateInput value={day} onChange={(v) => { if (v) setDay(v); }} aria-label="Day of the visits" className="h-9" />
      </div>
      <button
        type="button"
        onClick={() => setDay(addDays(day, 1))}
        disabled={day >= today}
        aria-label="The day after"
        className="flex h-9 w-9 items-center justify-center rounded-lg border border-slate-200 bg-white text-slate-600 hover:bg-slate-50 disabled:opacity-40 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 dark:border-white/10 dark:bg-transparent dark:text-slate-300"
      >
        <ChevronRight className="h-4 w-4" aria-hidden />
      </button>
    </div>
  );

  let body;
  if (sessions.isPending) {
    body = <div aria-busy="true"><ListBones rows={5} /></div>;
  } else if (sessions.isError && !sessions.data) {
    body = <InlineError what="The visits" error={sessions.error} onRetry={() => void sessions.refetch()} retrying={sessions.isFetching} />;
  } else if (rows.length === 0) {
    body = <EmptyNote icon={<Route className="h-5 w-5" />}>No visits on {label}. Share your link to get the first visitors!</EmptyNote>;
  } else {
    const converted = rows.filter((r) => r.converted).length;
    body = (
      <div className={cn('transition-opacity', sessions.isPlaceholderData && 'opacity-60')}>
        <p className="mb-2 px-2 text-xs text-slate-500">
          {fmt.number(rows.length)} {rows.length === 1 ? 'visit' : 'visits'} on {label}
          {converted > 0 && <>, <span className="font-semibold text-emerald-700 dark:text-emerald-300">{fmt.number(converted)} converted</span></>}
          {rows.length >= SITE_ANALYTICS_SESSIONS_LIMIT && ` (the latest ${SITE_ANALYTICS_SESSIONS_LIMIT})`}. Times in Zurich time.
        </p>
        <ul aria-label={`Visits on ${label}`} className="max-h-[560px] overflow-y-auto pr-1">
          {rows.map((s) => <SessionRow key={s.id} s={s} />)}
        </ul>
      </div>
    );
  }

  return (
    <SettingsCard icon={Route} tone="sky" title="Recent visits" description="Newest first. Open one to see its path." aside={picker}>
      {body}
    </SettingsCard>
  );
}

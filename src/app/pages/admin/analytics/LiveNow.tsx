// Live: who is on the site in the last 5 minutes, which pages they are reading, and a feed
// of what they just did. Read again every 15 seconds while the page is open and in front;
// the header's Live pill reads the same copy.

import { useEffect, useState, type ComponentType } from 'react';
import { Eye, MousePointerClick, Radio, Zap } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useFormat } from '@/app/i18n';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import {
  SITE_ANALYTICS_TIME_ZONE, type SiteAnalyticsAudience, type SiteAnalyticsLive, type SiteAnalyticsLiveEvent,
} from '@/app/api/siteAnalytics';
import { SITE_ANALYTICS_LIVE_REFRESH_MS } from '@/app/hooks/queries/siteAnalytics';
import { capitalise, eventLabel, relativeTime, sourceLabel } from './analyticsMath';
import { BarRows, Bone, EmptyNote, InlineError, ListBones } from './AnalyticsUI';
import { SourceIcon, deviceIcon } from './AnalyticsIcons';
import { AUDIENCE_NOUN } from './audience';

interface LiveRead {
  data: SiteAnalyticsLive | undefined;
  isPending: boolean;
  isError: boolean;
  isFetching: boolean;
  error: unknown;
  refetch: () => unknown;
}

/** The time now, moved on every `ms`, so "40 s ago" keeps counting between reads. */
function useNow(ms: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const id = window.setInterval(() => setNow(Date.now()), ms);
    return () => window.clearInterval(id);
  }, [ms]);
  return now;
}

function Pulse({ on, className }: { on: boolean; className?: string }) {
  return (
    <span className={cn('relative flex h-2.5 w-2.5 shrink-0', className)} aria-hidden>
      {on && <span className="absolute inline-flex h-full w-full animate-ping rounded-full bg-emerald-400 opacity-70 motion-reduce:animate-none" />}
      <span className={cn('relative inline-flex h-2.5 w-2.5 rounded-full', on ? 'bg-emerald-500' : 'bg-slate-400')} />
    </span>
  );
}

/** The header's pill: "Live · 3 now", a link down to the Live section. */
export function LivePill({ live }: { live: LiveRead }) {
  const fmt = useFormat();
  const n = live.data?.visitorsNow ?? 0;
  const label = live.isError
    ? 'Live: could not be read'
    : live.isPending
      ? 'Live: reading…'
      : `Live: ${fmt.number(n)} ${n === 1 ? 'visitor' : 'visitors'} in the last 5 minutes`;
  return (
    <a
      href="#live"
      aria-label={label}
      title={label}
      className={cn(
        'inline-flex h-9 items-center gap-2 whitespace-nowrap rounded-full px-3 text-sm font-semibold ring-1 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
        n > 0
          ? 'bg-emerald-50 text-emerald-700 ring-emerald-200 hover:bg-emerald-100 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/25'
          : 'bg-white text-slate-600 ring-slate-200 hover:bg-slate-50 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10',
      )}
    >
      <Pulse on={n > 0} />
      Live
      <span aria-hidden className="tabular-nums" data-testid="live-pill-count">
        {live.isPending ? '…' : live.isError ? '!' : `${fmt.number(n)} now`}
      </span>
    </a>
  );
}

const TYPE_LOOK: Record<SiteAnalyticsLiveEvent['type'], { icon: ComponentType<{ className?: string }>; tile: string }> = {
  page_view: { icon: Eye, tile: 'bg-indigo-50 text-indigo-600 dark:bg-indigo-500/10 dark:text-indigo-300' },
  click: { icon: MousePointerClick, tile: 'bg-orange-50 text-orange-600 dark:bg-orange-500/10 dark:text-orange-300' },
  event: { icon: Zap, tile: 'bg-emerald-50 text-emerald-600 dark:bg-emerald-500/10 dark:text-emerald-300' },
};

function what(e: SiteAnalyticsLiveEvent): { verb: string; object: string } {
  if (e.type === 'page_view') return { verb: 'Viewed', object: e.path || '/' };
  if (e.type === 'click') return { verb: 'Clicked', object: e.name ?? 'a link' };
  return { verb: eventLabel(e.name ?? 'event'), object: '' };
}

function FeedItem({ e, now }: { e: SiteAnalyticsLiveEvent; now: number }) {
  const fmt = useFormat();
  const look = TYPE_LOOK[e.type] ?? TYPE_LOOK.event;
  const Icon = look.icon;
  const Device = deviceIcon(e.device);
  const { verb, object } = what(e);
  const clock = fmt.custom(e.at, { hour: '2-digit', minute: '2-digit', second: '2-digit', hourCycle: 'h23', timeZone: SITE_ANALYTICS_TIME_ZONE });
  return (
    <li className="flex items-start gap-2.5 py-2">
      <span aria-hidden className={cn('mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg', look.tile)}>
        <Icon className="h-3.5 w-3.5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm text-slate-700 dark:text-slate-200">
          <span className="font-medium text-slate-900 dark:text-white">{verb}</span>
          {object && <> <span className={cn(e.type === 'page_view' && 'font-mono text-[12.5px]')}>{object}</span></>}
          {e.type !== 'page_view' && e.path && <span className="text-slate-500"> on <span className="font-mono text-[12px]">{e.path}</span></span>}
        </p>
        <p className="mt-0.5 flex flex-wrap items-center gap-x-2 gap-y-0.5 text-xs text-slate-500">
          <span className="inline-flex items-center gap-1"><SourceIcon source={e.source} size="sm" />{sourceLabel(e.source)}</span>
          <span className="inline-flex items-center gap-1"><Device className="h-3.5 w-3.5" aria-hidden />{capitalise(e.device)}</span>
        </p>
      </div>
      <time dateTime={e.at} title={clock} className="shrink-0 whitespace-nowrap pt-0.5 text-xs tabular-nums text-slate-500">
        {relativeTime(e.at, now)}
      </time>
    </li>
  );
}

export function LiveNow({ live, audience }: { live: LiveRead; audience: SiteAnalyticsAudience }) {
  const fmt = useFormat();
  const now = useNow(SITE_ANALYTICS_LIVE_REFRESH_MS);
  const data = live.data;
  const n = data?.visitorsNow ?? 0;

  let body;
  if (live.isPending) {
    body = (
      <div aria-busy="true" className="space-y-4">
        <Bone className="h-12 w-32" />
        <ListBones rows={3} />
      </div>
    );
  } else if (live.isError && !data) {
    body = <InlineError what="Live visitors" error={live.error} onRetry={() => void live.refetch()} retrying={live.isFetching} />;
  } else if (data) {
    const pages = [...data.pages].sort((a, b) => b.visitors - a.visitors);
    body = (
      <div className="space-y-4">
        <div className="flex items-end gap-4">
          <div aria-live="polite" aria-atomic="true">
            <p className="text-4xl font-bold leading-none tracking-[-0.03em] text-slate-900 dark:text-white" data-testid="live-visitors">{fmt.number(n)}</p>
            <p className="mt-1 text-xs text-slate-500">{n === 1 ? 'visitor' : 'visitors'} in the last 5 minutes</p>
          </div>
          <p className="pb-0.5 text-xs text-slate-500">
            <span className="font-semibold tabular-nums text-slate-700 dark:text-slate-200">{fmt.number(data.sessionsNow)}</span> {data.sessionsNow === 1 ? 'visit' : 'visits'}
          </p>
        </div>

        <div>
          <h4 className="mb-1.5 text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Being read now</h4>
          {pages.length === 0 ? (
            <EmptyNote className="min-h-0 py-3">Nobody on the site right now. Visitors show up here within seconds.</EmptyNote>
          ) : (
            <BarRows
              label="Pages being read now"
              tone="emerald"
              valueLabel="visitors"
              mono
              limit={5}
              rows={pages.map((p) => ({ key: p.path, label: p.path || '/', title: p.path, value: p.visitors }))}
            />
          )}
        </div>

        <div>
          <h4 className="mb-1 text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-500">Just happened</h4>
          {data.recent.length === 0 ? (
            <EmptyNote className="min-h-0 py-3">No activity in the last 5 minutes.</EmptyNote>
          ) : (
            <ol aria-label="Recent activity" className="max-h-[300px] divide-y divide-slate-100 overflow-y-auto pr-1 dark:divide-white/5">
              {data.recent.slice(0, 30).map((e, i) => <FeedItem key={`${e.at}-${e.type}-${e.path}-${e.name ?? ''}-${i}`} e={e} now={now} />)}
            </ol>
          )}
        </div>
        {live.isError && <p className="text-xs text-amber-700 dark:text-amber-300">The last refresh failed; trying again shortly.</p>}
      </div>
    );
  }

  return (
    <SettingsCard
      id="live"
      icon={Radio}
      tone="emerald"
      title={<>Live now <Pulse on={n > 0} className="ml-1" /></>}
      description={`${AUDIENCE_NOUN[audience]} in the last 5 minutes, refreshed every 15 seconds.`}
      className="h-full"
    >
      {body}
    </SettingsCard>
  );
}

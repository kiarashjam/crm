// Where they come from: sources with their marks, the sites that sent people, and the
// campaigns (utm) that did.

import { Compass, Globe, Megaphone } from 'lucide-react';
import { useFormat } from '@/app/i18n';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import type { SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { nonEmpty, sourceLabel } from './analyticsMath';
import { BarRows, EmptyNote } from './AnalyticsUI';
import { SourceIcon } from './AnalyticsIcons';

const NO_VISITS = 'No visits yet in this period. Share your link to get the first visitors!';

export function SourcesSection({ summary }: { summary: SiteAnalyticsSummary }) {
  const fmt = useFormat();
  const sources = nonEmpty(summary.sources, (r) => r.sessions);
  const referrers = nonEmpty(summary.referrers, (r) => r.sessions);
  const campaigns = nonEmpty(summary.campaigns, (r) => r.sessions);
  const visitors = (n: number) => `${fmt.number(n)} ${n === 1 ? 'visitor' : 'visitors'}`;

  return (
    <div className="grid gap-4 lg:grid-cols-2 [&>*]:min-w-0">
      <SettingsCard icon={Compass} tone="orange" title="Sources" description="Search, social, AI assistants, email, or typed straight in." className="lg:row-span-2">
        {sources.length === 0 ? (
          <EmptyNote>{NO_VISITS}</EmptyNote>
        ) : (
          <>
            <div className="mb-1 flex justify-end gap-2 px-2 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400" aria-hidden>
              <span>Visits</span><span className="w-[4.5rem] text-right">Visitors</span>
            </div>
            <BarRows
              label="Sources"
              tone="orange"
              valueLabel="visits"
              limit={10}
              rows={sources.map((s) => ({
                key: s.source,
                label: sourceLabel(s.source),
                title: s.source,
                icon: <SourceIcon source={s.source} />,
                value: s.sessions,
                secondary: visitors(s.visitors),
              }))}
            />
          </>
        )}
      </SettingsCard>

      <SettingsCard icon={Globe} tone="sky" title="Referrers" description="Sites with a link that someone followed.">
        {referrers.length === 0 ? (
          <EmptyNote>No other site has sent a visitor yet. A link from a partner, a directory or a post shows up here.</EmptyNote>
        ) : (
          <BarRows
            label="Referrers"
            tone="sky"
            valueLabel="visits"
            rows={referrers.map((r) => ({ key: r.host, label: r.host, title: r.host, icon: <SourceIcon source={r.host} size="sm" />, value: r.sessions }))}
          />
        )}
      </SettingsCard>

      <SettingsCard icon={Megaphone} tone="violet" title="Campaigns" description="Links tagged with utm_campaign.">
        {campaigns.length === 0 ? (
          <EmptyNote>No campaign visits yet. Add utm_source, utm_medium and utm_campaign to the links you share to see them here.</EmptyNote>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <caption className="sr-only">Campaigns</caption>
              <thead className="text-[10.5px] uppercase tracking-wide text-slate-400">
                <tr>
                  <th scope="col" className="px-2 pb-1.5 font-semibold">Campaign</th>
                  <th scope="col" className="px-2 pb-1.5 font-semibold">Source / medium</th>
                  <th scope="col" className="px-2 pb-1.5 text-right font-semibold">Visits</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 dark:divide-white/5">
                {campaigns.slice(0, 8).map((c) => (
                  <tr key={`${c.campaign}|${c.source}|${c.medium}`}>
                    <th scope="row" className="max-w-[12rem] truncate px-2 py-1.5 font-medium text-slate-800 dark:text-slate-100" title={c.campaign}>{c.campaign}</th>
                    <td className="max-w-[10rem] truncate px-2 py-1.5 text-slate-500" title={`${c.source} / ${c.medium}`}>{c.source || '—'} / {c.medium || '—'}</td>
                    <td className="px-2 py-1.5 text-right font-semibold tabular-nums text-slate-900 dark:text-white">{fmt.number(c.sessions)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </SettingsCard>
    </div>
  );
}

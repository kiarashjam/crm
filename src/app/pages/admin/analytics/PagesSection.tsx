// What they look at: the pages read most, where visits begin and where they end, as compact
// bar lists with views and visitors.

import { FileText } from 'lucide-react';
import { useFormat } from '@/app/i18n';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/app/components/ui/tabs';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import type { SiteAnalyticsPageRow, SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { nonEmpty } from './analyticsMath';
import { BarRows, EmptyNote } from './AnalyticsUI';

const LISTS = [
  { value: 'top', label: 'Top pages', field: 'topPages', blurb: 'Read most in the period.', empty: 'No visits yet in this period. Share your link to get the first visitors!' },
  { value: 'entry', label: 'Entry pages', field: 'entryPages', blurb: 'Where visits begin: the doors people walk in by.', empty: 'No visit has started yet in this period.' },
  { value: 'exit', label: 'Exit pages', field: 'exitPages', blurb: 'The last page before leaving.', empty: 'No visit has ended yet in this period.' },
] as const;

function PageList({ rows, label, empty }: { rows: SiteAnalyticsPageRow[]; label: string; empty: string }) {
  const fmt = useFormat();
  const list = nonEmpty(rows, (r) => r.views);
  if (list.length === 0) return <EmptyNote>{empty}</EmptyNote>;
  return (
    <>
      <div className="mb-1 flex justify-between gap-2 px-2 text-[10.5px] font-semibold uppercase tracking-wide text-slate-400" aria-hidden>
        <span>Page</span>
        <span className="flex gap-2"><span>Views</span><span className="w-[4.5rem] text-right">Visitors</span></span>
      </div>
      <BarRows
        label={label}
        tone="indigo"
        valueLabel="views"
        mono
        limit={10}
        rows={list.map((p) => ({
          key: p.path,
          label: p.path || '/',
          title: p.path,
          value: p.views,
          secondary: <>{fmt.number(p.visitors)}<span className="sr-only"> visitors</span></>,
        }))}
      />
    </>
  );
}

export function PagesSection({ summary }: { summary: SiteAnalyticsSummary }) {
  return (
    <SettingsCard icon={FileText} tone="violet" title="Pages" description="The pages people read, and where their visits begin and end.">
      <Tabs defaultValue="top" className="gap-3">
        <TabsList aria-label="Which pages" className="w-full sm:w-fit">
          {LISTS.map((l) => <TabsTrigger key={l.value} value={l.value} className="px-3">{l.label}</TabsTrigger>)}
        </TabsList>
        {LISTS.map((l) => (
          <TabsContent key={l.value} value={l.value}>
            <p className="mb-2 px-2 text-xs text-slate-500">{l.blurb}</p>
            <PageList rows={summary[l.field]} label={l.label} empty={l.empty} />
          </TabsContent>
        ))}
      </Tabs>
    </SettingsCard>
  );
}

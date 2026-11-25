// Audience: the devices, browsers, systems and languages visits came with. Devices are few
// and add up to the whole, so they get a small donut with its shares written beside it; the
// rest are ranked bar lists.

import { Globe2, Languages, Laptop, MonitorSmartphone } from 'lucide-react';
import { Cell, Pie, PieChart, ResponsiveContainer } from 'recharts';
import { useFormat } from '@/app/i18n';
import { SettingsCard } from '@/app/pages/settings/components/SettingsCard';
import type { SiteAnalyticsNamedRow, SiteAnalyticsSummary } from '@/app/api/siteAnalytics';
import { capitalise, languageName, nonEmpty } from './analyticsMath';
import { BarRows, EmptyNote } from './AnalyticsUI';
import { browserIcon, osIcon } from './AnalyticsIcons';

const EMPTY = 'No visits yet in this period.';

/** Each device keeps its colour whatever else is there (validated: indigo, orange, teal; slate for the rest). */
const DEVICE_COLOURS: Record<string, [string, string]> = {
  desktop: ['#6366f1', '#818cf8'],
  mobile: ['#ea580c', '#ea580c'],
  tablet: ['#0d9488', '#0d9488'],
};
const OTHER_COLOUR: [string, string] = ['#94a3b8', '#64748b'];

function Devices({ rows, dark }: { rows: SiteAnalyticsNamedRow[]; dark: boolean }) {
  const fmt = useFormat();
  const total = rows.reduce((s, r) => s + r.sessions, 0);
  if (total === 0) return <EmptyNote>{EMPTY}</EmptyNote>;
  const colour = (name: string) => (DEVICE_COLOURS[name.toLowerCase()] ?? OTHER_COLOUR)[dark ? 1 : 0];
  return (
    <div className="flex flex-wrap items-center gap-x-4 gap-y-3">
      <div className="relative h-24 w-24 shrink-0" aria-hidden>
        <ResponsiveContainer width="100%" height="100%">
          <PieChart>
            <Pie data={rows} dataKey="sessions" nameKey="name" innerRadius="64%" outerRadius="100%" paddingAngle={rows.length > 1 ? 3 : 0} stroke="none" isAnimationActive={false}>
              {rows.map((r) => <Cell key={r.name} fill={colour(r.name)} />)}
            </Pie>
          </PieChart>
        </ResponsiveContainer>
        <span className="absolute inset-0 flex flex-col items-center justify-center">
          <span className="text-lg font-bold leading-none text-slate-900 dark:text-white">{fmt.number(total)}</span>
          <span className="text-[10px] text-slate-500">visits</span>
        </span>
      </div>
      {/* The dot is the device's colour in the donut; its name says the rest. */}
      <ul aria-label="Devices" className="min-w-[10rem] flex-1 space-y-1.5">
        {rows.map((r) => (
          <li key={r.name} className="flex items-center gap-2 text-sm">
            <span aria-hidden className="h-2.5 w-2.5 shrink-0 rounded-full" style={{ backgroundColor: colour(r.name) }} />
            <span className="min-w-0 flex-1 truncate text-slate-700 dark:text-slate-200" title={capitalise(r.name)}>{capitalise(r.name)}</span>
            <span className="font-semibold tabular-nums text-slate-900 dark:text-white">{fmt.number(r.sessions / total, { style: 'percent', maximumFractionDigits: 0 })}</span>
            <span className="w-8 text-right text-xs tabular-nums text-slate-500">{fmt.number(r.sessions)}<span className="sr-only"> visits</span></span>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function AudienceSection({ summary, dark }: { summary: SiteAnalyticsSummary; dark: boolean }) {
  const fmt = useFormat();
  const devices = nonEmpty(summary.devices, (r) => r.sessions);
  const browsers = nonEmpty(summary.browsers, (r) => r.sessions);
  const os = nonEmpty(summary.os, (r) => r.sessions);
  const languages = nonEmpty(summary.languages, (r) => r.sessions);
  const icon = (Icon: ReturnType<typeof browserIcon>) => <Icon className="h-3.5 w-3.5 text-slate-400" aria-hidden />;
  return (
    <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-4 [&>*]:min-w-0">
      <SettingsCard icon={MonitorSmartphone} tone="violet" title="Devices">
        <Devices rows={devices} dark={dark} />
      </SettingsCard>
      <SettingsCard icon={Globe2} tone="sky" title="Browsers">
        {browsers.length === 0 ? <EmptyNote>{EMPTY}</EmptyNote> : (
          <BarRows label="Browsers" tone="sky" valueLabel="visits" limit={6}
            rows={browsers.map((b) => ({ key: b.name, label: b.name || 'Unknown', icon: icon(browserIcon(b.name)), value: b.sessions }))} />
        )}
      </SettingsCard>
      <SettingsCard icon={Laptop} tone="emerald" title="Operating systems">
        {os.length === 0 ? <EmptyNote>{EMPTY}</EmptyNote> : (
          <BarRows label="Operating systems" tone="teal" valueLabel="visits" limit={6}
            rows={os.map((o) => ({ key: o.name, label: o.name || 'Unknown', icon: icon(osIcon(o.name)), value: o.sessions }))} />
        )}
      </SettingsCard>
      <SettingsCard icon={Languages} tone="orange" title="Languages">
        {languages.length === 0 ? <EmptyNote>{EMPTY}</EmptyNote> : (
          <BarRows label="Languages" tone="orange" valueLabel="visits" limit={6}
            rows={languages.map((l) => ({
              key: l.name,
              label: <>{languageName(l.name, fmt.dateLocale)} <span className="ml-1 text-[11px] text-slate-400">{l.name}</span></>,
              title: l.name,
              value: l.sessions,
            }))} />
        )}
      </SettingsCard>
    </div>
  );
}

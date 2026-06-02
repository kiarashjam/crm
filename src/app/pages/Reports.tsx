import { useEffect, useMemo, useState } from 'react';
import {
  BarChart3, TrendingUp, Target, DollarSign, Percent, Loader2,
  Users, UserCheck, Gauge, Sparkles,
} from 'lucide-react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Cell,
  PieChart, Pie, LineChart, Line, Area, AreaChart,
} from 'recharts';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { cn } from '@/app/components/ui/utils';
import { getDeals, getLeads, getActivities } from '@/app/api';
import type { Deal, Lead, Activity } from '@/app/api/types';

const PALETTE = ['#6366f1', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#ec4899', '#84cc16'];

function parseMoney(v?: string): number {
  if (!v) return 0;
  const n = parseFloat(String(v).replace(/[^0-9.-]/g, ''));
  return Number.isNaN(n) ? 0 : n;
}
const fmtMoney = (n: number) =>
  new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(n);
const fmtCompact = (n: number) =>
  new Intl.NumberFormat(undefined, { notation: 'compact', maximumFractionDigits: 1 }).format(n);

function dealState(d: Deal): 'won' | 'lost' | 'open' {
  if (d.isWon) return 'won';
  if (d.closedAtUtc || d.closedReasonCategory) return 'lost';
  return 'open';
}

function groupSum<T>(rows: T[], key: (r: T) => string, val: (r: T) => number) {
  const m = new Map<string, { name: string; value: number; count: number }>();
  for (const r of rows) {
    const k = key(r);
    const e = m.get(k) ?? { name: k, value: 0, count: 0 };
    e.value += val(r);
    e.count += 1;
    m.set(k, e);
  }
  return [...m.values()].sort((a, b) => b.value - a.value);
}

export default function Reports() {
  const [deals, setDeals] = useState<Deal[]>([]);
  const [leads, setLeads] = useState<Lead[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([getDeals(), getLeads(), getActivities()])
      .then(([d, l, a]) => {
        setDeals(Array.isArray(d) ? d : []);
        setLeads(Array.isArray(l) ? l : []);
        setActivities(Array.isArray(a) ? a : []);
      })
      .catch(() => { /* show empty */ })
      .finally(() => setLoading(false));
  }, []);

  const m = useMemo(() => {
    const open = deals.filter((d) => dealState(d) === 'open');
    const won = deals.filter((d) => dealState(d) === 'won');
    const lost = deals.filter((d) => dealState(d) === 'lost');
    const openValue = open.reduce((s, d) => s + parseMoney(d.value), 0);
    const wonValue = won.reduce((s, d) => s + parseMoney(d.value), 0);
    const weighted = open.reduce((s, d) => s + parseMoney(d.value) * ((d.probability ?? 20) / 100), 0);
    const winRate = won.length + lost.length > 0 ? Math.round((won.length / (won.length + lost.length)) * 100) : 0;
    const avgDeal = won.length > 0 ? wonValue / won.length : open.length > 0 ? openValue / open.length : 0;

    const byStage = groupSum(open, (d) => d.dealStageName || d.stage || 'Unstaged', (d) => parseMoney(d.value));
    const byAssignee = groupSum(open, (d) => d.assigneeName || 'Unassigned', (d) => parseMoney(d.value)).slice(0, 8);
    const leadsByStatus = groupSum(leads, (l) => l.status || 'Unknown', () => 1);
    const leadsBySource = groupSum(leads, (l) => l.source || 'Unknown', () => 1).slice(0, 6);

    // ---- Lead analytics ----
    const totalLeads = leads.length;
    const convertedLeads = leads.filter((l) => l.isConverted).length;
    const conversionRate = totalLeads ? Math.round((convertedLeads / totalLeads) * 100) : 0;
    const qualifiedLeads = leads.filter((l) => (l.status || '').toLowerCase().includes('qualified')).length;
    const scored = leads.filter((l) => typeof l.leadScore === 'number');
    const avgScore = scored.length ? Math.round(scored.reduce((s, l) => s + (l.leadScore ?? 0), 0) / scored.length) : 0;
    const leadsByLifecycle = groupSum(leads.filter((l) => l.lifecycleStage), (l) => l.lifecycleStage || '—', () => 1);

    // New leads per week, last 8 weeks.
    const WEEK = 7 * 86_400_000;
    const leadWeeks: { name: string; value: number }[] = [];
    for (let i = 7; i >= 0; i--) {
      const start = Date.now() - (i + 1) * WEEK;
      const end = Date.now() - i * WEEK;
      const value = leads.filter((l) => {
        const t = Date.parse(l.createdAtUtc || '');
        return !Number.isNaN(t) && t >= start && t < end;
      }).length;
      leadWeeks.push({ name: new Date(end).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }), value });
    }
    // Acquisition → qualification → conversion funnel.
    const leadFunnel = [
      { name: 'All leads', value: totalLeads },
      { name: 'Contacted+', value: leads.filter((l) => !/^new$/i.test(l.status || '')).length },
      { name: 'Qualified', value: qualifiedLeads },
      { name: 'Converted', value: convertedLeads },
    ];

    // Activity volume over the last 14 days.
    const days: { name: string; value: number }[] = [];
    const byDay = new Map<string, number>();
    for (const a of activities) {
      const t = Date.parse(a.createdAt);
      if (Number.isNaN(t)) continue;
      const key = new Date(t).toISOString().slice(0, 10);
      byDay.set(key, (byDay.get(key) ?? 0) + 1);
    }
    for (let i = 13; i >= 0; i--) {
      const d = new Date(Date.now() - i * 86_400_000);
      const key = d.toISOString().slice(0, 10);
      days.push({ name: d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }), value: byDay.get(key) ?? 0 });
    }

    return {
      openValue, weighted, winRate, avgDeal,
      wonCount: won.length, lostCount: lost.length, openCount: open.length,
      byStage, byAssignee, leadsByStatus, leadsBySource, days,
      totalLeads, convertedLeads, conversionRate, qualifiedLeads, avgScore,
      leadsByLifecycle, leadWeeks, leadFunnel,
      winLoss: [
        { name: 'Won', value: won.length },
        { name: 'Lost', value: lost.length },
      ],
    };
  }, [deals, leads, activities]);

  const leadKpis = [
    { label: 'Total leads', value: String(m.totalLeads), sub: `${m.qualifiedLeads} qualified`, icon: Users, tone: 'text-blue-600 bg-blue-50' },
    { label: 'Conversion rate', value: `${m.conversionRate}%`, sub: `${m.convertedLeads} converted`, icon: UserCheck, tone: 'text-emerald-600 bg-emerald-50' },
    { label: 'Avg lead score', value: `${m.avgScore}`, sub: 'across scored leads', icon: Gauge, tone: 'text-violet-600 bg-violet-50' },
    { label: 'New (8 wks)', value: String(m.leadWeeks.reduce((s, w) => s + w.value, 0)), sub: 'created recently', icon: Sparkles, tone: 'text-amber-600 bg-amber-50' },
  ];

  const kpis = [
    { label: 'Open pipeline', value: fmtMoney(m.openValue), sub: `${m.openCount} open deals`, icon: DollarSign, tone: 'text-indigo-600 bg-indigo-50' },
    { label: 'Weighted forecast', value: fmtMoney(m.weighted), sub: 'probability-adjusted', icon: TrendingUp, tone: 'text-emerald-600 bg-emerald-50' },
    { label: 'Win rate', value: `${m.winRate}%`, sub: `${m.wonCount} won · ${m.lostCount} lost`, icon: Percent, tone: 'text-teal-600 bg-teal-50' },
    { label: 'Avg deal size', value: fmtMoney(m.avgDeal), sub: 'won (or open) average', icon: Target, tone: 'text-amber-600 bg-amber-50' },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <div className="mb-6">
            <h1 className="flex items-center gap-2 text-2xl font-bold text-slate-900">
              <BarChart3 className="h-6 w-6 text-indigo-600" /> Reports
            </h1>
            <p className="mt-1 text-sm text-slate-500">Pipeline, forecast and activity analytics across your CRM.</p>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : (
            <div className="space-y-6">
              {/* KPI row */}
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                {kpis.map((k) => (
                  <div key={k.label} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                    <div className="flex items-center justify-between">
                      <span className="text-sm font-medium text-slate-500">{k.label}</span>
                      <span className={cn('flex h-8 w-8 items-center justify-center rounded-lg', k.tone)}><k.icon className="h-4 w-4" /></span>
                    </div>
                    <p className="mt-2 text-2xl font-bold text-slate-900">{k.value}</p>
                    <p className="mt-0.5 text-xs text-slate-400">{k.sub}</p>
                  </div>
                ))}
              </div>

              <div className="grid gap-6 lg:grid-cols-2">
                <ChartCard title="Open pipeline by stage">
                  {m.byStage.length === 0 ? <Empty /> : (
                    <ResponsiveContainer width="100%" height={260}>
                      <BarChart data={m.byStage} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                        <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <YAxis tickFormatter={fmtCompact} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <Tooltip formatter={(v: number) => fmtMoney(v)} cursor={{ fill: '#f8fafc' }} />
                        <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={56}>
                          {m.byStage.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  )}
                </ChartCard>

                <ChartCard title="Win / loss">
                  {m.wonCount + m.lostCount === 0 ? <Empty /> : (
                    <ResponsiveContainer width="100%" height={260}>
                      <PieChart>
                        <Pie data={m.winLoss} dataKey="value" nameKey="name" innerRadius={60} outerRadius={92} paddingAngle={3}>
                          <Cell fill="#10b981" />
                          <Cell fill="#ef4444" />
                        </Pie>
                        <Tooltip />
                      </PieChart>
                    </ResponsiveContainer>
                  )}
                </ChartCard>

                <ChartCard title="Open pipeline by owner">
                  {m.byAssignee.length === 0 ? <Empty /> : (
                    <ResponsiveContainer width="100%" height={260}>
                      <BarChart data={m.byAssignee} layout="vertical" margin={{ top: 4, right: 16, left: 10, bottom: 0 }}>
                        <XAxis type="number" tickFormatter={fmtCompact} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <YAxis type="category" dataKey="name" width={90} tick={{ fontSize: 11, fill: '#64748b' }} />
                        <Tooltip formatter={(v: number) => fmtMoney(v)} cursor={{ fill: '#f8fafc' }} />
                        <Bar dataKey="value" radius={[0, 6, 6, 0]} maxBarSize={22}>
                          {m.byAssignee.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  )}
                </ChartCard>

                <ChartCard title="Activity (last 14 days)">
                  <ResponsiveContainer width="100%" height={260}>
                    <LineChart data={m.days} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                      <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#94a3b8' }} interval={1} />
                      <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                      <Tooltip />
                      <Line type="monotone" dataKey="value" stroke="#6366f1" strokeWidth={2.5} dot={false} />
                    </LineChart>
                  </ResponsiveContainer>
                </ChartCard>

                <ChartCard title="Leads by status">
                  {m.leadsByStatus.length === 0 ? <Empty /> : (
                    <ResponsiveContainer width="100%" height={260}>
                      <BarChart data={m.leadsByStatus} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                        <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <Tooltip cursor={{ fill: '#f8fafc' }} />
                        <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={56}>
                          {m.leadsByStatus.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  )}
                </ChartCard>

                <ChartCard title="Leads by source">
                  {m.leadsBySource.length === 0 ? <Empty /> : (
                    <ResponsiveContainer width="100%" height={260}>
                      <PieChart>
                        <Pie data={m.leadsBySource} dataKey="value" nameKey="name" outerRadius={92} label={(e) => e.name}>
                          {m.leadsBySource.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                        </Pie>
                        <Tooltip />
                      </PieChart>
                    </ResponsiveContainer>
                  )}
                </ChartCard>
              </div>

              {/* ---- Leads ---- */}
              <div className="pt-2">
                <h2 className="flex items-center gap-2 text-lg font-bold text-slate-900">
                  <Users className="h-5 w-5 text-indigo-600" /> Leads
                </h2>
                <p className="mb-4 mt-0.5 text-sm text-slate-500">Acquisition, qualification and conversion from your leads.</p>

                <div className="mb-6 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
                  {leadKpis.map((k) => (
                    <div key={k.label} className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-medium text-slate-500">{k.label}</span>
                        <span className={cn('flex h-8 w-8 items-center justify-center rounded-lg', k.tone)}><k.icon className="h-4 w-4" /></span>
                      </div>
                      <p className="mt-2 text-2xl font-bold text-slate-900">{k.value}</p>
                      <p className="mt-0.5 text-xs text-slate-400">{k.sub}</p>
                    </div>
                  ))}
                </div>

                <div className="grid gap-6 lg:grid-cols-2">
                  <ChartCard title="New leads (last 8 weeks)">
                    <ResponsiveContainer width="100%" height={260}>
                      <AreaChart data={m.leadWeeks} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                        <defs>
                          <linearGradient id="leadGrad" x1="0" y1="0" x2="0" y2="1">
                            <stop offset="0%" stopColor="#6366f1" stopOpacity={0.3} />
                            <stop offset="100%" stopColor="#6366f1" stopOpacity={0} />
                          </linearGradient>
                        </defs>
                        <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                        <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <Tooltip />
                        <Area type="monotone" dataKey="value" stroke="#6366f1" strokeWidth={2.5} fill="url(#leadGrad)" />
                      </AreaChart>
                    </ResponsiveContainer>
                  </ChartCard>

                  <ChartCard title="Lead funnel">
                    <ResponsiveContainer width="100%" height={260}>
                      <BarChart data={m.leadFunnel} layout="vertical" margin={{ top: 4, right: 28, left: 20, bottom: 0 }}>
                        <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                        <YAxis type="category" dataKey="name" width={84} tick={{ fontSize: 11, fill: '#64748b' }} />
                        <Tooltip cursor={{ fill: '#f8fafc' }} />
                        <Bar dataKey="value" radius={[0, 6, 6, 0]} maxBarSize={26}>
                          {m.leadFunnel.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                        </Bar>
                      </BarChart>
                    </ResponsiveContainer>
                  </ChartCard>

                  {m.leadsByLifecycle.length > 0 && (
                    <ChartCard title="Leads by lifecycle stage">
                      <ResponsiveContainer width="100%" height={260}>
                        <BarChart data={m.leadsByLifecycle} margin={{ top: 8, right: 12, left: 0, bottom: 0 }}>
                          <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                          <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
                          <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                          <Tooltip cursor={{ fill: '#f8fafc' }} />
                          <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={56}>
                            {m.leadsByLifecycle.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                          </Bar>
                        </BarChart>
                      </ResponsiveContainer>
                    </ChartCard>
                  )}
                </div>
              </div>
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

function ChartCard({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <h3 className="mb-3 text-sm font-semibold text-slate-700">{title}</h3>
      {children}
    </div>
  );
}

function Empty() {
  return <div className="flex h-[260px] items-center justify-center text-sm text-slate-400">No data yet</div>;
}

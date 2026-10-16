// Hospitality-specific reports — revenue by venue, tier distribution, member
// growth, top spenders, attendance trends, loyalty pool.
//
// This is separate from the existing Reports page (which covers leads/deals
// sales analytics) so the sales surface stays untouched.

import { useEffect, useMemo, useState } from 'react';
import {
  BarChart3,
  DollarSign,
  Crown,
  ScanLine,
  Sparkles,
  TrendingUp,
  CalendarHeart,
  Loader2,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getMembers, type Member, type MemberTier, MEMBER_TIERS } from '@/app/api/members';
import { getCharges, type Charge, type ChargeKind } from '@/app/api/charges';
import { getVisits, type VisitVenue } from '@/app/api/visits';
import { getLoyaltyLedger } from '@/app/api/loyalty';
import { getEvents } from '@/app/api/events';

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
}

const day = 86_400_000;

const TIER_COLOR: Record<MemberTier, string> = {
  Bronze: 'bg-amber-400',
  Silver: 'bg-slate-400',
  Gold: 'bg-yellow-400',
  Platinum: 'bg-indigo-500',
};

const KIND_COLOR: Record<ChargeKind, string> = {
  Dues: 'bg-indigo-500',
  Dining: 'bg-orange-500',
  Bar: 'bg-amber-500',
  Event: 'bg-pink-500',
  Spa: 'bg-rose-500',
  Booking: 'bg-emerald-500',
  Retail: 'bg-blue-500',
  Other: 'bg-slate-500',
};

const VENUE_COLOR: Record<VisitVenue, string> = {
  Dining: 'bg-orange-500',
  Bar: 'bg-amber-500',
  Spa: 'bg-pink-500',
  Gym: 'bg-emerald-500',
  Coworking: 'bg-blue-500',
  Event: 'bg-fuchsia-500',
  Lounge: 'bg-violet-500',
  Rooftop: 'bg-teal-500',
};

export default function HospitalityReports() {
  const [members, setMembers] = useState<Member[]>([]);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [visits, setVisits] = useState<Awaited<ReturnType<typeof getVisits>>>([]);
  const [ledger, setLedger] = useState<Awaited<ReturnType<typeof getLoyaltyLedger>>>([]);
  const [events, setEvents] = useState<Awaited<ReturnType<typeof getEvents>>>([]);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      const [m, c, v, l, e] = await Promise.all([
        getMembers(),
        getCharges(),
        getVisits(),
        getLoyaltyLedger(),
        getEvents(),
      ]);
      setMembers(m);
      setCharges(c);
      setVisits(v);
      setLedger(l);
      setEvents(e);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  // KPIs
  const kpis = useMemo(() => {
    const monthAgo = Date.now() - 30 * day;
    const sixMonthsAgo = Date.now() - 180 * day;
    let revenue30d = 0;
    for (const c of charges) {
      if (c.status !== 'Voided' && c.paidAmount > 0 && c.paidAtUtc && Date.parse(c.paidAtUtc) >= monthAgo) {
        revenue30d += c.paidAmount;
      }
    }
    const activeCount = members.filter((m) => m.status === 'Active').length;
    const lapsedCount = members.filter((m) => m.status === 'Lapsed').length;
    const churn = activeCount + lapsedCount > 0 ? lapsedCount / (activeCount + lapsedCount) : 0;
    const recentVisits = visits.filter((v) => Date.parse(v.visitedAtUtc) >= monthAgo).length;
    const newMembers180d = members.filter((m) => Date.parse(m.joinedAtUtc) >= sixMonthsAgo).length;
    const pointsOutstanding = ledger.reduce((s, e) => s + e.points, 0);
    return {
      revenue30d: Math.round(revenue30d * 100) / 100,
      activeCount,
      churn,
      recentVisits,
      newMembers180d,
      pointsOutstanding,
    };
  }, [members, charges, visits, ledger]);

  // Revenue by kind (paid, all-time)
  const revenueByKind = useMemo(() => {
    const map = new Map<ChargeKind, number>();
    for (const c of charges) {
      if (c.status === 'Voided') continue;
      map.set(c.kind, (map.get(c.kind) ?? 0) + c.paidAmount);
    }
    const entries = [...map.entries()].sort((a, b) => b[1] - a[1]);
    const total = entries.reduce((s, [, v]) => s + v, 0);
    return { entries, total };
  }, [charges]);

  // Visits by venue (last 30d)
  const visitsByVenue = useMemo(() => {
    const monthAgo = Date.now() - 30 * day;
    const map = new Map<VisitVenue, number>();
    for (const v of visits) {
      if (Date.parse(v.visitedAtUtc) < monthAgo) continue;
      map.set(v.venue, (map.get(v.venue) ?? 0) + 1);
    }
    const entries = [...map.entries()].sort((a, b) => b[1] - a[1]);
    const max = Math.max(1, ...entries.map(([, v]) => v));
    return { entries, max };
  }, [visits]);

  // Tier distribution
  const tierDistribution = useMemo(() => {
    const map = new Map<MemberTier, number>();
    for (const t of MEMBER_TIERS) map.set(t, 0);
    for (const m of members) {
      if (m.status === 'Active') map.set(m.tier, (map.get(m.tier) ?? 0) + 1);
    }
    const entries = [...map.entries()];
    const total = entries.reduce((s, [, v]) => s + v, 0) || 1;
    return { entries, total };
  }, [members]);

  // Top spenders
  const topSpenders = useMemo(() => {
    const totals = new Map<string, { member: Member; amount: number }>();
    for (const c of charges) {
      if (c.status === 'Voided') continue;
      const member = members.find((m) => m.id === c.memberId);
      if (!member) continue;
      const cur = totals.get(member.id) ?? { member, amount: 0 };
      cur.amount += c.paidAmount;
      totals.set(member.id, cur);
    }
    return [...totals.values()].sort((a, b) => b.amount - a.amount).slice(0, 8);
  }, [charges, members]);

  // Member growth - last 6 months bar chart
  const memberGrowth = useMemo(() => {
    const buckets: { label: string; count: number }[] = [];
    const now = new Date();
    for (let i = 5; i >= 0; i--) {
      const monthDate = new Date(now.getFullYear(), now.getMonth() - i, 1);
      const monthStart = monthDate.getTime();
      const monthEnd = new Date(now.getFullYear(), now.getMonth() - i + 1, 1).getTime();
      const count = members.filter(
        (m) => {
          const j = Date.parse(m.joinedAtUtc);
          return j >= monthStart && j < monthEnd;
        },
      ).length;
      buckets.push({
        label: monthDate.toLocaleDateString(undefined, { month: 'short' }),
        count,
      });
    }
    const max = Math.max(1, ...buckets.map((b) => b.count));
    return { buckets, max };
  }, [members]);

  // Event attendance (% capacity by event status published)
  const eventAttendance = useMemo(() => {
    const published = events.filter((e) => e.status === 'Published' || e.status === 'Completed');
    const totalCapacity = published.reduce((s, e) => s + e.capacity, 0);
    const totalReg = published.reduce((s, e) => s + e.registeredCount, 0);
    const totalWait = published.reduce((s, e) => s + e.waitlistCount, 0);
    return {
      utilization: totalCapacity > 0 ? totalReg / totalCapacity : 0,
      totalReg,
      totalCapacity,
      totalWait,
      count: published.length,
    };
  }, [events]);

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <div className="flex items-center justify-center py-20 text-slate-400">
          <Loader2 className="h-6 w-6 animate-spin" />
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
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-emerald-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-blue-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-emerald-500 to-blue-500 flex items-center justify-center shadow-2xl shadow-emerald-500/30">
                <BarChart3 className="w-8 h-8 text-white" />
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">
                  Club Reports
                </h1>
                <p className="text-slate-400 mt-1">
                  Revenue, attendance, retention, and loyalty — the operational picture.
                </p>
              </div>
            </div>
          </div>

          {/* KPIs */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-4 mb-8">
            <Kpi icon={<DollarSign className="w-4 h-4" />} label="Revenue · 30d" value={formatCurrency(kpis.revenue30d)} />
            <Kpi icon={<Crown className="w-4 h-4" />} label="Active members" value={String(kpis.activeCount)} />
            <Kpi icon={<TrendingUp className="w-4 h-4" />} label="Churn rate" value={`${Math.round(kpis.churn * 100)}%`} />
            <Kpi icon={<ScanLine className="w-4 h-4" />} label="Visits · 30d" value={String(kpis.recentVisits)} />
            <Kpi icon={<Crown className="w-4 h-4" />} label="New · 180d" value={String(kpis.newMembers180d)} />
            <Kpi icon={<Sparkles className="w-4 h-4" />} label="Points outstanding" value={kpis.pointsOutstanding.toLocaleString()} />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
            {/* Revenue by kind */}
            <Card title="Revenue by category" icon={<DollarSign className="w-4 h-4" />}>
              {revenueByKind.entries.length === 0 ? (
                <Empty />
              ) : (
                <ul className="space-y-2">
                  {revenueByKind.entries.map(([kind, val]) => {
                    const pct = revenueByKind.total > 0 ? (val / revenueByKind.total) * 100 : 0;
                    return (
                      <li key={kind}>
                        <div className="flex items-center justify-between text-sm mb-1">
                          <span className="text-slate-700">{kind}</span>
                          <span className="font-semibold text-slate-900">{formatCurrency(val)}</span>
                        </div>
                        <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className={`h-full ${KIND_COLOR[kind]}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </Card>

            {/* Visits by venue */}
            <Card title="Visits by venue · 30d" icon={<ScanLine className="w-4 h-4" />}>
              {visitsByVenue.entries.length === 0 ? (
                <Empty />
              ) : (
                <ul className="space-y-2">
                  {visitsByVenue.entries.map(([venue, count]) => {
                    const pct = (count / visitsByVenue.max) * 100;
                    return (
                      <li key={venue}>
                        <div className="flex items-center justify-between text-sm mb-1">
                          <span className="text-slate-700">{venue}</span>
                          <span className="font-semibold text-slate-900">{count}</span>
                        </div>
                        <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className={`h-full ${VENUE_COLOR[venue]}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </Card>

            {/* Tier distribution */}
            <Card title="Active tier distribution" icon={<Crown className="w-4 h-4" />}>
              <ul className="space-y-2">
                {tierDistribution.entries.map(([tier, count]) => {
                  const pct = (count / tierDistribution.total) * 100;
                  return (
                    <li key={tier}>
                      <div className="flex items-center justify-between text-sm mb-1">
                        <span className="text-slate-700">{tier}</span>
                        <span className="font-semibold text-slate-900">
                          {count}{' '}
                          <span className="text-xs text-slate-400">({Math.round(pct)}%)</span>
                        </span>
                      </div>
                      <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                        <div className={`h-full ${TIER_COLOR[tier]}`} style={{ width: `${pct}%` }} />
                      </div>
                    </li>
                  );
                })}
              </ul>
            </Card>

            {/* Member growth */}
            <Card title="New members · last 6 months" icon={<TrendingUp className="w-4 h-4" />}>
              <div className="flex items-end gap-2 h-32 mb-2">
                {memberGrowth.buckets.map((b) => {
                  const h = (b.count / memberGrowth.max) * 100;
                  return (
                    <div key={b.label} className="flex-1 flex flex-col items-center justify-end">
                      <div
                        className="w-full bg-emerald-500 rounded-t-lg"
                        style={{ height: `${h}%`, minHeight: b.count > 0 ? 4 : 0 }}
                        title={`${b.count} member${b.count === 1 ? '' : 's'}`}
                      />
                    </div>
                  );
                })}
              </div>
              <div className="flex justify-between text-xs text-slate-500">
                {memberGrowth.buckets.map((b) => (
                  <span key={b.label}>
                    {b.label} · {b.count}
                  </span>
                ))}
              </div>
            </Card>

            {/* Top spenders */}
            <Card title="Top spenders" icon={<DollarSign className="w-4 h-4" />}>
              {topSpenders.length === 0 ? (
                <Empty />
              ) : (
                <ol className="space-y-2">
                  {topSpenders.map((row, idx) => (
                    <li
                      key={row.member.id}
                      className="flex items-center justify-between gap-2 text-sm"
                    >
                      <div className="flex items-center gap-3 min-w-0">
                        <span className="text-xs font-bold text-slate-400 w-5">{idx + 1}</span>
                        <span className="font-medium text-slate-900 truncate">
                          {row.member.firstName} {row.member.lastName}
                        </span>
                        <span className="text-xs text-slate-500">{row.member.tier}</span>
                      </div>
                      <span className="font-semibold text-emerald-700">
                        {formatCurrency(row.amount)}
                      </span>
                    </li>
                  ))}
                </ol>
              )}
            </Card>

            {/* Event utilization */}
            <Card title="Event utilization" icon={<CalendarHeart className="w-4 h-4" />}>
              <div className="text-center py-4">
                <p className="text-5xl font-bold text-pink-600">
                  {Math.round(eventAttendance.utilization * 100)}%
                </p>
                <p className="text-sm text-slate-500 mt-2">
                  {eventAttendance.totalReg} reserved across {eventAttendance.totalCapacity} seats
                </p>
                <p className="text-xs text-slate-400 mt-1">
                  {eventAttendance.count} published events · {eventAttendance.totalWait} on waitlist
                </p>
              </div>
            </Card>
          </div>
        </main>
      </PageTransition>
    </div>
  );
}

function Kpi({ icon, label, value }: { icon: React.ReactNode; label: string; value: string }) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-4 shadow-sm">
      <div className="flex items-center gap-1.5 text-xs text-slate-500 uppercase tracking-wide mb-1">
        <span className="text-slate-400">{icon}</span> {label}
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
    </div>
  );
}

function Card({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-4">
        {icon} {title}
      </h2>
      {children}
    </div>
  );
}

function Empty() {
  return <p className="text-sm text-slate-400">Not enough data yet.</p>;
}

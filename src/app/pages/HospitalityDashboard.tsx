// Hospitality operational dashboard — the staff landing page that aggregates
// today's signals across every module without touching the existing sales
// Dashboard.

import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  LayoutDashboard,
  ScanLine,
  CalendarClock,
  CalendarHeart,
  ClipboardList,
  AlertTriangle,
  Wine,
  Sparkles,
  Cake,
  ChevronRight,
  DollarSign,
  Crown,
  Receipt,
  Calculator,
  Loader2,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getMembers, type Member } from '@/app/api/members';
import { getReservations, type Reservation } from '@/app/api/reservations';
import { getEvents, type ClubEvent } from '@/app/api/events';
import { getVisits, type Visit } from '@/app/api/visits';
import { getApplications, type Application } from '@/app/api/applications';
import { getCharges, type Charge } from '@/app/api/charges';
import { getSubscriptions, type Subscription } from '@/app/api/subscriptions';
import { getTabs, VENUE_LABELS as TAB_VENUE_LABELS, type OpenTab } from '@/app/api/tabs';

const day = 86_400_000;

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  } catch {
    return iso;
  }
}

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  );
}

function isThisWeek(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  const diff = d.getTime() - now.getTime();
  return diff >= 0 && diff < 7 * day;
}

interface AnniversaryRow {
  member: Member;
  years: number;
  date: Date;
}

function thisYearAnniversaries(members: Member[]): AnniversaryRow[] {
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const rows: AnniversaryRow[] = [];
  for (const m of members) {
    const joined = new Date(m.joinedAtUtc);
    const thisYear = new Date(now.getFullYear(), joined.getMonth(), joined.getDate());
    const diff = thisYear.getTime() - today.getTime();
    if (diff >= 0 && diff < 14 * day) {
      const years = now.getFullYear() - joined.getFullYear();
      if (years >= 1) rows.push({ member: m, years, date: thisYear });
    }
  }
  return rows.sort((a, b) => a.date.getTime() - b.date.getTime());
}

export default function HospitalityDashboard() {
  const [members, setMembers] = useState<Member[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [visits, setVisits] = useState<Visit[]>([]);
  const [applications, setApplications] = useState<Application[]>([]);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      const [m, r, e, v, a, c, s, t] = await Promise.all([
        getMembers(),
        getReservations(),
        getEvents(),
        getVisits(),
        getApplications(),
        getCharges(),
        getSubscriptions(),
        getTabs(),
      ]);
      setMembers(m);
      setReservations(r);
      setEvents(e);
      setVisits(v);
      setApplications(a);
      setCharges(c);
      setSubscriptions(s);
      setTabs(t);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const todayReservations = useMemo(
    () =>
      reservations
        .filter((r) => isToday(r.startAtUtc) && r.status !== 'Cancelled')
        .sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc)),
    [reservations],
  );

  const todayVisits = useMemo(() => visits.filter((v) => isToday(v.visitedAtUtc)), [visits]);

  const thisWeekEvents = useMemo(
    () =>
      events
        .filter((e) => e.status === 'Published' && isThisWeek(e.startAtUtc))
        .sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc)),
    [events],
  );

  const openTabs = useMemo(() => tabs.filter((t) => t.status === 'Open'), [tabs]);

  const pendingApplications = useMemo(
    () =>
      applications.filter((a) => a.status === 'Submitted' || a.status === 'UnderReview'),
    [applications],
  );

  const overdueSubscriptions = useMemo(
    () => subscriptions.filter((s) => s.status === 'PastDue'),
    [subscriptions],
  );

  const overdueCharges = useMemo(
    () => charges.filter((c) => c.status === 'Overdue'),
    [charges],
  );

  const anniversaries = useMemo(() => thisYearAnniversaries(members), [members]);

  const todayRevenueOpenTabs = useMemo(
    () => openTabs.reduce((s, t) => s + t.total, 0),
    [openTabs],
  );

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
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-cyan-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-emerald-500 to-cyan-500 flex items-center justify-center shadow-2xl shadow-emerald-500/30">
                <LayoutDashboard className="w-8 h-8 text-white" />
              </div>
              <div className="flex-1">
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">
                  Today at the Club
                </h1>
                <p className="text-slate-400 mt-1">
                  {new Date().toLocaleDateString(undefined, {
                    weekday: 'long',
                    month: 'long',
                    day: 'numeric',
                  })}
                </p>
              </div>
              <div className="hidden md:flex gap-2">
                <QuickLinkButton to="/pos" icon={<Calculator className="w-4 h-4" />} label="POS" />
                <QuickLinkButton to="/visits" icon={<ScanLine className="w-4 h-4" />} label="Check in" />
                <QuickLinkButton to="/tabs" icon={<Wine className="w-4 h-4" />} label="Tabs" />
              </div>
            </div>
          </div>

          {/* Top KPIs */}
          <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3 mb-6">
            <KPI
              icon={<ScanLine className="w-4 h-4" />}
              label="Check-ins"
              value={String(todayVisits.length)}
              tone="cyan"
              href="/visits"
            />
            <KPI
              icon={<CalendarClock className="w-4 h-4" />}
              label="Reservations"
              value={String(todayReservations.length)}
              tone="emerald"
              href="/reservations"
            />
            <KPI
              icon={<Wine className="w-4 h-4" />}
              label="Open tabs"
              value={String(openTabs.length)}
              hint={formatCurrency(todayRevenueOpenTabs)}
              tone="amber"
              href="/tabs"
            />
            <KPI
              icon={<ClipboardList className="w-4 h-4" />}
              label="Applications"
              value={String(pendingApplications.length)}
              hint="Awaiting decision"
              tone="blue"
              href="/applications"
            />
            <KPI
              icon={<AlertTriangle className="w-4 h-4" />}
              label="Past due"
              value={String(overdueSubscriptions.length + overdueCharges.length)}
              hint={`${overdueSubscriptions.length} subs · ${overdueCharges.length} charges`}
              tone="rose"
              href="/charges"
            />
            <KPI
              icon={<CalendarHeart className="w-4 h-4" />}
              label="Events · 7d"
              value={String(thisWeekEvents.length)}
              tone="pink"
              href="/events"
            />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Today's reservations */}
            <Card title="Today's reservations" icon={<CalendarClock />} href="/reservations">
              {todayReservations.length === 0 ? (
                <Empty text="No reservations today." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {todayReservations.slice(0, 8).map((r) => (
                    <li key={r.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">
                          {r.memberName}
                        </p>
                        <p className="text-xs text-slate-500 truncate">
                          {r.resourceName} · party of {r.partySize}
                        </p>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-sm font-semibold text-emerald-700">
                          {formatTime(r.startAtUtc)}
                        </p>
                        <p className="text-xs text-slate-400">{r.status}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* Open tabs */}
            <Card title="Open tabs" icon={<Wine />} href="/tabs">
              {openTabs.length === 0 ? (
                <Empty text="No open tabs." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {openTabs.slice(0, 8).map((t) => (
                    <li key={t.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{t.memberName}</p>
                        <p className="text-xs text-slate-500">
                          {TAB_VENUE_LABELS[t.venue]} · {t.items.length} items
                        </p>
                      </div>
                      <p className="text-sm font-semibold text-amber-700 shrink-0">
                        {formatCurrency(t.total)}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* Anniversaries / birthdays */}
            <Card title="Anniversaries this fortnight" icon={<Cake />} href="/members">
              {anniversaries.length === 0 ? (
                <Empty text="No upcoming anniversaries." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {anniversaries.map(({ member, years, date }) => (
                    <li key={member.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">
                          {member.firstName} {member.lastName}
                        </p>
                        <p className="text-xs text-slate-500">
                          {date.toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}{' '}
                          · {member.tier}
                        </p>
                      </div>
                      <span className="text-sm font-semibold text-amber-700 shrink-0">
                        {years} yr{years > 1 ? 's' : ''}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* Pending applications */}
            <Card title="Pending applications" icon={<ClipboardList />} href="/applications">
              {pendingApplications.length === 0 ? (
                <Empty text="No applications awaiting review." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {pendingApplications.slice(0, 6).map((a) => (
                    <li key={a.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">
                          {a.firstName} {a.lastName}
                        </p>
                        <p className="text-xs text-slate-500 truncate">
                          {a.requestedTier} · {a.occupation ?? '—'}
                        </p>
                      </div>
                      <span className="text-xs px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 font-medium">
                        {a.status === 'UnderReview' ? 'Reviewing' : 'New'}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* This week events */}
            <Card title="Events this week" icon={<CalendarHeart />} href="/events">
              {thisWeekEvents.length === 0 ? (
                <Empty text="No events scheduled this week." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {thisWeekEvents.slice(0, 6).map((e) => (
                    <li key={e.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-xl shrink-0">{e.coverEmoji}</span>
                        <div className="min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">{e.name}</p>
                          <p className="text-xs text-slate-500">
                            {new Date(e.startAtUtc).toLocaleDateString(undefined, {
                              weekday: 'short',
                              month: 'short',
                              day: 'numeric',
                            })}
                          </p>
                        </div>
                      </div>
                      <p className="text-xs text-slate-500 shrink-0">
                        {e.registeredCount}/{e.capacity}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* Past due */}
            <Card title="Needs attention" icon={<AlertTriangle />} href="/charges">
              <div className="space-y-3">
                {overdueCharges.length === 0 && overdueSubscriptions.length === 0 && (
                  <Empty text="Everything is on track." />
                )}
                {overdueCharges.slice(0, 4).map((c) => (
                  <div
                    key={c.id}
                    className="flex items-center justify-between gap-2 rounded-lg bg-rose-50 px-3 py-2"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-rose-900 truncate">{c.memberName}</p>
                      <p className="text-xs text-rose-700 truncate">{c.description}</p>
                    </div>
                    <p className="text-sm font-semibold text-rose-700 shrink-0">
                      {formatCurrency(c.amount - c.paidAmount)}
                    </p>
                  </div>
                ))}
                {overdueSubscriptions.slice(0, 4).map((s) => (
                  <div
                    key={s.id}
                    className="flex items-center justify-between gap-2 rounded-lg bg-amber-50 px-3 py-2"
                  >
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-amber-900 truncate">{s.memberName}</p>
                      <p className="text-xs text-amber-700 truncate">{s.name}</p>
                    </div>
                    <p className="text-sm font-semibold text-amber-700 shrink-0">
                      {formatCurrency(s.amount)}
                    </p>
                  </div>
                ))}
              </div>
            </Card>
          </div>

          {/* Quick links footer */}
          <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-6 gap-3 mt-8">
            <QuickLink to="/members" icon={<Crown />} label="Members" />
            <QuickLink to="/calendar" icon={<CalendarHeart />} label="Calendar" />
            <QuickLink to="/loyalty" icon={<Sparkles />} label="Loyalty" />
            <QuickLink to="/charges" icon={<Receipt />} label="House Accounts" />
            <QuickLink to="/campaigns" icon={<DollarSign />} label="Campaigns" />
            <QuickLink to="/club-reports" icon={<LayoutDashboard />} label="Club Reports" />
          </div>
        </main>
      </PageTransition>
    </div>
  );
}

function KPI({
  icon,
  label,
  value,
  hint,
  tone,
  href,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  hint?: string;
  tone: 'cyan' | 'emerald' | 'amber' | 'blue' | 'rose' | 'pink';
  href: string;
}) {
  const toneClass = {
    cyan: 'text-cyan-700 bg-cyan-50',
    emerald: 'text-emerald-700 bg-emerald-50',
    amber: 'text-amber-700 bg-amber-50',
    blue: 'text-blue-700 bg-blue-50',
    rose: 'text-rose-700 bg-rose-50',
    pink: 'text-pink-700 bg-pink-50',
  }[tone];
  return (
    <Link
      to={href}
      className="bg-white rounded-2xl border border-slate-200 p-4 shadow-sm hover:shadow-md transition group"
    >
      <div className={`w-9 h-9 rounded-xl ${toneClass} flex items-center justify-center mb-2`}>
        {icon}
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500 mt-0.5">{label}</p>
      {hint && <p className="text-xs text-slate-400">{hint}</p>}
    </Link>
  );
}

function Card({
  title,
  icon,
  href,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  href?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <div className="flex items-center justify-between mb-3">
        <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2">
          <span className="text-slate-400">{icon}</span> {title}
        </h2>
        {href && (
          <Link
            to={href}
            className="text-xs text-slate-500 hover:text-slate-700 inline-flex items-center gap-0.5"
          >
            Open <ChevronRight className="w-3 h-3" />
          </Link>
        )}
      </div>
      {children}
    </div>
  );
}

function Empty({ text }: { text: string }) {
  return <p className="text-sm text-slate-400 py-3">{text}</p>;
}

function QuickLink({ to, icon, label }: { to: string; icon: React.ReactNode; label: string }) {
  return (
    <Link
      to={to}
      className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm hover:shadow-md transition flex flex-col items-start gap-2"
    >
      <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-indigo-100 to-violet-100 text-indigo-700 flex items-center justify-center">
        {icon}
      </div>
      <span className="text-sm font-medium text-slate-900">{label}</span>
    </Link>
  );
}

function QuickLinkButton({ to, icon, label }: { to: string; icon: React.ReactNode; label: string }) {
  return (
    <Link
      to={to}
      className="inline-flex items-center gap-2 px-3 py-2 rounded-xl bg-white/10 text-white text-sm font-medium hover:bg-white/20 transition backdrop-blur-sm border border-white/20"
    >
      {icon} {label}
    </Link>
  );
}

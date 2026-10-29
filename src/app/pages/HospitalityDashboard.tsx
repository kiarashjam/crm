// Hospitality operational dashboard — the staff landing page that aggregates
// today's signals across every module without touching the existing sales
// Dashboard.

import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
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
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { Button } from '@/app/components/ui/button';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { useClubMoney } from '@/app/hooks/useClubMoney';

const day = 86_400_000;

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  } catch {
    return iso;
  }
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
  const { format: formatCurrency } = useClubMoney();
  const [members, setMembers] = useState<Member[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [visits, setVisits] = useState<Visit[]>([]);
  const [applications, setApplications] = useState<Application[]>([]);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const navigate = useNavigate();

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
        <div className="flex items-center justify-center py-20 text-slate-500">
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
          <PageHero
            icon={LayoutDashboard}
            tone="teal"
            title="Today at the Club"
            subtitle={new Date().toLocaleDateString(undefined, {
              weekday: 'long',
              month: 'long',
              day: 'numeric',
            })}
            actions={
              <div className="hidden md:flex gap-2">
                <QuickLinkButton to="/pos" icon={<Calculator className="h-4 w-4" />} label="POS" />
                <QuickLinkButton to="/visits" icon={<ScanLine className="h-4 w-4" />} label="Check in" />
                <QuickLinkButton to="/tabs" icon={<Wine className="h-4 w-4" />} label="Tabs" />
              </div>
            }
            stats={[
              {
                label: 'Check-ins', hint: 'today', value: todayVisits.length, icon: ScanLine, tone: 'cyan',
                onClick: () => navigate('/visits'), actionLabel: `Check-ins today, ${todayVisits.length}. Open visits.`,
              },
              {
                label: 'Reservations', hint: 'today', value: todayReservations.length, icon: CalendarClock, tone: 'emerald',
                onClick: () => navigate('/reservations'), actionLabel: `Reservations today, ${todayReservations.length}. Open reservations.`,
              },
              {
                label: 'Open tabs', hint: formatCurrency(todayRevenueOpenTabs), value: openTabs.length, icon: Wine, tone: 'amber',
                onClick: () => navigate('/tabs'), actionLabel: `Open tabs, ${openTabs.length}, ${formatCurrency(todayRevenueOpenTabs)}. Open tabs.`,
              },
              {
                label: 'Applications', hint: 'awaiting decision', value: pendingApplications.length, icon: ClipboardList, tone: 'blue',
                onClick: () => navigate('/applications'), actionLabel: `Applications awaiting decision, ${pendingApplications.length}. Open applications.`,
              },
              {
                label: 'Past due', hint: `${overdueSubscriptions.length} subs · ${overdueCharges.length} charges`,
                value: overdueSubscriptions.length + overdueCharges.length, icon: AlertTriangle, tone: 'rose',
                onClick: () => navigate('/charges'),
                actionLabel: `Past due, ${overdueSubscriptions.length + overdueCharges.length}: ${overdueSubscriptions.length} subscriptions and ${overdueCharges.length} charges. Open house accounts.`,
              },
              {
                label: 'Events', hint: '7d', value: thisWeekEvents.length, icon: CalendarHeart, tone: 'pink',
                onClick: () => navigate('/events'), actionLabel: `Events in the next 7 days, ${thisWeekEvents.length}. Open events.`,
              },
            ]}
          />

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Today's reservations */}
            <Card index={0} title="Today's reservations" icon={<CalendarClock />} href="/reservations">
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
                        <p className="text-xs text-slate-500">{r.status}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            {/* Open tabs */}
            <Card index={1} title="Open tabs" icon={<Wine />} href="/tabs">
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
            <Card index={2} title="Anniversaries this fortnight" icon={<Cake />} href="/members">
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
            <Card index={3} title="Pending applications" icon={<ClipboardList />} href="/applications">
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
            <Card index={4} title="Events this week" icon={<CalendarHeart />} href="/events">
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
            <Card index={5} title="Needs attention" icon={<AlertTriangle />} href="/charges">
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
            <QuickLink index={0} to="/members" icon={<Crown />} label="Members" />
            <QuickLink index={1} to="/calendar" icon={<CalendarHeart />} label="Calendar" />
            <QuickLink index={2} to="/loyalty" icon={<Sparkles />} label="Loyalty" />
            <QuickLink index={3} to="/charges" icon={<Receipt />} label="House Accounts" />
            <QuickLink index={4} to="/campaigns" icon={<DollarSign />} label="Campaigns" />
            <QuickLink index={5} to="/club-reports" icon={<LayoutDashboard />} label="Club Reports" />
          </div>
        </main>
      </PageTransition>
    </div>
  );
}

function Card({
  title,
  icon,
  href,
  index,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  href?: string;
  index: number;
  children: React.ReactNode;
}) {
  return (
    <StaggerItem index={index} className={`bg-white rounded-2xl border border-slate-200 p-5 shadow-sm hover:border-teal-200 ${liftOnHover}`}>
      <div className="flex items-center justify-between mb-3">
        <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2">
          <span className="text-slate-500">{icon}</span> {title}
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
    </StaggerItem>
  );
}

function Empty({ text }: { text: string }) {
  return <p className="text-sm text-slate-500 py-3">{text}</p>;
}

function QuickLink({ to, icon, label, index }: { to: string; icon: React.ReactNode; label: string; index: number }) {
  return (
    <StaggerItem index={index}>
      <Link
        to={to}
        className={`h-full rounded-xl border border-slate-200 bg-white p-4 shadow-sm hover:border-indigo-200 flex flex-col items-start gap-2 ${liftOnHover}`}
      >
        <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-indigo-100 to-violet-100 text-indigo-700 flex items-center justify-center">
          {icon}
        </div>
        <span className="text-sm font-medium text-slate-900">{label}</span>
      </Link>
    </StaggerItem>
  );
}

function QuickLinkButton({ to, icon, label }: { to: string; icon: React.ReactNode; label: string }) {
  return (
    <Button asChild variant="outline" className={heroSecondary}>
      <Link to={to}>
        {icon} {label}
      </Link>
    </Button>
  );
}

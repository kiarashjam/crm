// Today at the Club: the staff landing page, with today's signals from every part of the
// club, counted on the server (GET /api/club-reports/today) on the workspace's calendar.
//
// It downloaded every member, booking, event, visit, application, charge, subscription
// and tab to count today's in this browser, and when any of them failed it showed an
// empty day ("Everything is on track") that nobody could tell from a quiet one. It now
// asks the server for the day's figures and lists, and says so when it cannot read them.

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
import { VENUE_LABELS as TAB_VENUE_LABELS, type TabVenue } from '@/app/api/tabs';
import type { ClubCurrencyAmount } from '@/app/api/clubReports';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { useClubToday } from '@/app/hooks/queries/club';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';

function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, { hour: 'numeric', minute: '2-digit' });
  } catch {
    return iso;
  }
}

/** A date-only value (yyyy-MM-dd) as this browser's calendar day, with no time zone to shift it. */
function dayOf(date: string): Date {
  const [y, m, d] = date.split('-').map(Number) as [number, number, number];
  return new Date(y, m - 1, d);
}

export default function HospitalityDashboard() {
  const { locale } = useRegional();
  const query = useClubToday();
  const day = query.data;
  usePageLoading(query.isPending);
  const navigate = useNavigate();

  const money = (amount: number, currency: string) => formatClubMoney(amount, currency, locale);
  const amounts = (rows: ClubCurrencyAmount[]) => (rows.length ? rows.map((r) => money(r.amount, r.currency)).join(' + ') : money(0, day?.currency ?? 'CHF'));

  if (query.isPending) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <div className="flex items-center justify-center py-20 text-slate-500" role="status">
          <Loader2 className="h-6 w-6 animate-spin" aria-hidden="true" /> <span className="sr-only">Loading today</span>
        </div>
      </div>
    );
  }

  if (!day) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <LoadFailed what="Today at the Club" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} className="mt-6" />
        </main>
      </div>
    );
  }

  const pastDueCount = day.pastDue.charges + day.pastDue.subscriptions;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={LayoutDashboard}
            tone="teal"
            title="Today at the Club"
            subtitle={dayOf(day.date).toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}
            actions={
              <div className="hidden md:flex gap-2">
                <QuickLinkButton to="/pos" icon={<Calculator className="h-4 w-4" />} label="POS" />
                <QuickLinkButton to="/visits" icon={<ScanLine className="h-4 w-4" />} label="Check in" />
                <QuickLinkButton to="/tabs" icon={<Wine className="h-4 w-4" />} label="Tabs" />
              </div>
            }
            stats={[
              {
                label: 'Check-ins', hint: 'today', value: day.checkIns, icon: ScanLine, tone: 'cyan',
                onClick: () => navigate('/visits'), actionLabel: `Check-ins today, ${day.checkIns}. Open visits.`,
              },
              {
                label: 'Reservations', hint: 'today', value: day.reservations.count, icon: CalendarClock, tone: 'emerald',
                onClick: () => navigate('/reservations'), actionLabel: `Reservations today, ${day.reservations.count}. Open reservations.`,
              },
              {
                label: 'Open tabs', hint: amounts(day.openTabs.totals), value: day.openTabs.count, icon: Wine, tone: 'amber',
                onClick: () => navigate('/tabs'), actionLabel: `Open tabs, ${day.openTabs.count}, ${amounts(day.openTabs.totals)}. Open tabs.`,
              },
              {
                label: 'Applications', hint: 'awaiting decision', value: day.applications.count, icon: ClipboardList, tone: 'blue',
                onClick: () => navigate('/applications'), actionLabel: `Applications awaiting decision, ${day.applications.count}. Open applications.`,
              },
              {
                label: 'Past due', hint: `${day.pastDue.subscriptions} subs · ${day.pastDue.charges} charges`,
                value: pastDueCount, icon: AlertTriangle, tone: 'rose',
                onClick: () => navigate('/charges'),
                actionLabel: `Past due, ${pastDueCount}: ${day.pastDue.subscriptions} subscriptions and ${day.pastDue.charges} charges. Open house accounts.`,
              },
              {
                label: 'Events', hint: '7d', value: day.eventsThisWeek.count, icon: CalendarHeart, tone: 'pink',
                onClick: () => navigate('/events'), actionLabel: `Events in the next 7 days, ${day.eventsThisWeek.count}. Open events.`,
              },
            ]}
          />

          {query.isError && (
            <LoadFailed stale what="Today at the Club" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} className="mb-4" />
          )}

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            <Card index={0} title="Today's reservations" icon={<CalendarClock />} href="/reservations" more={day.reservations.count - day.reservations.items.length}>
              {day.reservations.items.length === 0 ? (
                <Empty text="No reservations today." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {day.reservations.items.map((r) => (
                    <li key={r.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{r.memberName}</p>
                        <p className="text-xs text-slate-500 truncate">{r.resourceName} · party of {r.partySize}</p>
                      </div>
                      <div className="text-right shrink-0">
                        <p className="text-sm font-semibold text-emerald-700">{formatTime(r.startAtUtc)}</p>
                        <p className="text-xs text-slate-500">{r.status}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card index={1} title="Open tabs" icon={<Wine />} href="/tabs" more={day.openTabs.count - day.openTabs.items.length}>
              {day.openTabs.items.length === 0 ? (
                <Empty text="No open tabs." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {day.openTabs.items.map((t) => (
                    <li key={t.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{t.memberName}</p>
                        <p className="text-xs text-slate-500">{TAB_VENUE_LABELS[t.venue as TabVenue] ?? t.venue} · {t.items} item{t.items === 1 ? '' : 's'}</p>
                      </div>
                      <p className="text-sm font-semibold text-amber-700 shrink-0">{money(t.total, t.currency)}</p>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card index={2} title="Anniversaries this fortnight" icon={<Cake />} href="/members">
              {day.anniversaries.length === 0 ? (
                <Empty text="No upcoming anniversaries." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {day.anniversaries.map((a) => (
                    <li key={a.memberId} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{a.firstName} {a.lastName}</p>
                        <p className="text-xs text-slate-500">
                          {dayOf(a.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })} · {a.tier}
                        </p>
                      </div>
                      <span className="text-sm font-semibold text-amber-700 shrink-0">{a.years} yr{a.years > 1 ? 's' : ''}</span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card index={3} title="Pending applications" icon={<ClipboardList />} href="/applications" more={day.applications.count - day.applications.items.length}>
              {day.applications.items.length === 0 ? (
                <Empty text="No applications awaiting review." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {day.applications.items.map((a) => (
                    <li key={a.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="min-w-0">
                        <p className="text-sm font-medium text-slate-900 truncate">{a.firstName} {a.lastName}</p>
                        <p className="text-xs text-slate-500 truncate">{a.requestedTier} · {a.occupation ?? '—'}</p>
                      </div>
                      <span className="text-xs px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 font-medium">
                        {a.status === 'UnderReview' ? 'Reviewing' : 'New'}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card index={4} title="Events this week" icon={<CalendarHeart />} href="/events" more={day.eventsThisWeek.count - day.eventsThisWeek.items.length}>
              {day.eventsThisWeek.items.length === 0 ? (
                <Empty text="No events scheduled this week." />
              ) : (
                <ul className="divide-y divide-slate-100">
                  {day.eventsThisWeek.items.map((e) => (
                    <li key={e.id} className="py-2.5 flex items-center justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="text-xl shrink-0">{e.coverEmoji}</span>
                        <div className="min-w-0">
                          <p className="text-sm font-medium text-slate-900 truncate">{e.name}</p>
                          <p className="text-xs text-slate-500">
                            {new Date(e.startAtUtc).toLocaleDateString(undefined, { weekday: 'short', month: 'short', day: 'numeric' })}
                          </p>
                        </div>
                      </div>
                      <p className="text-xs text-slate-500 shrink-0">
                        {/* A capacity of 0 is no limit. */}
                        {e.capacity > 0 ? `${e.registeredCount}/${e.capacity}` : `${e.registeredCount} registered`}
                      </p>
                    </li>
                  ))}
                </ul>
              )}
            </Card>

            <Card index={5} title="Needs attention" icon={<AlertTriangle />} href="/charges">
              <div className="space-y-3">
                {pastDueCount === 0 && <Empty text="Nothing is past due." />}
                {day.pastDue.charges > 0 && (
                  <p className="text-xs text-rose-700">
                    {day.pastDue.charges} charge{day.pastDue.charges === 1 ? '' : 's'} past due: {amounts(day.pastDue.owed)} still owed.
                  </p>
                )}
                {day.pastDue.chargeItems.map((c) => (
                  <div key={c.id} className="flex items-center justify-between gap-2 rounded-lg bg-rose-50 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-rose-900 truncate">{c.memberName}</p>
                      <p className="text-xs text-rose-700 truncate">{c.description}</p>
                    </div>
                    <p className="text-sm font-semibold text-rose-700 shrink-0">{money(c.owed, c.currency)}</p>
                  </div>
                ))}
                {day.pastDue.subscriptionItems.map((s) => (
                  <div key={s.id} className="flex items-center justify-between gap-2 rounded-lg bg-amber-50 px-3 py-2">
                    <div className="min-w-0">
                      <p className="text-sm font-medium text-amber-900 truncate">{s.memberName}</p>
                      <p className="text-xs text-amber-700 truncate">{s.name}</p>
                    </div>
                    <p className="text-sm font-semibold text-amber-700 shrink-0">{money(s.amount, s.currency)}</p>
                  </div>
                ))}
              </div>
            </Card>
          </div>

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

function Card({ title, icon, href, index, more = 0, children }: {
  title: string;
  icon: React.ReactNode;
  href?: string;
  index: number;
  /** How many more there are than the card lists. */
  more?: number;
  children: React.ReactNode;
}) {
  return (
    <StaggerItem index={index} className={`bg-white rounded-2xl border border-slate-200 p-5 shadow-sm hover:border-teal-200 ${liftOnHover}`}>
      <div className="flex items-center justify-between mb-3">
        <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2">
          <span className="text-slate-500">{icon}</span> {title}
        </h2>
        {href && (
          <Link to={href} className="text-xs text-slate-500 hover:text-slate-700 inline-flex items-center gap-0.5">
            {more > 0 ? `${more} more` : 'Open'} <ChevronRight className="w-3 h-3" />
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

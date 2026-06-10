// Member-facing portal preview.
//
// This is what a member sees when they log into the club's white-labeled
// portal — staff use this view to verify how a member's account looks from
// their side. Read-only by design; the staff CRM is the writeable surface.

import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Sparkles,
  Receipt,
  CalendarClock,
  CalendarHeart,
  Gift,
  Mail,
  Phone,
  ScanLine,
  Building2,
  ChevronRight,
  Loader2,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  getMembers,
  type Member,
  type MemberTier,
} from '@/app/api/members';
import { getCharges, type Charge } from '@/app/api/charges';
import { getReservations, type Reservation } from '@/app/api/reservations';
import { getLoyaltyLedger, type LoyaltyEntry, POINTS_PER_DOLLAR } from '@/app/api/loyalty';
import { getEvents, type ClubEvent } from '@/app/api/events';
import { getVisits, type Visit } from '@/app/api/visits';

const TIER_BAR: Record<MemberTier, string> = {
  Bronze: 'from-amber-300 to-amber-500',
  Silver: 'from-slate-300 to-slate-500',
  Gold: 'from-yellow-300 to-yellow-500',
  Platinum: 'from-indigo-300 to-indigo-500',
};

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

export default function MemberPortal() {
  const [members, setMembers] = useState<Member[]>([]);
  const [selectedId, setSelectedId] = useState<string>('');
  const [charges, setCharges] = useState<Charge[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [ledger, setLedger] = useState<LoyaltyEntry[]>([]);
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [visits, setVisits] = useState<Visit[]>([]);
  const [loading, setLoading] = useState(true);

  const load = async () => {
    try {
      const [m, c, r, l, e, v] = await Promise.all([
        getMembers(),
        getCharges(),
        getReservations(),
        getLoyaltyLedger(),
        getEvents(),
        getVisits(),
      ]);
      setMembers(m);
      setCharges(c);
      setReservations(r);
      setLedger(l);
      setEvents(e);
      setVisits(v);
      if (!selectedId && m[0]) setSelectedId(m[0].id);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const member = useMemo(
    () => members.find((m) => m.id === selectedId) ?? null,
    [members, selectedId],
  );

  const memberCharges = useMemo(
    () => charges.filter((c) => c.memberId === selectedId),
    [charges, selectedId],
  );
  const memberReservations = useMemo(
    () =>
      reservations.filter(
        (r) =>
          r.memberEmail === member?.email &&
          Date.parse(r.startAtUtc) > Date.now() &&
          r.status !== 'Cancelled',
      ),
    [reservations, member?.email],
  );
  const memberLedger = useMemo(
    () => ledger.filter((l) => l.memberId === selectedId),
    [ledger, selectedId],
  );
  const memberVisits = useMemo(
    () => visits.filter((v) => v.memberId === selectedId),
    [visits, selectedId],
  );

  const loyaltyBalance = useMemo(
    () => memberLedger.reduce((s, e) => s + e.points, 0),
    [memberLedger],
  );

  const outstanding = useMemo(
    () =>
      memberCharges
        .filter((c) => c.status !== 'Paid' && c.status !== 'Voided')
        .reduce((s, c) => s + (c.amount - c.paidAmount), 0),
    [memberCharges],
  );

  const upcomingEvents = useMemo(
    () =>
      events
        .filter((e) => e.status === 'Published' && Date.parse(e.startAtUtc) > Date.now())
        .slice(0, 6),
    [events],
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
          {/* Staff impersonation banner */}
          <div className="bg-amber-50 border border-amber-200 rounded-xl px-4 py-3 mb-6 flex items-center gap-3">
            <Building2 className="w-5 h-5 text-amber-600 shrink-0" />
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-amber-900">Staff preview — Member Portal</p>
              <p className="text-xs text-amber-700">
                This is what a member sees after logging in. Pick a member to preview their account.
              </p>
            </div>
            <Select value={selectedId} onValueChange={setSelectedId}>
              <SelectTrigger className="w-56 bg-white">
                <SelectValue placeholder="Member" />
              </SelectTrigger>
              <SelectContent>
                {members.map((m) => (
                  <SelectItem key={m.id} value={m.id}>
                    {m.firstName} {m.lastName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {!member ? (
            <div className="text-center text-slate-500 py-20">
              Pick a member to preview their portal view.
            </div>
          ) : (
            <>
              {/* Welcome card */}
              <div className="relative bg-white rounded-2xl shadow-sm border border-slate-200 overflow-hidden mb-6">
                <div className={`h-3 bg-gradient-to-r ${TIER_BAR[member.tier]}`} />
                <div className="p-6">
                  <p className="text-xs uppercase tracking-wider text-slate-400">
                    Welcome back
                  </p>
                  <h1 className="text-3xl font-bold text-slate-900 mt-1">
                    {member.firstName}
                  </h1>
                  <p className="text-sm text-slate-500 mt-1">
                    {member.tier} member · joined {formatDate(member.joinedAtUtc)}
                  </p>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-3 text-sm text-slate-600">
                    <span className="inline-flex items-center gap-1">
                      <Mail className="w-3.5 h-3.5 text-slate-400" /> {member.email}
                    </span>
                    {member.phone && (
                      <span className="inline-flex items-center gap-1">
                        <Phone className="w-3.5 h-3.5 text-slate-400" /> {member.phone}
                      </span>
                    )}
                    <span className="inline-flex items-center gap-1">
                      <CalendarClock className="w-3.5 h-3.5 text-slate-400" /> Renews {formatDate(member.renewsAtUtc)}
                    </span>
                  </div>
                </div>
              </div>

              {/* Balance + points + visits */}
              <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mb-6">
                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                  <p className="text-xs uppercase tracking-wide text-slate-500">House account</p>
                  <p
                    className={`text-3xl font-bold mt-1 ${
                      member.houseAccountBalance < 0 ? 'text-rose-600' : 'text-slate-900'
                    }`}
                  >
                    {formatCurrency(member.houseAccountBalance)}
                  </p>
                  {outstanding > 0 ? (
                    <p className="text-sm text-amber-700 mt-1">
                      {formatCurrency(outstanding)} outstanding — see below
                    </p>
                  ) : (
                    <p className="text-sm text-slate-500 mt-1">No open charges</p>
                  )}
                </div>
                <div className="rounded-2xl border border-amber-200 bg-gradient-to-br from-amber-50 to-yellow-50 p-5 shadow-sm">
                  <p className="text-xs uppercase tracking-wide text-amber-700">Loyalty points</p>
                  <p className="text-3xl font-bold mt-1 text-amber-900">
                    {loyaltyBalance.toLocaleString()}
                  </p>
                  <p className="text-sm text-amber-700 mt-1">
                    ≈ {formatCurrency(loyaltyBalance / POINTS_PER_DOLLAR)} in redemption value
                  </p>
                </div>
                <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
                  <p className="text-xs uppercase tracking-wide text-slate-500">Visits</p>
                  <p className="text-3xl font-bold mt-1 text-slate-900">
                    {memberVisits.length}
                  </p>
                  <p className="text-sm text-slate-500 mt-1">All-time</p>
                </div>
              </div>

              {/* Upcoming for me */}
              <Section title="My upcoming reservations" icon={<CalendarClock className="w-4 h-4" />}>
                {memberReservations.length === 0 ? (
                  <p className="text-sm text-slate-500">
                    No upcoming reservations.{' '}
                    <Link to="/reservations" className="text-pink-600 hover:underline">
                      Book a room
                    </Link>
                  </p>
                ) : (
                  <ul className="divide-y divide-slate-100">
                    {memberReservations.map((r) => (
                      <li key={r.id} className="py-3 flex items-center justify-between gap-3">
                        <div>
                          <p className="font-medium text-slate-900">{r.resourceName}</p>
                          <p className="text-sm text-slate-500">
                            {formatDateTime(r.startAtUtc)} · party of {r.partySize}
                          </p>
                        </div>
                        <span className="text-xs px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200">
                          {r.status}
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </Section>

              {/* Open charges */}
              <Section title="My open charges" icon={<Receipt className="w-4 h-4" />}>
                {memberCharges.filter((c) => c.status !== 'Paid' && c.status !== 'Voided').length === 0 ? (
                  <p className="text-sm text-slate-500">All paid up. Thank you!</p>
                ) : (
                  <ul className="divide-y divide-slate-100">
                    {memberCharges
                      .filter((c) => c.status !== 'Paid' && c.status !== 'Voided')
                      .map((c) => (
                        <li key={c.id} className="py-3 flex items-center justify-between gap-3">
                          <div className="min-w-0">
                            <p className="font-medium text-slate-900">{c.description}</p>
                            <p className="text-xs text-slate-500">
                              {c.kind} · posted {formatDate(c.postedAtUtc)}
                              {c.dueAtUtc && <> · due {formatDate(c.dueAtUtc)}</>}
                            </p>
                          </div>
                          <p className="font-semibold text-slate-900">{formatCurrency(c.amount - c.paidAmount)}</p>
                        </li>
                      ))}
                  </ul>
                )}
                <div className="mt-4 pt-4 border-t border-slate-100 flex justify-end">
                  <Button asChild variant="outline" size="sm">
                    <Link to="/charges">
                      View full statement <ChevronRight className="w-3.5 h-3.5 ml-1" />
                    </Link>
                  </Button>
                </div>
              </Section>

              {/* Discover events */}
              <Section title="Upcoming events" icon={<CalendarHeart className="w-4 h-4" />}>
                {upcomingEvents.length === 0 ? (
                  <p className="text-sm text-slate-500">No upcoming events on the calendar.</p>
                ) : (
                  <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3">
                    {upcomingEvents.map((e) => {
                      const available = e.capacity - e.registeredCount;
                      return (
                        <Link
                          key={e.id}
                          to={`/events/${e.id}`}
                          className="rounded-xl border border-slate-200 bg-white p-3 hover:shadow-md transition flex items-start gap-3"
                        >
                          <span className="text-3xl">{e.coverEmoji}</span>
                          <div className="min-w-0">
                            <p className="font-semibold text-slate-900 truncate">{e.name}</p>
                            <p className="text-xs text-slate-500">{formatDateTime(e.startAtUtc)}</p>
                            <p className="text-xs mt-1">
                              <span className="font-medium text-pink-600">
                                {formatCurrency(e.memberPrice)}
                              </span>{' '}
                              · {available > 0 ? `${available} spots` : 'Waitlist'}
                            </p>
                          </div>
                        </Link>
                      );
                    })}
                  </div>
                )}
              </Section>

              {/* Recent loyalty */}
              <Section title="Recent loyalty activity" icon={<Sparkles className="w-4 h-4" />}>
                {memberLedger.length === 0 ? (
                  <p className="text-sm text-slate-500">No loyalty activity yet.</p>
                ) : (
                  <ul className="divide-y divide-slate-100">
                    {memberLedger.slice(0, 6).map((e) => (
                      <li key={e.id} className="py-3 flex items-center justify-between">
                        <div className="min-w-0">
                          <p className="font-medium text-slate-900 truncate">
                            {e.note ?? e.reason}
                          </p>
                          <p className="text-xs text-slate-500">{formatDate(e.createdAtUtc)}</p>
                        </div>
                        <span
                          className={`font-semibold ${
                            e.points >= 0 ? 'text-emerald-600' : 'text-rose-600'
                          }`}
                        >
                          {e.points >= 0 ? '+' : ''}
                          {e.points.toLocaleString()} pts
                        </span>
                      </li>
                    ))}
                  </ul>
                )}
              </Section>

              {/* Quick links */}
              <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mt-6">
                <QuickLink href="/reservations" icon={<CalendarClock />} label="Book a room" />
                <QuickLink href="/events" icon={<CalendarHeart />} label="Browse events" />
                <QuickLink href="/gift-cards" icon={<Gift />} label="Send a gift card" />
                <QuickLink href="/visits" icon={<ScanLine />} label="Recent visits" />
              </div>

              <p className="text-xs text-slate-400 text-center mt-10">
                This is a read-only preview of the member portal. Account changes flow through staff workflows.
              </p>
            </>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

function Section({
  title,
  icon,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm mb-4">
      <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-3">
        {icon} {title}
      </h2>
      {children}
    </section>
  );
}

function QuickLink({ href, icon, label }: { href: string; icon: React.ReactNode; label: string }) {
  return (
    <Link
      to={href}
      className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm hover:shadow-md transition flex flex-col items-start gap-2"
    >
      <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-indigo-100 to-violet-100 text-indigo-700 flex items-center justify-center">
        {icon}
      </div>
      <span className="text-sm font-medium text-slate-900">{label}</span>
    </Link>
  );
}


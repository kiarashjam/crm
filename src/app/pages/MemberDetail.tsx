import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  Mail,
  Phone,
  CalendarClock,
  Sparkles,
  Receipt,
  ScanLine,
  CalendarHeart,
  Gift,
  MessageSquareText,
  Pencil,
  Trash2,
  Loader2,
  ChevronRight,
  AlertCircle,
  Crown,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { RecordSkeleton } from '@/app/components/PageSkeleton';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import {
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getMembers,
  deleteMember,
  type Member,
  type MemberTier,
  type MemberStatus,
} from '@/app/api/members';
import { getCharges, getPayments, type Charge, type Payment } from '@/app/api/charges';
import { getVisits, type Visit } from '@/app/api/visits';
import { getReservations, type Reservation } from '@/app/api/reservations';
import { getLoyaltyLedger, type LoyaltyEntry, REASON_LABELS } from '@/app/api/loyalty';
import { commStatusLabel, getCommunications, type Communication } from '@/app/api/communications';
import { getGiftCards, type GiftCard } from '@/app/api/giftCards';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { RecordAvatar } from '@/app/components/RecordCard';
import { useClubMoney } from '@/app/hooks/useClubMoney';

const TIER_BADGE: Record<MemberTier, string> = {
  Bronze: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  Silver: 'bg-slate-100 text-slate-700 ring-1 ring-slate-200',
  Gold: 'bg-yellow-50 text-yellow-800 ring-1 ring-yellow-200',
  Platinum: 'bg-indigo-50 text-indigo-800 ring-1 ring-indigo-200',
};

const STATUS_BADGE: Record<MemberStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Pending: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Lapsed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Cancelled: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

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
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

export default function MemberDetail() {
  const { format: formatCurrency } = useClubMoney();
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [member, setMember] = useState<Member | null>(null);
  const [allMembers, setAllMembers] = useState<Member[]>([]);
  const [charges, setCharges] = useState<Charge[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [visits, setVisits] = useState<Visit[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [ledger, setLedger] = useState<LoyaltyEntry[]>([]);
  const [comms, setComms] = useState<Communication[]>([]);
  const [giftCards, setGiftCards] = useState<GiftCard[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [deleting, setDeleting] = useState(false);

  const load = async () => {
    try {
      const [m, c, p, v, r, l, comm, g] = await Promise.all([
        getMembers(),
        getCharges(),
        getPayments(),
        getVisits(),
        getReservations(),
        getLoyaltyLedger(),
        getCommunications(),
        getGiftCards(),
      ]);
      setAllMembers(m);
      const found = m.find((x) => x.id === id) ?? null;
      setMember(found);
      setCharges(c);
      setPayments(p);
      setVisits(v);
      setReservations(r);
      setLedger(l);
      setComms(comm);
      setGiftCards(g);
    } catch {
      toast.error('Failed to load member');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [id]);

  const memberCharges = useMemo(
    () => charges.filter((c) => c.memberId === id),
    [charges, id],
  );
  const memberPayments = useMemo(
    () => payments.filter((p) => p.memberId === id),
    [payments, id],
  );
  const memberVisits = useMemo(() => visits.filter((v) => v.memberId === id), [visits, id]);
  const memberReservations = useMemo(
    () => reservations.filter((r) => r.memberEmail === member?.email),
    [reservations, member?.email],
  );
  const memberLedger = useMemo(() => ledger.filter((l) => l.memberId === id), [ledger, id]);
  const memberComms = useMemo(() => comms.filter((c) => c.memberId === id), [comms, id]);
  const memberGiftCards = useMemo(
    () => giftCards.filter((g) => g.purchaserMemberId === id),
    [giftCards, id],
  );

  const loyaltyBalance = useMemo(
    () => memberLedger.reduce((s, e) => s + e.points, 0),
    [memberLedger],
  );

  const outstandingBalance = useMemo(() => {
    return memberCharges.reduce((s, c) => {
      if (c.status === 'Voided' || c.status === 'Paid') return s;
      return s + (c.amount - c.paidAmount);
    }, 0);
  }, [memberCharges]);

  const lifetimeSpend = useMemo(
    () => memberCharges.filter((c) => c.status !== 'Voided').reduce((s, c) => s + c.paidAmount, 0),
    [memberCharges],
  );

  const upcomingReservations = useMemo(
    () =>
      memberReservations.filter(
        (r) => Date.parse(r.startAtUtc) > Date.now() && r.status !== 'Cancelled',
      ),
    [memberReservations],
  );

  const remove = async () => {
    if (!member) return;
    if (!confirm(`Remove ${member.firstName} ${member.lastName} from members?`)) return;
    setDeleting(true);
    try {
      const ok = await deleteMember(member.id);
      if (ok) {
        toast.success('Member removed');
        navigate('/members');
      }
    } finally {
      setDeleting(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <RecordSkeleton />
        </main>
      </div>
    );
  }


  if (!member) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <main className="px-[var(--page-padding)] py-12">
          <div className="max-w-md mx-auto text-center bg-white rounded-2xl border border-slate-200 p-10 shadow-sm">
            <AlertCircle className="w-10 h-10 text-rose-500 mx-auto mb-3" />
            <h1 className="text-lg font-semibold text-slate-900">Member not found</h1>
            <p className="text-sm text-slate-500 mt-1">
              They may have been removed, or the link is stale.
            </p>
            <Button asChild className="mt-5">
              <Link to="/members">Back to members</Link>
            </Button>
          </div>
        </main>
      </div>
    );
  }

  const renewsAt = Date.parse(member.renewsAtUtc);
  const overdue = renewsAt < Date.now() && member.status === 'Active';
  const renewSoon = renewsAt - Date.now() < 30 * 86_400_000 && renewsAt > Date.now();

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
            icon={Crown}
            avatar={<RecordAvatar name={`${member.firstName} ${member.lastName}`} tone="indigo" />}
            tone="indigo"
            back={{ to: '/members', label: 'All members' }}
            title={`${member.firstName} ${member.lastName}`}
            badges={
              <>
                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${TIER_BADGE[member.tier]}`}>{member.tier}</span>
                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[member.status]}`}>{member.status}</span>
                {overdue && (
                  <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-rose-50 text-rose-700 ring-1 ring-rose-200">Renewal overdue</span>
                )}
                {renewSoon && (
                  <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 ring-1 ring-amber-200">Renews soon</span>
                )}
              </>
            }
            subtitle={
              <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
                <span className="inline-flex items-center gap-1"><Mail className="w-3.5 h-3.5" aria-hidden /> {member.email}</span>
                {member.phone && <span className="inline-flex items-center gap-1"><Phone className="w-3.5 h-3.5" aria-hidden /> {member.phone}</span>}
                <span className="inline-flex items-center gap-1"><CalendarClock className="w-3.5 h-3.5" aria-hidden /> Member since {formatDate(member.joinedAtUtc)}</span>
              </span>
            }
            actions={
              <>
                <Button variant="outline" asChild className={heroSecondary}>
                  <Link to="/members" state={{ editMemberId: member.id }}>
                    <Pencil className="h-4 w-4" /> Edit
                  </Link>
                </Button>
                <Button
                  variant="outline"
                  onClick={remove}
                  disabled={deleting}
                  aria-label="Delete member"
                  className={`${heroSecondary} text-rose-600 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-700`}
                >
                  {deleting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                </Button>
              </>
            }
            stats={[
              { label: 'House balance', value: member.houseAccountBalance, icon: Receipt, tone: member.houseAccountBalance < 0 ? 'rose' : 'slate', format: formatCurrency, hint: outstandingBalance > 0 ? `${formatCurrency(outstandingBalance)} outstanding` : 'no open charges' },
              { label: 'Loyalty points', value: loyaltyBalance, icon: Sparkles, tone: 'amber', hint: `${memberLedger.length} ledger entries` },
              { label: 'Visits', value: memberVisits.length, icon: ScanLine, tone: 'cyan', hint: 'all-time check-ins' },
              { label: 'Lifetime spend', value: lifetimeSpend, icon: Receipt, tone: 'emerald', format: formatCurrency, hint: 'paid charges' },
            ]}
          >
            {member.notes && (
              <p className="rounded-xl border-l-4 border-indigo-300 bg-white/70 px-3 py-2 text-sm text-slate-600">{member.notes}</p>
            )}
          </PageHero>

          <Tabs defaultValue="overview">
            <TabsList className="mb-4 flex-wrap h-auto">
              <TabsTrigger value="overview">Overview</TabsTrigger>
              <TabsTrigger value="charges">
                House account ({memberCharges.length})
              </TabsTrigger>
              <TabsTrigger value="visits">Visits ({memberVisits.length})</TabsTrigger>
              <TabsTrigger value="reservations">
                Reservations ({memberReservations.length})
              </TabsTrigger>
              <TabsTrigger value="loyalty">Loyalty ({memberLedger.length})</TabsTrigger>
              <TabsTrigger value="comms">Communications ({memberComms.length})</TabsTrigger>
              <TabsTrigger value="giftcards">
                Gift cards ({memberGiftCards.length})
              </TabsTrigger>
            </TabsList>

            <TabsContent value="overview">
              <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
                <SectionCard
                  title="Dues & renewal"
                  icon={<CalendarClock className="w-4 h-4" />}
                  href="/charges"
                  hrefLabel="Open house accounts"
                >
                  <dl className="space-y-2 text-sm">
                    <DlRow label="Tier" value={member.tier} />
                    <DlRow
                      label="Dues"
                      value={`${formatCurrency(member.duesAmount)} / ${member.duesFrequency.toLowerCase()}`}
                    />
                    <DlRow label="Renewal" value={formatDate(member.renewsAtUtc)} />
                    <DlRow label="Joined" value={formatDate(member.joinedAtUtc)} />
                  </dl>
                </SectionCard>

                <SectionCard
                  title="Upcoming"
                  icon={<CalendarHeart className="w-4 h-4" />}
                  href="/reservations"
                  hrefLabel="All reservations"
                >
                  {upcomingReservations.length === 0 ? (
                    <p className="text-sm text-slate-500">No upcoming reservations.</p>
                  ) : (
                    <ul className="space-y-2 text-sm">
                      {upcomingReservations.slice(0, 4).map((r) => (
                        <li key={r.id} className="flex items-start justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-medium text-slate-900 truncate">{r.resourceName}</p>
                            <p className="text-xs text-slate-500">{formatDateTime(r.startAtUtc)}</p>
                          </div>
                          <span className="text-xs text-slate-500">Party of {r.partySize}</span>
                        </li>
                      ))}
                    </ul>
                  )}
                </SectionCard>

                <SectionCard
                  title="Recent visits"
                  icon={<ScanLine className="w-4 h-4" />}
                  href="/visits"
                  hrefLabel="All visits"
                >
                  {memberVisits.length === 0 ? (
                    <p className="text-sm text-slate-500">No visits yet.</p>
                  ) : (
                    <ul className="space-y-2 text-sm">
                      {memberVisits.slice(0, 5).map((v) => (
                        <li key={v.id} className="flex items-center justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-medium text-slate-900">{v.venue}</p>
                            <p className="text-xs text-slate-500">{formatDateTime(v.visitedAtUtc)}</p>
                          </div>
                          {v.pointsAwarded > 0 && (
                            <span className="text-xs font-semibold text-amber-700">+{v.pointsAwarded} pts</span>
                          )}
                        </li>
                      ))}
                    </ul>
                  )}
                </SectionCard>

                <SectionCard
                  title="Recent loyalty activity"
                  icon={<Sparkles className="w-4 h-4" />}
                  href="/loyalty"
                  hrefLabel="Open loyalty ledger"
                >
                  {memberLedger.length === 0 ? (
                    <p className="text-sm text-slate-500">No ledger entries yet.</p>
                  ) : (
                    <ul className="space-y-2 text-sm">
                      {memberLedger.slice(0, 5).map((e) => (
                        <li key={e.id} className="flex items-center justify-between gap-2">
                          <div className="min-w-0">
                            <p className="font-medium text-slate-900">{REASON_LABELS[e.reason]}</p>
                            <p className="text-xs text-slate-500">{formatDateTime(e.createdAtUtc)}</p>
                          </div>
                          <span
                            className={`text-xs font-semibold ${
                              e.points >= 0 ? 'text-emerald-700' : 'text-rose-600'
                            }`}
                          >
                            {e.points >= 0 ? '+' : ''}
                            {e.points.toLocaleString()}
                          </span>
                        </li>
                      ))}
                    </ul>
                  )}
                </SectionCard>
              </div>
            </TabsContent>

            <TabsContent value="charges">
              {memberCharges.length === 0 ? (
                <EmptyMini icon={<Receipt className="w-6 h-6" />} text="No charges on this account yet." />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-200">
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                        <th className="px-4 py-3">Charge</th>
                        <th className="px-4 py-3">Kind</th>
                        <th className="px-4 py-3">Status</th>
                        <th className="px-4 py-3 text-right">Amount</th>
                        <th className="px-4 py-3">Posted</th>
                      </tr>
                    </thead>
                    <tbody>
                      {memberCharges.map((c) => (
                        <tr key={c.id} className="border-b border-slate-100">
                          <td className="px-4 py-3">{c.description}</td>
                          <td className="px-4 py-3 text-slate-600">{c.kind}</td>
                          <td className="px-4 py-3">
                            <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">
                              {c.status}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-right font-semibold">{formatCurrency(c.amount)}</td>
                          <td className="px-4 py-3 text-slate-500">{formatDate(c.postedAtUtc)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                  {memberPayments.length > 0 && (
                    <div className="border-t border-slate-200 bg-slate-50 px-4 py-3">
                      <p className="text-xs font-semibold uppercase tracking-wide text-slate-500 mb-2">
                        Recent payments
                      </p>
                      <ul className="text-sm space-y-1">
                        {memberPayments.slice(0, 5).map((p) => (
                          <li key={p.id} className="flex items-center justify-between">
                            <span className="text-slate-600">
                              {p.method} {p.reference && <span className="text-slate-500">· {p.reference}</span>}
                            </span>
                            {p.status === 'Voided' ? (
                              <span className="text-slate-500 line-through" title={p.voidReason}>{formatCurrency(p.amount)} voided</span>
                            ) : (
                              <span className="font-semibold text-emerald-700">+{formatCurrency(p.amount)}</span>
                            )}
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}
                </div>
              )}
            </TabsContent>

            <TabsContent value="visits">
              {memberVisits.length === 0 ? (
                <EmptyMini icon={<ScanLine className="w-6 h-6" />} text="No check-ins recorded for this member yet." />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                  {memberVisits.map((v) => (
                    <div key={v.id} className="px-4 py-3 flex items-center justify-between">
                      <div>
                        <p className="font-medium text-slate-900">{v.venue}</p>
                        <p className="text-xs text-slate-500">{formatDateTime(v.visitedAtUtc)}</p>
                      </div>
                      {v.pointsAwarded > 0 && (
                        <span className="text-sm font-semibold text-amber-700">
                          +{v.pointsAwarded} pts
                        </span>
                      )}
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>

            <TabsContent value="reservations">
              {memberReservations.length === 0 ? (
                <EmptyMini
                  icon={<CalendarClock className="w-6 h-6" />}
                  text="No reservations on file for this member."
                />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                  {memberReservations.map((r) => (
                    <div key={r.id} className="px-4 py-3 flex items-center justify-between">
                      <div>
                        <p className="font-medium text-slate-900">{r.resourceName}</p>
                        <p className="text-xs text-slate-500">
                          {formatDateTime(r.startAtUtc)} · party of {r.partySize}
                        </p>
                      </div>
                      <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">{r.status}</span>
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>

            <TabsContent value="loyalty">
              {memberLedger.length === 0 ? (
                <EmptyMini icon={<Sparkles className="w-6 h-6" />} text="No loyalty activity yet." />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                  {memberLedger.map((e) => (
                    <div key={e.id} className="px-4 py-3 flex items-center justify-between">
                      <div>
                        <p className="font-medium text-slate-900">{REASON_LABELS[e.reason]}</p>
                        <p className="text-xs text-slate-500">
                          {e.note ? e.note + ' · ' : ''}
                          {formatDateTime(e.createdAtUtc)}
                        </p>
                      </div>
                      <span
                        className={`font-semibold ${
                          e.points >= 0 ? 'text-emerald-700' : 'text-rose-600'
                        }`}
                      >
                        {e.points >= 0 ? '+' : ''}
                        {e.points.toLocaleString()}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>

            <TabsContent value="comms">
              {memberComms.length === 0 ? (
                <EmptyMini
                  icon={<MessageSquareText className="w-6 h-6" />}
                  text="No messages logged for this member yet."
                />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                  {memberComms.map((c) => (
                    <div key={c.id} className="px-4 py-3">
                      <div className="flex items-start justify-between gap-2">
                        <p className="font-medium text-slate-900">{c.subject}</p>
                        <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 shrink-0">
                          {commStatusLabel(c.status)}
                        </span>
                      </div>
                      <p className="text-sm text-slate-500 mt-1 line-clamp-2">{c.preview}</p>
                      <p className="text-xs text-slate-500 mt-1">
                        {c.channel} · {c.category} · {formatDateTime(c.sentAtUtc)}
                      </p>
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>

            <TabsContent value="giftcards">
              {memberGiftCards.length === 0 ? (
                <EmptyMini icon={<Gift className="w-6 h-6" />} text="This member hasn't purchased any gift cards." />
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  {memberGiftCards.map((g) => (
                    <div
                      key={g.id}
                      className="rounded-2xl border border-slate-200 bg-gradient-to-br from-rose-500 to-pink-500 p-5 text-white shadow-sm"
                    >
                      <p className="text-xs uppercase tracking-wide opacity-80 mb-1">Gift card</p>
                      <p className="text-2xl font-bold mb-3">{formatCurrency(g.remainingValue)}</p>
                      <p className="text-xs opacity-80">to {g.recipientName}</p>
                      <p className="font-mono text-xs mt-3 opacity-90">{g.code}</p>
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>
          </Tabs>

          <p className="text-xs text-slate-500 mt-8">
            {allMembers.length} members on file · viewing {member.firstName} {member.lastName}
          </p>
        </main>
      </PageTransition>
    </div>
  );
}

function SectionCard({
  title,
  icon,
  href,
  hrefLabel,
  children,
}: {
  title: string;
  icon: React.ReactNode;
  href?: string;
  hrefLabel?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-semibold text-slate-900 inline-flex items-center gap-2">
          {icon} {title}
        </h3>
        {href && (
          <Link
            to={href}
            className="text-xs text-slate-500 hover:text-slate-700 inline-flex items-center gap-0.5"
          >
            {hrefLabel ?? 'Open'} <ChevronRight className="w-3 h-3" />
          </Link>
        )}
      </div>
      {children}
    </div>
  );
}

function DlRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-2">
      <dt className="text-slate-500">{label}</dt>
      <dd className="font-medium text-slate-900">{value}</dd>
    </div>
  );
}

function EmptyMini({ icon, text }: { icon: React.ReactNode; text: string }) {
  return (
    <div className="bg-white rounded-2xl border border-dashed border-slate-200 py-12 text-center">
      <div className="mx-auto w-10 h-10 rounded-xl bg-slate-50 text-slate-500 flex items-center justify-center mb-2">
        {icon}
      </div>
      <p className="text-sm text-slate-500">{text}</p>
    </div>
  );
}

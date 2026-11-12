// House charge: the front desk's screen for clubs without a connected POS, or for what is not
// rung up on it. Staff find a member, see their account at a glance, and ring up a sale: items
// from the price catalogue with their VAT rates, where it was sold, how the member paid (on
// their house account, by card, cash or TWINT) and a reason for any discount. The server posts
// it through the ledger and gives the points by the club's settings. Checking a member in and
// opening a tab stay one click away.
//
// It used to post one charge typed in by hand, with no VAT and no way to say a bill was paid
// at the table, beside gift card and points tiles switched off because they used up value
// without paying anything. A club with Lightspeed connected has its tickets come in on their
// own (the POS tickets page); this screen says so.

import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Calculator,
  Search,
  Sparkles,
  Receipt,
  Wine,
  ScanLine,
  Crown,
  ArrowRight,
  Loader2,
  CheckCircle2,
  PlugZap,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { checkIn, VISIT_VENUES, type VisitVenue } from '@/app/api/visits';
import { openTab, TAB_VENUES, VENUE_LABELS as TAB_VENUE_LABELS, type TabVenue } from '@/app/api/tabs';
import { TENDER_LABEL } from '@/app/api/sales';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import { NONE, useClubMembers, useClubReads, useLoyaltyLedger, usePosStatus, useSales } from '@/app/hooks/queries';
import PageHero from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { SaleBuilder } from '@/app/components/pos/SaleBuilder';
import { currentDateLocale, currentNumberLocale } from '@/app/i18n/current';

// The server says why it refused (a member who has left, an item taken off sale), and the
// sentence is what staff at the till need to see.
function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/** The tiers every club starts with; a tier the club added later gets the plain ring. */
const TIER_RING: Record<string, string> = {
  Bronze: 'ring-amber-400',
  Silver: 'ring-slate-400',
  Gold: 'ring-yellow-400',
  Platinum: 'ring-indigo-500',
};
const ringOf = (tier: string) => TIER_RING[tier] ?? 'ring-cyan-300';

function formatWhen(iso: string): string {
  try {
    return new Date(iso).toLocaleString(currentDateLocale(), { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export default function PosTerminal() {
  const { format: formatCurrency } = useClubMoney();
  // Read through the app's cache: a failed read says so with a way to try again, rather
  // than looking like a club with no members; a sale posted here refreshes the balance.
  const membersQuery = useClubMembers();
  const loyaltyQuery = useLoyaltyLedger();
  const reads = useClubReads(membersQuery, loyaltyQuery);
  const members = membersQuery.data ?? NONE;
  const ledger = loyaltyQuery.data ?? NONE;
  const pos = usePosStatus().data;
  const pointsByMember = useMemo(() => {
    const points = new Map<string, number>();
    for (const e of ledger) points.set(e.memberId, (points.get(e.memberId) ?? 0) + e.points);
    return points;
  }, [ledger]);
  const [search, setSearch] = useState('');
  // The member picked, by id: their balance on screen is the one last read, not the one
  // they had when they were picked.
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const selected = members.find((m) => m.id === selectedId) ?? null;
  const salesQuery = useSales(selectedId);
  const recentSales = (salesQuery.data ?? NONE).slice(0, 5);
  const loading = reads.loading;
  usePageLoading(loading);
  const load = reads.reload;
  const [busy, setBusy] = useState(false);
  const [actionDialog, setActionDialog] = useState<null | 'visit' | 'tab'>(null);

  const [visitForm, setVisitForm] = useState({ venue: 'Dining' as VisitVenue, guests: '0' });
  const [tabForm, setTabForm] = useState({ venue: 'Bar' as TabVenue });

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return members.slice(0, 8);
    return members
      .filter((m) =>
        `${m.firstName} ${m.lastName} ${m.email} ${m.phone ?? ''}`.toLowerCase().includes(q),
      )
      .slice(0, 12);
  }, [members, search]);

  const memberPoints = selected ? pointsByMember.get(selected.id) ?? 0 : 0;

  const recordVisit = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      const v = await checkIn({
        memberId: selected.id,
        venue: visitForm.venue,
        guestCount: Number(visitForm.guests) || 0,
        awardPoints: true,
      });
      if (v) toast.success(`Checked in · +${v.pointsAwarded} loyalty points`);
      setActionDialog(null);
      await load();
    } catch (err) {
      toast.error(errorText(err, 'Could not check the member in'));
    } finally {
      setBusy(false);
    }
  };

  const startTab = async () => {
    if (!selected) return;
    setBusy(true);
    try {
      await openTab({ memberId: selected.id, venue: tabForm.venue });
      toast.success(`Tab opened at ${TAB_VENUE_LABELS[tabForm.venue]}`);
      setActionDialog(null);
    } catch (err) {
      toast.error(errorText(err, 'Could not open the tab'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          {/* A staff terminal: no agent suggestions between the header and the till. */}
          <PageHero
            icon={Calculator}
            tone="cyan"
            title="House charge"
            subtitle="Find a member and ring up what they had: items from the catalogue, paid on their house account, by card, cash or TWINT."
            noAgentSlot
          />

          {pos?.connected && (
            <div className="mb-4 flex flex-wrap items-center gap-3 rounded-2xl border border-cyan-200 bg-cyan-50 px-4 py-3 text-sm text-cyan-900">
              <PlugZap className="w-4 h-4 shrink-0" />
              <span className="flex-1 min-w-0">
                {pos.importing
                  ? `Lightspeed's tickets${pos.locationName ? ` from ${pos.locationName}` : ''} come in on their own. Ring up here only what is not rung up on the POS.`
                  : 'Lightspeed is connected, but its tickets are not being imported yet.'}
                {pos.needsAttention > 0 && ` ${pos.needsAttention} ticket${pos.needsAttention === 1 ? '' : 's'} wait for staff.`}
              </span>
              <Link to="/pos-tickets" className="inline-flex items-center gap-1 font-medium hover:underline">
                POS tickets <ArrowRight className="w-3 h-3" />
              </Link>
            </div>
          )}

          <ClubReadState reads={reads} what="Members">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Search */}
            <StaggerItem as="section" index={0} className="lg:col-span-1 bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
              <div className="relative mb-3">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                <Input
                  autoFocus
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search by name, email, phone..."
                  className="pl-9 h-11"
                />
              </div>
              {loading ? (
                <div className="text-center py-10 text-slate-500">
                  <Loader2 className="w-5 h-5 animate-spin inline" />
                </div>
              ) : (
                <ul className="space-y-1.5 max-h-[500px] overflow-auto">
                  {filtered.map((m) => {
                    const active = selected?.id === m.id;
                    return (
                      <li key={m.id}>
                        <button
                          type="button"
                          onClick={() => setSelectedId(m.id)}
                          className={`w-full text-left rounded-xl p-3 transition flex items-center gap-3 ${
                            active
                              ? 'bg-cyan-50 ring-2 ring-cyan-400'
                              : 'hover:bg-slate-50'
                          }`}
                        >
                          <div
                            className={`w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-emerald-100 flex items-center justify-center text-cyan-800 font-semibold text-sm shrink-0 ring-2 ring-offset-2 ring-offset-white ${ringOf(m.tier)}`}
                          >
                            {(m.firstName[0] ?? '') + (m.lastName[0] ?? '')}
                          </div>
                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-medium text-slate-900 truncate">
                              {m.firstName} {m.lastName}
                            </p>
                            <p className="text-xs text-slate-500 truncate">
                              {m.tier} · {m.email}
                            </p>
                          </div>
                          {active && <CheckCircle2 className="w-4 h-4 text-cyan-600" />}
                        </button>
                      </li>
                    );
                  })}
                </ul>
              )}
            </StaggerItem>

            {/* Selected member, the sale, and the other actions */}
            <StaggerItem as="section" index={1} className="lg:col-span-2">
              {!selected ? (
                <div className="bg-white rounded-2xl border border-dashed border-slate-300 p-16 text-center">
                  <Search className="w-10 h-10 text-slate-300 mx-auto mb-3" />
                  <p className="text-slate-500">Search and select a member to begin.</p>
                </div>
              ) : (
                <>
                  {/* Card */}
                  <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 mb-4">
                    <div className="flex items-start gap-4">
                      <div
                        className={`w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-emerald-500 flex items-center justify-center text-white text-2xl font-bold shrink-0 ring-4 ring-offset-2 ring-offset-white ${ringOf(selected.tier)}`}
                      >
                        {(selected.firstName[0] ?? '') + (selected.lastName[0] ?? '')}
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex items-center gap-2 mb-1">
                          <h2 className="text-xl font-bold text-slate-900">
                            {selected.firstName} {selected.lastName}
                          </h2>
                          <span className="text-xs px-2 py-0.5 rounded-full bg-indigo-50 text-indigo-700 font-medium">
                            {selected.tier}
                          </span>
                          <span className="text-xs px-2 py-0.5 rounded-full bg-slate-100 text-slate-600 font-medium">
                            {selected.status}
                          </span>
                        </div>
                        <p className="text-sm text-slate-500">{selected.email}</p>
                      </div>
                      <Link
                        to={`/members/${selected.id}`}
                        className="inline-flex items-center gap-1 text-xs text-slate-500 hover:text-slate-700"
                      >
                        Full profile <ArrowRight className="w-3 h-3" />
                      </Link>
                    </div>

                    <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mt-5 pt-4 border-t border-slate-100">
                      <Mini
                        icon={<Receipt className="w-4 h-4 text-rose-500" />}
                        label="House"
                        value={formatCurrency(selected.houseAccountBalance)}
                        tone={selected.houseAccountBalance < 0 ? 'rose' : 'slate'}
                      />
                      <Mini
                        icon={<Sparkles className="w-4 h-4 text-amber-500" />}
                        label="Points"
                        value={memberPoints.toLocaleString(currentNumberLocale())}
                        tone="amber"
                      />
                      <Mini
                        icon={<Crown className="w-4 h-4 text-indigo-500" />}
                        label="Dues"
                        value={`${formatCurrency(selected.duesAmount)}/${selected.duesFrequency.toLowerCase()}`}
                        tone="slate"
                      />
                    </div>
                  </div>

                  <SaleBuilder key={selected.id} member={selected} />

                  {/* The other things the desk does for a member */}
                  <div className="grid grid-cols-2 md:grid-cols-3 gap-3 mb-4">
                    <WriteOnly permission={PERMISSION.FrontDesk}>
                      <ActionTile
                        icon={<ScanLine className="w-5 h-5" />}
                        title="Check in"
                        desc="Log a visit + award tier-multiplied points"
                        onClick={() => {
                          setVisitForm({ venue: 'Dining', guests: '0' });
                          setActionDialog('visit');
                        }}
                        color="cyan"
                      />
                    </WriteOnly>
                    <WriteOnly permission={PERMISSION.FrontDesk}>
                      <ActionTile
                        icon={<Wine className="w-5 h-5" />}
                        title="Open tab"
                        desc="Start a running check at a venue"
                        onClick={() => {
                          setTabForm({ venue: 'Bar' });
                          setActionDialog('tab');
                        }}
                        color="amber"
                      />
                    </WriteOnly>
                    <ActionTile
                      icon={<Crown className="w-5 h-5" />}
                      title="View profile"
                      desc="Open the full member record"
                      onClick={() => undefined}
                      color="slate"
                      href={`/members/${selected.id}`}
                    />
                  </div>

                  {recentSales.length > 0 && (
                    <section aria-label="Recent sales" className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
                      <h3 className="text-sm font-semibold text-slate-900 mb-2">Recent sales</h3>
                      <ul className="divide-y divide-slate-100">
                        {recentSales.map((sale) => (
                          <li key={sale.id} className="py-2 flex items-center gap-3 text-sm">
                            <span className="text-slate-500 w-28 shrink-0">{formatWhen(sale.soldAtUtc)}</span>
                            <span className="flex-1 min-w-0 truncate text-slate-800">
                              {sale.lines.map((l) => (l.quantity === 1 ? l.name : `${l.quantity} × ${l.name}`)).join(', ')}
                              {sale.source !== 'desk' && <span className="ml-1 text-xs text-slate-500">(POS)</span>}
                            </span>
                            <span className="text-xs text-slate-500">{TENDER_LABEL[sale.tender]}</span>
                            <span className="font-medium text-slate-900">{formatCurrency(sale.total)}</span>
                            {sale.pointsAwarded > 0 && <span className="text-xs text-amber-700">+{sale.pointsAwarded.toLocaleString(currentNumberLocale())} pts</span>}
                          </li>
                        ))}
                      </ul>
                    </section>
                  )}
                </>
              )}
            </StaggerItem>
          </div>
          </ClubReadState>
        </main>
      </PageTransition>

      {/* Visit */}
      <Dialog open={actionDialog === 'visit'} onOpenChange={(o) => !o && setActionDialog(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Check in</DialogTitle>
            <DialogDescription>Picks a venue and awards loyalty points.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Venue</Label>
              <Select
                value={visitForm.venue}
                onValueChange={(v) => setVisitForm({ ...visitForm, venue: v as VisitVenue })}
              >
                <SelectTrigger aria-label="Venue">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {VISIT_VENUES.map((v) => (
                    <SelectItem key={v} value={v}>
                      {v}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Guests</Label>
              <Input aria-label="Guests"
                type="number"
                min="0"
                value={visitForm.guests}
                onChange={(e) => setVisitForm({ ...visitForm, guests: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <WriteOnly permission={PERMISSION.FrontDesk}>
              <Button onClick={recordVisit} disabled={busy}>
                {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Check in'}
              </Button>
            </WriteOnly>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Tab */}
      <Dialog open={actionDialog === 'tab'} onOpenChange={(o) => !o && setActionDialog(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Open tab</DialogTitle>
            <DialogDescription>Start a running check at a venue.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Venue</Label>
              <Select
                value={tabForm.venue}
                onValueChange={(v) => setTabForm({ venue: v as TabVenue })}
              >
                <SelectTrigger aria-label="Venue">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {TAB_VENUES.map((v) => (
                    <SelectItem key={v} value={v}>
                      {TAB_VENUE_LABELS[v]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <WriteOnly permission={PERMISSION.FrontDesk}>
              <Button onClick={startTab} disabled={busy}>
                {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Open tab'}
              </Button>
            </WriteOnly>
          </DialogFooter>
        </DialogContent>
      </Dialog>

    </div>
  );
}

function Mini({
  icon,
  label,
  value,
  tone,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  tone: 'rose' | 'amber' | 'slate';
}) {
  const c = tone === 'rose' ? 'text-rose-600' : tone === 'amber' ? 'text-amber-700' : 'text-slate-900';
  return (
    <div className="rounded-lg bg-slate-50 p-3">
      <div className="flex items-center gap-1.5 text-xs text-slate-500 uppercase tracking-wide">
        {icon} {label}
      </div>
      <p className={`text-lg font-bold mt-0.5 ${c}`}>{value}</p>
    </div>
  );
}

const COLOR: Record<string, string> = {
  cyan: 'from-cyan-500 to-blue-500 text-cyan-600',
  amber: 'from-amber-500 to-orange-500 text-amber-700',
  slate: 'from-slate-500 to-slate-700 text-slate-600',
};

function ActionTile({
  icon,
  title,
  desc,
  onClick,
  color,
  href,
}: {
  icon: React.ReactNode;
  title: string;
  desc: string;
  onClick: () => void;
  color: string;
  href?: string;
}) {
  const cls =
    `group rounded-2xl border border-slate-200 bg-white p-4 text-left shadow-sm hover:border-slate-300 ${liftOnHover}`;
  const colorClass = COLOR[color] ?? COLOR.slate ?? 'from-slate-500 to-slate-700 text-slate-600';
  const content = (
    <>
      <div
        className={`w-10 h-10 rounded-xl bg-gradient-to-br ${colorClass.split(' ').slice(0, 2).join(' ')} text-white flex items-center justify-center mb-3`}
      >
        {icon}
      </div>
      <p className="font-semibold text-slate-900">{title}</p>
      <p className="text-xs text-slate-500 mt-0.5">{desc}</p>
    </>
  );
  if (href) {
    return (
      <Link to={href} className={cls}>
        {content}
      </Link>
    );
  }
  return (
    <button type="button" onClick={onClick} className={cls}>
      {content}
    </button>
  );
}

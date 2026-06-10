// POS terminal — one-screen staff workflow for member checkout.
//
// Staff search for a member, see their account at a glance, and pick from
// the common actions: log a visit, open a tab, redeem a gift card, redeem
// loyalty points, post a charge, or issue a card.

import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  Calculator,
  Search,
  Sparkles,
  Receipt,
  Wine,
  Gift,
  ScanLine,
  Crown,
  ArrowRight,
  Loader2,
  CheckCircle2,
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
import {
  getMembers,
  type Member,
  type MemberTier,
} from '@/app/api/members';
import { getLoyaltyLedger, addLoyaltyEntry, POINTS_PER_DOLLAR } from '@/app/api/loyalty';
import { checkIn, VISIT_VENUES, type VisitVenue } from '@/app/api/visits';
import { openTab, TAB_VENUES, VENUE_LABELS as TAB_VENUE_LABELS, type TabVenue } from '@/app/api/tabs';
import { createCharge, CHARGE_KINDS, type ChargeKind } from '@/app/api/charges';
import { getGiftCards, redeemGiftCard, type GiftCard } from '@/app/api/giftCards';

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

const TIER_RING: Record<MemberTier, string> = {
  Bronze: 'ring-amber-400',
  Silver: 'ring-slate-400',
  Gold: 'ring-yellow-400',
  Platinum: 'ring-indigo-500',
};

export default function PosTerminal() {
  const [members, setMembers] = useState<Member[]>([]);
  const [giftCards, setGiftCards] = useState<GiftCard[]>([]);
  const [pointsByMember, setPointsByMember] = useState<Map<string, number>>(new Map());
  const [search, setSearch] = useState('');
  const [selected, setSelected] = useState<Member | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [actionDialog, setActionDialog] = useState<
    null | 'visit' | 'tab' | 'charge' | 'redeemGiftCard' | 'redeemPoints'
  >(null);

  const [visitForm, setVisitForm] = useState({ venue: 'Dining' as VisitVenue, guests: '0' });
  const [tabForm, setTabForm] = useState({ venue: 'Bar' as TabVenue });
  const [chargeForm, setChargeForm] = useState({
    kind: 'Dining' as ChargeKind,
    description: '',
    amount: '',
  });
  const [gcForm, setGcForm] = useState({ code: '', amount: '' });
  const [redeemForm, setRedeemForm] = useState({ points: '' });

  const load = async () => {
    try {
      const [m, l, g] = await Promise.all([getMembers(), getLoyaltyLedger(), getGiftCards()]);
      setMembers(m);
      setGiftCards(g);
      const points = new Map<string, number>();
      for (const e of l) {
        points.set(e.memberId, (points.get(e.memberId) ?? 0) + e.points);
      }
      setPointsByMember(points);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

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
    } finally {
      setBusy(false);
    }
  };

  const postCharge = async () => {
    if (!selected) return;
    const amount = Number(chargeForm.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    if (!chargeForm.description.trim()) {
      toast.error('Description required');
      return;
    }
    setBusy(true);
    try {
      await createCharge({
        memberId: selected.id,
        memberName: `${selected.firstName} ${selected.lastName}`,
        kind: chargeForm.kind,
        description: chargeForm.description.trim(),
        amount,
      });
      toast.success(`Posted ${formatCurrency(amount)} to house account`);
      setActionDialog(null);
      setChargeForm({ kind: 'Dining', description: '', amount: '' });
      await load();
    } finally {
      setBusy(false);
    }
  };

  const redeemCard = async () => {
    if (!selected) return;
    const card = giftCards.find(
      (g) => g.code.toUpperCase() === gcForm.code.trim().toUpperCase() && g.status === 'Active',
    );
    if (!card) {
      toast.error('No active gift card with that code');
      return;
    }
    const amount = Number(gcForm.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount required');
      return;
    }
    setBusy(true);
    try {
      const result = await redeemGiftCard({
        giftCardId: card.id,
        amount,
        redeemedByName: `${selected.firstName} ${selected.lastName}`,
        appliedTo: 'POS terminal',
      });
      if (result) {
        toast.success(`Redeemed ${formatCurrency(result.amount)} from ${card.code}`);
        setActionDialog(null);
        setGcForm({ code: '', amount: '' });
        await load();
      } else {
        toast.error('Redemption failed');
      }
    } finally {
      setBusy(false);
    }
  };

  const redeemPoints = async () => {
    if (!selected) return;
    const points = Number(redeemForm.points);
    if (!points || points <= 0) {
      toast.error('Points required');
      return;
    }
    if (points > memberPoints) {
      toast.error(`Member only has ${memberPoints.toLocaleString()} points available`);
      return;
    }
    setBusy(true);
    try {
      await addLoyaltyEntry({
        memberId: selected.id,
        memberName: `${selected.firstName} ${selected.lastName}`,
        kind: 'Redeemed',
        reason: 'PointsRedemption',
        points: -points,
        note: `POS redemption — $${(points / POINTS_PER_DOLLAR).toFixed(2)} applied`,
      });
      toast.success(
        `Redeemed ${points.toLocaleString()} pts (${formatCurrency(points / POINTS_PER_DOLLAR)})`,
      );
      setActionDialog(null);
      setRedeemForm({ points: '' });
      await load();
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
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-cyan-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-emerald-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-emerald-500 flex items-center justify-center shadow-2xl shadow-cyan-500/30">
                <Calculator className="w-8 h-8 text-white" />
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">POS Terminal</h1>
                <p className="text-slate-400 mt-1">
                  Lookup a member, check in, redeem, or open a tab — one-screen staff flow.
                </p>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Search */}
            <section className="lg:col-span-1 bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
              <div className="relative mb-3">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                <Input
                  autoFocus
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search by name, email, phone..."
                  className="pl-9 h-11"
                />
              </div>
              {loading ? (
                <div className="text-center py-10 text-slate-400">
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
                          onClick={() => setSelected(m)}
                          className={`w-full text-left rounded-xl p-3 transition flex items-center gap-3 ${
                            active
                              ? 'bg-cyan-50 ring-2 ring-cyan-400'
                              : 'hover:bg-slate-50'
                          }`}
                        >
                          <div
                            className={`w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-100 to-emerald-100 flex items-center justify-center text-cyan-800 font-semibold text-sm shrink-0 ring-2 ring-offset-2 ring-offset-white ${TIER_RING[m.tier]}`}
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
            </section>

            {/* Selected member + actions */}
            <section className="lg:col-span-2">
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
                        className={`w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-emerald-500 flex items-center justify-center text-white text-2xl font-bold shrink-0 ring-4 ring-offset-2 ring-offset-white ${TIER_RING[selected.tier]}`}
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
                        value={memberPoints.toLocaleString()}
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

                  {/* Action grid */}
                  <div className="grid grid-cols-2 md:grid-cols-3 gap-3">
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
                    <ActionTile
                      icon={<Receipt className="w-5 h-5" />}
                      title="Post charge"
                      desc="One-time charge to house account"
                      onClick={() => {
                        setChargeForm({ kind: 'Dining', description: '', amount: '' });
                        setActionDialog('charge');
                      }}
                      color="emerald"
                    />
                    <ActionTile
                      icon={<Gift className="w-5 h-5" />}
                      title="Redeem gift card"
                      desc="Apply a gift card to a purchase"
                      onClick={() => {
                        setGcForm({ code: '', amount: '' });
                        setActionDialog('redeemGiftCard');
                      }}
                      color="rose"
                    />
                    <ActionTile
                      icon={<Sparkles className="w-5 h-5" />}
                      title="Redeem points"
                      desc={`Up to ${memberPoints.toLocaleString()} pts available`}
                      onClick={() => {
                        setRedeemForm({ points: '' });
                        setActionDialog('redeemPoints');
                      }}
                      color="violet"
                      disabled={memberPoints <= 0}
                    />
                    <ActionTile
                      icon={<Crown className="w-5 h-5" />}
                      title="View profile"
                      desc="Open the full member record"
                      onClick={() => undefined}
                      color="slate"
                      href={`/members/${selected.id}`}
                    />
                  </div>
                </>
              )}
            </section>
          </div>
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
                <SelectTrigger>
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
              <Input
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
            <Button onClick={recordVisit} disabled={busy}>
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Check in'}
            </Button>
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
                <SelectTrigger>
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
            <Button onClick={startTab} disabled={busy}>
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Open tab'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Charge */}
      <Dialog open={actionDialog === 'charge'} onOpenChange={(o) => !o && setActionDialog(null)}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Post charge</DialogTitle>
            <DialogDescription>Add a single charge to the house account.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Kind</Label>
              <Select
                value={chargeForm.kind}
                onValueChange={(v) => setChargeForm({ ...chargeForm, kind: v as ChargeKind })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {CHARGE_KINDS.map((k) => (
                    <SelectItem key={k} value={k}>
                      {k}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Description</Label>
              <Input
                value={chargeForm.description}
                onChange={(e) => setChargeForm({ ...chargeForm, description: e.target.value })}
              />
            </div>
            <div>
              <Label>Amount ($)</Label>
              <Input
                type="number"
                min="0"
                step="0.01"
                value={chargeForm.amount}
                onChange={(e) => setChargeForm({ ...chargeForm, amount: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={postCharge} disabled={busy}>
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Post charge'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Redeem gift card */}
      <Dialog
        open={actionDialog === 'redeemGiftCard'}
        onOpenChange={(o) => !o && setActionDialog(null)}
      >
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Redeem gift card</DialogTitle>
            <DialogDescription>Apply a gift card to a member's purchase.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Gift card code</Label>
              <Input
                value={gcForm.code}
                onChange={(e) => setGcForm({ ...gcForm, code: e.target.value })}
                placeholder="GIFT-XXXX-XXXX-XXXX"
                className="font-mono uppercase"
              />
            </div>
            <div>
              <Label>Amount to apply ($)</Label>
              <Input
                type="number"
                min="0"
                step="0.01"
                value={gcForm.amount}
                onChange={(e) => setGcForm({ ...gcForm, amount: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={redeemCard} disabled={busy}>
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Redeem'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Redeem points */}
      <Dialog
        open={actionDialog === 'redeemPoints'}
        onOpenChange={(o) => !o && setActionDialog(null)}
      >
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Redeem loyalty points</DialogTitle>
            <DialogDescription>
              100 points = $1. Member has {memberPoints.toLocaleString()} pts available.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Points to redeem</Label>
              <Input
                type="number"
                min="0"
                max={memberPoints}
                step="100"
                value={redeemForm.points}
                onChange={(e) => setRedeemForm({ points: e.target.value })}
              />
              {redeemForm.points && Number(redeemForm.points) > 0 && (
                <p className="text-xs text-slate-500 mt-1">
                  ≈ {formatCurrency(Number(redeemForm.points) / POINTS_PER_DOLLAR)} applied
                </p>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setActionDialog(null)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={redeemPoints} disabled={busy}>
              {busy ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Redeem points'}
            </Button>
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
  const c = tone === 'rose' ? 'text-rose-600' : tone === 'amber' ? 'text-amber-600' : 'text-slate-900';
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
  amber: 'from-amber-500 to-orange-500 text-amber-600',
  emerald: 'from-emerald-500 to-teal-500 text-emerald-600',
  rose: 'from-rose-500 to-pink-500 text-rose-600',
  violet: 'from-violet-500 to-purple-500 text-violet-600',
  slate: 'from-slate-500 to-slate-700 text-slate-600',
};

function ActionTile({
  icon,
  title,
  desc,
  onClick,
  color,
  disabled,
  href,
}: {
  icon: React.ReactNode;
  title: string;
  desc: string;
  onClick: () => void;
  color: string;
  disabled?: boolean;
  href?: string;
}) {
  const cls =
    'group rounded-2xl border border-slate-200 bg-white p-4 text-left shadow-sm hover:shadow-md transition disabled:opacity-50';
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
    <button type="button" onClick={onClick} disabled={disabled} className={cls}>
      {content}
    </button>
  );
}

import { useEffect, useMemo, useState } from 'react';
import {
  Gift,
  Plus,
  Trash2,
  Search,
  Trophy,
  TrendingUp,
  TrendingDown,
  Star,
  Loader2,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
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
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getLoyaltyLedger,
  addLoyaltyEntry,
  deleteLoyaltyEntry,
  computeLoyaltyStats,
  computeMemberBalances,
  LOYALTY_REASONS,
  REASON_LABELS,
  TIER_MULTIPLIERS,
  POINTS_PER_DOLLAR,
  type LoyaltyEntry,
  type LoyaltyEntryKind,
  type LoyaltyReason,
} from '@/app/api/loyalty';
import { getMembers, MEMBER_TIERS, type Member, type MemberTier } from '@/app/api/members';

const KIND_BADGE: Record<LoyaltyEntryKind, string> = {
  Earned: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Redeemed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Adjustment: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Expired: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
};

const TIER_GRADIENT: Record<MemberTier, string> = {
  Bronze: 'from-amber-200 to-amber-400',
  Silver: 'from-slate-200 to-slate-400',
  Gold: 'from-yellow-200 to-yellow-500',
  Platinum: 'from-indigo-300 to-indigo-500',
};

interface FormState {
  memberId: string;
  kind: LoyaltyEntryKind;
  reason: LoyaltyReason;
  points: string;
  note: string;
}

function newForm(members: Member[]): FormState {
  return {
    memberId: members[0]?.id ?? '',
    kind: 'Earned',
    reason: 'DiningPurchase',
    points: '100',
    note: '',
  };
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

function formatPoints(n: number): string {
  return n.toLocaleString();
}

export default function Loyalty() {
  const [entries, setEntries] = useState<LoyaltyEntry[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [kindFilter, setKindFilter] = useState<'All' | LoyaltyEntryKind>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<FormState>(newForm([]));
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [ledger, mems] = await Promise.all([getLoyaltyLedger(), getMembers()]);
      setEntries(ledger);
      setMembers(mems);
    } catch {
      toast.error('Failed to load loyalty ledger');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeLoyaltyStats(entries), [entries]);
  const balances = useMemo(() => computeMemberBalances(entries), [entries]);

  const filteredEntries = useMemo(() => {
    const q = search.trim().toLowerCase();
    return entries.filter((e) => {
      if (kindFilter !== 'All' && e.kind !== kindFilter) return false;
      if (!q) return true;
      return `${e.memberName} ${e.note ?? ''} ${REASON_LABELS[e.reason]}`.toLowerCase().includes(q);
    });
  }, [entries, search, kindFilter]);

  const openCreate = () => {
    setForm(newForm(members));
    setDialogOpen(true);
  };

  const save = async () => {
    const member = members.find((m) => m.id === form.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    const rawPoints = Number(form.points);
    if (!rawPoints || Number.isNaN(rawPoints)) {
      toast.error('Points must be a number');
      return;
    }
    // Sign points based on kind (negative for redemption/expiry).
    const signedPoints =
      form.kind === 'Redeemed' || form.kind === 'Expired'
        ? -Math.abs(rawPoints)
        : Math.abs(rawPoints);

    setSaving(true);
    try {
      await addLoyaltyEntry({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        kind: form.kind,
        reason: form.reason,
        points: signedPoints,
        note: form.note.trim() || undefined,
      });
      toast.success('Ledger entry added');
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to add entry');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (e: LoyaltyEntry) => {
    if (!confirm('Delete this ledger entry?')) return;
    setDeletingId(e.id);
    try {
      const ok = await deleteLoyaltyEntry(e.id);
      if (ok) {
        toast.success('Entry deleted');
        await load();
      } else {
        toast.error('Failed to delete');
      }
    } finally {
      setDeletingId(null);
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
          {/* Hero */}
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0 overflow-hidden">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-yellow-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-amber-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-yellow-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-yellow-500/30">
                    <Gift className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Loyalty</h1>
                    <p className="text-slate-400 mt-1">
                      Points, tier multipliers, redemptions, and house credit.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-yellow-500 to-amber-500 hover:from-yellow-600 hover:to-amber-600 shadow-lg shadow-yellow-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  Add entry
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<TrendingUp className="w-5 h-5 text-emerald-600" />}
              label="Total issued"
              value={formatPoints(stats.totalIssued)}
              hint="All-time earned points"
            />
            <StatCard
              icon={<TrendingDown className="w-5 h-5 text-rose-600" />}
              label="Total redeemed"
              value={formatPoints(stats.totalRedeemed)}
              hint="All-time spent points"
            />
            <StatCard
              icon={<Trophy className="w-5 h-5 text-yellow-600" />}
              label="Outstanding"
              value={formatPoints(stats.outstanding)}
              hint={`≈ $${stats.outstandingDollarValue.toLocaleString()} in liability`}
            />
            <StatCard
              icon={<Star className="w-5 h-5 text-violet-600" />}
              label="Active members"
              value={String(stats.activeMembers)}
              hint="Earning or redeeming"
            />
          </div>

          {/* Tier overview */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-8">
            {MEMBER_TIERS.map((t) => (
              <div key={t} className="bg-white rounded-2xl border border-slate-200 p-4 shadow-sm">
                <div className={`h-2 -mx-4 -mt-4 mb-3 bg-gradient-to-r ${TIER_GRADIENT[t]}`} />
                <p className="text-sm text-slate-500">{t}</p>
                <p className="text-xl font-bold text-slate-900 mt-1">{TIER_MULTIPLIERS[t]}×</p>
                <p className="text-xs text-slate-400 mt-1">points multiplier</p>
              </div>
            ))}
          </div>

          <Tabs defaultValue="ledger" className="w-full">
            <TabsList className="mb-4">
              <TabsTrigger value="ledger">Ledger</TabsTrigger>
              <TabsTrigger value="balances">Balances by member</TabsTrigger>
              <TabsTrigger value="rules">How it works</TabsTrigger>
            </TabsList>

            <TabsContent value="ledger">
              <div className="flex flex-col md:flex-row gap-3 mb-6">
                <div className="relative flex-1 max-w-md">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                  <Input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Search by member, reason, note..."
                    className="pl-9 h-11 rounded-xl"
                  />
                </div>
                <Select
                  value={kindFilter}
                  onValueChange={(v) => setKindFilter(v as 'All' | LoyaltyEntryKind)}
                >
                  <SelectTrigger className="h-11 rounded-xl md:w-44">
                    <SelectValue placeholder="Kind" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="All">All entries</SelectItem>
                    <SelectItem value="Earned">Earned</SelectItem>
                    <SelectItem value="Redeemed">Redeemed</SelectItem>
                    <SelectItem value="Adjustment">Adjustment</SelectItem>
                    <SelectItem value="Expired">Expired</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              {loading ? (
                <div className="flex items-center justify-center py-20 text-slate-400">
                  <Loader2 className="h-6 w-6 animate-spin" />
                </div>
              ) : filteredEntries.length === 0 ? (
                <EmptyState
                  icon={Gift}
                  title={entries.length === 0 ? 'No ledger entries yet' : 'No entries match these filters'}
                  description={
                    entries.length === 0
                      ? 'Add a points entry to start tracking member loyalty.'
                      : 'Try clearing filters or adjusting your search.'
                  }
                  actionLabel={entries.length === 0 ? 'Add entry' : undefined}
                  onAction={entries.length === 0 ? openCreate : undefined}
                  variant="orange"
                />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-200">
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                        <th className="px-4 py-3 font-medium">Member</th>
                        <th className="px-4 py-3 font-medium">Reason</th>
                        <th className="px-4 py-3 font-medium">Kind</th>
                        <th className="px-4 py-3 font-medium text-right">Points</th>
                        <th className="px-4 py-3 font-medium">When</th>
                        <th className="px-4 py-3 font-medium" aria-label="actions" />
                      </tr>
                    </thead>
                    <tbody>
                      {filteredEntries.map((e) => (
                        <tr key={e.id} className="border-b border-slate-100 hover:bg-slate-50">
                          <td className="px-4 py-3">
                            <p className="font-medium text-slate-900">{e.memberName}</p>
                            {e.note && <p className="text-xs text-slate-500 mt-0.5">{e.note}</p>}
                          </td>
                          <td className="px-4 py-3 text-slate-600">{REASON_LABELS[e.reason]}</td>
                          <td className="px-4 py-3">
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${KIND_BADGE[e.kind]}`}>
                              {e.kind}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-right font-semibold">
                            <span className={e.points >= 0 ? 'text-emerald-600' : 'text-rose-600'}>
                              {e.points >= 0 ? '+' : ''}
                              {formatPoints(e.points)}
                            </span>
                          </td>
                          <td className="px-4 py-3 text-slate-500 whitespace-nowrap">{formatDateTime(e.createdAtUtc)}</td>
                          <td className="px-4 py-3 text-right">
                            <button
                              type="button"
                              onClick={() => remove(e)}
                              disabled={deletingId === e.id}
                              className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                              aria-label="Delete entry"
                            >
                              {deletingId === e.id ? (
                                <Loader2 className="h-4 w-4 animate-spin" />
                              ) : (
                                <Trash2 className="h-4 w-4" />
                              )}
                            </button>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </TabsContent>

            <TabsContent value="balances">
              {balances.length === 0 ? (
                <EmptyState
                  icon={Trophy}
                  title="No balances yet"
                  description="Add ledger entries to see per-member point balances."
                />
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                  {balances.map((b, idx) => (
                    <div
                      key={b.memberId}
                      className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm"
                    >
                      <div className="flex items-center justify-between mb-3">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-yellow-100 to-amber-100 flex items-center justify-center text-yellow-700 font-bold">
                            #{idx + 1}
                          </div>
                          <div>
                            <p className="font-semibold text-slate-900">{b.memberName}</p>
                            <p className="text-xs text-slate-500">{b.entries} ledger entries</p>
                          </div>
                        </div>
                      </div>
                      <div className="flex items-baseline justify-between">
                        <span
                          className={`text-2xl font-bold ${
                            b.points < 0 ? 'text-rose-600' : 'text-slate-900'
                          }`}
                        >
                          {formatPoints(b.points)} pts
                        </span>
                        <span className="text-sm text-slate-500">
                          ≈ ${(b.points / POINTS_PER_DOLLAR).toFixed(2)}
                        </span>
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </TabsContent>

            <TabsContent value="rules">
              <div className="bg-white rounded-2xl border border-slate-200 p-6 shadow-sm space-y-4 max-w-2xl">
                <div>
                  <h3 className="font-semibold text-slate-900 mb-1">Earning rate</h3>
                  <p className="text-sm text-slate-600">
                    Members earn <span className="font-medium">10 base points per $1 spent</span>, multiplied by
                    their tier. Bronze 1×, Silver 1.25×, Gold 1.5×, Platinum 2×.
                  </p>
                </div>
                <div>
                  <h3 className="font-semibold text-slate-900 mb-1">Redemption rate</h3>
                  <p className="text-sm text-slate-600">
                    <span className="font-medium">{POINTS_PER_DOLLAR} points = $1</span> in redemption value.
                    Members can apply points to bar tabs, dining bills, event tickets, or convert to a gift card.
                  </p>
                </div>
                <div>
                  <h3 className="font-semibold text-slate-900 mb-1">Touchpoints that earn</h3>
                  <ul className="text-sm text-slate-600 list-disc list-inside space-y-1">
                    <li>Visits and check-ins</li>
                    <li>Dining and bar purchases (auto-applied via POS in production)</li>
                    <li>Event attendance</li>
                    <li>Member referrals — 1,000 bonus points on approved application</li>
                    <li>Membership anniversary — 500 bonus points per year</li>
                  </ul>
                </div>
              </div>
            </TabsContent>
          </Tabs>
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add ledger entry</DialogTitle>
            <DialogDescription>
              Award or redeem points for a member. Redemptions are recorded as negative balances.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.tier}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Kind</Label>
                <Select value={form.kind} onValueChange={(v) => setForm({ ...form, kind: v as LoyaltyEntryKind })}>
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Earned">Earned</SelectItem>
                    <SelectItem value="Redeemed">Redeemed</SelectItem>
                    <SelectItem value="Adjustment">Adjustment</SelectItem>
                    <SelectItem value="Expired">Expired</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Reason</Label>
                <Select
                  value={form.reason}
                  onValueChange={(v) => setForm({ ...form, reason: v as LoyaltyReason })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {LOYALTY_REASONS.map((r) => (
                      <SelectItem key={r} value={r}>
                        {REASON_LABELS[r]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label htmlFor="loy-points">Points</Label>
              <Input
                id="loy-points"
                type="number"
                min="0"
                value={form.points}
                onChange={(e) => setForm({ ...form, points: e.target.value })}
              />
              <p className="text-xs text-slate-500 mt-1">
                Enter a positive number. Redemption/expiry will be stored as a negative balance.
              </p>
            </div>
            <div>
              <Label htmlFor="loy-note">Note</Label>
              <Textarea
                id="loy-note"
                value={form.note}
                onChange={(e) => setForm({ ...form, note: e.target.value })}
                rows={2}
                placeholder="Optional context..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={save} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Saving
                </>
              ) : (
                'Add entry'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function StatCard({
  icon,
  label,
  value,
  hint,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <div className="flex items-center justify-between mb-2">
        <div className="w-10 h-10 rounded-xl bg-slate-50 flex items-center justify-center">{icon}</div>
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500 mt-0.5">{label}</p>
      {hint && <p className="text-xs text-slate-400 mt-1">{hint}</p>}
    </div>
  );
}

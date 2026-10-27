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
  type MemberBalance,
} from '@/app/api/loyalty';
import { getMembers, MEMBER_TIERS, type Member, type MemberTier } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

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
  usePageLoading(loading);
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

  // How this person likes the ledger and the balances shown, saved for them.
  const view = useViewPreference('loyalty', { layouts: ['table', 'list', 'grid'] });
  const balanceView = useViewPreference('loyalty.balances', { layouts: ['grid', 'list', 'table'] });

  const kindBadge = (e: LoyaltyEntry) => (
    <span className={`shrink-0 text-xs px-2 py-0.5 rounded-full font-medium ${KIND_BADGE[e.kind]}`}>{e.kind}</span>
  );
  const signedPoints = (e: LoyaltyEntry) => (
    <span className={`font-semibold ${e.points >= 0 ? 'text-emerald-600' : 'text-rose-600'}`}>
      {e.points >= 0 ? '+' : ''}
      {formatPoints(e.points)}
    </span>
  );

  /** Delete, the same in every layout. */
  const entryActions = (e: LoyaltyEntry) => (
    <button
      type="button"
      onClick={() => remove(e)}
      disabled={deletingId === e.id}
      className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
      aria-label={`Delete entry for ${e.memberName}`}
    >
      {deletingId === e.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
    </button>
  );

  const entryColumns: RecordColumn<LoyaltyEntry>[] = [
    {
      id: 'member',
      label: 'Member',
      required: true,
      cell: (e) => (
        <span className="block">
          <span className="flex items-center gap-2.5">
            <InitialsAvatar name={e.memberName} size="sm" />
            <span className="truncate">{e.memberName}</span>
          </span>
          {e.note && <span className="mt-0.5 block text-xs font-normal text-slate-500">{e.note}</span>}
        </span>
      ),
    },
    { id: 'reason', label: 'Reason', cell: (e) => REASON_LABELS[e.reason] },
    { id: 'kind', label: 'Kind', cell: kindBadge },
    { id: 'points', label: 'Points', numeric: true, cell: signedPoints },
    { id: 'when', label: 'When', cell: (e) => <span className="whitespace-nowrap">{formatDateTime(e.createdAtUtc)}</span> },
  ];

  const rankOf = new Map(balances.map((b, i) => [b.memberId, i + 1]));
  const balanceColumns: RecordColumn<MemberBalance>[] = [
    {
      id: 'member',
      label: 'Member',
      required: true,
      cell: (b) => (
        <span className="flex items-center gap-2.5">
          <span className="w-6 text-xs font-bold text-amber-700">#{rankOf.get(b.memberId)}</span>
          <InitialsAvatar name={b.memberName} size="sm" />
          <span className="truncate">{b.memberName}</span>
        </span>
      ),
    },
    { id: 'points', label: 'Points', numeric: true, cell: (b) => <span className={`font-semibold ${b.points < 0 ? 'text-rose-600' : 'text-slate-900'}`}>{formatPoints(b.points)}</span> },
    { id: 'value', label: 'Worth', numeric: true, cell: (b) => `$${(b.points / POINTS_PER_DOLLAR).toFixed(2)}` },
    { id: 'entries', label: 'Entries', numeric: true, cell: (b) => b.entries },
  ];

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
            icon={Gift}
            tone="amber"
            title="Loyalty"
            subtitle="Points, tier multipliers, redemptions, and house credit."
            actions={
              <Button onClick={openCreate} className={heroPrimary('amber')}>
                <Plus className="h-4 w-4" />
                Add entry
              </Button>
            }
            stats={[
              { label: 'Total issued', value: stats.totalIssued, icon: TrendingUp, tone: 'emerald', hint: 'all-time earned points' },
              { label: 'Total redeemed', value: stats.totalRedeemed, icon: TrendingDown, tone: 'rose', hint: 'all-time spent points' },
              { label: 'Outstanding', value: stats.outstanding, icon: Trophy, tone: 'amber', hint: `≈ $${stats.outstandingDollarValue.toLocaleString()} in liability` },
              { label: 'Active members', value: stats.activeMembers, icon: Star, tone: 'violet', hint: 'earning or redeeming' },
            ]}
          />

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
            <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
              <TabsTrigger value="ledger">Ledger</TabsTrigger>
              <TabsTrigger value="balances">Balances by member</TabsTrigger>
              <TabsTrigger value="rules">How it works</TabsTrigger>
            </TabsList>

            <TabsContent value="ledger">
              <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
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
                <ViewSwitcher view={view} columns={columnChoices(entryColumns)} className="md:ml-auto" />
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
                <ViewTransition layout={view.layout}>
                  {view.layout === 'list' ? (
                    <RecordList
                      label="Loyalty ledger"
                      items={filteredEntries}
                      view={view}
                      getKey={(e) => e.id}
                      leading={(e) => <InitialsAvatar name={e.memberName} />}
                      title={(e) => e.memberName}
                      subtitle={(e) => e.note || REASON_LABELS[e.reason]}
                      columns={entryColumns}
                      actions={entryActions}
                    />
                  ) : view.layout === 'grid' ? (
                    <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
                      {filteredEntries.map((e, index) => (
                        <StaggerItem
                          key={e.id}
                          index={index}
                          className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                        >
                          <div className="flex items-start gap-3">
                            <InitialsAvatar name={e.memberName} />
                            <div className="min-w-0 flex-1">
                              <p className="truncate font-semibold text-slate-900">{e.memberName}</p>
                              <p className="text-xs text-slate-500">{REASON_LABELS[e.reason]}</p>
                            </div>
                            <span className="shrink-0 text-lg">{signedPoints(e)}</span>
                          </div>
                          {e.note && <p className="mt-2 line-clamp-2 text-sm text-slate-500">{e.note}</p>}
                          <div className="mt-auto flex items-center justify-between gap-2 pt-3">
                            <span className="flex items-center gap-2 text-xs text-slate-500">
                              {kindBadge(e)}
                              {formatDateTime(e.createdAtUtc)}
                            </span>
                            {entryActions(e)}
                          </div>
                        </StaggerItem>
                      ))}
                    </div>
                  ) : (
                    <RecordTable
                      label="Loyalty ledger"
                      items={filteredEntries}
                      view={view}
                      getKey={(e) => e.id}
                      columns={entryColumns}
                      actions={entryActions}
                    />
                  )}
                </ViewTransition>
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
                <>
                  <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                    <p className="text-sm text-slate-500">
                      {balances.length} {balances.length === 1 ? 'member' : 'members'} with points
                    </p>
                    <ViewSwitcher view={balanceView} columns={columnChoices(balanceColumns)} />
                  </div>
                  <ViewTransition layout={balanceView.layout}>
                  {balanceView.layout === 'list' ? (
                    <RecordList
                      label="Balances by member"
                      items={balances}
                      view={balanceView}
                      getKey={(b) => b.memberId}
                      leading={(b) => <InitialsAvatar name={b.memberName} />}
                      title={(b) => `#${rankOf.get(b.memberId)} ${b.memberName}`}
                      subtitle={(b) => `${b.entries} ledger entries`}
                      columns={balanceColumns}
                    />
                  ) : balanceView.layout === 'table' ? (
                    <RecordTable
                      label="Balances by member"
                      items={balances}
                      view={balanceView}
                      getKey={(b) => b.memberId}
                      columns={balanceColumns}
                    />
                  ) : (
                <div className={`grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 ${gridDensity(balanceView, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
                  {balances.map((b, idx) => (
                    <StaggerItem
                      key={b.memberId}
                      index={idx}
                      className={`bg-white rounded-2xl border border-slate-200 shadow-sm hover:border-amber-200 ${balanceView.density === 'compact' ? 'p-4' : 'p-5'} ${liftOnHover}`}
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
                    </StaggerItem>
                  ))}
                </div>
                  )}
                  </ViewTransition>
                </>
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

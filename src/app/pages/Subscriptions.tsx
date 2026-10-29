import { useEffect, useMemo, useState } from 'react';
import {
  Repeat2,
  Plus,
  Search,
  Trash2,
  Pause,
  Play,
  Zap,
  DollarSign,
  AlertTriangle,
  Clock,
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
  getSubscriptions,
  createSubscription,
  updateSubscription,
  deleteSubscription,
  runBillingCycle,
  runDueBillingCycles,
  computeSubscriptionStats,
  SUBSCRIPTION_CADENCES,
  SUBSCRIPTION_STATUSES,
  type Subscription,
  type SubscriptionCadence,
  type SubscriptionStatus,
} from '@/app/api/subscriptions';
import { getMembers, type Member } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';

const STATUS_BADGE: Record<SubscriptionStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Paused: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Cancelled: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  PastDue: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
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

function daysUntil(iso: string): number {
  return Math.ceil((Date.parse(iso) - Date.now()) / 86_400_000);
}

export default function Subscriptions() {
  const { format: formatCurrency, currency } = useClubMoney();
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | SubscriptionStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    memberId: '',
    name: '',
    amount: '',
    cadence: 'Monthly' as SubscriptionCadence,
  });
  const [busy, setBusy] = useState(false);
  const [runningId, setRunningId] = useState<string | null>(null);
  const [runningAll, setRunningAll] = useState(false);

  const load = async () => {
    try {
      const [s, m] = await Promise.all([getSubscriptions(), getMembers()]);
      setSubscriptions(s);
      setMembers(m);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeSubscriptionStats(subscriptions), [subscriptions]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return subscriptions.filter((s) => {
      if (statusFilter !== 'All' && s.status !== statusFilter) return false;
      if (!q) return true;
      return `${s.memberName} ${s.name}`.toLowerCase().includes(q);
    });
  }, [subscriptions, search, statusFilter]);

  const openCreate = () => {
    setForm({ memberId: '', name: '', amount: '', cadence: 'Monthly' });
    setDialogOpen(true);
  };

  const submit = async () => {
    const member = members.find((m) => m.id === form.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    if (!form.name.trim()) {
      toast.error('Name is required');
      return;
    }
    const amount = Number(form.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    setBusy(true);
    try {
      await createSubscription({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        name: form.name.trim(),
        amount,
        cadence: form.cadence,
      });
      toast.success('Subscription created');
      setDialogOpen(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const togglePause = async (s: Subscription) => {
    const newStatus: SubscriptionStatus = s.status === 'Paused' ? 'Active' : 'Paused';
    await updateSubscription(s.id, { status: newStatus });
    toast.success(`Subscription ${newStatus.toLowerCase()}`);
    await load();
  };

  const cancel = async (s: Subscription) => {
    if (!confirm(`Cancel ${s.name} for ${s.memberName}?`)) return;
    await updateSubscription(s.id, { status: 'Cancelled' });
    toast.success('Cancelled');
    await load();
  };

  const remove = async (s: Subscription) => {
    if (!confirm(`Delete ${s.name} for ${s.memberName}?`)) return;
    await deleteSubscription(s.id);
    toast.success('Deleted');
    await load();
  };

  const charge = async (s: Subscription) => {
    setRunningId(s.id);
    try {
      await runBillingCycle(s.id);
      toast.success('Cycle charged');
      await load();
    } catch (err) {
      // Billed from another screen at the same moment, say: the server says so, and nothing was charged twice.
      toast.error(err instanceof Error && err.message ? err.message : 'Could not charge this cycle');
      await load();
    } finally {
      setRunningId(null);
    }
  };

  const chargeDue = async () => {
    setRunningAll(true);
    try {
      const count = await runDueBillingCycles();
      toast.success(`Ran ${count} due cycle${count === 1 ? '' : 's'}`);
      await load();
    } finally {
      setRunningAll(false);
    }
  };

  // How this person likes subscriptions shown, saved for them. A table, as it always was, until they choose.
  const view = useViewPreference('subscriptions', { layouts: ['table', 'list', 'grid'] });

  const statusBadge = (s: Subscription) => (
    <span className={`shrink-0 text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[s.status]}`}>
      {s.status === 'PastDue' ? 'Past due' : s.status}
    </span>
  );

  const nextCharge = (s: Subscription) => {
    const days = daysUntil(s.nextChargeAtUtc);
    return (
      <span className="block">
        <span className="block text-slate-700">{formatDate(s.nextChargeAtUtc)}</span>
        <span className={`block text-xs ${days < 0 ? 'text-rose-600' : 'text-slate-500'}`}>
          {days < 0 ? `${-days}d overdue` : days === 0 ? 'today' : `in ${days}d`}
        </span>
      </span>
    );
  };

  /** Bill, pause or resume, cancel and delete, the same in every layout. */
  const subscriptionActions = (s: Subscription) => (
    <div className="flex items-center justify-end gap-1">
      {/* A paused or cancelled subscription bills nothing, so there is no Bill to press. */}
      {s.status !== 'Cancelled' && s.status !== 'Paused' && (
        <Button
          size="sm"
          variant="outline"
          onClick={() => charge(s)}
          disabled={runningId === s.id}
        >
          {runningId === s.id ? (
            <Loader2 className="w-3.5 h-3.5 animate-spin" />
          ) : (
            <>
              <Zap className="w-3.5 h-3.5 mr-1" /> Bill
            </>
          )}
        </Button>
      )}
      {s.status === 'Active' && (
        <button
          type="button"
          onClick={() => togglePause(s)}
          className="rounded p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          aria-label="Pause"
        >
          <Pause className="w-4 h-4" />
        </button>
      )}
      {s.status === 'Paused' && (
        <button
          type="button"
          onClick={() => togglePause(s)}
          className="rounded p-1.5 text-slate-500 hover:bg-emerald-50 hover:text-emerald-700"
          aria-label="Resume"
        >
          <Play className="w-4 h-4" />
        </button>
      )}
      {s.status !== 'Cancelled' && (
        <button
          type="button"
          onClick={() => cancel(s)}
          className="rounded p-1.5 text-xs text-slate-500 hover:bg-rose-50 hover:text-rose-600"
        >
          Cancel
        </button>
      )}
      <button
        type="button"
        onClick={() => remove(s)}
        className="rounded p-1.5 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
        aria-label="Delete"
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </div>
  );

  const subscriptionColumns: RecordColumn<Subscription>[] = [
    {
      id: 'member',
      label: 'Member',
      required: true,
      cell: (s) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={s.memberName} size="sm" />
          <span className="truncate">{s.memberName}</span>
        </span>
      ),
    },
    {
      id: 'subscription',
      label: 'Subscription',
      cell: (s) => (
        <span className="block">
          <span className="block text-slate-700">{s.name}</span>
          {s.notes && <span className="mt-0.5 block text-xs text-slate-500">{s.notes}</span>}
        </span>
      ),
    },
    { id: 'cadence', label: 'Cadence', cell: (s) => s.cadence },
    { id: 'amount', label: 'Amount', numeric: true, cell: (s) => <span className="font-semibold text-slate-900">{formatCurrency(s.amount)}</span> },
    { id: 'next', label: 'Next charge', cell: nextCharge },
    { id: 'status', label: 'Status', cell: statusBadge },
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
            icon={Repeat2}
            tone="blue"
            title="Subscriptions"
            subtitle="Recurring dues and add-ons. Bill the cycle to drop a charge automatically."
            actions={
              <>
                <Button
                  onClick={chargeDue}
                  disabled={runningAll}
                  variant="outline"
                  className={`${heroSecondary} disabled:opacity-60`}
                >
                  {runningAll ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" /> Running
                    </>
                  ) : (
                    <>
                      <Zap className="h-4 w-4" /> Run due cycles
                    </>
                  )}
                </Button>
                <Button onClick={openCreate} className={heroPrimary('blue')}>
                  <Plus className="h-4 w-4" /> New subscription
                </Button>
              </>
            }
            stats={[
              { label: 'Active', value: stats.activeCount, icon: Repeat2, tone: 'blue', hint: `${stats.cancelledCount} cancelled` },
              { label: 'MRR', value: stats.mrr, icon: DollarSign, tone: 'emerald', hint: 'normalized recurring', format: formatCurrency },
              { label: 'Due in 14d', value: stats.dueSoonCount, icon: Clock, tone: 'amber' },
              { label: 'Past due', value: stats.pastDueCount, icon: AlertTriangle, tone: 'rose' },
            ]}
          />

          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search subscriptions..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | SubscriptionStatus)}
            >
              <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-44">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {SUBSCRIPTION_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {s}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(subscriptionColumns)} className="md:ml-auto" />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={Repeat2}
              title={subscriptions.length === 0 ? 'No subscriptions yet' : 'No subscriptions match'}
              description={
                subscriptions.length === 0
                  ? 'Create recurring billing for dues, lockers, or other monthly add-ons.'
                  : 'Try clearing filters.'
              }
              actionLabel={subscriptions.length === 0 ? 'New subscription' : undefined}
              onAction={subscriptions.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <ViewTransition layout={view.layout}>
              {view.layout === 'list' ? (
                <RecordList
                  label="Subscriptions"
                  items={filtered}
                  view={view}
                  getKey={(s) => s.id}
                  leading={(s) => <InitialsAvatar name={s.memberName} />}
                  title={(s) => s.memberName}
                  subtitle={(s) => `${s.name} · ${formatCurrency(s.amount)} ${s.cadence.toLowerCase()}`}
                  columns={subscriptionColumns}
                  actions={subscriptionActions}
                />
              ) : view.layout === 'grid' ? (
                <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
                  {filtered.map((s, index) => (
                    <StaggerItem
                      key={s.id}
                      index={index}
                      className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                    >
                      <div className="flex items-start gap-3">
                        <InitialsAvatar name={s.memberName} />
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-semibold text-slate-900">{s.memberName}</p>
                          <p className="truncate text-sm text-slate-600">{s.name}</p>
                        </div>
                        {statusBadge(s)}
                      </div>
                      <div className="mt-3 flex items-end justify-between gap-2">
                        <div>
                          <p className="text-lg font-bold text-slate-900">{formatCurrency(s.amount)}</p>
                          <p className="text-xs text-slate-500">{s.cadence}</p>
                        </div>
                        <div className="text-right text-sm">{nextCharge(s)}</div>
                      </div>
                      {s.notes && <p className="mt-2 line-clamp-2 text-xs text-slate-500">{s.notes}</p>}
                      <div className="mt-auto pt-3">{subscriptionActions(s)}</div>
                    </StaggerItem>
                  ))}
                </div>
              ) : (
                <RecordTable
                  label="Subscriptions"
                  items={filtered}
                  view={view}
                  getKey={(s) => s.id}
                  columns={subscriptionColumns}
                  actions={subscriptionActions}
                />
              )}
            </ViewTransition>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>New subscription</DialogTitle>
            <DialogDescription>Create a recurring billing schedule for a member.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger aria-label="Member">
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
            <div>
              <Label>Name</Label>
              <Input aria-label="Name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Gold dues, Locker rental"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Amount ({currency})</Label>
                <Input aria-label={`Amount (${currency})`}
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: e.target.value })}
                />
              </div>
              <div>
                <Label>Cadence</Label>
                <Select
                  value={form.cadence}
                  onValueChange={(v) => setForm({ ...form, cadence: v as SubscriptionCadence })}
                >
                  <SelectTrigger aria-label="Cadence">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {SUBSCRIPTION_CADENCES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {c}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Creating
                </>
              ) : (
                'Create subscription'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

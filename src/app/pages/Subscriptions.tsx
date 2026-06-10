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

const STATUS_BADGE: Record<SubscriptionStatus, string> = {
  Active: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Paused: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Cancelled: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  PastDue: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
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

function daysUntil(iso: string): number {
  return Math.ceil((Date.parse(iso) - Date.now()) / 86_400_000);
}

export default function Subscriptions() {
  const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-blue-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-indigo-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-blue-500 to-indigo-500 flex items-center justify-center shadow-2xl shadow-blue-500/30">
                    <Repeat2 className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Subscriptions</h1>
                    <p className="text-slate-400 mt-1">
                      Recurring dues and add-ons. Bill the cycle to drop a charge automatically.
                    </p>
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Button
                    onClick={chargeDue}
                    disabled={runningAll}
                    variant="outline"
                    className="bg-white/10 text-white border-white/20 hover:bg-white/20 hover:text-white"
                  >
                    {runningAll ? (
                      <>
                        <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Running
                      </>
                    ) : (
                      <>
                        <Zap className="w-4 h-4 mr-2" /> Run due cycles
                      </>
                    )}
                  </Button>
                  <Button
                    onClick={openCreate}
                    className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-blue-500 to-indigo-500 hover:from-blue-600 hover:to-indigo-600 shadow-lg shadow-blue-500/30 font-semibold text-white"
                  >
                    <Plus className="w-4 h-4" /> New subscription
                  </Button>
                </div>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<Repeat2 className="w-5 h-5 text-blue-600" />}
              label="Active"
              value={String(stats.activeCount)}
              hint={`${stats.cancelledCount} cancelled`}
            />
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-emerald-600" />}
              label="MRR"
              value={formatCurrency(stats.mrr)}
              hint="Normalized recurring"
            />
            <StatCard
              icon={<Clock className="w-5 h-5 text-amber-600" />}
              label="Due in 14d"
              value={String(stats.dueSoonCount)}
            />
            <StatCard
              icon={<AlertTriangle className="w-5 h-5 text-rose-600" />}
              label="Past due"
              value={String(stats.pastDueCount)}
            />
          </div>

          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
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
              <SelectTrigger className="h-11 rounded-xl md:w-44">
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
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
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
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 border-b border-slate-200">
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3">Member</th>
                    <th className="px-4 py-3">Subscription</th>
                    <th className="px-4 py-3">Cadence</th>
                    <th className="px-4 py-3 text-right">Amount</th>
                    <th className="px-4 py-3">Next charge</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((s) => {
                    const days = daysUntil(s.nextChargeAtUtc);
                    return (
                      <tr key={s.id} className="border-b border-slate-100 hover:bg-slate-50">
                        <td className="px-4 py-3 font-medium text-slate-900">{s.memberName}</td>
                        <td className="px-4 py-3">
                          <p className="text-slate-700">{s.name}</p>
                          {s.notes && <p className="text-xs text-slate-500 mt-0.5">{s.notes}</p>}
                        </td>
                        <td className="px-4 py-3 text-slate-600">{s.cadence}</td>
                        <td className="px-4 py-3 text-right font-semibold">{formatCurrency(s.amount)}</td>
                        <td className="px-4 py-3">
                          <p className="text-slate-700">{formatDate(s.nextChargeAtUtc)}</p>
                          <p className={`text-xs ${days < 0 ? 'text-rose-600' : 'text-slate-500'}`}>
                            {days < 0 ? `${-days}d overdue` : days === 0 ? 'today' : `in ${days}d`}
                          </p>
                        </td>
                        <td className="px-4 py-3">
                          <span
                            className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[s.status]}`}
                          >
                            {s.status === 'PastDue' ? 'Past due' : s.status}
                          </span>
                        </td>
                        <td className="px-4 py-3 text-right">
                          <div className="flex items-center justify-end gap-1">
                            {s.status !== 'Cancelled' && (
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
                                className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                                aria-label="Pause"
                              >
                                <Pause className="w-4 h-4" />
                              </button>
                            )}
                            {s.status === 'Paused' && (
                              <button
                                type="button"
                                onClick={() => togglePause(s)}
                                className="rounded p-1.5 text-slate-400 hover:bg-emerald-50 hover:text-emerald-700"
                                aria-label="Resume"
                              >
                                <Play className="w-4 h-4" />
                              </button>
                            )}
                            {s.status !== 'Cancelled' && (
                              <button
                                type="button"
                                onClick={() => cancel(s)}
                                className="rounded p-1.5 text-xs text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                              >
                                Cancel
                              </button>
                            )}
                            <button
                              type="button"
                              onClick={() => remove(s)}
                              className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                              aria-label="Delete"
                            >
                              <Trash2 className="w-4 h-4" />
                            </button>
                          </div>
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
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
            <div>
              <Label>Name</Label>
              <Input
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
                placeholder="e.g. Gold dues, Locker rental"
              />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Amount ($)</Label>
                <Input
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
                  <SelectTrigger>
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

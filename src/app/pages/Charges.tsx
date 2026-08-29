import { useEffect, useMemo, useState } from 'react';
import {
  Receipt,
  Plus,
  Trash2,
  Search,
  DollarSign,
  AlertTriangle,
  CheckCircle2,
  CreditCard,
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
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getCharges,
  getPayments,
  createCharge,
  recordPayment,
  deleteCharge,
  computeChargeStats,
  CHARGE_KINDS,
  CHARGE_STATUSES,
  PAYMENT_METHODS,
  PAYMENT_METHOD_LABELS,
  type Charge,
  type ChargeKind,
  type ChargeStatus,
  type Payment,
} from '@/app/api/charges';
import { getMembers, type Member } from '@/app/api/members';

const STATUS_BADGE: Record<ChargeStatus, string> = {
  Pending: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  PartiallyPaid: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Paid: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Overdue: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Voided: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

const STATUS_LABEL: Record<ChargeStatus, string> = {
  Pending: 'Pending',
  PartiallyPaid: 'Partial',
  Paid: 'Paid',
  Overdue: 'Overdue',
  Voided: 'Voided',
};

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

function formatDate(iso?: string): string {
  if (!iso) return '—';
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
    });
  } catch {
    return iso;
  }
}

export default function Charges() {
  const [charges, setCharges] = useState<Charge[]>([]);
  const [payments, setPayments] = useState<Payment[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | ChargeStatus>('All');
  const [memberFilter, setMemberFilter] = useState<string>('All');
  const [chargeDialog, setChargeDialog] = useState(false);
  const [paymentDialog, setPaymentDialog] = useState<Charge | null>(null);

  // Charge form
  const [chargeForm, setChargeForm] = useState({
    memberId: '',
    kind: 'Dining' as ChargeKind,
    description: '',
    amount: '',
    dueAtUtc: '',
  });
  const [paymentForm, setPaymentForm] = useState({
    amount: '',
    method: 'CardOnFile' as Payment['method'],
    reference: '',
  });
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [c, p, m] = await Promise.all([getCharges(), getPayments(), getMembers()]);
      setCharges(c);
      setPayments(p);
      setMembers(m);
    } catch {
      toast.error('Failed to load charges');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeChargeStats(charges), [charges]);

  const filteredCharges = useMemo(() => {
    const q = search.trim().toLowerCase();
    return charges.filter((c) => {
      if (statusFilter !== 'All' && c.status !== statusFilter) return false;
      if (memberFilter !== 'All' && c.memberId !== memberFilter) return false;
      if (!q) return true;
      return `${c.memberName} ${c.description} ${c.kind} ${c.reference ?? ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [charges, search, statusFilter, memberFilter]);

  const filteredPayments = useMemo(() => {
    if (memberFilter === 'All') return payments;
    return payments.filter((p) => p.memberId === memberFilter);
  }, [payments, memberFilter]);

  const openChargeDialog = () => {
    setChargeForm({
      memberId: members[0]?.id ?? '',
      kind: 'Dining',
      description: '',
      amount: '',
      dueAtUtc: '',
    });
    setChargeDialog(true);
  };

  const submitCharge = async () => {
    const member = members.find((m) => m.id === chargeForm.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    const amount = Number(chargeForm.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    if (!chargeForm.description.trim()) {
      toast.error('Description is required');
      return;
    }
    setSaving(true);
    try {
      await createCharge({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        kind: chargeForm.kind,
        description: chargeForm.description.trim(),
        amount,
        dueAtUtc: chargeForm.dueAtUtc ? new Date(chargeForm.dueAtUtc).toISOString() : undefined,
      });
      toast.success('Charge posted');
      setChargeDialog(false);
      await load();
    } catch {
      toast.error('Failed to post charge');
    } finally {
      setSaving(false);
    }
  };

  const openPaymentDialog = (charge: Charge) => {
    const remaining = charge.amount - charge.paidAmount;
    setPaymentForm({
      amount: remaining.toFixed(2),
      method: 'CardOnFile',
      reference: '',
    });
    setPaymentDialog(charge);
  };

  const submitPayment = async () => {
    if (!paymentDialog) return;
    const amount = Number(paymentForm.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    setSaving(true);
    try {
      await recordPayment({
        memberId: paymentDialog.memberId,
        memberName: paymentDialog.memberName,
        amount,
        method: paymentForm.method,
        reference: paymentForm.reference.trim() || undefined,
        chargeId: paymentDialog.id,
      });
      toast.success('Payment recorded');
      setPaymentDialog(null);
      await load();
    } catch {
      toast.error('Failed to record payment');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (c: Charge) => {
    if (!confirm(`Delete ${c.description}?`)) return;
    setDeletingId(c.id);
    try {
      const ok = await deleteCharge(c.id);
      if (ok) {
        toast.success('Charge deleted');
        await load();
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-emerald-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-lime-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-emerald-500 to-lime-500 flex items-center justify-center shadow-2xl shadow-emerald-500/30">
                    <Receipt className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">House Accounts</h1>
                    <p className="text-slate-400 mt-1">
                      Charges, payments, and outstanding balances across all members.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openChargeDialog}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-emerald-500 to-lime-500 hover:from-emerald-600 hover:to-lime-600 shadow-lg shadow-emerald-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  Post charge
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-amber-600" />}
              label="Outstanding"
              value={formatCurrency(stats.outstanding)}
              hint={`${stats.pendingCount} open charges`}
            />
            <StatCard
              icon={<AlertTriangle className="w-5 h-5 text-rose-600" />}
              label="Overdue"
              value={formatCurrency(stats.overdue)}
              hint="Past due date"
            />
            <StatCard
              icon={<CheckCircle2 className="w-5 h-5 text-emerald-600" />}
              label="Paid · 30d"
              value={formatCurrency(stats.paidThisMonth)}
              hint="Collected in last 30d"
            />
            <StatCard
              icon={<CreditCard className="w-5 h-5 text-indigo-600" />}
              label="Payments"
              value={String(payments.length)}
              hint="All-time recorded"
            />
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by member, description, reference..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select value={memberFilter} onValueChange={setMemberFilter}>
              <SelectTrigger className="h-11 rounded-xl md:w-56">
                <SelectValue placeholder="Member" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All members</SelectItem>
                {members.map((m) => (
                  <SelectItem key={m.id} value={m.id}>
                    {m.firstName} {m.lastName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | ChargeStatus)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {CHARGE_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {STATUS_LABEL[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <Tabs defaultValue="charges">
            <TabsList className="mb-4">
              <TabsTrigger value="charges">Charges ({filteredCharges.length})</TabsTrigger>
              <TabsTrigger value="payments">Payments ({filteredPayments.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="charges">
              {loading ? (
                <div className="flex items-center justify-center py-20 text-slate-400">
                  <Loader2 className="h-6 w-6 animate-spin" />
                </div>
              ) : filteredCharges.length === 0 ? (
                <EmptyState
                  icon={Receipt}
                  title={charges.length === 0 ? 'No charges yet' : 'No charges match these filters'}
                  description={
                    charges.length === 0
                      ? 'Post a charge to start tracking house account activity.'
                      : 'Try clearing filters or adjusting your search.'
                  }
                  actionLabel={charges.length === 0 ? 'Post charge' : undefined}
                  onAction={charges.length === 0 ? openChargeDialog : undefined}
                  variant="teal"
                />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-200">
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                        <th className="px-4 py-3 font-medium">Member</th>
                        <th className="px-4 py-3 font-medium">Charge</th>
                        <th className="px-4 py-3 font-medium">Kind</th>
                        <th className="px-4 py-3 font-medium">Status</th>
                        <th className="px-4 py-3 font-medium text-right">Amount</th>
                        <th className="px-4 py-3 font-medium">Posted</th>
                        <th className="px-4 py-3 font-medium" />
                      </tr>
                    </thead>
                    <tbody>
                      {filteredCharges.map((c) => {
                        const remaining = c.amount - c.paidAmount;
                        return (
                          <tr key={c.id} className="border-b border-slate-100 hover:bg-slate-50">
                            <td className="px-4 py-3">
                              <p className="font-medium text-slate-900">{c.memberName}</p>
                            </td>
                            <td className="px-4 py-3">
                              <p className="text-slate-700">{c.description}</p>
                              {c.reference && <p className="text-xs text-slate-400 mt-0.5">{c.reference}</p>}
                            </td>
                            <td className="px-4 py-3 text-slate-600">{c.kind}</td>
                            <td className="px-4 py-3">
                              <span
                                className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}
                              >
                                {STATUS_LABEL[c.status]}
                              </span>
                            </td>
                            <td className="px-4 py-3 text-right">
                              <p className="font-semibold text-slate-900">{formatCurrency(c.amount)}</p>
                              {c.paidAmount > 0 && c.paidAmount < c.amount && (
                                <p className="text-xs text-slate-500">
                                  {formatCurrency(remaining)} left
                                </p>
                              )}
                            </td>
                            <td className="px-4 py-3 text-slate-500 whitespace-nowrap">
                              {formatDate(c.postedAtUtc)}
                            </td>
                            <td className="px-4 py-3 text-right">
                              <div className="flex items-center justify-end gap-1">
                                {(c.status === 'Pending' ||
                                  c.status === 'PartiallyPaid' ||
                                  c.status === 'Overdue') && (
                                  <Button size="sm" variant="outline" onClick={() => openPaymentDialog(c)}>
                                    <CreditCard className="w-3.5 h-3.5 mr-1" /> Pay
                                  </Button>
                                )}
                                <button
                                  type="button"
                                  onClick={() => remove(c)}
                                  disabled={deletingId === c.id}
                                  className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                                  aria-label="Delete charge"
                                >
                                  {deletingId === c.id ? (
                                    <Loader2 className="h-4 w-4 animate-spin" />
                                  ) : (
                                    <Trash2 className="h-4 w-4" />
                                  )}
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
            </TabsContent>

            <TabsContent value="payments">
              {filteredPayments.length === 0 ? (
                <EmptyState
                  icon={CreditCard}
                  title="No payments yet"
                  description="Payments will appear here once you record them against charges."
                />
              ) : (
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-200">
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                        <th className="px-4 py-3 font-medium">Member</th>
                        <th className="px-4 py-3 font-medium">Method</th>
                        <th className="px-4 py-3 font-medium">Reference</th>
                        <th className="px-4 py-3 font-medium text-right">Amount</th>
                        <th className="px-4 py-3 font-medium">Received</th>
                      </tr>
                    </thead>
                    <tbody>
                      {filteredPayments.map((p) => (
                        <tr key={p.id} className="border-b border-slate-100 hover:bg-slate-50">
                          <td className="px-4 py-3 font-medium text-slate-900">{p.memberName}</td>
                          <td className="px-4 py-3 text-slate-600">
                            {PAYMENT_METHOD_LABELS[p.method]}
                          </td>
                          <td className="px-4 py-3 text-slate-500 font-mono text-xs">
                            {p.reference ?? '—'}
                          </td>
                          <td className="px-4 py-3 text-right font-semibold text-emerald-700">
                            +{formatCurrency(p.amount)}
                          </td>
                          <td className="px-4 py-3 text-slate-500 whitespace-nowrap">
                            {formatDate(p.receivedAtUtc)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </TabsContent>
          </Tabs>
        </main>
      </PageTransition>

      {/* Post charge */}
      <Dialog open={chargeDialog} onOpenChange={setChargeDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Post charge</DialogTitle>
            <DialogDescription>
              Add a charge to a member's house account. They'll see it on their next statement.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select
                value={chargeForm.memberId}
                onValueChange={(v) => setChargeForm({ ...chargeForm, memberId: v })}
              >
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
            <div>
              <Label>Description</Label>
              <Input
                value={chargeForm.description}
                onChange={(e) => setChargeForm({ ...chargeForm, description: e.target.value })}
                placeholder="e.g. Tasting menu × 2"
              />
            </div>
            <div>
              <Label>Due date</Label>
              <Input
                type="date"
                value={chargeForm.dueAtUtc}
                onChange={(e) => setChargeForm({ ...chargeForm, dueAtUtc: e.target.value })}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setChargeDialog(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={submitCharge} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Posting
                </>
              ) : (
                'Post charge'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Record payment */}
      <Dialog open={paymentDialog !== null} onOpenChange={(o) => !o && setPaymentDialog(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record payment</DialogTitle>
            <DialogDescription>
              {paymentDialog && (
                <>
                  Applying to <strong>{paymentDialog.description}</strong> ({paymentDialog.memberName})
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Amount ($)</Label>
              <Input
                type="number"
                min="0"
                step="0.01"
                value={paymentForm.amount}
                onChange={(e) => setPaymentForm({ ...paymentForm, amount: e.target.value })}
              />
            </div>
            <div>
              <Label>Method</Label>
              <Select
                value={paymentForm.method}
                onValueChange={(v) => setPaymentForm({ ...paymentForm, method: v as Payment['method'] })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {PAYMENT_METHODS.map((m) => (
                    <SelectItem key={m} value={m}>
                      {PAYMENT_METHOD_LABELS[m]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Reference</Label>
              <Input
                value={paymentForm.reference}
                onChange={(e) => setPaymentForm({ ...paymentForm, reference: e.target.value })}
                placeholder="auth code, check number, etc."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPaymentDialog(null)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={submitPayment} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Recording
                </>
              ) : (
                'Record payment'
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

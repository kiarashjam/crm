import { useEffect, useMemo, useState } from 'react';
import {
  HeartHandshake,
  Plus,
  Search,
  Trash2,
  CheckCircle2,
  DollarSign,
  Users,
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
import { Switch } from '@/app/components/ui/switch';
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
  getDonations,
  recordDonation,
  acknowledgeDonation,
  deleteDonation,
  computeDonationStats,
  DONATION_CAUSES,
  CAUSE_LABELS,
  type Donation,
  type DonationCause,
} from '@/app/api/donations';
import { getMembers, type Member } from '@/app/api/members';

const CAUSE_COLOR: Record<DonationCause, string> = {
  StaffSupport: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200',
  Scholarship: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  CommunityGrants: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Sustainability: 'bg-teal-50 text-teal-700 ring-1 ring-teal-200',
  CulinaryEducation: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Arts: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
  Other: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
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

export default function Donations() {
  const [donations, setDonations] = useState<Donation[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [causeFilter, setCauseFilter] = useState<'All' | DonationCause>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    memberId: '',
    cause: 'StaffSupport' as DonationCause,
    amount: '',
    anonymous: false,
    message: '',
  });
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      const [d, m] = await Promise.all([getDonations(), getMembers()]);
      setDonations(d);
      setMembers(m);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeDonationStats(donations), [donations]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return donations.filter((d) => {
      if (causeFilter !== 'All' && d.cause !== causeFilter) return false;
      if (!q) return true;
      return `${d.memberName} ${d.message ?? ''} ${CAUSE_LABELS[d.cause]}`
        .toLowerCase()
        .includes(q);
    });
  }, [donations, search, causeFilter]);

  const openCreate = () => {
    setForm({ memberId: '', cause: 'StaffSupport', amount: '', anonymous: false, message: '' });
    setDialogOpen(true);
  };

  const submit = async () => {
    const member = members.find((m) => m.id === form.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    const amount = Number(form.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    setBusy(true);
    try {
      await recordDonation({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        cause: form.cause,
        amount,
        isAnonymous: form.anonymous,
        message: form.message.trim() || undefined,
      });
      toast.success('Donation recorded');
      setDialogOpen(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const acknowledge = async (d: Donation) => {
    await acknowledgeDonation(d.id);
    toast.success('Acknowledged — thank-you logged');
    await load();
  };

  const remove = async (d: Donation) => {
    if (!confirm(`Delete this donation entry?`)) return;
    await deleteDonation(d.id);
    toast.success('Deleted');
    await load();
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-rose-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-orange-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-rose-500 to-orange-500 flex items-center justify-center shadow-2xl shadow-rose-500/30">
                    <HeartHandshake className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Donations</h1>
                    <p className="text-slate-400 mt-1">
                      Member contributions to staff support, scholarship, and community causes.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-rose-500 to-orange-500 hover:from-rose-600 hover:to-orange-600 shadow-lg shadow-rose-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" /> Record donation
                </Button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-rose-600" />}
              label="Total raised"
              value={formatCurrency(stats.totalRaised)}
            />
            <StatCard
              icon={<Users className="w-5 h-5 text-blue-600" />}
              label="Donors"
              value={String(stats.donorCount)}
            />
            <StatCard
              icon={<HeartHandshake className="w-5 h-5 text-amber-600" />}
              label="Top cause"
              value={stats.topCause ? CAUSE_LABELS[stats.topCause.cause] : '—'}
              hint={stats.topCause ? formatCurrency(stats.topCause.amount) : undefined}
            />
            <StatCard
              icon={<CheckCircle2 className="w-5 h-5 text-emerald-600" />}
              label="To acknowledge"
              value={String(stats.awaitingAcknowledgement)}
            />
          </div>

          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search donations..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={causeFilter}
              onValueChange={(v) => setCauseFilter(v as 'All' | DonationCause)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-56">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All causes</SelectItem>
                {DONATION_CAUSES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {CAUSE_LABELS[c]}
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
              icon={HeartHandshake}
              title={donations.length === 0 ? 'No donations yet' : 'No donations match'}
              description={
                donations.length === 0
                  ? 'Record member contributions to track impact and acknowledgement.'
                  : 'Try clearing filters.'
              }
              actionLabel={donations.length === 0 ? 'Record donation' : undefined}
              onAction={donations.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 border-b border-slate-200">
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3">Donor</th>
                    <th className="px-4 py-3">Cause</th>
                    <th className="px-4 py-3 text-right">Amount</th>
                    <th className="px-4 py-3">Received</th>
                    <th className="px-4 py-3">Status</th>
                    <th className="px-4 py-3" />
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((d) => (
                    <tr key={d.id} className="border-b border-slate-100 hover:bg-slate-50">
                      <td className="px-4 py-3">
                        <p className="font-medium text-slate-900">
                          {d.isAnonymous ? 'Anonymous' : d.memberName}
                        </p>
                        {!d.isAnonymous && d.memberName && (
                          <p className="text-xs text-slate-500">{d.memberName}</p>
                        )}
                        {d.message && (
                          <p className="text-xs italic text-slate-500 mt-1">"{d.message}"</p>
                        )}
                      </td>
                      <td className="px-4 py-3">
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${CAUSE_COLOR[d.cause]}`}>
                          {CAUSE_LABELS[d.cause]}
                        </span>
                      </td>
                      <td className="px-4 py-3 text-right font-semibold text-rose-700">
                        {formatCurrency(d.amount)}
                      </td>
                      <td className="px-4 py-3 text-slate-500 whitespace-nowrap">{formatDate(d.receivedAtUtc)}</td>
                      <td className="px-4 py-3">
                        {d.acknowledged ? (
                          <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
                            <CheckCircle2 className="w-3 h-3" /> Acknowledged
                          </span>
                        ) : (
                          <Button size="sm" variant="outline" onClick={() => acknowledge(d)}>
                            Acknowledge
                          </Button>
                        )}
                      </td>
                      <td className="px-4 py-3 text-right">
                        <button
                          type="button"
                          onClick={() => remove(d)}
                          className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                          aria-label="Delete"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record donation</DialogTitle>
            <DialogDescription>Log a contribution from a member.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Donor</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
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
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Cause</Label>
                <Select
                  value={form.cause}
                  onValueChange={(v) => setForm({ ...form, cause: v as DonationCause })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {DONATION_CAUSES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {CAUSE_LABELS[c]}
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
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: e.target.value })}
                />
              </div>
            </div>
            <div className="flex items-center justify-between bg-slate-50 rounded-lg px-3 py-2">
              <div>
                <p className="text-sm font-medium text-slate-900">Anonymous</p>
                <p className="text-xs text-slate-500">Hide the donor name from public lists.</p>
              </div>
              <Switch
                checked={form.anonymous}
                onCheckedChange={(c) => setForm({ ...form, anonymous: c })}
              />
            </div>
            <div>
              <Label>Message</Label>
              <Textarea
                value={form.message}
                onChange={(e) => setForm({ ...form, message: e.target.value })}
                rows={2}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Recording
                </>
              ) : (
                'Record donation'
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

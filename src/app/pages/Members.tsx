import { useEffect, useMemo, useState } from 'react';
import {
  Crown,
  Plus,
  Pencil,
  Trash2,
  Search,
  Mail,
  Phone,
  CalendarClock,
  DollarSign,
  Sparkles,
  AlertCircle,
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
  getMembers,
  createMember,
  updateMember,
  deleteMember,
  computeMemberStats,
  MEMBER_TIERS,
  MEMBER_STATUSES,
  DUES_FREQUENCIES,
  TIER_DUES,
  type Member,
  type MemberTier,
  type MemberStatus,
  type DuesFrequency,
} from '@/app/api/members';

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

interface FormState {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  tier: MemberTier;
  status: MemberStatus;
  duesAmount: string;
  duesFrequency: DuesFrequency;
  renewsAtUtc: string;
  notes: string;
}

const day = 86_400_000;

function newForm(): FormState {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    tier: 'Silver',
    status: 'Pending',
    duesAmount: String(TIER_DUES.Silver),
    duesFrequency: 'Monthly',
    renewsAtUtc: new Date(Date.now() + 365 * day).toISOString().slice(0, 10),
    notes: '',
  };
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

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
}

export default function Members() {
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [tierFilter, setTierFilter] = useState<'All' | MemberTier>('All');
  const [statusFilter, setStatusFilter] = useState<'All' | MemberStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Member | null>(null);
  const [form, setForm] = useState<FormState>(newForm());
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const list = await getMembers();
      setMembers(list);
    } catch {
      toast.error('Failed to load members');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeMemberStats(members), [members]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return members.filter((m) => {
      if (tierFilter !== 'All' && m.tier !== tierFilter) return false;
      if (statusFilter !== 'All' && m.status !== statusFilter) return false;
      if (!q) return true;
      const hay = `${m.firstName} ${m.lastName} ${m.email} ${m.phone ?? ''}`.toLowerCase();
      return hay.includes(q);
    });
  }, [members, search, tierFilter, statusFilter]);

  const openCreate = () => {
    setEditing(null);
    setForm(newForm());
    setDialogOpen(true);
  };

  const openEdit = (m: Member) => {
    setEditing(m);
    setForm({
      firstName: m.firstName,
      lastName: m.lastName,
      email: m.email,
      phone: m.phone ?? '',
      tier: m.tier,
      status: m.status,
      duesAmount: String(m.duesAmount),
      duesFrequency: m.duesFrequency,
      renewsAtUtc: m.renewsAtUtc.slice(0, 10),
      notes: m.notes ?? '',
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      toast.error('First and last name are required');
      return;
    }
    if (!form.email.trim()) {
      toast.error('Email is required');
      return;
    }
    setSaving(true);
    try {
      const renewsAt = new Date(form.renewsAtUtc).toISOString();
      const payload = {
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || undefined,
        tier: form.tier,
        status: form.status,
        duesAmount: Number(form.duesAmount) || 0,
        duesFrequency: form.duesFrequency,
        renewsAtUtc: renewsAt,
        joinedAtUtc: editing?.joinedAtUtc ?? new Date().toISOString(),
        notes: form.notes.trim() || undefined,
      };
      if (editing) {
        await updateMember(editing.id, payload);
        toast.success('Member updated');
      } else {
        await createMember(payload);
        toast.success('Member added');
      }
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to save member');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (m: Member) => {
    if (!confirm(`Remove ${m.firstName} ${m.lastName} from members?`)) return;
    setDeletingId(m.id);
    try {
      const ok = await deleteMember(m.id);
      if (ok) {
        toast.success('Member removed');
        await load();
      } else {
        toast.error('Failed to remove member');
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-indigo-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-amber-500/15 rounded-full blur-3xl" />
              <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-violet-500/10 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-indigo-500 to-violet-500 flex items-center justify-center shadow-2xl shadow-indigo-500/30">
                    <Crown className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Members</h1>
                    <p className="text-slate-400 mt-1">
                      Tiers, dues, renewals, and house account balances.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-indigo-500 to-violet-500 hover:from-indigo-600 hover:to-violet-600 shadow-lg shadow-indigo-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  Add member
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<Crown className="w-5 h-5 text-indigo-600" />}
              label="Total members"
              value={String(stats.total)}
              hint={`${stats.active} active`}
            />
            <StatCard
              icon={<Sparkles className="w-5 h-5 text-emerald-600" />}
              label="Monthly recurring"
              value={formatCurrency(stats.monthlyRecurringRevenue)}
              hint="Active dues, normalized"
            />
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-amber-600" />}
              label="House accounts"
              value={formatCurrency(stats.totalHouseAccountBalance)}
              hint="Total balance across members"
            />
            <StatCard
              icon={<CalendarClock className="w-5 h-5 text-rose-600" />}
              label="Renewing < 30d"
              value={String(stats.renewingSoon)}
              hint={`${stats.lapsed} lapsed • ${stats.pending} pending`}
            />
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by name, email, phone..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={tierFilter}
              onValueChange={(v) => setTierFilter(v as 'All' | MemberTier)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Tier" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All tiers</SelectItem>
                {MEMBER_TIERS.map((t) => (
                  <SelectItem key={t} value={t}>
                    {t}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | MemberStatus)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {MEMBER_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {s}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* List */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={Crown}
              title={members.length === 0 ? 'No members yet' : 'No members match these filters'}
              description={
                members.length === 0
                  ? 'Add your first member to start tracking tiers, dues, and renewals.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={members.length === 0 ? 'Add member' : undefined}
              onAction={members.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <div className="space-y-3">
              {filtered.map((m) => {
                const renewsAt = Date.parse(m.renewsAtUtc);
                const renewSoon = renewsAt - Date.now() < 30 * day && renewsAt > Date.now();
                const overdue = renewsAt < Date.now() && m.status === 'Active';
                return (
                  <div
                    key={m.id}
                    className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-shadow"
                  >
                    <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                      <div className="flex items-start gap-4 min-w-0 flex-1">
                        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-indigo-100 to-violet-100 flex items-center justify-center text-indigo-700 font-semibold shrink-0">
                          {(m.firstName[0] ?? '?') + (m.lastName[0] ?? '')}
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <h3 className="font-semibold text-slate-900 truncate">
                              {m.firstName} {m.lastName}
                            </h3>
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${TIER_BADGE[m.tier]}`}>
                              {m.tier}
                            </span>
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[m.status]}`}>
                              {m.status}
                            </span>
                            {overdue && (
                              <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium bg-rose-50 text-rose-700 ring-1 ring-rose-200">
                                <AlertCircle className="w-3 h-3" /> Overdue
                              </span>
                            )}
                            {renewSoon && (
                              <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 ring-1 ring-amber-200">
                                Renews soon
                              </span>
                            )}
                          </div>
                          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500">
                            <span className="inline-flex items-center gap-1 truncate">
                              <Mail className="w-3.5 h-3.5" /> {m.email}
                            </span>
                            {m.phone && (
                              <span className="inline-flex items-center gap-1">
                                <Phone className="w-3.5 h-3.5" /> {m.phone}
                              </span>
                            )}
                            <span className="inline-flex items-center gap-1">
                              <CalendarClock className="w-3.5 h-3.5" /> Renews {formatDate(m.renewsAtUtc)}
                            </span>
                          </div>
                          {m.notes && (
                            <p className="mt-2 text-sm text-slate-500 line-clamp-2">{m.notes}</p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-6 md:gap-8">
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-wide text-slate-400">Dues</p>
                          <p className="font-semibold text-slate-900">
                            {formatCurrency(m.duesAmount)}
                            <span className="text-xs font-normal text-slate-500">
                              {' '}/{m.duesFrequency.toLowerCase()}
                            </span>
                          </p>
                        </div>
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-wide text-slate-400">House</p>
                          <p
                            className={`font-semibold ${
                              m.houseAccountBalance < 0 ? 'text-rose-600' : 'text-slate-900'
                            }`}
                          >
                            {formatCurrency(m.houseAccountBalance)}
                          </p>
                        </div>
                        <div className="flex items-center gap-1">
                          <button
                            type="button"
                            onClick={() => openEdit(m)}
                            className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                            aria-label="Edit member"
                          >
                            <Pencil className="h-4 w-4" />
                          </button>
                          <button
                            type="button"
                            onClick={() => remove(m)}
                            disabled={deletingId === m.id}
                            className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                            aria-label="Remove member"
                          >
                            {deletingId === m.id ? (
                              <Loader2 className="h-4 w-4 animate-spin" />
                            ) : (
                              <Trash2 className="h-4 w-4" />
                            )}
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit member' : 'Add member'}</DialogTitle>
            <DialogDescription>
              Membership details, dues, and renewal date.
            </DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div>
              <Label htmlFor="firstName">First name</Label>
              <Input
                id="firstName"
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="lastName">Last name</Label>
              <Input
                id="lastName"
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="email">Email</Label>
              <Input
                id="email"
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="phone">Phone</Label>
              <Input
                id="phone"
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
              />
            </div>
            <div>
              <Label>Tier</Label>
              <Select
                value={form.tier}
                onValueChange={(v) =>
                  setForm({
                    ...form,
                    tier: v as MemberTier,
                    duesAmount: String(TIER_DUES[v as MemberTier]),
                  })
                }
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEMBER_TIERS.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t} — {formatCurrency(TIER_DUES[t])}/mo
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as MemberStatus })}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEMBER_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="duesAmount">Dues amount</Label>
              <Input
                id="duesAmount"
                type="number"
                min="0"
                step="0.01"
                value={form.duesAmount}
                onChange={(e) => setForm({ ...form, duesAmount: e.target.value })}
              />
            </div>
            <div>
              <Label>Frequency</Label>
              <Select
                value={form.duesFrequency}
                onValueChange={(v) => setForm({ ...form, duesFrequency: v as DuesFrequency })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DUES_FREQUENCIES.map((f) => (
                    <SelectItem key={f} value={f}>
                      {f}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="renews">Renews on</Label>
              <Input
                id="renews"
                type="date"
                value={form.renewsAtUtc}
                onChange={(e) => setForm({ ...form, renewsAtUtc: e.target.value })}
              />
            </div>
            <div className="md:col-span-2">
              <Label htmlFor="notes">Notes</Label>
              <Textarea
                id="notes"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                rows={3}
                placeholder="Preferences, allergies, anniversaries..."
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
              ) : editing ? (
                'Save changes'
              ) : (
                'Add member'
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

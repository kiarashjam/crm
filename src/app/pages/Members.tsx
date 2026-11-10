import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
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
  ChevronRight,
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
  MEMBER_STATUSES,
  DUES_FREQUENCIES,
  type Member,
  type MemberTier,
  type MemberStatus,
  type DuesFrequency,
} from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { DuplicateMerge, MEMBER_DUPLICATES } from '@/app/components/duplicates';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { Checkbox } from '@/app/components/ui/checkbox';
import { useClubCatalog } from '@/app/hooks/queries';
import { CADENCE_LABEL, tierNamed, tiersOnSale, type CatalogItem } from '@/app/api/catalog';

/** A tier's dues as a member's dues frequency: the catalogue bills the same four cadences. */
function frequencyOf(tier: CatalogItem | undefined): DuesFrequency {
  return tier?.cadence ?? 'Monthly';
}

/** The tier's badge: one of the four every club starts with, or a plain one for a tier of the club's own. */
const tierBadge = (tier: string) => TIER_BADGE[tier] ?? 'bg-slate-100 text-slate-700 ring-1 ring-slate-200';

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
  /** On adding: start their dues today, and charge the tier's initiation fee. */
  startDues: boolean;
  chargeInitiationFee: boolean;
}

const day = 86_400_000;

/** A new member's form, on the club's first tier on sale and its dues from the catalogue. */
function newForm(tier: CatalogItem | undefined): FormState {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    tier: tier?.name ?? '',
    status: 'Pending',
    duesAmount: tier ? String(tier.price) : '',
    duesFrequency: frequencyOf(tier),
    renewsAtUtc: new Date(Date.now() + 365 * day).toISOString().slice(0, 10),
    notes: '',
    startDues: false,
    chargeInitiationFee: false,
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

export default function Members() {
  const { format: formatCurrency } = useClubMoney();
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [tierFilter, setTierFilter] = useState<'All' | MemberTier>('All');
  const [statusFilter, setStatusFilter] = useState<'All' | MemberStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Member | null>(null);
  // The tiers and their dues are the club's own, from the price catalogue.
  const catalogQuery = useClubCatalog('Membership');
  const catalog = catalogQuery.data;
  const tiers = useMemo(() => tiersOnSale(catalog ?? []), [catalog]);
  const [form, setForm] = useState<FormState>(() => newForm(undefined));
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

  // How this person likes members shown: rows, cards or a table, saved for them.
  const navigate = useNavigate();
  const view = useViewPreference('members', { layouts: ['list', 'grid', 'table'] });
  const fullName = (m: Member) => `${m.firstName} ${m.lastName}`.trim();
  const memberColumns: RecordColumn<Member>[] = [
    {
      id: 'name',
      label: 'Member',
      required: true,
      cell: (m) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={fullName(m)} size="sm" />
          <span className="truncate">{fullName(m)}</span>
        </span>
      ),
    },
    { id: 'tier', label: 'Tier', cell: (m) => <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tierBadge(m.tier)}`}>{m.tier}</span> },
    { id: 'status', label: 'Status', cell: (m) => <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[m.status]}`}>{m.status}</span> },
    { id: 'email', label: 'Email', cell: (m) => m.email || '—' },
    { id: 'phone', label: 'Phone', byDefault: false, cell: (m) => m.phone || '—' },
    { id: 'dues', label: 'Dues', numeric: true, cell: (m) => `${formatCurrency(m.duesAmount)} /${m.duesFrequency.toLowerCase()}` },
    { id: 'house', label: 'House account', numeric: true, cell: (m) => <span className={m.houseAccountBalance < 0 ? 'text-rose-600' : ''}>{formatCurrency(m.houseAccountBalance)}</span> },
    { id: 'renews', label: 'Renews', cell: (m) => formatDate(m.renewsAtUtc) },
    { id: 'joined', label: 'Member since', byDefault: false, cell: (m) => formatDate(m.joinedAtUtc) },
  ];
  /** Open, edit and remove: the same in every layout. */
  const memberActions = (m: Member) => (
    <>
      <WriteOnly permission={PERMISSION.ClubMembers}>
        <button
          type="button"
          onClick={() => openEdit(m)}
          className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          aria-label="Edit member"
        >
          <Pencil className="h-4 w-4" />
        </button>
      </WriteOnly>
      <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
        <button
          type="button"
          onClick={() => remove(m)}
          disabled={deletingId === m.id}
          className="rounded p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
          aria-label="Remove member"
        >
          {deletingId === m.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
        </button>
      </WriteOnly>
    </>
  );

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
    setForm(newForm(tiers[0]));
    setDialogOpen(true);
  };

  // The tiers a member can be put on: those on sale, and the one an edited member has.
  const tierChoices = useMemo(() => {
    const names = tiers.map((t) => t.name);
    if (editing && !names.some((n) => n.toLowerCase() === editing.tier.toLowerCase())) names.push(editing.tier);
    return names;
  }, [tiers, editing]);
  // Every tier a member is on or the club has, for the list's filter.
  const tierFilterChoices = useMemo(() => {
    const names = (catalog ?? []).map((t) => t.name);
    for (const m of members) if (!names.includes(m.tier)) names.push(m.tier);
    return names;
  }, [catalog, members]);
  const formTier = tierNamed(catalog ?? [], form.tier);

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
      startDues: false,
      chargeInitiationFee: false,
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
    if (!form.tier) {
      toast.error('Pick a tier');
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
        // An emptied phone or note is sent as null, which takes it off. Left out, as it
        // was, the member kept the number they asked the club to delete.
        await updateMember(editing.id, { ...payload, phone: payload.phone ?? null, notes: payload.notes ?? null });
        toast.success('Member updated');
      } else {
        // What adding them does to their account, said as it is: the fee is charged now;
        // starting the dues starts a subscription, whose first period the billing bills
        // (the daily run, or "Bill what is due"). A fee box ticked for a tier switched
        // since to one without a fee is not sent.
        const feeCharged = form.chargeInitiationFee && (formTier?.initiationFee ?? 0) > 0;
        const duesStarted = form.startDues && (Number(form.duesAmount) || 0) > 0;
        await createMember({
          ...payload,
          startDues: form.startDues || undefined,
          chargeInitiationFee: feeCharged || undefined,
        });
        toast.success(feeCharged && duesStarted ? 'Member added: initiation fee charged, and their dues start today'
          : feeCharged ? 'Member added and initiation fee charged'
            : duesStarted ? 'Member added: their dues start today'
              : 'Member added');
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      // A tier no longer offered, or no right to bill: the server says so in a sentence.
      toast.error(err instanceof Error && err.message ? err.message : 'Failed to save member');
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
    } catch (err) {
      // A member who still owes the club, is owed by it, or has a tab or a subscription
      // running is refused, and the server says what to do instead. Uncaught, the refusal
      // went nowhere and the button seemed to do nothing.
      toast.error(err instanceof Error && err.message ? err.message : 'Failed to remove member');
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
          <PageHero
            icon={Crown}
            tone="indigo"
            title="Members"
            subtitle="Tiers, dues, renewals, and house account balances."
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={openCreate} className={heroPrimary('indigo')}>
                  <Plus className="h-4 w-4" />
                  Add member
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Total members', value: stats.total, icon: Crown, tone: 'indigo', hint: `${stats.active} active` },
              { label: 'Monthly recurring', value: stats.monthlyRecurringRevenue, icon: Sparkles, tone: 'emerald', hint: 'active dues', format: formatCurrency },
              { label: 'House accounts', value: stats.totalHouseAccountBalance, icon: DollarSign, tone: 'amber', hint: 'total balance', format: formatCurrency },
              { label: 'Renewing < 30d', value: stats.renewingSoon, icon: CalendarClock, tone: 'rose', hint: `${stats.lapsed} lapsed · ${stats.pending} pending` },
            ]}
          />

          <DuplicateMerge spec={MEMBER_DUPLICATES} onMerged={() => { void load(); }} />

          {/* Filters */}
          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
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
              <SelectTrigger aria-label="Filter by tier" className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Tier" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All tiers</SelectItem>
                {tierFilterChoices.map((t) => (
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
              <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-44">
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
            <ViewSwitcher view={view} columns={columnChoices(memberColumns)} className="md:ml-auto" />
          </div>

          {/* List */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
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
              actionPermission={PERMISSION.ClubMembers}
              variant="orange"
            />
          ) : (
            <ViewTransition layout={view.layout}>
            {view.layout === 'table' ? (
              <RecordTable
                label="Members"
                items={filtered}
                view={view}
                getKey={(m) => m.id}
                href={(m) => `/members/${m.id}`}
                columns={memberColumns}
                actions={memberActions}
              />
            ) : view.layout === 'grid' ? (
              <div className={`grid sm:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-4')}`}>
                {filtered.map((m, index) => (
                  <StaggerItem
                    key={m.id}
                    index={index}
                    className={`group flex flex-col rounded-2xl border border-slate-200 bg-white shadow-sm hover:border-indigo-200 ${view.density === 'compact' ? 'p-4' : 'p-5'} ${liftOnHover}`}
                  >
                    <div className="flex items-start gap-3">
                      <InitialsAvatar name={fullName(m)} />
                      <div className="min-w-0 flex-1">
                        <button type="button" onClick={() => navigate(`/members/${m.id}`)} className="block max-w-full truncate text-left font-semibold text-slate-900 hover:text-indigo-700">
                          {fullName(m)}
                        </button>
                        <p className="truncate text-sm text-slate-500">{m.email}</p>
                      </div>
                    </div>
                    <div className="mt-3 flex flex-wrap gap-1.5">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tierBadge(m.tier)}`}>{m.tier}</span>
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[m.status]}`}>{m.status}</span>
                    </div>
                    <div className="mt-4 grid grid-cols-2 gap-3 border-t border-slate-100 pt-3 text-sm">
                      <div>
                        <p className="text-[10px] uppercase tracking-wide text-slate-500">Dues</p>
                        <p className="font-semibold text-slate-900">{formatCurrency(m.duesAmount)}<span className="text-xs font-normal text-slate-500"> /{m.duesFrequency.toLowerCase()}</span></p>
                      </div>
                      <div>
                        <p className="text-[10px] uppercase tracking-wide text-slate-500">House</p>
                        <p className={`font-semibold ${m.houseAccountBalance < 0 ? 'text-rose-600' : 'text-slate-900'}`}>{formatCurrency(m.houseAccountBalance)}</p>
                      </div>
                    </div>
                    <div className="mt-auto flex items-center justify-between pt-3">
                      <span className="inline-flex items-center gap-1 text-xs text-slate-500"><CalendarClock className="h-3.5 w-3.5" /> Renews {formatDate(m.renewsAtUtc)}</span>
                      <div className="flex items-center">{memberActions(m)}</div>
                    </div>
                  </StaggerItem>
                ))}
              </div>
            ) : (
            <div className={view.density === 'compact' ? 'space-y-2' : 'space-y-3'}>
              {filtered.map((m, index) => {
                const renewsAt = Date.parse(m.renewsAtUtc);
                const renewSoon = renewsAt - Date.now() < 30 * day && renewsAt > Date.now();
                const overdue = renewsAt < Date.now() && m.status === 'Active';
                return (
                  <StaggerItem
                    key={m.id}
                    index={index}
                    className={`rounded-2xl border border-slate-200 bg-white shadow-sm hover:border-indigo-200 ${view.density === 'compact' ? 'px-4 py-3' : 'p-5'} ${liftOnHover}`}
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
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${tierBadge(m.tier)}`}>
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
                          <p className="text-xs uppercase tracking-wide text-slate-500">Dues</p>
                          <p className="font-semibold text-slate-900">
                            {formatCurrency(m.duesAmount)}
                            <span className="text-xs font-normal text-slate-500">
                              {' '}/{m.duesFrequency.toLowerCase()}
                            </span>
                          </p>
                        </div>
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-wide text-slate-500">House</p>
                          <p
                            className={`font-semibold ${
                              m.houseAccountBalance < 0 ? 'text-rose-600' : 'text-slate-900'
                            }`}
                          >
                            {formatCurrency(m.houseAccountBalance)}
                          </p>
                        </div>
                        <div className="flex items-center gap-1">
                          <Link
                            to={`/members/${m.id}`}
                            className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
                            aria-label="Open member"
                          >
                            <ChevronRight className="h-4 w-4" />
                          </Link>
                          {memberActions(m)}
                        </div>
                      </div>
                    </div>
                  </StaggerItem>
                );
              })}
            </div>
            )}
            </ViewTransition>
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
                onValueChange={(v) => {
                  const tier = tierNamed(catalog ?? [], v);
                  // A tier picked brings its dues from the catalogue; they can still be changed.
                  setForm({
                    ...form,
                    tier: v,
                    duesAmount: tier ? String(tier.price) : form.duesAmount,
                    duesFrequency: tier ? frequencyOf(tier) : form.duesFrequency,
                  });
                }}
              >
                <SelectTrigger aria-label="Tier">
                  <SelectValue placeholder={catalogQuery.isError ? 'The tiers could not be read' : 'Pick a tier'} />
                </SelectTrigger>
                <SelectContent>
                  {tierChoices.map((t) => {
                    const tier = tierNamed(catalog ?? [], t);
                    return (
                      <SelectItem key={t} value={t}>
                        {tier && tier.cadence ? `${t} — ${formatCurrency(tier.price)}/${CADENCE_LABEL[tier.cadence]}` : t}
                      </SelectItem>
                    );
                  })}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as MemberStatus })}>
                <SelectTrigger aria-label="Status">
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
                <SelectTrigger aria-label="Frequency">
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
            {!editing && (
              <WriteOnly permission={PERMISSION.ManageMoney}>
                <div className="md:col-span-2 space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <label className="flex items-center gap-2 text-sm text-slate-700">
                    <Checkbox
                      checked={form.startDues}
                      onCheckedChange={(v) => setForm({ ...form, startDues: v === true })}
                      aria-label="Start their dues today"
                    />
                    Start their dues today ({formatCurrency(Number(form.duesAmount) || 0)} a{' '}
                    {CADENCE_LABEL[form.duesFrequency] ?? 'month'})
                  </label>
                  {formTier?.initiationFee ? (
                    <label className="flex items-center gap-2 text-sm text-slate-700">
                      <Checkbox
                        checked={form.chargeInitiationFee}
                        onCheckedChange={(v) => setForm({ ...form, chargeInitiationFee: v === true })}
                        aria-label="Charge the initiation fee"
                      />
                      Charge the {formTier.name} initiation fee ({formatCurrency(formTier.initiationFee)})
                    </label>
                  ) : null}
                </div>
              </WriteOnly>
            )}
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


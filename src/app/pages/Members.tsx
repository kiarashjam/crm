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
import { DateInput } from '@/app/components/DateInput';
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
import { currentDateLocale } from '@/app/i18n/current';
import { asLanguage, LANGUAGES, LANGUAGE_NAMES, type Language } from '@/app/i18n/languages';
import { useRegional } from '@/app/hooks/useMoney';
import { useT, type I18nValue, type MessageKey } from '@/app/i18n';

/** A member's status ("Lapsed") in the reader's language. */
const statusLabel = (t: I18nValue['t'], status: MemberStatus) => t(`club.status.${status}` as MessageKey);
/** A dues frequency ("Monthly") in the reader's language. */
const frequencyLabel = (t: I18nValue['t'], frequency: DuesFrequency) => t(`club.frequency.${frequency}` as MessageKey);
/** "/monthly", "/Monat", "/mois": after an amount of dues. */
const perLabel = (t: I18nValue['t'], frequency: DuesFrequency) => t(`club.per.${frequency}` as MessageKey);

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
  /** yyyy-mm-dd, or empty. */
  startsOn: string;
  startUndetermined: boolean;
  /** The language the club writes to them in. */
  language: Language;
}

const day = 86_400_000;

/** When the membership starts, as a badge: a date, "to be decided", or nothing said. */
function StartBadge({ member }: { member: Member }) {
  const { t } = useT();
  if (member.startUndetermined) {
    return <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700 ring-1 ring-amber-200">{t('members.startUndecided')}</span>;
  }
  if (!member.startsOnUtc) return null;
  const starts = new Date(member.startsOnUtc);
  const future = starts.getTime() > Date.now();
  const date = starts.toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric' });
  return (
    <span className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ${future ? 'bg-sky-50 text-sky-700 ring-sky-200' : 'bg-slate-50 text-slate-600 ring-slate-200'}`}>
      {future ? t('members.startsOn', { date }) : t('members.startedOn', { date })}
    </span>
  );
}

/**
 * A new member's form, on the club's first tier on sale and its dues from the catalogue, and
 * written to in the workspace's language (from its locale) unless staff pick another.
 */
function newForm(tier: CatalogItem | undefined, language: Language): FormState {
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
    startsOn: '',
    startUndetermined: false,
    language,
  };
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(currentDateLocale(), {
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
  // A new member is written to in the workspace's language unless staff pick another.
  const workspaceLanguage = asLanguage(useRegional().locale) ?? 'en';
  const [form, setForm] = useState<FormState>(() => newForm(undefined, workspaceLanguage));
  const { t } = useT();
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const list = await getMembers();
      setMembers(list);
    } catch {
      toast.error(t('members.loadFailed'));
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
      label: t('members.colMember'),
      required: true,
      cell: (m) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={fullName(m)} size="sm" />
          <span className="truncate">{fullName(m)}</span>
        </span>
      ),
    },
    { id: 'tier', label: t('members.tier'), cell: (m) => <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tierBadge(m.tier)}`}>{m.tier}</span> },
    { id: 'status', label: t('members.status'), cell: (m) => <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[m.status]}`}>{statusLabel(t, m.status)}</span> },
    { id: 'email', label: t('members.colEmail'), cell: (m) => m.email || '—' },
    { id: 'phone', label: t('members.colPhone'), byDefault: false, cell: (m) => m.phone || '—' },
    { id: 'dues', label: t('members.colDues'), numeric: true, cell: (m) => `${formatCurrency(m.duesAmount)} ${perLabel(t, m.duesFrequency)}` },
    { id: 'house', label: t('members.colHouse'), numeric: true, cell: (m) => <span className={m.houseAccountBalance < 0 ? 'text-rose-600' : ''}>{formatCurrency(m.houseAccountBalance)}</span> },
    { id: 'renews', label: t('members.colRenews'), cell: (m) => formatDate(m.renewsAtUtc) },
    { id: 'joined', label: t('members.colJoined'), byDefault: false, cell: (m) => formatDate(m.joinedAtUtc) },
    { id: 'starts', label: t('members.colStarts'), cell: (m) => (m.startUndetermined || m.startsOnUtc ? <StartBadge member={m} /> : '—') },
  ];
  /** Open, edit and remove: the same in every layout. */
  const memberActions = (m: Member) => (
    <>
      <WriteOnly permission={PERMISSION.ClubMembers}>
        <button
          type="button"
          onClick={() => openEdit(m)}
          className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          aria-label={t('members.edit')}
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
          aria-label={t('members.remove')}
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
    setForm(newForm(tiers[0], workspaceLanguage));
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
      startsOn: m.startsOnUtc?.slice(0, 10) ?? '',
      startUndetermined: m.startUndetermined ?? false,
      language: asLanguage(m.preferredLanguage) ?? 'en',
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (!form.firstName.trim() || !form.lastName.trim()) {
      toast.error(t('members.namesRequired'));
      return;
    }
    if (!form.email.trim()) {
      toast.error(t('members.emailRequired'));
      return;
    }
    if (!form.tier) {
      toast.error(t('members.pickTier'));
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
        // A date, or still to be decided; sent as null, a date is taken off.
        startsOnUtc: form.startUndetermined || !form.startsOn ? null : form.startsOn,
        startUndetermined: form.startUndetermined,
      };
      if (editing) {
        // An emptied phone or note is sent as null, which takes it off. Left out, as it
        // was, the member kept the number they asked the club to delete. The language is
        // sent only when it was changed: one never set stays unset (English).
        const languageChanged = form.language !== (asLanguage(editing.preferredLanguage) ?? 'en');
        await updateMember(editing.id, {
          ...payload, phone: payload.phone ?? null, notes: payload.notes ?? null,
          ...(languageChanged ? { preferredLanguage: form.language } : {}),
        });
        toast.success(t('members.updated'));
      } else {
        // What adding them does to their account, said as it is: the fee is charged now;
        // starting the dues starts a subscription, whose first period the billing bills
        // (the daily run, or "Bill what is due"). A fee box ticked for a tier switched
        // since to one without a fee is not sent.
        const feeCharged = form.chargeInitiationFee && (formTier?.initiationFee ?? 0) > 0;
        const duesStarted = form.startDues && (Number(form.duesAmount) || 0) > 0;
        await createMember({
          ...payload,
          preferredLanguage: form.language,
          startDues: form.startDues || undefined,
          chargeInitiationFee: feeCharged || undefined,
        });
        toast.success(feeCharged && duesStarted ? t('members.addedFeeAndDues')
          : feeCharged ? t('members.addedFee')
            : duesStarted ? t('members.addedDues')
              : t('members.added'));
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      // A tier no longer offered, or no right to bill: the server says so in a sentence.
      toast.error(err instanceof Error && err.message ? err.message : t('members.saveFailed'));
    } finally {
      setSaving(false);
    }
  };

  const remove = async (m: Member) => {
    if (!confirm(t('members.removeConfirm', { name: `${m.firstName} ${m.lastName}` }))) return;
    setDeletingId(m.id);
    try {
      const ok = await deleteMember(m.id);
      if (ok) {
        toast.success(t('members.removed'));
        await load();
      } else {
        toast.error(t('members.removeFailed'));
      }
    } catch (err) {
      // A member who still owes the club, is owed by it, or has a tab or a subscription
      // running is refused, and the server says what to do instead. Uncaught, the refusal
      // went nowhere and the button seemed to do nothing.
      toast.error(err instanceof Error && err.message ? err.message : t('members.removeFailed'));
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
            title={t('members.title')}
            subtitle={t('members.subtitle')}
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={openCreate} className={heroPrimary('indigo')}>
                  <Plus className="h-4 w-4" />
                  {t('members.add')}
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: t('members.statTotal'), value: stats.total, icon: Crown, tone: 'indigo', hint: t('members.statTotalHint', { count: stats.active }) },
              { label: t('members.statRecurring'), value: stats.monthlyRecurringRevenue, icon: Sparkles, tone: 'emerald', hint: t('members.statRecurringHint'), format: formatCurrency },
              { label: t('members.statHouse'), value: stats.totalHouseAccountBalance, icon: DollarSign, tone: 'amber', hint: t('members.statHouseHint'), format: formatCurrency },
              { label: t('members.statRenewing'), value: stats.renewingSoon, icon: CalendarClock, tone: 'rose', hint: t('members.statRenewingHint', { lapsed: stats.lapsed, pending: stats.pending }) },
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
                placeholder={t('members.search')}
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={tierFilter}
              onValueChange={(v) => setTierFilter(v as 'All' | MemberTier)}
            >
              <SelectTrigger aria-label={t('members.filterTier')} className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder={t('members.tier')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">{t('members.allTiers')}</SelectItem>
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
              <SelectTrigger aria-label={t('members.filterStatus')} className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder={t('members.status')} />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">{t('members.allStatuses')}</SelectItem>
                {MEMBER_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {statusLabel(t, s)}
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
              title={members.length === 0 ? t('members.emptyTitle') : t('members.emptyFilteredTitle')}
              description={
                members.length === 0
                  ? t('members.emptyText')
                  : t('members.emptyFilteredText')
              }
              actionLabel={members.length === 0 ? t('members.add') : undefined}
              onAction={members.length === 0 ? openCreate : undefined}
              actionPermission={PERMISSION.ClubMembers}
              variant="orange"
            />
          ) : (
            <ViewTransition layout={view.layout}>
            {view.layout === 'table' ? (
              <RecordTable
                label={t('members.title')}
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
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATUS_BADGE[m.status]}`}>{statusLabel(t, m.status)}</span>
                    </div>
                    <div className="mt-4 grid grid-cols-2 gap-3 border-t border-slate-100 pt-3 text-sm">
                      <div>
                        <p className="text-[10px] uppercase tracking-wide text-slate-500">{t('members.dues')}</p>
                        <p className="font-semibold text-slate-900">{formatCurrency(m.duesAmount)}<span className="text-xs font-normal text-slate-500"> {perLabel(t, m.duesFrequency)}</span></p>
                      </div>
                      <div>
                        <p className="text-[10px] uppercase tracking-wide text-slate-500">{t('members.house')}</p>
                        <p className={`font-semibold ${m.houseAccountBalance < 0 ? 'text-rose-600' : 'text-slate-900'}`}>{formatCurrency(m.houseAccountBalance)}</p>
                      </div>
                    </div>
                    <div className="mt-auto flex items-center justify-between pt-3">
                      <span className="inline-flex items-center gap-1 text-xs text-slate-500"><CalendarClock className="h-3.5 w-3.5" /> {t('members.renewsOnDate', { date: formatDate(m.renewsAtUtc) })}</span>
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
                              {statusLabel(t, m.status)}
                            </span>
                            <StartBadge member={m} />
                            {overdue && (
                              <span className="inline-flex items-center gap-1 text-xs px-2 py-0.5 rounded-full font-medium bg-rose-50 text-rose-700 ring-1 ring-rose-200">
                                <AlertCircle className="w-3 h-3" /> {t('members.overdue')}
                              </span>
                            )}
                            {renewSoon && (
                              <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 ring-1 ring-amber-200">
                                {t('members.renewsSoon')}
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
                              <CalendarClock className="w-3.5 h-3.5" /> {t('members.renewsOnDate', { date: formatDate(m.renewsAtUtc) })}
                            </span>
                          </div>
                          {m.notes && (
                            <p className="mt-2 text-sm text-slate-500 line-clamp-2">{m.notes}</p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-6 md:gap-8">
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-wide text-slate-500">{t('members.dues')}</p>
                          <p className="font-semibold text-slate-900">
                            {formatCurrency(m.duesAmount)}
                            <span className="text-xs font-normal text-slate-500">
                              {' '}{perLabel(t, m.duesFrequency)}
                            </span>
                          </p>
                        </div>
                        <div className="text-right">
                          <p className="text-xs uppercase tracking-wide text-slate-500">{t('members.house')}</p>
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
                            aria-label={t('members.open')}
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
            <DialogTitle>{editing ? t('members.edit') : t('members.add')}</DialogTitle>
            <DialogDescription>
              {t('members.dialogText')}
            </DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div>
              <Label htmlFor="firstName">{t('members.firstName')}</Label>
              <Input
                id="firstName"
                value={form.firstName}
                onChange={(e) => setForm({ ...form, firstName: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="lastName">{t('members.lastName')}</Label>
              <Input
                id="lastName"
                value={form.lastName}
                onChange={(e) => setForm({ ...form, lastName: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="email">{t('members.email')}</Label>
              <Input
                id="email"
                type="email"
                value={form.email}
                onChange={(e) => setForm({ ...form, email: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="phone">{t('members.phone')}</Label>
              <Input
                id="phone"
                value={form.phone}
                onChange={(e) => setForm({ ...form, phone: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="member-language">{t('members.language')}</Label>
              <select
                id="member-language"
                value={form.language}
                onChange={(e) => setForm({ ...form, language: e.target.value as Language })}
                className="h-9 w-full rounded-md border border-input bg-input-background px-3 text-sm"
                aria-describedby="member-language-hint"
              >
                {LANGUAGES.map((code) => <option key={code} value={code} lang={code}>{LANGUAGE_NAMES[code]}</option>)}
              </select>
              <p id="member-language-hint" className="mt-1 text-xs text-slate-500">{t('members.languageHint')}</p>
            </div>
            <div>
              <Label>{t('members.tier')}</Label>
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
                <SelectTrigger aria-label={t('members.tier')}>
                  <SelectValue placeholder={catalogQuery.isError ? t('members.tiersUnreadable') : t('members.pickTier')} />
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
              <Label>{t('members.status')}</Label>
              <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as MemberStatus })}>
                <SelectTrigger aria-label={t('members.status')}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEMBER_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {statusLabel(t, s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="duesAmount">{t('members.duesAmount')}</Label>
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
              <Label>{t('members.frequency')}</Label>
              <Select
                value={form.duesFrequency}
                onValueChange={(v) => setForm({ ...form, duesFrequency: v as DuesFrequency })}
              >
                <SelectTrigger aria-label={t('members.frequency')}>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {DUES_FREQUENCIES.map((f) => (
                    <SelectItem key={f} value={f}>
                      {frequencyLabel(t, f)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="renews">{t('members.renewsOn')}</Label>
              <DateInput
                id="renews"
                value={form.renewsAtUtc}
                onChange={(day) => setForm({ ...form, renewsAtUtc: day })}
              />
            </div>
            <div>
              <Label htmlFor="startsOn">{t('members.startsLabel')}</Label>
              <DateInput
                id="startsOn"
                value={form.startUndetermined ? '' : form.startsOn}
                disabled={form.startUndetermined}
                onChange={(day) => setForm({ ...form, startsOn: day })}
              />
            </div>
            <label className="flex items-end gap-2 pb-2 text-sm text-slate-600">
              <Checkbox
                checked={form.startUndetermined}
                onCheckedChange={(v) => setForm({ ...form, startUndetermined: v === true })}
                aria-label={t('members.startUndetermined')}
              />
              {t('members.startUndetermined')}
            </label>
            {!editing && (
              <WriteOnly permission={PERMISSION.ManageMoney}>
                <div className="md:col-span-2 space-y-2 rounded-xl border border-slate-200 bg-slate-50 p-3">
                  <label className="flex items-center gap-2 text-sm text-slate-700">
                    <Checkbox
                      checked={form.startDues}
                      onCheckedChange={(v) => setForm({ ...form, startDues: v === true })}
                      aria-label={t('members.startDues')}
                    />
                    {t('members.startDuesText', {
                      amount: formatCurrency(Number(form.duesAmount) || 0),
                      period: CADENCE_LABEL[form.duesFrequency] ? t(`club.cadence.${form.duesFrequency}` as MessageKey) : t('club.cadence.Monthly'),
                    })}
                  </label>
                  {formTier?.initiationFee ? (
                    <label className="flex items-center gap-2 text-sm text-slate-700">
                      <Checkbox
                        checked={form.chargeInitiationFee}
                        onCheckedChange={(v) => setForm({ ...form, chargeInitiationFee: v === true })}
                        aria-label={t('members.chargeFee')}
                      />
                      {t('members.chargeFeeText', { tier: formTier.name, amount: formatCurrency(formTier.initiationFee) })}
                    </label>
                  ) : null}
                </div>
              </WriteOnly>
            )}
            <div className="md:col-span-2">
              <Label htmlFor="notes">{t('members.notes')}</Label>
              <Textarea
                id="notes"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                rows={3}
                placeholder={t('members.notesPlaceholder')}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              {t('common.cancel')}
            </Button>
            <Button onClick={save} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> {t('members.saving')}
                </>
              ) : editing ? (
                t('members.saveChanges')
              ) : (
                t('members.add')
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}


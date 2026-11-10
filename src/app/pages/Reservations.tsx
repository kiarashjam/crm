import { useMemo, useState } from 'react';
import {
  CalendarClock,
  Plus,
  Pencil,
  Trash2,
  Search,
  Users,
  Clock,
  MapPin,
  Coffee,
  Briefcase,
  Sparkles,
  Trophy,
  Activity as ActivityIcon,
  Wine,
  Loader2,
  Settings2,
  Wallet,
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
  createReservation,
  updateReservation,
  deleteReservation,
  addUsualResources,
  computeReservationStats,
  NEXT_STATUSES,
  RESERVATION_STATUSES,
  type Reservation,
  type ReservationInput,
  type ReservationStatus,
  type Resource,
  type ResourceKind,
} from '@/app/api/reservations';
import { servesDemoData } from '@/app/api/apiClient';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import { NONE, useClubMembers, useClubReads, useClubResources, useReservations } from '@/app/hooks/queries';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { WriteOnly } from '@/app/components/WriteOnly';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import ResourcesDialog from '@/app/pages/reservations/ResourcesDialog';

const KIND_ICON: Record<ResourceKind, React.ElementType> = {
  Dining: Coffee,
  Meeting: Briefcase,
  Spa: Sparkles,
  Court: Trophy,
  Studio: ActivityIcon,
  Lounge: Wine,
};

const KIND_COLOR: Record<ResourceKind, string> = {
  Dining: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200',
  Meeting: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Spa: 'bg-pink-50 text-pink-700 ring-1 ring-pink-200',
  Court: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Studio: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
  Lounge: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
};

const STATUS_BADGE: Record<ReservationStatus, string> = {
  Pending: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Confirmed: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  CheckedIn: 'bg-teal-50 text-teal-700 ring-1 ring-teal-200',
  Completed: 'bg-indigo-50 text-indigo-700 ring-1 ring-indigo-200',
  Cancelled: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
  NoShow: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
};

type Deposit = NonNullable<ReservationInput['deposit']>;
type DepositMethod = NonNullable<ReservationInput['depositMethod']>;

interface FormState {
  resourceId: string;
  /** Who it is for: a member picked from the list, or a guest by name. */
  forWho: 'member' | 'guest';
  memberId: string;
  memberName: string;
  memberEmail: string;
  partySize: string;
  startAtUtc: string;
  endAtUtc: string;
  status: ReservationStatus;
  notes: string;
  deposit: Deposit;
  depositMethod: DepositMethod;
  depositReason: string;
}

function newForm(resources: Resource[]): FormState {
  const startDefault = new Date(Date.now() + 86_400_000);
  startDefault.setHours(19, 0, 0, 0);
  const endDefault = new Date(startDefault.getTime() + 2 * 3_600_000);
  return {
    resourceId: resources.find((r) => r.active !== false)?.id ?? '',
    forWho: 'member',
    memberId: '',
    memberName: '',
    memberEmail: '',
    partySize: '2',
    startAtUtc: toDateTimeLocal(startDefault),
    endAtUtc: toDateTimeLocal(endDefault),
    status: 'Confirmed',
    notes: '',
    deposit: 'charge',
    depositMethod: 'CardOnFile',
    depositReason: '',
  };
}

function toDateTimeLocal(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

function isToday(iso: string): boolean {
  const d = new Date(iso);
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  );
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/**
 * The statuses a booking can move to, as the server allows them: a no-show posts the
 * resource's fee and keeps the deposit, so it is offered only once the booking has started.
 */
function movesFor(r: Reservation): ReservationStatus[] {
  const started = Date.parse(r.startAtUtc) <= Date.now();
  return NEXT_STATUSES[r.status].filter((s) => s !== 'NoShow' || started);
}

export default function Reservations() {
  const { format: money, currency } = useClubMoney();
  // Read through the app's cache, keyed by workspace: a failed read says so with a way to try
  // again rather than looking like there are no bookings.
  const reservationsQuery = useReservations();
  const resourcesQuery = useClubResources();
  const membersQuery = useClubMembers();
  const reads = useClubReads(reservationsQuery, resourcesQuery, membersQuery);
  const reservations: Reservation[] = reservationsQuery.data ?? NONE;
  const resources: Resource[] = resourcesQuery.data ?? NONE;
  const members = membersQuery.data ?? NONE;
  const loading = reads.loading;
  usePageLoading(loading);
  const load = reads.reload;
  const [search, setSearch] = useState('');
  const [resourceFilter, setResourceFilter] = useState<string>('All');
  const [statusFilter, setStatusFilter] = useState<'All' | ReservationStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [managing, setManaging] = useState(false);
  const [editing, setEditing] = useState<Reservation | null>(null);
  const [form, setForm] = useState<FormState>(newForm([]));
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [addingUsual, setAddingUsual] = useState(false);
  // Recording a deposit as paid, or waiving one, is money: the server refuses it to anyone
  // without money.manage, so it is only offered to those who have it.
  const org = useOrgOptional();
  const canHandleMoney = servesDemoData() || orgCan(org?.currentOrg ?? null, PERMISSION.ManageMoney);

  const stats = useMemo(() => computeReservationStats(reservations), [reservations]);
  const bookable = useMemo(() => resources.filter((r) => r.active !== false), [resources]);
  const resourceOf = (id: string) => resources.find((r) => r.id === id);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return reservations.filter((r) => {
      if (resourceFilter !== 'All' && r.resourceId !== resourceFilter) return false;
      if (statusFilter !== 'All' && r.status !== statusFilter) return false;
      if (!q) return true;
      return `${r.memberName} ${r.memberEmail ?? ''} ${r.resourceName}`.toLowerCase().includes(q);
    });
  }, [reservations, search, resourceFilter, statusFilter]);

  const openCreate = () => {
    setEditing(null);
    setForm({ ...newForm(resources), memberId: members[0]?.id ?? '' });
    setDialogOpen(true);
  };

  const openEdit = (r: Reservation) => {
    setEditing(r);
    setForm({
      ...newForm(resources),
      resourceId: r.resourceId,
      forWho: r.memberId ? 'member' : 'guest',
      memberId: r.memberId ?? members[0]?.id ?? '',
      memberName: r.memberId ? '' : r.memberName,
      memberEmail: r.memberId ? '' : r.memberEmail ?? '',
      partySize: String(r.partySize),
      startAtUtc: toDateTimeLocal(new Date(r.startAtUtc)),
      endAtUtc: toDateTimeLocal(new Date(r.endAtUtc)),
      status: r.status,
      notes: r.notes ?? '',
    });
    setDialogOpen(true);
  };

  const chosen = resourceOf(form.resourceId);
  // A booking made already checked in (taken at the door) asks no deposit: the server gives a
  // deposit back on check-in, so there is nothing left for one to secure.
  const depositAsked = !editing && form.status !== 'CheckedIn' && (chosen?.depositAmount ?? 0) > 0;

  const save = async () => {
    if (!form.resourceId) {
      toast.error('Pick a resource');
      return;
    }
    if (form.forWho === 'member' && !form.memberId) {
      toast.error('Pick the member this booking is for');
      return;
    }
    if (form.forWho === 'guest' && !form.memberName.trim()) {
      toast.error("Give the guest's name");
      return;
    }
    if (new Date(form.endAtUtc).getTime() <= new Date(form.startAtUtc).getTime()) {
      toast.error('End time must be after start time');
      return;
    }
    setSaving(true);
    try {
      // A booking names its member by id: their name and email come from their record.
      const person = form.forWho === 'member'
        ? { memberId: form.memberId }
        : { memberName: form.memberName.trim(), memberEmail: form.memberEmail.trim() || undefined };
      const times = {
        partySize: Number(form.partySize) || 1,
        startAtUtc: new Date(form.startAtUtc).toISOString(),
        endAtUtc: new Date(form.endAtUtc).toISOString(),
      };
      if (editing) {
        await updateReservation(editing.id, {
          resourceId: form.resourceId,
          ...person,
          guest: form.forWho === 'guest' ? true : undefined,
          ...times,
          status: form.status,
          notes: form.notes.trim(),
          version: editing.version,
        });
        toast.success('Reservation updated');
      } else {
        const created = await createReservation({
          resourceId: form.resourceId,
          ...person,
          ...times,
          status: form.status,
          notes: form.notes.trim() || undefined,
          ...(depositAsked
            ? {
                deposit: form.deposit,
                depositMethod: form.deposit === 'paid' ? form.depositMethod : undefined,
                depositReason: form.deposit === 'waive' ? form.depositReason.trim() : undefined,
              }
            : {}),
        });
        toast.success(depositMessage(created));
      }
      setDialogOpen(false);
      await load();
    } catch (err) {
      // A clash says which booking is in the way, and when, as the server put it.
      toast.error(errorText(err, 'Failed to save reservation'));
    } finally {
      setSaving(false);
    }
  };

  const depositMessage = (r: Reservation | null) => {
    if (!r || !r.depositAmount) return 'Reservation booked';
    const amount = money(r.depositAmount);
    switch (r.depositState) {
      case 'charged': return `Booked · deposit ${amount} on ${r.memberName}'s house account`;
      case 'paid': return `Booked · deposit ${amount} paid`;
      case 'waived': return `Booked · deposit waived`;
      default: return 'Reservation booked';
    }
  };

  const remove = async (r: Reservation) => {
    if (!confirm(`Delete reservation for ${r.memberName}?`)) return;
    setDeletingId(r.id);
    try {
      const ok = await deleteReservation(r.id);
      if (ok) {
        toast.success('Reservation deleted');
        await load();
      } else {
        toast.error('Failed to delete');
      }
    } catch (err) {
      // One with a deposit or a fee on the account is cancelled instead: the server says so.
      toast.error(errorText(err, 'Failed to delete'));
    } finally {
      setDeletingId(null);
    }
  };

  const setStatus = async (r: Reservation, status: ReservationStatus) => {
    try {
      const saved = await updateReservation(r.id, { status, version: r.version });
      if (saved?.depositState === 'released' && r.depositState !== 'released') toast.success(`Deposit ${money(saved.depositAmount ?? 0)} given back`);
      else if (saved?.noShowFeeState === 'charged' && r.noShowFeeState !== 'charged') toast.success(`No-show fee ${money(saved.noShowFee ?? 0)} posted to ${saved.memberName}'s account`);
    } catch (err) {
      toast.error(errorText(err, 'Failed to update'));
    }
    await load();
  };

  const addUsual = async () => {
    setAddingUsual(true);
    try {
      await addUsualResources();
      toast.success('The usual spaces are added');
      await load();
    } catch (err) {
      toast.error(errorText(err, 'Could not add them'));
    } finally {
      setAddingUsual(false);
    }
  };

  // How this person likes bookings shown: rows, cards or a table, saved for them.
  const view = useViewPreference('reservations', { layouts: ['list', 'grid', 'table'] });

  const statusLabel = (status: ReservationStatus) =>
    status === 'CheckedIn' ? 'Checked in' : status === 'NoShow' ? 'No-show' : status;

  /** A booking's deposit and fee, as the desk needs to see them. */
  const moneyLine = (r: Reservation) => {
    const parts: string[] = [];
    if (r.depositAmount) {
      const amount = money(r.depositAmount);
      const words: Record<string, string> = {
        charged: `Deposit ${amount} on account`,
        paid: `Deposit ${amount} paid`,
        waived: 'Deposit waived',
        released: `Deposit ${amount} given back`,
        kept: `Deposit ${amount} kept`,
      };
      const said = r.depositState ? words[r.depositState] : undefined;
      if (said) parts.push(said);
    } else if (r.depositState === 'waived') parts.push('Deposit waived');
    if (r.noShowFee && r.noShowFeeState) parts.push(r.noShowFeeState === 'charged' ? `No-show fee ${money(r.noShowFee)}` : 'No-show fee taken back');
    return parts.join(' · ');
  };

  /** Status, edit and delete: the same on a row, a card and a table line. */
  const reservationActions = (r: Reservation) => (
    <div className="flex items-center gap-2">
      {/* Changing a booking's status is the front desk's; only the moves the server allows are offered. */}
      <WriteOnly permission={PERMISSION.FrontDesk}>
        <Select value={r.status} onValueChange={(v) => setStatus(r, v as ReservationStatus)} disabled={movesFor(r).length === 0}>
          <SelectTrigger className="h-9 w-36 text-xs" aria-label={`Status of ${r.memberName}'s booking`}>
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {[r.status, ...movesFor(r)].map((s) => (
              <SelectItem key={s} value={s}>
                {statusLabel(s)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </WriteOnly>
      <WriteOnly permission={PERMISSION.FrontDesk}>
        <button
          type="button"
          onClick={() => openEdit(r)}
          className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
          aria-label="Edit reservation"
        >
          <Pencil className="h-4 w-4" />
        </button>
      </WriteOnly>
      <WriteOnly permission={[PERMISSION.FrontDesk, PERMISSION.DeleteRecords]} all>
        <button
          type="button"
          onClick={() => remove(r)}
          disabled={deletingId === r.id}
          className="rounded p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
          aria-label="Delete reservation"
        >
          {deletingId === r.id ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Trash2 className="h-4 w-4" />
          )}
        </button>
      </WriteOnly>
    </div>
  );

  const reservationColumns: RecordColumn<Reservation>[] = [
    {
      id: 'member',
      label: 'Member',
      required: true,
      cell: (r) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={r.memberName} size="sm" />
          <span className="truncate">{r.memberName}</span>
          {!r.memberId && <span className="shrink-0 text-[10px] text-slate-500">guest</span>}
          {isToday(r.startAtUtc) && (
            <span className="shrink-0 rounded-full bg-amber-50 px-1.5 text-[10px] font-semibold text-amber-700 ring-1 ring-amber-200">Today</span>
          )}
        </span>
      ),
    },
    { id: 'resource', label: 'Resource', cell: (r) => r.resourceName },
    { id: 'when', label: 'When', cell: (r) => formatDateTime(r.startAtUtc) },
    { id: 'party', label: 'Party', numeric: true, cell: (r) => r.partySize },
    {
      id: 'status',
      label: 'Status',
      cell: (r) => <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[r.status]}`}>{statusLabel(r.status)}</span>,
    },
    { id: 'money', label: 'Deposit', byDefault: false, cell: (r) => moneyLine(r) || '—' },
    { id: 'email', label: 'Email', byDefault: false, cell: (r) => r.memberEmail || '—' },
    { id: 'notes', label: 'Notes', byDefault: false, cell: (r) => <span className="line-clamp-1">{r.notes || '—'}</span> },
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
            icon={CalendarClock}
            tone="emerald"
            title="Reservations"
            subtitle="Book dining rooms, spa, courts, and meeting spaces."
            actions={
              <>
                <WriteOnly permission={PERMISSION.ClubMembers}>
                  <Button variant="outline" onClick={() => setManaging(true)} className={heroSecondary}>
                    <Settings2 className="h-4 w-4" />
                    Resources
                  </Button>
                </WriteOnly>
                <WriteOnly permission={PERMISSION.FrontDesk}>
                  <Button onClick={openCreate} className={heroPrimary('emerald')} disabled={bookable.length === 0}>
                    <Plus className="h-4 w-4" />
                    New booking
                  </Button>
                </WriteOnly>
              </>
            }
            stats={[
              { label: 'Today', value: stats.today, icon: CalendarClock, tone: 'emerald', hint: `${stats.totalGuestsToday} guests` },
              { label: 'This week', value: stats.upcomingWeek, icon: Clock, tone: 'teal', hint: 'next 7 days' },
              { label: 'Pending', value: stats.pending, icon: Users, tone: 'amber', hint: 'awaiting confirmation' },
              { label: 'Resources', value: bookable.length, icon: MapPin, tone: 'violet', hint: 'bookable spaces' },
            ]}
          />

          <ClubReadState reads={reads} what="Reservations">
          {/* Resource quick-look */}
          {!loading && resources.length === 0 && (
            <div className="mb-6 flex flex-col gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 md:flex-row md:items-center md:justify-between">
              <p>Nothing can be booked yet: add the club's dining rooms, courts, spa and meeting rooms first.</p>
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button size="sm" onClick={addUsual} disabled={addingUsual} className="shrink-0">
                  {addingUsual ? <Loader2 className="w-4 h-4 mr-1 animate-spin" /> : null}
                  Add the usual spaces
                </Button>
              </WriteOnly>
            </div>
          )}
          {resources.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
              {resources.slice(0, 8).map((r) => {
                const Icon = KIND_ICON[r.kind] ?? MapPin;
                return (
                  <button
                    type="button"
                    key={r.id}
                    onClick={() => setResourceFilter(resourceFilter === r.id ? 'All' : r.id)}
                    className={`text-left bg-white rounded-xl border p-3 shadow-sm hover:shadow-md transition ${
                      resourceFilter === r.id
                        ? 'border-emerald-400 ring-2 ring-emerald-200'
                        : 'border-slate-200'
                    } ${r.active === false ? 'opacity-60' : ''}`}
                  >
                    <div className="flex items-center gap-2 mb-1">
                      <div className={`w-7 h-7 rounded-lg flex items-center justify-center ${KIND_COLOR[r.kind] ?? 'bg-slate-100 text-slate-600'}`}>
                        <Icon className="w-3.5 h-3.5" />
                      </div>
                      <p className="text-sm font-medium text-slate-900 truncate">{r.name}</p>
                    </div>
                    <p className="text-xs text-slate-500">
                      {r.kind} · {r.bookingMode === 'Shared' ? `up to ${r.capacity} people` : `capacity ${r.capacity}, one booking at a time`}
                      {r.active === false && ' · not bookable'}
                    </p>
                    {((r.depositAmount ?? 0) > 0 || (r.noShowFee ?? 0) > 0) && (
                      <p className="text-xs text-slate-500 inline-flex items-center gap-1">
                        <Wallet className="w-3 h-3" />
                        {(r.depositAmount ?? 0) > 0 && `deposit ${money(r.depositAmount ?? 0)}`}
                        {(r.depositAmount ?? 0) > 0 && (r.noShowFee ?? 0) > 0 && ' · '}
                        {(r.noShowFee ?? 0) > 0 && `no-show ${money(r.noShowFee ?? 0)}`}
                      </p>
                    )}
                  </button>
                );
              })}
            </div>
          )}

          {/* Filters */}
          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by member or resource..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select value={resourceFilter} onValueChange={setResourceFilter}>
              <SelectTrigger aria-label="Filter by resource" className="h-11 rounded-xl md:w-56">
                <SelectValue placeholder="Resource" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All resources</SelectItem>
                {resources.map((r) => (
                  <SelectItem key={r.id} value={r.id}>
                    {r.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | ReservationStatus)}
            >
              <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {RESERVATION_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {statusLabel(s)}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(reservationColumns)} className="md:ml-auto" />
          </div>

          {/* List */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={CalendarClock}
              title={reservations.length === 0 ? 'No bookings yet' : 'No bookings match these filters'}
              description={
                reservations.length === 0
                  ? 'Create your first reservation to start tracking who is using which spaces.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={reservations.length === 0 && bookable.length > 0 ? 'New booking' : undefined}
              onAction={reservations.length === 0 && bookable.length > 0 ? openCreate : undefined}
              variant="teal"
            />
          ) : (
            <ViewTransition layout={view.layout}>
            {view.layout === 'table' ? (
              <RecordTable
                label="Bookings"
                items={filtered}
                view={view}
                getKey={(r) => r.id}
                columns={reservationColumns}
                actions={reservationActions}
              />
            ) : view.layout === 'grid' ? (
              <div className={`grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-4')}`}>
                {filtered.map((r, index) => {
                  const kind = resourceOf(r.resourceId)?.kind;
                  const Icon = kind ? KIND_ICON[kind] : MapPin;
                  return (
                    <StaggerItem
                      key={r.id}
                      index={index}
                      className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm hover:border-emerald-200 ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                    >
                      <div className="flex items-start gap-3">
                        <InitialsAvatar name={r.memberName} />
                        <div className="min-w-0 flex-1">
                          <h3 className="font-semibold text-slate-900 truncate">{r.memberName}</h3>
                          <span className={`mt-1 inline-flex text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[r.status]}`}>
                            {statusLabel(r.status)}
                          </span>
                        </div>
                        {isToday(r.startAtUtc) && (
                          <span className="shrink-0 text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 ring-1 ring-amber-200">
                            Today
                          </span>
                        )}
                      </div>
                      <div className="mt-3 space-y-1.5 text-sm text-slate-600">
                        <div className="flex items-center gap-2">
                          <span className={`inline-flex h-6 w-6 items-center justify-center rounded-md ${kind ? KIND_COLOR[kind] : 'bg-slate-100 text-slate-600'}`}>
                            <Icon className="h-3.5 w-3.5" />
                          </span>
                          <span className="truncate">{r.resourceName}</span>
                        </div>
                        <div className="flex items-center gap-2">
                          <Clock className="w-3.5 h-3.5 text-slate-500" /> {formatDateTime(r.startAtUtc)}
                        </div>
                        <div className="flex items-center gap-2">
                          <Users className="w-3.5 h-3.5 text-slate-500" /> Party of {r.partySize}
                        </div>
                        {moneyLine(r) && (
                          <div className="flex items-center gap-2">
                            <Wallet className="w-3.5 h-3.5 text-slate-500" /> {moneyLine(r)}
                          </div>
                        )}
                      </div>
                      {r.notes && view.density !== 'compact' && (
                        <p className="mt-2 text-sm text-slate-500 line-clamp-2">{r.notes}</p>
                      )}
                      <div className="mt-auto pt-3 flex justify-end">{reservationActions(r)}</div>
                    </StaggerItem>
                  );
                })}
              </div>
            ) : (
            <div className={view.density === 'compact' ? 'space-y-2' : 'space-y-3'}>
              {filtered.map((r, index) => {
                const today = isToday(r.startAtUtc);
                return (
                  <StaggerItem
                    key={r.id}
                    index={index}
                    className={`rounded-2xl border border-slate-200 bg-white ${view.density === 'compact' ? 'p-3' : 'p-5'} shadow-sm hover:border-emerald-200 ${liftOnHover}`}
                  >
                    <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
                      <div className="flex items-start gap-4 min-w-0 flex-1">
                        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-emerald-100 to-teal-100 flex items-center justify-center text-emerald-700 font-semibold shrink-0">
                          {r.memberName
                            .split(' ')
                            .map((n) => n[0])
                            .join('')
                            .slice(0, 2)
                            .toUpperCase()}
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <h3 className="font-semibold text-slate-900 truncate">{r.memberName}</h3>
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[r.status]}`}>
                              {statusLabel(r.status)}
                            </span>
                            {!r.memberId && <span className="text-xs text-slate-500">guest</span>}
                            {today && (
                              <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-amber-50 text-amber-700 ring-1 ring-amber-200">
                                Today
                              </span>
                            )}
                          </div>
                          <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500">
                            <span className="inline-flex items-center gap-1">
                              <MapPin className="w-3.5 h-3.5" /> {r.resourceName}
                            </span>
                            <span className="inline-flex items-center gap-1">
                              <Clock className="w-3.5 h-3.5" /> {formatDateTime(r.startAtUtc)}
                            </span>
                            <span className="inline-flex items-center gap-1">
                              <Users className="w-3.5 h-3.5" /> Party of {r.partySize}
                            </span>
                            {moneyLine(r) && (
                              <span className="inline-flex items-center gap-1">
                                <Wallet className="w-3.5 h-3.5" /> {moneyLine(r)}
                              </span>
                            )}
                          </div>
                          {r.notes && (
                            <p className="mt-2 text-sm text-slate-500 line-clamp-2">{r.notes}</p>
                          )}
                        </div>
                      </div>
                      {reservationActions(r)}
                    </div>
                  </StaggerItem>
                );
              })}
            </div>
            )}
            </ViewTransition>
          )}
          </ClubReadState>
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit reservation' : 'New booking'}</DialogTitle>
            <DialogDescription>Pick a resource, who it is for, the time and the party size. A clash with another booking is refused, saying which.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div className="md:col-span-2">
              <Label>Resource</Label>
              <Select value={form.resourceId} onValueChange={(v) => setForm({ ...form, resourceId: v })}>
                <SelectTrigger aria-label="Resource">
                  <SelectValue placeholder="Pick a resource" />
                </SelectTrigger>
                <SelectContent>
                  {(editing ? resources : bookable).map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name} ({r.kind}, {r.bookingMode === 'Shared' ? `up to ${r.capacity}` : `cap ${r.capacity}`}
                      {(r.depositAmount ?? 0) > 0 ? `, deposit ${money(r.depositAmount ?? 0)}` : ''})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="md:col-span-2 flex gap-2" role="radiogroup" aria-label="Who it is for">
              {(['member', 'guest'] as const).map((w) => (
                <button
                  key={w}
                  type="button"
                  role="radio"
                  aria-checked={form.forWho === w}
                  onClick={() => setForm({ ...form, forWho: w })}
                  className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition ${
                    form.forWho === w ? 'bg-emerald-100 text-emerald-800 ring-2 ring-emerald-400' : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {w === 'member' ? 'A member' : 'A guest'}
                </button>
              ))}
            </div>
            {form.forWho === 'member' ? (
              <div className="md:col-span-2">
                <Label htmlFor="rsv-member">Member</Label>
                <select
                  id="rsv-member"
                  value={form.memberId}
                  onChange={(e) => setForm({ ...form, memberId: e.target.value })}
                  className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
                >
                  {members.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.email}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <>
                <div>
                  <Label htmlFor="rsv-name">Guest name</Label>
                  <Input
                    id="rsv-name"
                    value={form.memberName}
                    onChange={(e) => setForm({ ...form, memberName: e.target.value })}
                  />
                </div>
                <div>
                  <Label htmlFor="rsv-email">Guest email</Label>
                  <Input
                    id="rsv-email"
                    type="email"
                    value={form.memberEmail}
                    onChange={(e) => setForm({ ...form, memberEmail: e.target.value })}
                  />
                </div>
              </>
            )}
            <div>
              <Label htmlFor="rsv-start">Starts</Label>
              <Input
                id="rsv-start"
                type="datetime-local"
                value={form.startAtUtc}
                onChange={(e) => setForm({ ...form, startAtUtc: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="rsv-end">Ends</Label>
              <Input
                id="rsv-end"
                type="datetime-local"
                value={form.endAtUtc}
                onChange={(e) => setForm({ ...form, endAtUtc: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="rsv-party">Party size</Label>
              <Input
                id="rsv-party"
                type="number"
                min="1"
                value={form.partySize}
                onChange={(e) => setForm({ ...form, partySize: e.target.value })}
              />
            </div>
            <div>
              <Label>Status</Label>
              <Select
                value={form.status}
                onValueChange={(v) => setForm({ ...form, status: v as ReservationStatus })}
              >
                <SelectTrigger aria-label="Status">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {(editing ? [editing.status, ...movesFor(editing)] : (['Pending', 'Confirmed', 'CheckedIn'] as ReservationStatus[])).map((s) => (
                    <SelectItem key={s} value={s}>
                      {statusLabel(s)}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {depositAsked && (
              <div className="md:col-span-2 rounded-xl border border-emerald-200 bg-emerald-50/50 p-3">
                <p className="text-sm font-medium text-slate-900">
                  Deposit {money(chosen?.depositAmount ?? 0)}
                </p>
                <p className="mb-2 text-xs text-slate-600">
                  Given back when the booking is checked in, completed or cancelled; kept if the member does not come.
                </p>
                <div className="flex flex-wrap gap-2" role="radiogroup" aria-label="Deposit">
                  {(['charge', 'paid', 'waive'] as const).filter((d) => d === 'charge' || canHandleMoney).map((d) => (
                    <button
                      key={d}
                      type="button"
                      role="radio"
                      aria-checked={form.deposit === d}
                      onClick={() => setForm({ ...form, deposit: d })}
                      className={`px-3 py-1.5 rounded-lg text-xs font-medium transition ${
                        form.deposit === d ? 'bg-emerald-100 text-emerald-800 ring-2 ring-emerald-400' : 'bg-white text-slate-600 ring-1 ring-slate-200 hover:bg-slate-50'
                      }`}
                    >
                      {d === 'charge' ? 'Charge to house account' : d === 'paid' ? 'Paid now' : 'Waive'}
                    </button>
                  ))}
                </div>
                {form.deposit === 'paid' && canHandleMoney && (
                  <div className="mt-2">
                    <Label htmlFor="rsv-deposit-method">Paid by</Label>
                    <select
                      id="rsv-deposit-method"
                      value={form.depositMethod}
                      onChange={(e) => setForm({ ...form, depositMethod: e.target.value as DepositMethod })}
                      className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
                    >
                      <option value="CardOnFile">Card</option>
                      <option value="Cash">Cash</option>
                      <option value="Check">Cheque</option>
                      <option value="Wire">Bank transfer</option>
                    </select>
                  </div>
                )}
                {form.deposit === 'waive' && canHandleMoney && (
                  <div className="mt-2">
                    <Label htmlFor="rsv-deposit-reason">Why it is waived</Label>
                    <Input id="rsv-deposit-reason" value={form.depositReason} onChange={(e) => setForm({ ...form, depositReason: e.target.value })} />
                  </div>
                )}
                {form.forWho === 'guest' && form.deposit !== 'waive' && (
                  <p className="mt-2 text-xs text-amber-800">
                    A deposit goes on a member's house account: book this under the member hosting the guest{canHandleMoney ? ', or waive it.' : '.'}
                  </p>
                )}
              </div>
            )}
            <div className="md:col-span-2">
              <Label htmlFor="rsv-notes">Notes</Label>
              <Textarea
                id="rsv-notes"
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                rows={3}
                placeholder="Special requests, dietary needs, occasion..."
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
                'Book'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      <ResourcesDialog open={managing} onOpenChange={setManaging} resources={resources} currency={currency} money={money} onSaved={load} />
    </div>
  );
}

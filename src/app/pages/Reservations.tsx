import { useEffect, useMemo, useState } from 'react';
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
  getReservations,
  getResources,
  createReservation,
  updateReservation,
  deleteReservation,
  computeReservationStats,
  RESERVATION_STATUSES,
  type Reservation,
  type ReservationStatus,
  type Resource,
  type ResourceKind,
} from '@/app/api/reservations';

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

interface FormState {
  resourceId: string;
  memberName: string;
  memberEmail: string;
  partySize: string;
  startAtUtc: string;
  endAtUtc: string;
  status: ReservationStatus;
  notes: string;
}

function newForm(resources: Resource[]): FormState {
  const startDefault = new Date(Date.now() + 86_400_000);
  startDefault.setHours(19, 0, 0, 0);
  const endDefault = new Date(startDefault.getTime() + 2 * 3_600_000);
  return {
    resourceId: resources[0]?.id ?? '',
    memberName: '',
    memberEmail: '',
    partySize: '2',
    startAtUtc: toDateTimeLocal(startDefault),
    endAtUtc: toDateTimeLocal(endDefault),
    status: 'Confirmed',
    notes: '',
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

export default function Reservations() {
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [resources, setResources] = useState<Resource[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [resourceFilter, setResourceFilter] = useState<string>('All');
  const [statusFilter, setStatusFilter] = useState<'All' | ReservationStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<Reservation | null>(null);
  const [form, setForm] = useState<FormState>(newForm([]));
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [rsv, res] = await Promise.all([getReservations(), getResources()]);
      setReservations(rsv);
      setResources(res);
    } catch {
      toast.error('Failed to load reservations');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeReservationStats(reservations), [reservations]);

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
    setForm(newForm(resources));
    setDialogOpen(true);
  };

  const openEdit = (r: Reservation) => {
    setEditing(r);
    setForm({
      resourceId: r.resourceId,
      memberName: r.memberName,
      memberEmail: r.memberEmail ?? '',
      partySize: String(r.partySize),
      startAtUtc: toDateTimeLocal(new Date(r.startAtUtc)),
      endAtUtc: toDateTimeLocal(new Date(r.endAtUtc)),
      status: r.status,
      notes: r.notes ?? '',
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (!form.resourceId) {
      toast.error('Pick a resource');
      return;
    }
    if (!form.memberName.trim()) {
      toast.error('Member name is required');
      return;
    }
    if (new Date(form.endAtUtc).getTime() <= new Date(form.startAtUtc).getTime()) {
      toast.error('End time must be after start time');
      return;
    }
    setSaving(true);
    try {
      const payload = {
        resourceId: form.resourceId,
        memberName: form.memberName.trim(),
        memberEmail: form.memberEmail.trim() || undefined,
        partySize: Number(form.partySize) || 1,
        startAtUtc: new Date(form.startAtUtc).toISOString(),
        endAtUtc: new Date(form.endAtUtc).toISOString(),
        status: form.status,
        notes: form.notes.trim() || undefined,
      };
      if (editing) {
        await updateReservation(editing.id, payload);
        toast.success('Reservation updated');
      } else {
        await createReservation(payload);
        toast.success('Reservation booked');
      }
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to save reservation');
    } finally {
      setSaving(false);
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
    } finally {
      setDeletingId(null);
    }
  };

  const setStatus = async (r: Reservation, status: ReservationStatus) => {
    setReservations((prev) => prev.map((x) => (x.id === r.id ? { ...x, status } : x)));
    const ok = await updateReservation(r.id, { status });
    if (!ok) {
      toast.error('Failed to update');
      void load();
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
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-teal-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center shadow-2xl shadow-emerald-500/30">
                    <CalendarClock className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Reservations</h1>
                    <p className="text-slate-400 mt-1">
                      Book dining rooms, spa, courts, and meeting spaces.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600 shadow-lg shadow-emerald-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  New booking
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<CalendarClock className="w-5 h-5 text-emerald-600" />}
              label="Today"
              value={String(stats.today)}
              hint={`${stats.totalGuestsToday} guests`}
            />
            <StatCard
              icon={<Clock className="w-5 h-5 text-teal-600" />}
              label="This week"
              value={String(stats.upcomingWeek)}
              hint="Bookings in next 7 days"
            />
            <StatCard
              icon={<Users className="w-5 h-5 text-amber-600" />}
              label="Pending"
              value={String(stats.pending)}
              hint="Awaiting confirmation"
            />
            <StatCard
              icon={<MapPin className="w-5 h-5 text-violet-600" />}
              label="Resources"
              value={String(resources.length)}
              hint="Bookable spaces"
            />
          </div>

          {/* Resource quick-look */}
          {resources.length > 0 && (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-6">
              {resources.slice(0, 8).map((r) => {
                const Icon = KIND_ICON[r.kind];
                return (
                  <button
                    type="button"
                    key={r.id}
                    onClick={() => setResourceFilter(resourceFilter === r.id ? 'All' : r.id)}
                    className={`text-left bg-white rounded-xl border p-3 shadow-sm hover:shadow-md transition ${
                      resourceFilter === r.id
                        ? 'border-emerald-400 ring-2 ring-emerald-200'
                        : 'border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-2 mb-1">
                      <div className={`w-7 h-7 rounded-lg flex items-center justify-center ${KIND_COLOR[r.kind]}`}>
                        <Icon className="w-3.5 h-3.5" />
                      </div>
                      <p className="text-sm font-medium text-slate-900 truncate">{r.name}</p>
                    </div>
                    <p className="text-xs text-slate-500">{r.kind} · capacity {r.capacity}</p>
                  </button>
                );
              })}
            </div>
          )}

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by member or resource..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select value={resourceFilter} onValueChange={setResourceFilter}>
              <SelectTrigger className="h-11 rounded-xl md:w-56">
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
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {RESERVATION_STATUSES.map((s) => (
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
              icon={CalendarClock}
              title={reservations.length === 0 ? 'No bookings yet' : 'No bookings match these filters'}
              description={
                reservations.length === 0
                  ? 'Create your first reservation to start tracking who is using which spaces.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={reservations.length === 0 ? 'New booking' : undefined}
              onAction={reservations.length === 0 ? openCreate : undefined}
              variant="teal"
            />
          ) : (
            <div className="space-y-3">
              {filtered.map((r) => {
                const today = isToday(r.startAtUtc);
                return (
                  <div
                    key={r.id}
                    className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-shadow"
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
                              {r.status === 'CheckedIn' ? 'Checked in' : r.status === 'NoShow' ? 'No-show' : r.status}
                            </span>
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
                          </div>
                          {r.notes && (
                            <p className="mt-2 text-sm text-slate-500 line-clamp-2">{r.notes}</p>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Select value={r.status} onValueChange={(v) => setStatus(r, v as ReservationStatus)}>
                          <SelectTrigger className="h-9 w-36 text-xs">
                            <SelectValue />
                          </SelectTrigger>
                          <SelectContent>
                            {RESERVATION_STATUSES.map((s) => (
                              <SelectItem key={s} value={s}>
                                {s}
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        <button
                          type="button"
                          onClick={() => openEdit(r)}
                          className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                          aria-label="Edit reservation"
                        >
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => remove(r)}
                          disabled={deletingId === r.id}
                          className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                          aria-label="Delete reservation"
                        >
                          {deletingId === r.id ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Trash2 className="h-4 w-4" />
                          )}
                        </button>
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
            <DialogTitle>{editing ? 'Edit reservation' : 'New booking'}</DialogTitle>
            <DialogDescription>Pick a resource, time, and party size.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div className="md:col-span-2">
              <Label>Resource</Label>
              <Select value={form.resourceId} onValueChange={(v) => setForm({ ...form, resourceId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="Pick a resource" />
                </SelectTrigger>
                <SelectContent>
                  {resources.map((r) => (
                    <SelectItem key={r.id} value={r.id}>
                      {r.name} ({r.kind}, cap {r.capacity})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="rsv-name">Member name</Label>
              <Input
                id="rsv-name"
                value={form.memberName}
                onChange={(e) => setForm({ ...form, memberName: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="rsv-email">Member email</Label>
              <Input
                id="rsv-email"
                type="email"
                value={form.memberEmail}
                onChange={(e) => setForm({ ...form, memberEmail: e.target.value })}
              />
            </div>
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
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {RESERVATION_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
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

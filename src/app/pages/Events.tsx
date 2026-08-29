import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarHeart,
  Plus,
  Pencil,
  Trash2,
  Search,
  MapPin,
  Users,
  Clock,
  DollarSign,
  Ticket,
  Loader2,
  ExternalLink,
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
  getEvents,
  createEvent,
  updateEvent,
  deleteEvent,
  rsvpToEvent,
  computeEventStats,
  EVENT_STATUSES,
  EVENT_VISIBILITIES,
  VISIBILITY_LABELS,
  type ClubEvent,
  type EventStatus,
  type EventVisibility,
} from '@/app/api/events';

const STATUS_BADGE: Record<EventStatus, string> = {
  Draft: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Published: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Cancelled: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Completed: 'bg-indigo-50 text-indigo-700 ring-1 ring-indigo-200',
};

const EMOJI_OPTIONS = ['🎉', '🍷', '🍣', '🥃', '🧘', '🎷', '🎨', '🏆', '🎁', '🎭', '🌅', '🎂'];

interface FormState {
  name: string;
  description: string;
  location: string;
  startAtUtc: string;
  endAtUtc: string;
  capacity: string;
  memberPrice: string;
  guestPrice: string;
  visibility: EventVisibility;
  status: EventStatus;
  coverEmoji: string;
}

function newForm(): FormState {
  const startDefault = new Date(Date.now() + 7 * 86_400_000);
  startDefault.setHours(19, 0, 0, 0);
  const endDefault = new Date(startDefault.getTime() + 2 * 3_600_000);
  return {
    name: '',
    description: '',
    location: '',
    startAtUtc: toDateTimeLocal(startDefault),
    endAtUtc: toDateTimeLocal(endDefault),
    capacity: '50',
    memberPrice: '0',
    guestPrice: '0',
    visibility: 'MembersAndGuests',
    status: 'Draft',
    coverEmoji: '🎉',
  };
}

function toDateTimeLocal(d: Date): string {
  // produce yyyy-MM-ddTHH:mm in local time for <input type="datetime-local">
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

function formatCurrency(n: number): string {
  return n === 0
    ? 'Free'
    : n.toLocaleString(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 });
}

export default function Events() {
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | EventStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editing, setEditing] = useState<ClubEvent | null>(null);
  const [form, setForm] = useState<FormState>(newForm());
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [rsvpId, setRsvpId] = useState<string | null>(null);

  const load = async () => {
    try {
      const list = await getEvents();
      setEvents(list);
    } catch {
      toast.error('Failed to load events');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeEventStats(events), [events]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return events.filter((e) => {
      if (statusFilter !== 'All' && e.status !== statusFilter) return false;
      if (!q) return true;
      return `${e.name} ${e.description ?? ''} ${e.location ?? ''}`.toLowerCase().includes(q);
    });
  }, [events, search, statusFilter]);

  const openCreate = () => {
    setEditing(null);
    setForm(newForm());
    setDialogOpen(true);
  };

  const openEdit = (e: ClubEvent) => {
    setEditing(e);
    setForm({
      name: e.name,
      description: e.description ?? '',
      location: e.location ?? '',
      startAtUtc: toDateTimeLocal(new Date(e.startAtUtc)),
      endAtUtc: toDateTimeLocal(new Date(e.endAtUtc)),
      capacity: String(e.capacity),
      memberPrice: String(e.memberPrice),
      guestPrice: String(e.guestPrice),
      visibility: e.visibility,
      status: e.status,
      coverEmoji: e.coverEmoji,
    });
    setDialogOpen(true);
  };

  const save = async () => {
    if (!form.name.trim()) {
      toast.error('Event name is required');
      return;
    }
    if (new Date(form.endAtUtc).getTime() <= new Date(form.startAtUtc).getTime()) {
      toast.error('End time must be after start time');
      return;
    }
    setSaving(true);
    try {
      const payload = {
        name: form.name.trim(),
        description: form.description.trim() || undefined,
        location: form.location.trim() || undefined,
        startAtUtc: new Date(form.startAtUtc).toISOString(),
        endAtUtc: new Date(form.endAtUtc).toISOString(),
        capacity: Number(form.capacity) || 0,
        memberPrice: Number(form.memberPrice) || 0,
        guestPrice: Number(form.guestPrice) || 0,
        visibility: form.visibility,
        status: form.status,
        coverEmoji: form.coverEmoji,
      };
      if (editing) {
        await updateEvent(editing.id, payload);
        toast.success('Event updated');
      } else {
        await createEvent(payload);
        toast.success('Event created');
      }
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to save event');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (e: ClubEvent) => {
    if (!confirm(`Delete event "${e.name}"?`)) return;
    setDeletingId(e.id);
    try {
      const ok = await deleteEvent(e.id);
      if (ok) {
        toast.success('Event deleted');
        await load();
      } else {
        toast.error('Failed to delete');
      }
    } finally {
      setDeletingId(null);
    }
  };

  const rsvp = async (e: ClubEvent) => {
    setRsvpId(e.id);
    try {
      const updated = await rsvpToEvent(e.id);
      if (updated) {
        const wasWaitlisted = updated.registeredCount === e.registeredCount;
        toast.success(wasWaitlisted ? 'Added to waitlist' : 'RSVP recorded');
        await load();
      } else {
        toast.error('Failed to record RSVP');
      }
    } finally {
      setRsvpId(null);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-pink-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-fuchsia-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-pink-500 to-fuchsia-500 flex items-center justify-center shadow-2xl shadow-pink-500/30">
                    <CalendarHeart className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Events</h1>
                    <p className="text-slate-400 mt-1">
                      Ticketing, capacity, waitlists, and member pricing.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-pink-500 to-fuchsia-500 hover:from-pink-600 hover:to-fuchsia-600 shadow-lg shadow-pink-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  New event
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<CalendarHeart className="w-5 h-5 text-pink-600" />}
              label="Upcoming"
              value={String(stats.upcoming)}
              hint={`${stats.draftCount} in draft`}
            />
            <StatCard
              icon={<Ticket className="w-5 h-5 text-fuchsia-600" />}
              label="Total RSVPs"
              value={String(stats.totalRsvps)}
              hint={`${stats.totalWaitlist} on waitlist`}
            />
            <StatCard
              icon={<DollarSign className="w-5 h-5 text-emerald-600" />}
              label="Ticket revenue"
              value={formatCurrency(stats.projectedRevenue)}
              hint="Member tier × RSVPs"
            />
            <StatCard
              icon={<Users className="w-5 h-5 text-indigo-600" />}
              label="Members-only"
              value={String(events.filter((e) => e.visibility === 'MembersOnly').length)}
              hint="Restricted events"
            />
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search events..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | EventStatus)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {EVENT_STATUSES.map((s) => (
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
              icon={CalendarHeart}
              title={events.length === 0 ? 'No events yet' : 'No events match these filters'}
              description={
                events.length === 0
                  ? 'Create your first event to start selling tickets and managing RSVPs.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={events.length === 0 ? 'New event' : undefined}
              onAction={events.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-5">
              {filtered.map((e) => {
                const capacityPct = e.capacity > 0 ? Math.min(100, (e.registeredCount / e.capacity) * 100) : 0;
                const atCapacity = e.registeredCount >= e.capacity;
                return (
                  <article
                    key={e.id}
                    className="bg-white rounded-2xl border border-slate-200 shadow-sm hover:shadow-md transition-shadow overflow-hidden"
                  >
                    <div className="relative h-28 bg-gradient-to-br from-pink-100 via-fuchsia-100 to-violet-100 flex items-center justify-center">
                      <span className="text-6xl">{e.coverEmoji}</span>
                      <div className="absolute top-3 right-3 flex gap-1.5">
                        <span className={`text-xs px-2 py-1 rounded-full font-medium ${STATUS_BADGE[e.status]}`}>
                          {e.status}
                        </span>
                      </div>
                    </div>
                    <div className="p-5">
                      <div className="flex items-start justify-between gap-2 mb-2">
                        <Link
                          to={`/events/${e.id}`}
                          className="font-semibold text-slate-900 leading-snug hover:text-pink-600 inline-flex items-start gap-1 group"
                        >
                          {e.name}
                          <ExternalLink className="w-3 h-3 mt-1 opacity-0 group-hover:opacity-100 transition" />
                        </Link>
                      </div>
                      <p className="text-xs text-slate-500 uppercase tracking-wide mb-1">
                        {VISIBILITY_LABELS[e.visibility]}
                      </p>
                      {e.description && (
                        <p className="text-sm text-slate-600 line-clamp-2 mb-3">{e.description}</p>
                      )}
                      <div className="space-y-1.5 mb-4">
                        <div className="flex items-center gap-2 text-sm text-slate-600">
                          <Clock className="w-3.5 h-3.5 text-slate-400" /> {formatDateTime(e.startAtUtc)}
                        </div>
                        {e.location && (
                          <div className="flex items-center gap-2 text-sm text-slate-600">
                            <MapPin className="w-3.5 h-3.5 text-slate-400" /> {e.location}
                          </div>
                        )}
                        <div className="flex items-center gap-2 text-sm text-slate-600">
                          <DollarSign className="w-3.5 h-3.5 text-slate-400" />
                          <span>
                            <span className="font-medium">{formatCurrency(e.memberPrice)}</span>{' '}
                            <span className="text-xs text-slate-400">member</span>
                            {e.visibility !== 'MembersOnly' && e.guestPrice > 0 && (
                              <>
                                {' '}·{' '}
                                <span className="font-medium">{formatCurrency(e.guestPrice)}</span>{' '}
                                <span className="text-xs text-slate-400">guest</span>
                              </>
                            )}
                          </span>
                        </div>
                      </div>
                      <div className="mb-3">
                        <div className="flex items-center justify-between text-xs text-slate-500 mb-1">
                          <span>
                            {e.registeredCount} / {e.capacity} registered
                          </span>
                          {e.waitlistCount > 0 && (
                            <span className="font-medium text-amber-700">+{e.waitlistCount} waitlist</span>
                          )}
                        </div>
                        <div className="h-1.5 bg-slate-100 rounded-full overflow-hidden">
                          <div
                            className={`h-full ${atCapacity ? 'bg-amber-500' : 'bg-pink-500'}`}
                            style={{ width: `${capacityPct}%` }}
                          />
                        </div>
                      </div>
                      <div className="flex items-center gap-2">
                        <Button
                          variant="outline"
                          size="sm"
                          onClick={() => rsvp(e)}
                          disabled={rsvpId === e.id || e.status !== 'Published'}
                          className="flex-1"
                        >
                          {rsvpId === e.id ? (
                            <Loader2 className="w-3.5 h-3.5 animate-spin" />
                          ) : atCapacity ? (
                            'Join waitlist'
                          ) : (
                            'RSVP'
                          )}
                        </Button>
                        <button
                          type="button"
                          onClick={() => openEdit(e)}
                          className="rounded p-2 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                          aria-label="Edit event"
                        >
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button
                          type="button"
                          onClick={() => remove(e)}
                          disabled={deletingId === e.id}
                          className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                          aria-label="Delete event"
                        >
                          {deletingId === e.id ? (
                            <Loader2 className="h-4 w-4 animate-spin" />
                          ) : (
                            <Trash2 className="h-4 w-4" />
                          )}
                        </button>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>{editing ? 'Edit event' : 'New event'}</DialogTitle>
            <DialogDescription>Capacity, pricing, visibility, and schedule.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div className="md:col-span-2">
              <Label htmlFor="evt-name">Name</Label>
              <Input
                id="evt-name"
                value={form.name}
                onChange={(e) => setForm({ ...form, name: e.target.value })}
              />
            </div>
            <div className="md:col-span-2">
              <Label htmlFor="evt-desc">Description</Label>
              <Textarea
                id="evt-desc"
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                rows={3}
              />
            </div>
            <div>
              <Label htmlFor="evt-loc">Location</Label>
              <Input
                id="evt-loc"
                value={form.location}
                onChange={(e) => setForm({ ...form, location: e.target.value })}
                placeholder="Library Bar, Rooftop, etc."
              />
            </div>
            <div>
              <Label>Cover</Label>
              <div className="flex flex-wrap gap-1.5 pt-1">
                {EMOJI_OPTIONS.map((e) => (
                  <button
                    key={e}
                    type="button"
                    onClick={() => setForm({ ...form, coverEmoji: e })}
                    className={`text-xl w-9 h-9 rounded-lg transition ${
                      form.coverEmoji === e
                        ? 'bg-pink-100 ring-2 ring-pink-400'
                        : 'bg-slate-50 hover:bg-slate-100'
                    }`}
                  >
                    {e}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <Label htmlFor="evt-start">Starts</Label>
              <Input
                id="evt-start"
                type="datetime-local"
                value={form.startAtUtc}
                onChange={(e) => setForm({ ...form, startAtUtc: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="evt-end">Ends</Label>
              <Input
                id="evt-end"
                type="datetime-local"
                value={form.endAtUtc}
                onChange={(e) => setForm({ ...form, endAtUtc: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="evt-cap">Capacity</Label>
              <Input
                id="evt-cap"
                type="number"
                min="1"
                value={form.capacity}
                onChange={(e) => setForm({ ...form, capacity: e.target.value })}
              />
            </div>
            <div>
              <Label>Visibility</Label>
              <Select
                value={form.visibility}
                onValueChange={(v) => setForm({ ...form, visibility: v as EventVisibility })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {EVENT_VISIBILITIES.map((v) => (
                    <SelectItem key={v} value={v}>
                      {VISIBILITY_LABELS[v]}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="evt-mprice">Member price ($)</Label>
              <Input
                id="evt-mprice"
                type="number"
                min="0"
                step="0.01"
                value={form.memberPrice}
                onChange={(e) => setForm({ ...form, memberPrice: e.target.value })}
              />
            </div>
            <div>
              <Label htmlFor="evt-gprice">Guest price ($)</Label>
              <Input
                id="evt-gprice"
                type="number"
                min="0"
                step="0.01"
                value={form.guestPrice}
                onChange={(e) => setForm({ ...form, guestPrice: e.target.value })}
              />
            </div>
            <div>
              <Label>Status</Label>
              <Select value={form.status} onValueChange={(v) => setForm({ ...form, status: v as EventStatus })}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {EVENT_STATUSES.map((s) => (
                    <SelectItem key={s} value={s}>
                      {s}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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
                'Create event'
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

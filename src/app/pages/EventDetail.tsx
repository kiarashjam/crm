import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
  ArrowLeft,
  CalendarHeart,
  Clock,
  MapPin,
  Users,
  Ticket,
  DollarSign,
  Plus,
  ChevronUp,
  Pencil,
  Trash2,
  Check,
  X,
  Loader2,
  AlertCircle,
  UserCheck,
  Mail,
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
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getEvents,
  updateEvent,
  deleteEvent,
  VISIBILITY_LABELS,
  type ClubEvent,
  type EventStatus,
} from '@/app/api/events';
import { createMockStore, mockId } from '@/app/api/mockStore';
import { getMembers, type Member } from '@/app/api/members';
import { addLoyaltyEntry, TIER_MULTIPLIERS } from '@/app/api/loyalty';

// Attendees live entirely in the demo store — there's no real-backend endpoint
// for them yet, but the structure is server-friendly when one shows up.

interface Attendee {
  id: string;
  eventId: string;
  memberId?: string;
  memberName: string;
  email?: string;
  ticketTier: 'Member' | 'Guest';
  status: 'Reserved' | 'CheckedIn' | 'Cancelled' | 'Waitlisted';
  rsvpAtUtc: string;
  checkedInAtUtc?: string;
}

const attendeeStore = createMockStore<Attendee>({
  storageKey: 'crm.mock.eventAttendees.v1',
  seed: [
    {
      id: 'att-seed-1',
      eventId: 'evt-seed-1',
      memberId: 'mem-seed-1',
      memberName: 'Amelia Hartwell',
      email: 'amelia.hartwell@example.com',
      ticketTier: 'Member',
      status: 'Reserved',
      rsvpAtUtc: new Date(Date.now() - 4 * 86_400_000).toISOString(),
    },
    {
      id: 'att-seed-2',
      eventId: 'evt-seed-1',
      memberId: 'mem-seed-2',
      memberName: 'Daniel Okafor',
      email: 'daniel.okafor@example.com',
      ticketTier: 'Member',
      status: 'Reserved',
      rsvpAtUtc: new Date(Date.now() - 3 * 86_400_000).toISOString(),
    },
    {
      id: 'att-seed-3',
      eventId: 'evt-seed-1',
      memberId: 'mem-seed-3',
      memberName: 'Priya Raman',
      email: 'priya.raman@example.com',
      ticketTier: 'Member',
      status: 'Reserved',
      rsvpAtUtc: new Date(Date.now() - 2 * 86_400_000).toISOString(),
    },
    {
      id: 'att-seed-4',
      eventId: 'evt-seed-2',
      memberId: 'mem-seed-1',
      memberName: 'Amelia Hartwell',
      email: 'amelia.hartwell@example.com',
      ticketTier: 'Member',
      status: 'Reserved',
      rsvpAtUtc: new Date(Date.now() - 10 * 86_400_000).toISOString(),
    },
    {
      id: 'att-seed-5',
      eventId: 'evt-seed-2',
      memberId: 'mem-seed-6',
      memberName: 'Jordan Patel',
      email: 'jordan.patel@example.com',
      ticketTier: 'Member',
      status: 'Waitlisted',
      rsvpAtUtc: new Date(Date.now() - 5 * 86_400_000).toISOString(),
    },
    {
      id: 'att-seed-6',
      eventId: 'evt-seed-5',
      memberId: 'mem-seed-1',
      memberName: 'Amelia Hartwell',
      ticketTier: 'Member',
      status: 'CheckedIn',
      rsvpAtUtc: new Date(Date.now() - 12 * 86_400_000).toISOString(),
      checkedInAtUtc: new Date(Date.now() - 6 * 86_400_000).toISOString(),
    },
  ],
  idOf: (a) => a.id,
});

const STATUS_BADGE: Record<EventStatus, string> = {
  Draft: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Published: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Cancelled: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Completed: 'bg-indigo-50 text-indigo-700 ring-1 ring-indigo-200',
};

const ATTENDEE_STATUS_BADGE: Record<Attendee['status'], string> = {
  Reserved: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  CheckedIn: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Cancelled: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
  Waitlisted: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
};

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
  return n === 0 ? 'Free' : n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

export default function EventDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [event, setEvent] = useState<ClubEvent | null>(null);
  const [attendees, setAttendees] = useState<Attendee[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [addDialog, setAddDialog] = useState(false);
  const [addForm, setAddForm] = useState({
    memberId: '',
    guestName: '',
    guestEmail: '',
    ticketTier: 'Member' as Attendee['ticketTier'],
  });
  const [busy, setBusy] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [allEvents, allMembers] = await Promise.all([getEvents(), getMembers()]);
      const found = allEvents.find((e) => e.id === id) ?? null;
      setEvent(found);
      setMembers(allMembers);
      const list = attendeeStore.list().filter((a) => a.eventId === id);
      list.sort((a, b) => Date.parse(a.rsvpAtUtc) - Date.parse(b.rsvpAtUtc));
      setAttendees(list);
    } catch {
      toast.error('Failed to load event');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, [id]);

  const reserved = useMemo(
    () => attendees.filter((a) => a.status === 'Reserved' || a.status === 'CheckedIn'),
    [attendees],
  );
  const waitlisted = useMemo(
    () => attendees.filter((a) => a.status === 'Waitlisted'),
    [attendees],
  );
  const cancelled = useMemo(
    () => attendees.filter((a) => a.status === 'Cancelled'),
    [attendees],
  );
  const checkedInCount = useMemo(
    () => attendees.filter((a) => a.status === 'CheckedIn').length,
    [attendees],
  );

  const revenue = useMemo(() => {
    if (!event) return 0;
    return reserved.reduce(
      (sum, a) => sum + (a.ticketTier === 'Member' ? event.memberPrice : event.guestPrice),
      0,
    );
  }, [event, reserved]);

  const openAdd = () => {
    setAddForm({
      memberId: members[0]?.id ?? '',
      guestName: '',
      guestEmail: '',
      ticketTier: 'Member',
    });
    setAddDialog(true);
  };

  const addAttendee = async () => {
    if (!event) return;
    const tier = addForm.ticketTier;
    let memberName: string;
    let email: string | undefined;
    let memberId: string | undefined;
    if (tier === 'Member') {
      const member = members.find((m) => m.id === addForm.memberId);
      if (!member) {
        toast.error('Pick a member');
        return;
      }
      memberName = `${member.firstName} ${member.lastName}`;
      email = member.email;
      memberId = member.id;
    } else {
      if (!addForm.guestName.trim()) {
        toast.error('Guest name is required');
        return;
      }
      memberName = addForm.guestName.trim();
      email = addForm.guestEmail.trim() || undefined;
    }
    const atCapacity = reserved.length >= event.capacity;
    setBusy(true);
    try {
      attendeeStore.add({
        id: mockId('att'),
        eventId: event.id,
        memberId,
        memberName,
        email,
        ticketTier: tier,
        status: atCapacity ? 'Waitlisted' : 'Reserved',
        rsvpAtUtc: new Date().toISOString(),
      });
      if (atCapacity) {
        await updateEvent(event.id, { waitlistCount: event.waitlistCount + 1 });
        toast.success('Added to waitlist');
      } else {
        await updateEvent(event.id, { registeredCount: event.registeredCount + 1 });
        toast.success('Attendee added');
      }
      setAddDialog(false);
      await load();
    } catch {
      toast.error('Failed to add');
    } finally {
      setBusy(false);
    }
  };

  const checkInAttendee = async (a: Attendee) => {
    if (!event) return;
    attendeeStore.update(a.id, {
      status: 'CheckedIn',
      checkedInAtUtc: new Date().toISOString(),
    });
    if (a.memberId) {
      const member = members.find((m) => m.id === a.memberId);
      if (member) {
        const points = Math.floor(50 * TIER_MULTIPLIERS[member.tier]);
        await addLoyaltyEntry({
          memberId: member.id,
          memberName: `${member.firstName} ${member.lastName}`,
          kind: 'Earned',
          reason: 'EventAttendance',
          points,
          note: event.name,
        });
        toast.success(`Checked in · +${points} loyalty points`);
      } else {
        toast.success('Checked in');
      }
    } else {
      toast.success('Checked in');
    }
    await load();
  };

  const promoteFromWaitlist = async (a: Attendee) => {
    if (!event) return;
    const atCapacity = reserved.length >= event.capacity;
    if (atCapacity) {
      toast.error('Event is full — cancel a reservation first');
      return;
    }
    attendeeStore.update(a.id, { status: 'Reserved' });
    await updateEvent(event.id, {
      registeredCount: event.registeredCount + 1,
      waitlistCount: Math.max(0, event.waitlistCount - 1),
    });
    toast.success(`${a.memberName} moved off the waitlist`);
    await load();
  };

  const cancelAttendee = async (a: Attendee) => {
    if (!event) return;
    const wasReserved = a.status === 'Reserved' || a.status === 'CheckedIn';
    const wasWaitlisted = a.status === 'Waitlisted';
    attendeeStore.update(a.id, { status: 'Cancelled' });
    if (wasReserved) {
      await updateEvent(event.id, {
        registeredCount: Math.max(0, event.registeredCount - 1),
      });
    } else if (wasWaitlisted) {
      await updateEvent(event.id, {
        waitlistCount: Math.max(0, event.waitlistCount - 1),
      });
    }
    toast.success('Cancelled');
    await load();
  };

  const removeAttendee = async (a: Attendee) => {
    if (!event) return;
    if (!confirm(`Remove ${a.memberName} from this event entirely?`)) return;
    setDeletingId(a.id);
    const wasReserved = a.status === 'Reserved' || a.status === 'CheckedIn';
    const wasWaitlisted = a.status === 'Waitlisted';
    attendeeStore.remove(a.id);
    if (wasReserved) {
      await updateEvent(event.id, {
        registeredCount: Math.max(0, event.registeredCount - 1),
      });
    } else if (wasWaitlisted) {
      await updateEvent(event.id, {
        waitlistCount: Math.max(0, event.waitlistCount - 1),
      });
    }
    setDeletingId(null);
    await load();
  };

  const changeStatus = async (status: EventStatus) => {
    if (!event) return;
    await updateEvent(event.id, { status });
    toast.success(`Event ${status.toLowerCase()}`);
    await load();
  };

  const removeEvent = async () => {
    if (!event) return;
    if (!confirm(`Delete "${event.name}"?`)) return;
    await deleteEvent(event.id);
    toast.success('Event deleted');
    navigate('/events');
  };

  if (loading) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <div className="flex items-center justify-center py-20 text-slate-400">
          <Loader2 className="h-6 w-6 animate-spin" />
        </div>
      </div>
    );
  }

  if (!event) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <main className="px-[var(--page-padding)] py-12">
          <div className="max-w-md mx-auto text-center bg-white rounded-2xl border border-slate-200 p-10 shadow-sm">
            <AlertCircle className="w-10 h-10 text-rose-500 mx-auto mb-3" />
            <h1 className="text-lg font-semibold text-slate-900">Event not found</h1>
            <Button asChild className="mt-5">
              <Link to="/events">Back to events</Link>
            </Button>
          </div>
        </main>
      </div>
    );
  }

  const capacityPct = event.capacity > 0 ? (reserved.length / event.capacity) * 100 : 0;
  const atCapacity = reserved.length >= event.capacity;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <Link
            to="/events"
            className="inline-flex items-center gap-1 text-sm text-slate-500 hover:text-slate-700 mb-4"
          >
            <ArrowLeft className="w-4 h-4" /> All events
          </Link>

          {/* Hero */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden mb-6">
            <div className="relative h-40 bg-gradient-to-br from-pink-200 via-fuchsia-200 to-violet-200 flex items-center justify-center">
              <span className="text-7xl">{event.coverEmoji}</span>
            </div>
            <div className="p-6">
              <div className="flex flex-col lg:flex-row lg:items-start gap-4">
                <div className="flex-1 min-w-0">
                  <div className="flex flex-wrap items-center gap-2 mb-2">
                    <h1 className="text-2xl font-bold text-slate-900">{event.name}</h1>
                    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[event.status]}`}>
                      {event.status}
                    </span>
                    <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">
                      {VISIBILITY_LABELS[event.visibility]}
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-600 mb-2">
                    <span className="inline-flex items-center gap-1">
                      <Clock className="w-3.5 h-3.5 text-slate-400" /> {formatDateTime(event.startAtUtc)}
                    </span>
                    {event.location && (
                      <span className="inline-flex items-center gap-1">
                        <MapPin className="w-3.5 h-3.5 text-slate-400" /> {event.location}
                      </span>
                    )}
                    <span className="inline-flex items-center gap-1">
                      <DollarSign className="w-3.5 h-3.5 text-slate-400" />
                      {formatCurrency(event.memberPrice)} member
                      {event.visibility !== 'MembersOnly' && event.guestPrice > 0 && (
                        <> · {formatCurrency(event.guestPrice)} guest</>
                      )}
                    </span>
                  </div>
                  {event.description && (
                    <p className="text-sm text-slate-600 mb-3">{event.description}</p>
                  )}
                </div>
                <div className="flex flex-wrap gap-2 shrink-0">
                  {event.status === 'Draft' && (
                    <Button onClick={() => changeStatus('Published')} className="bg-emerald-600 hover:bg-emerald-700">
                      Publish
                    </Button>
                  )}
                  {event.status === 'Published' && Date.parse(event.endAtUtc) < Date.now() && (
                    <Button onClick={() => changeStatus('Completed')}>Mark completed</Button>
                  )}
                  {event.status !== 'Cancelled' && event.status !== 'Completed' && (
                    <Button
                      variant="outline"
                      onClick={() => changeStatus('Cancelled')}
                      className="text-rose-600 hover:bg-rose-50"
                    >
                      Cancel event
                    </Button>
                  )}
                  <Button variant="outline" asChild>
                    <Link to="/events">
                      <Pencil className="w-4 h-4 mr-1.5" /> Edit
                    </Link>
                  </Button>
                  <Button variant="outline" onClick={removeEvent} className="text-rose-600 hover:bg-rose-50">
                    <Trash2 className="w-4 h-4" />
                  </Button>
                </div>
              </div>

              {/* Capacity bar */}
              <div className="mt-4">
                <div className="flex items-center justify-between text-sm text-slate-600 mb-1.5">
                  <span>
                    <strong>{reserved.length}</strong> / {event.capacity} registered ·{' '}
                    <strong>{checkedInCount}</strong> checked in
                  </span>
                  {waitlisted.length > 0 && (
                    <span className="text-amber-700 font-medium">{waitlisted.length} on waitlist</span>
                  )}
                </div>
                <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
                  <div
                    className={`h-full ${atCapacity ? 'bg-amber-500' : 'bg-pink-500'}`}
                    style={{ width: `${capacityPct}%` }}
                  />
                </div>
              </div>
            </div>
          </div>

          {/* Metrics */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <MetricCard icon={<Ticket />} label="Reserved" value={String(reserved.length)} />
            <MetricCard icon={<UserCheck />} label="Checked in" value={String(checkedInCount)} />
            <MetricCard icon={<Users />} label="Waitlist" value={String(waitlisted.length)} />
            <MetricCard icon={<DollarSign />} label="Revenue" value={formatCurrency(revenue)} />
          </div>

          {/* Attendee tabs */}
          <div className="flex items-center justify-between mb-3">
            <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2">
              <CalendarHeart className="w-4 h-4" /> Attendees
            </h2>
            <Button onClick={openAdd} size="sm">
              <Plus className="w-4 h-4 mr-1" /> Add attendee
            </Button>
          </div>

          <Tabs defaultValue="reserved">
            <TabsList className="mb-4">
              <TabsTrigger value="reserved">Reserved ({reserved.length})</TabsTrigger>
              <TabsTrigger value="waitlist">Waitlist ({waitlisted.length})</TabsTrigger>
              <TabsTrigger value="cancelled">Cancelled ({cancelled.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="reserved">
              <AttendeeList
                attendees={reserved}
                emptyText="No attendees reserved yet."
                renderActions={(a) => (
                  <>
                    {a.status === 'Reserved' && (
                      <Button size="sm" variant="outline" onClick={() => checkInAttendee(a)}>
                        <UserCheck className="w-3.5 h-3.5 mr-1" /> Check in
                      </Button>
                    )}
                    {a.status === 'CheckedIn' && a.checkedInAtUtc && (
                      <span className="text-xs text-emerald-600 inline-flex items-center gap-1">
                        <Check className="w-3 h-3" /> Checked in {formatDateTime(a.checkedInAtUtc)}
                      </span>
                    )}
                    <Button size="sm" variant="ghost" onClick={() => cancelAttendee(a)}>
                      <X className="w-3.5 h-3.5" />
                    </Button>
                  </>
                )}
                deletingId={deletingId}
                onRemove={removeAttendee}
              />
            </TabsContent>

            <TabsContent value="waitlist">
              <AttendeeList
                attendees={waitlisted}
                emptyText="Waitlist is empty."
                renderActions={(a) => (
                  <>
                    <Button size="sm" variant="outline" onClick={() => promoteFromWaitlist(a)}>
                      <ChevronUp className="w-3.5 h-3.5 mr-1" /> Promote
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => cancelAttendee(a)}>
                      <X className="w-3.5 h-3.5" />
                    </Button>
                  </>
                )}
                deletingId={deletingId}
                onRemove={removeAttendee}
              />
            </TabsContent>

            <TabsContent value="cancelled">
              <AttendeeList
                attendees={cancelled}
                emptyText="No cancellations."
                renderActions={() => null}
                deletingId={deletingId}
                onRemove={removeAttendee}
              />
            </TabsContent>
          </Tabs>
        </main>
      </PageTransition>

      <Dialog open={addDialog} onOpenChange={setAddDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Add attendee</DialogTitle>
            <DialogDescription>
              {atCapacity
                ? 'Event is at capacity — new attendees go on the waitlist.'
                : 'Add a member or guest to the reservation list.'}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div className="flex gap-2">
              {(['Member', 'Guest'] as const).map((t) => (
                <button
                  key={t}
                  type="button"
                  onClick={() => setAddForm({ ...addForm, ticketTier: t })}
                  className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition ${
                    addForm.ticketTier === t
                      ? 'bg-pink-100 text-pink-800 ring-2 ring-pink-400'
                      : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
                  }`}
                >
                  {t} · {formatCurrency(t === 'Member' ? event.memberPrice : event.guestPrice)}
                </button>
              ))}
            </div>
            {addForm.ticketTier === 'Member' ? (
              <div>
                <Label>Member</Label>
                <select
                  value={addForm.memberId}
                  onChange={(e) => setAddForm({ ...addForm, memberId: e.target.value })}
                  className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
                >
                  {members.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.tier}
                    </option>
                  ))}
                </select>
              </div>
            ) : (
              <>
                <div>
                  <Label>Guest name</Label>
                  <Input
                    value={addForm.guestName}
                    onChange={(e) => setAddForm({ ...addForm, guestName: e.target.value })}
                  />
                </div>
                <div>
                  <Label>Guest email</Label>
                  <Input
                    type="email"
                    value={addForm.guestEmail}
                    onChange={(e) => setAddForm({ ...addForm, guestEmail: e.target.value })}
                  />
                </div>
              </>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAddDialog(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={addAttendee} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Adding
                </>
              ) : atCapacity ? (
                'Add to waitlist'
              ) : (
                'Add attendee'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function MetricCard({
  icon,
  label,
  value,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-4 shadow-sm">
      <div className="flex items-center gap-2 text-xs text-slate-500 uppercase tracking-wide mb-1">
        <span className="text-slate-400">{icon}</span>
        {label}
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
    </div>
  );
}

function AttendeeList({
  attendees,
  emptyText,
  renderActions,
  deletingId,
  onRemove,
}: {
  attendees: Attendee[];
  emptyText: string;
  renderActions: (a: Attendee) => React.ReactNode;
  deletingId: string | null;
  onRemove: (a: Attendee) => void;
}) {
  if (attendees.length === 0) {
    return <EmptyState icon={CalendarHeart} title={emptyText} variant="orange" />;
  }
  return (
    <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
      {attendees.map((a) => (
        <div key={a.id} className="px-4 py-3 flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-pink-100 to-fuchsia-100 flex items-center justify-center text-pink-700 font-semibold shrink-0">
            {a.memberName
              .split(' ')
              .map((n) => n[0])
              .join('')
              .slice(0, 2)
              .toUpperCase()}
          </div>
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="font-medium text-slate-900 truncate">{a.memberName}</p>
              <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ATTENDEE_STATUS_BADGE[a.status]}`}>
                {a.status === 'CheckedIn' ? 'Checked in' : a.status}
              </span>
              <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">
                {a.ticketTier}
              </span>
            </div>
            {a.email && (
              <p className="text-xs text-slate-500 inline-flex items-center gap-1">
                <Mail className="w-3 h-3" /> {a.email}
              </p>
            )}
          </div>
          <div className="flex items-center gap-1.5 shrink-0">
            {renderActions(a)}
            <button
              type="button"
              onClick={() => onRemove(a)}
              disabled={deletingId === a.id}
              className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
              aria-label="Remove from event"
            >
              {deletingId === a.id ? (
                <Loader2 className="h-4 w-4 animate-spin" />
              ) : (
                <Trash2 className="h-4 w-4" />
              )}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import {
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
  UserX,
  Mail,
  UploadCloud,
  DoorOpen,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { RecordSkeleton } from '@/app/components/PageSkeleton';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { Button } from '@/app/components/ui/button';
import {
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  updateEvent,
  deleteEvent,
  VISIBILITY_LABELS,
  type EventStatus,
} from '@/app/api/events';
import {
  localAttendeesToUpload,
  removeEventAttendee,
  setAttendeeStatus,
  uploadLocalAttendees,
  type EventAttendee as Attendee,
} from '@/app/api/eventAttendees';
import { ApiError } from '@/app/api/apiClient';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import { NONE, useClubEvent, useClubMembers, useClubReads, useEventAttendees } from '@/app/hooks/queries';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { motion } from 'motion/react';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import RegisterDialog, { errorText } from '@/app/pages/events/RegisterDialog';
import { currentDateLocale } from '@/app/i18n/current';

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
  NoShow: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
};

const STATUS_WORDS: Record<Attendee['status'], string> = {
  Reserved: 'Expected',
  CheckedIn: 'Checked in',
  Cancelled: 'Cancelled',
  Waitlisted: 'Waitlist',
  NoShow: 'No-show',
};

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(currentDateLocale(), {
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

/** Where someone's ticket stands on the ledger, in the words the desk needs. */
function ticketWords(a: Attendee, money: (n: number) => string): string | null {
  const price = a.ticketPrice ?? 0;
  switch (a.chargeState) {
    case 'charged': return `Ticket ${money(price)} on ${a.ticketTier === 'Guest' ? `${a.hostName ?? 'the host'}'s` : 'their'} account`;
    case 'reversed': return 'Ticket taken back';
    case 'waiting': return price > 0 ? `${money(price)} when a seat comes` : null;
    case 'to-collect': return `${money(price)} to collect at the door`;
    case 'not-charged': return 'Registered before tickets were charged';
    default: return null;
  }
}

export default function EventDetail() {
  const { format: money } = useClubMoney();
  const formatCurrency = (n: number) => (n === 0 ? 'Free' : money(n));
  const { id = '' } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const reduce = useMotionPreference();
  // The event, its list and the members, through the app's cache: the door host's iPad and the
  // events manager's laptop read the same list from the server, and a failed read says so
  // instead of showing an empty one.
  const eventQuery = useClubEvent(id);
  const attendeesQuery = useEventAttendees(id);
  const membersQuery = useClubMembers();
  const reads = useClubReads(eventQuery, attendeesQuery, membersQuery);
  const event = eventQuery.data ?? null;
  const attendees: Attendee[] = attendeesQuery.data ?? NONE;
  const members = membersQuery.data ?? NONE;
  usePageLoading(reads.loading);
  const reload = reads.reload;
  const [dialog, setDialog] = useState<'add' | 'walk-in' | null>(null);
  // The person being changed, so a second press of a slow button cannot send it twice.
  const [actingOn, setActingOn] = useState<string | null>(null);
  // Attendees this browser kept before the list moved to the server.
  const [toUpload, setToUpload] = useState<Attendee[]>([]);
  const [uploading, setUploading] = useState(false);

  useEffect(() => {
    setToUpload(id ? localAttendeesToUpload(id) : []);
  }, [id]);

  const byStatus = useMemo(() => {
    const of = (status: Attendee['status']) => attendees.filter((a) => a.status === status);
    return {
      expected: of('Reserved'),
      came: of('CheckedIn'),
      waiting: of('Waitlisted').sort((a, b) => (a.waitlistPosition ?? 0) - (b.waitlistPosition ?? 0)),
      noShows: of('NoShow'),
      cancelled: of('Cancelled'),
    };
  }, [attendees]);

  // A 404 that explains itself is no such event; a bare one is a server without the route,
  // which the read state says as such.
  const notFound = (eventQuery.isError && eventQuery.error instanceof ApiError && eventQuery.error.status === 404 && eventQuery.error.hasBody)
    || (eventQuery.isSuccess && eventQuery.data === null);

  /** Runs one change to someone on the list, says how it went, and reads the list again. */
  const act = async (a: Attendee, work: () => Promise<string | null>, failure: string) => {
    if (actingOn) return;
    setActingOn(a.id);
    try {
      const said = await work();
      if (said) toast.success(said);
    } catch (err) {
      // Refused, say because another door host already checked them in or the event filled
      // up: the list is read again so this screen stops offering what is no longer there.
      toast.error(errorText(err, failure));
    } finally {
      setActingOn(null);
      await reload();
    }
  };

  const checkIn = (a: Attendee) => act(a, async () => {
    const row = await setAttendeeStatus(event!, a, 'CheckedIn');
    // The points are the server's, given once with the check-in.
    return row?.pointsAwarded ? `Checked in · +${row.pointsAwarded} loyalty points` : 'Checked in';
  }, 'Failed to check in');

  const markNoShow = (a: Attendee) => {
    const fee = event?.noShowFee ?? 0;
    const payer = a.ticketTier === 'Guest' ? a.hostName : a.memberName;
    const words = fee > 0 && payer ? ` A no-show fee of ${money(fee)} goes on ${payer}'s account.` : '';
    if (!confirm(`Mark ${a.memberName} a no-show? Their ticket stands.${words}`)) return;
    void act(a, async () => {
      await setAttendeeStatus(event!, a, 'NoShow');
      return `${a.memberName} marked a no-show`;
    }, 'Failed to mark a no-show');
  };

  const cancel = (a: Attendee) => {
    const reason = prompt(`Why is ${a.memberName}'s registration cancelled? It stays on the record.`, '');
    if (reason === null) return;
    // The server gives a freed seat to the first in line only before the event starts, while it
    // is a draft or published; after that the seat stays free for a walk-in.
    const seatGoesOn = a.status === 'Reserved' && byStatus.waiting.length > 0 && !!event
      && (event.status === 'Draft' || event.status === 'Published') && Date.parse(event.startAtUtc) > Date.now();
    void act(a, async () => {
      await setAttendeeStatus(event!, a, 'Cancelled', reason.trim() || undefined);
      const taken = a.chargeState === 'charged' ? ' Their ticket is taken off the account.' : '';
      const seat = seatGoesOn ? ' The first on the waitlist gets the seat.' : '';
      return `Cancelled.${taken}${seat}`;
    }, 'Failed to cancel');
  };

  const promote = (a: Attendee) => act(a, async () => {
    await setAttendeeStatus(event!, a, 'Reserved');
    // Someone with no address on the list is not emailed.
    return a.email ? `${a.memberName} has a seat and is emailed` : `${a.memberName} has a seat`;
  }, 'Failed to give them a seat');

  const remove = (a: Attendee) => {
    if (!confirm(`Remove ${a.memberName} from this event entirely?`)) return;
    void act(a, async () => {
      await removeEventAttendee(event!, a);
      return 'Removed from the list';
    }, 'Failed to remove');
  };

  const uploadFromThisBrowser = async () => {
    if (!event) return;
    setUploading(true);
    try {
      const { uploaded, failed: notUploaded } = await uploadLocalAttendees(event.id, toUpload);
      if (notUploaded > 0) toast.error(`${notUploaded} could not be uploaded and are still in this browser`);
      if (uploaded > 0) toast.success(`${uploaded} attendee${uploaded === 1 ? '' : 's'} uploaded for the whole team`);
    } finally {
      setUploading(false);
      setToUpload(localAttendeesToUpload(event.id));
    }
    await reload();
  };

  const changeStatus = async (status: EventStatus) => {
    if (!event) return;
    if (status === 'Cancelled' && !confirm(`Cancel "${event.name}"? Everyone on its list is cancelled, their tickets are taken off their accounts, and they are emailed.`)) return;
    try {
      await updateEvent(event.id, { status, version: event.version });
      toast.success(`Event ${status.toLowerCase()}`);
    } catch (err) {
      toast.error(errorText(err, 'Could not change the event'));
    }
    await reload();
  };

  const releaseUnnamed = async () => {
    if (!event) return;
    try {
      await updateEvent(event.id, { unnamedSeats: 0, unnamedWaitlist: 0, version: event.version });
      toast.success('Released: the seats go to whoever is waiting');
    } catch (err) {
      toast.error(errorText(err, 'Could not release the seats'));
    }
    await reload();
  };

  const removeEvent = async () => {
    if (!event) return;
    if (!confirm(`Delete "${event.name}"?`)) return;
    try {
      await deleteEvent(event.id);
      toast.success('Event deleted');
      navigate('/events');
    } catch (err) {
      // One with tickets on members' accounts is cancelled instead: the server says so.
      toast.error(errorText(err, 'Could not delete the event'));
    }
  };

  if (reads.loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <RecordSkeleton />
        </main>
      </div>
    );
  }

  if (notFound || (!event && !reads.failed)) {
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

  if (!event) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="px-[var(--page-padding)] py-12" tabIndex={-1}>
          <ClubReadState reads={reads} what="This event">{null}</ClubReadState>
        </main>
      </div>
    );
  }

  const started = Date.parse(event.startAtUtc) <= Date.now();
  const open = event.status !== 'Cancelled' && event.status !== 'Completed';
  const seatsTaken = event.registeredCount;
  const isFull = event.capacity > 0 && (event.seatsLeft ?? event.capacity - seatsTaken) <= 0;
  const capacityPct = event.capacity > 0 ? (seatsTaken / event.capacity) * 100 : 0;
  const unnamed = event.unnamedSeats ?? 0;
  const revenue = event.ticketRevenue ?? 0;

  const renderPerson = (a: Attendee) => (
    <div className="min-w-0 flex-1">
      <div className="flex flex-wrap items-center gap-2">
        <p className="font-medium text-slate-900 truncate">{a.memberName}</p>
        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${ATTENDEE_STATUS_BADGE[a.status]}`}>
          {a.status === 'Waitlisted' && a.waitlistPosition ? `Waitlist #${a.waitlistPosition}` : STATUS_WORDS[a.status]}
        </span>
        <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">{a.ticketTier}</span>
        {a.walkIn && <span className="text-xs px-1.5 py-0.5 rounded bg-teal-50 text-teal-700">Walk-in</span>}
      </div>
      <p className="text-xs text-slate-500 flex flex-wrap items-center gap-x-3 gap-y-0.5">
        {a.email && <span className="inline-flex items-center gap-1"><Mail className="w-3 h-3" /> {a.email}</span>}
        {a.hostName && <span>Guest of {a.hostName}</span>}
        {ticketWords(a, money) && <span>{ticketWords(a, money)}</span>}
        {a.noShowFeeState === 'charged' && a.noShowFee ? <span className="text-rose-700">No-show fee {money(a.noShowFee)}</span> : null}
        {a.noShowFeeState === 'reversed' && <span>No-show fee taken back</span>}
        {a.status === 'CheckedIn' && a.checkedInAtUtc && (
          <span className="text-emerald-700 inline-flex items-center gap-1">
            <Check className="w-3 h-3" /> Checked in {formatDateTime(a.checkedInAtUtc)}{a.checkedInByName ? ` by ${a.checkedInByName}` : ''}
          </span>
        )}
        {a.status === 'Cancelled' && a.cancelReason && <span>{a.cancelReason}</span>}
      </p>
    </div>
  );

  const busy = (a: Attendee) => actingOn === a.id;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <ClubReadState reads={reads} what="This event's list">
          <PageHero
            icon={CalendarHeart}
            avatar={
              <span className="flex h-12 w-12 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-pink-200 via-fuchsia-200 to-violet-200 text-2xl shadow-lg ring-4 ring-white/80 transition-transform duration-300 hover:-rotate-6 hover:scale-105 motion-reduce:transition-none" aria-hidden>
                {event.coverEmoji}
              </span>
            }
            tone="pink"
            back={{ to: '/events', label: 'All events' }}
            title={event.name}
            badges={
              <>
                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[event.status]}`}>{event.status}</span>
                <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-white/80 text-slate-600 ring-1 ring-slate-200">{VISIBILITY_LABELS[event.visibility]}</span>
              </>
            }
            subtitle={
              <span className="flex flex-wrap items-center gap-x-4 gap-y-1">
                <span className="inline-flex items-center gap-1"><Clock className="w-3.5 h-3.5" aria-hidden /> {formatDateTime(event.startAtUtc)}</span>
                {event.location && <span className="inline-flex items-center gap-1"><MapPin className="w-3.5 h-3.5" aria-hidden /> {event.location}</span>}
                <span className="inline-flex items-center gap-1">
                  <DollarSign className="w-3.5 h-3.5" aria-hidden />
                  {formatCurrency(event.memberPrice)} member
                  {event.visibility !== 'MembersOnly' && event.guestPrice > 0 && <> · {formatCurrency(event.guestPrice)} guest</>}
                  {(event.noShowFee ?? 0) > 0 && <> · no-show fee {money(event.noShowFee ?? 0)}</>}
                </span>
              </span>
            }
            actions={
              <>
                {event.status === 'Draft' && (
                  <WriteOnly permission={PERMISSION.ClubMembers}>
                    <Button onClick={() => changeStatus('Published')} className={heroPrimary('emerald')}>Publish</Button>
                  </WriteOnly>
                )}
                {event.status === 'Published' && Date.parse(event.endAtUtc) < Date.now() && (
                  <WriteOnly permission={PERMISSION.ClubMembers}>
                    <Button onClick={() => changeStatus('Completed')} className={heroPrimary('pink')}>Mark completed</Button>
                  </WriteOnly>
                )}
                {open && (
                  <WriteOnly permission={PERMISSION.ClubMembers}>
                    <Button
                      variant="outline"
                      onClick={() => changeStatus('Cancelled')}
                      className={`${heroSecondary} text-rose-600 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-700`}
                    >
                      Cancel event
                    </Button>
                  </WriteOnly>
                )}
                <WriteOnly permission={PERMISSION.ClubMembers}>
                  <Button variant="outline" asChild className={heroSecondary}>
                    <Link to="/events">
                      <Pencil className="h-4 w-4" /> Edit
                    </Link>
                  </Button>
                </WriteOnly>
                <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
                  <Button
                    variant="outline"
                    onClick={removeEvent}
                    aria-label="Delete event"
                    className={`${heroSecondary} text-rose-600 hover:border-rose-200 hover:bg-rose-50 hover:text-rose-700`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </WriteOnly>
              </>
            }
            stats={[
              { label: 'Expected', value: byStatus.expected.length, icon: Ticket, tone: 'pink', hint: event.capacity > 0 ? `of ${event.capacity}` : 'no limit' },
              { label: 'Checked in', value: byStatus.came.length, icon: UserCheck, tone: 'emerald', hint: `${event.walkInCount ?? 0} walk-in${(event.walkInCount ?? 0) === 1 ? '' : 's'}` },
              { label: 'Waitlist', value: event.waitlistCount, icon: Users, tone: 'amber' },
              { label: 'Tickets on accounts', value: revenue, icon: DollarSign, tone: 'violet', format: formatCurrency },
            ]}
          >
            {event.description && <p className="mb-3 text-sm text-slate-600">{event.description}</p>}
            {/* Capacity bar */}
            <div>
              <div className="mb-1.5 flex items-center justify-between text-sm text-slate-600">
                <span>
                  <strong>{seatsTaken}</strong> / {event.capacity > 0 ? event.capacity : 'no limit'} seats taken ·{' '}
                  <strong>{byStatus.came.length}</strong> checked in
                </span>
                {event.waitlistCount > 0 && (
                  <span className="font-medium text-amber-700">{event.waitlistCount} on waitlist</span>
                )}
              </div>
              <div className="h-2 overflow-hidden rounded-full bg-white/80 ring-1 ring-slate-200/70">
                <motion.div
                  className={`h-full rounded-full ${isFull ? 'bg-gradient-to-r from-amber-400 to-orange-500' : 'bg-gradient-to-r from-pink-500 to-fuchsia-500'}`}
                  initial={reduce ? false : { width: 0 }}
                  animate={{ width: `${Math.min(100, capacityPct)}%` }}
                  transition={{ duration: reduce ? 0 : 0.8, ease: [0.16, 1, 0.3, 1], delay: 0.2 }}
                />
              </div>
            </div>
          </PageHero>

          {unnamed > 0 && (
            <div role="status" className="mb-4 flex flex-col gap-3 rounded-xl border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 md:flex-row md:items-center md:justify-between">
              <p>
                {unnamed} seat{unnamed === 1 ? ' was' : 's were'} counted with no name on this list (an RSVP from the events list
                before names were kept, or a name still saved in another browser). {unnamed === 1 ? 'It still takes' : 'They still take'} a seat.
              </p>
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button size="sm" variant="outline" onClick={releaseUnnamed} className="shrink-0">
                  Release {unnamed === 1 ? 'it' : 'them'}
                </Button>
              </WriteOnly>
            </div>
          )}

          {toUpload.length > 0 && (
            <div
              role="status"
              className="mb-4 flex flex-col gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 md:flex-row md:items-center md:justify-between"
            >
              <p>
                <strong>{toUpload.length}</strong> attendee{toUpload.length === 1 ? ' was' : 's were'} saved only in this
                browser, before the list moved to the server. Nobody else can see {toUpload.length === 1 ? 'it' : 'them'}{' '}
                until uploaded.
              </p>
              <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                <Button size="sm" onClick={uploadFromThisBrowser} disabled={uploading} className="shrink-0">
                  {uploading ? (
                    <Loader2 className="w-4 h-4 mr-1 animate-spin" />
                  ) : (
                    <UploadCloud className="w-4 h-4 mr-1" />
                  )}
                  Upload to the server
                </Button>
              </WriteOnly>
            </div>
          )}

          {/* The list and the door, kept by whoever runs the club's members or the front desk. */}
          <div className="flex flex-wrap items-center justify-between gap-2 mb-3">
            <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2">
              <CalendarHeart className="w-4 h-4" /> Who is coming
            </h2>
            {open && (
              <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                <div className="flex gap-2">
                  {started && (
                    <Button onClick={() => setDialog('walk-in')} size="sm" variant="outline" disabled={isFull}>
                      <DoorOpen className="w-4 h-4 mr-1" /> Walk-in
                    </Button>
                  )}
                  <Button onClick={() => setDialog('add')} size="sm">
                    <Plus className="w-4 h-4 mr-1" /> Add attendee
                  </Button>
                </div>
              </WriteOnly>
            )}
          </div>

          <Tabs defaultValue="expected">
            <TabsList className="mb-4 flex-wrap h-auto">
              <TabsTrigger value="expected">Expected ({byStatus.expected.length})</TabsTrigger>
              <TabsTrigger value="came">Came ({byStatus.came.length})</TabsTrigger>
              <TabsTrigger value="waitlist">Waitlist ({byStatus.waiting.length})</TabsTrigger>
              <TabsTrigger value="noshow">No-shows ({byStatus.noShows.length})</TabsTrigger>
              <TabsTrigger value="cancelled">Cancelled ({byStatus.cancelled.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="expected">
              <AttendeeList
                attendees={byStatus.expected}
                emptyText="Nobody is expected yet."
                renderPerson={renderPerson}
                renderActions={(a) => (
                  <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                    <Button size="sm" variant="outline" onClick={() => checkIn(a)} disabled={busy(a) || event.status === 'Cancelled'}>
                      {busy(a) ? <Loader2 className="w-3.5 h-3.5 mr-1 animate-spin" /> : <UserCheck className="w-3.5 h-3.5 mr-1" />} Check in
                    </Button>
                    {started && (
                      <Button size="sm" variant="ghost" onClick={() => markNoShow(a)} disabled={busy(a)} aria-label={`Mark ${a.memberName} a no-show`}>
                        <UserX className="w-3.5 h-3.5" />
                      </Button>
                    )}
                    <Button size="sm" variant="ghost" onClick={() => cancel(a)} disabled={busy(a)} aria-label={`Cancel ${a.memberName}`}>
                      <X className="w-3.5 h-3.5" />
                    </Button>
                  </WriteOnly>
                )}
                onRemove={remove}
                busy={busy}
              />
            </TabsContent>

            <TabsContent value="came">
              <AttendeeList attendees={byStatus.came} emptyText="Nobody has been checked in yet." renderPerson={renderPerson} renderActions={() => null} busy={busy} />
            </TabsContent>

            <TabsContent value="waitlist">
              <AttendeeList
                attendees={byStatus.waiting}
                emptyText="The waitlist is empty."
                renderPerson={renderPerson}
                renderActions={(a) => (
                  <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                    <Button size="sm" variant="outline" onClick={() => promote(a)} disabled={busy(a) || isFull || !open}>
                      <ChevronUp className="w-3.5 h-3.5 mr-1" /> Give a seat
                    </Button>
                    {started && !isFull && (
                      <Button size="sm" variant="ghost" onClick={() => checkIn(a)} disabled={busy(a)} aria-label={`Check ${a.memberName} in`}>
                        <UserCheck className="w-3.5 h-3.5" />
                      </Button>
                    )}
                    <Button size="sm" variant="ghost" onClick={() => cancel(a)} disabled={busy(a)} aria-label={`Cancel ${a.memberName}`}>
                      <X className="w-3.5 h-3.5" />
                    </Button>
                  </WriteOnly>
                )}
                onRemove={remove}
                busy={busy}
              />
            </TabsContent>

            <TabsContent value="noshow">
              <AttendeeList
                attendees={byStatus.noShows}
                emptyText="No no-shows."
                renderPerson={renderPerson}
                renderActions={(a) => (
                  <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                    <Button size="sm" variant="outline" onClick={() => checkIn(a)} disabled={busy(a) || isFull}>
                      <UserCheck className="w-3.5 h-3.5 mr-1" /> Came after all
                    </Button>
                  </WriteOnly>
                )}
                busy={busy}
              />
            </TabsContent>

            <TabsContent value="cancelled">
              <AttendeeList attendees={byStatus.cancelled} emptyText="No cancellations." renderPerson={renderPerson} renderActions={() => null} onRemove={remove} busy={busy} />
            </TabsContent>
          </Tabs>
          </ClubReadState>
        </main>
      </PageTransition>

      <RegisterDialog
        event={event}
        members={members}
        open={dialog !== null}
        walkIn={dialog === 'walk-in'}
        onOpenChange={(next) => !next && setDialog(null)}
        onDone={() => reload()}
        money={money}
      />
    </div>
  );
}

function AttendeeList({
  attendees,
  emptyText,
  renderPerson,
  renderActions,
  onRemove,
  busy,
}: {
  attendees: Attendee[];
  emptyText: string;
  renderPerson: (a: Attendee) => React.ReactNode;
  renderActions: (a: Attendee) => React.ReactNode;
  /** Only for someone whose ticket is not on an account and who has not come; the server refuses the rest. */
  onRemove?: (a: Attendee) => void;
  busy: (a: Attendee) => boolean;
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
          {renderPerson(a)}
          <div className="flex items-center gap-1.5 shrink-0">
            {renderActions(a)}
            {onRemove && !a.chargeId && !a.noShowChargeId && a.status !== 'CheckedIn' && (
              <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.FrontDesk]}>
                <button
                  type="button"
                  onClick={() => onRemove(a)}
                  disabled={busy(a)}
                  className="rounded p-1.5 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                  aria-label="Remove from event"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              </WriteOnly>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}

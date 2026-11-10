// The calendar: a rep's tasks and meetings, and the club's events and reservations, on
// one month grid.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CalendarHeart,
  CalendarClock,
  CheckCircle2,
  ListTodo,
  Loader2,
  RotateCw,
  Users,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { getEvents, type ClubEvent } from '@/app/api/events';
import { getReservations, type Reservation } from '@/app/api/reservations';
import { getCalendar, type CalendarEntry } from '@/app/api/calendar';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero from '@/app/components/PageHero';
import { StaggerItem } from '@/app/components/motion/Stagger';
import { WorkScopeToggle } from '@/app/components/WorkScopeToggle';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { useModules } from '@/app/hooks/useModules';
import { useWorkScope } from '@/app/lib/workScope';
import { OUTCOME_LABELS } from '@/app/lib/activityFacts';
import { cn } from '@/app/components/ui/utils';

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function startOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}
function endOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
}
/** The six weeks on the grid for a month: from the Sunday on or before its first day. */
function gridCells(month: Date): Date[] {
  const first = startOfMonth(month);
  const start = new Date(first);
  start.setDate(first.getDate() - first.getDay());
  const cells: Date[] = [];
  for (let i = 0; i < 42; i++) {
    const d = new Date(start);
    d.setDate(start.getDate() + i);
    cells.push(d);
  }
  return cells;
}
function isSameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
}
function formatMonth(d: Date): string {
  return d.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
}
function formatTime(iso: string): string {
  try {
    return new Date(iso).toLocaleTimeString(undefined, {
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

/**
 * The local day an entry falls on. A lead's meeting date is a day with no time, so it
 * stays on that day wherever the viewer is; anything else is on the day of its time here.
 */
function dayKeyOf(entry: CalendarEntry): string {
  if (entry.allDay && entry.day) {
    const [y, m, d] = entry.day.split('-').map(Number);
    return new Date(y ?? 1970, (m ?? 1) - 1, d ?? 1).toDateString();
  }
  return new Date(entry.startUtc).toDateString();
}

const STATUS_WORDS: Record<string, string> = {
  todo: 'To do',
  in_progress: 'In progress',
  completed: 'Done',
  held: 'Held',
  missed: 'They did not come',
};
function statusWord(status?: string): string | null {
  if (!status) return null;
  return STATUS_WORDS[status] ?? OUTCOME_LABELS[status] ?? status.replace(/_/g, ' ');
}

const CHIP: Record<CalendarEntry['kind'], string> = {
  task: 'bg-amber-100 text-amber-800',
  meeting: 'bg-violet-100 text-violet-700',
  lead_meeting: 'bg-sky-100 text-sky-700',
};

export default function Calendar() {
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [clubFailed, setClubFailed] = useState(false);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [cursor, setCursor] = useState<Date>(() => startOfMonth(new Date()));
  const [selectedDay, setSelectedDay] = useState<Date>(() => new Date());
  // My tasks and meetings by default; the team's one switch away. The club's events and
  // reservations are the whole club's either way.
  const inWorkspace = !!useOrgOptional()?.currentOrgId;
  const [scope, setScope] = useWorkScope('calendar');
  const [entries, setEntries] = useState<CalendarEntry[]>([]);
  const [entriesState, setEntriesState] = useState<'loading' | 'ready' | 'failed'>('loading');
  const [truncated, setTruncated] = useState(false);

  // The club's events and reservations are the Club module's: a workspace that has turned
  // it off has none to show, and the server refuses to read them.
  const clubOn = useModules().club;

  // An outage used to leave an empty month that read as "nothing booked tonight".
  const loadClub = useCallback(async () => {
    if (!clubOn) {
      setEvents([]);
      setReservations([]);
      setClubFailed(false);
      setLoading(false);
      return;
    }
    try {
      const [e, r] = await Promise.all([getEvents(), getReservations()]);
      setEvents(e);
      setReservations(r);
      setClubFailed(false);
    } catch {
      setClubFailed(true);
    } finally {
      setLoading(false);
    }
  }, [clubOn]);

  useEffect(() => {
    void loadClub();
  }, [loadClub]);

  // Build the visible 6-week grid.
  const grid = useMemo(() => ({ cells: gridCells(cursor), monthStart: startOfMonth(cursor), monthEnd: endOfMonth(cursor) }), [cursor]);

  // Only the latest read lands: a slow answer for the month or scope just left must not
  // replace the one on show.
  const latestRead = useRef(0);
  const loadEntries = useCallback(async () => {
    const read = ++latestRead.current;
    const from = grid.cells[0]!;
    const to = new Date(grid.cells[grid.cells.length - 1]!);
    to.setDate(to.getDate() + 1);
    setEntriesState('loading');
    try {
      const page = await getCalendar(from, to, inWorkspace ? scope : 'mine');
      if (read !== latestRead.current) return;
      setEntries(page.entries);
      setTruncated(page.truncated);
      setEntriesState('ready');
    } catch {
      if (read !== latestRead.current) return;
      // What the last read held is for another month or scope: shown beside "could not
      // be loaded", it read as this one's.
      setEntries([]);
      setTruncated(false);
      setEntriesState('failed');
    }
  }, [grid, scope, inWorkspace]);

  useEffect(() => {
    void loadEntries();
  }, [loadEntries]);

  const eventsByDay = useMemo(() => {
    const m = new Map<string, ClubEvent[]>();
    for (const e of events) {
      if (e.status === 'Cancelled') continue;
      const k = new Date(e.startAtUtc).toDateString();
      if (!m.has(k)) m.set(k, []);
      m.get(k)!.push(e);
    }
    return m;
  }, [events]);

  const reservationsByDay = useMemo(() => {
    const m = new Map<string, Reservation[]>();
    for (const r of reservations) {
      if (r.status === 'Cancelled') continue;
      const k = new Date(r.startAtUtc).toDateString();
      if (!m.has(k)) m.set(k, []);
      m.get(k)!.push(r);
    }
    return m;
  }, [reservations]);

  const entriesByDay = useMemo(() => {
    const m = new Map<string, CalendarEntry[]>();
    for (const entry of entries) {
      const k = dayKeyOf(entry);
      if (!m.has(k)) m.set(k, []);
      m.get(k)!.push(entry);
    }
    // All-day first, then by time.
    for (const list of m.values()) list.sort((a, b) => Number(b.allDay) - Number(a.allDay) || a.startUtc.localeCompare(b.startUtc));
    return m;
  }, [entries]);

  const selectedKey = selectedDay.toDateString();
  const selectedEvents = useMemo(() => eventsByDay.get(selectedKey) ?? [], [eventsByDay, selectedKey]);
  const selectedReservations = useMemo(() => reservationsByDay.get(selectedKey) ?? [], [reservationsByDay, selectedKey]);
  const selectedEntries = useMemo(() => entriesByDay.get(selectedKey) ?? [], [entriesByDay, selectedKey]);
  const selectedMeetings = selectedEntries.filter((e) => e.kind !== 'task');
  const selectedTasks = selectedEntries.filter((e) => e.kind === 'task');

  // Tasks and meetings are read for the six weeks on the grid, so a selected day left off
  // it would read as having none: it moves to the same date in the month gone to.
  const goMonth = (step: number) => {
    const next = new Date(cursor.getFullYear(), cursor.getMonth() + step, 1);
    setCursor(next);
    if (!gridCells(next).some((d) => isSameDay(d, selectedDay))) {
      setSelectedDay(new Date(next.getFullYear(), next.getMonth(), Math.min(selectedDay.getDate(), endOfMonth(next).getDate())));
    }
  };
  const goPrev = () => goMonth(-1);
  const goNext = () => goMonth(1);
  const goToday = () => {
    const now = new Date();
    setCursor(startOfMonth(now));
    setSelectedDay(now);
  };

  const mine = !inWorkspace || scope === 'mine';

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
            icon={CalendarDays}
            tone="cyan"
            title="Calendar"
            subtitle={`${mine ? 'Your' : 'The team’s'} tasks and meetings${clubOn ? ', with the club’s events and reservations,' : ''} on a single grid.`}
            actions={inWorkspace ? <WorkScopeToggle value={scope} onChange={setScope} mineLabel="Mine" teamLabel="Team" /> : undefined}
          />

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Calendar grid */}
              <StaggerItem index={0} className="lg:col-span-2 bg-white rounded-2xl border border-slate-200 shadow-sm p-4">
                <div className="flex items-center justify-between mb-3">
                  <h2 className="text-lg font-semibold text-slate-900">{formatMonth(cursor)}</h2>
                  <div className="flex items-center gap-1.5">
                    <Button variant="outline" size="sm" onClick={goPrev} aria-label="Previous month">
                      <ChevronLeft className="w-4 h-4" />
                    </Button>
                    <Button variant="outline" size="sm" onClick={goToday}>
                      Today
                    </Button>
                    <Button variant="outline" size="sm" onClick={goNext} aria-label="Next month">
                      <ChevronRight className="w-4 h-4" />
                    </Button>
                  </div>
                </div>

                {(clubFailed || entriesState === 'failed') && (
                  <div role="alert" className="mb-3 flex flex-wrap items-center justify-between gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">
                    <span>
                      {clubFailed && entriesState === 'failed'
                        ? 'Nothing could be loaded: tasks, meetings, events and reservations are all missing here.'
                        : clubFailed
                          ? 'Events and reservations could not be loaded, so they are missing here.'
                          : 'Tasks and meetings could not be loaded, so they are missing here.'}
                    </span>
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => {
                        if (clubFailed) void loadClub();
                        if (entriesState === 'failed') void loadEntries();
                      }}
                    >
                      <RotateCw className="mr-1 h-3.5 w-3.5" aria-hidden="true" />
                      Try again
                    </Button>
                  </div>
                )}

                <ul className="mb-2 flex flex-wrap gap-3 text-[11px] text-slate-600" aria-label="Key">
                  <li className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-amber-400" aria-hidden="true" />Tasks</li>
                  <li className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-violet-400" aria-hidden="true" />Meetings</li>
                  <li className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-sky-400" aria-hidden="true" />Leads’ meetings</li>
                  {clubOn && (
                    <>
                      <li className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-pink-400" aria-hidden="true" />Events</li>
                      <li className="flex items-center gap-1"><span className="h-2 w-2 rounded-full bg-emerald-400" aria-hidden="true" />Reservations</li>
                    </>
                  )}
                  {entriesState === 'loading' && (
                    <li className="flex items-center gap-1 text-slate-500" role="status">
                      <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />Loading tasks and meetings
                    </li>
                  )}
                </ul>

                <div className="grid grid-cols-7 gap-1 mb-2">
                  {WEEKDAYS.map((w) => (
                    <div
                      key={w}
                      className="text-center text-xs font-semibold text-slate-500 uppercase tracking-wide py-1"
                    >
                      {w}
                    </div>
                  ))}
                </div>

                <div className="grid grid-cols-7 gap-1">
                  {grid.cells.map((d, i) => {
                    const inMonth = d.getMonth() === cursor.getMonth();
                    const isToday = isSameDay(d, new Date());
                    const isSelected = isSameDay(d, selectedDay);
                    const key = d.toDateString();
                    const evs = eventsByDay.get(key) ?? [];
                    const rsvs = reservationsByDay.get(key) ?? [];
                    const sales = entriesByDay.get(key) ?? [];
                    const chips = [
                      ...sales.map((entry) => ({
                        key: `${entry.kind}-${entry.id}`,
                        tone: CHIP[entry.kind],
                        text: entry.allDay ? entry.title : `${formatTime(entry.startUtc)} ${entry.title}`,
                        done: entry.status === 'completed',
                      })),
                      ...evs.map((e) => ({ key: `event-${e.id}`, tone: 'bg-pink-100 text-pink-700', text: `${e.coverEmoji} ${e.name}`, done: false })),
                      ...rsvs.map((r) => ({
                        key: `rsv-${r.id}`, tone: 'bg-emerald-100 text-emerald-700',
                        text: `${r.memberName.split(' ')[0]} · ${r.resourceName.split(' ')[0]}`, done: false,
                      })),
                    ];
                    return (
                      <button
                        type="button"
                        key={i}
                        onClick={() => setSelectedDay(d)}
                        aria-label={`${d.toLocaleDateString(undefined, { weekday: 'long', month: 'long', day: 'numeric' })}${chips.length ? `, ${chips.length} on the calendar` : ''}`}
                        className={`min-h-[72px] rounded-lg p-1.5 text-left transition border ${
                          isSelected
                            ? 'border-cyan-400 ring-2 ring-cyan-200 bg-cyan-50'
                            : 'border-transparent hover:bg-slate-50'
                        } ${!inMonth ? 'bg-slate-50' : ''}`}
                      >
                        <div
                          className={`text-xs font-semibold mb-1 ${
                            isToday
                              ? 'inline-flex items-center justify-center w-5 h-5 rounded-full bg-cyan-700 text-white'
                              : inMonth ? 'text-slate-700' : 'text-slate-500'
                          }`}
                        >
                          {d.getDate()}
                        </div>
                        <div className="space-y-0.5">
                          {chips.slice(0, 2).map((chip) => (
                            <div key={chip.key} className={cn('text-[10px] truncate px-1.5 py-0.5 rounded', chip.tone, chip.done && 'line-through opacity-70')}>
                              {chip.text}
                            </div>
                          ))}
                          {chips.length > 2 && (
                            <div className="text-[10px] text-slate-500">+{chips.length - 2} more</div>
                          )}
                        </div>
                      </button>
                    );
                  })}
                </div>
                {truncated && (
                  <p className="mt-2 text-xs text-slate-500">These weeks hold more than the calendar can show at once, so some tasks or meetings are missing.</p>
                )}
              </StaggerItem>

              {/* Day detail */}
              <StaggerItem index={1} className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
                <p className="text-xs uppercase tracking-wider text-slate-500">
                  {selectedDay.toLocaleDateString(undefined, { weekday: 'long' })}
                </p>
                <h2 className="text-2xl font-bold text-slate-900 mb-4">
                  {selectedDay.toLocaleDateString(undefined, {
                    month: 'long',
                    day: 'numeric',
                  })}
                </h2>

                <section className="mb-5">
                  <h3 className="text-sm font-semibold text-slate-700 inline-flex items-center gap-2 mb-2">
                    <Users className="w-4 h-4 text-violet-600" /> Meetings ({selectedMeetings.length})
                  </h3>
                  {selectedMeetings.length === 0 ? (
                    <p className="text-xs text-slate-500">No meetings.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedMeetings.map((m, index) => (
                        <StaggerItem as="li" key={`${m.kind}-${m.id}`} index={index}>
                          <Link
                            to={m.link}
                            className={cn('block rounded-lg px-3 py-2 transition', m.kind === 'lead_meeting' ? 'bg-sky-50 hover:bg-sky-100' : 'bg-violet-50 hover:bg-violet-100')}
                          >
                            <p className="text-sm font-medium text-slate-900 truncate">{m.title}</p>
                            <p className="text-xs text-slate-500">
                              {m.allDay ? 'Planned for the day' : formatTime(m.startUtc)}
                              {m.endUtc && <> – {formatTime(m.endUtc)}</>}
                              {statusWord(m.status) && <> · {statusWord(m.status)}</>}
                              {m.detail && <> · {m.detail}</>}
                              {m.who && <> · {m.who}</>}
                            </p>
                          </Link>
                        </StaggerItem>
                      ))}
                    </ul>
                  )}
                </section>

                <section className="mb-5">
                  <h3 className="text-sm font-semibold text-slate-700 inline-flex items-center gap-2 mb-2">
                    <ListTodo className="w-4 h-4 text-amber-600" /> Tasks ({selectedTasks.length})
                  </h3>
                  {selectedTasks.length === 0 ? (
                    <p className="text-xs text-slate-500">No tasks due.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedTasks.map((t, index) => (
                        <StaggerItem as="li" key={t.id} index={index}>
                          <Link to={t.link} className="flex items-center gap-2 rounded-lg bg-amber-50 px-3 py-2 transition hover:bg-amber-100">
                            {t.status === 'completed' && <CheckCircle2 className="h-4 w-4 shrink-0 text-emerald-600" aria-label="Done" />}
                            <div className="min-w-0 flex-1">
                              <p className={cn('text-sm font-medium text-slate-900 truncate', t.status === 'completed' && 'line-through opacity-70')}>{t.title}</p>
                              <p className="text-xs text-slate-500">
                                Due {formatTime(t.startUtc)}
                                {t.detail && <> · {t.detail}</>}
                                {t.who && <> · {t.who}</>}
                              </p>
                            </div>
                          </Link>
                        </StaggerItem>
                      ))}
                    </ul>
                  )}
                </section>

                {clubOn && (<>
                <section className="mb-5">
                  <h3 className="text-sm font-semibold text-slate-700 inline-flex items-center gap-2 mb-2">
                    <CalendarHeart className="w-4 h-4 text-pink-600" /> Events ({selectedEvents.length})
                  </h3>
                  {selectedEvents.length === 0 ? (
                    <p className="text-xs text-slate-500">No events.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedEvents.map((e, index) => (
                        <StaggerItem as="li" key={e.id} index={index}>
                          <Link
                            to={`/events/${e.id}`}
                            className="flex items-center gap-2 rounded-lg bg-pink-50 px-3 py-2 hover:bg-pink-100 transition"
                          >
                            <span className="text-xl">{e.coverEmoji}</span>
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium text-slate-900 truncate">{e.name}</p>
                              <p className="text-xs text-slate-500">
                                {formatTime(e.startAtUtc)} · {e.capacity > 0 ? `${e.registeredCount}/${e.capacity}` : `${e.registeredCount} registered`}
                              </p>
                            </div>
                          </Link>
                        </StaggerItem>
                      ))}
                    </ul>
                  )}
                </section>

                <section>
                  <h3 className="text-sm font-semibold text-slate-700 inline-flex items-center gap-2 mb-2">
                    <CalendarClock className="w-4 h-4 text-emerald-600" /> Reservations ({selectedReservations.length})
                  </h3>
                  {selectedReservations.length === 0 ? (
                    <p className="text-xs text-slate-500">No reservations.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedReservations.map((r, index) => (
                        <StaggerItem
                          as="li"
                          key={r.id}
                          index={index}
                          className="flex items-center gap-2 rounded-lg bg-emerald-50 px-3 py-2"
                        >
                          <div className="w-8 h-8 rounded-lg bg-emerald-100 flex items-center justify-center text-emerald-700 text-xs font-semibold shrink-0">
                            {r.memberName
                              .split(' ')
                              .map((n) => n[0])
                              .join('')
                              .slice(0, 2)
                              .toUpperCase()}
                          </div>
                          <div className="min-w-0 flex-1">
                            <p className="text-sm font-medium text-slate-900 truncate">
                              {r.resourceName}
                            </p>
                            <p className="text-xs text-slate-500">
                              {formatTime(r.startAtUtc)} · {r.memberName} · party of {r.partySize}
                            </p>
                          </div>
                        </StaggerItem>
                      ))}
                    </ul>
                  )}
                </section>
                </>)}
              </StaggerItem>
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

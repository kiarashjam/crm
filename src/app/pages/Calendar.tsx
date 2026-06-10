// Unified calendar view — events and reservations overlaid on a month grid.

import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  CalendarDays,
  ChevronLeft,
  ChevronRight,
  CalendarHeart,
  CalendarClock,
  Loader2,
} from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { getEvents, type ClubEvent } from '@/app/api/events';
import { getReservations, type Reservation } from '@/app/api/reservations';

const WEEKDAYS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

function startOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth(), 1);
}
function endOfMonth(d: Date): Date {
  return new Date(d.getFullYear(), d.getMonth() + 1, 0);
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

export default function Calendar() {
  const [events, setEvents] = useState<ClubEvent[]>([]);
  const [reservations, setReservations] = useState<Reservation[]>([]);
  const [loading, setLoading] = useState(true);
  const [cursor, setCursor] = useState<Date>(() => startOfMonth(new Date()));
  const [selectedDay, setSelectedDay] = useState<Date>(() => new Date());

  const load = async () => {
    try {
      const [e, r] = await Promise.all([getEvents(), getReservations()]);
      setEvents(e);
      setReservations(r);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  // Build the visible 6-week grid.
  const grid = useMemo(() => {
    const first = startOfMonth(cursor);
    const last = endOfMonth(cursor);
    const firstWeekday = first.getDay();
    const start = new Date(first);
    start.setDate(first.getDate() - firstWeekday);
    const cells: Date[] = [];
    for (let i = 0; i < 42; i++) {
      const d = new Date(start);
      d.setDate(start.getDate() + i);
      cells.push(d);
    }
    return { cells, monthStart: first, monthEnd: last };
  }, [cursor]);

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

  const selectedEvents = useMemo(
    () => eventsByDay.get(selectedDay.toDateString()) ?? [],
    [eventsByDay, selectedDay],
  );
  const selectedReservations = useMemo(
    () => reservationsByDay.get(selectedDay.toDateString()) ?? [],
    [reservationsByDay, selectedDay],
  );

  const goPrev = () => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() - 1, 1));
  const goNext = () => setCursor(new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1));
  const goToday = () => {
    const now = new Date();
    setCursor(startOfMonth(now));
    setSelectedDay(now);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-cyan-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-blue-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10 flex items-center gap-5">
              <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-blue-500 flex items-center justify-center shadow-2xl shadow-cyan-500/30">
                <CalendarDays className="w-8 h-8 text-white" />
              </div>
              <div>
                <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Calendar</h1>
                <p className="text-slate-400 mt-1">
                  Events and reservations across the club, on a single grid.
                </p>
              </div>
            </div>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Calendar grid */}
              <div className="lg:col-span-2 bg-white rounded-2xl border border-slate-200 shadow-sm p-4">
                <div className="flex items-center justify-between mb-3">
                  <h2 className="text-lg font-semibold text-slate-900">{formatMonth(cursor)}</h2>
                  <div className="flex items-center gap-1.5">
                    <Button variant="outline" size="sm" onClick={goPrev}>
                      <ChevronLeft className="w-4 h-4" />
                    </Button>
                    <Button variant="outline" size="sm" onClick={goToday}>
                      Today
                    </Button>
                    <Button variant="outline" size="sm" onClick={goNext}>
                      <ChevronRight className="w-4 h-4" />
                    </Button>
                  </div>
                </div>

                <div className="grid grid-cols-7 gap-1 mb-2">
                  {WEEKDAYS.map((w) => (
                    <div
                      key={w}
                      className="text-center text-xs font-semibold text-slate-400 uppercase tracking-wide py-1"
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
                    const evs = eventsByDay.get(d.toDateString()) ?? [];
                    const rsvs = reservationsByDay.get(d.toDateString()) ?? [];
                    return (
                      <button
                        type="button"
                        key={i}
                        onClick={() => setSelectedDay(d)}
                        className={`min-h-[72px] rounded-lg p-1.5 text-left transition border ${
                          isSelected
                            ? 'border-cyan-400 ring-2 ring-cyan-200 bg-cyan-50'
                            : 'border-transparent hover:bg-slate-50'
                        } ${!inMonth ? 'opacity-40' : ''}`}
                      >
                        <div
                          className={`text-xs font-semibold mb-1 ${
                            isToday
                              ? 'inline-flex items-center justify-center w-5 h-5 rounded-full bg-cyan-600 text-white'
                              : 'text-slate-700'
                          }`}
                        >
                          {d.getDate()}
                        </div>
                        <div className="space-y-0.5">
                          {evs.slice(0, 2).map((e) => (
                            <div
                              key={e.id}
                              className="text-[10px] truncate px-1.5 py-0.5 rounded bg-pink-100 text-pink-700"
                            >
                              {e.coverEmoji} {e.name}
                            </div>
                          ))}
                          {rsvs.slice(0, 2 - Math.min(2, evs.length)).map((r) => (
                            <div
                              key={r.id}
                              className="text-[10px] truncate px-1.5 py-0.5 rounded bg-emerald-100 text-emerald-700"
                            >
                              {r.memberName.split(' ')[0]} · {r.resourceName.split(' ')[0]}
                            </div>
                          ))}
                          {evs.length + rsvs.length > 2 && (
                            <div className="text-[10px] text-slate-400">
                              +{evs.length + rsvs.length - 2} more
                            </div>
                          )}
                        </div>
                      </button>
                    );
                  })}
                </div>
              </div>

              {/* Day detail */}
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
                <p className="text-xs uppercase tracking-wider text-slate-400">
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
                    <CalendarHeart className="w-4 h-4 text-pink-600" /> Events ({selectedEvents.length})
                  </h3>
                  {selectedEvents.length === 0 ? (
                    <p className="text-xs text-slate-400">No events.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedEvents.map((e) => (
                        <li key={e.id}>
                          <Link
                            to={`/events/${e.id}`}
                            className="flex items-center gap-2 rounded-lg bg-pink-50 px-3 py-2 hover:bg-pink-100 transition"
                          >
                            <span className="text-xl">{e.coverEmoji}</span>
                            <div className="min-w-0 flex-1">
                              <p className="text-sm font-medium text-slate-900 truncate">{e.name}</p>
                              <p className="text-xs text-slate-500">
                                {formatTime(e.startAtUtc)} · {e.registeredCount}/{e.capacity}
                              </p>
                            </div>
                          </Link>
                        </li>
                      ))}
                    </ul>
                  )}
                </section>

                <section>
                  <h3 className="text-sm font-semibold text-slate-700 inline-flex items-center gap-2 mb-2">
                    <CalendarClock className="w-4 h-4 text-emerald-600" /> Reservations ({selectedReservations.length})
                  </h3>
                  {selectedReservations.length === 0 ? (
                    <p className="text-xs text-slate-400">No reservations.</p>
                  ) : (
                    <ul className="space-y-1.5">
                      {selectedReservations.map((r) => (
                        <li
                          key={r.id}
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
                        </li>
                      ))}
                    </ul>
                  )}
                </section>
              </div>
            </div>
          )}
        </main>
      </PageTransition>
    </div>
  );
}

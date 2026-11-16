// The club's figures, summed on the server (GET /api/club-reports and /today).
//
// Club Reports and Today at the Club used to download every member, charge, visit and
// loyalty entry and add them up here. Revenue was what fully paid charges came to, so
// dues paid in three instalments showed nothing for two months, everything was labelled
// in dollars, and one failed request left a dashboard of zeros. The server now sums them
// from the ledger, per currency, on the workspace's calendar; these are its answers.
//
// Demo mode has no server: the same shapes are worked out from the demo's own records,
// roughly, so the demo's pages show something that agrees with its other pages.

import { authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { getCharges, getPayments, isSale } from './charges';
import { getGiftCards } from './giftCards';
import { getLoyaltyLedger, POINTS_PER_DOLLAR } from './loyalty';
import { getSubscriptions } from './subscriptions';
import { getEvents } from './events';
import { getMembers } from './members';
import { getVisits } from './visits';
import { getReservations } from './reservations';
import { getApplications } from './applications';
import { getTabs } from './tabs';
import { downloadFile } from '@/app/lib/exportCsv';
import { DEFAULT_CLUB_CURRENCY } from '@/app/lib/clubMoney';

export type ClubReportGrain = 'day' | 'week' | 'month';

export interface ClubReportPeriod {
  /** Its first day on the workspace's calendar, yyyy-MM-dd. */
  start: string;
  startUtc: string;
  endUtc: string;
}

export interface ClubPeriodAmount { in: number; out: number; net: number }

/** Money in and back out in one currency. Cash: received and paid back. Accrual: charged and voided. */
export interface ClubMoneyFlow extends ClubPeriodAmount {
  currency: string;
  /** In the order of the report's periods. */
  byPeriod: ClubPeriodAmount[];
}

export interface ClubKindRevenue { currency: string; kind: string; charged: number; voided: number; net: number }

export interface ClubAgeing {
  currency: string;
  current: number;
  days1To30: number;
  days31To60: number;
  days61To90: number;
  over90: number;
  total: number;
  openCharges: number;
}

export interface ClubGiftCardsOutstanding { currency: string; outstanding: number; cards: number }

export interface ClubPointsOutstanding { points: number; members: number; pointsPerUnit: number; value: number; currency: string }

export interface ClubMrr { currency: string; monthly: number; yearly: number; subscriptions: number }

export interface ClubEventRates {
  events: number;
  limitedEvents: number;
  capacity: number;
  seatsTaken: number;
  fillRate: number | null;
  ended: number;
  checkedIn: number;
  noShows: number;
  notMarked: number;
  noShowRate: number | null;
}

export interface ClubSpender { memberId: string; memberName: string; currency: string; amount: number }

export interface ClubCount { name: string; count: number }

export interface ClubReport {
  /** The workspace's currency: house accounts are kept in it. Lists put it first. */
  currency: string;
  timeZone: string;
  fromUtc: string;
  toUtc: string;
  /** When the figures that stand at a moment were read: the end of the period, or now. */
  asOfUtc: string;
  grain: ClubReportGrain;
  periods: ClubReportPeriod[];
  cash: ClubMoneyFlow[];
  accrual: ClubMoneyFlow[];
  accrualByKind: ClubKindRevenue[];
  receivables: ClubAgeing[];
  giftCards: ClubGiftCardsOutstanding[];
  points: ClubPointsOutstanding;
  mrr: ClubMrr[];
  events: ClubEventRates;
  topSpenders: ClubSpender[];
  members: { active: number; lapsed: number; activeByTier: ClubCount[]; joined: number; joinedByPeriod: number[] };
  visits: { checkIns: number; byVenue: ClubCount[] };
}

export interface ClubCurrencyAmount { currency: string; amount: number }

export interface ClubToday {
  /** Today on the workspace's calendar, yyyy-MM-dd. */
  date: string;
  timeZone: string;
  currency: string;
  checkIns: number;
  reservations: { count: number; items: { id: string; memberName: string; resourceName: string; partySize: number; startAtUtc: string; status: string }[] };
  openTabs: { count: number; totals: ClubCurrencyAmount[]; items: { id: string; memberName: string; venue: string; items: number; total: number; currency: string }[] };
  applications: { count: number; items: { id: string; firstName: string; lastName: string; requestedTier: string; occupation?: string | null; status: string }[] };
  pastDue: {
    charges: number;
    owed: ClubCurrencyAmount[];
    chargeItems: { id: string; memberName: string; description: string; owed: number; currency: string; dueAtUtc: string }[];
    subscriptions: number;
    subscriptionItems: { id: string; memberName: string; name: string; amount: number; currency: string }[];
  };
  eventsThisWeek: { count: number; items: { id: string; name: string; coverEmoji: string; startAtUtc: string; capacity: number; registeredCount: number }[] };
  anniversaries: { memberId: string; firstName: string; lastName: string; tier: string; date: string; years: number }[];
}

export interface ClubReportRange {
  fromUtc: string;
  toUtc: string;
  grain?: ClubReportGrain;
}

function rangeQuery(range: ClubReportRange): string {
  const q = new URLSearchParams({ from: range.fromUtc, to: range.toUtc });
  if (range.grain) q.set('grain', range.grain);
  return q.toString();
}

export async function getClubReport(range: ClubReportRange, req?: RequestOptions): Promise<ClubReport> {
  if (servesDemoData()) return demoReport(range);
  return authFetchJson<ClubReport>(`/api/club-reports?${rangeQuery(range)}`, { signal: req?.signal });
}

export async function getClubToday(req?: RequestOptions): Promise<ClubToday> {
  if (servesDemoData()) return demoToday();
  return authFetchJson<ClubToday>('/api/club-reports/today', { signal: req?.signal });
}

/** The report as the server's spreadsheet (one figure per row; formulas kept as text). */
export async function downloadClubReportCsv(range: ClubReportRange): Promise<void> {
  if (servesDemoData()) throw new Error('The demo has no server to write the spreadsheet; sign in to a workspace to export it.');
  const res = await authFetch(`/api/club-reports/export?${rangeQuery(range)}`);
  await ensureOk(res);
  const header = res.headers.get('Content-Disposition') ?? '';
  const name = /filename="?([^";]+)"?/i.exec(header)?.[1] ?? 'club-report.csv';
  downloadFile(await res.blob(), name);
}

// ── demo mode ────────────────────────────────────────────────────────────────

const DAY = 86_400_000;

function localDay(d: Date): string {
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
}

/** The periods in this browser's calendar: the demo has no workspace time zone. */
function demoPeriods(from: Date, to: Date, grain: ClubReportGrain): ClubReportPeriod[] {
  const periods: ClubReportPeriod[] = [];
  let cursor = new Date(from.getFullYear(), from.getMonth(), grain === 'month' ? 1 : from.getDate());
  if (grain === 'week') cursor = new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() - ((cursor.getDay() + 6) % 7));
  while (cursor < to && periods.length < 400) {
    const next = grain === 'month'
      ? new Date(cursor.getFullYear(), cursor.getMonth() + 1, 1)
      : new Date(cursor.getFullYear(), cursor.getMonth(), cursor.getDate() + (grain === 'week' ? 7 : 1));
    if (next > from) {
      periods.push({
        start: localDay(cursor),
        startUtc: (cursor < from ? from : cursor).toISOString(),
        endUtc: (next > to ? to : next).toISOString(),
      });
    }
    cursor = next;
  }
  return periods;
}

function demoGrain(from: Date, to: Date, grain?: ClubReportGrain): ClubReportGrain {
  if (grain) return grain;
  const days = (to.getTime() - from.getTime()) / DAY;
  return days <= 62 ? 'day' : days <= 182 ? 'week' : 'month';
}

function bucket(periods: ClubReportPeriod[], at: string): number {
  const t = Date.parse(at);
  return periods.findIndex((p) => t >= Date.parse(p.startUtc) && t < Date.parse(p.endUtc));
}

function flow(currency: string, periods: ClubReportPeriod[], entries: { at: string; amount: number }[]): ClubMoneyFlow {
  const byPeriod = periods.map(() => ({ in: 0, out: 0, net: 0 }));
  for (const e of entries) {
    const i = bucket(periods, e.at);
    if (i < 0) continue;
    const slot = byPeriod[i]!;
    if (e.amount >= 0) slot.in += e.amount;
    else slot.out += -e.amount;
    slot.net = slot.in - slot.out;
  }
  const totalIn = byPeriod.reduce((s, p) => s + p.in, 0);
  const totalOut = byPeriod.reduce((s, p) => s + p.out, 0);
  return { currency, in: totalIn, out: totalOut, net: totalIn - totalOut, byPeriod };
}

async function demoReport(range: ClubReportRange): Promise<ClubReport> {
  const from = new Date(range.fromUtc);
  const to = new Date(range.toUtc);
  const grain = demoGrain(from, to, range.grain);
  const periods = demoPeriods(from, to, grain);
  const currency = DEFAULT_CLUB_CURRENCY;
  const asOf = new Date(Math.min(to.getTime(), Date.now()));
  const [charges, payments, cards, ledger, subscriptions, events, members, visits] = await Promise.all([
    getCharges(), getPayments(), getGiftCards(), getLoyaltyLedger(), getSubscriptions(), getEvents(), getMembers(), getVisits(),
  ]);
  const sales = charges.filter((c) => isSale(c) && c.status !== 'Voided');
  const cash = payments.length ? [flow(currency, periods, payments.map((p) => ({ at: p.receivedAtUtc, amount: p.amount - (p.refundedAmount ?? 0) })))] : [];
  const accrual = sales.length ? [flow(currency, periods, sales.map((c) => ({ at: c.postedAtUtc, amount: c.amount })))] : [];
  const inRange = sales.filter((c) => bucket(periods, c.postedAtUtc) >= 0);
  const kinds = new Map<string, number>();
  for (const c of inRange) kinds.set(c.kind, (kinds.get(c.kind) ?? 0) + c.amount);
  const ageing: ClubAgeing = { currency, current: 0, days1To30: 0, days31To60: 0, days61To90: 0, over90: 0, total: 0, openCharges: 0 };
  for (const c of charges) {
    const owed = c.amount - c.paidAmount;
    if (c.status === 'Voided' || owed <= 0) continue;
    const late = (asOf.getTime() - Date.parse(c.dueAtUtc ?? c.postedAtUtc)) / DAY;
    if (late <= 0) ageing.current += owed;
    else if (late <= 30) ageing.days1To30 += owed;
    else if (late <= 60) ageing.days31To60 += owed;
    else if (late <= 90) ageing.days61To90 += owed;
    else ageing.over90 += owed;
    ageing.total += owed;
    ageing.openCharges += 1;
  }
  const live = cards.filter((c) => c.status === 'Active' && c.remainingValue > 0 && (!c.expiresAtUtc || Date.parse(c.expiresAtUtc) > asOf.getTime()));
  const held = new Map<string, number>();
  for (const e of ledger) held.set(e.memberId, (held.get(e.memberId) ?? 0) + e.points);
  const holders = [...held.values()].filter((p) => p > 0);
  const points = holders.reduce((s, p) => s + p, 0);
  const months: Record<string, number> = { Monthly: 1, Quarterly: 3, SemiAnnual: 6, Annual: 12 };
  const running = subscriptions.filter((s) => s.status === 'Active' || s.status === 'PastDue');
  const monthly = running.reduce((s, sub) => s + sub.amount / (months[sub.cadence] ?? 1), 0);
  const eventsInRange = events.filter((e) => (e.status === 'Published' || e.status === 'Completed') && bucket(periods, e.startAtUtc) >= 0);
  const limited = eventsInRange.filter((e) => e.capacity > 0);
  const capacity = limited.reduce((s, e) => s + e.capacity, 0);
  const taken = limited.reduce((s, e) => s + e.registeredCount, 0);
  const ended = eventsInRange.filter((e) => Date.parse(e.endAtUtc) < Date.now());
  const checkedIn = ended.reduce((s, e) => s + (e.checkedInCount ?? 0), 0);
  const noShows = ended.reduce((s, e) => s + (e.noShowCount ?? 0), 0);
  const spend = new Map<string, ClubSpender>();
  for (const c of inRange) {
    const id = c.incurredByMemberId ?? c.memberId;
    const row = spend.get(id) ?? { memberId: id, memberName: c.incurredByName ?? c.memberName, currency, amount: 0 };
    row.amount += c.amount;
    spend.set(id, row);
  }
  const tiers = new Map<string, number>();
  for (const m of members) if (m.status === 'Active') tiers.set(m.tier, (tiers.get(m.tier) ?? 0) + 1);
  const joinedByPeriod = periods.map(() => 0);
  for (const m of members) {
    const i = bucket(periods, m.joinedAtUtc);
    if (i >= 0) joinedByPeriod[i]! += 1;
  }
  const venues = new Map<string, number>();
  for (const v of visits) if (bucket(periods, v.visitedAtUtc) >= 0) venues.set(v.venue, (venues.get(v.venue) ?? 0) + 1);
  const counted = (map: Map<string, number>): ClubCount[] =>
    [...map.entries()].map(([name, count]) => ({ name, count })).sort((a, b) => b.count - a.count || a.name.localeCompare(b.name));
  return {
    currency, timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, fromUtc: range.fromUtc, toUtc: range.toUtc, asOfUtc: asOf.toISOString(),
    grain, periods, cash, accrual,
    accrualByKind: [...kinds.entries()].map(([kind, net]) => ({ currency, kind, charged: net, voided: 0, net })).sort((a, b) => b.net - a.net),
    receivables: ageing.openCharges ? [ageing] : [],
    giftCards: live.length ? [{ currency, outstanding: live.reduce((s, c) => s + c.remainingValue, 0), cards: live.length }] : [],
    points: { points, members: holders.length, pointsPerUnit: POINTS_PER_DOLLAR, value: Math.round((points / POINTS_PER_DOLLAR) * 100) / 100, currency },
    mrr: running.length ? [{ currency, monthly: Math.round(monthly * 100) / 100, yearly: Math.round(monthly * 1200) / 100, subscriptions: running.length }] : [],
    events: {
      events: eventsInRange.length, limitedEvents: limited.length, capacity, seatsTaken: taken,
      fillRate: capacity > 0 ? taken / capacity : null, ended: ended.length, checkedIn, noShows, notMarked: 0,
      noShowRate: checkedIn + noShows > 0 ? noShows / (checkedIn + noShows) : null,
    },
    topSpenders: [...spend.values()].filter((s) => s.amount > 0).sort((a, b) => b.amount - a.amount).slice(0, 8),
    members: {
      active: members.filter((m) => m.status === 'Active').length,
      lapsed: members.filter((m) => m.status === 'Lapsed').length,
      activeByTier: counted(tiers),
      joined: joinedByPeriod.reduce((s, n) => s + n, 0),
      joinedByPeriod,
    },
    visits: { checkIns: [...venues.values()].reduce((s, n) => s + n, 0), byVenue: counted(venues) },
  };
}

async function demoToday(): Promise<ClubToday> {
  const now = new Date();
  const start = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const end = new Date(start.getTime() + DAY);
  const isToday = (iso: string) => {
    const t = Date.parse(iso);
    return t >= start.getTime() && t < end.getTime();
  };
  const [visits, reservations, tabs, applications, charges, subscriptions, events, members] = await Promise.all([
    getVisits(), getReservations(), getTabs(), getApplications(), getCharges(), getSubscriptions(), getEvents(), getMembers(),
  ]);
  const currency = DEFAULT_CLUB_CURRENCY;
  const todays = reservations.filter((r) => isToday(r.startAtUtc) && r.status !== 'Cancelled').sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc));
  const open = tabs.filter((t) => t.status === 'Open');
  const waiting = applications.filter((a) => a.status === 'Submitted' || a.status === 'UnderReview');
  const overdue = charges.filter((c) => c.status !== 'Voided' && c.dueAtUtc && Date.parse(c.dueAtUtc) < now.getTime() && c.amount - c.paidAmount > 0);
  const pastDueSubs = subscriptions.filter((s) => s.status === 'PastDue');
  const week = events.filter((e) => e.status === 'Published' && Date.parse(e.startAtUtc) >= now.getTime() && Date.parse(e.startAtUtc) < now.getTime() + 7 * DAY)
    .sort((a, b) => Date.parse(a.startAtUtc) - Date.parse(b.startAtUtc));
  const anniversaries = members.flatMap((m) => {
    if (m.status !== 'Active') return [];
    const joined = new Date(m.joinedAtUtc);
    for (const year of [start.getFullYear(), start.getFullYear() + 1]) {
      const on = new Date(year, joined.getMonth(), Math.min(joined.getDate(), new Date(year, joined.getMonth() + 1, 0).getDate()));
      if (on < start || on.getTime() >= start.getTime() + 14 * DAY) continue;
      const years = year - joined.getFullYear();
      return years >= 1 ? [{ memberId: m.id, firstName: m.firstName, lastName: m.lastName, tier: m.tier, date: localDay(on), years }] : [];
    }
    return [];
  }).sort((a, b) => a.date.localeCompare(b.date));
  const owed = overdue.reduce((s, c) => s + c.amount - c.paidAmount, 0);
  return {
    date: localDay(start), timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone, currency,
    checkIns: visits.filter((v) => isToday(v.visitedAtUtc)).length,
    reservations: { count: todays.length, items: todays.slice(0, 8).map((r) => ({ id: r.id, memberName: r.memberName, resourceName: r.resourceName, partySize: r.partySize, startAtUtc: r.startAtUtc, status: r.status })) },
    openTabs: {
      count: open.length,
      totals: open.length ? [{ currency, amount: open.reduce((s, t) => s + t.total, 0) }] : [],
      items: open.slice(0, 8).map((t) => ({ id: t.id, memberName: t.memberName, venue: t.venue, items: t.items.length, total: t.total, currency })),
    },
    applications: { count: waiting.length, items: waiting.slice(0, 6).map((a) => ({ id: a.id, firstName: a.firstName, lastName: a.lastName, requestedTier: a.requestedTier, occupation: a.occupation, status: a.status })) },
    pastDue: {
      charges: overdue.length,
      owed: overdue.length ? [{ currency, amount: owed }] : [],
      chargeItems: overdue.slice(0, 4).map((c) => ({ id: c.id, memberName: c.memberName, description: c.description, owed: c.amount - c.paidAmount, currency, dueAtUtc: c.dueAtUtc! })),
      subscriptions: pastDueSubs.length,
      subscriptionItems: pastDueSubs.slice(0, 4).map((s) => ({ id: s.id, memberName: s.memberName, name: s.name, amount: s.amount, currency })),
    },
    eventsThisWeek: { count: week.length, items: week.slice(0, 6).map((e) => ({ id: e.id, name: e.name, coverEmoji: e.coverEmoji, startAtUtc: e.startAtUtc, capacity: e.capacity, registeredCount: e.registeredCount })) },
    anniversaries,
  };
}

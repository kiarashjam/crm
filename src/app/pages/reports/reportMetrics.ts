// Every number the Reports page shows, as pure functions.
//
// This module exists because the maths used to live inline in a `useMemo` inside
// the component, which meant none of it could be tested and several figures were
// simply wrong. The bugs it fixes, all of them things a user would notice:
//
//   · "Qualified" was counted with `status.includes('qualified')`, which matched
//     "Unqualified" — and then matched nothing at all after the status rename, so
//     the number silently became a permanent zero.
//   · Deal values in different currencies were added together and the total was
//     labelled USD. For an organisation billing in CHF every money figure was
//     both wrong and looked broken.
//   · The weighted forecast invented a 20% probability for deals that had none,
//     presenting a guess as analysis.
//   · The funnel could WIDEN, because its steps were independent filters rather
//     than nested sets.
//   · Activity was bucketed by UTC day but labelled with the local day, so
//     evening work landed on the wrong date.
//
// Rules for anything added here: pure in, pure out; never invent a value to fill
// a gap — report the gap instead; and if a chart implies a relationship between
// its numbers (a funnel narrows, a breakdown totals 100%), enforce that in code
// rather than hoping the data cooperates.

import type { Deal, Lead, Activity } from '@/app/api/types';
import {
  CONTACTED_OR_BEYOND, QUALIFIED_OR_BEYOND, SIGNED_STATUSES, statusIn,
} from '../leads/leadStatusSync';

// ── Money ────────────────────────────────────────────────────────────────────

/** Digits out of a free-text money field. Never NaN. */
export function parseMoney(v?: string | null): number {
  if (!v) return 0;
  const n = parseFloat(String(v).replace(/[^0-9.-]/g, ''));
  return Number.isFinite(n) ? n : 0;
}

export interface CurrencyTotal {
  currency: string;
  value: number;
  count: number;
}

/**
 * Deal value totalled PER CURRENCY, largest first.
 *
 * Deliberately does not convert. There is no exchange rate in the system, and
 * inventing one would produce a number that looks authoritative and is wrong.
 * Callers show the dominant currency and disclose that others exist.
 */
export function totalsByCurrency(deals: Deal[]): CurrencyTotal[] {
  const m = new Map<string, CurrencyTotal>();
  for (const d of deals) {
    const currency = (d.currency || 'USD').toUpperCase();
    const e = m.get(currency) ?? { currency, value: 0, count: 0 };
    e.value += parseMoney(d.value);
    e.count += 1;
    m.set(currency, e);
  }
  return [...m.values()].sort((a, b) => b.value - a.value);
}

/** The currency most of the money is in — what a single headline figure means. */
export function dominantCurrency(deals: Deal[]): string {
  return totalsByCurrency(deals)[0]?.currency ?? 'USD';
}

// ── Deals ────────────────────────────────────────────────────────────────────

export type DealState = 'won' | 'lost' | 'open';

export function dealState(d: Deal): DealState {
  if (d.isWon) return 'won';
  if (d.closedAtUtc || d.closedReasonCategory) return 'lost';
  return 'open';
}

export interface Forecast {
  /** Probability-weighted value of open deals THAT HAVE a probability. */
  value: number;
  /** How many open deals carried a probability. */
  covered: number;
  /** How many did not, and are therefore absent from `value`. */
  missing: number;
}

/**
 * Weighted forecast over open deals, counting only those with a probability set.
 *
 * The old code substituted 20% for a missing probability, which turned "we do not
 * know" into a confident number. Reporting the coverage instead makes an empty
 * forecast legible as "nobody has set probabilities" — which is actionable —
 * rather than as a suspiciously round figure.
 */
export function forecast(deals: Deal[], currency?: string): Forecast {
  const open = deals.filter((d) => dealState(d) === 'open'
    && (!currency || (d.currency || 'USD').toUpperCase() === currency));
  let value = 0;
  let covered = 0;
  for (const d of open) {
    if (typeof d.probability === 'number' && Number.isFinite(d.probability)) {
      value += parseMoney(d.value) * (d.probability / 100);
      covered += 1;
    }
  }
  return { value, covered, missing: open.length - covered };
}

export interface DealSummary {
  currency: string;
  /** True when deals exist in more than one currency; headline figures are
   *  then for `currency` only and the rest are disclosed separately. */
  mixedCurrency: boolean;
  otherCurrencies: CurrencyTotal[];
  openValue: number;
  openCount: number;
  wonValue: number;
  wonCount: number;
  lostCount: number;
  /** Won ÷ (won + lost), or null when nothing has closed yet. */
  winRate: number | null;
  /** Mean value of WON deals only, or null when none have been won. */
  avgWonValue: number | null;
  forecast: Forecast;
}

export function summariseDeals(deals: Deal[]): DealSummary {
  const currency = dominantCurrency(deals);
  const all = totalsByCurrency(deals);
  const inCurrency = deals.filter((d) => (d.currency || 'USD').toUpperCase() === currency);

  const open = inCurrency.filter((d) => dealState(d) === 'open');
  const won = inCurrency.filter((d) => dealState(d) === 'won');
  // Won/lost counts are currency-independent facts, so they use every deal.
  const wonAll = deals.filter((d) => dealState(d) === 'won');
  const lostAll = deals.filter((d) => dealState(d) === 'lost');

  const closed = wonAll.length + lostAll.length;
  const wonValue = won.reduce((s, d) => s + parseMoney(d.value), 0);

  return {
    currency,
    mixedCurrency: all.length > 1,
    otherCurrencies: all.filter((c) => c.currency !== currency),
    openValue: open.reduce((s, d) => s + parseMoney(d.value), 0),
    openCount: open.length,
    wonValue,
    wonCount: wonAll.length,
    lostCount: lostAll.length,
    winRate: closed > 0 ? wonAll.length / closed : null,
    // Only ever the won average. The old code silently fell back to the OPEN
    // average, so the tile changed meaning without changing its label.
    avgWonValue: won.length > 0 ? wonValue / won.length : null,
    forecast: forecast(deals, currency),
  };
}

// ── Leads ────────────────────────────────────────────────────────────────────

/**
 * How far a lead has got, as a rank. This is what makes the funnel a funnel:
 * each step counts `rank >= k`, so the steps are nested sets by construction and
 * a later step can never exceed an earlier one.
 */
export function leadRank(l: Lead): number {
  if (l.isConverted) return 4;
  if (statusIn(l.status, SIGNED_STATUSES)) return 3;
  if (statusIn(l.status, QUALIFIED_OR_BEYOND)) return 2;
  if (statusIn(l.status, CONTACTED_OR_BEYOND)) return 1;
  return 0;
}

export interface FunnelStep {
  name: string;
  value: number;
  /** Share of the total, 0–1. */
  share: number;
}

const FUNNEL_LABELS = ['All leads', 'Contacted', 'Qualified', 'Signed', 'Converted'];

/**
 * Status funnel, guaranteed non-increasing.
 *
 * Note it measures CURRENT status, so a lead that was contacted and later marked
 * lost counts only at "All leads". That is the honest reading of status data —
 * status has no memory. The lifecycle funnel in the sales-tracker section is the
 * one that measures how far leads actually got, because it reads the pipeline
 * fields, which do.
 */
export function statusFunnel(leads: Lead[]): FunnelStep[] {
  const total = leads.length;
  const ranks = leads.map(leadRank);
  return FUNNEL_LABELS.map((name, k) => {
    const value = ranks.filter((r) => r >= k).length;
    return { name, value, share: total > 0 ? value / total : 0 };
  });
}

export interface LeadSummary {
  total: number;
  converted: number;
  /** Converted ÷ total, or null with no leads. */
  conversionRate: number | null;
  qualifiedOrBeyond: number;
  contactedOrBeyond: number;
  /** Mean lead score across leads that HAVE a score, or null if none do. */
  avgScore: number | null;
  scoredCount: number;
  unscoredCount: number;
}

export function summariseLeads(leads: Lead[]): LeadSummary {
  const total = leads.length;
  const scored = leads.filter((l) => typeof l.leadScore === 'number' && Number.isFinite(l.leadScore));
  const converted = leads.filter((l) => l.isConverted).length;
  return {
    total,
    converted,
    conversionRate: total > 0 ? converted / total : null,
    qualifiedOrBeyond: leads.filter((l) => statusIn(l.status, QUALIFIED_OR_BEYOND) || l.isConverted).length,
    contactedOrBeyond: leads.filter((l) => leadRank(l) >= 1).length,
    avgScore: scored.length > 0
      ? scored.reduce((s, l) => s + (l.leadScore ?? 0), 0) / scored.length
      : null,
    scoredCount: scored.length,
    unscoredCount: total - scored.length,
  };
}

// ── Grouping ─────────────────────────────────────────────────────────────────

export interface GroupRow {
  name: string;
  value: number;
  count: number;
}

export function groupSum<T>(rows: T[], key: (r: T) => string, val: (r: T) => number): GroupRow[] {
  const m = new Map<string, GroupRow>();
  for (const r of rows) {
    const k = key(r);
    const e = m.get(k) ?? { name: k, value: 0, count: 0 };
    e.value += val(r);
    e.count += 1;
    m.set(k, e);
  }
  return [...m.values()].sort((a, b) => b.value - a.value);
}

/**
 * Top N, with everything else folded into a single "Other" row.
 *
 * The old code sliced to the top 6 and dropped the tail, so a pie chart of lead
 * sources did not add up to the number of leads. Any breakdown drawn as a share
 * of a whole has to actually contain the whole.
 */
export function topNWithOther(rows: GroupRow[], n: number, otherLabel = 'Other'): GroupRow[] {
  if (rows.length <= n) return rows;
  const head = rows.slice(0, n);
  const tail = rows.slice(n);
  return [...head, {
    name: otherLabel,
    value: tail.reduce((s, r) => s + r.value, 0),
    count: tail.reduce((s, r) => s + r.count, 0),
  }];
}

// ── Time series ──────────────────────────────────────────────────────────────

/**
 * Local calendar day as `YYYY-MM-DD`.
 *
 * NOT `toISOString().slice(0,10)`, which is UTC: east of Greenwich that pushes
 * late-evening records into the next day, and west of it pulls early-morning
 * records into the previous one. The old chart bucketed in UTC and labelled in
 * local time, so the two disagreed by a day at the edges.
 */
export function localDayKey(d: Date): string {
  const m = String(d.getMonth() + 1).padStart(2, '0');
  const day = String(d.getDate()).padStart(2, '0');
  return `${d.getFullYear()}-${m}-${day}`;
}

export interface SeriesPoint {
  name: string;
  value: number;
}

/** Activity counts for the last `days` local days, oldest first. */
export function activityByDay(activities: Activity[], days: number, now = new Date()): SeriesPoint[] {
  const counts = new Map<string, number>();
  for (const a of activities) {
    const t = Date.parse(a.createdAt);
    if (Number.isNaN(t)) continue;
    const k = localDayKey(new Date(t));
    counts.set(k, (counts.get(k) ?? 0) + 1);
  }
  const out: SeriesPoint[] = [];
  for (let i = days - 1; i >= 0; i -= 1) {
    // Step by calendar day rather than by 86_400_000ms, so a daylight-saving
    // change does not skip or duplicate a bucket.
    const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() - i);
    out.push({
      name: d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      value: counts.get(localDayKey(d)) ?? 0,
    });
  }
  return out;
}

/** New leads per week for the last `weeks` weeks, oldest first. */
export function newLeadsByWeek(leads: Lead[], weeks: number, now = new Date()): SeriesPoint[] {
  const WEEK = 7 * 86_400_000;
  const end0 = now.getTime();
  const out: SeriesPoint[] = [];
  for (let i = weeks - 1; i >= 0; i -= 1) {
    const end = end0 - i * WEEK;
    const start = end - WEEK;
    const value = leads.filter((l) => {
      const t = Date.parse(l.createdAtUtc || '');
      return !Number.isNaN(t) && t >= start && t < end;
    }).length;
    out.push({
      name: new Date(end).toLocaleDateString(undefined, { month: 'short', day: 'numeric' }),
      value,
    });
  }
  return out;
}

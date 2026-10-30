// What the Reports page still works out itself, as pure functions.
//
// Every money figure, count and time series now comes from the server
// (/api/reporting/summary), which adds deals up in SQL. The browser used to do
// that from whole tables with its own money parser, which read "€50.000" as 50
// and disagreed with the Dashboard; that code is gone. What is left is shaping
// the server's rows for display:
//
//   · The status funnel. The server counts leads per status; which statuses mean
//     "contacted" or "qualified" is the workspace's lead vocabulary, which lives in
//     leadStatusSync with the rest of the status rules. A funnel cannot widen,
//     because each step counts the leads at that rank or beyond.
//   · Top-N breakdowns that keep an "Other" row, so a breakdown still totals.
//
// Rules for anything added here: pure in, pure out; never invent a value to fill
// a gap — report the gap instead.

import type { Lead } from '@/app/api/types';
import type { LeadStatusRow, MoneyRow } from '@/app/api/reporting';
import {
  CONTACTED_OR_BEYOND, QUALIFIED_OR_BEYOND, SIGNED_STATUSES, statusIn,
} from '../leads/leadStatusSync';

// ── Leads ────────────────────────────────────────────────────────────────────

/** How far a lead with this status has got, as a rank: 0 new … 4 converted. */
export function leadRankOf(status: string | undefined, isConverted: boolean): number {
  if (isConverted) return 4;
  if (statusIn(status, SIGNED_STATUSES)) return 3;
  if (statusIn(status, QUALIFIED_OR_BEYOND)) return 2;
  if (statusIn(status, CONTACTED_OR_BEYOND)) return 1;
  return 0;
}

export function leadRank(l: Lead): number {
  return leadRankOf(l.status, Boolean(l.isConverted));
}

export interface FunnelStep {
  name: string;
  value: number;
  /** Share of the total, 0–1. */
  share: number;
}

const FUNNEL_LABELS = ['All leads', 'Contacted', 'Qualified', 'Signed', 'Converted'];

/**
 * Status funnel from the server's counts per status, guaranteed non-increasing.
 *
 * It measures CURRENT status, so a lead that was contacted and later marked lost
 * counts only at "All leads". That is the honest reading of status data — status
 * has no memory. The lifecycle funnel in the sales-tracker section is the one
 * that measures how far leads actually got.
 */
export function statusFunnelFromCounts(rows: LeadStatusRow[]): FunnelStep[] {
  const atRank = [0, 0, 0, 0, 0];
  for (const r of rows) {
    atRank[4]! += r.converted;
    atRank[leadRankOf(r.status, false)]! += r.count - r.converted;
  }
  const total = atRank.reduce((s, n) => s + n, 0);
  return FUNNEL_LABELS.map((name, k) => {
    const value = atRank.slice(k).reduce((s, n) => s + n, 0);
    return { name, value, share: total > 0 ? value / total : 0 };
  });
}

/** The same funnel from lead records, as demo data and tests have them. */
export function statusFunnel(leads: Lead[]): FunnelStep[] {
  const rows = new Map<string, LeadStatusRow>();
  for (const l of leads) {
    const row = rows.get(l.status) ?? { status: l.status, count: 0, converted: 0 };
    row.count += 1;
    if (l.isConverted) row.converted += 1;
    rows.set(l.status, row);
  }
  return statusFunnelFromCounts([...rows.values()]);
}

export interface StatusSummary {
  total: number;
  contactedOrBeyond: number;
  qualifiedOrBeyond: number;
  converted: number;
}

/** How many of the counted leads are contacted or qualified or beyond, by the workspace's vocabulary. */
export function summariseStatuses(rows: LeadStatusRow[]): StatusSummary {
  let total = 0;
  let contacted = 0;
  let qualified = 0;
  let converted = 0;
  for (const r of rows) {
    total += r.count;
    converted += r.converted;
    const open = r.count - r.converted;
    const rank = leadRankOf(r.status, false);
    // A converted lead has got past every step.
    contacted += r.converted + (rank >= 1 ? open : 0);
    qualified += r.converted + (rank >= 2 ? open : 0);
  }
  return { total, contactedOrBeyond: contacted, qualifiedOrBeyond: qualified, converted };
}

// ── Money rows ───────────────────────────────────────────────────────────────

/** The row for one currency, or an empty one. */
export function rowFor(rows: MoneyRow[], currency: string | null): MoneyRow {
  return rows.find((r) => r.currency === currency) ?? { currency: currency ?? 'USD', count: 0, value: 0, unreadable: 0 };
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

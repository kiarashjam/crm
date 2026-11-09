// The board for demo mode, worked out in the browser from the deals kept here.
//
// The server works the board out in SQL (DealBoardService). Demo deals live in this
// browser, so the same shape is built here by the same rules: the default stages by
// name, money per currency and never across currencies, a deal's own probability for
// the weighted value (demo stages have none), stale after the default 30 quiet days, and
// the filters narrowing the columns while the summary stays the whole pipeline's.

import type { Deal, DealBoard, DealBoardColumn, DealBoardQuery } from './types';
import type { MoneyRow, WeightedRow } from './reporting';
import { FALLBACK_STAGES } from '@/app/pages/pipeline/config';
import { currencyOf, roundCents } from '@/app/lib/money';

/** The stale days of a workspace nobody has changed; the server's default too. */
export const DEFAULT_STALE_DAYS = 30;

const UNSET = 'unset';
const DAY_MS = 24 * 60 * 60 * 1000;

interface DemoColumn {
  key: string;
  name: string;
  displayOrder: number;
  isWon: boolean;
  isLost: boolean;
}

const COLUMNS: DemoColumn[] = FALLBACK_STAGES.map((name, i) => ({
  key: name, name, displayOrder: i, isWon: name === 'Closed Won', isLost: name === 'Closed Lost',
}));
const UNSET_COLUMN: DemoColumn = { key: UNSET, name: 'No stage', displayOrder: Number.MAX_SAFE_INTEGER, isWon: false, isLost: false };

function columnOf(deal: Deal): DemoColumn {
  const name = (deal.stage ?? '').trim().toUpperCase();
  return COLUMNS.find((c) => c.name.toUpperCase() === name) ?? UNSET_COLUMN;
}

function isOpen(deal: Deal, column: DemoColumn): boolean {
  return deal.isWon === undefined && !column.isWon && !column.isLost;
}

/** The last time anything happened to the deal: made, or worked on. */
function idleSince(deal: Deal): string | undefined {
  const times = [deal.createdAtUtc, deal.lastActivityAtUtc].filter((t): t is string => !!t);
  return times.sort().pop();
}

function withPace(deal: Deal, column: DemoColumn, now: Date): Deal {
  const idle = idleSince(deal);
  const stale = !!idle && isOpen(deal, column) && now.getTime() - new Date(idle).getTime() > DEFAULT_STALE_DAYS * DAY_MS;
  return { ...deal, idleSinceUtc: idle, isStale: stale, staleAfterDays: DEFAULT_STALE_DAYS };
}

function moneyRows(deals: Deal[], fallback: string): MoneyRow[] {
  const rows = new Map<string, MoneyRow>();
  for (const d of deals) {
    const currency = currencyOf(d, fallback);
    const row = rows.get(currency) ?? { currency, count: 0, value: 0, unreadable: 0 };
    row.count += 1;
    if (typeof d.amount === 'number' && Number.isFinite(d.amount)) row.value = roundCents(row.value + d.amount);
    else row.unreadable += 1;
    rows.set(currency, row);
  }
  return [...rows.values()].sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
}

function weightedRows(open: Deal[], fallback: string): WeightedRow[] {
  const rows = new Map<string, WeightedRow>();
  for (const d of open) {
    const currency = currencyOf(d, fallback);
    const row = rows.get(currency) ?? { currency, value: 0, covered: 0, missing: 0 };
    if (d.probability === undefined || d.probability === null) row.missing += 1;
    else if (typeof d.amount === 'number') {
      row.value = roundCents(row.value + (d.amount * d.probability) / 100);
      row.covered += 1;
    }
    rows.set(currency, row);
  }
  return [...rows.values()].sort((a, b) => b.value - a.value || a.currency.localeCompare(b.currency));
}

function dayOf(iso?: string): string {
  return iso ? iso.slice(0, 10) : '';
}

function narrowed(deals: Deal[], query: DealBoardQuery, me: string | undefined, now: Date): Deal[] {
  const words = query.search?.trim().toLowerCase();
  return deals.filter((d) => {
    if (words && ![d.name, d.value, d.companyName, d.contactName, d.assigneeName].some((t) => t?.toLowerCase().includes(words))) return false;
    if (query.mine && (!me || d.assigneeId !== me)) return false;
    if (query.unassigned && d.assigneeId) return false;
    if (query.assigneeId && d.assigneeId !== query.assigneeId) return false;
    if (query.minAmount !== undefined && !(typeof d.amount === 'number' && d.amount >= query.minAmount)) return false;
    if (query.maxAmount !== undefined && !(typeof d.amount === 'number' && d.amount <= query.maxAmount)) return false;
    if (query.closeFrom && !(d.expectedCloseDateUtc && dayOf(d.expectedCloseDateUtc) >= query.closeFrom)) return false;
    if (query.closeTo && !(d.expectedCloseDateUtc && dayOf(d.expectedCloseDateUtc) <= query.closeTo)) return false;
    if (query.staleOnly && !withPace(d, columnOf(d), now).isStale) return false;
    return true;
  });
}

function sorted(deals: Deal[], sort: DealBoardQuery['sort']): Deal[] {
  const byName = (a: Deal, b: Deal) => a.name.localeCompare(b.name) || a.id.localeCompare(b.id);
  const copy = [...deals];
  switch (sort) {
    case 'value':
      return copy.sort((a, b) => (b.amount ?? -Infinity) - (a.amount ?? -Infinity) || byName(a, b));
    case 'created':
      return copy.sort((a, b) => (b.createdAtUtc ?? '').localeCompare(a.createdAtUtc ?? '') || a.id.localeCompare(b.id));
    case 'name':
      return copy.sort(byName);
    default:
      return copy.sort((a, b) => {
        const x = dayOf(a.expectedCloseDateUtc);
        const y = dayOf(b.expectedCloseDateUtc);
        if (!x !== !y) return x ? -1 : 1;
        return x.localeCompare(y) || byName(a, b);
      });
  }
}

/** The demo deals of one column, in the board's order, from the first `skip`. */
export function demoBoardColumn(
  all: Deal[], query: DealBoardQuery, key: string, skip: number, me?: string, now: Date = new Date(),
): { deals: Deal[]; hasMore: boolean } {
  const take = Math.min(Math.max(query.take ?? 25, 1), 100);
  const column = key === UNSET ? UNSET_COLUMN : COLUMNS.find((c) => c.key.toLowerCase() === key.toLowerCase());
  if (!column) return { deals: [], hasMore: false };
  const inColumn = sorted(narrowed(all, query, me, now).filter((d) => columnOf(d) === column), query.sort);
  return {
    deals: inColumn.slice(skip, skip + take).map((d) => withPace(d, column, now)),
    hasMore: inColumn.length > skip + take,
  };
}

/** The demo board: the default stages, their first cards, and the figures. */
export function demoBoard(all: Deal[], query: DealBoardQuery, fallbackCurrency: string, me?: string, now: Date = new Date()): DealBoard {
  const take = Math.min(Math.max(query.take ?? 25, 1), 100);
  const shown = narrowed(all, query, me, now);
  const columns: DealBoardColumn[] = [];
  for (const column of [...COLUMNS, UNSET_COLUMN]) {
    const deals = sorted(shown.filter((d) => columnOf(d) === column), query.sort);
    if (column === UNSET_COLUMN && deals.length === 0) continue;
    const closes = column.isWon || column.isLost;
    const paced = deals.map((d) => withPace(d, column, now));
    columns.push({
      key: column.key,
      name: column.name,
      displayOrder: column.displayOrder,
      isWon: column.isWon,
      isLost: column.isLost,
      count: deals.length,
      totals: moneyRows(deals, fallbackCurrency),
      weighted: closes ? [] : weightedRows(deals.filter((d) => d.isWon === undefined), fallbackCurrency),
      stale: paced.filter((d) => d.isStale).length,
      deals: paced.slice(0, take),
      hasMore: deals.length > take,
    });
  }

  const outcome = (d: Deal) => {
    if (d.isWon === true) return 'won';
    if (d.isWon === false) return 'lost';
    const column = columnOf(d);
    return column.isWon ? 'won' : column.isLost ? 'lost' : 'open';
  };
  const open = all.filter((d) => outcome(d) === 'open');
  const won = all.filter((d) => outcome(d) === 'won');
  const lost = all.filter((d) => outcome(d) === 'lost');
  const today = now.toISOString().slice(0, 10);
  const weekOut = new Date(now.getTime() + 7 * DAY_MS).toISOString().slice(0, 10);
  return {
    staleAfterDays: DEFAULT_STALE_DAYS,
    columns,
    count: shown.length,
    summary: {
      total: all.length,
      open: open.length,
      won: won.length,
      lost: lost.length,
      openTotals: moneyRows(open, fallbackCurrency),
      wonTotals: moneyRows(won, fallbackCurrency),
      weighted: weightedRows(open, fallbackCurrency),
      winRate: won.length + lost.length > 0 ? won.length / (won.length + lost.length) : undefined,
      overdue: open.filter((d) => d.expectedCloseDateUtc && dayOf(d.expectedCloseDateUtc) < today).length,
      closingThisWeek: open.filter((d) => {
        const day = dayOf(d.expectedCloseDateUtc);
        return day !== '' && day >= today && day <= weekOut;
      }).length,
      stale: open.filter((d) => withPace(d, columnOf(d), now).isStale).length,
    },
  };
}

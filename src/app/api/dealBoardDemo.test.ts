// The demo board is built in the browser by the server's rules: the default stages by
// name, money per currency, a deal's own probability for the weighted value, stale after
// 30 quiet days, filters that narrow the columns and leave the figures alone, and a
// page of cards per column.
import { describe, expect, it } from 'vitest';
import type { Deal } from './types';
import { demoBoard, demoBoardColumn } from './dealBoardDemo';

const NOW = new Date('2026-11-09T12:00:00Z');
const daysAgo = (n: number) => new Date(NOW.getTime() - n * 24 * 60 * 60 * 1000).toISOString();

const deal = (over: Partial<Deal>): Deal => ({
  id: over.name ?? Math.random().toString(36).slice(2), name: 'Deal', value: '0', amount: 0, currency: 'CHF',
  stage: 'Qualification', createdAtUtc: daysAgo(1), ...over,
});

const deals: Deal[] = [
  deal({ name: 'Alpine Club', amount: 1000, probability: 10, expectedCloseDateUtc: '2026-11-12T00:00:00Z' }),
  deal({ name: 'Bernese Bank', amount: 2000, currency: 'EUR', probability: 50, createdAtUtc: daysAgo(40), assigneeId: 'me' }),
  deal({ name: 'Club dinner', amount: 500, stage: 'proposal ', createdAtUtc: daysAgo(40), lastActivityAtUtc: daysAgo(2) }),
  deal({ name: 'Old win', amount: 3000, stage: 'Closed Won', createdAtUtc: daysAgo(90) }),
  deal({ name: 'No idea', amount: null, stage: undefined }),
];

describe('the demo board', () => {
  it('groups deals into the default stages by name and totals each currency apart', () => {
    const board = demoBoard(deals, {}, 'CHF', 'me', NOW);
    const qualification = board.columns.find((c) => c.name === 'Qualification')!;

    expect(board.columns.map((c) => c.key)).toEqual(
      ['Qualification', 'Proposal', 'Negotiation', 'Contract Sent', 'Contract Signed', 'Closed Won', 'Closed Lost', 'unset']);
    expect(qualification.count).toBe(2);
    expect(qualification.totals).toEqual([
      { currency: 'EUR', count: 1, value: 2000, unreadable: 0 },
      { currency: 'CHF', count: 1, value: 1000, unreadable: 0 },
    ]);
    expect(qualification.weighted.find((w) => w.currency === 'CHF')?.value).toBe(100);
    expect(board.columns.find((c) => c.name === 'Proposal')!.deals.map((d) => d.name)).toEqual(['Club dinner']);
    expect(board.columns.find((c) => c.key === 'unset')!.totals[0]).toMatchObject({ count: 1, unreadable: 1 });
  });

  it('flags an open deal nobody has touched for 30 days, and never a won one', () => {
    const board = demoBoard(deals, {}, 'CHF', 'me', NOW);
    const cards = board.columns.flatMap((c) => c.deals);

    expect(cards.find((d) => d.name === 'Bernese Bank')?.isStale).toBe(true);
    expect(cards.find((d) => d.name === 'Club dinner')?.isStale).toBe(false);
    expect(cards.find((d) => d.name === 'Old win')?.isStale).toBe(false);
    expect(board.summary.stale).toBe(1);
  });

  it('narrows the columns with the filters and keeps the figures the whole pipeline', () => {
    const board = demoBoard(deals, { mine: true }, 'CHF', 'me', NOW);

    expect(board.count).toBe(1);
    expect(board.summary.total).toBe(5);
    expect(board.summary.won).toBe(1);
    expect(board.summary.winRate).toBe(1);
    expect(demoBoard(deals, { staleOnly: true }, 'CHF', 'me', NOW).count).toBe(1);
    expect(demoBoard(deals, { minAmount: 900, maxAmount: 2500 }, 'CHF', 'me', NOW).count).toBe(2);
  });

  it('serves a column a page at a time, in the order asked for', () => {
    const many = Array.from({ length: 5 }, (_, i) => deal({ name: `Deal ${i}`, amount: (i + 1) * 100 }));
    const board = demoBoard(many, { take: 2, sort: 'value' }, 'CHF', 'me', NOW);
    const qualification = board.columns.find((c) => c.name === 'Qualification')!;

    expect(qualification.deals.map((d) => d.name)).toEqual(['Deal 4', 'Deal 3']);
    expect(qualification.hasMore).toBe(true);
    const next = demoBoardColumn(many, { take: 2, sort: 'value' }, 'Qualification', 2, 'me', NOW);
    expect(next.deals.map((d) => d.name)).toEqual(['Deal 2', 'Deal 1']);
    expect(next.hasMore).toBe(true);
  });
});

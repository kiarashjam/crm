// "Recent Activity" was not recent. It took the first five items the API returned
// and labelled them recent, which on screen read Aug 17, Aug 18, Aug 16, Aug 14,
// Aug 18. The bug is invisible unless you check the dates against each other, so
// these tests check exactly that.

import { describe, it, expect } from 'vitest';
import type { Activity } from '@/app/api/types';
import { mostRecent } from './dashboardData';

const act = (id: string, createdAt: string | undefined): Activity => ({
  id,
  type: 'call',
  subject: `activity ${id}`,
  createdAt: createdAt as string,
});

describe('mostRecent', () => {
  it('puts the newest first, whatever order they arrived in', () => {
    // The exact sequence the screenshot showed.
    const out = mostRecent([
      act('a', '2026-08-17T10:00:00Z'),
      act('b', '2026-08-18T10:00:00Z'),
      act('c', '2026-08-16T10:00:00Z'),
      act('d', '2026-08-14T10:00:00Z'),
      act('e', '2026-08-18T18:00:00Z'),
    ], 5);
    expect(out.map((a) => a.id)).toEqual(['e', 'b', 'a', 'c', 'd']);
  });

  it('sorts BEFORE slicing, so the newest is never cut', () => {
    // The original bug in one assertion: today's activity arrived sixth, so
    // slicing first dropped it and kept five older ones.
    const out = mostRecent([
      act('old1', '2026-01-01T00:00:00Z'),
      act('old2', '2026-01-02T00:00:00Z'),
      act('old3', '2026-01-03T00:00:00Z'),
      act('old4', '2026-01-04T00:00:00Z'),
      act('old5', '2026-01-05T00:00:00Z'),
      act('today', '2026-08-19T09:00:00Z'),
    ], 5);
    expect(out[0]!.id).toBe('today');
    expect(out).toHaveLength(5);
  });

  it('is a total order, so the list cannot reshuffle between renders', () => {
    const same = '2026-08-18T10:00:00Z';
    const first = mostRecent([act('b', same), act('a', same), act('c', same)], 3);
    const second = mostRecent([act('c', same), act('b', same), act('a', same)], 3);
    expect(first.map((a) => a.id)).toEqual(second.map((a) => a.id));
  });

  it('drops records with no readable date instead of dating them 1970', () => {
    // Sorting an unparseable date as 0 would park it at the bottom of the list
    // looking like the oldest thing in the CRM.
    const out = mostRecent([
      act('good', '2026-08-18T10:00:00Z'),
      act('nodate', undefined),
      act('bad', 'not a date'),
    ], 5);
    expect(out.map((a) => a.id)).toEqual(['good']);
  });

  it('never returns more than asked for, and copes with fewer', () => {
    expect(mostRecent([act('a', '2026-08-18T10:00:00Z')], 5)).toHaveLength(1);
    expect(mostRecent([], 5)).toEqual([]);
    expect(mostRecent([act('a', '2026-08-18T10:00:00Z')], 0)).toEqual([]);
    expect(mostRecent([act('a', '2026-08-18T10:00:00Z')], -1)).toEqual([]);
  });

  it('does not mutate the array it was given', () => {
    // It receives the same array the page keeps in state; sorting it in place
    // would silently reorder that state too.
    const input = [
      act('a', '2026-08-14T10:00:00Z'),
      act('b', '2026-08-18T10:00:00Z'),
    ];
    const before = input.map((a) => a.id);
    mostRecent(input, 2);
    expect(input.map((a) => a.id)).toEqual(before);
  });
});

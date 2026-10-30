import { describe, it, expect, beforeEach } from 'vitest';
import {
  addMonths, bucketLabel, customPeriod, dateInputValue, loadSavedFilters, monthLabel, monthOf, periodFor, saveFilters,
  toReportFilters, DEFAULT_FILTERS,
} from './reportFilters';

const ZURICH = 'Europe/Zurich';
const NEW_YORK = 'America/New_York';
const iso = (d: Date) => d.toISOString();

describe('periodFor', () => {
  const now = new Date('2026-10-30T14:00:00Z'); // 30 October 2026, afternoon in Zurich

  it("starts and ends periods at the workspace's midnight, across the clocks going back", () => {
    const p = periodFor('this-month', ZURICH, now);
    // Midnight on the 1st in summer time, and on the 1st of November in winter time.
    expect([iso(p.from), iso(p.to)]).toEqual(['2026-09-30T22:00:00.000Z', '2026-10-31T23:00:00.000Z']);
  });

  it('cuts the month at the club when it has begun there but not here', () => {
    // Half past midnight on 1 November in Zurich; still October in UTC and New York.
    const late = new Date('2026-10-31T23:30:00Z');
    expect(iso(periodFor('this-month', ZURICH, late).from)).toBe('2026-10-31T23:00:00.000Z');
    expect(iso(periodFor('this-month', NEW_YORK, late).from)).toBe('2026-10-01T04:00:00.000Z');
    expect(iso(periodFor('this-month', 'UTC', late).from)).toBe('2026-10-01T00:00:00.000Z');
  });

  it('knows last month, and quarters across a year end', () => {
    expect(dateInputValue(periodFor('last-month', ZURICH, now).from, ZURICH)).toBe('2026-09-01');
    const quarter = periodFor('this-quarter', ZURICH, now);
    expect([dateInputValue(quarter.from, ZURICH), dateInputValue(quarter.to, ZURICH)]).toEqual(['2026-10-01', '2027-01-01']);
    const january = new Date('2027-01-15T12:00:00Z');
    const last = periodFor('last-quarter', ZURICH, january);
    expect([dateInputValue(last.from, ZURICH), dateInputValue(last.to, ZURICH)]).toEqual(['2026-10-01', '2027-01-01']);
    expect(dateInputValue(periodFor('last-year', ZURICH, january).from, ZURICH)).toBe('2026-01-01');
  });

  it('includes today in the last N days', () => {
    const p = periodFor('last-30', ZURICH, now);
    expect([dateInputValue(p.from, ZURICH), dateInputValue(p.to, ZURICH)]).toEqual(['2026-10-01', '2026-10-31']);
  });
});

describe('customPeriod', () => {
  it('includes both days typed', () => {
    const p = customPeriod('2026-10-05', '2026-10-05', ZURICH)!;
    expect([iso(p.from), iso(p.to)]).toEqual(['2026-10-04T22:00:00.000Z', '2026-10-05T22:00:00.000Z']);
  });

  it('keeps the day the clocks change to its 23 hours', () => {
    const p = customPeriod('2026-03-29', '2026-03-29', ZURICH)!;
    expect(p.to.getTime() - p.from.getTime()).toBe(23 * 3600 * 1000);
  });

  it('refuses an end before the start or a missing day', () => {
    expect(customPeriod('2026-10-05', '2026-10-01', ZURICH)).toBeNull();
    expect(customPeriod('', '2026-10-01', ZURICH)).toBeNull();
  });
});

describe('toReportFilters', () => {
  it('sends a team only with the team scope, and nothing for "all"', () => {
    const f = toReportFilters({ ...DEFAULT_FILTERS, scope: 'org', teamId: 't1', pipelineId: '', currency: '' }, ZURICH, new Date('2026-10-30T12:00:00Z'))!;
    expect(f.teamId).toBeUndefined();
    expect(f.pipelineId).toBeUndefined();
    expect(f.currency).toBeUndefined();
    expect(toReportFilters({ ...DEFAULT_FILTERS, scope: 'team', teamId: 't1' }, ZURICH)!.teamId).toBe('t1');
  });

  it("cuts the period in the workspace's zone and has the server count the charts' days in it", () => {
    const f = toReportFilters(DEFAULT_FILTERS, ZURICH, new Date('2026-10-30T12:00:00Z'))!;
    expect(f).toMatchObject({ from: '2026-09-30T22:00:00.000Z', to: '2026-10-31T23:00:00.000Z', timeZone: ZURICH });
  });

  it('asks for nothing while a custom period is incomplete', () => {
    expect(toReportFilters({ ...DEFAULT_FILTERS, preset: 'custom', customFrom: '2026-10-01', customTo: '' }, ZURICH)).toBeNull();
  });
});

describe('months', () => {
  it("names the month an instant is in on the workspace's calendar", () => {
    const late = new Date('2026-10-31T23:30:00Z');
    expect(monthOf(late, ZURICH)).toBe('2026-11');
    expect(monthOf(late, NEW_YORK)).toBe('2026-10');
  });

  it('steps across a year end', () => {
    expect(addMonths('2026-01', -1)).toBe('2025-12');
    expect(addMonths('2026-12', 1)).toBe('2027-01');
    expect(addMonths('2026-10', 0)).toBe('2026-10');
  });

  it('writes calendar months and days as they are, in the language asked for', () => {
    expect(monthLabel('2026-10', 'en-GB')).toBe('Oct 2026');
    expect(bucketLabel('2026-10-01', 'day', 'en-GB')).toBe('1 Oct');
    expect(bucketLabel('2026-10-05', 'week', 'en-GB')).toBe('w/c 5 Oct');
  });
});

describe('remembered filters', () => {
  beforeEach(() => localStorage.clear());

  it('come back as they were saved, and fall back to defaults when unreadable', () => {
    saveFilters({ ...DEFAULT_FILTERS, preset: 'last-quarter', scope: 'me' });
    expect(loadSavedFilters()).toMatchObject({ preset: 'last-quarter', scope: 'me' });
    localStorage.setItem('cadence.reports.filters', '{not json');
    expect(loadSavedFilters()).toEqual(DEFAULT_FILTERS);
    localStorage.setItem('cadence.reports.filters', JSON.stringify({ scope: 'everyone', preset: 'forever' }));
    expect(loadSavedFilters()).toMatchObject({ scope: 'org', preset: 'this-month' });
  });
});

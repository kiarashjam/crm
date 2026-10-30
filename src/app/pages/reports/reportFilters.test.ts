import { describe, it, expect, beforeEach } from 'vitest';
import {
  customPeriod, dateInputValue, loadSavedFilters, periodFor, saveFilters, toReportFilters, DEFAULT_FILTERS,
} from './reportFilters';

const day = (d: Date) => dateInputValue(d);

describe('periodFor', () => {
  const now = new Date(2026, 9, 30, 15, 0); // 30 October 2026, afternoon

  it('starts and ends periods at local midnight', () => {
    const p = periodFor('this-month', now);
    expect([day(p.from), day(p.to)]).toEqual(['2026-10-01', '2026-11-01']);
    expect(p.from.getHours()).toBe(0);
  });

  it('knows last month, and quarters across a year end', () => {
    expect(day(periodFor('last-month', now).from)).toBe('2026-09-01');
    expect([day(periodFor('this-quarter', now).from), day(periodFor('this-quarter', now).to)]).toEqual(['2026-10-01', '2027-01-01']);
    const january = new Date(2027, 0, 15);
    expect([day(periodFor('last-quarter', january).from), day(periodFor('last-quarter', january).to)]).toEqual(['2026-10-01', '2027-01-01']);
  });

  it('includes today in the last N days', () => {
    const p = periodFor('last-30', now);
    expect([day(p.from), day(p.to)]).toEqual(['2026-10-01', '2026-10-31']);
  });
});

describe('customPeriod', () => {
  it('includes both days typed', () => {
    const p = customPeriod('2026-10-05', '2026-10-05')!;
    expect([day(p.from), day(p.to)]).toEqual(['2026-10-05', '2026-10-06']);
  });

  it('refuses an end before the start or a missing day', () => {
    expect(customPeriod('2026-10-05', '2026-10-01')).toBeNull();
    expect(customPeriod('', '2026-10-01')).toBeNull();
  });
});

describe('toReportFilters', () => {
  it('sends a team only with the team scope, and nothing for "all"', () => {
    const f = toReportFilters({ ...DEFAULT_FILTERS, scope: 'org', teamId: 't1', pipelineId: '', currency: '' }, new Date(2026, 9, 30))!;
    expect(f.teamId).toBeUndefined();
    expect(f.pipelineId).toBeUndefined();
    expect(f.currency).toBeUndefined();
    expect(f.from).toBe(new Date(2026, 9, 1).toISOString());
    expect(toReportFilters({ ...DEFAULT_FILTERS, scope: 'team', teamId: 't1' })!.teamId).toBe('t1');
  });

  it('asks for nothing while a custom period is incomplete', () => {
    expect(toReportFilters({ ...DEFAULT_FILTERS, preset: 'custom', customFrom: '2026-10-01', customTo: '' })).toBeNull();
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

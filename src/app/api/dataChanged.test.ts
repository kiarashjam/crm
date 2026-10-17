import { describe, expect, it, vi } from 'vitest';
import { announceDataChanged, changesRecords, onDataChanged } from './dataChanged';

describe('which writes count as a record change', () => {
  it('writes to records do', () => {
    expect(changesRecords('PUT', '/api/leads/abc')).toBe(true);
    expect(changesRecords('POST', '/api/activities')).toBe(true);
    expect(changesRecords('DELETE', '/api/sequences/enrollments/x')).toBe(true);
    expect(changesRecords('patch', '/api/deals/1')).toBe(true);
    expect(changesRecords('POST', 'https://api.example.com/api/contracts/1/send')).toBe(true);
  });

  it('reads never do', () => {
    expect(changesRecords(undefined, '/api/leads/all')).toBe(false);
    expect(changesRecords('GET', '/api/leads/all')).toBe(false);
  });

  it('sign-in, notifications, the writer and personal settings do not', () => {
    // The Settings page POSTs /api/auth/2fa/setup every time it opens; counting
    // that as a change made every listener re-read eight lists for nothing.
    expect(changesRecords('POST', '/api/auth/2fa/setup')).toBe(false);
    expect(changesRecords('POST', 'https://api.example.com/api/auth/login')).toBe(false);
    expect(changesRecords('PUT', '/api/notifications/read-all')).toBe(false);
    expect(changesRecords('POST', '/api/copy/generate')).toBe(false);
    expect(changesRecords('PUT', '/api/settings')).toBe(false);
  });

  it('lead routing setup does not, but a bulk edit of leads does', () => {
    // Adding a queue on the routing page used to make the sales agent re-read
    // all eight of its lists.
    expect(changesRecords('POST', '/api/lead-ops/queues')).toBe(false);
    expect(changesRecords('PUT', '/api/lead-ops/scoring')).toBe(false);
    expect(changesRecords('DELETE', '/api/lead-ops/assignment/1')).toBe(false);
    expect(changesRecords('POST', '/api/lead-ops/bulk')).toBe(true);
  });
});

describe('the signal', () => {
  it('reaches listeners, and stops after unsubscribing', () => {
    const heard = vi.fn();
    const off = onDataChanged(heard);
    announceDataChanged();
    expect(heard).toHaveBeenCalledTimes(1);
    off();
    announceDataChanged();
    expect(heard).toHaveBeenCalledTimes(1);
  });
});

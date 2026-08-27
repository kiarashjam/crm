// The shape contract for the status list, and why it is worth a test of its own.
//
// Everything about deriving a lead's status is downstream of this one call. When it
// returned the server's `Result` wrapper instead of the array, `Array.isArray` was
// false, the list read as empty, and auto-sync correctly refused to write a status
// it could not find — silently, on a 200, for every user. The behaviour under a
// malformed response is therefore not a detail: it is the difference between a
// visible fault and a feature that appears not to exist.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const api = { response: null as unknown };

vi.mock('./apiClient', () => ({
  isUsingRealApi: () => true,
  authFetchJson: () => Promise.resolve(api.response),
}));

const { getLeadStatuses } = await import('./leadStatuses');

let errors: unknown[][] = [];
beforeEach(() => {
  errors = [];
  vi.spyOn(console, 'error').mockImplementation((...args: unknown[]) => { errors.push(args); });
});
afterEach(() => { vi.restoreAllMocks(); });

describe('getLeadStatuses', () => {
  it('passes a real list straight through', async () => {
    api.response = [
      { id: 'a', name: 'New', displayOrder: 0, organizationId: 'o' },
      { id: 'b', name: 'Attempted Contact', displayOrder: 1, organizationId: 'o' },
    ];
    const list = await getLeadStatuses();
    expect(list.map((s) => s.name)).toEqual(['New', 'Attempted Contact']);
    expect(errors).toHaveLength(0);
  });

  it('an empty list is a legitimate answer and is not complained about', async () => {
    // A workspace really can have no statuses configured. That is a state the UI
    // now explains; it is not a malformed response.
    api.response = [];
    expect(await getLeadStatuses()).toEqual([]);
    expect(errors).toHaveLength(0);
  });

  it('the Result envelope reads as empty — but says so', async () => {
    // The actual bug, in the actual shape the server sent.
    api.response = {
      isSuccess: true,
      isFailure: false,
      error: { code: '', message: '' },
      value: [{ id: 'a', name: 'New', displayOrder: 0, organizationId: 'o' }],
    };

    expect(await getLeadStatuses()).toEqual([]);
    expect(errors).toHaveLength(1);
    // Unwrapping `value` here would be the wrong fix: it would keep a broken
    // endpoint working, so nobody would ever find out, and the next endpoint to do
    // it would fail differently. Loud and empty beats quietly correct.
    expect(String(errors[0]![0])).toMatch(/did not return an array/);
    expect(String(errors[0]![0])).toMatch(/no pipeline step can move/);
  });

  it('so does null, which is what a stray 204 deserialises to', async () => {
    api.response = null;
    expect(await getLeadStatuses()).toEqual([]);
    expect(errors).toHaveLength(1);
  });
});

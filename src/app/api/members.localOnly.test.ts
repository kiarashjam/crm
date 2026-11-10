// Members against a server too old to have the members route.
//
// Production once ran without one, and every call fell back to this browser: the
// list a manager entered on a laptop was invisible to the front desk's iPad, and
// the save looked like it had worked. Real records are now never kept in the
// browser. The call fails, says so, and nothing is left behind on the device; demo
// data still lives in the browser, as it always did.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { createMember, getMembers, type MemberInput } from './members';
import { NOT_ON_SERVER_MESSAGE } from './apiClient';
import { isLocalOnly, resetLocalOnly } from './localOnly';

const BASE = 'http://api.test';

const input: MemberInput = {
  firstName: 'Ines',
  lastName: 'Moreau',
  email: 'ines@example.com',
  tier: 'Gold',
  status: 'Active',
  joinedAtUtc: '2026-01-10T00:00:00.000Z',
  renewsAtUtc: '2027-01-10T00:00:00.000Z',
  duesAmount: 1200,
  duesFrequency: 'Annual',
};

let viewer = false;

function signIn(orgId: string) {
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pat', email: 'pat@example.com' }));
  localStorage.setItem('aci_org_id', orgId);
}

/** Every row this browser keeps for members, in any workspace. */
function keptInBrowser(): string[] {
  return Object.keys(localStorage).filter((key) => key.startsWith('crm.mock.members'));
}

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  viewer = false;
  vi.stubEnv('VITE_API_URL', BASE);
  // A server with no route for members; its read-only middleware still refuses a
  // Viewer's write before routing is considered.
  server.use(
    http.get(`${BASE}/api/members`, () => new HttpResponse(null, { status: 404 })),
    http.post(`${BASE}/api/members`, () =>
      viewer
        ? HttpResponse.json({ title: 'Forbidden', detail: 'You have read-only access to this organization.' }, { status: 403 })
        : new HttpResponse(null, { status: 404 })),
  );
});
afterEach(() => vi.unstubAllEnvs());

describe('members when the server has no members route', () => {
  it('says the list cannot be read, instead of showing a browser copy', async () => {
    signIn('org-a');
    await expect(getMembers()).rejects.toThrow(NOT_ON_SERVER_MESSAGE);
    expect(isLocalOnly(['members'])).toBe(false);
  });

  it('refuses a new member and keeps nothing in this browser', async () => {
    signIn('org-a');
    await expect(createMember(input)).rejects.toThrow(NOT_ON_SERVER_MESSAGE);
    expect(keptInBrowser()).toEqual([]);
  });

  it("still refuses a Viewer's new member with the server's own reason", async () => {
    signIn('org-a');
    viewer = true;
    await expect(createMember(input)).rejects.toThrow('read-only');
    expect(keptInBrowser()).toEqual([]);
  });
});

describe('members with demo data', () => {
  it('keeps them in this browser, as before', async () => {
    vi.stubEnv('VITE_API_URL', '');
    const before = (await getMembers()).length;
    await createMember(input);
    expect(await getMembers()).toHaveLength(before + 1);
  });
});

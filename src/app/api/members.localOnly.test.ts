// Members end to end, as production runs it today: the backend has no members
// endpoint, so every call 404s and the list lives in this browser.
//
// This is the case the scoping exists for. Two organizations opened in one
// browser must keep separate lists, nobody should be shown the demo people
// (Amelia, Daniel, Priya, ...) as if they were real members, and a Viewer's save
// must be refused by the server rather than quietly kept on the device.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { createMember, getMembers, type MemberInput } from './members';
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

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  viewer = false;
  vi.stubEnv('VITE_API_URL', BASE);
  // What the deployed backend does: no route for members, and the read-only
  // middleware refuses a Viewer's write before routing is even considered.
  server.use(
    http.get(`${BASE}/api/members`, () => new HttpResponse(null, { status: 404 })),
    http.post(`${BASE}/api/members`, () =>
      viewer
        ? HttpResponse.json({ title: 'Forbidden', detail: 'You have read-only access to this organization.' }, { status: 403 })
        : new HttpResponse(null, { status: 404 })),
  );
});
afterEach(() => vi.unstubAllEnvs());

describe('members with no backend endpoint', () => {
  it('shows no demo members to a signed-in user', async () => {
    signIn('org-a');
    expect(await getMembers()).toEqual([]);
  });

  it('flags the feature as saved on this device only', async () => {
    signIn('org-a');
    await getMembers();
    expect(isLocalOnly(['members'])).toBe(true);
  });

  it('keeps two organizations apart in the same browser', async () => {
    signIn('org-a');
    await createMember(input);
    expect((await getMembers()).map((m) => m.email)).toEqual(['ines@example.com']);

    signIn('org-b');
    expect(await getMembers()).toEqual([]);

    signIn('org-a');
    expect(await getMembers()).toHaveLength(1);
  });

  it("refuses a Viewer's new member instead of saving it locally", async () => {
    signIn('org-a');
    viewer = true;
    await expect(createMember(input)).rejects.toThrow('read-only');
    viewer = false;
    expect(await getMembers()).toEqual([]);
  });
});

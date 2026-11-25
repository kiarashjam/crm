// Inviting someone to the workspace: with a preset chosen, the invitation says which, so the
// person joins with it (a read-only founder joins read-only, never first as a Member who can
// edit, delete and export); without one, the request is the same as it always was.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { createInvite } from './organizations';

const BASE = 'http://api.test';
let bodies: unknown[];

const INVITE = {
  id: 'i1', organizationId: 'org-a', organizationName: 'Pavillon 46', email: 'founder@example.ch',
  expiresAtUtc: '2026-12-02T08:00:00Z', createdAtUtc: '2026-11-25T08:00:00Z',
};

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem('aci_user', JSON.stringify({ id: 'kia', name: 'Kia', email: 'kia@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  vi.stubEnv('VITE_API_URL', BASE);
  bodies = [];
  server.use(http.post(`${BASE}/api/invites/org-a`, async ({ request }) => {
    const body = (await request.json()) as { preset?: string };
    bodies.push(body);
    return HttpResponse.json({ ...INVITE, preset: body.preset ?? null });
  }));
});
afterEach(() => vi.unstubAllEnvs());

describe('inviting someone to the workspace', () => {
  it('sends the preset chosen, and the invitation says it', async () => {
    const invite = await createInvite('org-a', ' Founder@Example.ch ', 'read_only');

    expect(bodies).toEqual([{ email: 'founder@example.ch', preset: 'read_only' }]);
    expect(invite?.preset).toBe('read_only');
  });

  it('sends only the address without one, as it always did', async () => {
    const invite = await createInvite('org-a', 'newhire@example.ch');

    expect(bodies).toEqual([{ email: 'newhire@example.ch' }]);
    expect(invite?.preset).toBeNull();
  });
});

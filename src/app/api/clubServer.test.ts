// The club screens against the server.
//
// When these features moved from the browser to the server, several functions kept
// doing part of their work on the browser's own copy, which is empty once the data
// lives on the server: a charge reset the member's house balance to zero, removing a
// tab item never sent the request, and a billing run found nothing to bill. And a few
// asked "is there a server?" where they meant "is this a real account?", so the demo
// user — who has no account — was sent to the server and signed out by its 401.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { createCharge, recordPayment } from './charges';
import { removeItemFromTab } from './tabs';
import { runDueBillingCycles } from './subscriptions';
import { redeemGiftCard } from './giftCards';
import { resetHospitalitySettings } from './hospitalitySettings';
import { createHousehold, getHouseholdMembers } from './households';
import { mergeContacts } from './dedupe';
import { approveApplication } from './applications';

const BASE = 'http://api.test';
let requests: string[];

function signIn(as: 'member' | 'demo') {
  localStorage.setItem('aci_user', JSON.stringify(as === 'demo'
    ? { id: 'demo', name: 'Demo User', email: 'demo@example.com' }
    : { id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  if (as === 'member') {
    localStorage.setItem('aci_token', 'token');
    localStorage.setItem('aci_org_id', 'org-a');
  }
}

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

describe('on the server', () => {
  beforeEach(() => signIn('member'));

  it("leaves the house balance to the server: a charge doesn't write it back as zero", async () => {
    server.use(
      http.post(`${BASE}/api/charges`, () => HttpResponse.json({ id: 'c1', memberId: 'm1', memberName: 'Jean', kind: 'Dining', description: 'Dinner', amount: 20, paidAmount: 0, status: 'Pending', postedAtUtc: '' })),
      http.post(`${BASE}/api/payments`, () => HttpResponse.json({ id: 'p1', memberId: 'm1', memberName: 'Jean', amount: 20, method: 'Cash', receivedAtUtc: '', appliedToChargeIds: ['c1'] })),
    );

    await createCharge({ memberId: 'm1', memberName: 'Jean', kind: 'Dining', description: 'Dinner', amount: 20 });
    await recordPayment({ memberId: 'm1', memberName: 'Jean', amount: 20, method: 'Cash', chargeId: 'c1' });

    expect(requests).toEqual(['POST /api/charges', 'POST /api/payments']);
  });

  it('removes a tab item on the server and shows the tab the server returns', async () => {
    server.use(http.delete(`${BASE}/api/tabs/t1/items/i2`, () =>
      HttpResponse.json({ id: 't1', items: [{ id: 'i1', name: 'Negroni', quantity: 2, unitPrice: 15 }], total: 30, status: 'Open' })));

    const tab = await removeItemFromTab('t1', 'i2');

    expect(requests).toEqual(['DELETE /api/tabs/t1/items/i2']);
    expect(tab?.total).toBe(30);
  });

  it("bills the server's due subscriptions, not the browser's empty list", async () => {
    const past = new Date(Date.now() - 86_400_000).toISOString();
    const future = new Date(Date.now() + 10 * 86_400_000).toISOString();
    server.use(
      http.get(`${BASE}/api/subscriptions`, () => HttpResponse.json([
        { id: 's-due', status: 'Active', nextChargeAtUtc: past },
        { id: 's-later', status: 'Active', nextChargeAtUtc: future },
        { id: 's-paused', status: 'Paused', nextChargeAtUtc: past },
      ])),
      http.post(`${BASE}/api/subscriptions/s-due/bill`, () => HttpResponse.json({ id: 's-due', status: 'Active', nextChargeAtUtc: future })),
    );

    expect(await runDueBillingCycles()).toBe(1);
    expect(requests).toContain('POST /api/subscriptions/s-due/bill');
  });

  it('reads a refused gift card redemption as "could not redeem", like the browser did', async () => {
    server.use(http.post(`${BASE}/api/gift-cards/g1/redeem`, () =>
      HttpResponse.json({ title: 'Not Found', status: 404 }, { status: 404 })));

    expect(await redeemGiftCard({ giftCardId: 'g1', amount: 50, redeemedByName: 'Jean' })).toBeNull();
  });

  it('fills in the defaults after a reset, which the server answers with {}', async () => {
    server.use(http.post(`${BASE}/api/hospitality/settings/reset`, () => HttpResponse.json({})));

    const settings = await resetHospitalitySettings();

    expect(Array.isArray(settings.tiers)).toBe(true);
  });

  it("leaves a new household's primary link to the server", async () => {
    server.use(
      http.post(`${BASE}/api/households`, () => HttpResponse.json({ id: 'h1', name: 'Dupont', primaryMemberId: 'm1', createdAtUtc: '' })),
      http.get(`${BASE}/api/household-members`, () => HttpResponse.json([{ id: 'l1', householdId: 'h1', memberId: 'm1', role: 'Primary', rollUpBilling: true }])),
    );

    await createHousehold({ name: 'Dupont', primaryMemberId: 'm1' });

    expect(await getHouseholdMembers()).toHaveLength(1);
    expect(Object.keys(localStorage).some((k) => k.includes('householdMember'))).toBe(false);
  });

  it("approves an application once: a second approval doesn't add the member again", async () => {
    let status = 'Submitted';
    server.use(
      http.get(`${BASE}/api/applications`, () => HttpResponse.json([{ id: 'a1', firstName: 'Jean', lastName: 'Dupont', email: 'jean@example.ch', requestedTier: 'Gold', status }])),
      http.post(`${BASE}/api/members`, () => HttpResponse.json({ id: 'm1' })),
      http.put(`${BASE}/api/applications/a1`, () => {
        status = 'Approved';
        return HttpResponse.json({ id: 'a1', status });
      }),
    );

    await approveApplication('a1');
    await approveApplication('a1');

    expect(requests.filter((r) => r === 'POST /api/members')).toHaveLength(1);
  });
});

describe('the demo user on a build with a server', () => {
  beforeEach(() => signIn('demo'));

  it('merges duplicates in the browser, without asking a server it has no account on', async () => {
    await mergeContacts('c1', ['c2']);
    expect(requests).toEqual([]);
  });

  it("removes a tab item in the browser", async () => {
    await removeItemFromTab('t-none', 'i-none');
    expect(requests).toEqual([]);
  });
});

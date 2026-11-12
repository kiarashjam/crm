// The billing calls against the server: what each one sends.
//
// Billing what is due is one request to the server's run, not the browser billing each
// subscription in turn; billing a period early is asked for in so many words; and a
// statement is the server's PDF, downloaded or emailed for the period on screen.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { downloadStatementPdf, emailStatement, getBillingPreview, runBillingNow } from './billing';
import { runBillingCycle, isNotDue, nextPeriodStart } from './subscriptions';
import { getHospitalitySettings } from './hospitalitySettings';
import { getCatalog, updateCatalogItem } from './catalog';

const saved = vi.hoisted(() => ({ files: [] as { name: string; type: string }[] }));
vi.mock('@/app/lib/exportCsv', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/lib/exportCsv')>()),
  downloadFile: (blob: Blob, name: string) => saved.files.push({ name, type: blob.type }),
}));

const BASE = 'http://api.test';
let requests: { method: string; path: string; search: string; body: string }[];

beforeEach(() => {
  localStorage.clear();
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  vi.stubEnv('VITE_API_URL', BASE);
  requests = [];
  saved.files = [];
  server.events.on('request:start', async ({ request }) => {
    const url = new URL(request.url);
    requests.push({ method: request.method, path: url.pathname, search: url.search, body: await request.clone().text() });
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

const RUN = {
  id: 'r1', trigger: 'manual', status: 'succeeded', startedAtUtc: '2026-11-10T08:00:00Z', steps: ['billing'],
  subscriptionsBilled: 2, chargesPosted: 3, amountBilled: 897, currency: 'CHF', overdueCharges: 0, subscriptionsPastDue: 0,
  remindersQueued: 0, membersLapsed: 0, membersReinstated: 0, statementsQueued: 0, failures: [],
};

describe('billing against the server', () => {
  it('bills what is due in one request to the server run', async () => {
    server.use(http.post(`${BASE}/api/billing/run`, () => HttpResponse.json(RUN)));

    const run = await runBillingNow();

    expect(run.chargesPosted).toBe(3);
    expect(requests.map((r) => `${r.method} ${r.path}`)).toEqual(['POST /api/billing/run']);
  });

  it('reads what billing now would post', async () => {
    server.use(http.get(`${BASE}/api/billing/preview`, () =>
      HttpResponse.json({ subscriptions: 1, periods: 2, amountByCurrency: { CHF: 598 }, items: [] })));

    expect((await getBillingPreview()).periods).toBe(2);
  });

  it('bills a period by hand with no body, and early only when asked', async () => {
    server.use(http.post(`${BASE}/api/subscriptions/s1/bill`, () => HttpResponse.json({ id: 's1' })));

    await runBillingCycle('s1');
    await runBillingCycle('s1', { early: true });

    expect(requests.map((r) => r.body)).toEqual(['', JSON.stringify({ early: true })]);
  });

  it('tells a period not started yet from any other refusal', async () => {
    server.use(http.post(`${BASE}/api/subscriptions/s1/bill`, () => HttpResponse.json(
      { title: 'Conflict', status: 409, detail: "This subscription's next period starts on 1 Dec 2026, so nothing was billed.", errorCode: 'Subscription.NotDue.Conflict' },
      { status: 409 })));

    const err = await runBillingCycle('s1').catch((e: unknown) => e);

    expect(isNotDue(err)).toBe(true);
    expect((err as Error).message).toContain('1 Dec 2026');
    expect(isNotDue(new Error('Someone else changed this subscription.'))).toBe(false);
  });

  it("downloads the statement as the server's PDF, under the name it gives", async () => {
    server.use(http.get(`${BASE}/api/members/m1/statement.pdf`, () => new HttpResponse('%PDF-1.4', {
      headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': "attachment; filename=statement.pdf; filename*=UTF-8''Statement%20Jean%20Dupont%202026-11.pdf" },
    })));

    await downloadStatementPdf('m1', '2026-11-01T00:00:00.000Z', '2026-12-01T00:00:00.000Z');

    expect(saved.files).toEqual([{ name: 'Statement Jean Dupont 2026-11.pdf', type: 'application/pdf' }]);
    const asked = new URLSearchParams(requests[0]!.search);
    expect(asked.get('from')).toBe('2026-11-01T00:00:00.000Z');
    expect(asked.get('to')).toBe('2026-12-01T00:00:00.000Z');
  });

  it('emails the statement for the period on screen', async () => {
    server.use(http.post(`${BASE}/api/members/m1/statement/email`, () => HttpResponse.json({
      id: 'sd1', memberId: 'm1', periodStartUtc: '2026-11-01T00:00:00Z', periodEndUtc: '2026-12-01T00:00:00Z',
      status: 'queued', amountDue: 299, currency: 'CHF', outboundMessageId: 'o1',
    })));

    const sent = await emailStatement('m1', '2026-11-01T00:00:00.000Z', '2026-12-01T00:00:00.000Z');

    expect(sent.status).toBe('queued');
    expect(JSON.parse(requests[0]!.body)).toEqual({ from: '2026-11-01T00:00:00.000Z', to: '2026-12-01T00:00:00.000Z' });
  });
});

describe('the settings and the catalogue against the server', () => {
  it('fills in the billing defaults for settings saved before billing had any', async () => {
    server.use(http.get(`${BASE}/api/hospitality/settings`, () => HttpResponse.json({ currency: 'CHF', basePointsPerDollar: 12 })));

    const settings = await getHospitalitySettings();

    expect(settings.basePointsPerDollar).toBe(12);
    expect(settings.billing).toMatchObject({ automaticBilling: false, gracePeriodDays: 60, reminderDays: [3, 14, 30] });
    expect(settings.visitPointsByVenue.Dining).toBe(50);
  });

  it('reads the catalogue of one kind, and saves an item with the version it was read at', async () => {
    server.use(
      http.get(`${BASE}/api/catalog`, () => HttpResponse.json([{ id: 't1', kind: 'Membership', name: 'Gold', price: 299, version: 'v1' }])),
      http.put(`${BASE}/api/catalog/t1`, () => HttpResponse.json({ id: 't1', kind: 'Membership', name: 'Gold', price: 320, version: 'v2' })),
    );

    const [gold] = await getCatalog('Membership');
    // An item the server sent without benefits still has a list to show.
    expect(gold!.benefits).toEqual([]);
    await updateCatalogItem('t1', { price: 320, version: gold!.version });

    expect(requests[0]!.search).toBe('?kind=Membership');
    expect(JSON.parse(requests[1]!.body)).toEqual({ price: 320, version: 'v1' });
  });
});

describe('the next period', () => {
  it('is the same day a calendar period later, or the last day of a shorter month', () => {
    expect(nextPeriodStart('2026-01-31T00:00:00.000Z', 'Monthly')).toBe('2026-02-28T00:00:00.000Z');
    expect(nextPeriodStart('2026-02-28T00:00:00.000Z', 'Monthly', 31)).toBe('2026-03-31T00:00:00.000Z');
    expect(nextPeriodStart('2026-11-15T00:00:00.000Z', 'Quarterly')).toBe('2027-02-15T00:00:00.000Z');
    expect(nextPeriodStart('2024-02-29T00:00:00.000Z', 'Annual')).toBe('2025-02-28T00:00:00.000Z');
  });
});

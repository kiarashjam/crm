// The payment calls against the server: what each one sends, and what demo data answers.
//
// A payment is only ever taken on Stripe's page or read from the bank's file, and recorded
// by the server; demo data has none of it, so the screens say what is not set up. The pay
// page is public: the token in the link is the whole permission.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import {
  PayLinkError, downloadJournalCsv, getOnlinePayments, getPayPage, getPaymentStatus, importBankFile, savePaymentSettings,
  setAutoPay, startCheckout,
} from './payments';

const saved = vi.hoisted(() => ({ files: [] as { name: string; type: string }[] }));
vi.mock('@/app/lib/exportCsv', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/lib/exportCsv')>()),
  downloadFile: (blob: Blob, name: string) => saved.files.push({ name, type: blob.type }),
}));

const BASE = 'http://api.test';
let requests: { method: string; path: string; search: string; body: string; auth: string | null }[];

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
    requests.push({
      method: request.method, path: url.pathname, search: url.search, body: await request.clone().text(),
      auth: request.headers.get('Authorization'),
    });
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

describe('payments against the server', () => {
  it('leaves a secret out of the save unless one is typed, so the server keeps the one it has', async () => {
    server.use(http.put(`${BASE}/api/payment-settings`, () => HttpResponse.json({ stripeConfigured: true, version: 'v2' })));

    await savePaymentSettings({ stripeOfferTwint: true, iban: 'CH58 0079 1123 0008 8901 2', version: 'v1' });

    expect(requests).toHaveLength(1);
    expect(JSON.parse(requests[0].body)).toEqual({ stripeOfferTwint: true, iban: 'CH58 0079 1123 0008 8901 2', version: 'v1' });
  });

  it("asks for one member's online payments, or one state's", async () => {
    server.use(http.get(`${BASE}/api/online-payments`, () => HttpResponse.json([])));

    await getOnlinePayments({ memberId: 'm1' });
    await getOnlinePayments({ status: 'failed' });
    await getOnlinePayments();

    expect(requests.map((r) => r.search)).toEqual(['?memberId=m1', '?status=failed', '']);
  });

  it("sends the bank's file as it was read, with its name", async () => {
    server.use(http.post(`${BASE}/api/bank/imports`, () => HttpResponse.json({ id: 'i1', matched: 1 })));

    await importBankFile('camt054.xml', '<Document/>');

    expect(JSON.parse(requests[0].body)).toEqual({ fileName: 'camt054.xml', content: '<Document/>' });
  });

  it("turns dues on file on only with the member's agreement said in so many words", async () => {
    server.use(http.put(`${BASE}/api/members/m1/autopay`, () => HttpResponse.json({ memberId: 'm1', autoPayDues: true })));

    await setAutoPay('m1', true, true, 'v1');

    expect(JSON.parse(requests[0].body)).toEqual({ enabled: true, memberAgreed: true, version: 'v1' });
  });

  it("saves the journal under the server's file name", async () => {
    server.use(http.get(`${BASE}/api/accounting/journal`, () => new HttpResponse('Date;Document\r\n', {
      headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename="journal-2026-10.csv"' },
    })));

    await downloadJournalCsv('2026-10-01T00:00:00.000Z', '2026-11-01T00:00:00.000Z', 'net');

    expect(requests[0].search).toBe('?from=2026-10-01T00%3A00%3A00.000Z&to=2026-11-01T00%3A00%3A00.000Z&layout=net&format=csv');
    expect(saved.files.map((f) => f.name)).toEqual(['journal-2026-10.csv']);
  });
});

describe('the public pay page', () => {
  it('reads the link with no sign-in, and starts checkout from it', async () => {
    server.use(
      http.get(`${BASE}/api/public/pay/tok%2Fen`, () => HttpResponse.json({ clubName: 'Pavillon 46', amount: 120, available: true })),
      http.post(`${BASE}/api/public/pay/tok%2Fen/checkout`, () => HttpResponse.json({ url: 'https://checkout.stripe.com/c/pay/cs_1' })),
    );

    expect((await getPayPage('tok/en')).clubName).toBe('Pavillon 46');
    expect((await startCheckout('tok/en')).url).toBe('https://checkout.stripe.com/c/pay/cs_1');
    expect(requests.map((r) => r.auth)).toEqual([null, null]);
  });

  it("says why a link cannot be used, in the server's words", async () => {
    server.use(http.get(`${BASE}/api/public/pay/old`, () => HttpResponse.json(
      { title: 'Not found', status: 404, detail: 'This payment link is not valid any more. Ask the club for a new one.' }, { status: 404 })));

    const err = await getPayPage('old').catch((e: unknown) => e);

    expect(err).toBeInstanceOf(PayLinkError);
    expect((err as PayLinkError).status).toBe(404);
    expect((err as PayLinkError).message).toBe('This payment link is not valid any more. Ask the club for a new one.');
  });
});

describe('demo data', () => {
  it('has no payments set up, and asks the server nothing', async () => {
    vi.stubEnv('VITE_API_URL', '');

    expect(await getPaymentStatus()).toEqual(expect.objectContaining({ onlinePayments: false, qrBills: false }));
    expect(await getOnlinePayments()).toEqual([]);
    await expect(importBankFile('camt.xml', '<Document/>')).rejects.toThrow(/live app/);
    expect(requests).toHaveLength(0);
  });
});

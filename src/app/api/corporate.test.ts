// The calls for the club's clients against the server: what each one sends (the version a screen
// read for a change that must not overwrite another, a deposit choice when cancelling, the period
// of a statement), what a member with no seat reads as, and which cached lists each write refreshes.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import {
  addNominee, cancelPrivateEvent, checkInPrivateEventGuest, createPrivateEvent, downloadClientStatementPdf, getClientAccount,
  getMemberCorporateSeat, getPrivateEvents, renewCorporateMembership, updatePrivateEvent,
} from './corporate';
import { resourcesChangedBy } from './dataChanged';
import { deleteCompany } from './companies';
import { deleteContact } from './contacts';

const saved = vi.hoisted(() => ({ files: [] as { name: string; type: string }[] }));
vi.mock('@/app/lib/exportCsv', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/lib/exportCsv')>()),
  downloadFile: (blob: Blob, name: string) => saved.files.push({ name, type: blob.type }),
}));

const BASE = 'http://api.test';
let requests: { method: string; path: string; search: string; body: string; ifMatch: string | null }[];

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
    requests.push({ method: request.method, path: url.pathname, search: url.search, body: await request.clone().text(), ifMatch: request.headers.get('If-Match') });
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

const EVENT = {
  id: 'pe-1', title: 'Acme summer party', startAtUtc: '2026-12-13T17:00:00Z', endAtUtc: '2026-12-13T22:00:00Z', guestCount: 20, status: 'Enquiry',
  currency: 'CHF', spaces: [], lines: [], guests: [], guestsCheckedIn: 0, createdAtUtc: '2026-11-13T10:00:00Z', version: 'v2',
  money: { linesTotal: 0, depositState: 'none', depositPaid: 0, invoiced: false, invoiceChargeIds: [], invoiceTotal: 0, invoicePaid: 0 },
};

describe('the club client calls', () => {
  it("reads a company's account under its kind, and a contact's under theirs", async () => {
    server.use(http.get(`${BASE}/api/club-accounts/:kind/:id`, ({ params }) =>
      HttpResponse.json({ kind: params.kind === 'companies' ? 'company' : 'contact', id: params.id, name: 'Acme AG', open: true, currency: 'CHF', balance: -500, owed: 500, credit: 0, openCharges: 1 })));

    const account = await getClientAccount('company', 'acme');
    await getClientAccount('contact', 'lena');

    expect(account?.owed).toBe(500);
    expect(requests.map((r) => r.path)).toEqual(['/api/club-accounts/companies/acme', '/api/club-accounts/contacts/lena']);
  });

  it('asks the statement for the period chosen and saves the PDF under the name the server gave', async () => {
    server.use(http.get(`${BASE}/api/club-accounts/companies/acme/statement.pdf`, () => new HttpResponse('%PDF-1.4', {
      headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': "attachment; filename=statement.pdf; filename*=UTF-8''Company%20statement%20Acme%20AG.pdf" },
    })));

    await downloadClientStatementPdf('company', 'acme', '2026-11-01T00:00:00.000Z', '2026-12-01T00:00:00.000Z');

    expect(requests[0]!.search).toBe(`?from=${encodeURIComponent('2026-11-01T00:00:00.000Z')}&to=${encodeURIComponent('2026-12-01T00:00:00.000Z')}`);
    expect(saved.files).toEqual([{ name: 'Company statement Acme AG.pdf', type: 'application/pdf' }]);
  });

  it('reads a member who holds no seat as nobody, from the server saying there is none', async () => {
    server.use(http.get(`${BASE}/api/members/:id/corporate`, ({ params }) => params.id === 'anna'
      ? HttpResponse.json({ membershipId: 'cm-1', name: 'Acme corporate', companyId: 'acme', companyName: 'Acme AG', nomineeCharges: 'company', renewsAtUtc: '2027-11-13T00:00:00Z', nomineeId: 'n-1' })
      : new HttpResponse(null, { status: 204 })));

    expect((await getMemberCorporateSeat('anna'))?.companyName).toBe('Acme AG');
    expect(await getMemberCorporateSeat('ben')).toBeNull();
  });

  it('sends the version a screen read with a change that must not overwrite another, in the body and as If-Match', async () => {
    server.use(
      http.put(`${BASE}/api/private-events/pe-1`, () => HttpResponse.json(EVENT)),
      http.post(`${BASE}/api/corporate-memberships/cm-1/renew`, () => HttpResponse.json({})),
    );

    await updatePrivateEvent('pe-1', { guestCount: 30 }, 'v1');
    await renewCorporateMembership('cm-1', 'v7');

    expect(requests[0]).toMatchObject({ method: 'PUT', ifMatch: '"v1"' });
    expect(JSON.parse(requests[0]!.body)).toEqual({ guestCount: 30, version: 'v1' });
    expect(requests[1]).toMatchObject({ method: 'POST', path: '/api/corporate-memberships/cm-1/renew', ifMatch: '"v7"' });
  });

  it('sends what becomes of the deposit with a cancellation, and checks a guest in or takes it back', async () => {
    server.use(
      http.post(`${BASE}/api/private-events/pe-1/cancel`, () => HttpResponse.json(EVENT)),
      http.post(`${BASE}/api/private-events/pe-1/guests/g-1/:what`, () => HttpResponse.json(EVENT)),
    );

    await cancelPrivateEvent('pe-1', 'Acme postponed', 'release');
    await checkInPrivateEventGuest('pe-1', 'g-1');
    await checkInPrivateEventGuest('pe-1', 'g-1', true);

    expect(JSON.parse(requests[0]!.body)).toEqual({ reason: 'Acme postponed', deposit: 'release' });
    expect(requests.slice(1).map((r) => r.path)).toEqual(['/api/private-events/pe-1/guests/g-1/check-in', '/api/private-events/pe-1/guests/g-1/undo-check-in']);
  });

  it("lists private events by the period and client asked, and says a server's refusal as it is", async () => {
    server.use(
      http.get(`${BASE}/api/private-events`, () => HttpResponse.json([EVENT])),
      http.post(`${BASE}/api/private-events`, () => HttpResponse.json(
        { title: 'Conflict', status: 409, detail: 'Terrace is already booked 19:00 to 20:00 by Anna Keller (confirmed), so it can’t take this booking.', errorCode: 'Reservation.Overlap.Conflict' },
        { status: 409 })),
      http.post(`${BASE}/api/corporate-memberships/cm-1/nominees`, () => HttpResponse.json(
        { title: 'Conflict', status: 409, detail: 'Its one seat is taken. Swap a nominee, remove one, or give the membership more seats.', errorCode: 'CorporateMembership.SeatsFull.Conflict' },
        { status: 409 })),
    );

    expect(await getPrivateEvents({ fromUtc: '2026-11-13T00:00:00Z', companyId: 'acme' })).toHaveLength(1);
    expect(requests[0]!.search).toBe('?from=2026-11-13T00%3A00%3A00Z&companyId=acme');
    await expect(createPrivateEvent({ title: 'Party', companyId: 'acme' })).rejects.toThrow(/Terrace is already booked/);
    await expect(addNominee('cm-1', 'ben')).rejects.toMatchObject({ status: 409, code: 'CorporateMembership.SeatsFull.Conflict' });
  });
});

describe('deleting a company or contact the club keeps', () => {
  it("is refused in the server's words, and a delete that goes through still says so", async () => {
    server.use(
      http.delete(`${BASE}/api/companies/acme`, () => HttpResponse.json({
        title: 'Conflict', status: 409, errorCode: 'Company.KeptByClub.Conflict',
        detail: 'This company has a corporate membership at the club, and the club’s records would be left naming nobody, so it can’t be deleted. Merge it into the company it duplicates instead.',
      }, { status: 409 })),
      http.delete(`${BASE}/api/contacts/lena`, () => new HttpResponse(null, { status: 204 })),
      http.delete(`${BASE}/api/contacts/gone`, () => new HttpResponse(null, { status: 404 })),
    );

    await expect(deleteCompany('acme')).rejects.toMatchObject({ status: 409, code: 'Company.KeptByClub.Conflict', message: expect.stringContaining('corporate membership') });
    expect(await deleteContact('lena')).toBe(true);
    expect(await deleteContact('gone')).toBe(false);
  });
});

describe('what the club client writes refresh', () => {
  it('a private event refreshes the bookings its spaces are held in and the money its deposit and invoice post', () => {
    expect(resourcesChangedBy('POST', '/api/private-events')).toEqual(expect.arrayContaining(['private-events', 'reservations']));
    expect(resourcesChangedBy('POST', '/api/private-events/pe-1/invoice')).toEqual(
      expect.arrayContaining(['charges', 'payments', 'club-accounts', 'statements', 'club-reports']));
    expect(resourcesChangedBy('POST', '/api/private-events/pe-1/contract')).toEqual(expect.arrayContaining(['contracts', 'deals']));
  });

  it("a corporate membership's seat refreshes the members whose charges it routes, and its dues the subscriptions", () => {
    expect(resourcesChangedBy('POST', '/api/corporate-memberships/cm-1/nominees')).toEqual(expect.arrayContaining(['corporate', 'members']));
    expect(resourcesChangedBy('PUT', '/api/corporate-memberships/cm-1/dues')).toEqual(expect.arrayContaining(['subscriptions', 'club-accounts']));
  });

  it("money on a client's account refreshes the account, its membership and its event", () => {
    for (const path of ['/api/charges', '/api/payments']) {
      expect(resourcesChangedBy('POST', path)).toEqual(expect.arrayContaining(['club-accounts', 'corporate', 'private-events']));
    }
  });

  it("merging companies or contacts, a member's merge or delete, and a countersignature refresh the club's client records", () => {
    expect(resourcesChangedBy('POST', '/api/companies/acme/merge')).toEqual(expect.arrayContaining(['club-accounts', 'corporate', 'private-events', 'charges']));
    expect(resourcesChangedBy('POST', '/api/contacts/lena/merge')).toEqual(expect.arrayContaining(['club-accounts', 'private-events', 'leads']));
    expect(resourcesChangedBy('POST', '/api/members/m-1/merge')).toEqual(expect.arrayContaining(['corporate', 'private-events']));
    expect(resourcesChangedBy('DELETE', '/api/members/m-1')).toEqual(expect.arrayContaining(['corporate', 'private-events']));
    expect(resourcesChangedBy('POST', '/api/contracts/c-1/countersign')).toEqual(expect.arrayContaining(['private-events', 'reservations']));
  });
});

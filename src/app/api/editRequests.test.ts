// What the edit requests put on the wire, against the real API: the version a dialog was
// opened on, a field taken off as null, a pipeline step as a change rather than the whole
// pipeline, and a refusal because the record changed meanwhile read as exactly that.

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { updateContact } from './contacts';
import { updateDeal } from './deals';
import { updateLead } from './leads';
import { isEditConflict } from '@/app/lib/editConflict';

const BASE = 'http://api.test';

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'user-1', name: 'Pat', email: 'pat@example.com' }));
});
afterEach(() => vi.unstubAllEnvs());

/** Answers PUT `path` with `status` and `body`, and hands back what was sent. */
function capture(path: string, status: number, body: object) {
  const sent: unknown[] = [];
  server.use(http.put(`${BASE}${path}`, async ({ request }) => {
    sent.push(await request.json());
    return HttpResponse.json(body, { status });
  }));
  return sent;
}

const changed = (what: string) => ({
  title: 'Conflict',
  status: 409,
  detail: `Someone else changed this ${what} after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.`,
  errorCode: `${what[0]!.toUpperCase()}${what.slice(1)}.Changed.Conflict`,
});

describe('a contact edit', () => {
  it('sends the version and the fields taken off as null', async () => {
    const sent = capture('/api/contacts/c1', 200, { id: 'c1', name: 'Anna', email: 'anna@example.ch', version: 'v2' });

    const { contact } = await updateContact('c1', { name: 'Anna', phone: null, companyId: null, version: 'v1' });

    expect(sent[0]).toEqual({ name: 'Anna', phone: null, companyId: null, version: 'v1' });
    expect(contact?.version).toBe('v2');
  });

  it('reads a refusal for a stale copy as a conflict, with the server\'s sentence', async () => {
    capture('/api/contacts/c1', 409, changed('contact'));

    const result = await updateContact('c1', { name: 'Anna', version: 'v1' });

    expect(result.contact).toBeNull();
    expect(result.conflict).toBe(true);
    expect(result.error).toMatch(/someone else changed this contact/i);
  });

  it('tells a duplicate email apart from a stale copy, and says which it is', async () => {
    capture('/api/contacts/c1', 409, { title: 'Conflict', status: 409, detail: "A contact with email 'a@b.ch' already exists", errorCode: 'Contact.DuplicateEmail' });

    const result = await updateContact('c1', { email: 'a@b.ch' });

    expect(result.conflict).toBe(false);
    expect(result.error).toBe("A contact with email 'a@b.ch' already exists");
  });
});

describe('a deal edit', () => {
  it('sends the version and the fields taken off as null', async () => {
    const sent = capture('/api/deals/d1', 200, { id: 'd1', name: 'Gala', value: '100', version: 'v2' });

    const deal = await updateDeal('d1', { name: 'Gala', companyId: null, expectedCloseDateUtc: null, version: 'v1' });

    expect(sent[0]).toEqual({ name: 'Gala', companyId: null, expectedCloseDateUtc: null, version: 'v1' });
    expect(deal?.version).toBe('v2');
  });

  it('throws a refusal for a stale copy that the dialogs recognise as one', async () => {
    capture('/api/deals/d1', 409, changed('deal'));

    const failure = await updateDeal('d1', { name: 'Gala', version: 'v1' }).catch((err: unknown) => err);

    expect(isEditConflict(failure)).toBe(true);
    expect((failure as Error).message).toMatch(/someone else changed this deal/i);
  });
});

describe('a lead edit', () => {
  it('sends a pipeline step as the change, not the whole pipeline', async () => {
    const sent = capture('/api/leads/l1', 200, { id: 'l1', name: 'Jean', email: 'jean@example.ch', status: 'New', version: 'v2' });

    await updateLead('l1', { pipelineState: JSON.stringify({ meetingDate: null, meetingAttended: true }) });

    expect(sent[0]).toEqual({ pipelineState: '{"meetingDate":null,"meetingAttended":true}' });
  });
});

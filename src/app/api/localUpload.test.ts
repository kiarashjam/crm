// Moving custom fields and attachments out of the browser and onto the server.
//
// Until the server served them, both were saved only in localStorage. Once it does,
// the app stops reading the browser, so without this step everything entered before
// would vanish from view. The upload has to be careful in ways that are easy to get
// wrong, and each is pinned here: demo rows seeded into real browsers must not be
// uploaded as if someone made them; the browser's field keys differ from the
// server's, so values must be re-keyed; what can't go yet (another workspace's
// records, fields only a manager may add) must stay behind rather than be lost; and
// nothing is touched if the server can't take it.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import {
  LOCAL_KEYS, UPLOAD_BACKUP_SUFFIX, dataUrlToFile, findLocalUploads, hasLocalUploads,
  serverAcceptsUploads, uploadLocalData,
} from './localUpload';
import type { CustomFieldDef } from './customFields';

const BASE = 'http://api.test';
const LEAD = '11111111-1111-4111-8111-111111111111';
const OTHER_ORGS_LEAD = '22222222-2222-4222-8222-222222222222';
const toDataUrl = (text: string, type = 'text/plain') => `data:${type};base64,${btoa(text)}`;

type Server = {
  fields: CustomFieldDef[];
  created: unknown[];
  saved: { recordId: string; values: Record<string, unknown> }[];
  uploaded: { recordId: string; name: string; text: string }[];
};
let srv: Server;

function slug(label: string, taken: string[]) {
  const key = label.toLowerCase().replace(/[^a-z0-9]+/g, '_').replace(/^_|_$/g, '') || 'field';
  let n = 2;
  let candidate = key;
  while (taken.includes(candidate)) candidate = `${key}_${n++}`;
  return candidate;
}

// The backend as deployed: keys from labels, and a 404 *with a body* for a record
// that isn't in this workspace.
function backend() {
  const notHere = () => HttpResponse.json({ title: 'Not Found', detail: 'The record was not found' }, { status: 404 });
  server.use(
    http.get(`${BASE}/api/custom-fields`, () => HttpResponse.json(srv.fields)),
    http.post(`${BASE}/api/custom-fields`, async ({ request }) => {
      const body = (await request.json()) as Omit<CustomFieldDef, 'id' | 'key' | 'order'>;
      srv.created.push(body);
      const def = { ...body, id: `srv-${srv.fields.length}`, key: slug(body.label, srv.fields.map((f) => f.key)), order: srv.fields.length } as CustomFieldDef;
      srv.fields.push(def);
      return HttpResponse.json(def);
    }),
    http.put(`${BASE}/api/custom-fields/values`, async ({ request }) => {
      const body = (await request.json()) as { recordId: string; values: Record<string, unknown> };
      if (body.recordId === OTHER_ORGS_LEAD) return notHere();
      srv.saved.push({ recordId: body.recordId, values: body.values });
      return new HttpResponse(null, { status: 204 });
    }),
    http.get(`${BASE}/api/attachments`, () => HttpResponse.json([])),
    http.post(`${BASE}/api/attachments`, async ({ request }) => {
      const form = await request.formData();
      const recordId = String(form.get('recordId'));
      if (recordId === OTHER_ORGS_LEAD) return notHere();
      const file = form.get('file') as File;
      srv.uploaded.push({ recordId, name: file.name, text: await file.text() });
      return HttpResponse.json({ id: `att-${srv.uploaded.length}`, entityType: form.get('entityType'), recordId, fileName: file.name, contentType: file.type, size: file.size, url: '/x', uploadedAtUtc: new Date().toISOString() });
    }),
  );
}

function legacy(key: string, rows: unknown[]) {
  localStorage.setItem(key, JSON.stringify(rows));
}

const budgetDemo = { id: 'cf-seed-2', entityType: 'lead', key: 'budget', label: 'Budget', type: 'number', order: 0 };
const linkedinDemo = { id: 'cf-seed-1', entityType: 'contact', key: 'linkedin', label: 'LinkedIn', type: 'url', order: 0 };
const tourDate = { id: 'cf-mine-1', entityType: 'lead', key: 'tour_date_x7k2', label: 'Tour date', type: 'date', order: 1 };

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_org_id', 'org-a');
  srv = { fields: [], created: [], saved: [], uploaded: [] };
  backend();
});
afterEach(() => vi.unstubAllEnvs());

describe('findLocalUploads', () => {
  it("leaves out demo fields nobody used, and keeps the ones people did", () => {
    legacy(LOCAL_KEYS.fields, [budgetDemo, linkedinDemo, tourDate]);
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { budget: 9000, tour_date_x7k2: '' } }]);

    const plan = findLocalUploads();
    expect(plan.fields.map((f) => f.label)).toEqual(['Budget', 'Tour date']);
    expect(plan.records).toHaveLength(1);
  });

  it('ignores records whose values are all empty', () => {
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { budget: null, notes: '' } }]);
    expect(findLocalUploads().records).toEqual([]);
  });

  it('separates files the browser kept from ones it was too large to keep', () => {
    legacy(LOCAL_KEYS.attachments, [
      { id: 'a1', entityType: 'lead', recordId: LEAD, fileName: 'terms.txt', contentType: 'text/plain', size: 5, dataUrl: toDataUrl('terms'), uploadedAtUtc: '' },
      { id: 'a2', entityType: 'lead', recordId: LEAD, fileName: 'scan.pdf', contentType: 'application/pdf', size: 9_000_000, uploadedAtUtc: '' },
    ]);
    const plan = findLocalUploads();
    expect(plan.files.map((a) => a.fileName)).toEqual(['terms.txt']);
    expect(plan.unrecoverable.map((a) => a.fileName)).toEqual(['scan.pdf']);
  });

  it('reads both the old shared key and the current scoped one, without duplicates', () => {
    legacy(LOCAL_KEYS.fields, [tourDate]);
    legacy(`${LOCAL_KEYS.fields}:user:pat:org:org-a`, [tourDate, { ...tourDate, id: 'cf-mine-2', key: 'k2', label: 'Lake view' }]);
    expect(findLocalUploads().fields.map((f) => f.label)).toEqual(['Tour date', 'Lake view']);
  });

  it('brings back a demo field that only survives in the values that use it', () => {
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { budget: 500 } }]);
    expect(findLocalUploads().fields.map((f) => f.key)).toEqual(['budget']);
  });

  it("doesn't prompt a member about fields with no values, which only a manager could add", () => {
    legacy(LOCAL_KEYS.fields, [tourDate]);
    const plan = findLocalUploads();
    expect(hasLocalUploads(plan, false)).toBe(false);
    expect(hasLocalUploads(plan, true)).toBe(true);
  });
});

describe('uploadLocalData', () => {
  it('adds missing fields, reuses matching ones, and re-keys every value', async () => {
    srv.fields.push({ id: 'srv-existing', entityType: 'lead', key: 'budget', label: 'budget ', type: 'number', order: 0 });
    legacy(LOCAL_KEYS.fields, [budgetDemo, tourDate]);
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { budget: 9000, tour_date_x7k2: '2026-11-02' } }]);
    legacy(LOCAL_KEYS.attachments, [{ id: 'a1', entityType: 'lead', recordId: LEAD, fileName: 'terms.txt', contentType: 'text/plain', size: 5, dataUrl: toDataUrl('Terms v3'), uploadedAtUtc: '' }]);

    const result = await uploadLocalData(findLocalUploads(), { canDefineFields: true });

    expect(srv.created).toEqual([expect.objectContaining({ label: 'Tour date', type: 'date' })]);
    expect(srv.saved).toEqual([{ recordId: LEAD, values: { budget: 9000, tour_date: '2026-11-02' } }]);
    expect(srv.uploaded).toEqual([{ recordId: LEAD, name: 'terms.txt', text: 'Terms v3' }]);
    expect(result).toMatchObject({ fieldsAdded: 1, fieldsMatched: 1, recordsUploaded: 1, filesUploaded: 1, elsewhere: 0, failed: 0 });
  });

  it('clears what it uploaded and keeps a backup of the originals', async () => {
    legacy(LOCAL_KEYS.fields, [tourDate]);
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { tour_date_x7k2: '2026-11-02' } }]);

    await uploadLocalData(findLocalUploads(), { canDefineFields: true });

    expect(localStorage.getItem(LOCAL_KEYS.fields)).toBeNull();
    expect(localStorage.getItem(LOCAL_KEYS.values)).toBeNull();
    const backup = JSON.parse(localStorage.getItem(LOCAL_KEYS.values + UPLOAD_BACKUP_SUFFIX)!);
    expect(JSON.parse(backup[LOCAL_KEYS.values])[0].values).toEqual({ tour_date_x7k2: '2026-11-02' });
    expect(hasLocalUploads(findLocalUploads(), true)).toBe(false);
  });

  it("keeps another workspace's records in the browser and uploads them from there", async () => {
    legacy(LOCAL_KEYS.fields, [tourDate]);
    legacy(LOCAL_KEYS.values, [
      { id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { tour_date_x7k2: '2026-11-02' } },
      { id: `lead:${OTHER_ORGS_LEAD}`, entityType: 'lead', recordId: OTHER_ORGS_LEAD, values: { tour_date_x7k2: '2026-12-01' } },
    ]);
    legacy(LOCAL_KEYS.attachments, [{ id: 'a9', entityType: 'lead', recordId: OTHER_ORGS_LEAD, fileName: 'b.txt', contentType: 'text/plain', size: 1, dataUrl: toDataUrl('B'), uploadedAtUtc: '' }]);

    const result = await uploadLocalData(findLocalUploads(), { canDefineFields: true });
    expect(result).toMatchObject({ recordsUploaded: 1, elsewhere: 2, failed: 0 });

    const left = findLocalUploads();
    expect(left.records.map((r) => r.recordId)).toEqual([OTHER_ORGS_LEAD]);
    expect(left.files.map((f) => f.fileName)).toEqual(['b.txt']);
    // The field stays while its values still need it.
    expect(left.fields.map((f) => f.label)).toEqual(['Tour date']);
  });

  it('as a member, uploads values for fields the workspace has and keeps the rest', async () => {
    srv.fields.push({ id: 'srv-1', entityType: 'lead', key: 'budget', label: 'Budget', type: 'number', order: 0 });
    legacy(LOCAL_KEYS.fields, [budgetDemo, tourDate]);
    legacy(LOCAL_KEYS.values, [{ id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { budget: 9000, tour_date_x7k2: '2026-11-02' } }]);

    const result = await uploadLocalData(findLocalUploads(), { canDefineFields: false });

    expect(srv.created).toEqual([]);
    expect(srv.saved).toEqual([{ recordId: LEAD, values: { budget: 9000 } }]);
    expect(result.needsManager).toEqual(['Tour date']);
    expect(findLocalUploads().records[0]?.values).toEqual({ tour_date_x7k2: '2026-11-02' });
  });

  it('counts the server as ready when it says the made-up record is not found', async () => {
    // The server now checks the record before listing its files, so the readiness
    // probe's all-zero record gets a 404 that explains itself. A missing route says nothing.
    server.use(http.get(`${BASE}/api/attachments`, () =>
      HttpResponse.json({ status: 404, detail: 'The record to attach this to was not found' }, { status: 404 })));
    expect(await serverAcceptsUploads()).toBe(true);

    server.use(http.get(`${BASE}/api/attachments`, () => new HttpResponse(null, { status: 404 })));
    expect(await serverAcceptsUploads()).toBe(false);
  });

  it("changes nothing when the server doesn't serve these yet", async () => {
    server.use(http.get(`${BASE}/api/custom-fields`, () => new HttpResponse(null, { status: 404 })));
    legacy(LOCAL_KEYS.fields, [tourDate]);
    const before = localStorage.getItem(LOCAL_KEYS.fields);

    expect(await serverAcceptsUploads()).toBe(false);
    await expect(uploadLocalData(findLocalUploads(), { canDefineFields: true })).rejects.toThrow(/isn't ready/);
    expect(localStorage.getItem(LOCAL_KEYS.fields)).toBe(before);
    expect(localStorage.getItem(LOCAL_KEYS.fields + UPLOAD_BACKUP_SUFFIX)).toBeNull();
  });
});

describe('dataUrlToFile', () => {
  it('restores the bytes, name and type the browser saved', async () => {
    const file = dataUrlToFile(toDataUrl('héllo'.replace('é', 'e'), 'text/csv'), 'list.csv', 'application/octet-stream');
    expect(file.name).toBe('list.csv');
    expect(file.type).toBe('text/csv');
    expect(await file.text()).toBe('hello');
  });
});

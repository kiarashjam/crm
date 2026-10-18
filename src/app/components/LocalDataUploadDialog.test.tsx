// When the "upload what's saved in this browser" prompt appears, and what it says.
//
// It should ask the people who can act on it, once the server can take the data,
// and nobody else: not a Viewer, not with demo data, not before the backend serves
// these features. And after an upload it has to say plainly what moved and what is
// still waiting, because the rest stays in the browser for a later run.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import LocalDataUploadDialog from './LocalDataUploadDialog';

const BASE = 'http://api.test';
const LEAD = '11111111-1111-4111-8111-111111111111';

const org = vi.hoisted(() => ({
  value: null as null | { currentOrg: { id: string; name: string; role: number; isOwner?: boolean } | null; isReadOnly: boolean },
}));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.value }));

function asRole(role: number, isReadOnly = false) {
  org.value = { currentOrg: { id: 'org-a', name: 'Pavillon46', role }, isReadOnly };
}

function serverReady(ready = true) {
  server.use(
    http.get(`${BASE}/api/custom-fields`, () => (ready ? HttpResponse.json([]) : new HttpResponse(null, { status: 404 }))),
    http.get(`${BASE}/api/attachments`, () => HttpResponse.json([])),
    http.post(`${BASE}/api/custom-fields`, async ({ request }) => {
      const body = (await request.json()) as { label: string; entityType: string; type: string };
      return HttpResponse.json({ ...body, id: 'srv-1', key: 'tour_date', order: 0 });
    }),
    http.put(`${BASE}/api/custom-fields/values`, () => new HttpResponse(null, { status: 204 })),
  );
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_org_id', 'org-a');
  localStorage.setItem('crm.mock.customFieldDefs.v1', JSON.stringify([
    { id: 'cf-mine-1', entityType: 'lead', key: 'tour_date_x7k2', label: 'Tour date', type: 'date', order: 0 },
  ]));
  localStorage.setItem('crm.mock.customFieldValues.v1', JSON.stringify([
    { id: `lead:${LEAD}`, entityType: 'lead', recordId: LEAD, values: { tour_date_x7k2: '2026-11-02' } },
  ]));
});
afterEach(() => vi.unstubAllEnvs());

describe('LocalDataUploadDialog', () => {
  it('asks a manager, says what will move, and reports what did', async () => {
    asRole(2);
    serverReady();
    render(<LocalDataUploadDialog />);

    expect(await screen.findByText(/upload what's saved in this browser/i)).toBeInTheDocument();
    expect(screen.getByText('1 custom field')).toBeInTheDocument();
    expect(screen.getByText('Values on 1 record')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /upload/i }));
    expect(await screen.findByText('Added 1 custom field. Uploaded values on 1 record.')).toBeInTheDocument();
  });

  it("doesn't ask a Viewer, who can't upload anything", async () => {
    asRole(3, true);
    serverReady();
    render(<LocalDataUploadDialog />);
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.queryByText(/upload what's saved/i)).toBeNull();
  });

  it("doesn't ask before the server serves these features", async () => {
    asRole(0);
    serverReady(false);
    render(<LocalDataUploadDialog />);
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.queryByText(/upload what's saved/i)).toBeNull();
  });

  it("doesn't ask with demo data, where the browser is the store", async () => {
    vi.stubEnv('VITE_API_URL', '');
    asRole(0);
    render(<LocalDataUploadDialog />);
    await new Promise((r) => setTimeout(r, 50));
    expect(screen.queryByText(/upload what's saved/i)).toBeNull();
  });

  it('"Not now" leaves everything in place and stops asking for this session', async () => {
    asRole(0);
    serverReady();
    render(<LocalDataUploadDialog />);
    fireEvent.click(await screen.findByRole('button', { name: /not now/i }));

    expect(screen.queryByText(/upload what's saved/i)).toBeNull();
    expect(sessionStorage.getItem('crm.localUpload.snoozed.v1')).toBe('1');
    expect(localStorage.getItem('crm.mock.customFieldValues.v1')).not.toBeNull();
  });
});

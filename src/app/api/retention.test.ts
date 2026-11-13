// Keeping members against the server: what each call sends, and how a refusal comes back.
//
// The score, the board's stages, the bonus and the report are all the server's work, so these
// calls are thin: the tests pin the paths, the bodies (the version a save is made against, the
// card a stage is set on, a referrer taken off as null) and that a save from an older copy is
// told apart as an edit conflict.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import {
  downloadReferralReportCsv, getMemberRisk, getSponsorStanding, recomputeMemberRisk, saveRetentionSettings, setReferrer, setRenewalStage,
  type RenewalCard,
} from './retention';
import { createApplication, updateApplication, NO_MEMBER } from './applications';
import { isEditConflict } from '@/app/lib/editConflict';

const saved = vi.hoisted(() => ({ files: [] as string[] }));
vi.mock('@/app/lib/exportCsv', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/lib/exportCsv')>()),
  downloadFile: (_blob: Blob, name: string) => saved.files.push(name),
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

const RISK = {
  memberId: 'm1', score: 63, level: 'at_risk', computedAtUtc: '2026-11-13T05:00:00Z', atRiskThreshold: 60, watchThreshold: 35, notes: [],
  factors: [{ key: 'visits', label: 'Visits', weight: 30, strength: 1, points: 30, detail: '0 visits in the last 90 days, against about 13 in a usual 90 days before.' }],
};

describe('the at-risk score', () => {
  it("reads a member's score out of the server's answer, and null before their first", async () => {
    server.use(http.get(`${BASE}/api/members/m1/risk`, () => HttpResponse.json({ risk: RISK })));
    server.use(http.get(`${BASE}/api/members/m2/risk`, () => HttpResponse.json({ risk: null })));

    expect((await getMemberRisk('m1'))?.score).toBe(63);
    expect(await getMemberRisk('m2')).toBeNull();
  });

  it('works a score out again with one request', async () => {
    server.use(http.post(`${BASE}/api/members/m1/risk/recompute`, () => HttpResponse.json({ ...RISK, notes: ['A task was made.'] })));

    const risk = await recomputeMemberRisk('m1');

    expect(risk.notes).toEqual(['A task was made.']);
    expect(requests.map((r) => `${r.method} ${r.path}`)).toEqual(['POST /api/members/m1/risk/recompute']);
  });
});

describe("the club's rules", () => {
  it('are saved against the version read, and a save from an older copy is an edit conflict', async () => {
    server.use(http.put(`${BASE}/api/retention/settings`, () => HttpResponse.json(
      { title: 'Conflict', detail: 'Someone else changed the rules.', errorCode: 'RetentionSettings.Changed.Conflict' }, { status: 409 })));

    const refused = await saveRetentionSettings({ saveTask: true, version: 'v-1' }).catch((err: unknown) => err);

    expect(isEditConflict(refused)).toBe(true);
    expect(JSON.parse(requests[0]!.body)).toEqual({ saveTask: true, version: 'v-1' });
  });
});

describe('the renewals board', () => {
  it('sets a stage on the card it was shown, with its version', async () => {
    server.use(http.post(`${BASE}/api/renewals/stage`, () => HttpResponse.json({ memberId: 'm1', stage: 'renewed' })));
    const card = { memberId: 'm1', source: 'membership', endsAtUtc: '2026-12-01T10:00:00.1234567Z', version: 'v-2' } as RenewalCard;

    await setRenewalStage(card, { stage: 'renewed', note: 'Paid at the bar' });

    expect(JSON.parse(requests[0]!.body)).toEqual({
      memberId: 'm1', source: 'membership', subscriptionId: null, endsAtUtc: '2026-12-01T10:00:00.1234567Z', stage: 'renewed',
      note: 'Paid at the bar', version: 'v-2',
    });
  });
});

describe('referrals', () => {
  it('takes a referrer off as null', async () => {
    server.use(http.put(`${BASE}/api/members/m1/referrer`, () => HttpResponse.json({ memberId: 'm1', referred: [], proposed: [], seconded: [], bonusesEarned: [], bonusAvailable: false })));

    await setReferrer('m1', null);

    expect(JSON.parse(requests[0]!.body)).toEqual({ referredByMemberId: null });
  });

  it("asks for several members' standing in one request", async () => {
    server.use(http.get(`${BASE}/api/referrals/standing`, () => HttpResponse.json([])));

    await getSponsorStanding(['a', 'b']);

    expect(requests[0]!.search).toBe('?memberIds=a&memberIds=b');
  });

  it("saves the report as the server's spreadsheet, under the name it gave", async () => {
    server.use(http.get(`${BASE}/api/referrals/report/export`, () => new HttpResponse('Referrer;Referred member\n', {
      headers: { 'Content-Type': 'text/csv', 'Content-Disposition': 'attachment; filename="referrals-2025-11-13-to-2026-11-12.csv"' },
    })));

    await downloadReferralReportCsv({ fromUtc: '2025-11-13T00:00:00Z', toUtc: '2026-11-13T00:00:00Z' });

    expect(saved.files).toEqual(['referrals-2025-11-13-to-2026-11-12.csv']);
    expect(requests[0]!.search).toBe('?from=2025-11-13T00%3A00%3A00Z&to=2026-11-13T00%3A00%3A00Z');
  });

  it('sends an application its sponsors by member id, and one taken off as the empty id the server reads as none', async () => {
    server.use(http.post(`${BASE}/api/applications`, () => HttpResponse.json({ id: 'a1' })));
    server.use(http.put(`${BASE}/api/applications/a1`, () => HttpResponse.json({ id: 'a1' })));

    await createApplication({ firstName: 'Claire', lastName: 'Martin', email: 'claire@example.ch', requestedTier: 'Gold', proposerMemberId: 'm-ben' });
    await updateApplication('a1', { referredByMemberId: '', proposerMemberId: 'm-ana', version: 'v-3' });

    expect(JSON.parse(requests[0]!.body)).toMatchObject({ proposerMemberId: 'm-ben' });
    expect(JSON.parse(requests[1]!.body)).toEqual({ referredByMemberId: NO_MEMBER, proposerMemberId: 'm-ana', version: 'v-3' });
  });
});

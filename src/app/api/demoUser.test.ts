// The "Explore the demo" user on a build that has a server.
//
// The demo is offered on every build. Its user has no account and no token, so a
// request from it can only come back 401 — and a 401 signs the session out. The core
// modules asked "is there a server?" where they meant "is this a real account?", so
// opening the demo on a production build sent the visitor to the server and back to
// the login page. Everything here has to come from this browser's sample data.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { getLeads, getLeadStats } from './leads';
import { getContacts } from './contacts';
import { getDeals } from './deals';
import { getTasks, getTaskStats } from './tasks';
import { getActivities } from './activities';
import { getCompanies } from './companies';
import { getDashboardStats } from './reporting';
import { getPipelines } from './pipelines';
import { getSequences } from './sequences';
import { getTemplates } from './templates';
import { getUserSettings } from './settings';
import { getLeadSources } from './leadSources';
import { getLeadStatuses } from './leadStatuses';
import { getCopyHistory } from './copyHistory';
import { getOrgMembers } from './organizations';
import { getWebhookInfo } from './webhook';
import { getConversionReport } from './leadOps';
import { twoFactorSetup } from './auth';

let requests: string[];

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', 'http://api.test');
  localStorage.setItem('aci_user', JSON.stringify({ id: 'demo', name: 'Demo User', email: 'demo@example.com' }));
  requests = [];
  server.events.on('request:start', ({ request }) => {
    requests.push(`${request.method} ${new URL(request.url).pathname}`);
  });
});
afterEach(() => {
  server.events.removeAllListeners();
  vi.unstubAllEnvs();
});

describe('the demo user on a build with a server', () => {
  it('reads every core page from the sample data, and asks the server nothing', async () => {
    const results = await Promise.all([
      getLeads(), getLeadStats(), getContacts(), getDeals(), getTasks(), getTaskStats(),
      getActivities(), getCompanies(), getDashboardStats(), getPipelines(), getSequences(),
      getTemplates(), getUserSettings(), getLeadSources(), getLeadStatuses(), getCopyHistory(),
      getOrgMembers('demo-org'), getWebhookInfo('demo-org'), getConversionReport(),
    ]);
    // Settings asks for this on every visit; the demo has no account to protect.
    await expect(twoFactorSetup()).rejects.toThrow(/accounts/);

    expect(requests).toEqual([]);
    // The sample data, not an empty shell: the demo shows a working CRM.
    const [leads, , contacts, deals] = results;
    expect(leads.length).toBeGreaterThan(0);
    expect(contacts.length).toBeGreaterThan(0);
    expect(deals.length).toBeGreaterThan(0);
  });

  it('a signed-in account still goes to the server', async () => {
    localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
    localStorage.setItem('aci_token', 'token');
    server.use(http.get('http://api.test/*', () => HttpResponse.json([])));
    await getDeals();
    expect(requests.some((r) => r.startsWith('GET /api/deals'))).toBe(true);
  });
});

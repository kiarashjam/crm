// Settings > System health: the verdict, each area with its count and rows, links to where each
// thing is fixed, Try again where the app already has it, the jobs with a dot each, an empty
// state for each area, and a fresh read every minute.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type { SystemHealth } from '@/app/api/systemHealth';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false,
    currentOrgId: 'org-a',
    organizations: [],
    hasFetched: true,
    currentOrg: { id: 'org-a', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 2, permissions },
  };
};

const { SystemHealthSection } = await import('./SystemHealthSection');

const BASE = 'http://api.test';
const minutesAgo = (m: number) => new Date(Date.now() - m * 60_000).toISOString();

const job = (name: string, over: Partial<SystemHealth['jobs'][number]> = {}): SystemHealth['jobs'][number] => ({
  name, intervalSeconds: 300, lastSeenAtUtc: minutesAgo(2), lastBeatAtUtc: minutesAgo(2), lastFailureAtUtc: null,
  failing: false, silent: false, consecutiveFailures: 0, lastError: null, ...over,
});

const allGood = (): SystemHealth => ({
  checkedAtUtc: minutesAgo(0),
  overall: { status: 'ok', issues: 0, counts: { handoffs: 0, outbox: 0, webhooks: 0, payments: 0, automations: 0, email: 0, jobs: 0, portal: 0 } },
  handoffs: { count: 0, items: [] },
  outbox: { count: 0, queued: 0, failed: 0, items: [] },
  webhooks: { count: 0, items: [], link: '/settings?tab=integrations' },
  payments: { count: 0, items: [], link: '/club-payments' },
  automations: { count: 0, items: [] },
  email: { count: 0, bounces: 0, spamReports: 0, newSuppressions: 0, items: [], link: '/settings?tab=organization' },
  jobs: [job('outbox', { intervalSeconds: 30 }), job('portal-ping', { intervalSeconds: 86_400 })],
  portal: { configured: true, ok: true, atUtc: minutesAgo(180), statusCode: 200, error: null, link: '/settings?tab=welcome' },
});

const trouble = (): SystemHealth => ({
  ...allGood(),
  overall: { status: 'failing', issues: 4, counts: { handoffs: 1, outbox: 1, webhooks: 0, payments: 0, automations: 1, email: 0, jobs: 2, portal: 1 } },
  handoffs: {
    count: 1,
    items: [{
      id: 'h1', dealId: 'd1', dealName: 'Gold membership', person: 'Jean Dupont', state: 'failed',
      lastError: 'The portal refused the address', attempts: 9, sinceUtc: minutesAgo(90), link: '/deals/d1', retry: { kind: 'handoff', id: 'h1' },
    }],
  },
  outbox: {
    count: 1, queued: 0, failed: 1,
    items: [{
      id: 'm1', kind: 'email', state: 'failed', to: 'anna@example.ch', subject: 'Your visit', lastError: '550 mailbox unavailable',
      attempts: 1, createdAtUtc: minutesAgo(60 * 26), nextAttemptAtUtc: null, link: '/contacts/c1', retry: null,
    }],
  },
  automations: {
    count: 1,
    items: [{
      runId: 'run1', ruleId: 'r1', ruleName: 'Welcome rule', recordName: 'Lead Anna', lastError: 'No address',
      attempts: 3, failedAtUtc: minutesAgo(30), link: '/automations?rule=r1', retry: { kind: 'automation', id: 'run1' },
    }],
  },
  jobs: [
    job('automations', { failing: true, consecutiveFailures: 3, lastError: 'InvalidOperationException: The database went away' }),
    job('outbox', { intervalSeconds: 30 }),
    job('retention', { silent: true, intervalSeconds: 86_400, lastSeenAtUtc: minutesAgo(60 * 80) }),
  ],
  portal: { configured: true, ok: false, atUtc: minutesAgo(5), statusCode: 503, error: 'The receiver answered 503', link: '/settings?tab=welcome' },
});

let answer: () => Response;
let reads: number;
let resent: string[];
let retried: string[];

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'u-owner', name: 'Anna', email: 'anna@pavillon46.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  as([PERMISSION.ManageSettings, PERMISSION.EditRecords]);
  reads = 0;
  resent = [];
  retried = [];
  answer = () => HttpResponse.json(allGood());
  server.use(
    http.get(`${BASE}/api/system-health`, () => {
      reads++;
      return answer();
    }),
    http.post(`${BASE}/api/onboarding/:id/resend`, ({ params }) => {
      resent.push(String(params.id));
      return HttpResponse.json({ id: params.id, status: 'queued' });
    }),
    http.post(`${BASE}/api/automations/runs/:id/retry`, ({ params }) => {
      retried.push(String(params.id));
      return HttpResponse.json({ id: params.id, status: 'running' });
    }),
  );
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllEnvs();
});

const show = () => render(withQueries(<MemoryRouter><SystemHealthSection /></MemoryRouter>));

describe('System health', () => {
  it('says all is good, with an empty state for each area and a green dot for each job', async () => {
    show();

    expect(await screen.findByRole('heading', { name: 'All good' })).toBeInTheDocument();
    expect(screen.getByText('Every handoff reached the portal.')).toBeInTheDocument();
    expect(screen.getByText('Nothing stuck, nothing failed this week.')).toBeInTheDocument();
    expect(screen.getByText('No run failed this week.')).toBeInTheDocument();
    expect(screen.getByText('No bounce or spam report this week.')).toBeInTheDocument();
    expect(screen.getByText('Every event reached your address this week.')).toBeInTheDocument();
    expect(screen.getByText('Every Stripe event was processed this week.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Try again/ })).not.toBeInTheDocument();

    const jobs = screen.getByRole('table', { name: 'Background jobs' });
    const rows = within(jobs).getAllByRole('row').slice(1);
    expect(rows.map((r) => within(r).getAllByRole('cell')[0]!.textContent)).toEqual(['outbox', 'portal-ping']);
    expect(within(rows[0]!).getByText('Running')).toBeInTheDocument();
    expect(within(rows[1]!).getByText('day')).toBeInTheDocument();
    expect(screen.getByText('All running')).toBeInTheDocument();
    expect(screen.getByText('Connected')).toBeInTheDocument();
  });

  it('counts what needs attention, links each thing to where it is fixed, and shows the failing jobs', async () => {
    answer = () => HttpResponse.json(trouble());
    show();

    expect(await screen.findByRole('heading', { name: '4 things need attention' })).toBeInTheDocument();
    expect(screen.getByText(/Something is failing now/)).toBeInTheDocument();

    const handoffs = screen.getByRole('list', { name: 'Welcome handoffs' });
    expect(within(handoffs).getByRole('link', { name: 'Jean Dupont' })).toHaveAttribute('href', '/deals/d1');
    expect(within(handoffs).getByText('Needs attention')).toBeInTheDocument();
    expect(within(handoffs).getByText('The portal refused the address')).toBeInTheDocument();
    expect(within(handoffs).getByText('1 h ago')).toBeInTheDocument();

    const outbox = screen.getByRole('list', { name: 'Outbox' });
    expect(within(outbox).getByRole('link', { name: 'anna@example.ch' })).toHaveAttribute('href', '/contacts/c1');
    expect(within(outbox).getByText('550 mailbox unavailable')).toBeInTheDocument();
    expect(within(outbox).queryByRole('button', { name: /Try again/ })).not.toBeInTheDocument();

    const runs = screen.getByRole('list', { name: 'Automations' });
    expect(within(runs).getByRole('link', { name: 'Welcome rule' })).toHaveAttribute('href', '/automations?rule=r1');

    const jobs = screen.getByRole('table', { name: 'Background jobs' });
    expect(within(jobs).getByText('Failing')).toBeInTheDocument();
    expect(within(jobs).getByText('Silent')).toBeInTheDocument();
    expect(within(jobs).getByText('InvalidOperationException: The database went away')).toBeInTheDocument();
    expect(screen.getByText('2 not running well')).toBeInTheDocument();

    expect(screen.getByText('Not answering')).toBeInTheDocument();
    expect(screen.getByText(/The receiver answered 503/)).toBeInTheDocument();
  });

  it('tries a handoff and a run again with the endpoints that do that, and reads the page again', async () => {
    answer = () => HttpResponse.json(trouble());
    const user = userEvent.setup();
    show();

    await user.click(await screen.findByRole('button', { name: 'Try again: the welcome for Jean Dupont' }));
    await waitFor(() => expect(resent).toEqual(['h1']));
    expect(toast.success).toHaveBeenCalledWith('Sent to the portal again.');
    await waitFor(() => expect(reads).toBe(2));

    await user.click(screen.getByRole('button', { name: 'Try again: the run of Welcome rule' }));
    await waitFor(() => expect(retried).toEqual(['run1']));
    await waitFor(() => expect(reads).toBe(3));
  });

  it('offers to resend a handoff only to those who may change records', async () => {
    as([PERMISSION.ManageSettings]);
    answer = () => HttpResponse.json(trouble());
    show();

    expect(await screen.findByRole('button', { name: 'Try again: the run of Welcome rule' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /the welcome for Jean Dupont/ })).not.toBeInTheDocument();
  });

  it('reads it again every minute while it is open', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    show();
    expect(await screen.findByRole('heading', { name: 'All good' })).toBeInTheDocument();
    expect(reads).toBe(1);

    answer = () => HttpResponse.json(trouble());
    await act(async () => { await vi.advanceTimersByTimeAsync(60_000); });

    expect(await screen.findByRole('heading', { name: '4 things need attention' })).toBeInTheDocument();
    expect(reads).toBe(2);
  });

  it('says why when the server refuses', async () => {
    answer = () => HttpResponse.json(
      { title: 'Not allowed for your role', detail: "Your role here does not let you see the workspace's system health." },
      { status: 403 },
    );
    show();

    expect(await screen.findByText(/does not let you see the workspace's system health/)).toBeInTheDocument();
  });
});

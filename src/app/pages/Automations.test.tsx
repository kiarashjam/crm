// The automations page, against the real server.
//
// It used to read its rules once into local state, so a failed read looked like a
// workspace with no rules; its rules were a trigger and a list of actions, every time.
// Now a rule has conditions on the record, waits, if/else branches and a send window,
// the server runs it and keeps a log of every run, and whoever manages the workspace's
// settings sees the runs, tries a failed one again or stops one.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import type {
  AutomationCatalog, AutomationRule, AutomationRun, AutomationRunDetail, AutomationTriggerDef,
} from '@/app/api/automations';

vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
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
    currentOrg: {
      id: 'org-a', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, timeZone: 'Europe/Zurich',
    },
  };
};

const Automations = (await import('./Automations')).default;

const BASE = 'http://api.test';
const CATALOG: AutomationCatalog = {
  triggers: [
    {
      key: 'lead.created', label: 'When a lead is created', group: 'Sales', module: 'sales', record: 'lead',
      fields: [
        { key: 'status', label: 'Status', type: 'choice', options: 'leadStatus' },
        { key: 'score', label: 'Score', type: 'number' },
      ],
      dates: [],
      actions: ['create_task', 'send_email', 'notify', 'assign', 'add_to_sequence'],
      description: 'A lead is added.',
    },
    {
      key: 'reservation.created', label: 'When a booking is made', group: 'Club', module: 'club', record: 'reservation',
      fields: [{ key: 'partySize', label: 'Party size', type: 'number' }],
      dates: [{ key: 'start', label: "The booking's start" }],
      actions: ['create_task', 'send_email', 'notify'],
      description: 'A space is booked.',
    },
  ],
  ops: {
    text: ['is', 'is_not', 'contains', 'not_contains', 'is_any_of', 'is_none_of', 'is_empty', 'is_not_empty'],
    choice: ['is', 'is_not', 'is_any_of', 'is_none_of', 'is_empty', 'is_not_empty'],
    user: ['is', 'is_not', 'is_any_of', 'is_none_of', 'is_empty', 'is_not_empty'],
    number: ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'is_empty', 'is_not_empty'],
    money: ['eq', 'neq', 'gt', 'gte', 'lt', 'lte', 'between', 'is_empty', 'is_not_empty'],
    bool: ['is_true', 'is_false'],
  },
};
const WELCOME: AutomationRule = {
  id: 'rule-1',
  name: 'Welcome new leads',
  enabled: true,
  trigger: 'lead.created',
  conditions: null,
  sendWindow: null,
  actions: [{ id: 'a1b2c3d4-0000-4000-8000-000000000001', type: 'create_task', config: { title: 'Qualify', dueInDays: '1' } }],
  runCount: 12,
  createdAtUtc: '2026-10-01T08:00:00Z',
  version: 'v1',
  activeRuns: 0,
  failedRuns: 1,
};
const FAILED: AutomationRun = {
  id: 'run-1',
  ruleId: WELCOME.id,
  ruleName: WELCOME.name,
  trigger: 'lead.created',
  status: 'failed',
  recordType: 'lead',
  recordId: 'lead-1',
  recordName: 'Jean Dupont',
  recordLink: '/leads/lead-1',
  currentPath: '0',
  currentStep: 'send_email',
  stepAttempts: 5,
  nextAttemptAtUtc: null,
  waitingFor: null,
  lastError: 'The mail server did not answer.',
  startedAtUtc: '2026-11-10T08:00:00Z',
  updatedAtUtc: '2026-11-10T11:00:00Z',
  finishedAtUtc: '2026-11-10T11:00:00Z',
};
const FAILED_LOG: AutomationRunDetail = {
  run: FAILED,
  steps: [
    {
      id: 'log-1', path: '0', stepType: 'send_email', outcome: 'retrying', summary: 'Could not queue the email; trying again in 1 minute.',
      error: 'The mail server did not answer.', attempt: 1, atUtc: '2026-11-10T08:00:00Z', emailStatus: null,
    },
    {
      id: 'log-2', path: '0', stepType: 'send_email', outcome: 'failed', summary: 'Gave up after 5 attempts.',
      error: 'The mail server did not answer.', attempt: 5, atUtc: '2026-11-10T11:00:00Z', emailStatus: 'failed',
    },
  ],
};

let rules: AutomationRule[];
let failReads: number;
let posted: Record<string, unknown> | null;
let put: Record<string, unknown> | null;
let putAnswer: () => Response;
let runQueries: string[];
let retried: string[];

beforeEach(() => {
  localStorage.clear();
  toast.success.mockReset();
  toast.error.mockReset();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_user', JSON.stringify({ id: 'pat', name: 'Pierre', email: 'pierre@example.ch' }));
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_org_id', 'org-a');
  as([PERMISSION.EditRecords, PERMISSION.ManageSettings]);
  rules = [WELCOME];
  failReads = 0;
  posted = null;
  put = null;
  putAnswer = () => HttpResponse.json({ ...WELCOME, ...put, version: 'v2' });
  runQueries = [];
  retried = [];
  server.use(
    http.get(`${BASE}/api/automations`, () => (failReads-- > 0
      ? HttpResponse.json({ title: 'Server Error', status: 500, detail: 'The server is busy.' }, { status: 500 })
      : HttpResponse.json(rules))),
    http.get(`${BASE}/api/automations/catalog`, () => HttpResponse.json(CATALOG)),
    http.get(`${BASE}/api/automations/runs`, ({ request }) => {
      runQueries.push(new URL(request.url).search);
      return HttpResponse.json([FAILED]);
    }),
    http.get(`${BASE}/api/automations/runs/:id`, () => HttpResponse.json(FAILED_LOG)),
    http.post(`${BASE}/api/automations/runs/:id/retry`, ({ params }) => {
      retried.push(String(params.id));
      return HttpResponse.json({ ...FAILED, status: 'completed' });
    }),
    http.post(`${BASE}/api/automations`, async ({ request }) => {
      posted = (await request.json()) as Record<string, unknown>;
      return HttpResponse.json({ ...posted, id: 'rule-new', createdAtUtc: '2026-11-12T08:00:00Z', version: 'v9', actions: [] });
    }),
    http.put(`${BASE}/api/automations/:id`, async ({ request }) => {
      put = (await request.json()) as Record<string, unknown>;
      return putAnswer();
    }),
    http.get(`${BASE}/api/leadstatuses`, () => HttpResponse.json([
      { id: 's-1', name: 'New', order: 0 },
      { id: 's-2', name: 'Qualified', order: 1 },
    ])),
    http.get(`${BASE}/api/organizations/org-a/members`, () => HttpResponse.json([
      { userId: 'u-anna', name: 'Anna Keller', email: 'anna@example.ch', role: 1 },
    ])),
    http.get(`${BASE}/api/sequences`, () => HttpResponse.json([])),
    // The regional settings, for how dates are written.
    http.get(`${BASE}/api/settings`, () => HttpResponse.json({})),
  );
});
afterEach(() => vi.unstubAllEnvs());

const show = () => render(withQueries(<MemoryRouter><Automations /></MemoryRouter>));

async function openNew(user: ReturnType<typeof userEvent.setup>) {
  await user.click(await screen.findByRole('button', { name: /New automation/ }));
  const dialog = await screen.findByRole('dialog');
  await within(dialog).findByRole('combobox', { name: 'Trigger' });
  return dialog;
}

describe('the automations page', () => {
  it('says a failed read failed, with a way to try again, instead of showing no rules', async () => {
    failReads = 1;
    const user = userEvent.setup();
    show();

    expect(await screen.findByText('Automations could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText('No automations yet')).toBeNull();

    await user.click(screen.getByRole('button', { name: /Try again/ }));
    expect(await screen.findByRole('heading', { name: 'Welcome new leads' })).toBeInTheDocument();
  });

  it('saves conditions, a wait, an if / else and a send window as the server reads them', async () => {
    const user = userEvent.setup();
    show();
    const dialog = await openNew(user);

    await user.type(within(dialog).getByLabelText('Name'), 'Qualified follow-up');

    // Only qualified leads: the statuses are the workspace's own.
    await user.click(within(dialog).getByRole('button', { name: /Add a condition/ }));
    await user.selectOptions(await within(dialog).findByRole('combobox', { name: 'Condition 1: value' }), 'Qualified');

    // The first step is an email.
    await user.type(within(dialog).getByLabelText('Step 1: subject'), 'Welcome');
    await user.type(within(dialog).getByLabelText('Step 1: body'), 'Hello there');

    // Then two days' wait.
    await user.click(within(dialog).getByRole('button', { name: /Add a wait/ }));
    const howLong = within(dialog).getByLabelText('Step 2: how long');
    await user.clear(howLong);
    await user.type(howLong, '2');

    // Then, if the lead is still new, a task.
    await user.click(within(dialog).getByRole('button', { name: /Add an if \/ else/ }));
    await user.selectOptions(await within(dialog).findByRole('combobox', { name: 'Step 3 condition 1: value' }), 'New');
    await user.click(within(dialog).getByRole('button', { name: 'Add an action to Step 3, then' }));
    await user.type(within(dialog).getByLabelText('Step 3 then step 1: task title'), 'Call them');

    // Emails only on weekdays and Saturdays, in office hours.
    await user.click(within(dialog).getByLabelText(/Only send this rule.s emails at certain times/));
    await user.click(within(dialog).getByRole('button', { name: 'Saturday' }));
    expect(within(dialog).getByText(/Europe\/Zurich/)).toBeInTheDocument();

    await user.click(within(dialog).getByRole('button', { name: 'Create automation' }));

    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted).toEqual({
      name: 'Qualified follow-up',
      enabled: true,
      trigger: 'lead.created',
      conditions: { match: 'all', conditions: [{ field: 'status', op: 'is', value: 'Qualified' }], groups: [] },
      sendWindow: { days: [1, 2, 3, 4, 5, 6], start: '09:00', end: '17:00' },
      actions: [
        { type: 'send_email', config: { subject: 'Welcome', body: 'Hello there' } },
        { type: 'wait', config: { amount: '2', unit: 'days' } },
        {
          type: 'branch',
          config: {},
          condition: { match: 'all', conditions: [{ field: 'status', op: 'is', value: 'New' }], groups: [] },
          then: [{ type: 'create_task', config: { title: 'Call them', dueInDays: '1' } }],
          else: [],
        },
      ],
    });
    expect(toast.success).toHaveBeenCalledWith('Automation created');
  });

  it("waits until a date on the record, and takes off conditions the new trigger cannot test", async () => {
    const user = userEvent.setup();
    show();
    const dialog = await openNew(user);
    await user.type(within(dialog).getByLabelText('Name'), 'Booking reminder');
    await user.click(within(dialog).getByRole('button', { name: /Add a condition/ }));

    // A booking has no status: the condition on it goes, and the person is told.
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Trigger' }), 'reservation.created');
    expect(within(dialog).getByText('Conditions this trigger cannot test were taken off.')).toBeInTheDocument();
    expect(within(dialog).queryByRole('combobox', { name: 'Condition 1: field' })).toBeNull();

    await user.type(within(dialog).getByLabelText('Step 1: subject'), 'See you tomorrow');
    await user.type(within(dialog).getByLabelText('Step 1: body'), 'Your booking is tomorrow.');
    // A member's email says what it is for: this one is about their booking.
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Step 1: kind of email' }), 'service');

    // The email waits for a day before the booking starts.
    await user.click(within(dialog).getByRole('button', { name: /Add a wait/ }));
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Step 2: wait for' }), 'until');
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Step 2: when' }), 'before');
    await user.click(within(dialog).getByRole('button', { name: 'Move step 2 up' }));

    await user.click(within(dialog).getByRole('button', { name: 'Create automation' }));
    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted?.trigger).toBe('reservation.created');
    expect(posted?.conditions).toBeNull();
    expect(posted?.actions).toEqual([
      { type: 'wait', config: { mode: 'until', date: 'start', offsetAmount: '-1', offsetUnit: 'days' } },
      { type: 'send_email', config: { subject: 'See you tomorrow', body: 'Your booking is tomorrow.', purpose: 'service' } },
    ]);
  });

  it("tests the record's tags and custom fields, and waits for one of its custom dates", async () => {
    // The server adds the workspace's own tags and custom fields to each trigger it can test.
    const [lead, booking] = CATALOG.triggers as [AutomationTriggerDef, AutomationTriggerDef];
    server.use(http.get(`${BASE}/api/automations/catalog`, () => HttpResponse.json({
      ...CATALOG,
      triggers: [{
        ...lead,
        fields: [
          ...lead.fields,
          { key: 'tags', label: 'Tags', type: 'choice', choices: [{ value: 't-vip', label: 'VIP' }, { value: 't-cold', label: 'Cold' }] },
          { key: 'cf:renewal', label: 'Renewal: days from today', type: 'number' },
        ],
        dates: [{ key: 'cf:renewal', label: "The lead's Renewal" }],
      }, booking],
    })));
    const user = userEvent.setup();
    show();
    const dialog = await openNew(user);
    await user.type(within(dialog).getByLabelText('Name'), 'VIP renewals');

    // VIP leads whose renewal is at most 30 days away.
    await user.click(within(dialog).getByRole('button', { name: /Add a condition/ }));
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Condition 1: field' }), 'tags');
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Condition 1: value' }), 'VIP');
    await user.click(within(dialog).getByRole('button', { name: /Add a condition/ }));
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Condition 2: field' }), 'cf:renewal');
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Condition 2: comparison' }), 'lte');
    await user.type(within(dialog).getByLabelText('Condition 2: value'), '30');

    await user.type(within(dialog).getByLabelText('Step 1: subject'), 'Renewal soon');
    await user.type(within(dialog).getByLabelText('Step 1: body'), 'Your renewal is coming up.');
    // The email waits for a day before the renewal date.
    await user.click(within(dialog).getByRole('button', { name: /Add a wait/ }));
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Step 2: wait for' }), 'until');
    await user.selectOptions(within(dialog).getByRole('combobox', { name: 'Step 2: when' }), 'before');
    expect(within(dialog).getByRole('combobox', { name: 'Step 2: date' })).toHaveDisplayValue("the lead's Renewal");
    await user.click(within(dialog).getByRole('button', { name: 'Move step 2 up' }));

    await user.click(within(dialog).getByRole('button', { name: 'Create automation' }));
    await waitFor(() => expect(posted).not.toBeNull());
    expect(posted?.conditions).toEqual({
      match: 'all',
      conditions: [{ field: 'tags', op: 'is', value: 't-vip' }, { field: 'cf:renewal', op: 'lte', value: '30' }],
      groups: [],
    });
    expect(posted?.actions).toEqual([
      { type: 'wait', config: { mode: 'until', date: 'cf:renewal', offsetAmount: '-1', offsetUnit: 'days' } },
      { type: 'send_email', config: { subject: 'Renewal soon', body: 'Your renewal is coming up.' } },
    ]);
  });

  it('sends the version it read, and says so when someone else saved the rule meanwhile', async () => {
    putAnswer = () => HttpResponse.json(
      { title: 'Conflict', status: 409, detail: 'Someone changed this automation after you opened it.', errorCode: 'Automation.Changed.Conflict' },
      { status: 409 },
    );
    const user = userEvent.setup();
    show();
    await user.click(await screen.findByRole('button', { name: 'Edit Welcome new leads' }));
    const dialog = await screen.findByRole('dialog');
    const name = await within(dialog).findByLabelText('Name');
    await user.clear(name);
    await user.type(name, 'Welcome every lead');
    await user.click(within(dialog).getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(put).not.toBeNull());
    expect(put).toMatchObject({
      name: 'Welcome every lead',
      version: 'v1',
      // A rule without conditions keeps running for every record.
      conditions: null,
      sendWindow: null,
      actions: [{ id: WELCOME.actions[0]?.id, type: 'create_task', config: { title: 'Qualify', dueInDays: '1' } }],
    });
    await waitFor(() => expect(toast.error).toHaveBeenCalledWith(
      'Someone changed this automation after you opened it.',
      expect.objectContaining({ action: expect.objectContaining({ label: 'Reload' }) }),
    ));
    expect(toast.success).not.toHaveBeenCalled();
  });

  it("shows the runs, a failed run's log with its email, and tries it again", async () => {
    const user = userEvent.setup();
    show();

    expect(await screen.findByRole('heading', { name: 'Runs' })).toBeInTheDocument();
    expect(await screen.findByText(/Failed at "Send an email" after 5 attempts\. The mail server did not answer\./)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Jean Dupont' })).toHaveAttribute('href', '/leads/lead-1');

    await user.click(screen.getByRole('button', { name: /Show the log of Welcome new leads for Jean Dupont/ }));
    const log = await screen.findByRole('list', { name: 'Run log' });
    expect(within(log).getByText('Gave up after 5 attempts.')).toBeInTheDocument();
    expect(within(log).getByText('Email: failed')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Failed' }));
    await waitFor(() => expect(runQueries).toContain('?status=failed'));

    await user.click(screen.getByRole('button', { name: /Try again/ }));
    await waitFor(() => expect(retried).toEqual(['run-1']));
    expect(toast.success).toHaveBeenCalledWith('Tried again. The log says how it went.');
  });

  it("shows a member who cannot manage settings the rules, but no controls and no runs", async () => {
    as([PERMISSION.EditRecords]);
    show();

    expect(await screen.findByRole('heading', { name: 'Welcome new leads' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /New automation/ })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Edit Welcome new leads' })).toBeNull();
    expect(screen.queryByRole('switch')).toBeNull();
    expect(screen.queryByRole('heading', { name: 'Runs' })).toBeNull();
    // What the rule did is counted on it, but its runs are not read.
    expect(screen.getByText('1 failed')).toBeInTheDocument();
    expect(runQueries).toEqual([]);
  });
});

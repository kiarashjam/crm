// Workflow automation.
//
// A rule is a trigger, optional conditions on the record that set it off, and steps:
// actions, waits and branches. The server runs it: the steps up to the first wait in the
// save that set it off, the rest from its automation runner, with a log of every step.
// Demo mode keeps the rules in the browser so the builder can be tried; it runs nothing.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type TriggerType =
  | 'lead.created'
  | 'lead.status_changed'
  | 'deal.stage_changed'
  | 'deal.won'
  | 'deal.lost'
  | 'contract.signed'
  | 'contract.countersigned'
  | 'task.overdue'
  | 'membership.renewal_due'
  | 'member.created'
  | 'application.approved'
  | 'reservation.created'
  | 'event.registered'
  | 'payment.failed'
  | 'member.at_risk';

export type ActionType =
  | 'create_task'
  | 'send_email'
  | 'notify'
  | 'assign'
  | 'add_to_sequence';

/** Every step a rule can have: an action, a wait, or a branch. */
export type StepType = ActionType | 'wait' | 'branch';

/** One test of one field of the record: "stage is Proposal", "value is at least 5000". */
export interface AutomationCondition {
  field: string;
  op: string;
  value?: string;
  /** The choices for is any of / is none of. */
  values?: string[];
  /** The upper end for between. */
  to?: string;
}

/** Conditions joined by all (and) or any (or), with groups of their own one level down. */
export interface ConditionGroup {
  match: 'all' | 'any';
  conditions: AutomationCondition[];
  groups?: ConditionGroup[];
}

export interface AutomationStep {
  /** Kept across edits once the server gave it; a new step has none. */
  id?: string;
  type: StepType;
  config: Record<string, string>;
  /** A branch's condition, read on the record when the branch is reached. */
  condition?: ConditionGroup | null;
  then?: AutomationStep[] | null;
  else?: AutomationStep[] | null;
}

/** A step as the server keeps it: always with an id. */
export type AutomationAction = AutomationStep & { id: string };

/** The days (1 Monday … 7 Sunday) and hours a rule's emails may go, in the workspace's time zone. */
export interface SendWindow {
  days: number[];
  start: string;
  end: string;
}

export interface AutomationRule {
  id: string;
  name: string;
  enabled: boolean;
  trigger: TriggerType;
  /** The trigger's own settings: the days ahead a renewal rule fires. */
  triggerConfig?: Record<string, string> | null;
  conditions?: ConditionGroup | null;
  sendWindow?: SendWindow | null;
  actions: AutomationAction[];
  runCount?: number;
  createdAtUtc: string;
  /** The rule as read; an edit of an older copy is refused (409). */
  version?: string;
  /** Runs still going (waiting, retrying) and runs that failed. */
  activeRuns?: number;
  failedRuns?: number;
}

export const TRIGGER_LABELS: Record<TriggerType, string> = {
  'lead.created': 'When a lead is created',
  'lead.status_changed': 'When a lead status changes',
  'deal.stage_changed': 'When a deal moves stage',
  'deal.won': 'When a deal is won',
  'deal.lost': 'When a deal is lost',
  'contract.signed': 'When a client signs a contract',
  'contract.countersigned': 'When a contract is countersigned',
  'task.overdue': 'When a task becomes overdue',
  'membership.renewal_due': 'When a membership is due to renew',
  'member.created': 'When a member is added',
  'application.approved': 'When an application is approved',
  'reservation.created': 'When a booking is made',
  'event.registered': 'When someone registers for an event',
  'payment.failed': 'When an online payment fails',
  'member.at_risk': 'When a member becomes at risk',
};

export const ACTION_LABELS: Record<ActionType, string> = {
  create_task: 'Create a task',
  send_email: 'Send an email',
  notify: 'Send a notification',
  assign: 'Assign owner',
  add_to_sequence: 'Enroll in a sequence',
};

export const STEP_LABELS: Record<StepType, string> = {
  ...ACTION_LABELS,
  wait: 'Wait',
  branch: 'If / else',
};

/** How each comparison reads in the builder. */
export const OP_LABELS: Record<string, string> = {
  is: 'is',
  is_not: 'is not',
  is_any_of: 'is any of',
  is_none_of: 'is none of',
  contains: 'contains',
  not_contains: 'does not contain',
  is_empty: 'is empty',
  is_not_empty: 'is set',
  eq: 'equals',
  neq: 'does not equal',
  gt: 'is over',
  gte: 'is at least',
  lt: 'is under',
  lte: 'is at most',
  between: 'is between',
  is_true: 'is yes',
  is_false: 'is no',
};

// ── the catalogue ────────────────────────────────────────────────────────────

export type AutomationFieldType = 'text' | 'choice' | 'user' | 'number' | 'money' | 'bool';

export interface AutomationChoice {
  value: string;
  label: string;
}

export interface AutomationFieldDef {
  key: string;
  label: string;
  type: AutomationFieldType;
  /** For a choice of the workspace's own (leadStatus, dealStage, tier, user…): the list to read. */
  options?: string | null;
  /** For a fixed choice: its values. */
  choices?: AutomationChoice[] | null;
}

export interface AutomationTriggerDef {
  key: TriggerType;
  label: string;
  group: string;
  module?: 'sales' | 'club' | null;
  record: string;
  fields: AutomationFieldDef[];
  dates: { key: string; label: string }[];
  actions: ActionType[];
  description: string;
  takesDaysBefore?: boolean;
}

export interface AutomationCatalog {
  triggers: AutomationTriggerDef[];
  ops: Record<string, string[]>;
}

const LEAD_FIELDS: AutomationFieldDef[] = [
  { key: 'status', label: 'Status', type: 'choice', options: 'leadStatus' },
  { key: 'source', label: 'Source', type: 'choice', options: 'leadSource' },
  { key: 'owner', label: 'Owner', type: 'user', options: 'user' },
  { key: 'score', label: 'Score', type: 'number' },
];
const DEAL_FIELDS: AutomationFieldDef[] = [
  { key: 'stage', label: 'Stage', type: 'choice', options: 'dealStage' },
  { key: 'value', label: 'Value', type: 'money' },
  { key: 'owner', label: 'Owner', type: 'user', options: 'user' },
];
const MEMBER_FIELDS: AutomationFieldDef[] = [
  { key: 'tier', label: "Member's tier", type: 'choice', options: 'tier' },
  {
    key: 'memberStatus', label: "Member's status", type: 'choice',
    choices: ['Pending', 'Active', 'Lapsed', 'Cancelled'].map((v) => ({ value: v, label: v })),
  },
];
const SALES: ActionType[] = ['create_task', 'send_email', 'notify', 'assign', 'add_to_sequence'];
const CLUB: ActionType[] = ['create_task', 'send_email', 'notify'];

/** What demo mode builds rules from: the server's catalogue, cut down. */
export const DEMO_CATALOG: AutomationCatalog = {
  triggers: [
    { key: 'lead.created', label: TRIGGER_LABELS['lead.created'], group: 'Sales', module: 'sales', record: 'lead', fields: LEAD_FIELDS, dates: [], actions: SALES, description: 'A lead is added.' },
    {
      key: 'lead.status_changed', label: TRIGGER_LABELS['lead.status_changed'], group: 'Sales', module: 'sales', record: 'lead',
      fields: [...LEAD_FIELDS, { key: 'fromStatus', label: 'Previous status', type: 'choice', options: 'leadStatus' }], dates: [], actions: SALES,
      description: 'A lead moves to another status.',
    },
    { key: 'deal.stage_changed', label: TRIGGER_LABELS['deal.stage_changed'], group: 'Sales', module: 'sales', record: 'deal', fields: DEAL_FIELDS, dates: [{ key: 'expectedClose', label: "The deal's expected close date" }], actions: SALES, description: 'A deal moves stage.' },
    { key: 'deal.won', label: TRIGGER_LABELS['deal.won'], group: 'Sales', module: 'sales', record: 'deal', fields: DEAL_FIELDS, dates: [], actions: SALES, description: 'A deal is won.' },
    { key: 'deal.lost', label: TRIGGER_LABELS['deal.lost'], group: 'Sales', module: 'sales', record: 'deal', fields: DEAL_FIELDS, dates: [], actions: SALES, description: 'A deal is lost.' },
    { key: 'task.overdue', label: TRIGGER_LABELS['task.overdue'], group: 'Tasks', module: null, record: 'task', fields: [], dates: [{ key: 'due', label: "The task's due date" }], actions: SALES, description: 'A task passes its due time.' },
    {
      key: 'membership.renewal_due', label: TRIGGER_LABELS['membership.renewal_due'], group: 'Club', module: 'club', record: 'subscription',
      fields: MEMBER_FIELDS, dates: [{ key: 'periodEnd', label: 'The date the subscription renews' }], actions: CLUB, takesDaysBefore: true,
      description: "A running subscription's period ends within the days the rule says.",
    },
    { key: 'member.created', label: TRIGGER_LABELS['member.created'], group: 'Club', module: 'club', record: 'member', fields: MEMBER_FIELDS, dates: [], actions: CLUB, description: 'A member is added.' },
    { key: 'reservation.created', label: TRIGGER_LABELS['reservation.created'], group: 'Club', module: 'club', record: 'reservation', fields: MEMBER_FIELDS, dates: [{ key: 'start', label: "The booking's start" }], actions: CLUB, description: 'A space is booked.' },
    {
      key: 'member.at_risk', label: TRIGGER_LABELS['member.at_risk'], group: 'Club', module: 'club', record: 'member',
      fields: [...MEMBER_FIELDS, { key: 'riskScore', label: 'At-risk score (0 to 100)', type: 'number' }], dates: [], actions: CLUB,
      description: "The daily scoring finds a member's at-risk score has reached the club's threshold. It fires once each time they become at risk, and not again within the club's cooldown.",
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

export async function getAutomationCatalog(req?: RequestOptions): Promise<AutomationCatalog> {
  return apiWithFallback(
    () => authFetchJson<AutomationCatalog>('/api/automations/catalog', req),
    async () => DEMO_CATALOG,
  );
}

// ── rules ────────────────────────────────────────────────────────────────────

const ruleStore = createMockStore<AutomationRule>({
  storageKey: 'crm.mock.automations.v1',
  seed: [
    {
      id: 'auto-seed-1',
      name: 'Welcome new leads',
      enabled: true,
      trigger: 'lead.created',
      actions: [
        { id: 'a1', type: 'send_email', config: { subject: 'Thanks for reaching out', body: 'Hi {{firstName}}, thanks for your interest!' } },
        { id: 'a2', type: 'create_task', config: { title: 'Qualify new lead', dueInDays: '1' } },
      ],
      runCount: 42,
      createdAtUtc: new Date(Date.now() - 20 * 86_400_000).toISOString(),
    },
    {
      id: 'auto-seed-2',
      name: 'Celebrate + handoff won deals',
      enabled: false,
      trigger: 'deal.won',
      actions: [
        { id: 'a3', type: 'notify', config: { message: 'A deal was won 🎉' } },
        { id: 'a4', type: 'create_task', config: { title: 'Kick off onboarding', dueInDays: '2' } },
      ],
      runCount: 7,
      createdAtUtc: new Date(Date.now() - 5 * 86_400_000).toISOString(),
    },
  ],
  idOf: (r) => r.id,
});

export async function getAutomations(req?: RequestOptions): Promise<AutomationRule[]> {
  return apiWithFallback(
    async () => { const res = await authFetchJson<AutomationRule[]>('/api/automations', req); return Array.isArray(res) ? res : []; },
    async () => { await delay(140); return [...ruleStore.list()].sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc)); },
  );
}

export interface AutomationInput {
  name: string;
  enabled?: boolean;
  trigger: TriggerType;
  /** Null takes the trigger's settings off. */
  triggerConfig?: Record<string, string> | null;
  /** Null takes the conditions off. */
  conditions?: ConditionGroup | null;
  /** Null takes the send window off. */
  sendWindow?: SendWindow | null;
  actions: AutomationStep[];
  /** The version the rule was read at: an edit of an older one is refused. */
  version?: string;
}

/** Steps as the mock store keeps them: each with an id, branches' sides too. */
function withIds(steps: AutomationStep[]): AutomationAction[] {
  return steps.map((s) => ({
    ...s,
    id: s.id ?? mockId('step'),
    then: s.then ? withIds(s.then) : s.then,
    else: s.else ? withIds(s.else) : s.else,
  }));
}

export async function createAutomation(input: AutomationInput): Promise<AutomationRule | null> {
  return apiWithFallback(
    () => authFetchJson<AutomationRule>('/api/automations', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(180);
      return ruleStore.add({
        id: mockId('auto'),
        name: input.name,
        enabled: input.enabled ?? true,
        trigger: input.trigger,
        triggerConfig: input.triggerConfig,
        conditions: input.conditions,
        sendWindow: input.sendWindow,
        actions: withIds(input.actions),
        runCount: 0,
        createdAtUtc: new Date().toISOString(),
      });
    },
  );
}

export async function updateAutomation(id: string, input: Partial<AutomationInput>): Promise<AutomationRule | null> {
  return apiWithFallback(
    () => authFetchJson<AutomationRule>(`/api/automations/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
    async () => {
      await delay(140);
      const { version: _version, actions, ...rest } = input;
      const patch: Partial<AutomationRule> = { ...rest } as Partial<AutomationRule>;
      if (actions) patch.actions = withIds(actions);
      return ruleStore.update(id, patch);
    },
  );
}

export async function deleteAutomation(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/automations/${id}`, { method: 'DELETE' }); await ensureOk(res); return true; },
    async () => { await delay(120); return ruleStore.remove(id); },
  );
}

// ── runs ─────────────────────────────────────────────────────────────────────

export type AutomationRunStatus = 'running' | 'waiting' | 'retrying' | 'completed' | 'failed' | 'cancelled';

export interface AutomationRun {
  id: string;
  ruleId: string;
  ruleName: string;
  trigger: TriggerType;
  status: AutomationRunStatus;
  recordType: string;
  recordId?: string | null;
  recordName?: string | null;
  /** The page of the record it is about. */
  recordLink?: string | null;
  currentPath: string;
  /** The step it is at (or failed at), while it is not finished. */
  currentStep?: StepType | null;
  stepAttempts: number;
  nextAttemptAtUtc?: string | null;
  waitingFor?: string | null;
  lastError?: string | null;
  startedAtUtc: string;
  updatedAtUtc: string;
  finishedAtUtc?: string | null;
}

export interface AutomationRunLogEntry {
  id: string;
  path: string;
  stepType: StepType;
  outcome: 'done' | 'skipped' | 'waiting' | 'retrying' | 'failed';
  summary: string;
  error?: string | null;
  attempt: number;
  atUtc: string;
  outboundMessageId?: string | null;
  /** Where the email it queued stands in the outbox: queued, sent, delivered, failed… */
  emailStatus?: string | null;
  taskId?: string | null;
}

export interface AutomationRunDetail {
  run: AutomationRun;
  steps: AutomationRunLogEntry[];
}

export interface RunQuery {
  ruleId?: string;
  /** active (still going), failed, completed, cancelled; all when left out. */
  status?: string;
}

export async function getAutomationRuns(query: RunQuery = {}, req?: RequestOptions): Promise<AutomationRun[]> {
  const params = new URLSearchParams();
  if (query.ruleId) params.set('ruleId', query.ruleId);
  if (query.status) params.set('status', query.status);
  const qs = params.toString();
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<AutomationRun[]>(`/api/automations/runs${qs ? `?${qs}` : ''}`, req);
      return Array.isArray(res) ? res : [];
    },
    // Demo mode runs nothing, so it has no runs to show.
    async () => [],
  );
}

export async function getAutomationRun(runId: string, req?: RequestOptions): Promise<AutomationRunDetail | null> {
  return apiWithFallback(
    () => authFetchJson<AutomationRunDetail>(`/api/automations/runs/${runId}`, req),
    async () => null,
  );
}

/** Tries a failed run's failed step again, now. */
export async function retryAutomationRun(runId: string): Promise<AutomationRun> {
  return authFetchJson<AutomationRun>(`/api/automations/runs/${runId}/retry`, { method: 'POST' });
}

/** Stops a run that has not finished. */
export async function cancelAutomationRun(runId: string): Promise<AutomationRun> {
  return authFetchJson<AutomationRun>(`/api/automations/runs/${runId}/cancel`, { method: 'POST' });
}

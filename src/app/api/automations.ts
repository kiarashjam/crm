// Workflow automation.
//
// A rule is trigger → (optional conditions) → actions. The backend runs the
// engine that evaluates triggers and executes actions; demo mode persists the
// rule definitions locally so the builder is fully usable offline.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
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
  | 'task.overdue';

export type ActionType =
  | 'create_task'
  | 'send_email'
  | 'notify'
  | 'assign'
  | 'add_to_sequence';

export interface AutomationAction {
  id: string;
  type: ActionType;
  config: Record<string, string>;
}

export interface AutomationRule {
  id: string;
  name: string;
  enabled: boolean;
  trigger: TriggerType;
  triggerConfig?: Record<string, string>;
  actions: AutomationAction[];
  runCount?: number;
  createdAtUtc: string;
}

export const TRIGGER_LABELS: Record<TriggerType, string> = {
  'lead.created': 'When a lead is created',
  'lead.status_changed': 'When a lead status changes',
  'deal.stage_changed': 'When a deal moves stage',
  'deal.won': 'When a deal is won',
  'deal.lost': 'When a deal is lost',
  'task.overdue': 'When a task becomes overdue',
};

export const ACTION_LABELS: Record<ActionType, string> = {
  create_task: 'Create a task',
  send_email: 'Send an email',
  notify: 'Send a notification',
  assign: 'Assign owner',
  add_to_sequence: 'Enroll in a sequence',
};

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

export async function getAutomations(): Promise<AutomationRule[]> {
  return apiWithFallback(
    async () => { const res = await authFetchJson<AutomationRule[]>('/api/automations'); return Array.isArray(res) ? res : []; },
    async () => { await delay(140); return [...ruleStore.list()].sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc)); },
  );
}

export interface AutomationInput {
  name: string;
  enabled?: boolean;
  trigger: TriggerType;
  triggerConfig?: Record<string, string>;
  actions: Omit<AutomationAction, 'id'>[];
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
        actions: input.actions.map((a) => ({ ...a, id: mockId('act') })),
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
      const patch: Partial<AutomationRule> = { ...input } as Partial<AutomationRule>;
      if (input.actions) patch.actions = input.actions.map((a) => ({ ...a, id: mockId('act') }));
      return ruleStore.update(id, patch);
    },
  );
}

export async function deleteAutomation(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/automations/${id}`, { method: 'DELETE' }); if (!(res.status === 204 || res.ok)) throw new Error('failed'); return true; },
    async () => { await delay(120); return ruleStore.remove(id); },
  );
}

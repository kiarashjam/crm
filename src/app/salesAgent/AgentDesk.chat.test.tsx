import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { AgentWorld } from './brain/answer';
import type { WorkSuggestion } from './workspace';

vi.mock('@/app/api/activities', () => ({ createActivity: vi.fn() }));
vi.mock('@/app/api/contacts', () => ({ createContact: vi.fn() }));
vi.mock('@/app/api/deals', () => ({ createDeal: vi.fn(), updateDeal: vi.fn() }));
vi.mock('@/app/api/dedupe', () => ({ mergeCompanies: vi.fn(), mergeContacts: vi.fn() }));
vi.mock('@/app/api/leads', () => ({ assignLead: vi.fn(), convertLead: vi.fn() }));
vi.mock('@/app/api/sequences', () => ({ enrollInSequence: vi.fn() }));
vi.mock('@/app/api/tasks', () => ({ addTaskComment: vi.fn(), createTask: vi.fn() }));
vi.mock('@/app/api/email', () => ({ sendEmail: vi.fn() }));
vi.mock('@/app/api/assistant', () => ({ askAssistant: vi.fn() }));
vi.mock('@/app/api/charges', () => ({ createCharge: vi.fn() }));
vi.mock('@/app/api/reservations', () => ({ createReservation: vi.fn() }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({})),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));
vi.mock('@/app/lib/auth', () => ({
  getCurrentUser: () => ({ id: 'user-1', name: 'Rep', email: 'rep@example.com' }),
  getCurrentOrganizationId: () => 'org-a',
}));

import { askAssistant, type AssistantReply } from '@/app/api/assistant';
import { ApiError } from '@/app/api/apiClient';
import { createCharge } from '@/app/api/charges';
import { createTask } from '@/app/api/tasks';
import { AgentDesk, resetAgentDeskForTests } from './AgentDesk';

const ask = vi.mocked(askAssistant);
const usage = { used: 1, limit: 300, month: '2026-11-01', resetsOn: '2026-12-01', refused: 0, inputTokens: 100, outputTokens: 20, left: 299 };
const reply = (over: Partial<AssistantReply>): AssistantReply => ({ reply: '', proposals: [], lookedUp: [], usage, incomplete: false, ...over });

const ada: WorkSuggestion = {
  key: 'lead:a1',
  href: '/leads/a1',
  name: 'Ada Lovelace',
  list: 'Outreach',
  action: 'Make the first call.',
  because: 'Outreach has not started.',
  priority: 80,
  email: 'ada@example.ch',
  guide: 'start_outreach',
};

function world(extra: Partial<AgentWorld> = {}): AgentWorld {
  return {
    items: [ada],
    leads: [{ id: 'a1', name: 'Ada Lovelace', email: 'ada@example.ch', phone: '555-0100' }],
    deals: [{ id: 'd1', name: 'Pavilion renewal', value: '48000', amount: 48000, stage: 'Proposal' }],
    page: { mode: 'focus' },
    patterns: { lines: [], wonDeals: 0, lostDeals: 0, lostLeads: 0 },
    ...extra,
  };
}

function open(props: Partial<Parameters<typeof AgentDesk>[0]> = {}) {
  return render(
    <MemoryRouter>
      <AgentDesk world={world()} readOnly={false} accountKey="user-1:org-a" initialOpen {...props} />
    </MemoryRouter>,
  );
}

const box = () => screen.getByLabelText('Ask the agent');

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  resetAgentDeskForTests();
  vi.clearAllMocks();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('the ask box as a chat', () => {
  it('answers from its rules at once: no pause is staged, and it does not call itself AI', () => {
    open();
    fireEvent.change(box(), { target: { value: 'what should I do next?' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    // The question and the answer are both there straight away, with nothing pretending to think.
    expect(screen.getByText('what should I do next?')).toBeTruthy();
    expect(screen.getAllByText(/Make the first call/).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Open Ada Lovelace' })).toBeTruthy();
    expect(screen.queryByRole('status', { name: 'The assistant is answering' })).toBeNull();
    expect(screen.getByText('Ready')).toBeTruthy();
    expect(screen.getByRole('heading', { name: 'Ask Cadence' })).toBeTruthy();
    expect(screen.queryByText('AI')).toBeNull();
    expect(ask).not.toHaveBeenCalled();
  });

  it('Shift+Enter is a new line, not a send', () => {
    open();
    fireEvent.change(box(), { target: { value: 'who went quiet?' } });
    fireEvent.keyDown(box(), { key: 'Enter', shiftKey: true });
    expect(screen.queryByRole('list', { name: 'Conversation with the agent' })).toBeNull();
    expect((box() as HTMLTextAreaElement).value).toBe('who went quiet?');
  });

  it('starts with questions that fit the page, and one press asks them', () => {
    open();
    const starters = screen.getByRole('group', { name: 'Questions to start with' });
    expect(starters.textContent).toContain('What should I do next?');
    expect(starters.textContent).toContain('Pipeline value');
    fireEvent.click(screen.getByRole('button', { name: 'Pipeline value' }));
    expect(screen.getAllByText(/Calculated from loaded open deals/).length).toBeGreaterThan(0);
  });

  it('on a lead page the starters are about that lead', () => {
    open({ world: world({ page: { mode: 'record', href: '/leads/a1' } }) });
    fireEvent.click(screen.getByRole('button', { name: 'Brief this' }));
    expect(screen.getAllByText(/Briefing for Ada Lovelace/).length).toBeGreaterThan(0);
  });

  it('after an answer about a record, the follow-ups name it', () => {
    open();
    fireEvent.change(box(), { target: { value: 'brief Ada Lovelace' } });
    fireEvent.submit(box().closest('form') as HTMLFormElement);
    const next = screen.getByRole('group', { name: 'Questions to ask next' });
    expect(next.textContent).toContain('Draft a message to Ada Lovelace');
    fireEvent.click(screen.getByRole('button', { name: 'Draft a message to Ada Lovelace' }));
    expect(screen.getByRole('button', { name: 'Draft message for Ada Lovelace' })).toBeTruthy();
  });

  it('a new chat clears the conversation and what it was about', () => {
    open();
    fireEvent.change(box(), { target: { value: 'brief Ada Lovelace' } });
    fireEvent.submit(box().closest('form') as HTMLFormElement);
    fireEvent.click(screen.getByRole('button', { name: 'New chat' }));
    expect(screen.queryByText('brief Ada Lovelace')).toBeNull();
    expect(screen.getByRole('group', { name: 'Questions to start with' })).toBeTruthy();
    expect(localStorage.getItem('cadence.agent.turns.user-1:org-a')).toBe('[]');
  });

  it('will not send an empty question', () => {
    open();
    expect(screen.getByRole('button', { name: 'Send' })).toHaveProperty('disabled', true);
    fireEvent.keyDown(box(), { key: 'Enter' });
    expect(screen.queryByRole('list', { name: 'Conversation with the agent' })).toBeNull();
  });

  it('starts closed, as a bar that opens the chat, and closes again to that bar', async () => {
    open({ initialOpen: false });
    expect(screen.queryByLabelText('Ask the agent')).toBeNull();
    fireEvent.click(screen.getByRole('button', { name: /^Ask Cadence/ }));
    // The bar animates out first, then the chat opens with the cursor in its box.
    expect(await screen.findByLabelText('Ask the agent')).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByLabelText('Ask the agent'));
    expect(localStorage.getItem('cadence.nextActions.chatOpen')).toBe('true');

    fireEvent.change(box(), { target: { value: 'who went quiet?' } });
    fireEvent.keyDown(box(), { key: 'Enter' });
    fireEvent.click(screen.getByRole('button', { name: 'Close chat' }));
    // The conversation is kept, and the bar says so.
    expect(await screen.findByRole('button', { name: /Continue the conversation · 2 messages/ })).toBeTruthy();
    expect(screen.queryByLabelText('Ask the agent')).toBeNull();
    expect(localStorage.getItem('cadence.nextActions.chatOpen')).toBe('false');
  });

  it('remembers the choice across pages', () => {
    localStorage.setItem('cadence.nextActions.chatOpen', 'true');
    const first = open({ initialOpen: false });
    expect(screen.getByLabelText('Ask the agent')).toBeTruthy();
    first.unmount();
    localStorage.setItem('cadence.nextActions.chatOpen', 'false');
    open({ initialOpen: true });
    expect(screen.queryByLabelText('Ask the agent')).toBeNull();
  });
});

describe('with a language model set up', () => {
  const task = {
    id: 'p1', kind: 'task' as const, title: 'Create a task', details: ['Task: Call Ada', 'On lead: Ada Lovelace'],
    payload: { title: 'Call Ada', leadId: 'a1' }, link: '/leads/a1',
  };

  it('asks it, with the conversation so far and the page, and calls itself AI', async () => {
    ask.mockResolvedValue(reply({ reply: 'Ada is new: make the first call.', lookedUp: ['get_lead'] }));
    open({ modelReady: true });
    expect(screen.getByRole('heading', { name: /Ask Cadence AI/ })).toBeTruthy();

    fireEvent.change(box(), { target: { value: 'Who is Ada?' } });
    fireEvent.keyDown(box(), { key: 'Enter' });
    expect(screen.getByRole('status', { name: 'The assistant is answering' })).toBeTruthy();

    expect(await screen.findByText('Ada is new: make the first call.')).toBeTruthy();
    expect(screen.getByText('Looked up: get lead')).toBeTruthy();
    expect(ask).toHaveBeenCalledWith(
      expect.objectContaining({ message: 'Who is Ada?', history: [], page: '/', language: 'en' }),
      expect.anything(),
    );
  });

  it('shows a proposed change as a card, and saves it only on Confirm, once', async () => {
    ask.mockResolvedValue(reply({ reply: 'I propose a task.', proposals: [task] }));
    vi.mocked(createTask).mockResolvedValue({ id: 't1', title: 'Call Ada' } as never);
    open({ modelReady: true });
    fireEvent.change(box(), { target: { value: 'Remind me to call Ada' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    const card = await screen.findByRole('group', { name: 'Proposed: Create a task' });
    expect(within(card).getByText('Task: Call Ada')).toBeTruthy();
    expect(createTask).not.toHaveBeenCalled();

    fireEvent.click(within(card).getByRole('button', { name: 'Confirm' }));
    expect(await screen.findByText('Task saved: Call Ada.')).toBeTruthy();
    expect(createTask).toHaveBeenCalledTimes(1);
    expect(createTask).toHaveBeenCalledWith({ title: 'Call Ada', leadId: 'a1' });
    expect(within(card).queryByRole('button', { name: 'Confirm' })).toBeNull();
    expect(within(card).getByText('Done.')).toBeTruthy();
  });

  it('a dismissed proposal changes nothing', async () => {
    ask.mockResolvedValue(reply({ reply: 'I propose a task.', proposals: [task] }));
    open({ modelReady: true });
    fireEvent.change(box(), { target: { value: 'Remind me to call Ada' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    const card = await screen.findByRole('group', { name: 'Proposed: Create a task' });
    fireEvent.click(within(card).getByRole('button', { name: 'Dismiss' }));
    expect(within(card).getByText('Dismissed. Nothing was changed.')).toBeTruthy();
    expect(createTask).not.toHaveBeenCalled();
  });

  it("a proposed charge carries its proposal's id, so a second request cannot charge twice", async () => {
    const charge = {
      id: 'p2', kind: 'charge' as const, title: 'Charge a member', details: ['Ada Lovelace: CHF 40.00'],
      payload: { memberId: 'm1', memberName: 'Ada Lovelace', kind: 'Bar', description: 'Wine', amount: 40 },
    };
    ask.mockResolvedValue(reply({ reply: 'Shall I charge it?', proposals: [charge] }));
    vi.mocked(createCharge).mockResolvedValue({ id: 'c1', memberName: 'Ada Lovelace', description: 'Wine', amount: 40 } as never);
    open({ modelReady: true });
    fireEvent.change(box(), { target: { value: 'Charge Ada for the wine' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    fireEvent.click(within(await screen.findByRole('group', { name: 'Proposed: Charge a member' })).getByRole('button', { name: 'Confirm' }));
    expect(await screen.findByText(/Charged to Ada Lovelace/)).toBeTruthy();
    expect(createCharge).toHaveBeenCalledWith(charge.payload, { idempotencyKey: 'p2' });
  });

  it('in a read-only workspace a proposal cannot be confirmed', async () => {
    ask.mockResolvedValue(reply({ reply: 'I propose a task.', proposals: [task] }));
    open({ modelReady: true, readOnly: true });
    fireEvent.change(box(), { target: { value: 'Remind me to call Ada' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    const card = await screen.findByRole('group', { name: 'Proposed: Create a task' });
    expect(within(card).getByRole('button', { name: 'Confirm' })).toHaveProperty('disabled', true);
  });

  it('when the model cannot answer, the built-in rules do, and the chat says why', async () => {
    ask.mockRejectedValue(new ApiError('This workspace has used its 300 AI calls for this month.', 429, '/api/agent/chat', true, 'Ai.MonthlyLimit'));
    open({ modelReady: true });
    fireEvent.change(box(), { target: { value: 'what should I do next?' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    expect((await screen.findAllByText(/Make the first call/)).length).toBeGreaterThan(0);
    const notice = await screen.findByText(/used its 300 AI calls/);
    expect(notice.textContent).toContain("Cadence's built-in rules");
  });

  it('in a workspace with only the club, it offers club questions and leaves the sales rules out of it', async () => {
    ask.mockRejectedValue(new ApiError('No language model is set up.', 503, '/api/agent/chat', true, 'Assistant.NotConfigured'));
    open({ modelReady: true, clubOnly: true, world: world({ items: [], leads: [], deals: [] }) });
    expect(screen.getByRole('group', { name: 'Questions to start with' }).textContent).toContain('What is on at the club today?');

    fireEvent.click(screen.getByRole('button', { name: 'Who is past due on their account?' }));
    expect(await screen.findByText('The assistant is not set up on the server.')).toBeTruthy();
    expect(screen.queryByText(/built-in rules/)).toBeNull();
  });
});

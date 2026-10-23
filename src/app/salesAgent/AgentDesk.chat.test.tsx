import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
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
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({})),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));
vi.mock('@/app/lib/auth', () => ({
  getCurrentUser: () => ({ id: 'user-1', name: 'Rep', email: 'rep@example.com' }),
  getCurrentOrganizationId: () => 'org-a',
}));

import { AgentDesk, resetAgentDeskForTests } from './AgentDesk';

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
    deals: [{ id: 'd1', name: 'Pavilion renewal', value: '48000', stage: 'Proposal' }],
    page: { mode: 'focus' },
    patterns: { lines: [], wonDeals: 0, lostDeals: 0, lostLeads: 0 },
    ...extra,
  };
}

function open(props: Partial<Parameters<typeof AgentDesk>[0]> = {}) {
  return render(
    <MemoryRouter>
      <AgentDesk world={world()} readOnly={false} accountKey="user-1:org-a" {...props} />
    </MemoryRouter>,
  );
}

const box = () => screen.getByLabelText('Ask the agent');

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
  resetAgentDeskForTests();
});

afterEach(() => {
  vi.useRealTimers();
});

describe('the ask box as a chat', () => {
  it('shows the question at once, thinks, then answers', () => {
    vi.useFakeTimers();
    open({ thinkingMs: 600 });
    fireEvent.change(box(), { target: { value: 'what should I do next?' } });
    fireEvent.keyDown(box(), { key: 'Enter' });

    expect(screen.getByText('what should I do next?')).toBeTruthy();
    expect(screen.getByRole('status', { name: 'The agent is thinking' })).toBeTruthy();
    expect(screen.queryByText(/Make the first call/)).toBeNull();

    act(() => { vi.advanceTimersByTime(650); });
    // The thinking bubble fades out; the header is ready again and the answer is in.
    expect(screen.getByText('Ready')).toBeTruthy();
    expect(screen.getAllByText(/Make the first call/).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Open Ada Lovelace' })).toBeTruthy();
  });

  it('Shift+Enter is a new line, not a send', () => {
    open({ thinkingMs: 0 });
    fireEvent.change(box(), { target: { value: 'who went quiet?' } });
    fireEvent.keyDown(box(), { key: 'Enter', shiftKey: true });
    expect(screen.queryByRole('list', { name: 'Conversation with the agent' })).toBeNull();
    expect((box() as HTMLTextAreaElement).value).toBe('who went quiet?');
  });

  it('starts with questions that fit the page, and one press asks them', () => {
    open({ thinkingMs: 0 });
    const starters = screen.getByRole('group', { name: 'Questions to start with' });
    expect(starters.textContent).toContain('What should I do next?');
    expect(starters.textContent).toContain('Pipeline value');
    fireEvent.click(screen.getByRole('button', { name: 'Pipeline value' }));
    expect(screen.getAllByText(/Calculated from loaded open deals/).length).toBeGreaterThan(0);
  });

  it('on a lead page the starters are about that lead', () => {
    open({ thinkingMs: 0, world: world({ page: { mode: 'record', href: '/leads/a1' } }) });
    fireEvent.click(screen.getByRole('button', { name: 'Brief this' }));
    expect(screen.getAllByText(/Briefing for Ada Lovelace/).length).toBeGreaterThan(0);
  });

  it('after an answer about a record, the follow-ups name it', () => {
    open({ thinkingMs: 0 });
    fireEvent.change(box(), { target: { value: 'brief Ada Lovelace' } });
    fireEvent.submit(box().closest('form') as HTMLFormElement);
    const next = screen.getByRole('group', { name: 'Questions to ask next' });
    expect(next.textContent).toContain('Draft a message to Ada Lovelace');
    fireEvent.click(screen.getByRole('button', { name: 'Draft a message to Ada Lovelace' }));
    expect(screen.getByRole('button', { name: 'Draft message for Ada Lovelace' })).toBeTruthy();
  });

  it('a new chat clears the conversation and what it was about', () => {
    open({ thinkingMs: 0 });
    fireEvent.change(box(), { target: { value: 'brief Ada Lovelace' } });
    fireEvent.submit(box().closest('form') as HTMLFormElement);
    fireEvent.click(screen.getByRole('button', { name: 'New chat' }));
    expect(screen.queryByText('brief Ada Lovelace')).toBeNull();
    expect(screen.getByRole('group', { name: 'Questions to start with' })).toBeTruthy();
    expect(localStorage.getItem('cadence.agent.turns.user-1:org-a')).toBe('[]');
  });

  it('will not send an empty question', () => {
    open({ thinkingMs: 0 });
    expect(screen.getByRole('button', { name: 'Send' })).toHaveProperty('disabled', true);
    fireEvent.keyDown(box(), { key: 'Enter' });
    expect(screen.queryByRole('list', { name: 'Conversation with the agent' })).toBeNull();
  });
});

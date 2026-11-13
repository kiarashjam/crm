// What the panel calls itself: "AI" only for a chat a language model answers. The next steps
// come from rules and never say AI; a workspace with only the club gets the chat when a model
// can answer it, and nothing otherwise.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

vi.mock('@/app/api', () => ({
  getLeads: vi.fn(), getDeals: vi.fn(), getTasks: vi.fn(), getContacts: vi.fn(), getCompanies: vi.fn(),
}));
vi.mock('@/app/api/sequences', () => ({ getEnrollments: vi.fn(), getSequences: vi.fn() }));
vi.mock('@/app/api/automations', () => ({ getAutomations: vi.fn() }));
vi.mock('@/app/api/settings', () => ({
  getUserSettings: vi.fn(async () => ({})),
  saveUserSettings: vi.fn(async (value: unknown) => value),
}));
vi.mock('@/app/api/workQueue', () => ({ getWorkQueue: vi.fn() }));
vi.mock('@/app/api/assistant', () => ({ getAssistantSettings: vi.fn(), getMorningBrief: vi.fn(), askAssistant: vi.fn() }));

const workspace = vi.hoisted(() => ({ salesEnabled: true, clubEnabled: true }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrgId: 'org-1', isReadOnly: false, currentOrg: { id: 'org-1', ...workspace } }),
}));

import { getCompanies, getContacts, getDeals, getLeads, getTasks } from '@/app/api';
import { getAssistantSettings, type AssistantSettings } from '@/app/api/assistant';
import { getAutomations } from '@/app/api/automations';
import { getEnrollments, getSequences } from '@/app/api/sequences';
import { getWorkQueue } from '@/app/api/workQueue';
import { NextActions } from './NextActions';
import { withQueries } from '@/test/queryWrapper';

const lost = (id: string, name: string) => ({
  id, name, email: `${id}@example.com`, status: 'New',
  pipelineState: JSON.stringify({ outreachStatus: 'contacted', contactOutcome: 'not_interested' }),
});

const settings = (configured: boolean): AssistantSettings => ({
  configured, provider: 'OpenAI', model: configured ? 'gpt-4o-mini' : '', monthlyLimit: 300, customLimit: null, defaultLimit: 300,
  maxLimit: 3000, morningBriefs: true, version: 'v1',
  usage: { used: 0, limit: 300, month: '2026-11-01', resetsOn: '2026-12-01', refused: 0, inputTokens: 0, outputTokens: 0, left: 300 },
});

function at(path: string) {
  return render(withQueries(
    <MemoryRouter initialEntries={[path]}>
      <NextActions />
    </MemoryRouter>,
  ));
}

beforeEach(() => {
  vi.clearAllMocks();
  localStorage.clear();
  workspace.salesEnabled = true;
  workspace.clubEnabled = true;
  vi.mocked(getLeads as () => Promise<unknown>).mockResolvedValue([lost('l1', 'Ada')]);
  for (const fn of [getDeals, getTasks, getContacts, getCompanies, getAutomations, getEnrollments, getSequences]) {
    vi.mocked(fn as () => Promise<unknown>).mockResolvedValue([]);
  }
  vi.mocked(getWorkQueue).mockResolvedValue({ scope: 'mine', tasks: [], leads: [lost('l1', 'Ada')] as never, deals: [], truncated: false });
  vi.mocked(getAssistantSettings).mockReset();
});

describe('without a language model', () => {
  it('the next steps and the chat make no claim to be AI', async () => {
    vi.mocked(getAssistantSettings).mockResolvedValue(settings(false));
    at('/dashboard');

    expect(await screen.findByRole('heading', { name: 'Do this next' })).toBeInTheDocument();
    await waitFor(() => expect(getAssistantSettings).toHaveBeenCalled());
    expect(screen.queryByText('AI')).toBeNull();
    expect(screen.queryByText('Ask AI')).toBeNull();
    expect(screen.getByRole('button', { name: 'Ask Cadence' })).toBeInTheDocument();
  });
});

describe('with a language model set up', () => {
  it('the chat is called AI, and the next steps still are not', async () => {
    vi.mocked(getAssistantSettings).mockResolvedValue(settings(true));
    const user = userEvent.setup();
    at('/dashboard');

    const ask = await screen.findByRole('button', { name: 'Ask Cadence AI' });
    expect(screen.getByRole('heading', { name: 'Do this next' }).parentElement?.textContent).not.toContain('AI');
    await user.click(ask);
    expect(await screen.findByRole('heading', { name: /Ask Cadence AI/ })).toBeInTheDocument();
  });

  it('a workspace with only the club gets the chat, though it has no sales queue', async () => {
    workspace.salesEnabled = false;
    vi.mocked(getAssistantSettings).mockResolvedValue(settings(true));
    at('/hospitality');

    expect(await screen.findByRole('button', { name: /Ask Cadence AI/ })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Do this next' })).toBeNull();
    expect(getLeads).not.toHaveBeenCalled();
  });
});

describe('a workspace with only the club and no model', () => {
  it('shows nothing: the built-in rules know only the sales records', async () => {
    workspace.salesEnabled = false;
    vi.mocked(getAssistantSettings).mockResolvedValue(settings(false));
    const { container } = at('/hospitality');

    await waitFor(() => expect(getAssistantSettings).toHaveBeenCalled());
    expect(container.textContent).toBe('');
  });
});

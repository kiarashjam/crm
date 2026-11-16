// Settings › Assistant: what the server says about the model and the month, and the limit and
// briefs switch, changed only by someone who manages settings, with the version they read.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { withQueries } from '@/test/queryWrapper';
import type { AssistantSettings } from '@/app/api/assistant';

vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
const org = vi.hoisted(() => ({ permissions: ['settings.manage', 'records.edit'] as string[] }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrgId: 'org-1', isReadOnly: false, currentOrg: { id: 'org-1', name: 'Pavillon', role: 2, permissions: org.permissions } }),
}));
const api = vi.hoisted(() => ({ getAssistantSettings: vi.fn(), saveAssistantSettings: vi.fn() }));
vi.mock('@/app/api/assistant', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/assistant')>()),
  getAssistantSettings: (...a: unknown[]) => api.getAssistantSettings(...a),
  saveAssistantSettings: (...a: unknown[]) => api.saveAssistantSettings(...a),
}));

const { AssistantSection } = await import('./AssistantSection');

const settings = (over: Partial<AssistantSettings> = {}): AssistantSettings => ({
  configured: true, provider: 'OpenAI', model: 'gpt-4o-mini', monthlyLimit: 300, customLimit: null, defaultLimit: 300, maxLimit: 3000,
  morningBriefs: true, version: 'v1',
  usage: { used: 120, limit: 300, month: '2026-11-01', resetsOn: '2026-12-01', refused: 2, inputTokens: 5000, outputTokens: 900, left: 180 },
  ...over,
});

beforeEach(() => {
  api.getAssistantSettings.mockReset();
  api.saveAssistantSettings.mockReset();
  org.permissions = ['settings.manage', 'records.edit'];
});

describe('Settings › Assistant', () => {
  it("shows the model, the month's calls and the limit that applies", async () => {
    api.getAssistantSettings.mockResolvedValue(settings());
    render(withQueries(<AssistantSection />));

    expect(await screen.findByText(/OpenAI \(gpt-4o-mini\) answers the chat/)).toBeInTheDocument();
    expect(screen.getByText(/120 of 300 calls to the language model used/)).toBeInTheDocument();
    expect(screen.getByText(/2 refused because the month's calls were used up/)).toBeInTheDocument();
    expect(screen.getByText(/uses the server's default: 300 calls a month/)).toBeInTheDocument();
  });

  it('names the day the count starts again as the server gives it, wherever the browser is', async () => {
    const zone = process.env.TZ;
    process.env.TZ = 'America/New_York';
    try {
      api.getAssistantSettings.mockResolvedValue(settings());
      render(withQueries(<AssistantSection />));
      const line = (await screen.findByText(/the count starts again on/)).textContent ?? '';
      expect(line).toMatch(/Dec/i);
      expect(line).not.toMatch(/Nov/i);
    } finally {
      process.env.TZ = zone;
    }
  });

  it('says plainly when no model is set up', async () => {
    api.getAssistantSettings.mockResolvedValue(settings({ configured: false, model: '' }));
    render(withQueries(<AssistantSection />));

    expect(await screen.findByText(/Not set up\. The chat answers from Cadence's built-in rules/)).toBeInTheDocument();
  });

  it('a manager saves a new limit with the version it was read at', async () => {
    api.getAssistantSettings.mockResolvedValue(settings());
    api.saveAssistantSettings.mockResolvedValue(settings({ monthlyLimit: 500, customLimit: 500, version: 'v2' }));
    const user = userEvent.setup();
    render(withQueries(<AssistantSection />));

    const box = await screen.findByLabelText('Calls a month');
    await user.clear(box);
    await user.type(box, '500');
    await user.click(screen.getByRole('button', { name: 'Save limit' }));

    await waitFor(() => expect(api.saveAssistantSettings).toHaveBeenCalledWith({ monthlyLimit: 500, version: 'v1' }));
    expect(await screen.findByText(/This workspace's own limit: 500 calls a month/)).toBeInTheDocument();
  });

  it('refuses a limit above what the server allows before sending it', async () => {
    api.getAssistantSettings.mockResolvedValue(settings());
    const user = userEvent.setup();
    render(withQueries(<AssistantSection />));

    const box = await screen.findByLabelText('Calls a month');
    await user.clear(box);
    await user.type(box, '5000');
    expect(screen.getByRole('button', { name: 'Save limit' })).toBeDisabled();
    expect(screen.getByText(/A whole number from 0 to 3000/)).toBeInTheDocument();
  });

  it('someone who does not manage settings reads them but cannot change them', async () => {
    org.permissions = ['records.edit'];
    api.getAssistantSettings.mockResolvedValue(settings());
    render(withQueries(<AssistantSection />));

    expect(await screen.findByText(/Only someone who manages the workspace's settings can change it/)).toBeInTheDocument();
    expect(screen.queryByLabelText('Calls a month')).toBeNull();
    expect(screen.getByRole('switch', { name: 'Morning briefs for this workspace' })).toBeDisabled();
  });

  it('the morning briefs switch is saved for the workspace', async () => {
    api.getAssistantSettings.mockResolvedValue(settings());
    api.saveAssistantSettings.mockResolvedValue(settings({ morningBriefs: false, version: 'v2' }));
    const user = userEvent.setup();
    render(withQueries(<AssistantSection />));

    await user.click(await screen.findByRole('switch', { name: 'Morning briefs for this workspace' }));
    await waitFor(() => expect(api.saveAssistantSettings).toHaveBeenCalledWith({ morningBriefs: false, version: 'v1' }));
  });
});

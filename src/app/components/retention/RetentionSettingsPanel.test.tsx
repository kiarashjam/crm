// The club's retention and referral rules: saved against the version read, a save from an older
// copy said so with a way to load the latest, scoring run by hand, and read-only for whoever may
// not change settings.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION, type Permission } from '@/app/api/organizations';
import {
  DEFAULT_RETENTION_SETTINGS, type RetentionSettings, type RetentionSettingsChange, type ScoringRun,
} from '@/app/api/retention';
import { withQueries } from '@/test/queryWrapper';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));
const org = vi.hoisted(() => ({ current: null as null | Record<string, unknown> }));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => org.current, useOrg: () => org.current }));
const as = (permissions: Permission[]) => {
  org.current = {
    isReadOnly: false, currentOrgId: 'org-1', organizations: [], hasFetched: true,
    currentOrg: { id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u-owner', isOwner: false, role: 1, permissions, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' },
  };
};
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));

const api = vi.hoisted(() => ({
  get: vi.fn<() => Promise<RetentionSettings>>(),
  save: vi.fn<(change: RetentionSettingsChange) => Promise<RetentionSettings>>(),
  runs: vi.fn<() => Promise<ScoringRun[]>>(),
  run: vi.fn<() => Promise<ScoringRun>>(),
}));
vi.mock('@/app/api/retention', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/retention')>()),
  getRetentionSettings: () => api.get(),
  saveRetentionSettings: (change: RetentionSettingsChange) => api.save(change),
  getScoringRuns: () => api.runs(),
  runScoringNow: () => api.run(),
}));

const { RetentionSettingsPanel } = await import('./RetentionSettingsPanel');

const SAVED: RetentionSettings = { ...DEFAULT_RETENTION_SETTINGS, version: 'v-1', updatedAtUtc: '2026-11-12T10:00:00Z', updatedByName: 'Olivia' };

beforeEach(() => {
  Object.values(api).forEach((fn) => fn.mockReset());
  toast.success.mockReset();
  toast.error.mockReset();
  api.runs.mockResolvedValue([]);
  as([PERMISSION.ManageSettings]);
});

describe("the club's retention and referral rules", () => {
  it('are saved against the version read, with what was changed', async () => {
    api.get.mockResolvedValue(SAVED);
    api.save.mockImplementation(async (change) => ({ ...SAVED, ...change, version: 'v-2' } as RetentionSettings));
    const user = userEvent.setup();
    render(withQueries(<RetentionSettingsPanel />));

    const visits = await screen.findByLabelText('Visits');
    await user.clear(visits);
    await user.type(visits, '40');
    await user.click(screen.getByRole('switch', { name: /A task for the member/ }));
    await user.click(screen.getByRole('button', { name: /Save rules/ }));

    await waitFor(() => expect(api.save).toHaveBeenCalledTimes(1));
    expect(api.save.mock.calls[0]![0]).toMatchObject({ visitWeight: 40, saveTask: true, version: 'v-1' });
    expect(toast.success).toHaveBeenCalledWith('Retention and referral rules saved.');
  });

  it('say so when someone saved them since, and offer the latest', async () => {
    api.get.mockResolvedValue(SAVED);
    api.save.mockRejectedValue(new ApiError('Someone else changed the rules.', 409, '/api/retention/settings', true, 'RetentionSettings.Changed.Conflict'));
    const user = userEvent.setup();
    render(withQueries(<RetentionSettingsPanel />));

    await user.click(await screen.findByRole('button', { name: /Save rules/ }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('Someone else changed the rules.',
      expect.objectContaining({ action: expect.objectContaining({ label: 'Reload' }) })));
  });

  it('score everyone by hand, and say what it did', async () => {
    api.get.mockResolvedValue(SAVED);
    api.run.mockResolvedValue({
      id: 'r1', trigger: 'manual', status: 'succeeded', startedAtUtc: '2026-11-13T09:00:00Z', membersScored: 42, becameAtRisk: 3,
      noLongerAtRisk: 1, tasksCreated: 3, emailsQueued: 0, renewalsTracked: 5,
    });
    const user = userEvent.setup();
    render(withQueries(<RetentionSettingsPanel />));

    await user.click(await screen.findByRole('button', { name: /Score everyone now/ }));

    await waitFor(() => expect(toast.success).toHaveBeenCalledWith('42 members scored: 3 newly at risk, 1 no longer.'));
  });

  it('are read-only for whoever may not change settings', async () => {
    as([PERMISSION.ClubMembers]);
    api.get.mockResolvedValue(SAVED);

    render(withQueries(<RetentionSettingsPanel />));

    expect(await screen.findByLabelText('Visits')).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Save rules/ })).not.toBeInTheDocument();
  });
});

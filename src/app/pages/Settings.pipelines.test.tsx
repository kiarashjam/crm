// Settings > Pipelines & Stages: a manager sets a stage's default probability and
// whether it closes deals as won or lost, a pipeline's own stale days, and the
// workspace's. Stages could only be renamed: every stage was created open, with no way
// to make one the won stage, and there was nowhere to set a probability.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';

const api = vi.hoisted(() => ({
  getPipelines: vi.fn(), getDealStagesByPipeline: vi.fn(), updateDealStage: vi.fn(), updatePipeline: vi.fn(),
}));
const orgs = vi.hoisted(() => ({ updateOrganizationSettings: vi.fn(), refreshOrgs: vi.fn() }));

vi.mock('@/app/api', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api')>()),
  ...api,
  getUserSettings: vi.fn(async () => ({
    brandName: 'Club', brandTone: 'professional', theme: 'light', dataDensity: 'comfortable',
    sidebarCollapsed: false, showWelcomeBanner: true, timezone: 'UTC', language: 'en',
    emailNotificationsEnabled: true, emailOnNewLead: true, emailOnDealUpdate: true, emailOnTaskDue: true,
    emailOnTeamMention: true, inAppNotificationsEnabled: true, inAppSoundEnabled: true,
    browserNotificationsEnabled: false, defaultFollowUpDays: 3, defaultCurrency: 'CHF',
    showActivityStatus: true, allowAnalytics: true,
  })),
  getTimezones: vi.fn(async () => []),
  getCurrencies: vi.fn(async () => []),
  getLanguages: vi.fn(async () => []),
  twoFactorStatus: vi.fn(async () => ({ enabled: false })),
}));
vi.mock('@/app/api/apiClient', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/api/organizations', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/organizations')>()),
  updateOrganizationSettings: orgs.updateOrganizationSettings,
}));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, staleDealDays: 30 }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('./McpSetup', () => ({ ClaudeSection: () => null }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ organizations: [org], currentOrg: org, setCurrentOrg: vi.fn(), refreshOrgs: orgs.refreshOrgs }),
  useOrgOptional: () => ({ organizations: [org], currentOrg: org, refreshOrgs: orgs.refreshOrgs }),
}));
vi.mock('@/app/contexts/ThemeContext', () => ({
  useTheme: () => ({ updateAppearance: vi.fn() }),
  useThemeOptional: () => null,
}));

const Settings = (await import('./Settings')).default;

const stages = [
  { id: 'st-1', pipelineId: 'p-1', name: 'Proposal', displayOrder: 1, isWon: false, isLost: false, probability: 30 },
  { id: 'st-2', pipelineId: 'p-1', name: 'Signed', displayOrder: 2, isWon: false, isLost: false },
];

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(orgs)]) fn.mockReset();
  api.getPipelines.mockResolvedValue([{ id: 'p-1', organizationId: 'org-1', name: 'Memberships', displayOrder: 0, dealStages: stages, staleDealDays: 60 }]);
  api.getDealStagesByPipeline.mockResolvedValue(stages);
  api.updateDealStage.mockImplementation(async (id: string, params: object) => ({ ...stages.find((s) => s.id === id), ...params }));
  api.updatePipeline.mockImplementation(async (id: string) => ({ id, organizationId: 'org-1', name: 'Memberships', displayOrder: 0 }));
  orgs.updateOrganizationSettings.mockResolvedValue({ ...org, staleDealDays: 14 });
});

const openPipelines = async () => {
  render(<MemoryRouter initialEntries={['/settings?tab=pipelines']}><Settings /></MemoryRouter>);
  fireEvent.click(await screen.findByRole('button', { name: /^Memberships/ }));
  await screen.findByText('30%');
};

describe('pipeline settings', () => {
  it("sets a stage's probability and makes it the won stage", async () => {
    await openPipelines();

    fireEvent.click(screen.getByRole('button', { name: 'Edit Signed' }));
    fireEvent.change(screen.getByLabelText('Probability for Signed'), { target: { value: '100' } });
    fireEvent.change(screen.getByLabelText('Outcome for Signed'), { target: { value: 'won' } });
    // The deals already in it follow, which the manager is told before saving.
    expect(screen.getByText('The open deals in this stage are marked won.')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Save stage Signed' }));

    await waitFor(() => expect(api.updateDealStage).toHaveBeenCalledWith('st-2',
      { name: 'Signed', probability: 100, isWon: true, isLost: false }));
  });

  it("takes a stage's probability away when it is left blank", async () => {
    await openPipelines();

    fireEvent.click(screen.getByRole('button', { name: 'Edit Proposal' }));
    expect(screen.getByLabelText('Probability for Proposal')).toHaveValue(30);
    expect(screen.queryByText(/deals in this stage are/)).toBeNull();
    fireEvent.change(screen.getByLabelText('Probability for Proposal'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save stage Proposal' }));

    await waitFor(() => expect(api.updateDealStage).toHaveBeenCalledWith('st-1',
      { name: 'Proposal', clearProbability: true, isWon: false, isLost: false }));
  });

  it("sets a pipeline's own stale days, or lets it follow the workspace's again", async () => {
    await openPipelines();
    expect(screen.getByText(/stale after 60 days/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Edit pipeline Memberships' }));
    fireEvent.change(screen.getByLabelText('Stale days for Memberships'), { target: { value: '45' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save pipeline Memberships' }));
    await waitFor(() => expect(api.updatePipeline).toHaveBeenCalledWith('p-1', { name: 'Memberships', staleDealDays: 45 }));

    fireEvent.click(screen.getByRole('button', { name: 'Edit pipeline Memberships' }));
    fireEvent.change(screen.getByLabelText('Stale days for Memberships'), { target: { value: '' } });
    fireEvent.click(screen.getByRole('button', { name: 'Save pipeline Memberships' }));
    await waitFor(() => expect(api.updatePipeline).toHaveBeenLastCalledWith('p-1', { name: 'Memberships', followWorkspaceStaleDays: true }));
  });

  it("saves the workspace's stale days", async () => {
    await openPipelines();

    fireEvent.change(screen.getByLabelText(/Flag an open deal as stale after/), { target: { value: '14' } });
    fireEvent.click(screen.getByRole('button', { name: "Save the workspace's stale days" }));

    await waitFor(() => expect(orgs.updateOrganizationSettings).toHaveBeenCalledWith('org-1', { staleDealDays: 14 }));
    expect(orgs.refreshOrgs).toHaveBeenCalled();
  });
});

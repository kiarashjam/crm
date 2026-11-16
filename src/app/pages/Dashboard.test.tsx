// The dashboard when a figure cannot be read: that panel says so and offers to try
// again, and the others show what they read.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { testQueryClient, withQueries } from '@/test/queryWrapper';

const api = {
  getDashboardStats: vi.fn(), getPipelineValueByStage: vi.fn(), getPipelineValueByAssignee: vi.fn(),
  getActivitiesPaged: vi.fn(), getCopyHistoryStats: vi.fn(), getTemplates: vi.fn(),
};
vi.mock('@/app/api', async () => {
  const actual = await vi.importActual<typeof import('@/app/api')>('@/app/api');
  return { ...actual, ...api };
});
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
// Figures count up to their value; with reduced motion each is shown at once.
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const Dashboard = (await import('./Dashboard')).default;

const STATS = {
  activeLeadsCount: 42, activeDealsCount: 7, pipelineValue: 125000, pipelineCurrency: 'CHF',
  pipelineByCurrency: [{ currency: 'CHF', value: 125000 }], unreadableValueCount: 0, dealsWonCount: 3, dealsLostCount: 1,
};

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  api.getDashboardStats.mockResolvedValue(STATS);
  api.getPipelineValueByStage.mockResolvedValue([]);
  api.getPipelineValueByAssignee.mockResolvedValue([{ assigneeUserId: 'u1', assigneeName: 'Rep One', currency: 'CHF', dealCount: 2, value: 5000 }]);
  api.getActivitiesPaged.mockResolvedValue({ items: [], totalCount: 0, page: 1, pageSize: 5, totalPages: 0, hasNextPage: false, hasPreviousPage: false });
  api.getCopyHistoryStats.mockResolvedValue({ sentThisWeek: 0, totalSent: 0 });
  api.getTemplates.mockResolvedValue([]);
});

const show = () => render(withQueries(<MemoryRouter><Dashboard /></MemoryRouter>));

describe('the dashboard', () => {
  it('says the headline figures could not be loaded, and reads them again on request', async () => {
    api.getDashboardStats.mockRejectedValueOnce(new Error('Server error'));
    show();

    const note = (await screen.findByText('These figures could not be loaded.')).closest('div')!;
    // The team figures beside it read fine and are shown.
    expect(await screen.findByText('Rep One')).toBeInTheDocument();

    await userEvent.click(within(note).getByRole('button', { name: 'Try again' }));

    await waitFor(() => expect(screen.queryByText('These figures could not be loaded.')).toBeNull());
    expect(api.getDashboardStats).toHaveBeenCalledTimes(2);
  });

  it('offers a retry where the team figures failed', async () => {
    api.getPipelineValueByAssignee.mockRejectedValueOnce(new Error('Server error'));
    show();

    const note = (await screen.findByText('Could not load team figures.')).closest('div')!;
    await userEvent.click(within(note).getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Rep One')).toBeInTheDocument();
  });
});

describe('the dashboard when a refresh fails', () => {
  it('keeps the figures it read and says, on each panel, that they are the last copy', async () => {
    const client = testQueryClient();
    render(withQueries(<MemoryRouter><Dashboard /></MemoryRouter>, client));
    expect(await screen.findByText('Rep One')).toBeInTheDocument();
    await screen.findByText('No open deals yet. Stages appear here once a deal has a value.');

    // The next read of every figure fails, as when the server goes away in the background.
    api.getDashboardStats.mockRejectedValue(new Error('Server error'));
    api.getPipelineValueByStage.mockRejectedValue(new Error('Server error'));
    api.getPipelineValueByAssignee.mockRejectedValue(new Error('Server error'));
    api.getActivitiesPaged.mockRejectedValue(new Error('Server error'));
    api.getCopyHistoryStats.mockRejectedValue(new Error('Server error'));
    await act(() => client.refetchQueries());

    expect(await screen.findByText(/These figures could not be refreshed, so this is the last copy read/)).toBeInTheDocument();
    expect(screen.getByText(/The pipeline breakdown could not be refreshed/)).toBeInTheDocument();
    expect(screen.getByText(/The team's figures could not be refreshed/)).toBeInTheDocument();
    expect(screen.getByText(/Recent activity could not be refreshed/)).toBeInTheDocument();
    expect(screen.getByText('These could not be refreshed, so they are the last copy read.')).toBeInTheDocument();
    // What was read is still there, rather than replaced by "could not be loaded".
    expect(screen.getByText('Rep One')).toBeInTheDocument();
    expect(screen.queryByText('Could not load team figures.')).toBeNull();
  });

  it('shows writer figures it could not read as dashes, not zeros', async () => {
    api.getCopyHistoryStats.mockRejectedValue(new Error('Server error'));
    show();

    expect(await screen.findByText('These could not be read: the dashes are missing figures, not zeros.')).toBeInTheDocument();
  });
});

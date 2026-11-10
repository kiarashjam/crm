// The activity timeline when the server does not answer: it says so and offers to try
// again, instead of the empty "Start your activity timeline" a new workspace sees.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Activity } from '@/app/api/types';
import { announceDataChanged } from '@/app/api/dataChanged';
import { withQueries } from '@/test/queryWrapper';

const api = {
  getActivitiesPaged: vi.fn(), getActivitiesByContact: vi.fn(), getActivitiesByDeal: vi.fn(), getActivitiesByLead: vi.fn(),
  getContacts: vi.fn(), getDeals: vi.fn(), getLeads: vi.fn(),
};
vi.mock('@/app/api', async () => {
  const actual = await vi.importActual<typeof import('@/app/api')>('@/app/api');
  return { ...actual, ...api };
});
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));

const Activities = (await import('./Activities')).default;

const CALL: Activity = { id: 'a-1', type: 'call', subject: 'Called the Müllers about the gala', createdAt: '2026-11-09T09:00:00Z' };
const page = (items: Activity[]) => ({ items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, hasNextPage: false, hasPreviousPage: false });

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  api.getActivitiesPaged.mockResolvedValue(page([CALL]));
  api.getContacts.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getLeads.mockResolvedValue([]);
});

const show = () => render(withQueries(<MemoryRouter><Activities /></MemoryRouter>));

describe('the activity timeline', () => {
  it('says it could not be loaded, instead of looking empty, and loads on retry', async () => {
    api.getActivitiesPaged.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    show();

    expect(await screen.findByText('Activities could not be loaded.')).toBeInTheDocument();
    expect(screen.queryByText('Start Your Activity Timeline')).toBeNull();

    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));

    expect(await screen.findByText('Called the Müllers about the gala')).toBeInTheDocument();
  });

  it('says when a refresh failed, so the copy on screen is not taken for the latest', async () => {
    // A save elsewhere asks for the timeline again; if that read fails, the earlier copy
    // stays, and the page says it is the last one read, as the other lists do.
    show();
    await screen.findByText('Called the Müllers about the gala');
    api.getActivitiesPaged.mockRejectedValue(new Error('The server took too long to answer.'));

    announceDataChanged({ resources: ['activities'] });

    expect(await screen.findByText(/Activities could not be refreshed, so this is the last copy read/, undefined, { timeout: 3000 })).toBeInTheDocument();
    expect(screen.getByText('Called the Müllers about the gala')).toBeInTheDocument();
    api.getActivitiesPaged.mockResolvedValue(page([CALL]));
    await userEvent.click(screen.getByRole('button', { name: 'Try again' }));
    await waitFor(() => expect(screen.queryByText(/could not be refreshed/)).toBeNull());
  });

  it('reads a page with a signal, so a page or filter left behind is cancelled', async () => {
    show();
    await screen.findByText('Called the Müllers about the gala');
    expect(api.getActivitiesPaged).toHaveBeenCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20 }),
      expect.objectContaining({ signal: expect.any(AbortSignal) }),
    );
  });

  it('still shows the timeline when the lists for the log dialog fail', async () => {
    api.getContacts.mockRejectedValue(new Error('offline'));
    api.getDeals.mockRejectedValue(new Error('offline'));
    show();
    expect(await screen.findByText('Called the Müllers about the gala')).toBeInTheDocument();
  });
});

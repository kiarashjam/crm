// The deal edit dialog sends the version of the deal it shows, takes an emptied field
// off, and says so when someone else saved the deal first. It sent a stale copy of every
// field, so a rep who left it open undid a colleague's stage move and new value when
// they saved a change to the description; and an emptied field was left out, so it could
// never be cleared.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { Deal, DealBoardQuery } from '@/app/api/types';
import { ApiError } from '@/app/api/apiClient';
import { demoBoard } from '@/app/api/dealBoardDemo';

const api = vi.hoisted(() => ({
  getDeals: vi.fn(), updateDeal: vi.fn(), createDeal: vi.fn(), deleteDeal: vi.fn(), getDealById: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getPipelines: vi.fn(), getOrgMembers: vi.fn(),
  getDealBoard: vi.fn(), getDealBoardColumn: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/activities', () => ({ getActivitiesByDeal: async () => [], createActivity: vi.fn() }));
vi.mock('@/app/api/tasks', () => ({ getTasks: async () => [], createTask: vi.fn() }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, DEAL_DUPLICATES: {} }));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org }),
  useOrgOptional: () => ({ currentOrg: org }),
}));

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Pipeline = (await import('./Pipeline')).default;

const gala: Deal = {
  id: 'deal-1', name: 'Autumn gala', value: '12000', amount: 12000, currency: 'CHF', stage: 'Proposal',
  companyId: 'co-1', companyName: 'Acme', description: 'Venue booked', probability: 60,
  expectedCloseDateUtc: '2026-12-01T00:00:00Z', version: 'v1',
};

const conflict = () => new ApiError(
  'Someone else changed this deal after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.',
  409, '/api/deals/deal-1', true, 'Deal.Changed.Conflict');

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.getContacts.mockResolvedValue([]);
  api.getCompanies.mockResolvedValue([{ id: 'co-1', name: 'Acme' }]);
  api.getPipelines.mockResolvedValue([]);
  api.getOrgMembers.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([gala]);
  api.getDealBoard.mockImplementation(async (query: DealBoardQuery) => demoBoard([gala], query, 'CHF'));
  api.getDealById.mockResolvedValue(gala);
  api.updateDeal.mockImplementation(async (_id: string, params: Partial<Deal>) => ({ ...gala, ...params, version: 'v-saved' }));
});

const openEdit = async () => {
  render(withQueries(<MemoryRouter><Pipeline /></MemoryRouter>));
  await screen.findAllByText('Autumn gala', undefined, { timeout: 5000 });
  fireEvent.click(screen.getByRole('button', { name: 'Edit deal' }));
  return screen.findByLabelText(/deal name/i);
};

const save = () => fireEvent.click(screen.getByRole('button', { name: /save changes/i }));

describe('the deal edit dialog', () => {
  it('sends the version it shows, and an emptied field as null so it is taken off', async () => {
    await openEdit();
    await waitFor(() => expect(api.getDealById).toHaveBeenCalledWith('deal-1'));

    fireEvent.change(screen.getByLabelText('Description'), { target: { value: '' } });
    fireEvent.change(screen.getByLabelText('Expected close date'), { target: { value: '' } });
    fireEvent.change(screen.getByLabelText(/win probability/i), { target: { value: '' } });
    save();

    await waitFor(() => expect(api.updateDeal).toHaveBeenCalledTimes(1));
    expect(api.updateDeal.mock.calls[0]).toEqual(['deal-1', expect.objectContaining({
      version: 'v1', description: null, expectedCloseDateUtc: null, probability: null, companyId: 'co-1', assigneeId: null,
    })]);
  });

  it('opens on the deal as it is now when the board holds an older copy', async () => {
    api.getDealById.mockResolvedValue({ ...gala, name: 'Autumn gala 2027', version: 'v2' });

    await openEdit();
    await screen.findByDisplayValue('Autumn gala 2027');
    save();

    await waitFor(() => expect(api.updateDeal).toHaveBeenCalledTimes(1));
    expect(api.updateDeal.mock.calls[0]?.[1]).toMatchObject({ name: 'Autumn gala 2027', version: 'v2' });
  });

  it('keeps what was typed when a newer copy arrives after typing began', async () => {
    let arrive: (deal: Deal) => void = () => {};
    api.getDealById.mockReturnValue(new Promise<Deal>((resolve) => { arrive = resolve; }));

    const name = await openEdit();
    fireEvent.change(name, { target: { value: 'Autumn gala, typed' } });
    await act(async () => { arrive({ ...gala, name: 'Autumn gala 2027', version: 'v2' }); });

    expect(screen.getByDisplayValue('Autumn gala, typed')).toBeInTheDocument();
    save();
    await waitFor(() => expect(api.updateDeal).toHaveBeenCalledTimes(1));
    // Still checked against the copy it was typed on, so a stale save is refused, not written.
    expect(api.updateDeal.mock.calls[0]?.[1]).toMatchObject({ name: 'Autumn gala, typed', version: 'v1' });
  });

  it('says someone else saved the deal first, and Reload brings the latest copy in', async () => {
    api.updateDeal.mockRejectedValueOnce(conflict());
    await openEdit();
    await waitFor(() => expect(api.getDealById).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText('Description'), { target: { value: 'Called the venue' } });
    save();

    await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(1));
    const [message, options] = toast.error.mock.calls[0] as [string, { action: { label: string; onClick: () => void } }];
    expect(message).toMatch(/someone else changed this deal/i);
    expect(options.action.label).toBe('Reload');
    expect(toast.success).not.toHaveBeenCalled();

    api.getDealById.mockResolvedValue({ ...gala, description: 'Deposit paid', probability: 80, version: 'v3' });
    await act(async () => { options.action.onClick(); });

    await screen.findByDisplayValue('Deposit paid');
    save();
    await waitFor(() => expect(api.updateDeal).toHaveBeenCalledTimes(2));
    expect(api.updateDeal.mock.calls[1]?.[1]).toMatchObject({ description: 'Deposit paid', probability: 80, version: 'v3' });
  });
});

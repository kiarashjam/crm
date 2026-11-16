// A saved view of the deals board: the board's own filters and order, and its tag and custom
// field conditions, kept under a name and opened again from the Views menu.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { Deal, DealBoard, DealBoardColumn } from '@/app/api/types';
import type { SavedView } from '@/app/api/savedViews';
import { withQueries } from '@/test/queryWrapper';

const api = vi.hoisted(() => ({
  getDeals: vi.fn(), updateDeal: vi.fn(), createDeal: vi.fn(), deleteDeal: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getPipelines: vi.fn(), getOrgMembers: vi.fn(),
  getDealBoard: vi.fn(), getDealBoardColumn: vi.fn(), getSavedViews: vi.fn(), createSavedView: vi.fn(),
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

const PIPELINE = '11111111-1111-1111-1111-111111111111';
const ENQUIRY = '22222222-2222-2222-2222-222222222222';
const SIGNED = '33333333-3333-3333-3333-333333333333';
const daysAgo = (n: number) => new Date(Date.now() - n * 24 * 60 * 60 * 1000).toISOString();

const card = (over: Partial<Deal>): Deal => ({
  id: over.name ?? 'd', name: 'Deal', value: '1000', amount: 1000, currency: 'CHF', stage: 'Enquiry',
  pipelineId: PIPELINE, dealStageId: ENQUIRY, ...over,
});

const column = (over: Partial<DealBoardColumn>): DealBoardColumn => ({
  key: ENQUIRY, dealStageId: ENQUIRY, name: 'Enquiry', displayOrder: 1, isWon: false, isLost: false,
  count: 0, totals: [], weighted: [], stale: 0, deals: [], hasMore: false, ...over,
});

const board = (): DealBoard => ({
  pipelineId: PIPELINE,
  pipelineName: 'Memberships',
  staleAfterDays: 30,
  count: 31,
  columns: [
    column({
      count: 30,
      probability: 25,
      totals: [{ currency: 'CHF', count: 30, value: 120000, unreadable: 0 }],
      weighted: [{ currency: 'CHF', value: 30000, covered: 30, missing: 0 }],
      stale: 4,
      hasMore: true,
      deals: [
        card({ name: 'Alpine Club', stageEnteredAtUtc: daysAgo(12), idleSinceUtc: daysAgo(40), isStale: true, staleAfterDays: 30 }),
        card({ name: 'Bernese Bank', stageEnteredAtUtc: daysAgo(0) }),
      ],
    }),
    column({
      key: SIGNED, dealStageId: SIGNED, name: 'Signed', displayOrder: 2, isWon: true, count: 1,
      totals: [{ currency: 'CHF', count: 1, value: 5000, unreadable: 0 }],
      deals: [card({ name: 'Old win', dealStageId: SIGNED, stage: 'Signed', amount: 5000, isWon: true })],
    }),
  ],
  summary: {
    total: 31, open: 30, won: 1, lost: 0,
    openTotals: [{ currency: 'CHF', count: 30, value: 120000, unreadable: 0 }],
    wonTotals: [{ currency: 'CHF', count: 1, value: 5000, unreadable: 0 }],
    weighted: [{ currency: 'CHF', value: 30000, covered: 30, missing: 0 }],
    winRate: 1, overdue: 0, closingThisWeek: 0, stale: 4,
  },
});

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  toast.success.mockReset();
  toast.error.mockReset();
  api.getContacts.mockResolvedValue([]);
  api.getCompanies.mockResolvedValue([]);
  api.getOrgMembers.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([{
    id: PIPELINE, organizationId: 'org-1', name: 'Memberships', displayOrder: 0,
    dealStages: [
      { id: ENQUIRY, pipelineId: PIPELINE, name: 'Enquiry', displayOrder: 1, isWon: false, isLost: false, probability: 25 },
      { id: SIGNED, pipelineId: PIPELINE, name: 'Signed', displayOrder: 2, isWon: true, isLost: false },
    ],
  }]);
  api.getDealBoard.mockImplementation(async () => board());
  api.getSavedViews.mockResolvedValue([]);
});

const show = async () => {
  render(withQueries(<MemoryRouter><Pipeline /></MemoryRouter>));
  await screen.findAllByText('Alpine Club', undefined, { timeout: 5000 });
};

const lastBoardQuery = () => api.getDealBoard.mock.calls[api.getDealBoard.mock.calls.length - 1]![0];

const STALE_VIPS: SavedView = {
  id: 'v1', list: 'deals', name: 'Stale VIP gold deals', shared: true, mine: true, isDefault: false,
  query: { search: 'Alpine', staleOnly: 'true', sort: 'value', tag: ['t-vip'], field: ['tier:is:Gold'] },
  columns: null, version: 'v1', updatedAtUtc: '2026-11-13T00:00:00Z',
};

describe('saved views of the deals board', () => {
  it('open with the board filters, order and tag and field conditions they keep', async () => {
    api.getSavedViews.mockResolvedValue([STALE_VIPS]);
    const user = userEvent.setup();
    await show();

    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /^Stale VIP gold deals/ }));

    const wanted = { search: 'Alpine', staleOnly: true, sort: 'value', tag: ['t-vip'], field: ['tier:is:Gold'] };
    await waitFor(() => expect(lastBoardQuery()).toMatchObject(wanted));
    // And it stays so once the typed search has settled.
    await new Promise((resolve) => setTimeout(resolve, 500));
    expect(lastBoardQuery()).toMatchObject(wanted);
  });

  it("save the board's filters and order with its tag and field conditions", async () => {
    api.createSavedView.mockImplementation(async (input: Record<string, unknown>) => ({ ...STALE_VIPS, ...input }));
    const user = userEvent.setup();
    render(withQueries(<MemoryRouter initialEntries={['/pipeline?tag=t-vip']}><Pipeline /></MemoryRouter>));
    await screen.findAllByText('Alpine Club', undefined, { timeout: 5000 });
    await user.click(screen.getByRole('button', { name: 'Stale' }));

    await user.click(screen.getByRole('button', { name: /^views$/i }));
    await user.click(await screen.findByRole('menuitem', { name: /save these filters/i }));
    await user.type(screen.getByLabelText('Name'), 'Stale VIPs');
    await user.click(screen.getByRole('button', { name: /save view/i }));

    await waitFor(() => expect(api.createSavedView).toHaveBeenCalledWith(expect.objectContaining({
      list: 'deals', name: 'Stale VIPs', query: { staleOnly: 'true', tag: ['t-vip'] },
    })));
  });
});

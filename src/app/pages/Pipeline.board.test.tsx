// The deals board reads its columns from the server: each stage's count and money are
// the server's, over every deal in it, while the cards arrive a page at a time. It used
// to download every deal and add them up here, so a column could only total the cards
// it had.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Deal, DealBoard, DealBoardColumn } from '@/app/api/types';
import { formatMoney } from '@/app/lib/money';

const api = vi.hoisted(() => ({
  getDeals: vi.fn(), updateDeal: vi.fn(), createDeal: vi.fn(), deleteDeal: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getPipelines: vi.fn(), getOrgMembers: vi.fn(),
  getDealBoard: vi.fn(), getDealBoardColumn: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/activities', () => ({ getActivitiesByDeal: async () => [], createActivity: vi.fn() }));
vi.mock('@/app/api/tasks', () => ({ getTasks: async () => [], createTask: vi.fn() }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
// The duplicate finder above the board: kept here is what it calls after a merge.
const merge = vi.hoisted(() => ({ onMerged: undefined as undefined | (() => void) }));
vi.mock('@/app/components/duplicates', () => ({
  DuplicateMerge: (props: { onMerged?: () => void }) => { merge.onMerged = props.onMerged; return null; },
  DEAL_DUPLICATES: {},
}));

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
const flat = (s: string) => s.replace(/\s+/g, ' ');
const chf = (n: number) => flat(formatMoney(n, 'CHF', { locale: 'de-CH', cents: 'never' }));
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
});

const show = async () => {
  render(<MemoryRouter><Pipeline /></MemoryRouter>);
  await screen.findAllByText('Alpine Club', undefined, { timeout: 5000 });
};

const enquiryColumn = () => screen.getByRole('heading', { name: 'Enquiry' }).closest('div.flex.flex-col') as HTMLElement;

describe('the board from the server', () => {
  it("asks for the pipeline on screen and shows each stage's own count and money, not the loaded cards'", async () => {
    await show();

    expect(api.getDealBoard).toHaveBeenCalledWith(expect.objectContaining({ pipelineId: PIPELINE }));
    const enquiry = enquiryColumn();
    expect(within(enquiry).getByText(/30 deals/)).toBeInTheDocument();
    expect(within(enquiry).getByText(chf(120000))).toBeInTheDocument();
    expect(within(enquiry).getByText(`Weighted ${chf(30000)}`)).toBeInTheDocument();
    expect(within(enquiry).getByText('4 stale')).toBeInTheDocument();
  });

  it('loads the next cards of a column when asked, with the same filters', async () => {
    api.getDealBoardColumn.mockResolvedValue({ deals: [card({ name: 'Club dinner' })], hasMore: false });
    await show();

    fireEvent.click(within(enquiryColumn()).getByRole('button', { name: 'Show more (28 more)' }));

    await screen.findByText('Club dinner');
    expect(api.getDealBoardColumn).toHaveBeenCalledWith(expect.objectContaining({ pipelineId: PIPELINE }), ENQUIRY, 2);
    expect(within(enquiryColumn()).queryByRole('button', { name: /Show more/ })).toBeNull();
  });

  it('says on a card how long it has been in its stage, and that it has gone stale', async () => {
    await show();

    expect(screen.getByText('In Enquiry for 12 days')).toBeInTheDocument();
    expect(screen.getByText('In Enquiry since today')).toBeInTheDocument();
    expect(screen.getByTitle('No activity for 40 days')).toBeInTheDocument();
  });

  it('sends the filters to the server, which narrows the columns', async () => {
    await show();

    fireEvent.click(screen.getByRole('button', { name: /My Deals/ }));
    await waitFor(() => expect(api.getDealBoard).toHaveBeenLastCalledWith(expect.objectContaining({ mine: true })));

    fireEvent.click(screen.getByRole('button', { name: /^Stale$/ }));
    await waitFor(() => expect(api.getDealBoard).toHaveBeenLastCalledWith(expect.objectContaining({ mine: true, staleOnly: true })));

    fireEvent.change(screen.getByLabelText('Search deals'), { target: { value: 'gala' } });
    await waitFor(() => expect(api.getDealBoard).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'gala' })));

    fireEvent.change(screen.getByLabelText('Maximum deal value'), { target: { value: '5000' } });
    await waitFor(() => expect(api.getDealBoard).toHaveBeenLastCalledWith(expect.objectContaining({ search: 'gala', maxAmount: 5000 })));
  });

  it("puts the weighted pipeline and the stale count in the board's figures", async () => {
    await show();

    expect(screen.getByText(new RegExp(`${chf(30000)} weighted`))).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Stale deals, 4/ })).toBeInTheDocument();
  });

  it('reads the board again after duplicates are merged, so the deal folded away leaves it', async () => {
    await show();
    const before = api.getDealBoard.mock.calls.length;

    act(() => merge.onMerged?.());

    await waitFor(() => expect(api.getDealBoard.mock.calls.length).toBeGreaterThan(before));
  });

  it('reads the board again after a deal is changed, so its stage figures stay true', async () => {
    api.updateDeal.mockResolvedValue(card({ name: 'Alpine Club 2027' }));
    await show();
    const before = api.getDealBoard.mock.calls.length;

    fireEvent.click(within(enquiryColumn()).getAllByRole('button', { name: 'Edit deal' })[0]!);
    fireEvent.change(await screen.findByLabelText(/deal name/i), { target: { value: 'Alpine Club 2027' } });
    fireEvent.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => expect(api.updateDeal).toHaveBeenCalled());
    await waitFor(() => expect(api.getDealBoard.mock.calls.length).toBeGreaterThan(before));
  });
});

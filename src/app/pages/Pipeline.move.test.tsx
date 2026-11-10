// A deal moved on the board, and a board that could not be read.
//
// The board is the server's, read through the shared cache. A move shows at once, and
// the cache reads the board again once the save is announced, for the server's counts
// and money; a refused move puts the board back. A board that could not be read says
// so, rather than showing "Build your sales pipeline" to a team with deals.
import { Children, isValidElement, type ReactNode } from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Deal, DealBoard, DealBoardColumn } from '@/app/api/types';
import { withQueries } from '@/test/queryWrapper';

const api = vi.hoisted(() => ({
  getDeals: vi.fn(), updateDeal: vi.fn(), createDeal: vi.fn(), deleteDeal: vi.fn(),
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

// The stage pickers as plain selects, which the test can drive.
const textOf = (node: ReactNode): string =>
  Children.toArray(node).map((child) => (typeof child === 'string' || typeof child === 'number'
    ? String(child)
    : isValidElement<{ children?: ReactNode }>(child) ? textOf(child.props.children) : '')).join('').trim();
vi.mock('@/app/components/ui/select', () => ({
  Select: ({ value, onValueChange, children }: { value?: string; onValueChange?: (v: string) => void; children?: ReactNode }) => (
    <select value={value} onChange={(e) => onValueChange?.(e.target.value)}>{children}</select>
  ),
  SelectTrigger: () => null,
  SelectValue: () => null,
  SelectContent: ({ children }: { children?: ReactNode }) => <>{children}</>,
  SelectItem: ({ value, children }: { value: string; children?: ReactNode }) => <option value={value}>{textOf(children)}</option>,
}));

const Pipeline = (await import('./Pipeline')).default;

const PIPELINE = '11111111-1111-1111-1111-111111111111';
const ENQUIRY = '22222222-2222-2222-2222-222222222222';
const SIGNED = '33333333-3333-3333-3333-333333333333';

const card = (over: Partial<Deal>): Deal => ({
  id: over.name ?? 'd', name: 'Deal', value: '1000', amount: 1000, currency: 'CHF', stage: 'Enquiry',
  pipelineId: PIPELINE, dealStageId: ENQUIRY, ...over,
});
const column = (over: Partial<DealBoardColumn>): DealBoardColumn => ({
  key: ENQUIRY, dealStageId: ENQUIRY, name: 'Enquiry', displayOrder: 1, isWon: false, isLost: false,
  count: 0, totals: [], weighted: [], stale: 0, deals: [], hasMore: false, ...over,
});
const board = (): DealBoard => ({
  pipelineId: PIPELINE, pipelineName: 'Memberships', staleAfterDays: 0, count: 2,
  columns: [
    column({ count: 1, deals: [card({ name: 'Alpine Club' })] }),
    column({ key: SIGNED, dealStageId: SIGNED, name: 'Signed', displayOrder: 2, isWon: true, count: 1, deals: [card({ name: 'Old win', dealStageId: SIGNED, stage: 'Signed', isWon: true })] }),
  ],
  summary: {
    total: 2, open: 1, won: 1, lost: 0, openTotals: [], wonTotals: [], weighted: [],
    winRate: 1, overdue: 0, closingThisWeek: 0, stale: 0,
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
      { id: ENQUIRY, pipelineId: PIPELINE, name: 'Enquiry', displayOrder: 1, isWon: false, isLost: false },
      { id: SIGNED, pipelineId: PIPELINE, name: 'Signed', displayOrder: 2, isWon: true, isLost: false },
    ],
  }]);
  api.getDealBoard.mockImplementation(async () => board());
});

const show = () => render(withQueries(<MemoryRouter><Pipeline /></MemoryRouter>));
const columnOf = (stage: string) => screen.getByRole('heading', { name: stage }).closest('div.flex.flex-col') as HTMLElement;
const stagePickerOf = (deal: string) => within(screen.getByTitle(deal).closest('.group') as HTMLElement).getByRole('combobox');

describe('moving a deal on the board', () => {
  it('shows the card in its new column at once, before the server answers', async () => {
    api.updateDeal.mockReturnValue(new Promise(() => {}));
    show();
    await screen.findByTitle('Alpine Club', undefined, { timeout: 5000 });

    fireEvent.change(stagePickerOf('Alpine Club'), { target: { value: SIGNED } });

    await waitFor(() => expect(within(columnOf('Signed')).getByTitle('Alpine Club')).toBeInTheDocument());
    // The old place lets the card go once its exit animation has played.
    await waitFor(() => expect(within(columnOf('Enquiry')).queryByTitle('Alpine Club')).toBeNull());
    expect(api.updateDeal).toHaveBeenCalledWith('Alpine Club', expect.objectContaining({ dealStageId: SIGNED, isWon: true }));
  });

  it('puts the card back when the server refuses the move', async () => {
    api.updateDeal.mockRejectedValue(new Error('You cannot change this deal.'));
    show();
    await screen.findByTitle('Alpine Club', undefined, { timeout: 5000 });

    fireEvent.change(stagePickerOf('Alpine Club'), { target: { value: SIGNED } });

    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(within(columnOf('Enquiry')).getByTitle('Alpine Club')).toBeInTheDocument();
    expect(within(columnOf('Signed')).queryByTitle('Alpine Club')).toBeNull();
  });
});

describe('a board that could not be read', () => {
  it('says so with a retry, instead of the empty first-day screen', async () => {
    api.getDealBoard.mockRejectedValueOnce(new Error('The server took too long to answer.'));
    show();

    expect(await screen.findByText('Your deals could not be loaded.', undefined, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByText('Build Your Sales Pipeline')).toBeNull();

    fireEvent.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByTitle('Alpine Club')).toBeInTheDocument();
  });
});

// A deal's page says how the deal is moving: how long it has been in its stage, whether
// it has gone stale, and the stages it has been in. It showed none of this; the stage
// history was written on the server and never read.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Deal, DealStageChange } from '@/app/api/types';

const api = vi.hoisted(() => ({ getDealById: vi.fn(), getDealStageHistory: vi.fn(), updateDeal: vi.fn(), deleteDeal: vi.fn() }));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/activities', () => ({ getActivitiesByDeal: async () => [], createActivity: vi.fn() }));
vi.mock('@/app/api/tasks', () => ({ getTasksByDeal: async () => [] }));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/DealLineItemsCard', () => ({ default: () => null }));
vi.mock('@/app/components/CustomFieldsCard', () => ({ default: () => null }));
vi.mock('@/app/components/flex/RecordTagsEditor', () => ({ default: () => null }));
vi.mock('@/app/components/AttachmentsCard', () => ({ default: () => null }));
// The deal's contract and the member it brought in have their own tests; here, that the page shows them for this deal.
vi.mock('./contracts/ContractPanel', () => ({
  ContractPanel: ({ dealId }: { dealId?: string }) => <p data-testid="contract-panel">{dealId}</p>,
}));
vi.mock('@/app/components/MembershipLinks', () => ({
  PersonMembershipCard: ({ kind, id }: { kind: string; id: string }) => <p data-testid="membership-card">{kind}:{id}</p>,
}));
const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org }),
  useOrgOptional: () => ({ currentOrg: org, isReadOnly: false }),
}));

const DealDetail = (await import('./DealDetail')).default;

const DAY = 24 * 60 * 60 * 1000;
const at = (daysAgo: number) => new Date(Date.now() - daysAgo * DAY).toISOString();

const deal: Deal = {
  id: 'd-1', name: 'Fondation Léman', value: '12000', amount: 12000, currency: 'CHF', stage: 'Tour booked',
  dealStageId: 's2', dealStageName: 'Tour booked', createdAtUtc: at(30),
  stageEnteredAtUtc: at(12), idleSinceUtc: at(40), isStale: true, staleAfterDays: 30,
};
const history: DealStageChange[] = [
  { id: 'c2', fromStageId: 's1', fromStageName: 'Enquiry', toStageId: 's2', toStageName: 'Tour booked', changedByUserId: 'u', changedByName: 'Anna Keller', changedAtUtc: at(12), isCreation: false },
  { id: 'c1', toStageId: 's1', toStageName: 'Enquiry', changedByUserId: 'u', changedByName: 'Anna Keller', changedAtUtc: at(30), leftAtUtc: at(12), isCreation: true },
];

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  api.getDealById.mockResolvedValue(deal);
  api.getDealStageHistory.mockResolvedValue(history);
});

const show = async () => {
  render(
    <MemoryRouter initialEntries={['/deals/d-1']}>
      <Routes><Route path="/deals/:id" element={<DealDetail />} /></Routes>
    </MemoryRouter>,
  );
  await screen.findByRole('heading', { name: 'Fondation Léman' });
};

describe('how a deal is moving, on its page', () => {
  it('shows how long the deal has been in its stage, and that it has gone stale', async () => {
    await show();

    expect(screen.getByText('Stale: no activity for 40 days')).toBeInTheDocument();
    expect(screen.getByText('In stage')).toBeInTheDocument();
    expect(screen.getByText('12 days')).toBeInTheDocument();
  });

  it('reads the stage history and lists the stages the deal has been in', async () => {
    await show();

    expect(api.getDealStageHistory).toHaveBeenCalledWith('d-1');
    expect(await screen.findByText('Created in Enquiry')).toBeInTheDocument();
    expect(screen.getByText(/18 days in Enquiry/)).toBeInTheDocument();
    expect(screen.getByText(/In Tour booked for 12 days/)).toBeInTheDocument();
  });

  it('says the time in stage is not recorded for a deal from before the history', async () => {
    api.getDealById.mockResolvedValue({ ...deal, stageEnteredAtUtc: undefined, isStale: false });
    api.getDealStageHistory.mockResolvedValue([]);
    await show();

    expect(screen.getByText(/not recorded/)).toBeInTheDocument();
    expect(screen.queryByText(/^Stale/)).toBeNull();
  });

  it('shows the deal’s contracts and the club membership it brought in', async () => {
    await show();

    expect(screen.getByTestId('contract-panel')).toHaveTextContent('d-1');
    expect(screen.getByTestId('membership-card')).toHaveTextContent('deal:d-1');
  });
});

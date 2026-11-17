// Converting a lead into a deal prices the deal in the workspace's currency, shown in
// the dialog, and a refusal says why. The dialog offered no currency at all and sent
// whatever the page assumed, and every refusal read "Something went wrong".
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { Lead } from '@/app/api/types';

const lead: Lead = { id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified' };

const api = vi.hoisted(() => ({
  getLeads: vi.fn(), getLeadsPaged: vi.fn(), getLeadStats: vi.fn(), getLeadById: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getDeals: vi.fn(), getPipelines: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLeadIds: vi.fn(), convertLead: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/organizations', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/organizations')>('@/app/api/organizations')),
  getOrgMembers: async () => [],
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/components/duplicates', () => ({ DuplicateMerge: () => null, LEAD_DUPLICATES: {} }));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org, currentOrgId: org.id, organizations: [org] }),
  useOrgOptional: () => ({ currentOrg: org }),
}));

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Leads = (await import('./Leads')).default;

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  org.currency = 'CHF';
  api.getLeads.mockResolvedValue([lead]);
  api.getLeadsPaged.mockResolvedValue({ items: [lead], totalCount: 1, page: 1, pageSize: 25 });
  api.getLeadStats.mockResolvedValue(null);
  api.getLeadById.mockResolvedValue(lead);
  api.getCompanies.mockResolvedValue([]);
  api.getContacts.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLeadIds.mockResolvedValue([]);
});

// The lead page hands over with ?convertLeadId=, which opens the dialog for that lead.
async function openConversion() {
  render(withQueries(
    <MemoryRouter initialEntries={['/leads?convertLeadId=l1']}>
      <Leads />
    </MemoryRouter>,
  ));
  fireEvent.click(await screen.findByRole('button', { name: /create new deal/i }, { timeout: 5000 }));
  fireEvent.change(await screen.findByLabelText('Deal value'), { target: { value: "2'400.50" } });
}

describe('converting a lead into a deal', () => {
  it("prices the deal in the workspace's currency, shown in the dialog, and sends the amount as a number", async () => {
    org.currency = 'EUR';
    api.convertLead.mockResolvedValue({ leadId: 'l1', dealId: 'd1' });
    await openConversion();

    expect(screen.getByRole('combobox', { name: 'Deal currency' })).toHaveTextContent('EUR');
    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createDeal: true, dealAmount: 2400.5, dealCurrency: 'EUR' });
    expect(api.convertLead.mock.calls[0]?.[1]).not.toHaveProperty('dealValue');
  });

  it("says why the server refused, not that something went wrong", async () => {
    api.convertLead.mockRejectedValue(new Error('A currency is a three-letter ISO code, such as CHF or EUR.'));
    await openConversion();

    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(toast.error).toHaveBeenCalledWith('A currency is a three-letter ISO code, such as CHF or EUR.'));
  });
});

// The pipeline and stage on screen are where the deal goes. They were shown as chosen but
// not sent, so the deal went on no pipeline at all; and picking another pipeline sent an
// empty stage, which the server refused along with the whole conversion.
describe('the pipeline and stage a converted deal goes into', () => {
  const stage = (id: string, pipelineId: string, name: string, displayOrder: number, extra: { isWon?: boolean; isLost?: boolean } = {}) =>
    ({ id, pipelineId, name, displayOrder, isWon: false, isLost: false, ...extra });
  const sales = { id: '11111111-1111-4111-8111-111111111111', name: 'Sales', displayOrder: 0, dealStages: [
    stage('11111111-0000-4000-8000-000000000002', '11111111-1111-4111-8111-111111111111', 'Proposal', 1),
    stage('11111111-0000-4000-8000-000000000001', '11111111-1111-4111-8111-111111111111', 'Qualification', 0),
  ] };
  const membership = { id: '22222222-2222-4222-8222-222222222222', name: 'Membership', displayOrder: 1, dealStages: [
    stage('22222222-0000-4000-8000-000000000009', '22222222-2222-4222-8222-222222222222', 'Declined', 0, { isLost: true }),
    stage('22222222-0000-4000-8000-000000000001', '22222222-2222-4222-8222-222222222222', 'Enquiry', 1),
  ] };

  beforeEach(() => {
    api.getPipelines.mockResolvedValue([sales, membership]);
    api.convertLead.mockResolvedValue({ leadId: 'l1', dealId: 'd1' });
  });

  it('sends the pipeline and stage the dialog shows, without being touched', async () => {
    await openConversion();
    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({
      createDeal: true, pipelineId: sales.id, dealStageId: '11111111-0000-4000-8000-000000000001', dealStage: 'Qualification',
    });
  });

  it("sends another pipeline's opening stage when it is picked, never an empty one", async () => {
    await openConversion();
    // As the stage list changes with the pipeline, the stage picker reports an empty value.
    const [pipelinePicker] = screen.getAllByRole('combobox').filter((c) => c.textContent?.includes('Sales'));
    fireEvent.click(pipelinePicker!);
    fireEvent.click(await screen.findByRole('option', { name: 'Membership' }));
    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    const sent = api.convertLead.mock.calls[0]?.[1];
    // The first stage that is neither won nor lost.
    expect(sent).toMatchObject({ pipelineId: membership.id, dealStageId: '22222222-0000-4000-8000-000000000001', dealStage: 'Enquiry' });
    // No id the server cannot read: an empty one made it refuse the whole conversion.
    const ids = Object.entries(sent as Record<string, unknown>).filter(([key]) => key.endsWith('Id'));
    expect(ids.filter(([, value]) => value === '')).toEqual([]);
  });

  it('leads to the new deal from the message that says it was made', async () => {
    await openConversion();
    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(toast.success).toHaveBeenCalled());
    expect(toast.success.mock.calls[0]?.[1]).toMatchObject({ action: { label: 'Open the deal' } });
  });
});

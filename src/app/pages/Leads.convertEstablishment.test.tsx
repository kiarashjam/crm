// Converting an establishment: it becomes its company (already made for it), its contact
// person a contact when they have a name and an email, and a deal. An establishment can
// convert into a deal alone; a person converts as before, always into a contact.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { Lead } from '@/app/api/types';

const trattoria: Lead = {
  id: 'l1', kind: 'establishment', name: 'Trattoria Bella', email: '', status: 'Qualified',
  establishmentType: 'restaurant', city: 'Lausanne', companyId: 'c1', companyName: 'Trattoria Bella',
  contactPersonName: 'Marco Rossi', contactPersonEmail: 'marco@trattoria.ch',
};
const current = vi.hoisted(() => ({ lead: null as Lead | null }));

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

const org = vi.hoisted(() => ({
  id: 'org-1', name: 'Bonap', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'fr-CH', timeZone: 'Europe/Zurich',
  leadKinds: 'establishment' as string | undefined,
}));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org, currentOrgId: org.id, organizations: [org] }),
  useOrgOptional: () => ({ currentOrg: org }),
}));

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Leads = (await import('./Leads')).default;

function serve(lead: Lead) {
  current.lead = lead;
  api.getLeads.mockResolvedValue([lead]);
  api.getLeadsPaged.mockResolvedValue({ items: [lead], totalCount: 1, page: 1, pageSize: 25 });
  api.getLeadById.mockResolvedValue(lead);
}

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  org.leadKinds = 'establishment';
  serve(trattoria);
  api.getLeadStats.mockResolvedValue(null);
  api.getCompanies.mockResolvedValue([{ id: 'c1', name: 'Trattoria Bella' }]);
  api.getContacts.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLeadIds.mockResolvedValue([]);
  api.convertLead.mockResolvedValue({ leadId: 'l1', dealId: 'd1' });
});

function open() {
  render(withQueries(
    <MemoryRouter initialEntries={['/leads?convertLeadId=l1']}>
      <Leads />
    </MemoryRouter>,
  ));
}

describe('converting an establishment', () => {
  it('reads as the establishment becoming its company, its contact person and a deal', async () => {
    open();

    expect(await screen.findByText(/into a company, its contact person and a deal/, undefined, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Make Marco Rossi a contact/ })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByText(/The establishment’s company/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /convert establishment/i }));
    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createContact: true, existingCompanyId: 'c1' });
  });

  it('converts into a deal alone when the contact person is taken off', async () => {
    open();

    fireEvent.click(await screen.findByRole('button', { name: /Make Marco Rossi a contact/ }, { timeout: 5000 }));
    expect(screen.getByRole('button', { name: /convert establishment/i })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: /create new deal/i }));
    expect(screen.getByLabelText('Deal name')).toHaveValue('Trattoria Bella');
    fireEvent.click(screen.getByRole('button', { name: /convert establishment/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createContact: false, createDeal: true, dealName: 'Trattoria Bella' });
  });

  it('offers no contact without the contact person’s email, and converts into a deal', async () => {
    serve({ ...trattoria, contactPersonEmail: undefined });
    open();

    expect(await screen.findByText(/Add Marco Rossi’s email to the lead/, undefined, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Make Marco Rossi a contact/ })).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /convert establishment/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createContact: false, createDeal: true });
  });
});

describe('converting a person', () => {
  it('reads as before, and still always becomes a contact', async () => {
    org.leadKinds = undefined;
    serve({ id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified' });
    open();

    const contact = await screen.findByRole('button', { name: /create new contact/i }, { timeout: 5000 });
    expect(screen.getByText(/into a contact, company, or deal/)).toBeInTheDocument();
    expect(screen.queryByText(/Establishment/)).not.toBeInTheDocument();

    // Pressing it again keeps it: a person's contact is not optional (a welcome needs one).
    fireEvent.click(contact);
    expect(contact).toHaveAttribute('aria-pressed', 'true');
    fireEvent.click(screen.getByRole('button', { name: /create new deal/i }));
    fireEvent.click(screen.getByRole('button', { name: /convert lead/i }));

    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createContact: true, createDeal: true, dealName: 'Jean Dupont' });
  });
});

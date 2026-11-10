// The lead edit dialog sends the version of the lead it was opened on and an emptied
// field as null, and says so when someone else saved the lead first. It sent back
// every field it showed, so a stale dialog put back whatever had changed since (a
// colleague's edit, a step the pipeline recorded), and a cleared phone was kept.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Lead } from '@/app/api/types';
import { ApiError } from '@/app/api/apiClient';

const lead: Lead = {
  id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', phone: '+41 79 123 45 67', status: 'Contacted',
  description: 'Met at the open day', version: 'v1',
};

const api = vi.hoisted(() => ({
  getLeads: vi.fn(), getLeadsPaged: vi.fn(), getLeadStats: vi.fn(), getLeadById: vi.fn(), updateLead: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getDeals: vi.fn(), getPipelines: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLeadIds: vi.fn(),
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
  api.updateLead.mockImplementation(async (_id: string, params: Partial<Lead>) => ({ ...lead, ...params, version: 'v-saved' }));
});

// The lead page hands over with ?edit=, which opens the dialog for that lead.
async function openEdit() {
  render(
    <MemoryRouter initialEntries={['/leads?edit=l1']}>
      <Leads />
    </MemoryRouter>,
  );
  return screen.findByLabelText(/phone number/i, undefined, { timeout: 5000 });
}

/** Through the wizard's steps to its last, and save. */
async function saveDialog() {
  for (let step = 0; step < 3; step++) {
    const next = screen.queryByRole('button', { name: /continue/i });
    if (!next) break;
    fireEvent.click(next);
  }
  fireEvent.click(await screen.findByRole('button', { name: /update lead/i }));
}

describe('the lead edit dialog', () => {
  it('sends the version it was opened on, and an emptied phone as null so it is taken off', async () => {
    const phone = await openEdit();

    fireEvent.change(phone, { target: { value: '' } });
    await saveDialog();

    await waitFor(() => expect(api.updateLead).toHaveBeenCalledTimes(1));
    expect(api.updateLead.mock.calls[0]).toEqual(['l1', expect.objectContaining({
      phone: null, companyId: null, version: 'v1', description: 'Met at the open day',
    })]);
  });

  it('says someone else saved the lead first, and Reload opens the latest copy', async () => {
    api.updateLead.mockRejectedValueOnce(new ApiError(
      'Someone else changed this lead after you opened it, so your changes were not saved. Reload it to see the latest, then make your change again.',
      409, '/api/leads/l1', true, 'Lead.Changed.Conflict'));
    await openEdit();

    await saveDialog();

    await waitFor(() => expect(toast.error).toHaveBeenCalledTimes(1));
    const [message, options] = toast.error.mock.calls[0] as [string, { action: { label: string; onClick: () => void } }];
    expect(message).toMatch(/someone else changed this lead/i);
    expect(options.action.label).toBe('Reload');
    expect(toast.success).not.toHaveBeenCalled();

    api.getLeadById.mockResolvedValue({ ...lead, phone: '+41 79 999 99 99', version: 'v2' });
    await act(async () => { options.action.onClick(); });
    await screen.findByDisplayValue('+41 79 999 99 99');
    await saveDialog();

    await waitFor(() => expect(api.updateLead).toHaveBeenCalledTimes(2));
    expect(api.updateLead.mock.calls[1]![1]).toMatchObject({ phone: '+41 79 999 99 99', version: 'v2' });
  });
});

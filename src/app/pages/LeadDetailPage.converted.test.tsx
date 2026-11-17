// A converted lead leads to what it became. Its page said "Converted" and nothing more,
// so there was no way from the lead to its deal, or to the welcome on it.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Lead } from '@/app/api/types';
import { withQueries } from '@/test/queryWrapper';

const api = vi.hoisted(() => ({
  getLeadById: vi.fn(), getCompanies: vi.fn(), getContacts: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLead: vi.fn(), getTasksByLead: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
vi.mock('@/app/api/organizations', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/organizations')>('@/app/api/organizations')),
  getOrgMembers: async () => [],
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));

const org = vi.hoisted(() => ({ id: 'org-1', name: 'Pavillon 46', ownerUserId: 'u', isOwner: true, role: 0, currency: 'CHF', locale: 'de-CH', timeZone: 'Europe/Zurich' }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: org, currentOrgId: org.id, organizations: [org] }),
  useOrgOptional: () => ({ currentOrg: org }),
}));
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() } }));

const LeadDetailPage = (await import('./LeadDetailPage')).default;

const base: Lead = { id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified', version: 'v1' };

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  api.getCompanies.mockResolvedValue([]);
  api.getContacts.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLead.mockResolvedValue([]);
  api.getTasksByLead.mockResolvedValue([]);
});

function renderPage() {
  render(withQueries(
    <MemoryRouter initialEntries={['/leads/l1']}>
      <Routes><Route path="/leads/:id" element={<LeadDetailPage />} /></Routes>
    </MemoryRouter>,
  ));
}

describe('a converted lead', () => {
  it('leads to the deal and the contact it became', async () => {
    api.getLeadById.mockResolvedValue({ ...base, isConverted: true, convertedToDealId: 'd1', convertedToContactId: 'c1' });
    renderPage();

    expect(await screen.findByRole('link', { name: /open the deal/i }, { timeout: 5000 })).toHaveAttribute('href', '/deals/d1');
    expect(screen.getByRole('link', { name: /^contact$/i })).toHaveAttribute('href', '/contacts/c1');
    expect(screen.queryByRole('button', { name: /^convert$/i })).not.toBeInTheDocument();
  });

  it('offers the conversion, and no deal, while it is still a lead', async () => {
    api.getLeadById.mockResolvedValue(base);
    renderPage();

    expect(await screen.findByRole('button', { name: /^convert$/i }, { timeout: 5000 })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /open the deal/i })).not.toBeInTheDocument();
  });
});

// The lead page's editor starts from the lead as it is now. The page's own changes (an
// assignment, a referrer, a logged call, which moves "last contacted" on the server)
// give the lead new versions the page never reads back, and the editor's save is
// checked against the version it opens with: opened on the page's copy, the next edit
// was refused as changed by someone else.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import type { Lead } from '@/app/api/types';
import { withQueries } from '@/test/queryWrapper';

const lead: Lead = {
  id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', phone: '+41 79 123 45 67', status: 'Contacted',
  description: 'Met at the open day', version: 'v1',
};

const api = vi.hoisted(() => ({
  getLeadById: vi.fn(), updateLead: vi.fn(), getCompanies: vi.fn(), getContacts: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLead: vi.fn(), getTasksByLead: vi.fn(),
  createActivity: vi.fn(), setLeadReferral: vi.fn(),
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

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const LeadDetailPage = (await import('./LeadDetailPage')).default;

beforeEach(() => {
  for (const fn of Object.values(api)) fn.mockReset();
  for (const fn of Object.values(toast)) fn.mockReset();
  localStorage.clear();
  api.getLeadById.mockResolvedValue(lead);
  api.getCompanies.mockResolvedValue([]);
  api.getContacts.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLead.mockResolvedValue([]);
  api.getTasksByLead.mockResolvedValue([]);
  api.createActivity.mockResolvedValue(null);
  api.updateLead.mockImplementation(async (_id: string, params: Partial<Lead>) => ({ ...lead, ...params, version: 'v-saved' }));
});

async function renderPage() {
  // Under the app's query cache, as in the app: the club membership card reads through it.
  render(withQueries(
    <MemoryRouter initialEntries={['/leads/l1']}>
      <Routes>
        <Route path="/leads/:id" element={<LeadDetailPage />} />
      </Routes>
    </MemoryRouter>,
  ));
  return screen.findByRole('button', { name: 'Edit' }, { timeout: 5000 });
}

/** Through the editor's steps to its last, and save. */
async function saveEditor() {
  for (let step = 0; step < 3; step++) {
    const next = screen.queryByRole('button', { name: /continue/i });
    if (!next) break;
    fireEvent.click(next);
  }
  fireEvent.click(await screen.findByRole('button', { name: /update lead/i }));
}

describe('the lead page editor', () => {
  it('opens on the lead as it is now, so its save is not refused as someone else\'s change', async () => {
    const edit = await renderPage();
    // Since the page read the lead, a call logged on it moved it to a new version.
    api.getLeadById.mockResolvedValue({ ...lead, phone: '+41 79 555 55 55', version: 'v2' });

    fireEvent.click(edit);
    await screen.findByDisplayValue('+41 79 555 55 55');
    await saveEditor();

    await waitFor(() => expect(api.updateLead).toHaveBeenCalledTimes(1));
    expect(api.updateLead.mock.calls[0]).toEqual(['l1', expect.objectContaining({ phone: '+41 79 555 55 55', version: 'v2' })]);
  });

  it('keeps what was typed when the newer copy arrives after typing began', async () => {
    const edit = await renderPage();
    let arrive: (fresh: Lead) => void = () => {};
    api.getLeadById.mockReturnValue(new Promise<Lead>((resolve) => { arrive = resolve; }));

    fireEvent.click(edit);
    const phone = await screen.findByLabelText(/phone number/i);
    fireEvent.change(phone, { target: { value: '+41 79 222 22 22' } });
    await act(async () => { arrive({ ...lead, phone: '+41 79 555 55 55', version: 'v2' }); });

    expect(screen.getByDisplayValue('+41 79 222 22 22')).toBeInTheDocument();
    await saveEditor();

    await waitFor(() => expect(api.updateLead).toHaveBeenCalledTimes(1));
    // Still checked against the copy it was typed on, so a stale save is refused, not written.
    expect(api.updateLead.mock.calls[0]![1]).toMatchObject({ phone: '+41 79 222 22 22', version: 'v1' });
  });
});

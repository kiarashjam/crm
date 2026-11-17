// Converting a lead can welcome them to the member portal. The choice sat out of sight
// until a deal was picked, so people did not know a conversion could send a welcome. It is
// now one of the dialog's choices from the start: choosing it adds the deal it goes with,
// and the welcome is previewed and sent once the lead is converted.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { withQueries } from '@/test/queryWrapper';
import type { Lead } from '@/app/api/types';
import type { OnboardingSettings } from '@/app/api/onboarding';

const person: Lead = { id: 'l1', name: 'Jean Dupont', email: 'jean@example.ch', status: 'Qualified' };
const noEmail: Lead = { id: 'l2', name: 'Anna Keller', email: '', status: 'Qualified' };

const api = vi.hoisted(() => ({
  getLeads: vi.fn(), getLeadsPaged: vi.fn(), getLeadStats: vi.fn(), getLeadById: vi.fn(),
  getCompanies: vi.fn(), getContacts: vi.fn(), getDeals: vi.fn(), getPipelines: vi.fn(),
  getLeadStatuses: vi.fn(), getLeadSources: vi.fn(), getActivitiesByLeadIds: vi.fn(), convertLead: vi.fn(),
}));
vi.mock('@/app/api', async () => ({ ...(await vi.importActual<typeof import('@/app/api')>('@/app/api')), ...api }));
const onboarding = vi.hoisted(() => ({ getOnboardingSettings: vi.fn(), previewOnboarding: vi.fn(), startOnboarding: vi.fn() }));
vi.mock('@/app/api/onboarding', async () => ({ ...(await vi.importActual<typeof import('@/app/api/onboarding')>('@/app/api/onboarding')), ...onboarding }));
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

function settings(over: Partial<OnboardingSettings> = {}): OnboardingSettings {
  return {
    hasSecret: true, enabled: true, defaultLanguage: 'en', addToClubMembers: true, configured: true,
    cadenceFields: ['firstName'], receiverFields: ['setPasswordUrl'], ...over,
  };
}

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(onboarding), ...Object.values(toast)]) fn.mockReset();
  localStorage.clear();
  api.getLeads.mockResolvedValue([person, noEmail]);
  api.getLeadsPaged.mockResolvedValue({ items: [person, noEmail], totalCount: 2, page: 1, pageSize: 25 });
  api.getLeadStats.mockResolvedValue(null);
  api.getLeadById.mockImplementation(async (id: string) => (id === 'l2' ? noEmail : person));
  api.getCompanies.mockResolvedValue([]);
  api.getContacts.mockResolvedValue([]);
  api.getDeals.mockResolvedValue([]);
  api.getPipelines.mockResolvedValue([]);
  api.getLeadStatuses.mockResolvedValue([]);
  api.getLeadSources.mockResolvedValue([]);
  api.getActivitiesByLeadIds.mockResolvedValue([]);
  onboarding.getOnboardingSettings.mockResolvedValue(settings());
  onboarding.previewOnboarding.mockResolvedValue({ subject: 'Welcome, Jean', html: '<p>Welcome</p>', usesReceiverDesign: false, unknown: [] });
  onboarding.startOnboarding.mockResolvedValue({ id: 'o1', dealId: 'd1', status: 'queued', events: [] });
});

async function openConversion(leadId = 'l1') {
  render(withQueries(
    <MemoryRouter initialEntries={[`/leads?convertLeadId=${leadId}`]}>
      <Leads />
    </MemoryRouter>,
  ));
  await screen.findByRole('button', { name: /create new deal/i }, { timeout: 5000 });
  return screen.findByRole('button', { name: /send welcome email/i });
}

describe('welcoming a lead as they are converted', () => {
  it('opens with the deal and its welcome when the workspace welcomes every new customer', async () => {
    // Unticked, the usual conversion made a contact alone: no deal to find, no welcome sent.
    const welcome = await openConversion();
    await waitFor(() => expect(welcome).toHaveAttribute('aria-pressed', 'true'));
    expect(screen.getByRole('button', { name: /create new deal/i })).toHaveClass('border-amber-400');
    expect(screen.getByRole('button', { name: /preview welcome & convert/i })).toBeInTheDocument();
  });

  it('offers the welcome from the start, before any deal is picked, where it is not on by default', async () => {
    onboarding.getOnboardingSettings.mockResolvedValue(settings({ enabled: false }));
    const welcome = await openConversion();
    expect(welcome).toHaveAttribute('aria-pressed', 'false');
    expect(welcome).toBeEnabled();
    expect(screen.getByRole('button', { name: /create new deal/i })).not.toHaveClass('border-amber-400');
    expect(screen.getByRole('button', { name: /convert lead/i })).toBeInTheDocument();
  });

  it('adds the deal it goes with when chosen, then previews the welcome before converting', async () => {
    onboarding.getOnboardingSettings.mockResolvedValue(settings({ enabled: false }));
    api.convertLead.mockResolvedValue({ leadId: 'l1', contactId: 'c1', dealId: 'd1' });
    fireEvent.click(await openConversion());

    expect(screen.getByRole('button', { name: /send welcome email/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /create new deal/i })).toHaveClass('border-amber-400');
    expect(screen.getByText(/goes with the deal/i)).toHaveTextContent('Goes with the deal Jean Dupont, their membership.');
    expect(screen.getByLabelText('Welcome message')).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText('Welcome message'), { target: { value: 'Welcome to the club!' } });
    fireEvent.click(screen.getByRole('button', { name: /preview welcome & convert/i }));
    // Nothing is converted until the email has been seen and confirmed.
    await waitFor(() => expect(onboarding.previewOnboarding).toHaveBeenCalled());
    expect(api.convertLead).not.toHaveBeenCalled();
    expect(onboarding.previewOnboarding.mock.calls[0]?.[0]).toMatchObject({ email: 'jean@example.ch', welcomeMessage: 'Welcome to the club!' });

    fireEvent.click(await screen.findByRole('button', { name: /convert & send welcome/i }));
    await waitFor(() => expect(api.convertLead).toHaveBeenCalled());
    expect(api.convertLead.mock.calls[0]?.[1]).toMatchObject({ createContact: true, createDeal: true });
    await waitFor(() => expect(onboarding.startOnboarding).toHaveBeenCalledWith('d1', expect.objectContaining({ leadId: 'l1', welcomeMessage: 'Welcome to the club!' })));
  });

  it('can be taken off again, leaving the deal', async () => {
    const welcome = await openConversion();
    await waitFor(() => expect(welcome).toHaveAttribute('aria-pressed', 'true'));
    fireEvent.click(welcome);

    expect(screen.getByRole('button', { name: /send welcome email/i })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /create new deal/i })).toHaveClass('border-amber-400');
    expect(screen.getByRole('button', { name: /^convert lead$/i })).toBeInTheDocument();
  });

  it('goes off with the deal it went with', async () => {
    const welcome = await openConversion();
    await waitFor(() => expect(welcome).toHaveAttribute('aria-pressed', 'true'));
    fireEvent.click(screen.getByRole('button', { name: /create new deal/i }));

    expect(screen.getByRole('button', { name: /send welcome email/i })).toHaveAttribute('aria-pressed', 'false');
    expect(screen.getByRole('button', { name: /^convert lead$/i })).toBeInTheDocument();
  });

  it('says why it cannot go when there is nobody to send it to', async () => {
    const welcome = await openConversion('l2');
    expect(welcome).toBeDisabled();
    expect(welcome).toHaveTextContent('Anna Keller has no email address: add one to the lead, or pick an existing contact above.');
  });

  it('points to the settings while the portal is not connected', async () => {
    onboarding.getOnboardingSettings.mockResolvedValue(settings({ configured: false, enabled: false, hasSecret: false }));
    render(withQueries(
      <MemoryRouter initialEntries={['/leads?convertLeadId=l1']}>
        <Leads />
      </MemoryRouter>,
    ));
    fireEvent.click(await screen.findByRole('button', { name: /create new deal/i }, { timeout: 5000 }));

    const link = await screen.findByRole('link', { name: 'Connect your portal' });
    expect(link).toHaveAttribute('href', '/settings?tab=welcome');
    expect(screen.queryByRole('button', { name: /send welcome email/i })).not.toBeInTheDocument();
    expect(within(link.closest('div')!).getByText(/welcome them to your member portal/i)).toBeInTheDocument();
  });
});

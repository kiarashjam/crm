// Welcome & portal settings: someone who may change them gets the editor, and someone who
// may not is told why and who can, rather than shown greyed-out fields and nothing else.
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { Organization } from '@/app/api/organizations';

vi.mock('@/app/hooks/useMotionPreference', () => ({ useMotionPreference: () => true }));
vi.mock('@/app/components/LoadingOverlay', () => ({ usePageLoading: () => {} }));
vi.mock('@/app/api/apiClient', async (original) => ({
  ...(await original<typeof import('@/app/api/apiClient')>()),
  servesDemoData: () => false,
}));
vi.mock('@/app/hooks/queries/records', () => ({
  useOrgMembers: () => ({
    data: [
      { userId: 'o', name: 'Anna Keller', email: 'anna@example.ch', role: 0 },
      { userId: 'm', name: 'Beat Meier', email: 'beat@example.ch', role: 2, permissions: ['settings.manage', 'records.edit'] },
      { userId: 'r', name: 'Kia', email: 'kia@example.ch', role: 1, permissions: ['records.edit'] },
    ],
  }),
}));
vi.mock('@/app/api/onboarding', async (original) => ({
  ...(await original<typeof import('@/app/api/onboarding')>()),
  getOnboardingSettings: async () => ({
    webhookUrl: 'https://portal.example.ch/api/integrations/cadence/onboarding', hasSecret: true, secretHint: 'ab12', enabled: true,
    emailSubject: null, emailHtml: null, defaultLanguage: 'fr', addToClubMembers: true, configured: true,
    cadenceFields: ['firstName'], receiverFields: ['setPasswordUrl'],
  }),
  previewOnboarding: async () => ({ subject: 'Bienvenue', html: '<p>Hi</p>', usesReceiverDesign: true, unknown: [] }),
}));

const { WelcomePortalSection } = await import('./WelcomePortalSection');

const org = (access: Partial<Organization>): Organization =>
  ({ id: 'org-a', name: 'Pavillon 46', ownerUserId: 'o', isOwner: false, role: 1, ...access }) as Organization;

describe('Welcome & portal settings', () => {
  it('lets someone who manages settings change them', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    expect(await screen.findByLabelText('Welcome email HTML')).toBeEnabled();
    expect(screen.queryByText(/not change them/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Make a new one/ })).toBeInTheDocument();
  });

  it("tells someone who may not why, and who can", async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ permissions: ['records.edit'] })} /></MemoryRouter>);
    expect(await screen.findByText('You can see these settings, but not change them.')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('which Anna Keller and Beat Meier have');
    expect(screen.getByRole('link', { name: /Settings › Team/ })).toHaveAttribute('href', '/settings?tab=organization');
    expect(screen.getByLabelText('Welcome email HTML')).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Make a new one/ })).not.toBeInTheDocument();
  });
});

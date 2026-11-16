// Welcome & portal settings: someone who may change them gets the editor, and someone who
// may not is told why and who can, rather than shown greyed-out fields and nothing else.
import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
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
  testOnboardingPortal: async () => ({
    ok: true, statusCode: 200,
    callbackProblem: 'The portal reports back only to an https address on the internet, and this CRM\'s address is http://localhost:5160.',
  }),
}));

const { WelcomePortalSection } = await import('./WelcomePortalSection');

const org = (access: Partial<Organization>): Organization =>
  ({ id: 'org-a', name: 'Pavillon 46', ownerUserId: 'o', isOwner: false, role: 1, ...access }) as Organization;

describe('Welcome & portal settings', () => {
  it('lets someone who manages settings change them', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    expect(await screen.findByLabelText('HTML')).toBeEnabled();
    expect(screen.queryByText(/not change them/)).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Make a new one/ })).toBeInTheDocument();
  });

  it('says when there are unsaved changes, and takes them back on Discard', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    const subject = await screen.findByLabelText('Subject');
    expect(screen.getByText('All changes saved')).toBeInTheDocument();
    fireEvent.change(subject, { target: { value: 'Welcome, {{firstName}}' } });
    expect(screen.getByText('Unsaved changes')).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: /Discard/ }));
    expect(subject).toHaveValue('');
    expect(screen.getByText('All changes saved')).toBeInTheDocument();
  });

  it('warns that a <style> block is removed before sending', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    const html = await screen.findByLabelText('HTML');
    expect(screen.queryByText(/block\. The portal removes it/)).not.toBeInTheDocument();
    // Words about a <style> block in a comment are not one.
    fireEvent.change(html, { target: { value: '<!-- the portal removes <style> blocks --><p>Hi</p>' } });
    expect(screen.queryByText(/block\. The portal removes it/)).not.toBeInTheDocument();
    fireEvent.change(html, { target: { value: '<html><head><style>@media (max-width:600px){.col{display:block}}</style></head><body>Hi</body></html>' } });
    expect(screen.getByText(/block\. The portal removes it/)).toBeInTheDocument();
  });

  it('says when the portal could not report back, though the test went through', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    fireEvent.click(await screen.findByRole('button', { name: /Send a test/ }));
    expect(await screen.findByRole('alert')).toHaveTextContent(/could not report back.*https address/);
  });

  it('shows the preview at a phone\'s width on request', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    const phone = await screen.findByRole('button', { name: /Phone/ });
    expect(phone).toHaveAttribute('aria-pressed', 'false');
    fireEvent.click(phone);
    expect(phone).toHaveAttribute('aria-pressed', 'true');
    expect(await screen.findByTitle('Email preview')).toHaveStyle({ width: '375px' });
  });

  it("tells someone who may not why, and who can", async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ permissions: ['records.edit'] })} /></MemoryRouter>);
    expect(await screen.findByText('You can see these settings, but not change them.')).toBeInTheDocument();
    expect(screen.getByRole('status')).toHaveTextContent('which Anna Keller and Beat Meier have');
    expect(screen.getByRole('link', { name: /Settings › Team/ })).toHaveAttribute('href', '/settings?tab=organization');
    expect(screen.getByLabelText('HTML')).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Make a new one/ })).not.toBeInTheDocument();
  });
});

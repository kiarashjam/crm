// Welcome & portal settings: someone who may change them gets the editor, and someone who
// may not is told why and who can, rather than shown greyed-out fields and nothing else.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
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
const api = vi.hoisted(() => ({
  emailHtml: null as string | null,
  saved: [] as Array<Record<string, unknown>>,
  tests: [] as Array<Record<string, unknown>>,
}));
vi.mock('@/app/lib/auth', async (original) => ({
  ...(await original<typeof import('@/app/lib/auth')>()),
  getCurrentUser: () => ({ id: 'o', name: 'Anna Keller', email: 'anna@example.ch' }),
}));
vi.mock('@/app/api/onboarding', async (original) => ({
  ...(await original<typeof import('@/app/api/onboarding')>()),
  getOnboardingSettings: async () => ({
    webhookUrl: 'https://portal.example.ch/api/integrations/cadence/onboarding', hasSecret: true, secretHint: 'ab12', enabled: true,
    emailSubject: null, emailHtml: api.emailHtml, defaultLanguage: 'fr', addToClubMembers: true, configured: true,
    cadenceFields: ['firstName', 'lastName'], receiverFields: ['setPasswordUrl'],
  }),
  saveOnboardingSettings: async (request: Record<string, unknown>) => {
    api.saved.push(request);
    return {
      webhookUrl: request.webhookUrl, hasSecret: true, secretHint: 'ab12', enabled: request.enabled, emailSubject: request.emailSubject,
      emailHtml: request.emailHtml, defaultLanguage: request.defaultLanguage, addToClubMembers: request.addToClubMembers, configured: true,
      cadenceFields: ['firstName', 'lastName'], receiverFields: ['setPasswordUrl'],
    };
  },
  sendOnboardingTestEmail: async (request: Record<string, unknown>) => {
    api.tests.push(request);
    return { to: request.to, status: 'sent' };
  },
  previewOnboarding: async () => ({ subject: 'Bienvenue', html: '<p>Hi</p>', usesReceiverDesign: true, unknown: [] }),
  testOnboardingPortal: async () => ({
    ok: true, statusCode: 200,
    callbackProblem: 'The portal reports back only to an https address on the internet, and this CRM\'s address is http://localhost:5160.',
  }),
}));

const { WelcomePortalSection } = await import('./WelcomePortalSection');
const { buildSimpleHtml, simpleDefaults } = await import('@/app/components/onboarding/simpleEmail');

beforeEach(() => {
  api.emailHtml = null;
  api.saved = [];
  api.tests = [];
  vi.stubGlobal('confirm', vi.fn(() => true));
});

const org = (access: Partial<Organization>): Organization =>
  ({ id: 'org-a', name: 'Pavillon 46', ownerUserId: 'o', isOwner: false, role: 1, ...access }) as Organization;

describe('Welcome & portal settings', () => {
  it('lets someone who manages settings change them', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    expect(await screen.findByRole('radio', { name: /Portal design/ })).toHaveAttribute('aria-checked', 'true');
    fireEvent.click(screen.getByRole('radio', { name: /HTML/ }));
    expect(screen.getByLabelText('HTML')).toBeEnabled();
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
    fireEvent.click(await screen.findByRole('radio', { name: /HTML/ }));
    const html = screen.getByLabelText('HTML');
    fireEvent.change(html, { target: { value: '<p>Hi</p>' } });
    expect(screen.queryByText(/block\. The portal removes it/)).not.toBeInTheDocument();
    // Words about a <style> block in a comment are not one.
    fireEvent.change(html, { target: { value: '<!-- the portal removes <style> blocks --><p>Hi</p>' } });
    expect(screen.queryByText(/block\. The portal removes it/)).not.toBeInTheDocument();
    fireEvent.change(html, { target: { value: '<html><head><style>@media (max-width:600px){.col{display:block}}</style></head><body>Hi</body></html>' } });
    expect(screen.getByText(/block\. The portal removes it/)).toBeInTheDocument();
  });

  it('says when the portal could not report back, though the test went through', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    fireEvent.click(await screen.findByRole('button', { name: /Test the connection/ }));
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
    expect(screen.getByLabelText('Subject')).toBeDisabled();
    expect(screen.getByRole('radio', { name: /Simple/ })).toBeDisabled();
    expect(screen.queryByRole('button', { name: /Make a new one/ })).not.toBeInTheDocument();
  });
  it('writes the email in the simple editor, and a field button adds to the box last typed in', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    fireEvent.click(await screen.findByRole('radio', { name: /Simple/ }));
    expect(screen.getByLabelText('Heading')).toHaveValue('Bienvenue, {{firstName}}');

    const message = screen.getByLabelText('Message');
    fireEvent.change(message, { target: { value: 'Bonjour ' } });
    fireEvent.focus(message);
    (message as HTMLTextAreaElement).setSelectionRange(8, 8);
    fireEvent.click(screen.getByRole('button', { name: /Last name/ }));
    await waitFor(() => expect(screen.getByLabelText('Message')).toHaveValue('Bonjour {{lastName}}'));

    fireEvent.click(screen.getByRole('button', { name: /^Save$/ }));
    await waitFor(() => expect(api.saved).toHaveLength(1));
    const html = String(api.saved[0]?.emailHtml);
    expect(html).toContain('<p style="margin:0 0 16px;font:15px/1.65 Helvetica,Arial,sans-serif;color:#44403c">Bonjour {{lastName}}</p>');
    expect(html).toContain('href="{{setPasswordUrl}}"');
  });

  it('opens a simple email in the simple editor, and HTML of your own in the HTML one', async () => {
    api.emailHtml = buildSimpleHtml({ ...simpleDefaults('en'), heading: 'Hello {{firstName}}' });
    const { unmount } = render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    expect(await screen.findByLabelText('Heading')).toHaveValue('Hello {{firstName}}');
    expect(screen.getByRole('radio', { name: /Simple/ })).toHaveAttribute('aria-checked', 'true');
    unmount();

    api.emailHtml = '<p>My own</p>';
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    expect(await screen.findByLabelText('HTML')).toHaveValue('<p>My own</p>');
  });

  it('sends a test of the email as it stands to the address given', async () => {
    render(<MemoryRouter><WelcomePortalSection currentOrg={org({ isOwner: true, role: 0 })} /></MemoryRouter>);
    const to = await screen.findByLabelText(/Send a test email/);
    expect(to).toHaveValue('anna@example.ch');
    fireEvent.change(to, { target: { value: 'sarah@pavillon46.ch' } });
    fireEvent.click(screen.getByRole('radio', { name: /Simple/ }));
    // The preview has come back with nothing unfilled, so the test may go.
    await screen.findByTitle('Email preview');
    fireEvent.click(screen.getByRole('button', { name: /Send test email/ }));

    const result = await screen.findByText(/Sent to sarah@pavillon46\.ch/);
    expect(within(result).queryByRole('alert')).not.toBeInTheDocument();
    expect(api.tests).toHaveLength(1);
    expect(api.tests[0]).toMatchObject({ to: 'sarah@pavillon46.ch', name: 'Camille Laurent', language: 'fr' });
    expect(String(api.tests[0]?.emailHtml)).toContain('Choisir mon mot de passe');
    expect(api.saved).toHaveLength(0);
  });
});

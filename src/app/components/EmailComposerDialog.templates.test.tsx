// The composer starts from a prepared email, in French or English: chosen, its subject and
// text come up for the person to change, shown as they will read it, and sent from the
// workspace. Blanks left to fill in, and placeholders nothing fills, keep it from going.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { EmailTemplate } from '@/app/api/emailTemplates';

const api = vi.hoisted(() => ({ sendEmail: vi.fn(), cancelEmail: vi.fn(), createActivity: vi.fn(), generateCopy: vi.fn() }));
vi.mock('@/app/api', async () => {
  const { mailtoHref } = await vi.importActual<typeof import('@/app/api/email')>('@/app/api/email');
  return { ...api, isUsingRealApi: () => true, servesDemoData: () => false, mailtoHref };
});
const templates = vi.hoisted(() => ({ getEmailTemplates: vi.fn(), createEmailTemplate: vi.fn() }));
vi.mock('@/app/api/emailTemplates', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/emailTemplates')>('@/app/api/emailTemplates')),
  ...templates,
}));
const orgs = vi.hoisted(() => ({ getEmailSending: vi.fn() }));
vi.mock('@/app/api/organizations', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/organizations')>('@/app/api/organizations')),
  ...orgs,
}));
vi.mock('@/app/contexts/OrgContext', () => ({ useOrgOptional: () => ({ currentOrg: { id: 'org-1', name: 'Pavillon 46' } }) }));
vi.mock('@/app/lib/auth', async () => ({
  ...(await vi.importActual<typeof import('@/app/lib/auth')>('@/app/lib/auth')),
  getCurrentUser: () => ({ id: 'u1', name: 'Claire Martin', email: 'claire@pavillon46.ch' }),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), info: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const EmailComposerDialog = (await import('./EmailComposerDialog')).default;

const visit: EmailTemplate = {
  id: 't1', name: 'Invitation to visit the club', category: 'Club', description: 'Invites a prospective member.', starter: true,
  createdAtUtc: '2026-11-17T00:00:00Z',
  versions: [
    { language: 'fr', subject: 'Une invitation à découvrir {{workspace}}', body: 'Bonjour {{firstName|Madame, Monsieur}},\n\nNous serions ravis de vous accueillir le [[date]].\n\n{{senderName}}' },
    { language: 'en', subject: 'An invitation to discover {{workspace}}', body: 'Dear {{firstName|Sir or Madam}},\n\nWe would be delighted to welcome you.\n\n{{senderName}}' },
  ],
};
const frenchOnly: EmailTemplate = {
  id: 't2', name: 'Petit rappel', category: 'Follow-up', starter: false, createdAtUtc: '2026-11-17T00:00:00Z',
  versions: [{ language: 'fr', subject: 'Petit rappel', body: 'Bonjour {{firstName}},' }],
};

beforeEach(() => {
  for (const fn of [...Object.values(api), ...Object.values(templates), ...Object.values(orgs), ...Object.values(toast)]) fn.mockReset();
  localStorage.clear();
  templates.getEmailTemplates.mockResolvedValue({
    templates: [visit, frenchOnly], languages: ['fr', 'en'], defaultLanguage: 'fr',
    placeholders: ['firstName', 'lastName', 'name', 'email', 'company', 'senderName', 'workspace'],
  });
  orgs.getEmailSending.mockResolvedValue({ sendingAddress: 'hello@pavillon46.ch', verified: true, sharedAddress: 'mail@cadencee.ai', repliesCaptured: false });
  api.sendEmail.mockResolvedValue({ id: 'm1', status: 'sent' });
});

async function openComposer() {
  const user = userEvent.setup();
  const onOpenChange = vi.fn();
  render(
    <MemoryRouter>
      <EmailComposerDialog open onOpenChange={onOpenChange} to="jean@example.ch" recipientName="Jean Dupont" company="Dupont SA" context={{ leadId: 'l1' }} />
    </MemoryRouter>,
  );
  const list = await screen.findByRole('complementary', { name: 'Prepared emails' });
  await within(list).findByRole('button', { name: /Invitation to visit the club/ });
  return { user, list, onOpenChange };
}

const subject = () => screen.getByLabelText('Subject') as HTMLInputElement;
const body = () => screen.getByLabelText('Message') as HTMLTextAreaElement;

describe('the composer with prepared emails', () => {
  it('says it goes from the workspace, and replies come back to the person writing', async () => {
    await openComposer();
    expect(await screen.findByTestId('email-from')).toHaveTextContent('Pavillon 46 <hello@pavillon46.ch>');
    expect(screen.getByText(/Replies come to you at claire@pavillon46\.ch/)).toBeInTheDocument();
  });

  it('puts a prepared email in, in the workspace language, and the other language when switched', async () => {
    const { user, list } = await openComposer();
    expect(screen.getByRole('radio', { name: 'Français' })).toHaveAttribute('aria-checked', 'true');

    await user.click(within(list).getByRole('button', { name: /Invitation to visit the club/ }));
    expect(subject().value).toBe('Une invitation à découvrir {{workspace}}');
    expect(body().value).toContain('Nous serions ravis');

    await user.click(screen.getByRole('radio', { name: 'English' }));
    expect(subject().value).toBe('An invitation to discover {{workspace}}');
    expect(localStorage.getItem('cadence.email.language')).toBe('en');
  });

  it('asks before replacing what the person wrote', async () => {
    const { user, list } = await openComposer();
    await user.click(within(list).getByRole('button', { name: /Invitation to visit the club/ }));
    await user.type(body(), ' Avec plaisir.');

    await user.click(screen.getByRole('radio', { name: 'English' }));
    expect(screen.getByRole('alert')).toHaveTextContent('Replace what you wrote');
    await user.click(screen.getByRole('button', { name: 'Keep my text' }));
    expect(body().value).toContain('Avec plaisir.');

    await user.click(screen.getByRole('radio', { name: 'English' }));
    await user.click(screen.getByRole('button', { name: 'Replace' }));
    expect(subject().value).toBe('An invitation to discover {{workspace}}');
  });

  it('says when a prepared email has no version in the language, and offers the one it has', async () => {
    const { user, list } = await openComposer();
    await user.click(screen.getByRole('radio', { name: 'English' }));
    await user.click(within(list).getByRole('button', { name: /Petit rappel/ }));
    // Chosen in English, it opens in the French it has.
    expect(subject().value).toBe('Petit rappel');
    expect(screen.getByRole('radio', { name: 'Français' })).toHaveAttribute('aria-checked', 'true');

    await user.click(screen.getByRole('radio', { name: 'English' }));
    expect(screen.getByText(/has no English version yet/)).toBeInTheDocument();
  });

  it('shows it as the person will read it, filled in for them', async () => {
    const { user, list } = await openComposer();
    await user.click(within(list).getByRole('button', { name: /Invitation to visit the club/ }));
    await user.click(screen.getByRole('tab', { name: /Preview/ }));

    const preview = screen.getByLabelText('Preview');
    expect(preview).toHaveTextContent('Une invitation à découvrir Pavillon 46');
    expect(preview).toHaveTextContent('Bonjour Jean,');
    expect(preview).toHaveTextContent('Claire Martin');
    expect(within(preview).getByText('date').tagName).toBe('MARK');
  });

  it('will not send while a blank is left to fill in, and sends once it is', async () => {
    const { user, list, onOpenChange } = await openComposer();
    await user.click(within(list).getByRole('button', { name: /Invitation to visit the club/ }));
    expect(screen.getByTestId('email-left-to-fill')).toHaveTextContent('date');

    await user.click(screen.getByRole('button', { name: 'Send' }));
    expect(api.sendEmail).not.toHaveBeenCalled();
    expect(toast.error).toHaveBeenCalledWith('Fill in before sending: date');

    await user.clear(body());
    await user.type(body(), 'Bonjour {{{{firstName}},{enter}{enter}Le 3 décembre ?');
    await user.click(screen.getByRole('button', { name: 'Send' }));
    await waitFor(() => expect(api.sendEmail).toHaveBeenCalled());
    // The placeholders go to the server, which fills them from the record.
    expect(api.sendEmail.mock.calls[0]?.[0]).toMatchObject({
      to: 'jean@example.ch', subject: 'Une invitation à découvrir {{workspace}}', body: 'Bonjour {{firstName}},\n\nLe 3 décembre ?', leadId: 'l1',
    });
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('will not send a placeholder nothing fills', async () => {
    const { user } = await openComposer();
    await user.type(subject(), 'Hello');
    await user.type(body(), 'Hi {{{{first_name}}');
    await user.click(screen.getByRole('button', { name: 'Send' }));
    expect(api.sendEmail).not.toHaveBeenCalled();
    expect(toast.error.mock.calls[0]?.[0]).toContain('{{first_name}} is not something Cadence can fill in');
  });

  it('saves what was written as a prepared email, in its language', async () => {
    templates.createEmailTemplate.mockImplementation(async (r: { name: string; versions: unknown[] }) => ({ ...r, id: 't9', starter: false, createdAtUtc: 'x' }));
    const { user } = await openComposer();
    await user.type(subject(), 'Soirée dégustation');
    await user.type(body(), 'Bonjour {{{{firstName}},');
    await user.click(screen.getByRole('button', { name: /Save as prepared email/ }));
    await user.clear(screen.getByLabelText('Name'));
    await user.type(screen.getByLabelText('Name'), 'Wine evening');
    await user.type(screen.getByLabelText('Category'), 'Events');
    await user.click(screen.getByRole('button', { name: 'Save' }));

    await waitFor(() => expect(templates.createEmailTemplate).toHaveBeenCalledWith({
      name: 'Wine evening', category: 'Events', versions: [{ language: 'fr', subject: 'Soirée dégustation', body: 'Bonjour {{firstName}},' }],
    }));
  });
});

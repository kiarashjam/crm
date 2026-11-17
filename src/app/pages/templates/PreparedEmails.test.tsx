// The workspace's prepared emails, French and English side by side: listed by category,
// with what is missing a language said, written and changed in both languages at once.
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import type { EmailTemplate } from '@/app/api/emailTemplates';
import { withQueries } from '@/test/queryWrapper';

const templates = vi.hoisted(() => ({
  getEmailTemplates: vi.fn(), createEmailTemplate: vi.fn(), updateEmailTemplate: vi.fn(), deleteEmailTemplate: vi.fn(),
}));
vi.mock('@/app/api/emailTemplates', async () => ({
  ...(await vi.importActual<typeof import('@/app/api/emailTemplates')>('@/app/api/emailTemplates')),
  ...templates,
}));
vi.mock('@/app/components/AppHeader', () => ({ default: () => null }));
vi.mock('@/app/contexts/OrgContext', () => ({
  useOrg: () => ({ isReadOnly: false, currentOrg: { id: 'org-1', name: 'Pavillon 46' } }),
  useOrgOptional: () => ({ currentOrg: { id: 'org-1', name: 'Pavillon 46' } }),
}));
const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const Templates = (await import('@/app/pages/Templates')).default;

const visit: EmailTemplate = {
  id: 't1', name: 'Invitation to visit the club', category: 'Club', description: 'Invites a prospective member.', starter: true, createdAtUtc: 'x',
  versions: [
    { language: 'fr', subject: 'Une invitation à découvrir {{workspace}}', body: 'Bonjour {{firstName}},\n\nLe [[date]].' },
    { language: 'en', subject: 'An invitation to discover {{workspace}}', body: 'Dear {{firstName}},' },
  ],
};
const reminder: EmailTemplate = {
  id: 't2', name: 'Petit rappel', category: 'Follow-up', starter: false, createdAtUtc: 'x',
  versions: [{ language: 'fr', subject: 'Petit rappel', body: 'Bonjour,' }],
};

beforeEach(() => {
  for (const fn of [...Object.values(templates), ...Object.values(toast)]) fn.mockReset();
  templates.getEmailTemplates.mockResolvedValue({
    templates: [visit, reminder], languages: ['fr', 'en'], defaultLanguage: 'fr',
    placeholders: ['firstName', 'lastName', 'name', 'email', 'company', 'senderName', 'workspace'],
  });
});

function renderPage() {
  render(withQueries(<MemoryRouter initialEntries={['/templates']}><Templates /></MemoryRouter>));
}

describe('prepared emails', () => {
  it('lists them by category, and says which language one is missing', async () => {
    renderPage();
    const club = await screen.findByRole('region', { name: 'Club' }, { timeout: 5000 });
    expect(within(club).getByRole('article', { name: 'Invitation to visit the club' })).toHaveTextContent('Une invitation à découvrir {{workspace}}');
    const followUp = screen.getByRole('region', { name: 'Follow-up' });
    expect(within(followUp).getByRole('article', { name: 'Petit rappel' })).toHaveTextContent('No English version yet');
    expect(screen.getByRole('tab', { name: 'Prepared emails' })).toHaveAttribute('aria-selected', 'true');
  });

  it('adds the missing language to one, both sent together', async () => {
    templates.updateEmailTemplate.mockImplementation(async (id: string, r: Record<string, unknown>) => ({ ...reminder, ...r, id }));
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Edit Petit rappel' }, { timeout: 5000 }));
    const english = screen.getByRole('region', { name: 'English version' });
    await user.type(within(english).getByLabelText('Subject'), 'A gentle reminder');
    await user.type(within(english).getByLabelText('Text'), 'Dear ');
    await user.click(within(english).getByRole('button', { name: 'First name' }));
    await user.click(screen.getByRole('button', { name: 'Save changes' }));

    await waitFor(() => expect(templates.updateEmailTemplate).toHaveBeenCalledWith('t2', {
      name: 'Petit rappel', category: 'Follow-up', description: null,
      versions: [
        { language: 'fr', subject: 'Petit rappel', body: 'Bonjour,' },
        { language: 'en', subject: 'A gentle reminder', body: 'Dear {{firstName}}' },
      ],
    }));
  });

  it('shows a version as someone would read it, blanks marked', async () => {
    const user = userEvent.setup();
    renderPage();
    await user.click(await screen.findByRole('button', { name: 'Edit Invitation to visit the club' }, { timeout: 5000 }));
    const french = screen.getByRole('region', { name: 'French version' });
    await user.click(within(french).getByRole('button', { name: /Preview/ }));
    expect(french).toHaveTextContent('Une invitation à découvrir Pavillon 46');
    expect(french).toHaveTextContent('Bonjour Camille,');
    expect(within(french).getByText('date').tagName).toBe('MARK');
  });

  it('keeps the AI writing templates on their own tab', async () => {
    renderPage();
    expect(await screen.findByRole('tab', { name: 'AI writing templates' }, { timeout: 5000 })).toHaveAttribute('href', '/templates?tab=ai');
  });
});

// Someone signed in who never chose a language reads their workspace's (fr-CH: French). The
// app says so once and offers the others; whatever they pick, closing included, goes onto
// their account, and the notice is gone. A person who chose already is never asked.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';

vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: { id: 'org-1', name: 'Pavillon 46', locale: 'fr-CH' } }),
}));

const { I18nProvider, loadCatalogue, useT } = await import('./index');
const { LanguageNotice } = await import('./LanguageNotice');

const BASE = 'http://api.test';
let account: string | null;
let saved: unknown[];

function Page() {
  const { t } = useT();
  return <p>{t('unsubscribe.button')}</p>;
}

beforeEach(async () => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'session-token');
  account = null;
  saved = [];
  await loadCatalogue('fr');
  server.use(
    http.get(`${BASE}/api/settings/language`, () => HttpResponse.json({ language: account })),
    http.put(`${BASE}/api/settings/language`, async ({ request }) => {
      const body = (await request.json()) as { language: string | null };
      saved.push(body);
      account = body.language;
      return HttpResponse.json(body);
    }),
  );
});

describe('the language notice', () => {
  it('says the app follows the workspace, and keeps the language picked on the account', async () => {
    const user = userEvent.setup();
    render(<I18nProvider><LanguageNotice /><Page /></I18nProvider>);
    // French, from the workspace's locale.
    expect(await screen.findByText('Se désinscrire')).toBeInTheDocument();
    expect(await screen.findByRole('status')).toHaveTextContent('Cadence suit désormais la langue de votre espace de travail');

    await user.click(screen.getByRole('button', { name: 'English' }));
    expect(screen.getByText('Unsubscribe')).toBeInTheDocument();
    await waitFor(() => expect(saved).toEqual([{ language: 'en' }]));
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('closing it keeps the workspace’s language as the person’s own', async () => {
    const user = userEvent.setup();
    render(<I18nProvider><LanguageNotice /><Page /></I18nProvider>);
    await screen.findByRole('status');
    await user.click(screen.getByRole('button', { name: 'Fermer' }));
    await waitFor(() => expect(saved).toEqual([{ language: 'fr' }]));
    expect(screen.queryByRole('status')).toBeNull();
    expect(screen.getByText('Se désinscrire')).toBeInTheDocument();
  });

  it('is not shown to someone who chose a language', async () => {
    account = 'de';
    await loadCatalogue('de');
    render(<I18nProvider><LanguageNotice /><Page /></I18nProvider>);
    expect(await screen.findByText('Abmelden')).toBeInTheDocument();
    expect(screen.queryByRole('status')).toBeNull();
  });
});

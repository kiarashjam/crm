// A club's front desk shares one browser between the people who work it. The app keeps a
// person's language in the browser as well as on their account, so the sign-in page and the
// app open in it at once; that copy is theirs, not the next person's. Someone who signs in
// after them and never chose a language reads their workspace's, and is offered the others.

import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { clearSession, setSession } from '@/app/lib/auth';

vi.mock('@/app/contexts/OrgContext', () => ({
  useOrgOptional: () => ({ currentOrg: { id: 'org-1', name: 'Pavillon 46', locale: 'de-CH' } }),
}));

const { I18nProvider, loadCatalogue, useT } = await import('./index');
const { LanguageNotice } = await import('./LanguageNotice');

const BASE = 'http://api.test';
/** Each account's language, by the token it signs in with. */
let accounts: Record<string, string | null>;

function Page() {
  const { language } = useT();
  return <p data-testid="language">{language}</p>;
}

const shown = () => screen.getByTestId('language');

beforeEach(async () => {
  localStorage.clear();
  sessionStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  accounts = { 'token-ada': 'fr', 'token-ben': null };
  await loadCatalogue('de');
  await loadCatalogue('fr');
  server.use(
    http.get(`${BASE}/api/settings/language`, ({ request }) => {
      const token = (request.headers.get('Authorization') ?? '').replace('Bearer ', '');
      return HttpResponse.json({ language: accounts[token] ?? null });
    }),
  );
});

describe('a browser two people sign in on', () => {
  it('opens the next person in their workspace’s language, not in the last person’s', async () => {
    setSession('token-ada', { id: 'ada', name: 'Ada', email: 'ada@example.ch' });
    render(<I18nProvider><LanguageNotice /><Page /></I18nProvider>);
    await waitFor(() => expect(shown()).toHaveTextContent('fr'));

    // Signed out, the sign-in page stays in the language this browser was last in.
    act(() => clearSession());
    await waitFor(() => expect(shown()).toHaveTextContent('fr'));

    // Ben has chosen no language: he reads the workspace's (de-CH) and is offered the others.
    act(() => setSession('token-ben', { id: 'ben', name: 'Ben', email: 'ben@example.ch' }));
    await waitFor(() => expect(shown()).toHaveTextContent('de'));
    expect(await screen.findByRole('status')).toBeInTheDocument();
  });

  it('still opens someone in the language picked in this browser before they signed in', async () => {
    // Picked on a public page, or the sign-in page, by whoever is at the browser now.
    localStorage.setItem('cadence.language', 'fr');
    setSession('token-ben', { id: 'ben', name: 'Ben', email: 'ben@example.ch' });
    render(<I18nProvider><Page /></I18nProvider>);
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 50));
    });
    expect(shown()).toHaveTextContent('fr');
  });
});

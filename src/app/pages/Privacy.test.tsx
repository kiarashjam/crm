// The privacy notice and the terms, as a stranger reads them: the facts come from the server
// (who answers for the data, the services it goes to, where it is kept and for how long), not
// from a placeholder company in San Francisco under California law.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';

const Privacy = (await import('./Privacy')).default;
const Terms = (await import('./Terms')).default;

const BASE = 'http://api.test';

const operator = { name: 'Example Software GmbH', address: 'Bahnhofstrasse 1\n8001 Zürich', email: 'privacy@operator.example', dpoContact: null };
const clubNotice = {
  operator,
  controller: { name: 'Pavillon 46', address: 'Seestrasse 46, 8002 Zürich', email: 'datenschutz@pavillon46.example', dpoContact: 'A. Muster, Datenschutz' },
  workspaceName: 'Pavillon 46',
  hosting: 'Microsoft Azure (App Service and Azure SQL Database), West Europe region (the Netherlands)',
  processors: [
    { name: 'Microsoft (Azure)', purpose: 'Hosting the app and its database.', location: 'West Europe', transfer: null },
    {
      name: 'OpenAI',
      purpose: "Drafting the text of an email when staff ask the AI writer; the recipient's email address does not go with the request.",
      location: 'United States',
      transfer: "Standard contractual clauses in OpenAI's data processing addendum",
    },
  ],
  retention: { leadMonths: 18, memberYears: null, mailMonths: 6, detailMonths: null },
  marketingConsentText: 'Yes, send me the club news.',
  sessionHours: 24,
};

let asked: string[] = [];

beforeEach(() => {
  asked = [];
  vi.stubEnv('VITE_API_URL', BASE);
  server.use(http.get(`${BASE}/api/public/privacy`, ({ request }) => {
    const form = new URL(request.url).searchParams.get('form');
    asked.push(form ?? '');
    if (form === 'tok') return HttpResponse.json(clubNotice);
    return HttpResponse.json({ ...clubNotice, controller: operator, workspaceName: null, retention: null, marketingConsentText: null, processors: clubNotice.processors.slice(0, 1) });
  }));
});
afterEach(() => vi.unstubAllEnvs());

const show = (url: string, page = <Privacy />) => render(withQueries(
  <MemoryRouter initialEntries={[url]}>
    <Routes>
      <Route path="/privacy" element={page} />
      <Route path="/terms" element={page} />
    </Routes>
  </MemoryRouter>,
));

describe('the privacy notice', () => {
  it("opened from a club's form, names the club, the services the data goes to and how long it is kept", async () => {
    show('/privacy?form=tok');

    expect(await screen.findByText('Pavillon 46')).toBeInTheDocument();
    expect(asked).toEqual(['tok']);
    // Named as the controller, and as where to write to use a right.
    const mail = screen.getAllByRole('link', { name: 'datenschutz@pavillon46.example' });
    expect(mail).toHaveLength(2);
    for (const link of mail) expect(link).toHaveAttribute('href', 'mailto:datenschutz@pavillon46.example');
    expect(screen.getByText(/A\. Muster, Datenschutz/)).toBeInTheDocument();
    // The operator runs the app for the club.
    expect(screen.getByText('Example Software GmbH')).toBeInTheDocument();
    // The AI writer's transfer to the United States, and how it is covered.
    expect(screen.getByText('OpenAI')).toBeInTheDocument();
    expect(screen.getByText(/Where: United States\. Covered by: Standard contractual clauses/)).toBeInTheDocument();
    // The club's own periods, and what has none.
    expect(screen.getByText(/once nothing has been done on them for a while: after 18 months/)).toBeInTheDocument();
    expect(screen.getByText(/whether they arrived stay\): after 6 months/)).toBeInTheDocument();
    expect(screen.getByText(/for a while: no period is set, so it is kept until the club deletes it/)).toBeInTheDocument();
    // The sign-in token is a bearer token kept in the browser, for as long as the server says.
    expect(screen.getByText(/valid for 24 hours/)).toHaveTextContent(/bearer token/);
    expect(screen.getByText(/Yes, send me the club news\./)).toBeInTheDocument();

    const text = document.body.textContent ?? '';
    expect(text).toMatch(/nFADP/);
    expect(text).toMatch(/GDPR/);
    expect(text).toMatch(/FDPIC/);
    expect(text).not.toMatch(/privacy@aci\.com|San Francisco|California|never exposed/);
  });

  it('opened from no form, names the operator and leaves the periods to each club', async () => {
    show('/privacy');

    expect(await screen.findByText('This app is run by:')).toBeInTheDocument();
    expect(asked).toEqual(['']);
    expect(screen.getByText('Example Software GmbH')).toBeInTheDocument();
    expect(screen.getByText(/Each club sets its own periods/)).toBeInTheDocument();
    expect(screen.queryByText('OpenAI')).toBeNull();
  });

  it('says so when its details cannot be loaded, and tries again', async () => {
    let calls = 0;
    server.use(http.get(`${BASE}/api/public/privacy`, () => {
      calls += 1;
      return calls === 1 ? new HttpResponse(null, { status: 503 }) : HttpResponse.json(clubNotice);
    }));
    show('/privacy?form=tok');

    expect(await screen.findByRole('alert')).toHaveTextContent('could not be loaded');
    await userEvent.setup().click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByText('Pavillon 46')).toBeInTheDocument();
  });

  it('the demo says where the facts come from instead of making them up', async () => {
    vi.stubEnv('VITE_API_URL', '');
    show('/privacy');

    expect(await screen.findByText(/This is the demo/)).toBeInTheDocument();
    expect(asked).toEqual([]);
  });
});

describe('the terms', () => {
  it('name the operator from the settings, not a placeholder company under California law', async () => {
    show('/terms', <Terms />);

    expect(await screen.findByText('Example Software GmbH')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'privacy@operator.example' })).toBeInTheDocument();
    expect(document.body.textContent).not.toMatch(/legal@aci\.com|San Francisco|California/);
  });
});

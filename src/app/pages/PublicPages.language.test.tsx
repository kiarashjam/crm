// What people outside the workspace open from a link reads in their language.
//
// The application and enquiry forms take the ?lang= the club's website put on the link,
// else the visitor's browser, else the club's own language; they say which with what they
// send, so the thank-you, the consent record and the member's language follow. A member's
// unsubscribe and pay links open in the language the club writes to them in. The picker
// above every page switches it at once, and the club's own words stay as written.

import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { I18nProvider, loadCatalogue } from '@/app/i18n';
import type { PublicPayPage } from '@/app/api/payments';
import type { PublicContract } from '@/app/api/contracts';

const pay = vi.hoisted(() => ({ read: vi.fn(), start: vi.fn() }));
vi.mock('@/app/api/payments', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/payments')>()),
  getPayPage: (...a: unknown[]) => pay.read(...a),
  startCheckout: (...a: unknown[]) => pay.start(...a),
}));
const unsubscribe = vi.hoisted(() => ({ get: vi.fn(), confirm: vi.fn() }));
vi.mock('@/app/api/unsubscribe', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/unsubscribe')>()),
  getUnsubscribe: (...a: unknown[]) => unsubscribe.get(...a),
  confirmUnsubscribe: (...a: unknown[]) => unsubscribe.confirm(...a),
}));
const contracts = vi.hoisted(() => ({ contract: null as PublicContract | null }));
vi.mock('@/app/api/contracts', async (importOriginal) => ({
  ...(await importOriginal<typeof import('@/app/api/contracts')>()),
  getPublicContract: () => Promise.resolve(contracts.contract!),
  publicContractPdfUrl: (t: string) => `/api/public/contracts/${t}/pdf`,
}));

const PublicApply = (await import('./PublicApply')).default;
const PublicCapture = (await import('./PublicCapture')).default;
const Unsubscribe = (await import('./Unsubscribe')).default;
const PayPage = (await import('./PayPage')).default;
const SignContract = (await import('./SignContract')).default;

const BASE = 'http://api.test';
const realLanguages = Object.getOwnPropertyDescriptor(window.navigator, 'languages');

function browserSpeaks(...languages: string[]) {
  Object.defineProperty(window.navigator, 'languages', { value: languages, configurable: true });
}

const open = (path: string, route: string, page: JSX.Element) => render(
  <I18nProvider>
    <MemoryRouter initialEntries={[path]}>
      <Routes><Route path={route} element={page} /></Routes>
    </MemoryRouter>
  </I18nProvider>,
);

let applyGets: string[];
let applyPost: Record<string, unknown> | null;
let captureGets: string[];
let capturePost: Record<string, unknown> | null;

beforeAll(async () => {
  await loadCatalogue('de');
  await loadCatalogue('fr');
});

beforeEach(() => {
  localStorage.clear();
  browserSpeaks('en-GB');
  vi.stubEnv('VITE_API_URL', BASE);
  applyGets = [];
  applyPost = null;
  captureGets = [];
  capturePost = null;
  server.use(
    http.get(`${BASE}/api/public/apply/tok`, ({ request }) => {
      applyGets.push(new URL(request.url).searchParams.get('lang') ?? '');
      return HttpResponse.json({
        clubName: 'Pavillon 46', intro: 'Le club vous souhaite la bienvenue.', consentText: 'Les nouvelles du club, s’il vous plaît.',
        tiers: [{ name: 'Gold', price: 299, cadence: 'Monthly', initiationFee: null, currency: 'CHF' }], language: 'de',
      });
    }),
    http.post(`${BASE}/api/public/apply/tok`, async ({ request }) => {
      applyPost = (await request.json()) as Record<string, unknown>;
      return HttpResponse.json({ message: 'Merci, à bientôt.' }, { status: 201 });
    }),
    http.get(`${BASE}/api/public/capture/cap`, ({ request }) => {
      captureGets.push(new URL(request.url).searchParams.get('lang') ?? '');
      return HttpResponse.json({ name: '', marketingConsent: 'off', language: 'fr' });
    }),
    http.post(`${BASE}/api/public/capture/cap`, async ({ request }) => {
      capturePost = (await request.json()) as Record<string, unknown>;
      return HttpResponse.json({ message: null });
    }),
  );
});

afterEach(() => {
  vi.unstubAllEnvs();
  if (realLanguages) Object.defineProperty(window.navigator, 'languages', realLanguages);
  else delete (window.navigator as unknown as Record<string, unknown>).languages;
  document.documentElement.lang = 'en';
});

describe('the membership application form', () => {
  it('reads in the language on its link, asks the server in it, and sends it with the application', async () => {
    const user = userEvent.setup();
    open('/apply/tok?lang=fr', '/apply/:token', <PublicApply />);

    expect(await screen.findByRole('heading', { name: 'Demande d’adhésion à Pavillon 46' })).toBeInTheDocument();
    expect(document.documentElement.lang).toBe('fr');
    // The club's own words stay as the club wrote them.
    expect(screen.getByText('Le club vous souhaite la bienvenue.')).toBeInTheDocument();
    await user.type(screen.getByLabelText(/Prénom/), 'Léa');
    await user.type(screen.getByLabelText(/^Nom/), 'Rochat');
    await user.type(screen.getByLabelText(/^E-mail/), 'lea@example.ch');
    await user.selectOptions(screen.getByLabelText('Formule d’adhésion'), 'Gold');
    await user.click(screen.getByRole('button', { name: 'Envoyer ma demande' }));

    expect(await screen.findByText('Merci, à bientôt.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Merci' })).toBeInTheDocument();
    expect(applyGets).toContain('fr');
    expect(applyPost).toMatchObject({ firstName: 'Léa', language: 'fr' });
  });

  it('follows the browser before the club, and the club when the browser speaks none of ours', async () => {
    browserSpeaks('fr-CH', 'en');
    const first = open('/apply/tok', '/apply/:token', <PublicApply />);
    expect(await screen.findByRole('heading', { name: 'Demande d’adhésion à Pavillon 46' })).toBeInTheDocument();
    first.unmount();

    // An Italian browser on a club whose locale is German reads German.
    browserSpeaks('it-CH');
    open('/apply/tok', '/apply/:token', <PublicApply />);
    expect(await screen.findByRole('heading', { name: 'Mitgliedschaft bei Pavillon 46 beantragen' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Antrag senden' })).toBeInTheDocument();
    await waitFor(() => expect(applyGets[applyGets.length - 1]).toBe('de'));
  });
});

describe('the enquiry form', () => {
  it('switches language from its picker without a reload, keeps what was typed, and sends the new one', async () => {
    const user = userEvent.setup();
    open('/capture/cap', '/capture/:token', <PublicCapture />);

    expect(await screen.findByRole('heading', { name: 'Tell us about you' })).toBeInTheDocument();
    await user.type(screen.getByLabelText(/^Name/), 'Jean');
    await user.selectOptions(screen.getByLabelText('Language'), 'de');

    expect(await screen.findByRole('heading', { name: 'Erzählen Sie uns von sich' })).toBeInTheDocument();
    expect(screen.getByLabelText(/^Name/)).toHaveValue('Jean');
    await waitFor(() => expect(captureGets[captureGets.length - 1]).toBe('de'));
    await user.type(screen.getByLabelText(/^E-Mail/), 'jean@example.ch');
    await user.click(screen.getByRole('button', { name: 'Senden' }));

    expect(await screen.findByText('Danke. Wir melden uns bei Ihnen.')).toBeInTheDocument();
    expect(capturePost).toMatchObject({ name: 'Jean', language: 'de' });
    expect(localStorage.getItem('cadence.language')).toBe('de');
  });

  it('asks a business for its place in the language read, keeps the choice of business through a switch, and sends the box with it', async () => {
    server.use(http.get(`${BASE}/api/public/capture/biz`, ({ request }) => {
      captureGets.push(new URL(request.url).searchParams.get('lang') ?? '');
      return HttpResponse.json({ name: '', marketingConsent: 'optional', language: 'fr', leadKinds: 'both' });
    }), http.post(`${BASE}/api/public/capture/biz`, async ({ request }) => {
      capturePost = (await request.json()) as Record<string, unknown>;
      return HttpResponse.json({ message: null });
    }));
    const user = userEvent.setup();
    open('/capture/biz', '/capture/:token', <PublicCapture />);

    await user.click(await screen.findByRole('radio', { name: 'For a business' }));
    await user.type(screen.getByLabelText(/^Establishment name/), 'Trattoria Bella');
    await user.selectOptions(screen.getByLabelText('Language'), 'de');

    // The form is read again in German: the visitor still writes for the business they chose.
    expect(await screen.findByRole('radio', { name: 'Für einen Betrieb' })).toHaveAttribute('aria-checked', 'true');
    await waitFor(() => expect(captureGets[captureGets.length - 1]).toBe('de'));
    expect(screen.getByLabelText(/^Name des Betriebs/)).toHaveValue('Trattoria Bella');
    expect(screen.getByRole('option', { name: 'Bäckerei' })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Art'), 'restaurant');
    await user.type(screen.getByLabelText(/^Ihr Name/), 'Marco Rossi');
    await user.type(screen.getByLabelText(/^E-Mail/), 'marco@trattoria.ch');
    await user.click(screen.getByRole('checkbox', { name: /Ja, ich möchte per E-Mail/ }));
    await user.click(screen.getByRole('button', { name: 'Senden' }));

    expect(await screen.findByText('Danke. Wir melden uns bei Ihnen.')).toBeInTheDocument();
    expect(capturePost).toMatchObject({
      kind: 'establishment', establishmentName: 'Trattoria Bella', establishmentType: 'restaurant',
      name: 'Marco Rossi', email: 'marco@trattoria.ch', marketingConsent: true, language: 'de',
    });
    expect(capturePost).not.toHaveProperty('company');
  });
});

describe('a member’s own links', () => {
  it('opens the unsubscribe page in the language the club writes to the member in', async () => {
    unsubscribe.get.mockReset().mockResolvedValue({
      workspaceName: 'Pavillon 46', email: 'jean@example.ch', scope: 'marketing', language: 'de',
    });
    open('/unsubscribe/tok', '/unsubscribe/:token', <Unsubscribe />);

    expect(await screen.findByRole('heading', { name: 'Keine Neuigkeiten und Angebote mehr von Pavillon 46?' })).toBeInTheDocument();
    expect(screen.getByText(/Kontoauszüge und Buchungsbestätigungen, erhalten Sie weiterhin/)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Abmelden' })).toBeInTheDocument();
  });

  it('opens the pay page in the member’s language, with the methods in it and the amount in the club’s format', async () => {
    const page: PublicPayPage = {
      clubName: 'Pavillon 46', memberFirstName: 'Jean', purpose: 'account', description: 'Ce que vous devez à Pavillon 46',
      amount: 1234.5, currency: 'CHF', methods: ['card', 'twint'], available: true, autoPay: false, language: 'fr', locale: 'de-CH',
    };
    pay.read.mockReset().mockResolvedValue(page);
    open('/pay/tok', '/pay/:token', <PayPage />);

    expect(await screen.findByRole('heading', { name: 'Bonjour Jean' })).toBeInTheDocument();
    expect(screen.getByText('Payez par carte ou TWINT sur la page sécurisée de Stripe.')).toBeInTheDocument();
    // de-CH groups thousands with an apostrophe, typographic or straight by ICU version.
    expect(screen.getByRole('button', { name: /^Payer CHF.1.234\.50$/ })).toBeInTheDocument();
  });

  it('lets a signer read the contract page in their language, with the signature explained in it', async () => {
    localStorage.setItem('cadence.language', 'fr');
    contracts.contract = {
      status: 'sent', title: 'Contrat d’adhésion', body: 'Le texte du contrat.', counterpartyName: 'Jean Dupont',
      organizationName: 'Pavillon 46', sentAtUtc: '2026-08-01T09:00:00Z', canSign: true,
    } as PublicContract;
    const user = userEvent.setup();
    open('/sign/tok', '/sign/:token', <SignContract />);

    expect(await screen.findByRole('button', { name: /Signer le contrat/ })).toBeInTheDocument();
    expect(screen.getByText(/Il s’agit d’une signature électronique simple/)).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Signer le contrat/ }));
    expect(screen.getByRole('alert')).toHaveTextContent('Saisissez votre nom complet comme signature.');
  });
});

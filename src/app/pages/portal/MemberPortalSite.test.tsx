// The member portal as a member uses it: asking for a sign-in link, using it (once, with a
// press of Continue, or at once when the club's own portal hands them over), and their own
// account, read with the member's session and never the staff app's, in their language, with
// a failed read said and tried again.

import { afterEach, beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { withQueries } from '@/test/queryWrapper';
import { I18nProvider, loadCatalogue } from '@/app/i18n';
import type { PortalEvent, PortalMe } from '@/app/api/portal';

const toast = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), message: vi.fn() }));
vi.mock('sonner', () => ({ toast }));

const MemberPortalSite = (await import('./MemberPortalSite')).default;

const BASE = 'http://api.test';
const KEY = 'key1';
const realLanguages = Object.getOwnPropertyDescriptor(window.navigator, 'languages');

const CLUB = { clubName: 'Pavillon 46', open: true, locale: 'de-CH', language: null };

function member(over: Partial<PortalMe> = {}): PortalMe {
  return {
    memberId: 'm-anna',
    firstName: 'Anna',
    lastName: 'Keller',
    email: 'anna@example.ch',
    preferredLanguage: null,
    tier: 'Gold',
    status: 'Active',
    active: true,
    joinedAtUtc: '2024-03-01T00:00:00Z',
    renewsAtUtc: '2027-03-01T00:00:00Z',
    startUndetermined: false,
    duesAmount: 1200,
    duesFrequency: 'Annual',
    clubName: 'Pavillon 46',
    currency: 'CHF',
    locale: 'de-CH',
    timeZone: 'Europe/Zurich',
    memberCancelHours: 24,
    paysOnline: false,
    ...over,
  };
}

function event(over: Partial<PortalEvent> = {}): PortalEvent {
  return {
    id: 'e1',
    name: 'Wine evening',
    startAtUtc: '2030-12-05T18:00:00Z',
    endAtUtc: '2030-12-05T21:00:00Z',
    coverEmoji: '🍷',
    memberPrice: 0,
    currency: 'CHF',
    seatsLeft: 12,
    myStatus: 'none',
    canCancel: false,
    householdComing: [],
    ...over,
  };
}

function signedIn(language: string | null = null) {
  localStorage.setItem(`cadence.portal.${KEY}`, JSON.stringify({
    token: 'member-token',
    expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(),
    memberId: 'm-anna',
    firstName: 'Anna',
    clubName: 'Pavillon 46',
    language,
  }));
}

/** The member's own reads, answered only for the member's token. */
function memberApi(me: () => PortalMe | Response, seen: string[] = []) {
  const authorised = (request: Request) => {
    const header = request.headers.get('Authorization') ?? '';
    seen.push(header);
    return header === 'Bearer member-token';
  };
  const answer = <T,>(request: Request, body: T) => (authorised(request) ? HttpResponse.json(body) : new HttpResponse(null, { status: 401 }));
  server.use(
    http.get(`${BASE}/api/portal/me`, ({ request }) => {
      if (!authorised(request)) return new HttpResponse(null, { status: 401 });
      const body = me();
      return body instanceof Response ? body : HttpResponse.json(body);
    }),
    http.get(`${BASE}/api/portal/account`, ({ request }) => answer(request, {
      currency: 'CHF', balance: 0, owed: 0, credit: 0, openCharges: [], firstMonth: '2026-11', currentMonth: '2026-11', billsGoToPrimary: false,
    })),
    http.get(`${BASE}/api/portal/events`, ({ request }) => answer(request, [])),
    http.get(`${BASE}/api/portal/reservations`, ({ request }) => answer(request, [])),
    http.get(`${BASE}/api/portal/points`, ({ request }) => answer(request, { balance: 120, history: [] })),
  );
}

function open(path: string) {
  return render(withQueries(
    <I18nProvider>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/member-portal/:key" element={<MemberPortalSite />} />
          <Route path="/member-portal/:key/sign-in" element={<MemberPortalSite landing />} />
        </Routes>
      </MemoryRouter>
    </I18nProvider>,
  ));
}

beforeAll(async () => {
  await loadCatalogue('de');
  await loadCatalogue('fr');
});

beforeEach(() => {
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  Object.defineProperty(window.navigator, 'languages', { value: ['en-GB'], configurable: true });
  window.history.replaceState(null, '', '/');
  toast.success.mockReset();
  toast.error.mockReset();
  server.use(http.get(`${BASE}/api/public/portal/${KEY}`, () => HttpResponse.json(CLUB)));
});

afterEach(() => {
  vi.unstubAllGlobals();
  if (realLanguages) Object.defineProperty(window.navigator, 'languages', realLanguages);
});

describe('signing in', () => {
  it('asks for a link by email and says it is on its way, without saying whether the address is a member’s', async () => {
    let sent: unknown;
    server.use(http.post(`${BASE}/api/public/portal/${KEY}/sign-in-link`, async ({ request }) => {
      sent = await request.json();
      return new HttpResponse(null, { status: 202 });
    }));
    const user = userEvent.setup();
    open(`/member-portal/${KEY}`);

    expect(await screen.findByText('Pavillon 46')).toBeInTheDocument();
    await user.type(screen.getByLabelText('Email'), ' anna@example.ch ');
    await user.click(screen.getByRole('button', { name: 'Send me a link' }));

    expect(await screen.findByRole('heading', { name: 'Check your email' })).toBeInTheDocument();
    expect(sent).toEqual({ email: 'anna@example.ch' });
    expect(screen.getByText(/If anna@example\.ch is the address Pavillon 46 has for you/)).toBeInTheDocument();
  });

  it('says when the club has the portal closed, and when the address is no portal at all', async () => {
    server.use(http.get(`${BASE}/api/public/portal/${KEY}`, () => HttpResponse.json({ ...CLUB, open: false })));
    const { unmount } = open(`/member-portal/${KEY}`);
    expect(await screen.findByRole('heading', { name: 'The member portal is closed' })).toBeInTheDocument();
    expect(screen.queryByLabelText('Email')).not.toBeInTheDocument();
    unmount();

    server.use(http.get(`${BASE}/api/public/portal/nope`, () => HttpResponse.json({ detail: 'Not found' }, { status: 404 })));
    open('/member-portal/nope');
    expect(await screen.findByRole('heading', { name: 'This address is not a member portal' })).toBeInTheDocument();
  });

  it('uses an emailed link only when Continue is pressed, takes it off the address bar, and opens the account with the member’s own session', async () => {
    let redeemed: unknown;
    const seen: string[] = [];
    // A staff session in the same browser is neither sent to the portal nor touched by it.
    localStorage.setItem('aci_token', 'staff-session');
    server.use(http.get(`${BASE}/api/settings/language`, () => HttpResponse.json({ language: null })));
    server.use(http.post(`${BASE}/api/public/portal/${KEY}/sign-in`, async ({ request }) => {
      redeemed = await request.json();
      return HttpResponse.json({
        token: 'member-token', expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(),
        memberId: 'm-anna', firstName: 'Anna', clubName: 'Pavillon 46', language: null,
      });
    }));
    memberApi(() => member(), seen);
    window.history.replaceState(null, '', `/member-portal/${KEY}/sign-in#token=abc123`);
    const user = userEvent.setup();
    open(`/member-portal/${KEY}/sign-in`);

    const go = await screen.findByRole('button', { name: 'Continue' });
    expect(window.location.hash).toBe('');
    expect(redeemed).toBeUndefined();
    await user.click(go);

    expect(await screen.findByRole('heading', { name: 'Welcome back, Anna' })).toBeInTheDocument();
    expect(redeemed).toEqual({ token: 'abc123' });
    expect(seen.length).toBeGreaterThan(0);
    expect(seen.every((h) => h === 'Bearer member-token')).toBe(true);
    expect(localStorage.getItem('aci_token')).toBe('staff-session');
    expect(JSON.parse(localStorage.getItem(`cadence.portal.${KEY}`) ?? '{}').token).toBe('member-token');
  });

  it('says a spent link is spent, with the way to ask for a new one', async () => {
    server.use(http.post(`${BASE}/api/public/portal/${KEY}/sign-in`, () =>
      HttpResponse.json({ detail: 'This sign-in link no longer works.', errorCode: 'Portal.LinkInvalid' }, { status: 400 })));
    window.history.replaceState(null, '', `/member-portal/${KEY}/sign-in#token=spent`);
    const user = userEvent.setup();
    open(`/member-portal/${KEY}/sign-in`);

    await user.click(await screen.findByRole('button', { name: 'Continue' }));
    expect(await screen.findByRole('heading', { name: 'This link no longer works' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Ask for a new link' })).toHaveAttribute('href', `/member-portal/${KEY}`);
    expect(localStorage.getItem(`cadence.portal.${KEY}`)).toBeNull();
  });

  it('signs in at once from a link the club’s own portal handed over', async () => {
    let redeemed: unknown;
    server.use(http.post(`${BASE}/api/public/portal/${KEY}/sign-in`, async ({ request }) => {
      redeemed = await request.json();
      return HttpResponse.json({
        token: 'member-token', expiresAtUtc: new Date(Date.now() + 86_400_000).toISOString(),
        memberId: 'm-anna', firstName: 'Anna', clubName: 'Pavillon 46', language: null,
      });
    }));
    memberApi(() => member());
    window.history.replaceState(null, '', `/member-portal/${KEY}/sign-in?handoff=1#token=handed`);
    open(`/member-portal/${KEY}/sign-in?handoff=1`);

    expect(await screen.findByRole('heading', { name: 'Welcome back, Anna' })).toBeInTheDocument();
    expect(redeemed).toEqual({ token: 'handed' });
  });
});

describe('the member’s account', () => {
  it('opens in the member’s language, and keeps the one they pick on their record', async () => {
    let current = member({ preferredLanguage: 'fr' });
    let patched: unknown;
    signedIn('fr');
    memberApi(() => current);
    server.use(http.patch(`${BASE}/api/portal/me`, async ({ request }) => {
      patched = await request.json();
      current = { ...current, preferredLanguage: 'de' };
      return HttpResponse.json(current);
    }));
    const user = userEvent.setup();
    open(`/member-portal/${KEY}`);

    expect(await screen.findByRole('heading', { name: 'Bon retour, Anna' })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Langue'), 'de');

    expect(await screen.findByRole('heading', { name: 'Willkommen zurück, Anna' })).toBeInTheDocument();
    await waitFor(() => expect(patched).toEqual({ preferredLanguage: 'de' }));
  });

  it('says when a read failed, and reads it again on Try again', async () => {
    let calls = 0;
    signedIn();
    memberApi(() => {
      calls += 1;
      return calls === 1 ? HttpResponse.json({ detail: 'Boom' }, { status: 500 }) : member();
    });
    const user = userEvent.setup();
    open(`/member-portal/${KEY}`);

    expect(await screen.findByText('This could not be loaded.')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(await screen.findByRole('heading', { name: 'Welcome back, Anna' })).toBeInTheDocument();
  });

  it('goes back to the sign-in page when the club ended the session', async () => {
    signedIn();
    server.use(http.get(`${BASE}/api/portal/me`, () => new HttpResponse(null, { status: 401 })));
    open(`/member-portal/${KEY}`);

    expect(await screen.findByRole('button', { name: 'Send me a link' })).toBeInTheDocument();
    expect(localStorage.getItem(`cadence.portal.${KEY}`)).toBeNull();
  });

  it('says when the membership starts, or that its start is still to be decided', async () => {
    signedIn();
    let current = member({ startUndetermined: true });
    memberApi(() => current);
    const { unmount } = open(`/member-portal/${KEY}`);
    expect(await screen.findByText('The start of your membership is still to be decided.')).toBeInTheDocument();
    unmount();

    current = member({ startsOnUtc: '2030-01-15T00:00:00Z' });
    open(`/member-portal/${KEY}`);
    expect(await screen.findByText(/^Your membership starts on .*2030\.$/)).toBeInTheDocument();
  });

  it('signs up for an event and cancels the place before the club’s cut-off', async () => {
    signedIn();
    memberApi(() => member());
    let current = event();
    const posted: string[] = [];
    server.use(
      http.get(`${BASE}/api/portal/events`, () => HttpResponse.json([current])),
      http.post(`${BASE}/api/portal/events/e1/register`, () => {
        posted.push('register');
        current = event({ myStatus: 'Reserved', myRegistrationId: 'r1', canCancel: true, seatsLeft: 11 });
        return HttpResponse.json(current);
      }),
      http.post(`${BASE}/api/portal/registrations/r1/cancel`, () => {
        posted.push('cancel');
        current = event({ myStatus: 'Cancelled', myRegistrationId: 'r1' });
        return HttpResponse.json(current);
      }),
    );
    vi.stubGlobal('confirm', vi.fn(() => true));
    const user = userEvent.setup();
    open(`/member-portal/${KEY}?tab=events`);

    expect(await screen.findByText('Places can be cancelled here up to 24 hours before the start.')).toBeInTheDocument();
    await user.click(await screen.findByRole('button', { name: 'Sign me up' }));
    expect(await screen.findByText('You have a place')).toBeInTheDocument();
    expect(toast.success).toHaveBeenCalledWith('You are on the list.');

    await user.click(screen.getByRole('button', { name: 'Cancel my place' }));
    expect(await screen.findByText('Cancelled')).toBeInTheDocument();
    expect(posted).toEqual(['register', 'cancel']);
  });

  it('books in the club’s own time, and says a taken slot in the member’s words', async () => {
    signedIn();
    memberApi(() => member());
    let booked: Record<string, unknown> | undefined;
    server.use(
      http.get(`${BASE}/api/portal/resources`, () => HttpResponse.json([
        { id: 'court', name: 'Tennis court', kind: 'Court', capacity: 4, depositAmount: 0, currency: 'CHF', confirmedAtOnce: true },
      ])),
      http.post(`${BASE}/api/portal/reservations`, async ({ request }) => {
        booked = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ detail: 'That time is taken.', errorCode: 'Portal.Taken.Conflict' }, { status: 409 });
      }),
    );
    const user = userEvent.setup();
    open(`/member-portal/${KEY}?tab=bookings`);

    const form = (await screen.findByRole('heading', { name: 'Book' })).closest('section')!;
    fireEvent.change(within(form).getByLabelText('Day'), { target: { value: '2030-12-02' } });
    fireEvent.change(within(form).getByLabelText('From'), { target: { value: '18:00' } });
    fireEvent.change(within(form).getByLabelText('Until'), { target: { value: '19:30' } });
    await user.click(within(form).getByRole('button', { name: 'Book it' }));

    expect(await within(form).findByText('That time is already booked. Choose another time.')).toBeInTheDocument();
    // 18:00 in Zurich in December is 17:00 UTC, wherever the member's browser is.
    expect(booked).toMatchObject({ resourceId: 'court', startAtUtc: '2030-12-02T17:00:00.000Z', endAtUtc: '2030-12-02T18:30:00.000Z', partySize: 1 });
  });

  it('records the member’s own choice of what they hear about, channel by channel', async () => {
    signedIn();
    memberApi(() => member());
    let sent: unknown;
    const state = (sms: string) => ({
      current: [
        { channel: 'email', purpose: 'marketing', state: 'granted' },
        { channel: 'sms', purpose: 'marketing', state: sms },
        { channel: 'post', purpose: 'marketing', state: 'unknown' },
      ],
    });
    server.use(
      http.get(`${BASE}/api/portal/consent`, () => HttpResponse.json(state('withdrawn'))),
      http.put(`${BASE}/api/portal/consent`, async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(state('granted'));
      }),
    );
    const user = userEvent.setup();
    open(`/member-portal/${KEY}?tab=preferences`);

    const sms = await screen.findByRole('checkbox', { name: 'News and offers by text message' });
    expect(sms).not.toBeChecked();
    expect(screen.getByRole('checkbox', { name: 'News and offers by email' })).toBeChecked();
    await user.click(sms);

    await waitFor(() => expect(sent).toEqual({ channel: 'sms', purpose: 'marketing', granted: true }));
    expect(await screen.findByRole('checkbox', { name: 'News and offers by text message' })).toBeChecked();
  });
});

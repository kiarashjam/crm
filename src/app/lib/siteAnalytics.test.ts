import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  enableSiteAnalyticsInTests,
  FLUSH_INTERVAL_MS,
  flushSiteAnalytics,
  installSiteAnalytics,
  LANDING_STORAGE_KEY,
  MAX_BATCH,
  pendingSiteEvents,
  resetSiteAnalytics,
  SESSION_STORAGE_KEY,
  siteAnalyticsEnabled,
  templatePath,
  trackEvent,
  trackPageView,
  type SiteEvent,
} from './siteAnalytics';

const BASE = 'https://api.example.test';
const ENDPOINT = `${BASE}/api/site-analytics/events`;
const GUID = '3fa85f64-5717-4562-b3fc-2c963f66afa6';
const TOKEN = 'Q2hhbmdlTWVQbGVhc2UxMjM0NTY3ODkw';

/** The events each beacon carried. */
function beaconBatches(beacon: ReturnType<typeof vi.fn>): SiteEvent[][] {
  return beacon.mock.calls.map((call) => (JSON.parse(String(call[1])) as { events: SiteEvent[] }).events);
}

function setNavigator(name: string, value: unknown) {
  Object.defineProperty(navigator, name, { value, configurable: true, writable: true });
}

describe('templatePath', () => {
  it.each([
    ['/', '/'],
    [`/leads/${GUID}`, '/leads/:id'],
    [`/deals/${GUID.replace(/-/g, '')}/edit`, '/deals/:id/edit'],
    ['/tasks/1234567', '/tasks/:id'],
    ['/leads?name=Jane%20Doe&section=Leads#top', '/leads'],
    [`/sign/${TOKEN}`, '/sign/:token'],
    ['/unsubscribe/abc', '/unsubscribe/:token'],
    [`/pay/${TOKEN}/done`, '/pay/:token/done'],
    ['/capture/form-key', '/capture/:token'],
    ['/apply/form-key', '/apply/:token'],
    ['/member-portal/pavillon/sign-in', '/member-portal/:key/sign-in'],
    ['/reset-password?token=secret', '/reset-password'],
    ['/people/jane.doe%40example.ch', '/people/:email'],
    [`/files/${TOKEN}`, '/files/:token'],
    ['/blog/crm-for-swiss-clubs-2026', '/blog/crm-for-swiss-clubs-2026'],
    ['/pricing', '/pricing'],
  ])('%s → %s', (raw, templated) => {
    expect(templatePath(raw)).toBe(templated);
  });
});

describe('siteAnalytics', () => {
  let beacon: ReturnType<typeof vi.fn>;
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    vi.useFakeTimers();
    resetSiteAnalytics();
    enableSiteAnalyticsInTests();
    localStorage.clear();
    sessionStorage.clear();
    vi.stubEnv('VITE_API_URL', BASE);
    beacon = vi.fn(() => true);
    setNavigator('sendBeacon', beacon);
    setNavigator('doNotTrack', null);
    setNavigator('globalPrivacyControl', undefined);
    setNavigator('webdriver', false);
    fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 204 })));
    vi.stubGlobal('fetch', fetchMock);
    window.history.pushState({}, '', '/');
    document.title = 'CadenceE';
  });

  afterEach(() => {
    resetSiteAnalytics();
    vi.useRealTimers();
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    localStorage.clear();
    sessionStorage.clear();
    Object.defineProperty(document, 'referrer', { value: '', configurable: true });
    document.body.innerHTML = '';
  });

  it('is off in tests unless a test turns it on', () => {
    resetSiteAnalytics();
    expect(siteAnalyticsEnabled()).toBe(false);
    trackEvent('login');
    expect(pendingSiteEvents()).toHaveLength(0);
    enableSiteAnalyticsInTests();
    expect(siteAnalyticsEnabled()).toBe(true);
  });

  it.each([
    ['Do Not Track', () => setNavigator('doNotTrack', '1')],
    ['Global Privacy Control', () => setNavigator('globalPrivacyControl', true)],
    ['automation', () => setNavigator('webdriver', true)],
    ['a build with no API', () => vi.stubEnv('VITE_API_URL', '')],
    ['the demo', () => localStorage.setItem('aci_user', JSON.stringify({ id: 'demo', name: 'Demo', email: 'demo@example.com' }))],
  ])('records and sends nothing under %s', (_, turnOff) => {
    turnOff();
    installSiteAnalytics();
    trackPageView('/pricing');
    trackEvent('signup_started');
    vi.advanceTimersByTime(FLUSH_INTERVAL_MS * 2);
    document.dispatchEvent(new Event('visibilitychange'));
    window.dispatchEvent(new Event('pagehide'));

    expect(siteAnalyticsEnabled()).toBe(false);
    expect(pendingSiteEvents()).toHaveLength(0);
    expect(beacon).not.toHaveBeenCalled();
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('sends a page view a tick later with the templated path, the title, the session and where the visit came from', () => {
    Object.defineProperty(document, 'referrer', { value: 'https://www.google.com/search?q=jane+doe+crm', configurable: true });
    window.history.pushState({}, '', '/pricing?utm_source=newsletter&utm_medium=email&utm_campaign=launch&ref=x');
    trackPageView();
    document.title = 'Pricing — CadenceE';
    expect(pendingSiteEvents()).toHaveLength(0);

    vi.advanceTimersByTime(0);
    flushSiteAnalytics();

    const [batch] = beaconBatches(beacon);
    expect(beacon).toHaveBeenCalledWith(ENDPOINT, expect.any(String));
    const event = batch?.[0];
    expect(event).toMatchObject({
      type: 'page_view',
      path: '/pricing',
      title: 'Pricing — CadenceE',
      referrer: 'https://www.google.com',
      utm: { source: 'newsletter', medium: 'email', campaign: 'launch' },
      signedIn: false,
    });
    expect(event?.sessionId).toMatch(/^[a-f0-9]{32}$/);
    expect(event?.lang).toBeTruthy();
    expect(Date.parse(event?.at ?? '')).not.toBeNaN();
    // The session lives in the tab alone; nothing in localStorage.
    expect(sessionStorage.getItem(SESSION_STORAGE_KEY)).toBe(event?.sessionId);
    expect(sessionStorage.getItem(LANDING_STORAGE_KEY)).toContain('newsletter');
    expect(localStorage.length).toBe(0);
    // The referrer's search never leaves the browser.
    expect(String(beacon.mock.calls[0]?.[1])).not.toContain('jane');
  });

  it('keeps the landing attribution for every event of the visit, and leaves out a referrer from this site', () => {
    Object.defineProperty(document, 'referrer', { value: `${location.origin}/pricing`, configurable: true });
    trackEvent('signup_started');
    window.history.pushState({}, '', '/login?utm_source=late');
    trackEvent('login');
    flushSiteAnalytics();

    const [batch] = beaconBatches(beacon);
    expect(batch).toHaveLength(2);
    expect(batch?.every((e) => e.referrer === undefined && e.utm === undefined)).toBe(true);
    expect(new Set(batch?.map((e) => e.sessionId)).size).toBe(1);
  });

  it('reads where the visit landed as it starts, so a redirect that drops the utm tags loses nothing', () => {
    window.history.pushState({}, '', '/?utm_source=linkedin&utm_campaign=autumn');
    installSiteAnalytics();
    window.history.replaceState({}, '', '/login');
    trackPageView();
    vi.advanceTimersByTime(0);
    flushSiteAnalytics();

    expect(beaconBatches(beacon)[0]?.[0]).toMatchObject({ path: '/login', utm: { source: 'linkedin', campaign: 'autumn' } });
  });

  it('marks a signed-in visitor as signed in, and sends no title then: the app puts record names in it', () => {
    localStorage.setItem('aci_token', 'token');
    localStorage.setItem('aci_user', JSON.stringify({ id: 'u1', name: 'Jane Doe', email: 'jane@example.ch' }));
    document.title = 'Jane Doe » Leads » CadenceE';
    trackPageView(`/leads/${GUID}`);
    vi.advanceTimersByTime(0);
    flushSiteAnalytics();

    const event = beaconBatches(beacon)[0]?.[0];
    expect(event).toMatchObject({ type: 'page_view', path: '/leads/:id', signedIn: true });
    expect(event?.title).toBeUndefined();
    const body = String(beacon.mock.calls[0]?.[1]);
    expect(body).not.toContain('Jane');
    expect(body).not.toContain('u1');
    expect(body).not.toContain('jane@example.ch');
  });

  it('batches: nothing goes until five seconds pass, the tab is hidden or the page goes', () => {
    installSiteAnalytics();
    trackEvent('one');
    trackEvent('two');
    expect(beacon).not.toHaveBeenCalled();

    vi.advanceTimersByTime(FLUSH_INTERVAL_MS);
    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beaconBatches(beacon)[0]?.map((e) => e.name)).toEqual(['one', 'two']);

    trackEvent('three');
    Object.defineProperty(document, 'visibilityState', { value: 'hidden', configurable: true });
    document.dispatchEvent(new Event('visibilitychange'));
    expect(beacon).toHaveBeenCalledTimes(2);
    Object.defineProperty(document, 'visibilityState', { value: 'visible', configurable: true });

    trackEvent('four');
    window.dispatchEvent(new Event('pagehide'));
    expect(beacon).toHaveBeenCalledTimes(3);
    expect(beaconBatches(beacon).map((b) => b.map((e) => e.name))).toEqual([['one', 'two'], ['three'], ['four']]);

    // Nothing queued: nothing sent.
    vi.advanceTimersByTime(FLUSH_INTERVAL_MS * 3);
    expect(beacon).toHaveBeenCalledTimes(3);
  });

  it(`sends at once when ${MAX_BATCH} events are waiting, never more than ${MAX_BATCH} in one post`, () => {
    for (let i = 0; i < MAX_BATCH; i++) trackEvent(`e${i}`);
    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beaconBatches(beacon)[0]).toHaveLength(MAX_BATCH);
  });

  it('falls back to a keepalive fetch, as text/plain without credentials, when the beacon is refused', () => {
    beacon.mockReturnValue(false);
    trackEvent('login');
    flushSiteAnalytics();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(ENDPOINT);
    expect(init).toMatchObject({ method: 'POST', keepalive: true, credentials: 'omit', headers: { 'Content-Type': 'text/plain;charset=UTF-8' } });
    expect(JSON.parse(String(init.body)).events[0]).toMatchObject({ type: 'event', name: 'login' });
  });

  it('counts a click on a data-track element by its name, on the page it is on', () => {
    installSiteAnalytics();
    window.history.pushState({}, '', `/deals/${GUID}`);
    document.body.innerHTML = '<a href="/login" data-track="cta-signup"><span id="inner">Start free</span></a><button id="plain">Plain</button>';

    document.getElementById('inner')!.click();
    document.getElementById('plain')!.click();
    flushSiteAnalytics();

    const batch = beaconBatches(beacon)[0];
    expect(batch).toHaveLength(1);
    expect(batch?.[0]).toMatchObject({ type: 'click', name: 'cta-signup', path: '/deals/:id' });
  });

  it('counts a click on a link to another site as outbound, with its host, and sends it at once', () => {
    installSiteAnalytics();
    document.body.innerHTML = '<a id="out" href="https://www.linkedin.com/company/cadencee?trk=abc">LinkedIn</a><a id="in" href="/pricing">Pricing</a>';

    document.getElementById('in')!.click();
    expect(beacon).not.toHaveBeenCalled();
    document.getElementById('out')!.click();

    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beaconBatches(beacon)[0]?.[0]).toMatchObject({ type: 'click', name: 'outbound', path: 'www.linkedin.com' });
    expect(String(beacon.mock.calls[0]?.[1])).not.toContain('trk=abc');
  });

  it('trackEvent records an event on the current page, or the page it is given', () => {
    window.history.pushState({}, '', '/login?next=/leads');
    trackEvent('signup_completed');
    trackEvent('signup_started', { path: `/sign/${TOKEN}` });
    flushSiteAnalytics();

    const batch = beaconBatches(beacon)[0];
    expect(batch?.[0]).toMatchObject({ type: 'event', name: 'signup_completed', path: '/login' });
    expect(batch?.[1]).toMatchObject({ type: 'event', name: 'signup_started', path: '/sign/:token' });
    expect(String(beacon.mock.calls[0]?.[1])).not.toContain(TOKEN);
  });

  it('does not count the analytics dashboard itself', () => {
    trackPageView('/admin/analytics');
    window.history.pushState({}, '', '/admin/analytics');
    trackEvent('refresh');
    vi.advanceTimersByTime(0);
    flushSiteAnalytics();
    expect(beacon).not.toHaveBeenCalled();
  });

  it('never throws, whatever the browser does', () => {
    beacon.mockImplementation(() => {
      throw new Error('beacon broke');
    });
    fetchMock.mockImplementation(() => {
      throw new Error('fetch broke');
    });
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('storage blocked');
    });

    expect(() => {
      installSiteAnalytics();
      trackPageView();
      vi.advanceTimersByTime(0);
      trackEvent('login');
      trackEvent('');
      // @ts-expect-error junk from a caller
      trackEvent(null, 42);
      templatePath(undefined as unknown as string);
      document.body.innerHTML = '<a id="out" href="https://example.org">x</a>';
      document.getElementById('out')!.click();
      flushSiteAnalytics();
      window.dispatchEvent(new Event('pagehide'));
    }).not.toThrow();
    vi.restoreAllMocks();
  });
});

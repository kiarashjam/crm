/**
 * First-party, cookieless visitor analytics of cadencee.ai: what pages people open, the tracked
 * buttons and outbound links they click, and a few events the app reports (a sign-up, a sign-in),
 * posted in batches to POST /api/site-analytics/events. Platform admins read the figures back.
 *
 * Nothing that could name or follow someone leaves the browser:
 * - no cookie, and nothing in localStorage: the session is a random id in sessionStorage, which
 *   dies with the tab and only groups one visit's page views (the server keeps a hash of it);
 * - a path goes as its route's template (/leads/:id), without its query string or fragment, and
 *   the secret in a signing, unsubscribe or pay link (and any other token) is never sent;
 * - the referrer goes as its scheme and host only, and only when it is another site;
 * - a page's title goes on public pages only (the app's own put the open record's name in it);
 * - a signed-in visitor is only "signed in": no id, workspace, name or email.
 *
 * It does nothing when the browser asks not to be tracked (Do Not Track or Global Privacy
 * Control), in the demo, in a build with no API, under automation (navigator.webdriver), and in
 * tests unless a test turns it on. It never throws: counting a visit must never break the page.
 *
 * Mark a button to count its clicks with data-track="<event-name>", e.g.
 * `<a href="/login?register=1" data-track="cta-signup">`. Clicks on links to other sites are
 * counted as "outbound", with that site's host.
 */
import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { getApiBaseUrl } from '@/app/api/apiClient';
import { getAuthToken, getCurrentUser, isDemoMode } from '@/app/lib/auth';

/** How often the queue is sent while the page is open. */
export const FLUSH_INTERVAL_MS = 5000;

/** Events in one post: the server takes at most 25. */
export const MAX_BATCH = 25;

/** A post stays under the server's 32 KB, with room to spare. */
const MAX_BODY_CHARS = 30_000;

/** Events kept while nothing can be sent; the oldest go first. */
const MAX_QUEUE = 200;

/** The tab's random session id, in sessionStorage: gone with the tab. */
export const SESSION_STORAGE_KEY = 'cadence.analytics.session';

/** Where the visit came from (referring site and utm_* tags), kept for the tab's session. */
export const LANDING_STORAGE_KEY = 'cadence.analytics.landing';

/** The app's routes whose second segment is a link's secret or a form's key (App.tsx). */
const ROUTE_TEMPLATES: Record<string, string> = {
  sign: ':token',
  unsubscribe: ':token',
  pay: ':token',
  capture: ':token',
  apply: ':token',
  'member-portal': ':key',
};

/** Pages not counted: the analytics dashboard itself. */
const UNTRACKED_PREFIXES = ['/admin'];

const LIMITS = { path: 300, title: 200, name: 64, utm: 100, referrer: 300, segment: 80, segments: 12 };

const UTM_KEYS = ['source', 'medium', 'campaign', 'term', 'content'] as const;
type UtmKey = (typeof UTM_KEYS)[number];
export type Utm = Partial<Record<UtmKey, string>>;

export type SiteEventType = 'page_view' | 'click' | 'event';

/** One event as it is posted. */
export interface SiteEvent {
  type: SiteEventType;
  name?: string;
  path: string;
  title?: string;
  referrer?: string;
  utm?: Utm;
  sessionId: string;
  at: string;
  screen?: { w: number; h: number };
  lang?: string;
  signedIn: boolean;
}

/** Where a trackEvent happened, when not the current page. Nothing else is sent: free-form properties are where personal data leaks in. */
export interface TrackEventProps {
  path?: string;
  title?: string;
}

interface Landing {
  referrer?: string;
  utm?: Utm;
}

let queue: SiteEvent[] = [];
let installed = false;
let enabledInTests = false;
let timer: ReturnType<typeof setInterval> | null = null;
let memorySessionId: string | null = null;
let memoryLanding: Landing | null = null;
let listeners: Array<() => void> = [];

// ── Paths ──────────────────────────────────────────────────────────────────

const GUID = /^[0-9a-f]{8}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{4}-?[0-9a-f]{12}$/i;
const LONG_NUMBER = /^\d{4,}$/;
const LONG_HEX = /^[0-9a-f]{16,}$/i;
const TOKEN_CHARACTERS = /^[A-Za-z0-9_\-.~=+]{20,}$/;
const TEMPLATE = /^:[a-z]{1,16}$/;

/**
 * A path as its route's template: no query string or fragment; a GUID, a long number or a long
 * hex string is ":id"; the variable segment of a signing, unsubscribe, pay, capture, application
 * or member portal link is ":token" (":key" for the portal); anything shaped like a token is
 * ":token"; anything with an @ is ":email". The server does the same again (SiteTraffic).
 */
export function templatePath(raw: string): string {
  try {
    let path = String(raw ?? '');
    const cut = path.search(/[?#]/);
    if (cut >= 0) path = path.slice(0, cut);
    const segments = path.split('/').filter(Boolean).slice(0, LIMITS.segments);
    const first = segments[0]?.toLowerCase() ?? '';
    const parts = segments.map((segment, index) =>
      index === 1 && ROUTE_TEMPLATES[first] ? ROUTE_TEMPLATES[first]! : templateSegment(segment),
    );
    return clip(`/${parts.join('/')}`, LIMITS.path) ?? '/';
  } catch {
    return '/';
  }
}

function templateSegment(segment: string): string {
  if (TEMPLATE.test(segment)) return segment;
  let decoded = segment;
  try {
    decoded = decodeURIComponent(segment);
  } catch {
    // Kept as it came.
  }
  if (decoded.includes('@')) return ':email';
  if (GUID.test(decoded) || LONG_NUMBER.test(decoded) || LONG_HEX.test(decoded)) return ':id';
  if (decoded.length > LIMITS.segment || looksLikeToken(decoded)) return ':token';
  return segment;
}

/** Twenty or more token characters with letters and digits, and both cases or no hyphen: a token, not a slug. */
function looksLikeToken(segment: string): boolean {
  if (!TOKEN_CHARACTERS.test(segment)) return false;
  const digit = /\d/.test(segment);
  const upper = /[A-Z]/.test(segment);
  const lower = /[a-z]/.test(segment);
  return digit && (upper || lower) && ((upper && lower) || !segment.includes('-'));
}

// ── Switches ───────────────────────────────────────────────────────────────

/** Turns the tracker on in tests (it is off in tests otherwise), or off again. */
export function enableSiteAnalyticsInTests(on = true): void {
  enabledInTests = on;
}

/** Whether visits are counted in this browser, now. */
export function siteAnalyticsEnabled(): boolean {
  try {
    if (typeof window === 'undefined' || typeof navigator === 'undefined' || typeof document === 'undefined') return false;
    if (import.meta.env.MODE === 'test' && !enabledInTests) return false;
    if (!getApiBaseUrl() || isDemoMode()) return false;
    const nav = navigator as Navigator & { globalPrivacyControl?: boolean };
    const win = window as Window & { doNotTrack?: string };
    if (nav.doNotTrack === '1' || win.doNotTrack === '1') return false;
    if (nav.globalPrivacyControl === true) return false;
    if (nav.webdriver === true) return false;
    return true;
  } catch {
    return false;
  }
}

function signedIn(): boolean {
  try {
    return Boolean(getAuthToken()) && getCurrentUser() !== null && !isDemoMode();
  } catch {
    return false;
  }
}

function untracked(path: string): boolean {
  return UNTRACKED_PREFIXES.some((prefix) => path === prefix || path.startsWith(`${prefix}/`));
}

// ── The session and where it came from ────────────────────────────────────

function randomId(): string {
  try {
    const bytes = new Uint8Array(16);
    crypto.getRandomValues(bytes);
    return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  } catch {
    return `${Date.now().toString(36)}${Math.random().toString(36).slice(2, 14)}`.replace(/[^a-z0-9]/g, '').padEnd(16, '0');
  }
}

function sessionId(): string {
  try {
    const existing = sessionStorage.getItem(SESSION_STORAGE_KEY);
    if (existing && /^[A-Za-z0-9_-]{8,64}$/.test(existing)) return existing;
    const id = randomId();
    sessionStorage.setItem(SESSION_STORAGE_KEY, id);
    return id;
  } catch {
    memorySessionId ??= randomId();
    return memorySessionId;
  }
}

/** The referring site as scheme and host, when it is another site; its path never leaves. */
function referrerOrigin(): string | undefined {
  try {
    if (!document.referrer) return undefined;
    const url = new URL(document.referrer);
    if (url.host === location.host) return undefined;
    if (!['http:', 'https:', 'android-app:'].includes(url.protocol) || !url.host) return undefined;
    return clip(`${url.protocol}//${url.host}`, LIMITS.referrer);
  } catch {
    return undefined;
  }
}

function utmTags(): Utm | undefined {
  try {
    const params = new URLSearchParams(location.search);
    const utm: Utm = {};
    for (const key of UTM_KEYS) {
      const value = clip(params.get(`utm_${key}`)?.trim(), LIMITS.utm);
      if (value) utm[key] = value;
    }
    return Object.keys(utm).length > 0 ? utm : undefined;
  } catch {
    return undefined;
  }
}

/**
 * Where this tab's visit came from, read once as it lands and kept for the session, so every
 * event of the visit carries it (the server counts sources by session).
 */
function landing(): Landing {
  try {
    const kept = sessionStorage.getItem(LANDING_STORAGE_KEY);
    if (kept) return JSON.parse(kept) as Landing;
    const fresh: Landing = { referrer: referrerOrigin(), utm: utmTags() };
    sessionStorage.setItem(LANDING_STORAGE_KEY, JSON.stringify(fresh));
    return fresh;
  } catch {
    memoryLanding ??= { referrer: referrerOrigin(), utm: utmTags() };
    return memoryLanding;
  }
}

// ── Recording ──────────────────────────────────────────────────────────────

function currentPath(): string {
  try {
    return templatePath(location.pathname);
  } catch {
    return '/';
  }
}

function makeEvent(type: SiteEventType, path: string, extra: { name?: string; title?: string } = {}): SiteEvent {
  const isSignedIn = signedIn();
  const from = landing();
  const event: SiteEvent = { type, path, sessionId: sessionId(), at: new Date().toISOString(), signedIn: isSignedIn };
  if (extra.name) event.name = clip(extra.name, LIMITS.name);
  // A title only on a public page: in the app it carries the open record's name.
  if (extra.title && !isSignedIn && !path.includes(':')) event.title = clip(extra.title.trim(), LIMITS.title);
  if (from.referrer) event.referrer = from.referrer;
  if (from.utm) event.utm = from.utm;
  try {
    if (typeof screen !== 'undefined' && screen.width > 0) event.screen = { w: screen.width, h: screen.height };
    if (navigator.language) event.lang = navigator.language;
  } catch {
    // Left out.
  }
  return event;
}

function enqueue(event: SiteEvent): void {
  if (untracked(event.type === 'click' && event.name === 'outbound' ? currentPath() : event.path)) return;
  ensureInstalled();
  queue.push(event);
  if (queue.length > MAX_QUEUE) queue = queue.slice(queue.length - MAX_QUEUE);
  if (queue.length >= MAX_BATCH) flushSiteAnalytics();
}

/** Counts a page view of `pathname` (the current page by default), a tick later so the page has set its title. */
export function trackPageView(pathname?: string): void {
  try {
    if (!siteAnalyticsEnabled()) return;
    const path = templatePath(pathname ?? location.pathname);
    // Where the visit came from is read now, before a redirect can drop the utm_* tags.
    landing();
    setTimeout(() => {
      try {
        if (!siteAnalyticsEnabled()) return;
        enqueue(makeEvent('page_view', path, { title: document.title }));
      } catch {
        // Never breaks the page.
      }
    }, 0);
  } catch {
    // Never breaks the page.
  }
}

/**
 * Counts something the app did: 'signup_started', 'signup_completed', 'login'. The name is a word
 * of letters, digits and _ . : -; where it happened is the current page unless props say.
 */
export function trackEvent(name: string, props?: TrackEventProps): void {
  try {
    if (!siteAnalyticsEnabled() || !name) return;
    const path = props?.path ? templatePath(props.path) : currentPath();
    enqueue(makeEvent('event', path, { name, title: props?.title }));
  } catch {
    // Never breaks the page.
  }
}

/** A click: a tracked element (data-track) by its name, else a link to another site as "outbound". */
function onClick(event: MouseEvent): void {
  try {
    if (!siteAnalyticsEnabled()) return;
    const target = event.target instanceof Element ? event.target : null;
    if (!target) return;
    const tracked = target.closest('[data-track]');
    const name = tracked?.getAttribute('data-track')?.trim();
    if (name) {
      enqueue(makeEvent('click', currentPath(), { name }));
      return;
    }
    const link = target.closest('a[href]');
    if (!(link instanceof HTMLAnchorElement)) return;
    const url = new URL(link.href, location.href);
    if ((url.protocol !== 'http:' && url.protocol !== 'https:') || url.host === location.host) return;
    enqueue(makeEvent('click', url.hostname.toLowerCase(), { name: 'outbound' }));
    // The page may be about to go: send it now.
    flushSiteAnalytics();
  } catch {
    // Never breaks the page.
  }
}

// ── Sending ────────────────────────────────────────────────────────────────

/** Sends what is queued: a beacon when the browser takes one, else a fetch that outlives the page. */
export function flushSiteAnalytics(): void {
  try {
    if (queue.length === 0) return;
    const base = getApiBaseUrl();
    if (!base || !siteAnalyticsEnabled()) {
      queue = [];
      return;
    }
    const url = `${base}/api/site-analytics/events`;
    const pending = queue;
    queue = [];
    for (let i = 0; i < pending.length; i += MAX_BATCH) sendBatch(url, pending.slice(i, i + MAX_BATCH));
  } catch {
    // Never breaks the page.
  }
}

function sendBatch(url: string, events: SiteEvent[]): void {
  const body = JSON.stringify({ events });
  if (body.length > MAX_BODY_CHARS && events.length > 1) {
    const half = Math.ceil(events.length / 2);
    sendBatch(url, events.slice(0, half));
    sendBatch(url, events.slice(half));
    return;
  }
  send(url, body);
}

/**
 * As text/plain, so it needs no preflight to reach the API on its own origin, and carries no
 * cookie or token: the beacon (which outlives the page), else a keepalive fetch.
 */
function send(url: string, body: string): void {
  try {
    if (typeof navigator.sendBeacon === 'function' && navigator.sendBeacon(url, body)) return;
  } catch {
    // Try the fetch instead.
  }
  try {
    void fetch(url, {
      method: 'POST',
      body,
      keepalive: true,
      credentials: 'omit',
      headers: { 'Content-Type': 'text/plain;charset=UTF-8' },
    }).catch(() => undefined);
  } catch {
    // Nothing more to try.
  }
}

// ── Installing ─────────────────────────────────────────────────────────────

function ensureInstalled(): void {
  if (installed) return;
  installed = true;
  const onVisibility = () => {
    if (document.visibilityState === 'hidden') flushSiteAnalytics();
  };
  const onPageHide = () => flushSiteAnalytics();
  document.addEventListener('click', onClick, true);
  document.addEventListener('visibilitychange', onVisibility);
  window.addEventListener('pagehide', onPageHide);
  timer = setInterval(flushSiteAnalytics, FLUSH_INTERVAL_MS);
  listeners = [
    () => document.removeEventListener('click', onClick, true),
    () => document.removeEventListener('visibilitychange', onVisibility),
    () => window.removeEventListener('pagehide', onPageHide),
  ];
}

/**
 * Starts counting clicks and sending batches: every five seconds, when the tab is hidden and
 * when the page goes. Once, however often it is called; nothing at all when the tracker is off.
 */
export function installSiteAnalytics(): void {
  try {
    if (!siteAnalyticsEnabled()) return;
    // The address the visit landed on, before the app redirects anywhere.
    landing();
    ensureInstalled();
  } catch {
    // Never breaks the page.
  }
}

/** Forgets the queue, the listeners and the test switch (tests). */
export function resetSiteAnalytics(): void {
  for (const remove of listeners) remove();
  listeners = [];
  if (timer) clearInterval(timer);
  timer = null;
  queue = [];
  installed = false;
  enabledInTests = false;
  memorySessionId = null;
  memoryLanding = null;
}

/** What is waiting to be sent (tests). */
export function pendingSiteEvents(): readonly SiteEvent[] {
  return queue;
}

/** Counts a page view on every route change. Rendered once, inside the router (App.tsx). */
export function SiteAnalyticsPageViews(): null {
  const { pathname } = useLocation();
  useEffect(() => {
    trackPageView(pathname);
  }, [pathname]);
  return null;
}

function clip(text: string | undefined | null, max: number): string | undefined {
  if (!text) return undefined;
  return text.length <= max ? text : text.slice(0, max);
}

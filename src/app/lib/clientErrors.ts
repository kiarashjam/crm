/**
 * Crashes in the browser, sent to the server's log (POST /api/client-errors) instead of staying
 * in the visitor's console: what the app's error boundary catches, and the window's uncaught
 * errors and unhandled promise rejections. The server logs each as one warning,
 * "monitoring.browser.error", which Application Insights keeps.
 *
 * At most ten a page load, each message and stack once. Nothing in the demo, which has no
 * account, or in a build with no API. The page's address goes without its query string and
 * fragment (where reset and sign-in tokens travel); the server takes the token out of a
 * signing or pay link's path. It never throws, whatever it is given: a crash report must not
 * cause another crash.
 */
import { getApiBaseUrl } from '@/app/api/apiClient';
import { isDemoMode } from '@/app/lib/auth';

/** Reports one page load sends at most. */
export const MAX_REPORTS_PER_PAGE = 10;

/** The server's own caps (ClientErrorsController), so a report is never refused for its size. */
const LIMITS = { message: 1000, stack: 4000, componentStack: 4000, url: 2000, userAgent: 512, release: 100 };

/**
 * Errors that say nothing about the app: the browser's own resize-loop notice, a script from
 * another origin that hides its error, and a request the app cancelled itself (a list's read
 * given up when the search changed).
 */
const IGNORED = [/^(?:\w+: )?ResizeObserver loop/i, /^Script error\.?$/i, /^AbortError\b/];

let seen = new Set<string>();
let sent = 0;
let installed = false;

export interface ClientErrorDetails {
  /** React's component stack, from the error boundary. */
  componentStack?: string | null;
}

/** Sends one crash to the server, unless it was sent already, ten have gone, or there is nowhere to send it. */
export function reportClientError(error: unknown, details: ClientErrorDetails = {}): void {
  try {
    if (sent >= MAX_REPORTS_PER_PAGE) return;
    const base = getApiBaseUrl();
    if (!base || isDemoMode()) return;

    const { message, stack } = describe(error);
    if (!message || IGNORED.some((pattern) => pattern.test(message))) return;
    const key = `${message}\n${stack ?? ''}`;
    if (seen.has(key)) return;
    seen.add(key);
    sent += 1;

    const body = JSON.stringify({
      message: clip(message, LIMITS.message),
      stack: clip(stack, LIMITS.stack),
      url: clip(pageAddress(), LIMITS.url),
      componentStack: clip(details.componentStack ?? undefined, LIMITS.componentStack),
      userAgent: clip(typeof navigator === 'undefined' ? undefined : navigator.userAgent, LIMITS.userAgent),
      release: clip(release(), LIMITS.release),
    });
    send(`${base}/api/client-errors`, body);
  } catch {
    // A crash report must never cause another.
  }
}

/** Reports the window's uncaught errors and unhandled rejections; once, however often it is called. */
export function installClientErrorReporting(target: Window = window): void {
  if (installed) return;
  installed = true;
  target.addEventListener('error', (event: ErrorEvent) => reportClientError(event.error ?? event.message));
  target.addEventListener('unhandledrejection', (event: PromiseRejectionEvent) => reportClientError(event.reason));
}

/** Forgets what was sent and the listeners installed (tests). */
export function resetClientErrorReporting(): void {
  seen = new Set<string>();
  sent = 0;
  installed = false;
}

function describe(error: unknown): { message: string; stack?: string } {
  if (error instanceof Error) {
    const name = error.name || 'Error';
    const text = typeof error.message === 'string' ? error.message : '';
    return { message: text ? `${name}: ${text}` : name, stack: typeof error.stack === 'string' ? error.stack : undefined };
  }
  if (typeof error === 'string') return { message: error };
  if (error == null) return { message: '' };
  try {
    return { message: JSON.stringify(error) ?? String(error) };
  } catch {
    return { message: Object.prototype.toString.call(error) };
  }
}

/** The page, without the query string or fragment. */
function pageAddress(): string | undefined {
  if (typeof location === 'undefined') return undefined;
  return `${location.origin}${location.pathname}`;
}

function release(): string | undefined {
  const value: unknown = import.meta.env.VITE_RELEASE;
  return typeof value === 'string' && value.trim() ? value.trim() : undefined;
}

function clip(text: string | undefined, max: number): string | undefined {
  if (!text) return undefined;
  return text.length <= max ? text : text.slice(0, max);
}

/**
 * A beacon when the browser has one: it outlives the page, and as text/plain it needs no
 * preflight to reach the API on its own origin. Otherwise a fetch that also outlives the page.
 */
function send(url: string, body: string): void {
  try {
    if (typeof navigator !== 'undefined' && typeof navigator.sendBeacon === 'function' && navigator.sendBeacon(url, body)) return;
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

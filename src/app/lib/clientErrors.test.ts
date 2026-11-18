import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
  installClientErrorReporting,
  MAX_REPORTS_PER_PAGE,
  reportClientError,
  resetClientErrorReporting,
} from './clientErrors';

const BASE = 'https://api.example.test';

/** What the reporter handed the browser: the beacons, or failing those the fetches. */
function sentBodies(beacon: ReturnType<typeof vi.fn>): Array<Record<string, string>> {
  return beacon.mock.calls.map((call) => JSON.parse(String(call[1])) as Record<string, string>);
}

describe('clientErrors', () => {
  let beacon: ReturnType<typeof vi.fn>;
  let fetchMock: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    resetClientErrorReporting();
    localStorage.clear();
    vi.stubEnv('VITE_API_URL', BASE);
    beacon = vi.fn(() => true);
    Object.defineProperty(navigator, 'sendBeacon', { value: beacon, configurable: true, writable: true });
    fetchMock = vi.fn(() => Promise.resolve(new Response(null, { status: 204 })));
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllEnvs();
    vi.unstubAllGlobals();
    localStorage.clear();
  });

  it('sends one report to the API with the message, stack, page and component stack', () => {
    const error = new TypeError("Cannot read properties of undefined (reading 'name')");

    reportClientError(error, { componentStack: '\n    at LeadCard\n    at App' });

    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beacon).toHaveBeenCalledWith(`${BASE}/api/client-errors`, expect.any(String));
    const body = sentBodies(beacon)[0] ?? {};
    expect(body.message).toBe("TypeError: Cannot read properties of undefined (reading 'name')");
    expect(body.stack).toBe(error.stack);
    expect(body.componentStack).toBe('\n    at LeadCard\n    at App');
    expect(body.url).toBe(`${location.origin}${location.pathname}`);
    expect(body.url).not.toContain('?');
    expect(typeof body.userAgent).toBe('string');
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it('sends the same message and stack once', () => {
    const error = new Error('Boom');

    reportClientError(error);
    reportClientError(error);
    reportClientError(error, { componentStack: 'a different tree' });

    expect(beacon).toHaveBeenCalledTimes(1);

    const elsewhere = new Error('Boom');
    elsewhere.stack = 'Error: Boom\n    at somewhere else';
    reportClientError(elsewhere);
    expect(beacon).toHaveBeenCalledTimes(2);
  });

  it(`sends at most ${MAX_REPORTS_PER_PAGE} a page load`, () => {
    for (let i = 0; i < 25; i++) reportClientError(new Error(`Error ${i}`));

    expect(beacon).toHaveBeenCalledTimes(MAX_REPORTS_PER_PAGE);
  });

  it('sends nothing without an API or in the demo', () => {
    vi.stubEnv('VITE_API_URL', '');
    reportClientError(new Error('No API'));
    expect(beacon).not.toHaveBeenCalled();

    vi.stubEnv('VITE_API_URL', BASE);
    localStorage.setItem('aci_user', JSON.stringify({ id: 'demo', name: 'Demo', email: 'demo@example.com' }));
    reportClientError(new Error('In the demo'));
    expect(beacon).not.toHaveBeenCalled();
  });

  it('leaves out what says nothing about the app', () => {
    reportClientError(new Error('ResizeObserver loop completed with undelivered notifications.'));
    reportClientError('Script error.');
    reportClientError(new DOMException('The user aborted a request.', 'AbortError'));
    reportClientError(null);
    reportClientError(undefined);

    expect(beacon).not.toHaveBeenCalled();
  });

  it('falls back to a fetch that outlives the page when there is no beacon', () => {
    beacon.mockReturnValue(false);

    reportClientError(new Error('Queued beacon refused'));

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit];
    expect(url).toBe(`${BASE}/api/client-errors`);
    expect(init).toMatchObject({ method: 'POST', keepalive: true, credentials: 'omit' });
    expect(JSON.parse(String(init.body)).message).toBe('Error: Queued beacon refused');
  });

  it('never throws, whatever it is given and however sending fails', async () => {
    beacon.mockImplementation(() => {
      throw new Error('beacon broke');
    });
    fetchMock.mockImplementation(() => Promise.reject(new Error('offline')));
    const circular: Record<string, unknown> = { kind: 'circular' };
    circular.self = circular;
    const hostile = {
      get message(): string {
        throw new Error('getter throws');
      },
    };

    expect(() => reportClientError(new Error('Sent anyway'))).not.toThrow();
    expect(() => reportClientError(circular)).not.toThrow();
    expect(() => reportClientError(hostile)).not.toThrow();
    expect(() => reportClientError(42)).not.toThrow();
    expect(() => reportClientError({ reason: 'plain object' })).not.toThrow();

    fetchMock.mockImplementation(() => {
      throw new TypeError('fetch is broken');
    });
    expect(() => reportClientError(new Error('Fetch throws at once'))).not.toThrow();
    // A rejected fetch is caught, not left unhandled.
    await Promise.resolve();
    expect(fetchMock).toHaveBeenCalled();
  });

  it('reports the window\'s uncaught errors and unhandled rejections, installed once', () => {
    installClientErrorReporting();
    installClientErrorReporting();

    window.dispatchEvent(new ErrorEvent('error', { error: new RangeError('Out of range'), message: 'Out of range' }));
    const rejection = Object.assign(new Event('unhandledrejection'), { reason: new Error('Nobody caught me') });
    window.dispatchEvent(rejection);

    expect(sentBodies(beacon).map((b) => b.message)).toEqual(['RangeError: Out of range', 'Error: Nobody caught me']);
  });
});

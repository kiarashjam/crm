// When `apiWithFallback` may use this browser's copy instead of the server.
//
// It used to fall back on any error at all. A Viewer's 403, a validation error
// or a 500 then turned into a local save that looked like it had worked, so the
// screen said "saved" for a write the server had refused or never seen. The
// rule now is narrow: only the server saying the endpoint doesn't exist (404,
// 405, 501) falls back. These tests pin that rule to real HTTP responses.

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse, type JsonBodyType } from 'msw';
import { server } from '@/test/mocks/server';
import { ApiError, apiWithFallback, authFetch, authFetchJson, ensureOk } from './apiClient';
import { isLocalOnly, resetLocalOnly } from './localOnly';

const BASE = 'http://api.test';

function signIn(userId: string) {
  localStorage.setItem('aci_token', 'token');
  localStorage.setItem('aci_user', JSON.stringify({ id: userId, name: 'Pat', email: 'pat@example.com' }));
}

function respond(status: number, body: JsonBodyType = { title: `HTTP ${status}` }) {
  server.use(http.get(`${BASE}/api/widgets`, () => HttpResponse.json(body, { status })));
}

const real = () => authFetchJson<string>('/api/widgets');
const local = vi.fn(async () => 'local copy');

beforeEach(() => {
  localStorage.clear();
  resetLocalOnly();
  local.mockClear();
  vi.stubEnv('VITE_API_URL', BASE);
  signIn('user-1');
});
afterEach(() => vi.unstubAllEnvs());

describe('apiWithFallback against the real API', () => {
  it('uses the server when it answers', async () => {
    respond(200, 'from server');
    await expect(apiWithFallback(real, local)).resolves.toBe('from server');
    expect(local).not.toHaveBeenCalled();
  });

  // What the backend sends for a route it doesn't have: the status and nothing else.
  function missing(status: number) {
    server.use(http.get(`${BASE}/api/widgets`, () => new HttpResponse(null, { status })));
  }

  it.each([404, 405, 501])('falls back when the endpoint is missing (%i)', async (status) => {
    missing(status);
    await expect(apiWithFallback(real, local)).resolves.toBe('local copy');
  });

  it('rethrows a 404 that explains itself: the record is missing, not the feature', async () => {
    respond(404, { title: 'Not Found', detail: 'The record was not found' });
    const err = await apiWithFallback(real, local).catch((e: unknown) => e);
    expect(err).toMatchObject({ status: 404, message: 'The record was not found' });
    expect(local).not.toHaveBeenCalled();
    expect(isLocalOnly(['widgets'])).toBe(false);
  });

  it('records the feature that fell back, so the page can say so', async () => {
    missing(404);
    await apiWithFallback(real, local);
    expect(isLocalOnly(['widgets'])).toBe(true);
    expect(isLocalOnly(['members'])).toBe(false);
  });

  it.each([
    [403, 'a Viewer saving'],
    [400, 'a validation error'],
    [409, 'a conflict'],
    [422, 'an invalid entity'],
    [500, 'a server error'],
  ])('rethrows %i (%s) instead of saving locally', async (status) => {
    respond(status, { title: 'Refused', detail: 'You have read-only access.' });
    const err = await apiWithFallback(real, local).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect((err as ApiError).status).toBe(status);
    expect(local).not.toHaveBeenCalled();
    expect(isLocalOnly(['widgets'])).toBe(false);
  });

  it('rethrows when the server cannot be reached', async () => {
    server.use(http.get(`${BASE}/api/widgets`, () => HttpResponse.error()));
    await expect(apiWithFallback(real, local)).rejects.toThrow();
    expect(local).not.toHaveBeenCalled();
  });

  it('rethrows an unexpected non-HTTP error', async () => {
    await expect(apiWithFallback(() => Promise.reject(new Error('bug')), local)).rejects.toThrow('bug');
    expect(local).not.toHaveBeenCalled();
  });
});

describe('apiWithFallback with demo data', () => {
  it('stays local, without a request, on a build with no backend', async () => {
    vi.stubEnv('VITE_API_URL', '');
    const realSpy = vi.fn(real);
    await expect(apiWithFallback(realSpy, local)).resolves.toBe('local copy');
    expect(realSpy).not.toHaveBeenCalled();
  });

  it('stays local for the "Explore the demo" user on a real build', async () => {
    localStorage.removeItem('aci_token');
    localStorage.setItem('aci_user', JSON.stringify({ id: 'demo', name: 'Demo User', email: 'demo@example.com' }));
    const realSpy = vi.fn(real);
    await expect(apiWithFallback(realSpy, local)).resolves.toBe('local copy');
    expect(realSpy).not.toHaveBeenCalled();
  });
});

describe('ensureOk', () => {
  it('turns a raw failed response into an ApiError with its status, message and path', async () => {
    server.use(http.delete(`${BASE}/api/widgets/w-1`, () =>
      HttpResponse.json({ title: 'Forbidden', detail: 'Viewers cannot delete.' }, { status: 403 })));
    const res = await authFetch('/api/widgets/w-1', { method: 'DELETE' });
    const err = await ensureOk(res).catch((e: unknown) => e);
    expect(err).toBeInstanceOf(ApiError);
    expect(err).toMatchObject({ status: 403, message: 'Viewers cannot delete.', path: '/api/widgets/w-1' });
  });

  it('passes a successful response through', async () => {
    server.use(http.delete(`${BASE}/api/widgets/w-1`, () => new HttpResponse(null, { status: 204 })));
    const res = await authFetch('/api/widgets/w-1', { method: 'DELETE' });
    await expect(ensureOk(res)).resolves.toBeUndefined();
  });
});

// A workspace that requires single sign-on refuses a password or passkey session's every
// request with 403 Sso.Required. The API client says so once for all of them, naming the
// workspace, so the app can show why; any other refusal is left to the page that asked.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { ApiError, authFetchJson } from './apiClient';
import { SSO_SIGN_IN_NEEDED } from '@/app/lib/auth';

const BASE = 'http://api.test';

let heard: string[] = [];
const listen = (event: Event) => heard.push(String((event as CustomEvent<{ organizationId: string }>).detail.organizationId));

beforeEach(() => {
  heard = [];
  localStorage.clear();
  vi.stubEnv('VITE_API_URL', BASE);
  localStorage.setItem('aci_token', 'password-session');
  window.addEventListener(SSO_SIGN_IN_NEEDED, listen);
});

afterEach(() => {
  window.removeEventListener(SSO_SIGN_IN_NEEDED, listen);
  vi.unstubAllEnvs();
});

describe('a refusal for want of single sign-on', () => {
  it('names the workspace, and the caller still gets the server’s sentence', async () => {
    server.use(http.get(`${BASE}/api/contacts`, () => HttpResponse.json(
      { detail: 'Pavillon 46 asks you to sign in with Google or Microsoft to work here.', errorCode: 'Sso.Required', organizationId: 'org-p46' },
      { status: 403 },
    )));

    const failure = await authFetchJson('/api/contacts').catch((err: unknown) => err);

    expect(failure).toBeInstanceOf(ApiError);
    expect((failure as ApiError).code).toBe('Sso.Required');
    expect((failure as ApiError).message).toBe('Pavillon 46 asks you to sign in with Google or Microsoft to work here.');
    await vi.waitFor(() => expect(heard).toEqual(['org-p46']));
    // The session is not ended: it works in the person's other workspaces.
    expect(localStorage.getItem('aci_token')).toBe('password-session');
  });

  it('is not claimed for any other refusal', async () => {
    server.use(http.get(`${BASE}/api/contacts`, () =>
      HttpResponse.json({ detail: 'You need permission to see contacts.', errorCode: 'Permission.Required' }, { status: 403 })));

    await authFetchJson('/api/contacts').catch(() => undefined);
    await new Promise((resolve) => setTimeout(resolve, 20));

    expect(heard).toEqual([]);
  });
});

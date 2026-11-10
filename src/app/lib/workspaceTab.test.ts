// The workspace belongs to the browser tab.
//
// It used to be one value in localStorage, shared by every tab. An owner with
// Pavillon 46 open in one tab switched a second tab to Lac Léman SA, went back to
// the first, whose header still said Pavillon 46, and added a lead: it was saved
// in Lac Léman. Session storage is per tab; localStorage now only says which
// workspace a new tab opens in.

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { http, HttpResponse } from 'msw';
import { server } from '@/test/mocks/server';
import { authFetch } from '@/app/api/apiClient';
import {
  AUTH_SESSION_CHANGED,
  clearSession,
  getCurrentOrganizationId,
  keepWorkspaceInThisTab,
  offerWorkspaceToNewTabs,
  setCurrentOrganizationId,
  setSession,
  watchSessionInOtherTabs,
} from './auth';

const BASE = 'http://api.test';

/** What another tab does when it switches workspace: this tab only sees localStorage change. */
function otherTabPicks(orgId: string) {
  localStorage.setItem('aci_org_id', orgId);
}

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});

afterEach(() => vi.unstubAllEnvs());

describe('the workspace of a tab', () => {
  it('stays put when another tab switches', () => {
    setCurrentOrganizationId('pavillon');
    otherTabPicks('lac-leman');
    expect(getCurrentOrganizationId()).toBe('pavillon');
  });

  it('is, in a new tab, the one last picked, and is then kept', () => {
    otherTabPicks('pavillon');
    expect(keepWorkspaceInThisTab()).toBe('pavillon');

    otherTabPicks('lac-leman');
    expect(getCurrentOrganizationId()).toBe('pavillon');
    expect(keepWorkspaceInThisTab()).toBe('pavillon');
  });

  it("is offered to new tabs when this tab gets the focus", () => {
    setCurrentOrganizationId('pavillon');
    otherTabPicks('lac-leman');

    offerWorkspaceToNewTabs();

    expect(localStorage.getItem('aci_org_id')).toBe('pavillon');
  });

  it('is forgotten, in this tab too, on signing in and signing out', () => {
    setCurrentOrganizationId('pavillon');
    setSession('token', { id: 'u2', name: 'Next', email: 'next@example.com' });
    expect(getCurrentOrganizationId()).toBeNull();

    setCurrentOrganizationId('pavillon');
    clearSession();
    expect(getCurrentOrganizationId()).toBeNull();
    expect(sessionStorage.getItem('aci_org_id')).toBeNull();
  });

  it("is the one this tab's requests carry, whatever another tab picked", async () => {
    vi.stubEnv('VITE_API_URL', BASE);
    localStorage.setItem('aci_token', 'token');
    let sentTo: string | null = null;
    server.use(http.post(`${BASE}/api/leads`, ({ request }) => {
      sentTo = request.headers.get('X-Organization-Id');
      return HttpResponse.json({ id: 'l1' });
    }));

    setCurrentOrganizationId('pavillon');
    otherTabPicks('lac-leman');
    await authFetch('/api/leads', { method: 'POST', body: '{}' });

    expect(sentTo).toBe('pavillon');
  });
});

describe('a sign-in in another tab', () => {
  it('is announced in this tab, which shares the token', () => {
    const heard = vi.fn();
    window.addEventListener(AUTH_SESSION_CHANGED, heard);
    const stop = watchSessionInOtherTabs();

    window.dispatchEvent(new StorageEvent('storage', { key: 'aci_token', newValue: 'other', storageArea: localStorage }));
    // A workspace picked in another tab is that tab's business.
    window.dispatchEvent(new StorageEvent('storage', { key: 'aci_org_id', newValue: 'x', storageArea: localStorage }));

    expect(heard).toHaveBeenCalledTimes(1);
    stop();
    window.removeEventListener(AUTH_SESSION_CHANGED, heard);
  });
});

const TOKEN_KEY = 'aci_token';
const USER_KEY = 'aci_user';
const ORG_ID_KEY = 'aci_org_id';

/** Fired on same-tab login/logout so OrgProvider can refetch workspaces. */
export const AUTH_SESSION_CHANGED = 'aci-auth-session-changed';

function notifySessionChanged(): void {
  try {
    window.dispatchEvent(new Event(AUTH_SESSION_CHANGED));
  } catch {
    // ignore (non-browser)
  }
}

export type AuthUser = { id: string; name: string; email: string };

export function setSession(token: string, user: AuthUser): void {
  try {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    // New login must not keep another user's workspace id (breaks org list / APIs).
    localStorage.removeItem(ORG_ID_KEY);
    notifySessionChanged();
  } catch {
    // ignore (e.g. private mode / quota) — do not notify; org refetch would see stale auth
  }
}

export function getCurrentOrganizationId(): string | null {
  try {
    return localStorage.getItem(ORG_ID_KEY);
  } catch {
    return null;
  }
}

export function setCurrentOrganizationId(orgId: string | null): void {
  try {
    if (orgId == null) localStorage.removeItem(ORG_ID_KEY);
    else localStorage.setItem(ORG_ID_KEY, orgId);
  } catch {
    // ignore
  }
}

export function getAuthToken(): string | null {
  try {
    return localStorage.getItem(TOKEN_KEY);
  } catch {
    return null;
  }
}

export function getCurrentUser(): AuthUser | null {
  try {
    const raw = localStorage.getItem(USER_KEY);
    if (!raw) return null;
    const data = JSON.parse(raw) as AuthUser;
    return data?.id && data?.name && data?.email ? data : null;
  } catch {
    return null;
  }
}

export function clearSession(): void {
  try {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    localStorage.removeItem(ORG_ID_KEY);
    notifySessionChanged();
  } catch {
    // ignore
  }
}

export function isLoggedIn(): boolean {
  return getCurrentUser() !== null;
}

/** Set user only (demo mode: no token). For real API use setSession after login/register. */
export function setDemoUser(user: { name: string; email: string }): void {
  try {
    localStorage.setItem(USER_KEY, JSON.stringify({ id: 'demo', name: user.name, email: user.email }));
  } catch {
    // ignore
  }
}

export const getDemoUser = (): AuthUser | null => getCurrentUser();

/** True when the user is in demo mode (no backend; sample data only). */
export function isDemoMode(): boolean {
  const user = getCurrentUser();
  return user?.id === 'demo';
}

export const clearDemoUser = (): void => clearSession();

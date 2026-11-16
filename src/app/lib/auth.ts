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

/**
 * Fired when a workspace refused a request because it requires single sign-on and this
 * session began another way (errorCode Sso.Required), with the workspace's id as
 * `detail.organizationId`. Its owner may have turned the requirement on while the
 * workspace was open: OrgProvider reads the workspaces again, and the workspace then
 * says why and offers the way in instead of failing every request.
 */
export const SSO_SIGN_IN_NEEDED = 'aci-sso-sign-in-needed';

export function announceSsoSignInNeeded(organizationId: string): void {
  try {
    window.dispatchEvent(new CustomEvent(SSO_SIGN_IN_NEEDED, { detail: { organizationId } }));
  } catch {
    // ignore (non-browser)
  }
}

/**
 * `emailVerified` is false while the account has not confirmed its address, and
 * absent for a session saved before the server reported it.
 */
export type AuthUser = { id: string; name: string; email: string; emailVerified?: boolean };

export function setSession(token: string, user: AuthUser): void {
  try {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
    // New login must not keep another user's workspace id (breaks org list / APIs).
    forgetWorkspace();
    notifySessionChanged();
  } catch {
    // ignore (e.g. private mode / quota) — do not notify; org refetch would see stale auth
  }
}

/**
 * Swap in a new token for the same signed-in user, keeping the chosen workspace.
 * Turning 2FA on or off ends every earlier session on the server, this one included,
 * and answers with the token this browser carries on with.
 */
export function replaceToken(token: string): void {
  try {
    localStorage.setItem(TOKEN_KEY, token);
  } catch {
    // ignore
  }
}

// The workspace is kept per browser tab. With one copy in localStorage for every
// tab, switching workspace in one tab moved all the others: a tab whose header still
// said Pavillon 46 sent its next write, and read its next list, as Lac Léman SA.
// Each tab now keeps its own (sessionStorage). localStorage keeps the workspace a
// new tab opens in: the one last picked, or last used in a tab that had focus.

function read(storage: Storage, key: string): string | null {
  try {
    return storage.getItem(key);
  } catch {
    return null;
  }
}

function write(storage: Storage, key: string, value: string | null): void {
  try {
    if (value == null) storage.removeItem(key);
    else storage.setItem(key, value);
  } catch {
    // ignore (private mode, quota)
  }
}

function forgetWorkspace(): void {
  write(sessionStorage, ORG_ID_KEY, null);
  write(localStorage, ORG_ID_KEY, null);
}

/** The workspace this tab works in: its own, or before it has one, the one a new tab opens in. */
export function getCurrentOrganizationId(): string | null {
  return read(sessionStorage, ORG_ID_KEY) ?? read(localStorage, ORG_ID_KEY);
}

/** Pick this tab's workspace. A tab opened after this opens in it too. */
export function setCurrentOrganizationId(orgId: string | null): void {
  write(sessionStorage, ORG_ID_KEY, orgId);
  write(localStorage, ORG_ID_KEY, orgId);
}

/**
 * Give this tab its own copy of the workspace it opened in. Until then it reads the
 * shared one, which a switch in any other tab would change under it. Called once, as
 * the workspaces load.
 */
export function keepWorkspaceInThisTab(): string | null {
  const own = read(sessionStorage, ORG_ID_KEY);
  if (own) return own;
  const shared = read(localStorage, ORG_ID_KEY);
  if (shared) write(sessionStorage, ORG_ID_KEY, shared);
  return shared;
}

/**
 * Make this tab's workspace the one a new tab opens in. Called when the tab gets the
 * focus, so a deal opened in a new tab from here (or any link middle-clicked) opens in
 * this tab's workspace, not in the one last picked in another tab.
 */
export function offerWorkspaceToNewTabs(): void {
  const own = read(sessionStorage, ORG_ID_KEY);
  if (own) write(localStorage, ORG_ID_KEY, own);
}

/**
 * Tell this tab when another tab signs in or out. They share one sign-in (the token is
 * in localStorage), and without this a tab went on showing the last account's
 * workspace while its requests already went out as the next one.
 */
export function watchSessionInOtherTabs(): () => void {
  if (typeof window === 'undefined') return () => {};
  const onStorage = (event: StorageEvent) => {
    if (event.storageArea !== localStorage) return;
    // localStorage.clear() in another tab reports a null key.
    if (event.key === TOKEN_KEY || event.key === USER_KEY || event.key === null) notifySessionChanged();
  };
  window.addEventListener('storage', onStorage);
  return () => window.removeEventListener('storage', onStorage);
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

/** The sales agent's saved conversation and focus (salesAgent/AgentDesk). They name customers. */
const AGENT_KEY_PREFIX = 'cadence.agent.';

function forgetAgentConversations(storage: Storage): void {
  const keys: string[] = [];
  for (let index = 0; index < storage.length; index += 1) {
    const key = storage.key(index);
    if (key?.startsWith(AGENT_KEY_PREFIX)) keys.push(key);
  }
  for (const key of keys) storage.removeItem(key);
}

/**
 * Change the stored details of the signed-in user (for example once the address is
 * confirmed) without touching the token or the chosen workspace, which setSession resets.
 */
export function updateCurrentUser(patch: Partial<Omit<AuthUser, 'id'>>): void {
  const user = getCurrentUser();
  if (!user) return;
  try {
    localStorage.setItem(USER_KEY, JSON.stringify({ ...user, ...patch }));
  } catch {
    // ignore
  }
}

export function clearSession(): void {
  try {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    forgetWorkspace();
    // The next person at this browser must not find the last one's questions about customers.
    forgetAgentConversations(localStorage);
    forgetAgentConversations(sessionStorage);
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
    // Notify OrgProvider so it can react to the just-entered demo session in the same tab.
    notifySessionChanged();
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

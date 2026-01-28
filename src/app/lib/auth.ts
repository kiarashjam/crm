const TOKEN_KEY = 'aci_token';
const USER_KEY = 'aci_user';

export type AuthUser = { id: string; name: string; email: string };

export function setSession(token: string, user: AuthUser): void {
  try {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
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
export const clearDemoUser = (): void => clearSession();

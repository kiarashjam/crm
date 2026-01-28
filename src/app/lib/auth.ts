const CRM_USER_KEY = 'crm_demo_user';

export type DemoUser = { name: string; email: string };

export function setDemoUser(user: DemoUser): void {
  try {
    localStorage.setItem(CRM_USER_KEY, JSON.stringify(user));
  } catch {
    // ignore
  }
}

export function getDemoUser(): DemoUser | null {
  try {
    const raw = localStorage.getItem(CRM_USER_KEY);
    if (!raw) return null;
    const data = JSON.parse(raw) as DemoUser;
    return data?.name && data?.email ? data : null;
  } catch {
    return null;
  }
}

export function clearDemoUser(): void {
  try {
    localStorage.removeItem(CRM_USER_KEY);
  } catch {
    // ignore
  }
}

export function isLoggedIn(): boolean {
  return getDemoUser() !== null;
}

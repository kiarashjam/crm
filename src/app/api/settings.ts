import type { UserSettings } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

const STORAGE_KEY = 'crm_user_settings';

function getStored(): UserSettings | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as UserSettings;
  } catch {
    return null;
  }
}

function setStored(settings: UserSettings) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(settings));
  } catch {
    // ignore
  }
}

const defaults: UserSettings = {
  companyName: 'Acme Corporation',
  brandTone: 'professional',
};

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Get user settings (company name, brand tone). */
export async function getUserSettings(): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ companyName: string; brandTone: string }>('/api/settings');
    return res ? { companyName: res.companyName, brandTone: res.brandTone as UserSettings['brandTone'] } : { ...defaults };
  }
  await delay(80);
  const stored = getStored();
  return stored ? { ...defaults, ...stored } : { ...defaults };
}

/** Save user settings. */
export async function saveUserSettings(settings: Partial<UserSettings>): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<{ companyName: string; brandTone: string }>('/api/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
    return res ? { companyName: res.companyName, brandTone: res.brandTone as UserSettings['brandTone'] } : { ...defaults };
  }
  await delay(100);
  const current = getStored() ?? defaults;
  const next = { ...current, ...settings };
  setStored(next);
  return next;
}

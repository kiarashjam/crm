import type { UserSettings } from './types';

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

/** Get user settings (company name, brand tone). */
export async function getUserSettings(): Promise<UserSettings> {
  await delay(80);
  const stored = getStored();
  return stored ? { ...defaults, ...stored } : { ...defaults };
}

/** Save user settings. */
export async function saveUserSettings(settings: Partial<UserSettings>): Promise<UserSettings> {
  await delay(100);
  const current = getStored() ?? defaults;
  const next = { ...current, ...settings };
  setStored(next);
  return next;
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

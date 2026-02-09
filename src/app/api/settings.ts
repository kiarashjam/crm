import type { UserSettings, UpdateUserSettingsRequest, TimezoneOption, CurrencyOption, LanguageOption } from './types';
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
  // Profile
  brandName: 'My Brand',
  timezone: 'UTC',
  language: 'en',

  // Brand
  brandTone: 'professional',

  // Appearance
  theme: 'light',
  dataDensity: 'comfortable',
  sidebarCollapsed: false,
  showWelcomeBanner: true,

  // Notifications
  emailNotificationsEnabled: true,
  emailOnNewLead: true,
  emailOnDealUpdate: true,
  emailOnTaskDue: true,
  emailOnTeamMention: true,
  inAppNotificationsEnabled: true,
  inAppSoundEnabled: true,
  browserNotificationsEnabled: false,

  // Defaults
  defaultFollowUpDays: 3,
  defaultCurrency: 'USD',

  // Privacy
  showActivityStatus: true,
  allowAnalytics: true,
};

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Get user settings. */
export async function getUserSettings(): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<UserSettings>('/api/settings');
    return res ? { ...defaults, ...res } : { ...defaults };
  }
  await delay(80);
  const stored = getStored();
  return stored ? { ...defaults, ...stored } : { ...defaults };
}

/** Save user settings (partial update). */
export async function saveUserSettings(settings: UpdateUserSettingsRequest): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<UserSettings>('/api/settings', {
      method: 'PUT',
      body: JSON.stringify(settings),
    });
    return res ? { ...defaults, ...res } : { ...defaults };
  }
  await delay(100);
  const current = getStored() ?? defaults;
  const next = { ...current, ...settings } as UserSettings;
  setStored(next);
  return next;
}

/** Save a specific section of settings. */
export async function saveSettingsSection(
  section: 'profile' | 'brand' | 'appearance' | 'notifications' | 'defaults' | 'privacy',
  settings: UpdateUserSettingsRequest
): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<UserSettings>(`/api/settings/${section}`, {
      method: 'PATCH',
      body: JSON.stringify(settings),
    });
    return res ? { ...defaults, ...res } : { ...defaults };
  }
  // Mock: same as saveUserSettings
  return saveUserSettings(settings);
}

/** Reset all settings to defaults. */
export async function resetUserSettings(): Promise<UserSettings> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<UserSettings>('/api/settings/reset', {
      method: 'POST',
    });
    return res ? { ...defaults, ...res } : { ...defaults };
  }
  await delay(100);
  setStored(defaults);
  return defaults;
}

/** Get available timezones. */
export async function getTimezones(): Promise<TimezoneOption[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<TimezoneOption[]>('/api/settings/timezones');
    return res ?? getDefaultTimezones();
  }
  await delay(50);
  return getDefaultTimezones();
}

/** Get available currencies. */
export async function getCurrencies(): Promise<CurrencyOption[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<CurrencyOption[]>('/api/settings/currencies');
    return res ?? getDefaultCurrencies();
  }
  await delay(50);
  return getDefaultCurrencies();
}

/** Get available languages. */
export async function getLanguages(): Promise<LanguageOption[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<LanguageOption[]>('/api/settings/languages');
    return res ?? getDefaultLanguages();
  }
  await delay(50);
  return getDefaultLanguages();
}

// Default data for mock mode
function getDefaultTimezones(): TimezoneOption[] {
  return [
    { id: 'UTC', displayName: '(UTC) Coordinated Universal Time', utcOffset: 0 },
    { id: 'America/New_York', displayName: '(UTC-05:00) Eastern Time', utcOffset: -5 },
    { id: 'America/Chicago', displayName: '(UTC-06:00) Central Time', utcOffset: -6 },
    { id: 'America/Denver', displayName: '(UTC-07:00) Mountain Time', utcOffset: -7 },
    { id: 'America/Los_Angeles', displayName: '(UTC-08:00) Pacific Time', utcOffset: -8 },
    { id: 'Europe/London', displayName: '(UTC+00:00) London', utcOffset: 0 },
    { id: 'Europe/Paris', displayName: '(UTC+01:00) Paris, Berlin', utcOffset: 1 },
    { id: 'Europe/Moscow', displayName: '(UTC+03:00) Moscow', utcOffset: 3 },
    { id: 'Asia/Dubai', displayName: '(UTC+04:00) Dubai', utcOffset: 4 },
    { id: 'Asia/Kolkata', displayName: '(UTC+05:30) India', utcOffset: 5.5 },
    { id: 'Asia/Singapore', displayName: '(UTC+08:00) Singapore', utcOffset: 8 },
    { id: 'Asia/Tokyo', displayName: '(UTC+09:00) Tokyo', utcOffset: 9 },
    { id: 'Australia/Sydney', displayName: '(UTC+10:00) Sydney', utcOffset: 10 },
  ];
}

function getDefaultCurrencies(): CurrencyOption[] {
  return [
    { code: 'USD', name: 'US Dollar', symbol: '$' },
    { code: 'EUR', name: 'Euro', symbol: '€' },
    { code: 'GBP', name: 'British Pound', symbol: '£' },
    { code: 'JPY', name: 'Japanese Yen', symbol: '¥' },
    { code: 'AUD', name: 'Australian Dollar', symbol: 'A$' },
    { code: 'CAD', name: 'Canadian Dollar', symbol: 'C$' },
    { code: 'CHF', name: 'Swiss Franc', symbol: 'CHF' },
    { code: 'CNY', name: 'Chinese Yuan', symbol: '¥' },
    { code: 'INR', name: 'Indian Rupee', symbol: '₹' },
    { code: 'MXN', name: 'Mexican Peso', symbol: 'MX$' },
  ];
}

function getDefaultLanguages(): LanguageOption[] {
  return [
    { code: 'en', name: 'English', nativeName: 'English' },
    { code: 'es', name: 'Spanish', nativeName: 'Español' },
    { code: 'fr', name: 'French', nativeName: 'Français' },
    { code: 'de', name: 'German', nativeName: 'Deutsch' },
    { code: 'it', name: 'Italian', nativeName: 'Italiano' },
    { code: 'pt', name: 'Portuguese', nativeName: 'Português' },
    { code: 'nl', name: 'Dutch', nativeName: 'Nederlands' },
    { code: 'ja', name: 'Japanese', nativeName: '日本語' },
    { code: 'ko', name: 'Korean', nativeName: '한국어' },
    { code: 'zh', name: 'Chinese', nativeName: '中文' },
  ];
}

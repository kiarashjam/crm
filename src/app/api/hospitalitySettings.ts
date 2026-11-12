// Hospitality settings: the rules the server applies to the club. Stored as a single
// document, read on the server (ClubSettings) wherever the rules are applied.
//
// - `currency`: what every club page formats amounts in (see useClubMoney).
// - Points: base points per unit spent (a tab's close), points per visit by venue (a
//   check-in), and per event check-in; each times the member's tier multiplier, which
//   is the tier's in the price catalogue (api/catalog).
// - `billing`: what the daily billing run does on its own, all off until turned on.
//
// The tiers, dues and initiation fees are the price catalogue's now. The redemption rate
// and the referral and anniversary bonuses were saved here and never used by anything,
// so they left the settings rather than look as if they did something.

import { apiWithFallback, authFetchJson, type RequestOptions } from './apiClient';
import { LEGACY_BACKUP_SUFFIX, scopedStorageKey } from './mockStore';
import type { VisitVenue } from './visits';
import { DEFAULT_CLUB_CURRENCY } from '@/app/lib/clubMoney';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** What the club's billing does on its own. */
export interface BillingSettings {
  /** The daily run bills every subscription whose period has started. */
  automaticBilling: boolean;
  /** Days a billed charge has before it is due. */
  paymentTermsDays: number;
  /** The daily run reminds members of charges past their due date. */
  remindersEnabled: boolean;
  /** Days after the due date each reminder goes, ascending: one to five of them. */
  reminderDays: number[];
  /** The daily run lapses an active member with a charge overdue past the grace period. */
  lapseEnabled: boolean;
  gracePeriodDays: number;
  /** The daily run emails last month's statements from the statement day on. */
  monthlyStatements: boolean;
  statementDay: number;
}

export interface HospitalitySettings {
  /** ISO code every club amount is shown in. A label only: nothing is converted. */
  currency: string;
  basePointsPerDollar: number;
  visitPointsByVenue: Record<VisitVenue, number>;
  eventCheckInPoints: number;
  billing: BillingSettings;
}

export const DEFAULT_BILLING: BillingSettings = {
  automaticBilling: false,
  paymentTermsDays: 14,
  remindersEnabled: false,
  reminderDays: [3, 14, 30],
  lapseEnabled: false,
  gracePeriodDays: 60,
  monthlyStatements: false,
  statementDay: 1,
};

const DEFAULT_SETTINGS: HospitalitySettings = {
  currency: DEFAULT_CLUB_CURRENCY,
  basePointsPerDollar: 10,
  visitPointsByVenue: {
    Dining: 50,
    Bar: 40,
    Spa: 60,
    Gym: 20,
    Coworking: 30,
    Event: 50,
    Lounge: 25,
    Rooftop: 30,
  },
  eventCheckInPoints: 50,
  billing: DEFAULT_BILLING,
};

/** What the server answered (or demo data stored), over the defaults, so a partial object stays valid. */
function withDefaults(raw: Partial<HospitalitySettings> | null | undefined): HospitalitySettings {
  const value = raw ?? {};
  return {
    ...DEFAULT_SETTINGS,
    ...value,
    visitPointsByVenue: { ...DEFAULT_SETTINGS.visitPointsByVenue, ...(value.visitPointsByVenue ?? {}) },
    billing: { ...DEFAULT_BILLING, ...(value.billing ?? {}) },
  };
}

const STORAGE_KEY = 'crm.mock.hospitalitySettings.v1';

/** Stored settings, scoped like every other local store (see `scopedStorageKey`).
 *  Settings saved under the bare key before scoping move once to the first scope that reads them. */
function readRaw(): string | null {
  const key = scopedStorageKey(STORAGE_KEY);
  const raw = localStorage.getItem(key);
  if (raw || key === STORAGE_KEY) return raw;
  const legacy = localStorage.getItem(STORAGE_KEY);
  if (!legacy) return null;
  localStorage.setItem(key, legacy);
  localStorage.setItem(STORAGE_KEY + LEGACY_BACKUP_SUFFIX, legacy);
  localStorage.removeItem(STORAGE_KEY);
  return legacy;
}

function readSettings(): HospitalitySettings {
  try {
    const raw = readRaw();
    return withDefaults(raw ? JSON.parse(raw) : null);
  } catch {
    return DEFAULT_SETTINGS;
  }
}

function writeSettings(s: HospitalitySettings): HospitalitySettings {
  try {
    localStorage.setItem(scopedStorageKey(STORAGE_KEY), JSON.stringify(s));
  } catch {
    // ignore
  }
  return s;
}

export async function getHospitalitySettings(req?: RequestOptions): Promise<HospitalitySettings> {
  return apiWithFallback(
    async () => withDefaults(await authFetchJson<Partial<HospitalitySettings>>('/api/hospitality/settings', { signal: req?.signal })),
    async () => {
      await delay(80);
      return readSettings();
    },
  );
}

/** Saves the settings sent; the server checks them and answers what now applies. */
export async function updateHospitalitySettings(
  patch: Partial<HospitalitySettings>,
): Promise<HospitalitySettings> {
  return apiWithFallback(
    async () => withDefaults(await authFetchJson<Partial<HospitalitySettings>>('/api/hospitality/settings', {
      method: 'PUT',
      body: JSON.stringify(patch),
    })),
    async () => {
      await delay(120);
      const current = readSettings();
      return writeSettings(withDefaults({ ...current, ...patch }));
    },
  );
}

export async function resetHospitalitySettings(): Promise<HospitalitySettings> {
  return apiWithFallback(
    // The server forgets the saved settings and answers the defaults it applies.
    async () => withDefaults(await authFetchJson<Partial<HospitalitySettings>>('/api/hospitality/settings/reset', {
      method: 'POST',
    })),
    async () => {
      await delay(80);
      return writeSettings(DEFAULT_SETTINGS);
    },
  );
}

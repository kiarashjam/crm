// Hospitality settings — admin-configurable rules for tiers, dues, loyalty
// multipliers, and venue point values. Stored as a single document.

import { apiWithFallback, authFetchJson } from './apiClient';
import type { MemberTier } from './members';
import type { VisitVenue } from './visits';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export interface TierConfig {
  tier: MemberTier;
  monthlyDues: number;
  loyaltyMultiplier: number;
  benefits: string[];
  initiationFee: number;
}

export interface HospitalitySettings {
  tiers: TierConfig[];
  basePointsPerDollar: number;
  pointsPerDollarRedemption: number;
  visitPointsByVenue: Record<VisitVenue, number>;
  eventCheckInPoints: number;
  referralBonus: number;
  anniversaryBonus: number;
}

const DEFAULT_SETTINGS: HospitalitySettings = {
  tiers: [
    {
      tier: 'Bronze',
      monthlyDues: 49,
      loyaltyMultiplier: 1.0,
      initiationFee: 0,
      benefits: ['Coworking access', 'Member rate on events', 'Member directory'],
    },
    {
      tier: 'Silver',
      monthlyDues: 129,
      loyaltyMultiplier: 1.25,
      initiationFee: 250,
      benefits: ['All Bronze benefits', 'Spa access', 'Fitness studio', 'Rooftop access'],
    },
    {
      tier: 'Gold',
      monthlyDues: 299,
      loyaltyMultiplier: 1.5,
      initiationFee: 1000,
      benefits: ['All Silver benefits', 'Private dining priority', 'Guest passes (4/mo)', 'Tennis court'],
    },
    {
      tier: 'Platinum',
      monthlyDues: 599,
      loyaltyMultiplier: 2.0,
      initiationFee: 2500,
      benefits: ['All Gold benefits', 'Unlimited guest passes', 'Comp spa treatment monthly', 'Founders events'],
    },
  ],
  basePointsPerDollar: 10,
  pointsPerDollarRedemption: 100,
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
  referralBonus: 1000,
  anniversaryBonus: 500,
};

const STORAGE_KEY = 'crm.mock.hospitalitySettings.v1';

function readSettings(): HospitalitySettings {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return DEFAULT_SETTINGS;
    const parsed = JSON.parse(raw);
    // Merge with defaults so a stored partial object stays valid.
    return { ...DEFAULT_SETTINGS, ...parsed };
  } catch {
    return DEFAULT_SETTINGS;
  }
}

function writeSettings(s: HospitalitySettings): HospitalitySettings {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(s));
  } catch {
    // ignore
  }
  return s;
}

export async function getHospitalitySettings(): Promise<HospitalitySettings> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<HospitalitySettings>('/api/hospitality/settings');
      return { ...DEFAULT_SETTINGS, ...res };
    },
    async () => {
      await delay(80);
      return readSettings();
    },
  );
}

export async function updateHospitalitySettings(
  patch: Partial<HospitalitySettings>,
): Promise<HospitalitySettings> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<HospitalitySettings>('/api/hospitality/settings', {
        method: 'PUT',
        body: JSON.stringify(patch),
      });
      return { ...DEFAULT_SETTINGS, ...res };
    },
    async () => {
      await delay(120);
      const current = readSettings();
      return writeSettings({ ...current, ...patch });
    },
  );
}

export async function resetHospitalitySettings(): Promise<HospitalitySettings> {
  return apiWithFallback(
    () =>
      authFetchJson<HospitalitySettings>('/api/hospitality/settings/reset', {
        method: 'POST',
      }),
    async () => {
      await delay(80);
      return writeSettings(DEFAULT_SETTINGS);
    },
  );
}

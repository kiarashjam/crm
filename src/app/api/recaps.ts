// The workspace's recap emails: a daily, weekly and monthly email of the sales funnel and the
// club's figures, each beside the period before, for the people who see all deals. Who gets
// which is chosen in Settings › Recap emails by those who manage the settings
// (/api/recaps/recipients); each person switches their own in Settings › Notifications
// (/api/settings/recaps, with their other preferences, so a read-only member can too).
//
// Server only: in demo mode nothing is sent, so there is nothing to set up.

import { authFetchJson, servesDemoData, type RequestOptions } from './apiClient';

export type RecapFrequency = 'daily' | 'weekly' | 'monthly';

/** The three, in the order they are shown, with how each is described. */
export const RECAP_FREQUENCIES: readonly { key: RecapFrequency; label: string; when: string }[] = [
  { key: 'daily', label: 'Daily', when: 'Every day from 7:00, for the day before.' },
  { key: 'weekly', label: 'Weekly', when: 'Every Monday from 7:00, for Monday to Sunday just ended.' },
  { key: 'monthly', label: 'Monthly', when: 'On the 1st from 7:00, for the month just ended.' },
];

export interface RecapFrequencies {
  daily: boolean;
  weekly: boolean;
  monthly: boolean;
}

/** Someone who may receive recaps (they see all deals), and which they get. */
export interface RecapRecipient extends RecapFrequencies {
  userId: string;
  name: string;
  email: string;
  /** The preset their permissions start from: read_only, manager… */
  preset: string;
}

export interface RecapRecipients {
  /** The workspace's time zone, which the recaps' 7:00 is in. */
  timeZone: string;
  /** Everyone who sees all deals, the owner first. */
  people: RecapRecipient[];
  /** How many others are on the team: they do not see all deals, so they get none. */
  notEligible: number;
}

/** The signed-in person's own recaps in the workspace. */
export interface MyRecaps extends RecapFrequencies {
  /** Whether they see all deals here, without which they receive no recap. */
  eligible: boolean;
  timeZone: string;
}

/** Frequencies to switch on or off; one left out stays as it is. */
export type RecapChange = Partial<RecapFrequencies>;

/** Who receives which; null in demo mode. Takes settings.manage. */
export async function getRecapRecipients(req?: RequestOptions): Promise<RecapRecipients | null> {
  if (servesDemoData()) return null;
  return authFetchJson<RecapRecipients>('/api/recaps/recipients', { signal: req?.signal });
}

/** Switches one person's recaps on or off. Refused (Recap.NotEligible) for someone who does not see all deals. */
export async function setRecapRecipient(userId: string, change: RecapChange): Promise<RecapRecipient> {
  return authFetchJson<RecapRecipient>(`/api/recaps/recipients/${encodeURIComponent(userId)}`, {
    method: 'PUT',
    body: JSON.stringify(change),
  });
}

/** The person's own recaps here; null in demo mode. */
export async function getMyRecaps(req?: RequestOptions): Promise<MyRecaps | null> {
  if (servesDemoData()) return null;
  return authFetchJson<MyRecaps>('/api/settings/recaps', { signal: req?.signal });
}

/** Switches the person's own recaps on or off. */
export async function setMyRecaps(change: RecapChange): Promise<MyRecaps> {
  return authFetchJson<MyRecaps>('/api/settings/recaps', { method: 'PUT', body: JSON.stringify(change) });
}

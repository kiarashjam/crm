// The club currency for the workspace in use, and a formatter for it.
//
// Read once per workspace from the hospitality settings and shared by every club page,
// so the members list, a tab and a printed statement cannot disagree about it. Until
// the settings answer (or if they cannot be read) amounts show in the default, CHF.

import { useCallback, useEffect, useSyncExternalStore } from 'react';
import { getHospitalitySettings } from '@/app/api/hospitalitySettings';
import { getCurrentOrganizationId } from '@/app/lib/auth';
import { DEFAULT_CLUB_CURRENCY, formatClubMoney, normaliseCurrency } from '@/app/lib/clubMoney';

const known = new Map<string, string>();
const loading = new Set<string>();
const listeners = new Set<() => void>();

/** Workspaces can bill in different currencies, so the answer is kept per workspace. */
const scope = () => getCurrentOrganizationId() ?? '';

function notify(): void {
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function load(key: string): void {
  if (known.has(key) || loading.has(key)) return;
  loading.add(key);
  getHospitalitySettings()
    .then((settings) => {
      known.set(key, normaliseCurrency(settings.currency));
    })
    .catch(() => {
      // Keep showing the default; the next page that asks tries again.
    })
    .finally(() => {
      loading.delete(key);
      notify();
    });
}

/** After the settings are saved or reset, so open pages switch without a reload. */
export function setClubCurrency(currency: string): void {
  known.set(scope(), normaliseCurrency(currency));
  notify();
}

/** Test hook: forget every workspace's currency. */
export function resetClubCurrency(): void {
  known.clear();
  loading.clear();
  notify();
}

export function useClubMoney(): { currency: string; format: (amount: number) => string } {
  const key = scope();
  const currency = useSyncExternalStore(subscribe, () => known.get(key) ?? DEFAULT_CLUB_CURRENCY);
  useEffect(() => {
    load(key);
  }, [key]);
  const format = useCallback((amount: number) => formatClubMoney(amount, currency), [currency]);
  return { currency, format };
}

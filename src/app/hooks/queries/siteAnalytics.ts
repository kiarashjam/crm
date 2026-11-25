// Site analytics (Admin, Site analytics) and whether the signed-in person may see it.
//
// These reads are the platform's, not a workspace's: they are keyed by the signed-in user
// only (platformKeys), so a workspace switch keeps them and no workspace's save re-reads them.

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { servesDemoData } from '@/app/api/apiClient';
import {
  getPlatformMe, getSiteAnalyticsLive, getSiteAnalyticsSessions, getSiteAnalyticsSummary, type SiteAnalyticsRange,
} from '@/app/api/siteAnalytics';
import { getCurrentUser } from '@/app/lib/auth';
import { platformKeys } from './queryKeys';
import { timed } from './queryClient';

/** How long whether someone is a platform admin is trusted: it changes rarely, and every page's menu asks. */
export const PLATFORM_ME_FRESH_MS = 10 * 60_000;

/** How often "Live now" (and the header's Live pill) is read again while the page is open and in front. */
export const SITE_ANALYTICS_LIVE_REFRESH_MS = 15_000;

/** How many visits a day's journeys list asks for. */
export const SITE_ANALYTICS_SESSIONS_LIMIT = 50;

function useUserId(): string | null {
  return getCurrentUser()?.id ?? null;
}

/**
 * Whether the signed-in person is a CadenceE platform admin: the "Site analytics" menu item
 * and page are theirs only. Never asked in demo mode, or before anyone has signed in.
 */
export function usePlatformMe() {
  const userId = useUserId();
  return useQuery({
    queryKey: platformKeys.me(userId),
    queryFn: ({ signal }) => timed(signal, (s) => getPlatformMe({ signal: s })),
    enabled: userId !== null && !servesDemoData(),
    staleTime: PLATFORM_ME_FRESH_MS,
    refetchOnWindowFocus: false,
  });
}

/** True only once the server has said so. */
export function useIsPlatformAdmin(): boolean {
  return usePlatformMe().data?.isPlatformAdmin === true;
}

/** The period's figures. The last period's stay on screen, dimmed, while a new range loads. */
export function useSiteAnalyticsSummary(range: SiteAnalyticsRange, enabled = true) {
  const userId = useUserId();
  return useQuery({
    queryKey: platformKeys.siteAnalytics.summary(userId, range),
    queryFn: ({ signal }) => timed(signal, (s) => getSiteAnalyticsSummary(range, { signal: s })),
    placeholderData: keepPreviousData,
    enabled,
  });
}

/** The last 5 minutes, read again every 15 seconds while the tab is in front. */
export function useSiteAnalyticsLive(enabled = true) {
  const userId = useUserId();
  return useQuery({
    queryKey: platformKeys.siteAnalytics.live(userId),
    queryFn: ({ signal }) => timed(signal, (s) => getSiteAnalyticsLive({ signal: s })),
    refetchInterval: SITE_ANALYTICS_LIVE_REFRESH_MS,
    staleTime: 0,
    enabled,
  });
}

/** One day's visits, newest first. */
export function useSiteAnalyticsSessions(day: string, enabled = true, limit = SITE_ANALYTICS_SESSIONS_LIMIT) {
  const userId = useUserId();
  return useQuery({
    queryKey: platformKeys.siteAnalytics.sessions(userId, day, limit),
    queryFn: ({ signal }) => timed(signal, (s) => getSiteAnalyticsSessions(day, limit, { signal: s })),
    placeholderData: keepPreviousData,
    enabled: enabled && /^\d{4}-\d{2}-\d{2}$/.test(day),
  });
}

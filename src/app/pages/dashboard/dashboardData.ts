// Pure helpers behind the dashboard.
//
// Small, but the kind of thing that is wrong in a way nobody notices: the
// "Recent Activity" panel took the first five items the API happened to return
// and called them recent. On screen that read Aug 17, Aug 18, Aug 16, Aug 14,
// Aug 18 — a list labelled "recent" whose entries were in no order at all, and
// which could omit today's work entirely if it came back sixth.

import type { Activity } from '@/app/api/types';

/**
 * Milliseconds for an activity's timestamp, or null when it has none we can read.
 *
 * A record with an unparseable date must not sort as 1970 and land at the bottom
 * looking like the oldest thing in the CRM; it is pushed out of the list instead,
 * which is the honest treatment for "we do not know when this happened".
 */
function timeOf(a: Pick<Activity, 'createdAt'>): number | null {
  if (!a.createdAt) return null;
  const t = new Date(a.createdAt).getTime();
  return Number.isFinite(t) ? t : null;
}

/**
 * The `limit` most recent activities, newest first.
 *
 * Sorts before slicing — the other way round takes an arbitrary five and then
 * tidies them, which is what made the panel wrong. Ties break on id so the order
 * is total and the list cannot reshuffle between renders.
 */
export function mostRecent(activities: Activity[], limit: number): Activity[] {
  if (limit <= 0) return [];
  return activities
    .filter((a) => timeOf(a) !== null)
    .sort((a, b) => (timeOf(b) ?? 0) - (timeOf(a) ?? 0) || a.id.localeCompare(b.id))
    .slice(0, limit);
}

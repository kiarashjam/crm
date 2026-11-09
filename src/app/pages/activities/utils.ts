import { daysBetween, dayKeyIn, formatDay, todayKeyIn, weekdayOf } from '@/app/lib/zonedTime';

export function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
}

export function formatRelativeDate(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diffMs = now.getTime() - d.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);
  
  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/**
 * The heading an activity is listed under, by the day it happened on the workspace's
 * calendar (`timeZone`). It was this browser's day: away from the club, a call made
 * there just after midnight was listed under yesterday while the reports counted it
 * today. Weeks begin on Sunday, as they did.
 */
export function getDateGroup(iso: string, timeZone: string, now: Date = new Date()): string {
  const day = dayKeyIn(iso, timeZone);
  const ago = daysBetween(day, todayKeyIn(timeZone, now));
  const sinceSunday = weekdayOf(todayKeyIn(timeZone, now));
  if (ago <= 0) return 'Today';
  if (ago === 1) return 'Yesterday';
  if (ago <= sinceSunday) return 'This Week';
  if (ago <= sinceSunday + 7) return 'Last Week';
  return formatDay(day, { month: 'long', year: 'numeric' });
}

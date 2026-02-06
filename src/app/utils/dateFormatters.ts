/**
 * Date formatting utilities
 * Extracted from Activities.tsx and other components
 */

/**
 * Format a date string to locale format
 * @param iso - ISO date string
 * @returns Formatted date string (e.g., "1/15/24, 10:30 AM")
 */
export function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleString(undefined, { dateStyle: 'short', timeStyle: 'short' });
}

/**
 * Format a date as relative time (e.g., "5m ago", "2h ago")
 * @param iso - ISO date string
 * @returns Relative time string
 */
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
 * Get a date group label for grouping items (Today, Yesterday, This Week, etc.)
 * @param iso - ISO date string
 * @returns Group label string
 */
export function getDateGroup(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const yesterday = new Date(today.getTime() - 86400000);
  const thisWeekStart = new Date(today.getTime() - today.getDay() * 86400000);
  const lastWeekStart = new Date(thisWeekStart.getTime() - 7 * 86400000);
  
  if (d >= today) return 'Today';
  if (d >= yesterday) return 'Yesterday';
  if (d >= thisWeekStart) return 'This Week';
  if (d >= lastWeekStart) return 'Last Week';
  return d.toLocaleDateString(undefined, { month: 'long', year: 'numeric' });
}

/**
 * Format a date for display in forms
 * @param iso - ISO date string
 * @returns Formatted date string (e.g., "January 15, 2024")
 */
export function formatLongDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString(undefined, { 
    year: 'numeric', 
    month: 'long', 
    day: 'numeric' 
  });
}

/**
 * Format a date for input fields (YYYY-MM-DD)
 * @param date - Date object or ISO string
 * @returns Formatted date string for input[type="date"]
 */
export function formatDateForInput(date: Date | string): string {
  const d = typeof date === 'string' ? new Date(date) : date;
  return d.toISOString().split('T')[0] ?? '';
}

/**
 * Check if a date is overdue (past current date)
 * @param iso - ISO date string
 * @returns True if the date is in the past
 */
export function isOverdue(iso: string | undefined): boolean {
  if (!iso) return false;
  return new Date(iso) < new Date();
}

/**
 * Check if a date is today
 * @param iso - ISO date string
 * @returns True if the date is today
 */
export function isToday(iso: string | undefined): boolean {
  if (!iso) return false;
  const d = new Date(iso);
  const today = new Date();
  return (
    d.getDate() === today.getDate() &&
    d.getMonth() === today.getMonth() &&
    d.getFullYear() === today.getFullYear()
  );
}

/**
 * Get days until a date
 * @param iso - ISO date string
 * @returns Number of days (negative if in past)
 */
export function getDaysUntil(iso: string | undefined): number | null {
  if (!iso) return null;
  const d = new Date(iso);
  const now = new Date();
  const diffMs = d.getTime() - now.getTime();
  return Math.ceil(diffMs / 86400000);
}

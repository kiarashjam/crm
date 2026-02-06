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

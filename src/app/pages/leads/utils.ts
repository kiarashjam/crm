// Check if a string is a valid GUID (backend expects Guid? for leadStatusId/leadSourceId)
export const isValidGuid = (str: string | undefined): boolean => {
  if (!str) return false;
  const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
  return guidRegex.test(str);
};

// Format date helper for display
export const formatDate = (dateStr: string): string => {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;
  
  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
};

// Format relative time for activity timeline
export const formatRelativeTime = (dateStr: string): string => {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins} minute${diffMins > 1 ? 's' : ''} ago`;
  if (diffHours < 24) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
  if (diffDays < 7) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  
  return date.toLocaleDateString('en-US', { 
    month: 'short', 
    day: 'numeric',
    year: date.getFullYear() !== now.getFullYear() ? 'numeric' : undefined 
  });
};

// Get initials from name
export const getInitials = (name: string): string => {
  return name
    .split(' ')
    .map(n => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();
};

// Calculate lead score color
export const getScoreColor = (score: number): { bg: string; text: string } => {
  if (score >= 80) return { bg: 'bg-emerald-100', text: 'text-emerald-700' };
  if (score >= 60) return { bg: 'bg-blue-100', text: 'text-blue-700' };
  if (score >= 40) return { bg: 'bg-amber-100', text: 'text-amber-700' };
  return { bg: 'bg-slate-100', text: 'text-slate-600' };
};

// Get activity color class
export const getActivityColor = (type: string): string => {
  const colors: Record<string, string> = {
    call: 'blue',
    email: 'purple',
    meeting: 'green',
    note: 'amber',
    system: 'slate',
  };
  return colors[type] || 'slate';
};

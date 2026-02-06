import type { TaskItem } from '@/app/api/types';
import type { TaskGroup } from './types';

/**
 * Determines which group a task belongs to based on its status and due date
 */
export function getTaskGroup(
  task: TaskItem,
  today: Date,
  tomorrow: Date,
  endOfWeek: Date
): TaskGroup {
  if (task.status === 'completed' || task.status === 'cancelled') return 'completed';
  if (!task.dueDateUtc) return 'noDue';

  const dueDate = new Date(task.dueDateUtc);
  dueDate.setHours(0, 0, 0, 0);

  if (dueDate < today) return 'overdue';
  if (dueDate.getTime() === today.getTime()) return 'today';
  if (dueDate.getTime() === tomorrow.getTime()) return 'tomorrow';
  if (dueDate <= endOfWeek) return 'thisWeek';
  return 'later';
}

/**
 * Formats a due date ISO string into a human-readable format
 */
export function formatDue(iso: string | undefined): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  const dueDate = new Date(d);
  dueDate.setHours(0, 0, 0, 0);

  const now = new Date();
  const today = new Date(now);
  today.setHours(0, 0, 0, 0);
  const tomorrow = new Date(today);
  tomorrow.setDate(tomorrow.getDate() + 1);

  if (dueDate.getTime() === today.getTime()) return 'Today';
  if (dueDate.getTime() === tomorrow.getTime()) return 'Tomorrow';
  if (dueDate < today) {
    const days = Math.floor((today.getTime() - dueDate.getTime()) / (1000 * 60 * 60 * 24));
    return `${days}d overdue`;
  }
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}

/**
 * Gets initials from a person's name
 */
export function getInitials(name: string): string {
  return name.split(' ').map((n) => n[0]).join('').toUpperCase().slice(0, 2);
}

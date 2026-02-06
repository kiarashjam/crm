import type { TaskStatusType, TaskPriorityType } from '@/app/api/types';

export interface TaskStatusConfig {
  label: string;
  color: string;
  bgColor: string;
  textColor: string;
  borderColor: string;
}

export interface TaskPriorityConfig {
  label: string;
  color: string;
  bgColor: string;
  textColor: string;
  icon: string;
}

/**
 * Task status configuration
 */
export const TASK_STATUS_CONFIG: Record<TaskStatusType, TaskStatusConfig> = {
  todo: {
    label: 'To Do',
    color: 'slate',
    bgColor: 'bg-slate-100',
    textColor: 'text-slate-700',
    borderColor: 'border-slate-200',
  },
  in_progress: {
    label: 'In Progress',
    color: 'blue',
    bgColor: 'bg-blue-100',
    textColor: 'text-blue-700',
    borderColor: 'border-blue-200',
  },
  completed: {
    label: 'Completed',
    color: 'green',
    bgColor: 'bg-green-100',
    textColor: 'text-green-700',
    borderColor: 'border-green-200',
  },
  cancelled: {
    label: 'Cancelled',
    color: 'red',
    bgColor: 'bg-red-100',
    textColor: 'text-red-700',
    borderColor: 'border-red-200',
  },
} as const;

/**
 * Task priority configuration
 */
export const TASK_PRIORITY_CONFIG: Record<TaskPriorityType, TaskPriorityConfig> = {
  none: {
    label: 'No Priority',
    color: 'slate',
    bgColor: 'bg-slate-50',
    textColor: 'text-slate-500',
    icon: '○',
  },
  low: {
    label: 'Low',
    color: 'blue',
    bgColor: 'bg-blue-50',
    textColor: 'text-blue-600',
    icon: '▽',
  },
  medium: {
    label: 'Medium',
    color: 'amber',
    bgColor: 'bg-amber-50',
    textColor: 'text-amber-600',
    icon: '◇',
  },
  high: {
    label: 'High',
    color: 'red',
    bgColor: 'bg-red-50',
    textColor: 'text-red-600',
    icon: '△',
  },
} as const;

/**
 * Kanban column order
 */
export const KANBAN_COLUMN_ORDER: TaskStatusType[] = [
  'todo',
  'in_progress',
  'completed',
  'cancelled',
] as const;

/**
 * Get status config by status type
 */
export function getStatusConfig(status: TaskStatusType): TaskStatusConfig {
  return TASK_STATUS_CONFIG[status];
}

/**
 * Get priority config by priority type
 */
export function getPriorityConfig(priority: TaskPriorityType): TaskPriorityConfig {
  return TASK_PRIORITY_CONFIG[priority];
}

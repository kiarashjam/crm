import type { TaskItem, Lead, Contact, Deal } from '@/app/api/types';

export type ViewMode = 'list' | 'kanban';
export type TaskGroup = 'overdue' | 'today' | 'tomorrow' | 'thisWeek' | 'later' | 'noDue' | 'completed';
export type KanbanColumn = 'todo' | 'in_progress' | 'completed';

// Re-export
export type { TaskItem, Lead, Contact, Deal };

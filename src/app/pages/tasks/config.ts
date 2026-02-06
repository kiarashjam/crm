import {
  Circle,
  Play,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Target,
  CalendarClock,
  Calendar,
  Clock,
  ListTodo,
  CheckCheck,
} from 'lucide-react';
import type { TaskStatusType, TaskPriorityType } from '@/app/api/types';
import type { TaskGroup, KanbanColumn } from './types';

// Status configuration
export const statusConfig: Record<TaskStatusType, { label: string; color: string; bgColor: string; icon: typeof Circle }> = {
  todo: { label: 'To Do', color: 'text-slate-600', bgColor: 'bg-slate-100', icon: Circle },
  in_progress: { label: 'In Progress', color: 'text-blue-600', bgColor: 'bg-blue-100', icon: Play },
  completed: { label: 'Completed', color: 'text-emerald-600', bgColor: 'bg-emerald-100', icon: CheckCircle2 },
  cancelled: { label: 'Cancelled', color: 'text-slate-400', bgColor: 'bg-slate-100', icon: XCircle },
};

// Priority configuration
export const priorityConfig: Record<TaskPriorityType, { label: string; color: string; bgColor: string; borderColor: string }> = {
  high: { label: 'High', color: 'text-red-600', bgColor: 'bg-red-50', borderColor: 'border-l-red-500' },
  medium: { label: 'Medium', color: 'text-amber-600', bgColor: 'bg-amber-50', borderColor: 'border-l-amber-500' },
  low: { label: 'Low', color: 'text-blue-600', bgColor: 'bg-blue-50', borderColor: 'border-l-blue-500' },
  none: { label: 'No priority', color: 'text-slate-400', bgColor: 'bg-slate-50', borderColor: 'border-l-slate-200' },
};

// Group configuration for list view
export const groupConfig: Record<TaskGroup, { label: string; icon: typeof Clock; color: string; emptyMessage: string }> = {
  overdue: { label: 'Overdue', icon: AlertTriangle, color: 'text-red-600', emptyMessage: 'No overdue tasks' },
  today: { label: 'Today', icon: Target, color: 'text-orange-600', emptyMessage: 'Nothing due today' },
  tomorrow: { label: 'Tomorrow', icon: CalendarClock, color: 'text-blue-600', emptyMessage: 'Nothing due tomorrow' },
  thisWeek: { label: 'This Week', icon: Calendar, color: 'text-purple-600', emptyMessage: 'Nothing due this week' },
  later: { label: 'Later', icon: Clock, color: 'text-slate-600', emptyMessage: 'No upcoming tasks' },
  noDue: { label: 'No Due Date', icon: ListTodo, color: 'text-slate-500', emptyMessage: 'All tasks have due dates' },
  completed: { label: 'Completed', icon: CheckCheck, color: 'text-emerald-600', emptyMessage: 'No completed tasks' },
};

// Kanban column configuration with enhanced styling
export const kanbanColumns: { 
  id: KanbanColumn; 
  status: TaskStatusType; 
  label: string; 
  color: string; 
  bgColor: string; 
  headerBg: string;
  icon: typeof Circle;
  accentColor: string;
  dotColor: string;
}[] = [
  { 
    id: 'todo', 
    status: 'todo', 
    label: 'To Do', 
    color: 'text-slate-700', 
    bgColor: 'bg-gradient-to-b from-slate-50 to-slate-100/50', 
    headerBg: 'bg-gradient-to-r from-slate-100 to-slate-50',
    icon: Circle,
    accentColor: 'border-slate-300',
    dotColor: 'bg-slate-400'
  },
  { 
    id: 'in_progress', 
    status: 'in_progress', 
    label: 'In Progress', 
    color: 'text-blue-700', 
    bgColor: 'bg-gradient-to-b from-blue-50 to-blue-100/50', 
    headerBg: 'bg-gradient-to-r from-blue-100 to-blue-50',
    icon: Play,
    accentColor: 'border-blue-300',
    dotColor: 'bg-blue-500'
  },
  { 
    id: 'completed', 
    status: 'completed', 
    label: 'Done', 
    color: 'text-emerald-700', 
    bgColor: 'bg-gradient-to-b from-emerald-50 to-emerald-100/50', 
    headerBg: 'bg-gradient-to-r from-emerald-100 to-emerald-50',
    icon: CheckCircle2,
    accentColor: 'border-emerald-300',
    dotColor: 'bg-emerald-500'
  },
];

// Drag type for react-dnd
export const TASK_CARD_TYPE = 'TASK_CARD';

// Priority order for sorting
export const priorityOrder: Record<TaskPriorityType, number> = { 
  high: 0, 
  medium: 1, 
  low: 2, 
  none: 3 
};

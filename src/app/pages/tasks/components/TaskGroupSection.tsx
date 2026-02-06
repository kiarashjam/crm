import { ChevronDown, ChevronRight } from 'lucide-react';
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/app/components/ui/collapsible';
import { Badge } from '@/app/components/ui/badge';
import { ListTaskCard } from './ListTaskCard';
import { groupConfig } from '../config';
import type { TaskGroup } from '../types';
import type { TaskItem, TaskStatusType, TaskPriorityType, Contact } from '@/app/api/types';

export interface TaskGroupSectionProps {
  group: TaskGroup;
  tasks: TaskItem[];
  isCollapsed: boolean;
  onToggle: () => void;
  openEdit: (task: TaskItem) => void;
  handleDelete: (task: TaskItem) => void;
  handleStatusChange: (task: TaskItem, newStatus: TaskStatusType) => void;
  handlePriorityChange: (task: TaskItem, newPriority: TaskPriorityType) => void;
  statusConfig: Record<TaskStatusType, { label: string; color: string; bgColor: string; icon: typeof import('lucide-react').Circle }>;
  priorityConfig: Record<TaskPriorityType, { label: string; color: string; bgColor: string; borderColor: string }>;
  contacts: Contact[];
  members: { userId: string; name: string; email: string }[];
  getInitials: (name: string) => string;
  formatDue: (iso: string | undefined) => string | null;
}

export function TaskGroupSection({
  group,
  tasks: groupTasks,
  isCollapsed,
  onToggle,
  openEdit,
  handleDelete,
  handleStatusChange,
  handlePriorityChange,
  statusConfig,
  priorityConfig,
  contacts,
  members,
  getInitials,
  formatDue,
}: TaskGroupSectionProps) {
  const config = groupConfig[group];
  const Icon = config.icon;

  if (groupTasks.length === 0 && group !== 'today') return null;

  return (
    <Collapsible open={!isCollapsed} onOpenChange={onToggle}>
      <CollapsibleTrigger asChild>
        <button type="button" className="w-full flex items-center justify-between py-3 px-1 group/header hover:bg-slate-50/50 rounded-lg transition-colors">
          <div className="flex items-center gap-2">
            {isCollapsed ? <ChevronRight className="w-4 h-4 text-slate-400" /> : <ChevronDown className="w-4 h-4 text-slate-400" />}
            <Icon className={`w-4 h-4 ${config.color}`} />
            <span className={`font-semibold ${config.color}`}>{config.label}</span>
            <Badge variant="secondary" className="ml-1 text-xs">{groupTasks.length}</Badge>
          </div>
        </button>
      </CollapsibleTrigger>
      <CollapsibleContent>
        {groupTasks.length === 0 ? (
          <p className="text-sm text-slate-400 py-4 pl-7">{config.emptyMessage}</p>
        ) : (
          <div className="space-y-2 pb-4">
            {groupTasks.map((task) => (
              <ListTaskCard
                key={task.id}
                task={task}
                openEdit={openEdit}
                handleDelete={handleDelete}
                handleStatusChange={handleStatusChange}
                handlePriorityChange={handlePriorityChange}
                statusConfig={statusConfig}
                priorityConfig={priorityConfig}
                contacts={contacts}
                members={members}
                getInitials={getInitials}
                formatDue={formatDue}
              />
            ))}
          </div>
        )}
      </CollapsibleContent>
    </Collapsible>
  );
}

import {
  Calendar,
  Flag,
  Target,
  Link2,
  MoreHorizontal,
  Pencil,
  Trash2,
  Circle,
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
} from '@/app/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/app/components/ui/avatar';
import type { TaskItem, TaskStatusType, TaskPriorityType, Contact } from '@/app/api/types';

export interface ListTaskCardProps {
  task: TaskItem;
  openEdit: (task: TaskItem) => void;
  handleDelete: (task: TaskItem) => void;
  handleStatusChange: (task: TaskItem, newStatus: TaskStatusType) => void;
  handlePriorityChange: (task: TaskItem, newPriority: TaskPriorityType) => void;
  statusConfig: Record<TaskStatusType, { label: string; color: string; bgColor: string; icon: typeof Circle }>;
  priorityConfig: Record<TaskPriorityType, { label: string; color: string; bgColor: string; borderColor: string }>;
  contacts: Contact[];
  members: { userId: string; name: string; email: string }[];
  getInitials: (name: string) => string;
  formatDue: (iso: string | undefined) => string | null;
}

export function ListTaskCard({
  task,
  openEdit,
  handleDelete,
  handleStatusChange,
  handlePriorityChange,
  statusConfig,
  priorityConfig,
  getInitials,
  formatDue,
}: ListTaskCardProps) {
  const priority = priorityConfig[task.priority || 'none'];
  const status = statusConfig[task.status || 'todo'];
  const now = new Date();
  const isOverdue = task.status !== 'completed' && task.status !== 'cancelled' && task.dueDateUtc && new Date(task.dueDateUtc) < now;
  const StatusIcon = status.icon;

  return (
    <div className={`group relative bg-white rounded-xl border border-slate-200 shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden ${
      task.status === 'completed' || task.status === 'cancelled' ? 'opacity-60' : ''
    }`}>
      <div className={`absolute left-0 top-0 bottom-0 w-1 ${priority.borderColor.replace('border-l-', 'bg-')}`} />

      <div className="flex items-start gap-3 p-4 pl-5">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <button type="button" className="mt-0.5 shrink-0 rounded-full focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 transition-transform hover:scale-110">
              <StatusIcon className={`w-5 h-5 ${status.color}`} />
            </button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="start" className="w-44">
            <DropdownMenuRadioGroup value={task.status} onValueChange={(v) => handleStatusChange(task, v as TaskStatusType)}>
              <DropdownMenuRadioItem value="todo">To Do</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="in_progress">In Progress</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="completed">Completed</DropdownMenuRadioItem>
              <DropdownMenuRadioItem value="cancelled">Cancelled</DropdownMenuRadioItem>
            </DropdownMenuRadioGroup>
          </DropdownMenuContent>
        </DropdownMenu>

        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <p className={`font-medium leading-snug ${
              task.status === 'completed' || task.status === 'cancelled' ? 'text-slate-500 line-through' : 'text-slate-900'
            }`}>
              {task.title}
            </p>

            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="sm" className="h-7 w-7 p-0 opacity-0 group-hover:opacity-100 transition-opacity">
                  <MoreHorizontal className="w-4 h-4" />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end" className="w-48">
                <DropdownMenuItem onClick={() => openEdit(task)}>
                  <Pencil className="w-4 h-4 mr-2" />
                  Edit task
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Flag className="w-4 h-4 mr-2" />
                    Priority
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent>
                    <DropdownMenuRadioGroup value={task.priority || 'none'} onValueChange={(v) => handlePriorityChange(task, v as TaskPriorityType)}>
                      <DropdownMenuRadioItem value="none">No priority</DropdownMenuRadioItem>
                      <DropdownMenuRadioItem value="low">Low</DropdownMenuRadioItem>
                      <DropdownMenuRadioItem value="medium">Medium</DropdownMenuRadioItem>
                      <DropdownMenuRadioItem value="high">High</DropdownMenuRadioItem>
                    </DropdownMenuRadioGroup>
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={() => handleDelete(task)} className="text-red-600">
                  <Trash2 className="w-4 h-4 mr-2" />
                  Delete
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </div>

          {task.description && (
            <p className="text-sm text-slate-500 mt-1 line-clamp-2">{task.description}</p>
          )}

          <div className="flex items-center gap-3 mt-2 flex-wrap">
            <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full ${status.bgColor} ${status.color}`}>
              {status.label}
            </span>

            {task.dueDateUtc && (
              <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full ${
                isOverdue ? 'bg-red-100 text-red-700' : 'bg-slate-100 text-slate-600'
              }`}>
                <Calendar className="w-3 h-3" />
                {formatDue(task.dueDateUtc)}
              </span>
            )}

            {task.priority && task.priority !== 'none' && (
              <span className={`inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full ${priority.bgColor} ${priority.color}`}>
                <Flag className="w-3 h-3" />
                {priority.label}
              </span>
            )}

            {task.leadName && (
              <span className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full bg-purple-50 text-purple-700">
                <Target className="w-3 h-3" />
                {task.leadName}
              </span>
            )}

            {task.dealName && (
              <span className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full bg-teal-50 text-teal-700">
                <Link2 className="w-3 h-3" />
                {task.dealName}
              </span>
            )}

            {task.assigneeName && (
              <span className="inline-flex items-center gap-1.5">
                <Avatar className="w-5 h-5">
                  <AvatarFallback className="text-[10px] bg-orange-100 text-orange-700">
                    {getInitials(task.assigneeName)}
                  </AvatarFallback>
                </Avatar>
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}

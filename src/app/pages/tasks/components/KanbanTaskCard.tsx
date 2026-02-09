import { useNavigate } from 'react-router-dom';
import { useDrag } from 'react-dnd';
import {
  Calendar,
  Flag,
  GripVertical,
  Link2,
  MoreHorizontal,
  Pencil,
  Target,
  Trash2,
  UserPlus,
} from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuSub,
  DropdownMenuSubContent,
  DropdownMenuSubTrigger,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';
import { Avatar, AvatarFallback } from '@/app/components/ui/avatar';
import type { TaskItem, TaskPriorityType, Lead, Deal } from '@/app/api/types';

export interface KanbanTaskCardProps {
  task: TaskItem;
  openEdit: (task: TaskItem) => void;
  handleDelete: (task: TaskItem) => void;
  handlePriorityChange: (task: TaskItem, priority: TaskPriorityType) => void;
  handleAssigneeChange: (task: TaskItem, assigneeId: string | null) => void;
  handleLeadChange: (task: TaskItem, leadId: string | null) => void;
  handleDealChange: (task: TaskItem, dealId: string | null) => void;
  members: Array<{ userId: string; name: string; email: string }>;
  leads: Lead[];
  deals: Deal[];
  priorityConfig: Record<TaskPriorityType, { label: string; color: string; bgColor: string; borderColor: string }>;
  getInitials: (name: string) => string;
  formatDue: (iso: string | undefined) => string | null;
  taskCardType: string;
  onViewDetails?: (task: TaskItem) => void;
}

export function KanbanTaskCard({
  task,
  openEdit,
  handleDelete,
  handlePriorityChange,
  handleAssigneeChange,
  handleLeadChange,
  handleDealChange,
  members,
  leads,
  deals,
  priorityConfig,
  getInitials,
  formatDue,
  taskCardType,
  onViewDetails,
}: KanbanTaskCardProps) {
  const navigate = useNavigate();
  const priority = priorityConfig[task.priority || 'none'];
  const now = new Date();
  const isOverdue = task.status !== 'completed' && task.status !== 'cancelled' && task.dueDateUtc && new Date(task.dueDateUtc) < now;

  const [{ isDragging }, dragRef] = useDrag(() => ({
    type: taskCardType,
    item: { taskId: task.id, fromStatus: task.status },
    collect: (monitor) => ({
      isDragging: monitor.isDragging(),
    }),
  }), [task.id, task.status, taskCardType]);

  const handleCardClick = (e: React.MouseEvent) => {
    // Don't trigger if clicking on the dropdown menu or buttons
    const target = e.target as HTMLElement;
    if (target.closest('[data-radix-collection-item]') || 
        target.closest('button') || 
        target.closest('[role="menu"]') ||
        target.closest('[data-state]')) {
      return;
    }
    if (onViewDetails) {
      onViewDetails(task);
    }
  };

  return (
    <div
      ref={dragRef}
      onClick={handleCardClick}
      className={`group relative bg-white dark:bg-slate-800 rounded-xl border border-slate-200/80 dark:border-slate-700 shadow-sm hover:shadow-lg hover:border-slate-300 dark:hover:border-slate-600 transition-all duration-200 cursor-pointer overflow-hidden ${
        isDragging ? 'opacity-60 scale-[0.98] rotate-1 shadow-2xl ring-2 ring-orange-400/50 cursor-grabbing' : ''
      } ${task.status === 'completed' ? 'opacity-60' : ''}`}
    >
      {/* Priority indicator - left edge */}
      <div className={`absolute left-0 top-0 bottom-0 w-1 ${priority.borderColor.replace('border-l-', 'bg-')}`} />
      
      {/* Drag handle indicator */}
      <div className="absolute right-2 top-2 opacity-0 group-hover:opacity-40 transition-opacity">
        <GripVertical className="w-4 h-4 text-slate-400" />
      </div>
      
      <div className="p-4 pl-4">
        <div className="flex items-start justify-between gap-2">
          <p className={`text-sm font-medium leading-snug ${
            task.status === 'completed' ? 'text-slate-500 dark:text-slate-400 line-through' : 'text-slate-900 dark:text-slate-100'
          }`}>
            {task.title}
          </p>
          
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="sm" className="h-6 w-6 p-0 opacity-0 group-hover:opacity-100">
                <MoreHorizontal className="w-4 h-4" />
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end" className="w-48">
              <DropdownMenuItem onClick={() => openEdit(task)}>
                <Pencil className="w-4 h-4 mr-2" />
                Edit
              </DropdownMenuItem>
              <DropdownMenuSub>
                <DropdownMenuSubTrigger>
                  <Flag className={`w-4 h-4 mr-2 ${priority.color}`} />
                  Priority
                </DropdownMenuSubTrigger>
                <DropdownMenuSubContent>
                  <DropdownMenuRadioGroup value={task.priority} onValueChange={(v) => handlePriorityChange(task, v as TaskPriorityType)}>
                    <DropdownMenuRadioItem value="high">High</DropdownMenuRadioItem>
                    <DropdownMenuRadioItem value="medium">Medium</DropdownMenuRadioItem>
                    <DropdownMenuRadioItem value="low">Low</DropdownMenuRadioItem>
                    <DropdownMenuRadioItem value="none">None</DropdownMenuRadioItem>
                  </DropdownMenuRadioGroup>
                </DropdownMenuSubContent>
              </DropdownMenuSub>
              {members.length > 0 && (
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <UserPlus className="w-4 h-4 mr-2" />
                    Assign
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent>
                    <DropdownMenuItem onClick={() => handleAssigneeChange(task, null)}>Unassigned</DropdownMenuItem>
                    <DropdownMenuSeparator />
                    {members.map((m) => (
                      <DropdownMenuItem key={m.userId} onClick={() => handleAssigneeChange(task, m.userId)}>
                        {m.name}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
              )}
              {leads.length > 0 && (
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Target className="w-4 h-4 mr-2" />
                    Lead
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent className="max-h-48 overflow-y-auto">
                    <DropdownMenuItem onClick={() => handleLeadChange(task, null)}>No lead</DropdownMenuItem>
                    <DropdownMenuSeparator />
                    {leads.map((l) => (
                      <DropdownMenuItem key={l.id} onClick={() => handleLeadChange(task, l.id)}>
                        {l.name}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
              )}
              {deals.length > 0 && (
                <DropdownMenuSub>
                  <DropdownMenuSubTrigger>
                    <Link2 className="w-4 h-4 mr-2" />
                    Deal
                  </DropdownMenuSubTrigger>
                  <DropdownMenuSubContent className="max-h-48 overflow-y-auto">
                    <DropdownMenuItem onClick={() => handleDealChange(task, null)}>No deal</DropdownMenuItem>
                    <DropdownMenuSeparator />
                    {deals.map((d) => (
                      <DropdownMenuItem key={d.id} onClick={() => handleDealChange(task, d.id)}>
                        {d.name}
                      </DropdownMenuItem>
                    ))}
                  </DropdownMenuSubContent>
                </DropdownMenuSub>
              )}
              <DropdownMenuSeparator />
              <DropdownMenuItem onClick={() => handleDelete(task)} className="text-red-600">
                <Trash2 className="w-4 h-4 mr-2" />
                Delete
              </DropdownMenuItem>
            </DropdownMenuContent>
          </DropdownMenu>
        </div>

        {task.description && (
          <p className="text-xs text-slate-500 dark:text-slate-400 mt-2 line-clamp-2 leading-relaxed">{task.description}</p>
        )}

        {/* Meta info */}
        <div className="flex items-center gap-1.5 mt-3 flex-wrap">
          {task.dueDateUtc && (
            <span className={`inline-flex items-center gap-1 text-[11px] font-medium px-2 py-1 rounded-lg ${
              isOverdue ? 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400 border border-red-200 dark:border-red-800' : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-600'
            }`}>
              <Calendar className="w-3 h-3" />
              {formatDue(task.dueDateUtc)}
            </span>
          )}

          {task.priority && task.priority !== 'none' && (
            <span className={`inline-flex items-center gap-1 text-[11px] font-medium px-2 py-1 rounded-lg border ${priority.bgColor} ${priority.color}`}>
              <Flag className="w-3 h-3" />
              {priority.label}
            </span>
          )}

          {task.leadName && (
            <span className="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-1 rounded-lg bg-purple-50 dark:bg-purple-900/30 text-purple-700 dark:text-purple-400 border border-purple-200 dark:border-purple-800">
              <Target className="w-3 h-3" />
              <span className="max-w-[80px] truncate">{task.leadName}</span>
            </span>
          )}

          {task.dealName && task.dealId && (
            <span
              onClick={(e) => { e.stopPropagation(); navigate(`/deals/${task.dealId}`); }}
              className="inline-flex items-center gap-1 text-[11px] font-medium px-2 py-1 rounded-lg bg-teal-50 dark:bg-teal-900/30 text-teal-700 dark:text-teal-400 border border-teal-200 dark:border-teal-800 cursor-pointer hover:bg-teal-100 dark:hover:bg-teal-900/50 transition-colors"
            >
              <Link2 className="w-3 h-3" />
              <span className="max-w-[80px] truncate">{task.dealName}</span>
            </span>
          )}
        </div>

        {/* Assignee */}
        {task.assigneeName && (
          <div className="flex items-center gap-2 mt-3 pt-3 border-t border-slate-100 dark:border-slate-700">
            <Avatar className="w-6 h-6 ring-2 ring-white dark:ring-slate-800 shadow-sm">
              <AvatarFallback className="text-[10px] font-semibold bg-gradient-to-br from-orange-100 to-orange-200 dark:from-orange-900/50 dark:to-orange-800/50 text-orange-700 dark:text-orange-400">
                {getInitials(task.assigneeName)}
              </AvatarFallback>
            </Avatar>
            <span className="text-xs text-slate-600 dark:text-slate-400 font-medium">{task.assigneeName}</span>
          </div>
        )}
      </div>
    </div>
  );
}

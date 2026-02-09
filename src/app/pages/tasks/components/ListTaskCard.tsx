import { useNavigate } from 'react-router-dom';
import {
  Calendar,
  Flag,
  Target,
  Link2,
  MoreHorizontal,
  Pencil,
  Trash2,
  Circle,
  User,
  Briefcase,
  UserX,
  LinkIcon,
  Unlink,
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
import type { TaskItem, TaskStatusType, TaskPriorityType, Contact, Lead, Deal } from '@/app/api/types';

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
  onViewDetails?: (task: TaskItem) => void;
  // HP-6: Callbacks for deal/lead/assignee actions from dropdown
  onAssigneeChange?: (task: TaskItem, assigneeId: string | null) => void;
  onDealChange?: (task: TaskItem, dealId: string | null) => void;
  onLeadChange?: (task: TaskItem, leadId: string | null) => void;
  leads?: Lead[];
  deals?: Deal[];
  // HP-9: Bulk selection
  isSelected?: boolean;
  onSelectionToggle?: (taskId: string) => void;
  bulkMode?: boolean;
}

export function ListTaskCard({
  task,
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
  onViewDetails,
  onAssigneeChange,
  onDealChange,
  onLeadChange,
  leads = [],
  deals = [],
  isSelected,
  onSelectionToggle,
  bulkMode,
}: ListTaskCardProps) {
  const navigate = useNavigate();
  const priority = priorityConfig[task.priority || 'none'];
  const status = statusConfig[task.status || 'todo'];
  const now = new Date();
  const isOverdue = task.status !== 'completed' && task.status !== 'cancelled' && task.dueDateUtc && new Date(task.dueDateUtc) < now;
  const StatusIcon = status.icon;

  const handleCardClick = (e: React.MouseEvent) => {
    const target = e.target as HTMLElement;
    if (target.closest('[data-radix-collection-item]') || 
        target.closest('button') || 
        target.closest('[role="menu"]') ||
        target.closest('[data-state]') ||
        target.closest('[data-badge-link]')) {
      return;
    }
    if (bulkMode && onSelectionToggle) {
      onSelectionToggle(task.id);
      return;
    }
    if (onViewDetails) {
      onViewDetails(task);
    }
  };

  return (
    <div 
      onClick={handleCardClick}
      className={`group relative bg-white rounded-xl border shadow-sm hover:shadow-md transition-all duration-200 overflow-hidden cursor-pointer ${
        isSelected ? 'border-orange-400 ring-2 ring-orange-200 bg-orange-50/30' : 'border-slate-200'
      } ${task.status === 'completed' || task.status === 'cancelled' ? 'opacity-60' : ''}`}
    >
      <div className={`absolute left-0 top-0 bottom-0 w-1 ${priority.borderColor.replace('border-l-', 'bg-')}`} />

      <div className="flex items-start gap-3 p-4 pl-5">
        {/* HP-9: Bulk selection checkbox */}
        {bulkMode && (
          <button
            type="button"
            onClick={(e) => { e.stopPropagation(); onSelectionToggle?.(task.id); }}
            className={`mt-0.5 shrink-0 w-5 h-5 rounded border-2 flex items-center justify-center transition-colors ${
              isSelected 
                ? 'bg-orange-500 border-orange-500 text-white' 
                : 'border-slate-300 hover:border-orange-400'
            }`}
          >
            {isSelected && <svg className="w-3 h-3" viewBox="0 0 12 12" fill="none"><path d="M2 6l3 3 5-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>}
          </button>
        )}

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
              <DropdownMenuContent align="end" className="w-52">
                <DropdownMenuItem onClick={() => openEdit(task)}>
                  <Pencil className="w-4 h-4 mr-2" />
                  Edit task
                </DropdownMenuItem>
                <DropdownMenuSeparator />

                {/* HP-6: Priority submenu */}
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

                {/* HP-6: Assignee submenu */}
                {onAssigneeChange && members.length > 0 && (
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <User className="w-4 h-4 mr-2" />
                      Assignee
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="max-h-48 overflow-y-auto">
                      <DropdownMenuItem onClick={() => onAssigneeChange(task, null)}>
                        <UserX className="w-4 h-4 mr-2 text-slate-400" />
                        Unassign
                      </DropdownMenuItem>
                      <DropdownMenuSeparator />
                      {members.map((m) => (
                        <DropdownMenuItem key={m.userId} onClick={() => onAssigneeChange(task, m.userId)}>
                          <Avatar className="w-4 h-4 mr-2">
                            <AvatarFallback className="text-[8px] bg-orange-100 text-orange-700">
                              {getInitials(m.name)}
                            </AvatarFallback>
                          </Avatar>
                          <span className="truncate">{m.name}</span>
                          {task.assigneeId === m.userId && <span className="ml-auto text-orange-500 text-xs">current</span>}
                        </DropdownMenuItem>
                      ))}
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>
                )}

                {/* HP-6: Deal link submenu */}
                {onDealChange && deals.length > 0 && (
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <Briefcase className="w-4 h-4 mr-2" />
                      Link Deal
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="max-h-48 overflow-y-auto">
                      {task.dealId && (
                        <>
                          <DropdownMenuItem onClick={() => onDealChange(task, null)}>
                            <Unlink className="w-4 h-4 mr-2 text-slate-400" />
                            Unlink deal
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                        </>
                      )}
                      {deals.map((d) => (
                        <DropdownMenuItem key={d.id} onClick={() => onDealChange(task, d.id)}>
                          <LinkIcon className="w-4 h-4 mr-2 text-teal-500" />
                          <span className="truncate">{d.name}</span>
                          {task.dealId === d.id && <span className="ml-auto text-teal-500 text-xs">linked</span>}
                        </DropdownMenuItem>
                      ))}
                    </DropdownMenuSubContent>
                  </DropdownMenuSub>
                )}

                {/* HP-6: Lead link submenu */}
                {onLeadChange && leads.length > 0 && (
                  <DropdownMenuSub>
                    <DropdownMenuSubTrigger>
                      <Target className="w-4 h-4 mr-2" />
                      Link Lead
                    </DropdownMenuSubTrigger>
                    <DropdownMenuSubContent className="max-h-48 overflow-y-auto">
                      {task.leadId && (
                        <>
                          <DropdownMenuItem onClick={() => onLeadChange(task, null)}>
                            <Unlink className="w-4 h-4 mr-2 text-slate-400" />
                            Unlink lead
                          </DropdownMenuItem>
                          <DropdownMenuSeparator />
                        </>
                      )}
                      {leads.map((l) => (
                        <DropdownMenuItem key={l.id} onClick={() => onLeadChange(task, l.id)}>
                          <LinkIcon className="w-4 h-4 mr-2 text-purple-500" />
                          <span className="truncate">{l.name}</span>
                          {task.leadId === l.id && <span className="ml-auto text-purple-500 text-xs">linked</span>}
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

            {/* HP-7: Clickable lead badge for navigation */}
            {task.leadName && (
              <button
                data-badge-link
                type="button"
                onClick={(e) => { e.stopPropagation(); navigate(`/leads`); }}
                className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full bg-purple-50 text-purple-700 hover:bg-purple-100 transition-colors cursor-pointer"
                title={`Go to leads (${task.leadName})`}
              >
                <Target className="w-3 h-3" />
                {task.leadName}
              </button>
            )}

            {/* HP-7: Clickable deal badge — navigates to deal detail page */}
            {task.dealName && task.dealId && (
              <button
                data-badge-link
                type="button"
                onClick={(e) => { e.stopPropagation(); navigate(`/deals/${task.dealId}`); }}
                className="inline-flex items-center gap-1 text-xs font-medium px-2 py-0.5 rounded-full bg-teal-50 text-teal-700 hover:bg-teal-100 transition-colors cursor-pointer"
                title={`Open deal: ${task.dealName}`}
              >
                <Link2 className="w-3 h-3" />
                {task.dealName}
              </button>
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

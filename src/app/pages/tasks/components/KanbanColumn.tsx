import { useDrop } from 'react-dnd';
import { Plus } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Badge } from '@/app/components/ui/badge';
import { ScrollArea } from '@/app/components/ui/scroll-area';
import type { TaskItem, TaskStatusType } from '@/app/api/types';

export type KanbanColumn = 'todo' | 'in_progress' | 'completed';

export const TASK_CARD_TYPE = 'TASK_CARD';

export interface KanbanColumnConfig {
  id: KanbanColumn;
  status: TaskStatusType;
  label: string;
  color: string;
  bgColor: string;
  headerBg: string;
  icon: React.ComponentType<{ className?: string }>;
  accentColor: string;
  dotColor: string;
}

export interface KanbanColumnProps {
  column: KanbanColumnConfig;
  tasks: TaskItem[];
  onDrop: (taskId: string, columnId: KanbanColumn) => void;
  quickAddColumn: KanbanColumn | null;
  quickAddTitle: string;
  setQuickAddTitle: (title: string) => void;
  setQuickAddColumn: (columnId: KanbanColumn | null) => void;
  handleQuickAdd: (columnId: KanbanColumn) => void;
  isQuickAdding: boolean;
  onOpenCreate: (status?: TaskStatusType) => void;
  KanbanTaskCard: React.ComponentType<{ task: TaskItem }>;
}

export function KanbanColumnComponent({
  column,
  tasks,
  onDrop,
  quickAddColumn,
  quickAddTitle,
  setQuickAddTitle,
  setQuickAddColumn,
  handleQuickAdd,
  isQuickAdding,
  onOpenCreate,
  KanbanTaskCard,
}: KanbanColumnProps) {
  const ColumnIcon = column.icon;

  const [{ isOver, canDrop }, dropRef] = useDrop(() => ({
    accept: TASK_CARD_TYPE,
    drop: (item: { taskId: string; fromStatus: TaskStatusType }) => {
      onDrop(item.taskId, column.id);
    },
    canDrop: (item: { taskId: string; fromStatus: TaskStatusType }) => {
      // Allow drop if it's a different status
      const statusMap: Record<KanbanColumn, TaskStatusType> = {
        todo: 'todo',
        in_progress: 'in_progress',
        completed: 'completed',
      };
      return item.fromStatus !== statusMap[column.id];
    },
    collect: (monitor) => ({
      isOver: monitor.isOver(),
      canDrop: monitor.canDrop(),
    }),
  }), [column.id, onDrop]);

  const showDropIndicator = isOver && canDrop;

  return (
    <div className="flex flex-col flex-1 min-w-0">
      {/* Column Header */}
      <div className={`flex items-center justify-between px-4 py-3 rounded-t-2xl border-b ${column.accentColor} ${column.headerBg}`}>
        <div className="flex items-center gap-3">
          <div className={`w-2.5 h-2.5 rounded-full ${column.dotColor}`} />
          <div className="flex items-center gap-2">
            <ColumnIcon className={`w-4 h-4 ${column.color}`} />
            <span className={`font-semibold text-sm ${column.color}`}>{column.label}</span>
          </div>
          <Badge variant="secondary" className="text-xs px-2 py-0.5 font-medium bg-white/60 dark:bg-slate-700/60 border border-slate-200/50 dark:border-slate-600/50">
            {tasks.length}
          </Badge>
        </div>
        <Button
          variant="ghost"
          size="sm"
          className={`h-8 w-8 p-0 rounded-lg hover:bg-white/80 ${column.color}`}
          onClick={() => onOpenCreate(column.status)}
        >
          <Plus className="w-4 h-4" />
        </Button>
      </div>

      {/* Column Content */}
      <div
        ref={dropRef}
        className={`flex-1 p-3 rounded-b-2xl transition-all duration-300 min-h-[400px] border border-t-0 ${column.accentColor} ${column.bgColor} ${
          showDropIndicator 
            ? 'ring-2 ring-orange-400 ring-inset bg-orange-50/90 scale-[1.01] shadow-lg' 
            : isOver && !canDrop
            ? 'ring-2 ring-slate-300/50 ring-inset'
            : ''
        }`}
      >
        <ScrollArea className="h-[calc(100vh-400px)]">
          <div className="space-y-3 pr-2">
            {/* Quick add input */}
            {quickAddColumn === column.id ? (
              <div className="bg-white dark:bg-slate-800 rounded-xl border border-slate-200 dark:border-slate-700 shadow-md p-3">
                <Input
                  autoFocus
                  value={quickAddTitle}
                  onChange={(e) => setQuickAddTitle(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && quickAddTitle.trim()) handleQuickAdd(column.id);
                    if (e.key === 'Escape') {
                      setQuickAddColumn(null);
                      setQuickAddTitle('');
                    }
                  }}
                  onBlur={() => {
                    if (!quickAddTitle.trim()) {
                      setQuickAddColumn(null);
                    }
                  }}
                  placeholder="What needs to be done?"
                  className="h-9 text-sm border-slate-200 dark:border-slate-600 focus:border-orange-300 dark:focus:border-orange-600 focus:ring-orange-200 dark:focus:ring-orange-800"
                />
                <div className="flex items-center gap-2 mt-3">
                  <Button
                    size="sm"
                    className="h-8 px-4 text-xs bg-orange-600 hover:bg-orange-500 dark:bg-orange-500 dark:hover:bg-orange-400 rounded-lg"
                    onClick={() => handleQuickAdd(column.id)}
                    disabled={!quickAddTitle.trim() || isQuickAdding}
                  >
                    <Plus className="w-3.5 h-3.5 mr-1" />
                    Add Task
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-8 px-3 text-xs rounded-lg"
                    onClick={() => {
                      setQuickAddColumn(null);
                      setQuickAddTitle('');
                    }}
                  >
                    Cancel
                  </Button>
                </div>
              </div>
            ) : (
              <button
                type="button"
                onClick={() => setQuickAddColumn(column.id)}
                className="w-full text-left px-4 py-3 text-sm text-slate-500 dark:text-slate-400 hover:text-slate-700 dark:hover:text-slate-200 bg-white/40 dark:bg-slate-800/40 hover:bg-white/80 dark:hover:bg-slate-700/80 border border-dashed border-slate-300 dark:border-slate-600 hover:border-slate-400 dark:hover:border-slate-500 rounded-xl transition-all duration-200 flex items-center gap-2 group"
              >
                <Plus className="w-4 h-4 transition-transform group-hover:rotate-90" />
                <span>Add task</span>
              </button>
            )}

            {/* Task cards */}
            {tasks.map((task) => (
              <KanbanTaskCard key={task.id} task={task} />
            ))}

            {tasks.length === 0 && quickAddColumn !== column.id && (
              <div className="flex flex-col items-center justify-center py-12 text-center">
                <div className={`w-12 h-12 rounded-full ${column.bgColor} flex items-center justify-center mb-3 border ${column.accentColor}`}>
                  <ColumnIcon className={`w-6 h-6 ${column.color} opacity-60`} />
                </div>
                <p className="text-sm font-medium text-slate-500 dark:text-slate-400">No tasks here</p>
                <p className="text-xs text-slate-400 dark:text-slate-500 mt-1">Drag tasks here or click add</p>
              </div>
            )}
          </div>
        </ScrollArea>
      </div>
    </div>
  );
}

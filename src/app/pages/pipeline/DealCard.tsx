import { useDrag } from 'react-dnd';
import {
  GripVertical,
  User,
  Clock,
  Pencil,
  Trash2,
  Calendar,
  AlertCircle,
  ListTodo,
  Plus,
} from 'lucide-react';
import { motion } from 'motion/react';
import { Button } from '@/app/components/ui/button';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import type { Deal } from './types';

export const DEAL_CARD_TYPE = 'DEAL_CARD';

/** Returns a currency symbol string for a given currency code. */
export function getCurrencySymbol(code?: string): string {
  switch (code?.toUpperCase()) {
    case 'USD': return '$';
    case 'EUR': return '\u20AC';
    case 'GBP': return '\u00A3';
    case 'CHF': return 'CHF';
    case 'JPY': return '\u00A5';
    case 'CAD': return 'C$';
    case 'AUD': return 'A$';
    default: return code || '$';
  }
}

export const STAGE_COLORS: Record<string, { bar: string; accent: string; bg: string; border: string }> = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', bg: 'bg-blue-500/10', border: 'border-l-blue-500' },
  Proposal: { bar: '#f59e0b', accent: 'text-amber-600', bg: 'bg-amber-500/10', border: 'border-l-amber-500' },
  Negotiation: { bar: '#8b5cf6', accent: 'text-violet-600', bg: 'bg-violet-500/10', border: 'border-l-violet-500' },
  'Closed Won': { bar: '#10b981', accent: 'text-emerald-600', bg: 'bg-emerald-500/10', border: 'border-l-emerald-500' },
  'Closed Lost': { bar: '#64748b', accent: 'text-slate-500', bg: 'bg-slate-500/10', border: 'border-l-slate-400' },
};

export function formatLastActivity(iso: string | undefined): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return '';
  }
}

export function getDaysUntilClose(iso: string | undefined): number | null {
  if (!iso) return null;
  try {
    const d = new Date(iso);
    d.setHours(0, 0, 0, 0);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    return Math.ceil((d.getTime() - today.getTime()) / (1000 * 60 * 60 * 24));
  } catch {
    return null;
  }
}

export function UrgencyBadge({ days }: { days: number }) {
  if (days < 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-red-500/15 dark:bg-red-900/30 px-2 py-0.5 text-xs font-medium text-red-700 dark:text-red-400">
        <AlertCircle className="h-3 w-3" /> Overdue
      </span>
    );
  }
  if (days <= 7) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-amber-500/15 dark:bg-amber-900/30 px-2 py-0.5 text-xs font-medium text-amber-700 dark:text-amber-400">
        <Calendar className="h-3 w-3" /> Due in {days}d
      </span>
    );
  }
  if (days <= 30) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-slate-500/10 dark:bg-slate-700 px-2 py-0.5 text-xs font-medium text-slate-600 dark:text-slate-400">
        <Calendar className="h-3 w-3" /> {days}d to close
      </span>
    );
  }
  return null;
}

export interface DealCardProps {
  deal: Deal;
  contactName: string | undefined;
  stageId: string;
  stageName: string;
  onMoveStage: (dealId: string, stageId: string, stageName: string) => void;
  onEdit: () => void;
  onDelete: () => void;
  onOpenDetail: () => void;
  stageList: { id: string; name: string }[];
  isDragging?: boolean;
  taskCount?: number;
  onAddTask?: (dealId: string) => void;
}

export function DealCard({
  deal,
  contactName,
  stageId,
  stageName,
  onMoveStage,
  onEdit,
  onDelete,
  onOpenDetail,
  stageList,
  isDragging,
  taskCount,
  onAddTask,
}: DealCardProps) {
  const lastActivity = formatLastActivity(deal.lastActivityAtUtc);
  const daysToClose = getDaysUntilClose(deal.expectedCloseDateUtc);

  const [{ isDragging: drag }, dragRef] = useDrag(() => ({
    type: DEAL_CARD_TYPE,
    item: { dealId: deal.id, fromStageId: stageId },
    collect: (monitor) => ({ isDragging: monitor.isDragging() }),
  }), [deal.id, stageId]);

  const isActuallyDragging = isDragging ?? drag;

  return (
    <motion.div
      ref={dragRef}
      layout
      initial={{ opacity: 0, y: 8 }}
      animate={{ opacity: 1, y: 0 }}
      exit={{ opacity: 0, scale: 0.95, y: -10 }}
      whileHover={{ y: -2 }}
      className={cn(
        'group cursor-grab active:cursor-grabbing rounded-2xl border-2 bg-white dark:bg-slate-800 shadow-md transition-all duration-200 overflow-hidden',
        isActuallyDragging 
          ? 'opacity-70 shadow-2xl rotate-2 scale-105 border-emerald-400 dark:border-emerald-500 ring-4 ring-emerald-200/50 dark:ring-emerald-800/50' 
          : 'border-slate-200/80 dark:border-slate-700 hover:shadow-xl hover:border-slate-300 dark:hover:border-slate-600'
      )}
    >
      {/* Top Color Bar */}
      <div className={cn(
        'h-1',
        stageName === 'Closed Won' && 'bg-gradient-to-r from-emerald-400 to-green-400',
        stageName === 'Closed Lost' && 'bg-gradient-to-r from-slate-400 to-slate-500',
        stageName === 'Qualification' && 'bg-gradient-to-r from-blue-400 to-cyan-400',
        stageName === 'Proposal' && 'bg-gradient-to-r from-amber-400 to-orange-400',
        stageName === 'Negotiation' && 'bg-gradient-to-r from-violet-400 to-purple-400',
        !['Closed Won', 'Closed Lost', 'Qualification', 'Proposal', 'Negotiation'].includes(stageName) && 'bg-gradient-to-r from-slate-400 to-slate-500'
      )} />
      
      <div className="p-4">
        {/* Header with Drag Handle */}
        <div className="flex items-start gap-3">
          <div className="mt-1 shrink-0 text-slate-300 dark:text-slate-600 group-hover:text-slate-500 dark:group-hover:text-slate-400 transition-colors cursor-grab active:cursor-grabbing">
            <GripVertical className="w-5 h-5" />
          </div>
          
          <div className="flex-1 min-w-0">
            {/* Deal Name */}
            <button
              type="button"
              onClick={onOpenDetail}
              className="text-left w-full font-bold text-slate-800 dark:text-slate-100 text-sm leading-tight hover:text-emerald-600 dark:hover:text-emerald-400 transition-colors line-clamp-2"
              title={deal.name}
            >
              {deal.name}
            </button>
            
            {/* Value Badge — HP-3: correct currency symbol */}
            <div className="mt-3 flex flex-wrap items-center gap-2">
              <span className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-emerald-500/10 to-teal-500/10 dark:from-emerald-900/30 dark:to-teal-900/30 border border-emerald-200 dark:border-emerald-800 px-3 py-1.5 text-sm font-bold text-emerald-700 dark:text-emerald-400">
                <span className="text-xs font-semibold">{getCurrencySymbol(deal.currency)}</span>
                {deal.value}
              </span>
              {daysToClose !== null && <UrgencyBadge days={daysToClose} />}
            </div>
            
            {/* Contact Info */}
            {(contactName || deal.contactName) && (
              <div className="mt-3 flex items-center gap-2 text-xs text-slate-500 dark:text-slate-400">
                <div className="w-6 h-6 rounded-full bg-slate-100 dark:bg-slate-700 flex items-center justify-center">
                  <User className="w-3.5 h-3.5 text-slate-500 dark:text-slate-400" />
                </div>
                <span className="truncate font-medium" title={contactName || deal.contactName}>{contactName || deal.contactName}</span>
              </div>
            )}

            {/* Assignee — HP-7 */}
            {deal.assigneeName && (
              <div className="mt-1.5 flex items-center gap-2 text-xs text-slate-400 dark:text-slate-500">
                <div className="w-5 h-5 rounded-full bg-blue-100 dark:bg-blue-900/40 flex items-center justify-center">
                  <User className="w-3 h-3 text-blue-500 dark:text-blue-400" />
                </div>
                <span className="truncate" title={deal.assigneeName}>{deal.assigneeName}</span>
              </div>
            )}
            
            {/* HP-7: Task count + HP-8: Add Task */}
            {(taskCount !== undefined || onAddTask) && (
              <div className="mt-2 flex items-center gap-2 text-xs text-slate-400 dark:text-slate-500">
                <ListTodo className="w-3 h-3" />
                <span>{taskCount ?? 0} task{taskCount !== 1 ? 's' : ''}</span>
                {onAddTask && (
                  <button
                    onClick={(e) => { e.stopPropagation(); onAddTask(deal.id); }}
                    className="ml-auto flex items-center gap-1 text-orange-500 hover:text-orange-400 transition-colors"
                    title="Add task to this deal"
                  >
                    <Plus className="w-3 h-3" />
                    <span className="text-[10px] font-medium">Task</span>
                  </button>
                )}
              </div>
            )}

            {/* Last Activity */}
            {lastActivity && (
              <div className="mt-2 flex items-center gap-1.5 text-xs text-slate-400 dark:text-slate-500">
                <Clock className="w-3 h-3" />
                <span>Last activity: {lastActivity}</span>
              </div>
            )}
          </div>
          
          {/* Action Buttons */}
          <div className="flex flex-col gap-1 opacity-0 group-hover:opacity-100 transition-all shrink-0">
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 rounded-lg text-slate-400 hover:text-emerald-600 hover:bg-emerald-50 dark:hover:bg-emerald-900/30 transition-all"
              onClick={(e) => { e.stopPropagation(); onEdit(); }}
              aria-label="Edit deal"
            >
              <Pencil className="w-4 h-4" />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon"
              className="h-8 w-8 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/30 transition-all"
              onClick={(e) => { e.stopPropagation(); onDelete(); }}
              aria-label="Delete deal"
            >
              <Trash2 className="w-4 h-4" />
            </Button>
          </div>
        </div>
        
        {/* Stage Selector */}
        <div className="mt-4 pt-3 border-t border-slate-100 dark:border-slate-700">
          <Select
            value={stageId}
            onValueChange={(v) => {
              const s = stageList.find((x) => x.id === v);
              if (s) onMoveStage(deal.id, s.id, s.name);
            }}
          >
            <SelectTrigger 
              className="h-10 text-xs border-2 border-slate-200 dark:border-slate-600 bg-slate-50/80 dark:bg-slate-700/80 hover:bg-slate-100 dark:hover:bg-slate-600 hover:border-slate-300 dark:hover:border-slate-500 rounded-xl transition-all" 
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex items-center gap-2">
                <span 
                  className="w-2.5 h-2.5 rounded-full"
                  style={{ backgroundColor: STAGE_COLORS[stageName]?.bar ?? '#94a3b8' }}
                />
                <SelectValue />
              </div>
            </SelectTrigger>
            <SelectContent className="rounded-xl border-2 border-slate-200 dark:border-slate-600">
              {stageList.map((s) => (
                <SelectItem key={s.id} value={s.id} className="rounded-lg">
                  <span className="flex items-center gap-2">
                    <span 
                      className="w-2.5 h-2.5 rounded-full"
                      style={{ backgroundColor: STAGE_COLORS[s.name]?.bar ?? '#94a3b8' }}
                    />
                    {s.name}
                  </span>
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
      </div>
    </motion.div>
  );
}

import { useDrop } from 'react-dnd';
import {
  Briefcase,
  Target,
  Handshake,
  Trophy,
  XCircle,
} from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { cn } from '@/app/components/ui/utils';
import type { Deal, Contact } from './types';
import { DealCard } from './DealCard';

// Drag type constant for deal cards
export const DEAL_CARD_TYPE = 'DEAL_CARD';

// Stage color configurations
const STAGE_COLORS: Record<string, { bar: string; accent: string; bg: string; border: string }> = {
  Qualification: { bar: '#3b82f6', accent: 'text-blue-600', bg: 'bg-blue-500/10', border: 'border-l-blue-500' },
  Proposal: { bar: '#f59e0b', accent: 'text-amber-600', bg: 'bg-amber-500/10', border: 'border-l-amber-500' },
  Negotiation: { bar: '#8b5cf6', accent: 'text-violet-600', bg: 'bg-violet-500/10', border: 'border-l-violet-500' },
  'Closed Won': { bar: '#10b981', accent: 'text-emerald-600', bg: 'bg-emerald-500/10', border: 'border-l-emerald-500' },
  'Closed Lost': { bar: '#64748b', accent: 'text-slate-500', bg: 'bg-slate-500/10', border: 'border-l-slate-400' },
};

// Stage icons mapping
const STAGE_ICONS: Record<string, typeof Briefcase> = {
  Qualification: Target,
  Proposal: Briefcase,
  Negotiation: Handshake,
  'Closed Won': Trophy,
  'Closed Lost': XCircle,
};

// Format currency sum from array of values
function formatValueSum(values: string[]): string {
  const sum = values.reduce((acc, v) => {
    const num = parseFloat(String(v).replace(/[^0-9.-]/g, '')) || 0;
    return acc + num;
  }, 0);
  return new Intl.NumberFormat(undefined, { style: 'currency', currency: 'USD', maximumFractionDigits: 0 }).format(sum);
}

export interface DroppableStageColumnProps {
  stageId: string;
  stageName: string;
  deals: Deal[];
  contacts: Contact[];
  stageList: { id: string; name: string }[];
  onMoveStage: (dealId: string, stageId: string, stageName: string) => void;
  onEdit: (deal: Deal) => void;
  onDelete: (deal: Deal) => void;
  onOpenDetail: (deal: Deal) => void;
}

export function DroppableStageColumn({
  stageId,
  stageName,
  deals,
  contacts,
  stageList,
  onMoveStage,
  onEdit,
  onDelete,
  onOpenDetail,
}: DroppableStageColumnProps) {
  const config = STAGE_COLORS[stageName] ?? STAGE_COLORS.Qualification;
  const StageIcon = STAGE_ICONS[stageName] ?? Briefcase;
  const totalValue = formatValueSum(deals.map((d) => d.value));

  const [{ isOver: dropIsOver }, dropRef] = useDrop(
    () => ({
      accept: DEAL_CARD_TYPE,
      drop: (item: { dealId: string; fromStageId: string }) => {
        if (item.fromStageId !== stageId) {
          onMoveStage(item.dealId, stageId, stageName);
        }
      },
      collect: (monitor) => ({ isOver: monitor.isOver() }),
    }),
    [stageId, stageName, onMoveStage]
  );

  const showDropZone = dropIsOver;

  return (
    <motion.div
      ref={dropRef}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
      className={cn(
        'flex flex-col min-w-[320px] w-[320px] h-full max-h-full flex-shrink-0 rounded-2xl border-2 bg-white/90 backdrop-blur-sm overflow-hidden transition-all duration-300 shadow-lg',
        showDropZone 
          ? 'ring-4 ring-emerald-400/50 border-emerald-400 bg-emerald-50/50 scale-[1.02] shadow-2xl shadow-emerald-200/50' 
          : 'border-slate-200/80 shadow-slate-100 hover:shadow-xl hover:border-slate-300'
      )}
    >
      {/* Column Header */}
      <div className={cn(
        'shrink-0 px-5 py-4 border-b-2 transition-colors',
        stageName === 'Unset' ? 'bg-slate-100 border-slate-200' : `${config?.bg} border-slate-200/50`
      )}>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className={cn(
              'w-10 h-10 rounded-xl flex items-center justify-center shadow-lg transition-transform group-hover:scale-110',
              stageName === 'Closed Won' && 'bg-gradient-to-br from-emerald-500 to-green-500 shadow-emerald-200/50',
              stageName === 'Closed Lost' && 'bg-gradient-to-br from-slate-500 to-slate-600 shadow-slate-200/50',
              stageName === 'Qualification' && 'bg-gradient-to-br from-blue-500 to-cyan-500 shadow-blue-200/50',
              stageName === 'Proposal' && 'bg-gradient-to-br from-amber-500 to-orange-500 shadow-amber-200/50',
              stageName === 'Negotiation' && 'bg-gradient-to-br from-violet-500 to-purple-500 shadow-violet-200/50',
              stageName === 'Unset' && 'bg-gradient-to-br from-slate-400 to-slate-500 shadow-slate-200/50',
              !['Closed Won', 'Closed Lost', 'Qualification', 'Proposal', 'Negotiation', 'Unset'].includes(stageName) && 'bg-gradient-to-br from-slate-500 to-slate-600 shadow-slate-200/50'
            )}>
              <StageIcon className="w-5 h-5 text-white" aria-hidden />
            </div>
            <div>
              <h2 className="font-bold text-sm text-slate-800">{stageName}</h2>
              <p className="text-xs text-slate-500">{deals.length} deal{deals.length !== 1 ? 's' : ''}</p>
            </div>
          </div>
          <div className={cn(
            'px-3 py-1.5 rounded-xl text-sm font-bold',
            stageName === 'Closed Won' && 'bg-emerald-100 text-emerald-700',
            stageName === 'Closed Lost' && 'bg-slate-100 text-slate-600',
            stageName !== 'Closed Won' && stageName !== 'Closed Lost' && 'bg-slate-100 text-slate-700'
          )}>
            {totalValue}
          </div>
        </div>
        
        {/* Progress indicator */}
        {deals.length > 0 && (
          <div className="mt-3 flex items-center gap-2">
            <div className="flex-1 h-1.5 bg-slate-200/70 rounded-full overflow-hidden">
              <motion.div 
                initial={{ width: 0 }}
                animate={{ width: '100%' }}
                transition={{ duration: 0.8, delay: 0.2 }}
                className={cn(
                  'h-full rounded-full',
                  stageName === 'Closed Won' && 'bg-gradient-to-r from-emerald-400 to-green-400',
                  stageName === 'Closed Lost' && 'bg-gradient-to-r from-slate-400 to-slate-500',
                  stageName === 'Qualification' && 'bg-gradient-to-r from-blue-400 to-cyan-400',
                  stageName === 'Proposal' && 'bg-gradient-to-r from-amber-400 to-orange-400',
                  stageName === 'Negotiation' && 'bg-gradient-to-r from-violet-400 to-purple-400',
                  !['Closed Won', 'Closed Lost', 'Qualification', 'Proposal', 'Negotiation'].includes(stageName) && 'bg-gradient-to-r from-slate-400 to-slate-500'
                )}
              />
            </div>
          </div>
        )}
      </div>
      
      {/* Cards Container */}
      <div className="flex-1 min-h-0 overflow-y-auto p-3 space-y-3">
        {showDropZone && deals.length === 0 && (
          <motion.div 
            initial={{ opacity: 0, scale: 0.9 }}
            animate={{ opacity: 1, scale: 1 }}
            className="h-24 rounded-xl border-2 border-dashed border-emerald-300 bg-emerald-50/50 flex items-center justify-center"
          >
            <p className="text-sm text-emerald-600 font-medium">Drop here</p>
          </motion.div>
        )}
        <AnimatePresence mode="popLayout">
          {deals.map((deal) => (
            <DealCard
              key={deal.id}
              deal={deal}
              contactName={deal.contactId ? contacts.find((c) => c.id === deal.contactId)?.name : undefined}
              stageId={stageId}
              stageName={stageName}
              onMoveStage={onMoveStage}
              onEdit={() => onEdit(deal)}
              onDelete={() => onDelete(deal)}
              onOpenDetail={() => onOpenDetail(deal)}
              stageList={stageList}
            />
          ))}
        </AnimatePresence>
        
        {deals.length === 0 && !showDropZone && (
          <div className="h-32 flex flex-col items-center justify-center text-center p-4">
            <div className="w-12 h-12 rounded-xl bg-slate-100 flex items-center justify-center mb-2">
              <StageIcon className="w-6 h-6 text-slate-400" />
            </div>
            <p className="text-sm text-slate-400">No deals in this stage</p>
            <p className="text-xs text-slate-300 mt-1">Drag deals here or create new ones</p>
          </div>
        )}
      </div>
    </motion.div>
  );
}

export default DroppableStageColumn;

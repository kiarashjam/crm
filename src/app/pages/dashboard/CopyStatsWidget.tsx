import { Zap, FileOutput, Send, LayoutTemplate, Clock } from 'lucide-react';
import type { CopyStats } from './types';

interface CopyStatsWidgetProps {
  stats: CopyStats;
}

export function CopyStatsWidget({ stats }: CopyStatsWidgetProps) {
  return (
    <section className="bg-gradient-to-br from-slate-900 to-slate-800 rounded-2xl p-5 text-white">
      <div className="flex items-center gap-2 mb-4">
        <Zap className="w-5 h-5 text-amber-400" />
        <h2 className="text-sm font-bold">Writer Stats</h2>
      </div>
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <FileOutput className="w-4 h-4 text-slate-400" />
            <span className="text-sm text-slate-300">This week</span>
          </div>
          <span className="text-lg font-bold">{stats.sentThisWeek}</span>
        </div>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Send className="w-4 h-4 text-slate-400" />
            <span className="text-sm text-slate-300">Total sent</span>
          </div>
          <span className="text-lg font-bold">{stats.totalSent}</span>
        </div>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <LayoutTemplate className="w-4 h-4 text-slate-400" />
            <span className="text-sm text-slate-300">Templates</span>
          </div>
          <span className="text-lg font-bold">{stats.templateCount}</span>
        </div>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Clock className="w-4 h-4 text-slate-400" />
            <span className="text-sm text-slate-300">Time saved</span>
          </div>
          <span className="text-lg font-bold text-emerald-400">~2 min/copy</span>
        </div>
      </div>
    </section>
  );
}

export default CopyStatsWidget;

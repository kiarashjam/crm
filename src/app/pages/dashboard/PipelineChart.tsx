import { Link } from 'react-router-dom';
import { BarChart3, ArrowRight } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import type { PipelineStage } from './types';

interface PipelineChartProps {
  stages: PipelineStage[];
}

const stageColors = [
  'from-blue-500 to-cyan-500',
  'from-violet-500 to-purple-500',
  'from-orange-500 to-amber-500',
  'from-emerald-500 to-teal-500',
  'from-rose-500 to-pink-500',
];

export function PipelineChart({ stages }: PipelineChartProps) {
  if (stages.length === 0) return null;

  const pipelineTotal = stages.reduce((sum, s) => sum + s.value, 0);
  const pipelineMax = Math.max(...stages.map(s => s.value), 1);

  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center">
            <BarChart3 className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-base font-bold text-slate-900">Pipeline Overview</h2>
            <p className="text-xs text-slate-500">Deal value by stage</p>
          </div>
        </div>
        <Link to="/deals" className="text-sm text-orange-600 hover:text-orange-700 font-medium flex items-center gap-1">
          View all <ArrowRight className="w-4 h-4" />
        </Link>
      </div>
      <div className="p-6">
        <div className="space-y-4">
          {stages.map((stage, i) => {
            const percentage = pipelineTotal > 0 ? (stage.value / pipelineTotal) * 100 : 0;
            const barWidth = pipelineMax > 0 ? (stage.value / pipelineMax) * 100 : 0;
            return (
              <div key={stage.stageId} className="group">
                <div className="flex items-center justify-between mb-2">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-slate-700">{stage.stageName}</span>
                    <span className="text-xs text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">{stage.dealCount} deals</span>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className="text-xs text-slate-500">{percentage.toFixed(1)}%</span>
                    <span className="text-sm font-semibold text-slate-900">
                      ${stage.value.toLocaleString()}
                    </span>
                  </div>
                </div>
                <div className="h-3 bg-slate-100 rounded-full overflow-hidden">
                  <div 
                    className={cn("h-full rounded-full bg-gradient-to-r transition-all duration-500", stageColors[i % stageColors.length])}
                    style={{ width: `${barWidth}%` }}
                  />
                </div>
              </div>
            );
          })}
        </div>
        <div className="mt-6 pt-4 border-t border-slate-100 flex items-center justify-between">
          <span className="text-sm text-slate-600">Total Pipeline</span>
          <span className="text-lg font-bold text-slate-900">${pipelineTotal.toLocaleString()}</span>
        </div>
      </div>
    </section>
  );
}

export default PipelineChart;

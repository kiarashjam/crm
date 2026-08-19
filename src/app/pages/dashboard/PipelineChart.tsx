import { Link } from 'react-router-dom';
import { BarChart3, ArrowRight, TriangleAlert } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { formatMoney } from '@/app/lib/money';
import type { PipelineStage } from './types';

interface PipelineChartProps {
  stages: PipelineStage[];
  /** True once the request has actually returned. */
  loaded: boolean;
  /** True when it failed, which is not the same as having no deals. */
  failed: boolean;
}

const stageColors = [
  'from-blue-500 to-cyan-500',
  'from-violet-500 to-purple-500',
  'from-orange-500 to-amber-500',
  'from-emerald-500 to-teal-500',
  'from-rose-500 to-pink-500',
];

function Shell({ children }: { children: React.ReactNode }) {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center">
            <BarChart3 className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-base font-bold text-slate-900">Pipeline Overview</h2>
            <p className="text-xs text-slate-500">Open deal value by stage</p>
          </div>
        </div>
        <Link to="/deals" className="text-sm text-orange-600 hover:text-orange-700 font-medium flex items-center gap-1">
          View all <ArrowRight className="w-4 h-4" />
        </Link>
      </div>
      <div className="p-6">{children}</div>
    </section>
  );
}

export function PipelineChart({ stages, loaded, failed }: PipelineChartProps) {
  // This used to `return null` whenever the list was empty, so a failed request
  // made the whole section vanish — indistinguishable from having no deals, and
  // leaving a hole in the layout with nothing to explain it.
  if (failed) {
    return (
      <Shell>
        <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
          <p className="text-sm text-amber-900">
            <strong className="font-semibold">Could not load the pipeline breakdown.</strong>{' '}
            This is not the same as having no deals — reload rather than reading it as empty.
          </p>
        </div>
      </Shell>
    );
  }

  if (!loaded) {
    return (
      <Shell>
        <div className="space-y-4" aria-busy="true">
          <p className="sr-only" role="status">Loading the pipeline breakdown…</p>
          {[0, 1, 2, 3].map((i) => (
            <div key={i} className="space-y-2">
              <div className="h-3 w-40 animate-pulse rounded bg-slate-200" />
              <div className="h-3 animate-pulse rounded-full bg-slate-100" />
            </div>
          ))}
        </div>
      </Shell>
    );
  }

  if (stages.length === 0) {
    return (
      <Shell>
        <p className="py-6 text-center text-sm text-slate-500">
          No open deals yet. Stages appear here once a deal has a value.
        </p>
      </Shell>
    );
  }

  // Grouped by currency, because the server now returns one row per stage per
  // currency and adding those together would produce a figure that is not a
  // quantity of anything. The largest currency leads; the rest follow, labelled.
  const byCurrency = new Map<string, PipelineStage[]>();
  for (const s of stages) {
    const list = byCurrency.get(s.currency);
    if (list) list.push(s);
    else byCurrency.set(s.currency, [s]);
  }
  const blocks = [...byCurrency.entries()]
    .map(([currency, rows]) => ({
      currency,
      rows,
      total: rows.reduce((sum, r) => sum + r.value, 0),
    }))
    .sort((a, b) => b.total - a.total || a.currency.localeCompare(b.currency));
  const showCurrencyHeadings = blocks.length > 1;

  return (
    <Shell>
      <div className="space-y-7">
        {blocks.map((block, blockIndex) => {
          const max = Math.max(...block.rows.map((r) => r.value), 1);
          return (
            <div key={block.currency}>
              {showCurrencyHeadings && (
                <p className="mb-3 text-[11px] font-semibold uppercase tracking-[0.08em] text-slate-400">
                  {block.currency}
                </p>
              )}
              <div className="space-y-4">
                {block.rows.map((stage, i) => {
                  const share = block.total > 0 ? (stage.value / block.total) * 100 : 0;
                  const barWidth = (stage.value / max) * 100;
                  return (
                    <div key={`${stage.stageId}-${stage.currency}`} className="group">
                      <div className="flex items-center justify-between mb-2">
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-medium text-slate-700">{stage.stageName}</span>
                          <span className="text-xs text-slate-400 bg-slate-100 px-2 py-0.5 rounded-full">
                            {stage.dealCount} deal{stage.dealCount === 1 ? '' : 's'}
                          </span>
                        </div>
                        <div className="flex items-center gap-3">
                          {/* Share of this currency's total, not of a mixed one. */}
                          <span className="text-xs text-slate-500">{share.toFixed(1)}%</span>
                          <span className="text-sm font-semibold text-slate-900">
                            {formatMoney(stage.value, stage.currency)}
                          </span>
                        </div>
                      </div>
                      <div className="h-3 bg-slate-100 rounded-full overflow-hidden">
                        <div
                          className={cn(
                            'h-full rounded-full bg-gradient-to-r transition-all duration-500',
                            stageColors[(blockIndex + i) % stageColors.length],
                          )}
                          style={{ width: `${barWidth}%` }}
                        />
                      </div>
                    </div>
                  );
                })}
              </div>
              <div className="mt-4 pt-3 border-t border-slate-100 flex items-center justify-between">
                <span className="text-sm text-slate-600">
                  Total{showCurrencyHeadings ? ` in ${block.currency}` : ' pipeline'}
                </span>
                <span className="text-lg font-bold text-slate-900">
                  {formatMoney(block.total, block.currency)}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </Shell>
  );
}

export default PipelineChart;

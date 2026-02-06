import { AlertCircle, Calendar } from 'lucide-react';
import type { Deal, Pipeline } from '@/app/api/types';
import { FALLBACK_STAGES } from './config';

/**
 * Resolve stage list: from pipeline's dealStages (sorted by displayOrder) or fallback to FALLBACK_STAGES.
 */
export function getStageList(
  pipelines: Pipeline[],
  selectedPipelineId: string | null
): { id: string; name: string }[] {
  const pipeline = selectedPipelineId
    ? pipelines.find((p) => p.id === selectedPipelineId)
    : pipelines[0];
  const stages = pipeline?.dealStages?.slice().sort((a, b) => a.displayOrder - b.displayOrder);
  if (stages?.length) return stages.map((s) => ({ id: s.id, name: s.name }));
  return FALLBACK_STAGES.map((name) => ({ id: name, name }));
}

/**
 * Group deals by stage: when stageList has id !== name use dealStageId; else use deal.stage.
 */
export function groupDealsByStage(
  deals: Deal[],
  stageList: { id: string; name: string }[]
): { stageId: string; stageName: string; deals: Deal[] }[] {
  const byId = new Map<string, Deal[]>();
  const useApiStages = stageList.length > 0 && stageList.some((s) => s.id !== s.name);
  for (const s of stageList) {
    byId.set(s.id, []);
  }
  const unsetKey = '__unset__';
  if (!byId.has(unsetKey)) byId.set(unsetKey, []);

  for (const deal of deals) {
    if (useApiStages && deal.dealStageId) {
      const list = byId.get(deal.dealStageId) ?? byId.get(unsetKey)!;
      list.push(deal);
    } else {
      const stageName = deal.stage || 'Qualification';
      const match = stageList.find((s) => s.name === stageName);
      const key = match ? match.id : stageName;
      const list = byId.get(key) ?? byId.get(unsetKey)!;
      list.push(deal);
    }
  }

  return stageList
    .map((s) => ({
      stageId: s.id,
      stageName: s.name,
      deals: byId.get(s.id) ?? [],
    }))
    .concat(
      byId.get(unsetKey)!.length
        ? [{ stageId: unsetKey, stageName: 'Unset', deals: byId.get(unsetKey)! }]
        : []
    );
}

/**
 * Format ISO date string to a readable last activity format.
 */
export function formatLastActivity(iso: string | undefined): string {
  if (!iso) return '';
  try {
    const d = new Date(iso);
    return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return '';
  }
}

/**
 * Format an array of value strings to a currency sum.
 */
export function formatValueSum(values: string[]): string {
  const sum = values.reduce((acc, v) => {
    const num = parseFloat(String(v).replace(/[^0-9.-]/g, '')) || 0;
    return acc + num;
  }, 0);
  return new Intl.NumberFormat(undefined, {
    style: 'currency',
    currency: 'USD',
    maximumFractionDigits: 0,
  }).format(sum);
}

/**
 * Calculate days until close from an ISO date string.
 */
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

/**
 * UrgencyBadge component - displays urgency status based on days until close.
 */
export function UrgencyBadge({ days }: { days: number }) {
  if (days < 0) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-red-500/15 px-2 py-0.5 text-xs font-medium text-red-700">
        <AlertCircle className="h-3 w-3" /> Overdue
      </span>
    );
  }
  if (days <= 7) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-amber-500/15 px-2 py-0.5 text-xs font-medium text-amber-700">
        <Calendar className="h-3 w-3" /> Due in {days}d
      </span>
    );
  }
  if (days <= 30) {
    return (
      <span className="inline-flex items-center gap-1 rounded-md bg-slate-500/10 px-2 py-0.5 text-xs font-medium text-slate-600">
        <Calendar className="h-3 w-3" /> {days}d to close
      </span>
    );
  }
  return null;
}

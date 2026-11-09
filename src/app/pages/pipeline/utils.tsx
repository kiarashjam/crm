import { AlertCircle, Calendar } from 'lucide-react';
import type { Deal, Pipeline } from '@/app/api/types';
import { FALLBACK_STAGES } from './config';
import { dateOnlyKey, daysBetween, formatDay, todayKeyIn } from '@/app/lib/zonedTime';

/**
 * Resolve stage list: from pipeline's dealStages (sorted by displayOrder) or fallback to FALLBACK_STAGES.
 */
export function getStageList(
  pipelines: Pipeline[],
  selectedPipelineId: string | null
): BoardStage[] {
  const pipeline = selectedPipelineId
    ? pipelines.find((p) => p.id === selectedPipelineId)
    : pipelines[0];
  const stages = pipeline?.dealStages?.slice().sort((a, b) => a.displayOrder - b.displayOrder);
  if (stages?.length) return stages.map((s) => ({ id: s.id, name: s.name, isWon: s.isWon, isLost: s.isLost }));
  return FALLBACK_STAGES.map((name) => ({ id: name, name, isWon: name === 'Closed Won', isLost: name === 'Closed Lost' }));
}

/** A board column. Won and lost come from the stage's settings, not its name. */
export type BoardStage = { id: string; name: string; isWon?: boolean; isLost?: boolean };

/** Whether a deal is won, lost or still open: its own outcome first, then its stage's. */
export function dealOutcome(deal: Deal, stageList: BoardStage[]): 'won' | 'lost' | 'open' {
  if (deal.isWon === true) return 'won';
  if (deal.isWon === false) return 'lost';
  const stage = deal.dealStageId
    ? stageList.find((s) => s.id === deal.dealStageId)
    : stageList.find((s) => s.name === deal.stage);
  if (stage?.isWon) return 'won';
  if (stage?.isLost) return 'lost';
  return 'open';
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
 * Days from today to a deal's expected close date: 0 on the day, negative once past.
 *
 * A close date is the day that was picked, stored as that day's UTC midnight, and
 * "today" is the workspace's, in its time zone. Both were read by this browser's
 * clock, so west of UTC a deal showed as due a day early, and the board's Overdue
 * figure, counted on the workspace's calendar, could disagree with the card's badge.
 */
export function getDaysUntilClose(iso: string | undefined | null, timeZone: string, now: Date = new Date()): number | null {
  if (!iso) return null;
  const day = dateOnlyKey(iso);
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return null;
  return daysBetween(todayKeyIn(timeZone, now), day);
}

/** Whole days from an instant until now, never below 0; null when there is no instant. */
export function daysSince(iso: string | undefined | null, now: Date = new Date()): number | null {
  if (!iso) return null;
  const then = Date.parse(iso);
  if (Number.isNaN(then)) return null;
  return Math.max(0, Math.floor((now.getTime() - then) / (24 * 60 * 60 * 1000)));
}

/** A number of days as people say it: "today", "1 day", "12 days". */
export function formatDays(days: number): string {
  if (days <= 0) return 'today';
  return days === 1 ? '1 day' : `${days} days`;
}

/** A close date written as the day that was picked, wherever the viewer is. */
export function formatCloseDate(iso: string, month: 'short' | 'long' = 'short'): string {
  return formatDay(iso, { month, day: 'numeric', year: 'numeric' });
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

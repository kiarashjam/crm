import { ArrowRight, GitCommitVertical } from 'lucide-react';
import type { DealStageChange } from '@/app/api/types';
import { RecordCard } from '@/app/components/RecordCard';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { daysSince, formatDays } from '@/app/pages/pipeline/utils';
import { currentDateLocale } from '@/app/i18n/current';

/** Whole days between two instants, never below 0. */
function daysBetweenInstants(from: string, to: string): number {
  return Math.max(0, Math.floor((Date.parse(to) - Date.parse(from)) / (24 * 60 * 60 * 1000)));
}

function when(iso: string): string {
  return new Date(iso).toLocaleDateString(currentDateLocale(), { month: 'short', day: 'numeric', year: 'numeric' });
}

/**
 * The stages a deal has been in, newest first, from the server's stage history: when it
 * came into each, from where, who moved it, and how long it stayed.
 */
export default function StageHistoryCard({ history, loading, index }: { history: DealStageChange[]; loading: boolean; index?: number }) {
  // The history starts with the stage the deal was created in. A deal from before the
  // history was kept has no such line, and says so rather than seem complete.
  const oldest = history[history.length - 1];
  const startsAtCreation = oldest?.isCreation ?? false;

  return (
    <RecordCard title="Stage history" icon={<GitCommitVertical className="h-3.5 w-3.5" />} tone="violet" count={history.length} index={index}>
      {loading ? (
        <div className="py-6 text-center">
          <Spinner size={22} className="mx-auto mb-2" />
          <p className="text-xs text-slate-500">Loading stage history...</p>
        </div>
      ) : history.length === 0 ? (
        <p className="py-3 text-sm text-slate-500">No stage changes recorded yet. Each move from now on is.</p>
      ) : (
        <ol className="relative ml-2 space-y-3 border-l border-slate-200 pl-4 dark:border-slate-700">
          {history.map((change) => {
            const to = change.toStageName ?? 'a stage';
            const stayed = change.leftAtUtc
              ? `${formatDays(daysBetweenInstants(change.changedAtUtc, change.leftAtUtc))} in ${to}`
              : `In ${to} ${(daysSince(change.changedAtUtc) ?? 0) === 0 ? 'since today' : `for ${formatDays(daysSince(change.changedAtUtc) ?? 0)}`}`;
            return (
              <li key={change.id} className="relative">
                <span
                  className={change.leftAtUtc
                    ? 'absolute -left-[21px] top-1.5 h-2.5 w-2.5 rounded-full bg-slate-300'
                    : 'absolute -left-[21px] top-1.5 h-2.5 w-2.5 rounded-full bg-violet-500 ring-4 ring-violet-100'}
                  aria-hidden
                />
                <p className="flex flex-wrap items-center gap-1.5 text-sm font-medium text-slate-800 dark:text-slate-200">
                  {change.isCreation ? (
                    <>Created in {to}</>
                  ) : change.fromStageName ? (
                    <>
                      {change.fromStageName}
                      <ArrowRight className="h-3.5 w-3.5 text-slate-400" aria-label="to" />
                      {to}
                    </>
                  ) : (
                    <>Moved to {to}</>
                  )}
                </p>
                <p className="mt-0.5 text-xs text-slate-500">
                  {when(change.changedAtUtc)}
                  {change.changedByName ? ` · by ${change.changedByName}` : ''}
                  {' · '}
                  {stayed}
                </p>
              </li>
            );
          })}
        </ol>
      )}
      {!loading && history.length > 0 && !startsAtCreation && (
        <p className="mt-3 text-xs text-slate-500">Earlier stage changes were not recorded.</p>
      )}
    </RecordCard>
  );
}

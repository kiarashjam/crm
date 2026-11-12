import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronDown, ChevronRight, Loader2, RotateCw, Square } from 'lucide-react';
import { toast } from 'sonner';
import { LoadFailed } from '@/app/components/LoadFailed';
import { cn } from '@/app/components/ui/utils';
import {
  STEP_LABELS, TRIGGER_LABELS, cancelAutomationRun, retryAutomationRun,
  type AutomationRule, type AutomationRun, type AutomationRunLogEntry, type AutomationRunStatus, type RunQuery,
} from '@/app/api/automations';
import { useAutomationRun, useAutomationRuns } from '@/app/hooks/queries';
import { SELECT_CLASS } from './ConditionsEditor';
import { currentDateLocale } from '@/app/i18n/current';

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export const RUN_FILTERS: { value: string; label: string; empty: string }[] = [
  { value: '', label: 'All', empty: 'Nothing has set these rules off yet.' },
  { value: 'active', label: 'Still going', empty: 'No run is waiting or being tried again.' },
  { value: 'failed', label: 'Failed', empty: 'No run has failed.' },
  { value: 'completed', label: 'Finished', empty: 'No run has finished yet.' },
  { value: 'cancelled', label: 'Stopped', empty: 'No run was stopped.' },
];

const STATUS: Record<AutomationRunStatus, { label: string; tone: string }> = {
  running: { label: 'Running', tone: 'bg-sky-50 text-sky-700' },
  waiting: { label: 'Waiting', tone: 'bg-indigo-50 text-indigo-700' },
  retrying: { label: 'Trying again', tone: 'bg-amber-50 text-amber-800' },
  completed: { label: 'Finished', tone: 'bg-emerald-50 text-emerald-700' },
  failed: { label: 'Failed', tone: 'bg-red-50 text-red-700' },
  cancelled: { label: 'Stopped', tone: 'bg-slate-100 text-slate-600' },
};

const OUTCOME: Record<AutomationRunLogEntry['outcome'], { label: string; tone: string }> = {
  done: { label: 'Done', tone: 'text-emerald-700' },
  skipped: { label: 'Skipped', tone: 'text-slate-500' },
  waiting: { label: 'Waiting', tone: 'text-indigo-700' },
  retrying: { label: 'Will try again', tone: 'text-amber-800' },
  failed: { label: 'Failed', tone: 'text-red-700' },
};

const ACTIVE = new Set<AutomationRunStatus>(['running', 'waiting', 'retrying']);

/** A moment as this person reads dates: the day and the time. */
export function when(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString(currentDateLocale(), { dateStyle: 'medium', timeStyle: 'short' });
}

/** Where a run stands, in a sentence. */
function standing(run: AutomationRun): string {
  const step = run.currentStep ? STEP_LABELS[run.currentStep] ?? run.currentStep : 'a step';
  switch (run.status) {
    case 'waiting':
      return run.waitingFor ? `Waiting ${run.waitingFor}.` : `Waiting until ${when(run.nextAttemptAtUtc)}.`;
    case 'retrying':
      return `${step} failed (attempt ${run.stepAttempts}); trying again ${when(run.nextAttemptAtUtc)}.${run.lastError ? ` ${run.lastError}` : ''}`;
    case 'failed':
      return `Failed at "${step}" after ${run.stepAttempts} ${run.stepAttempts === 1 ? 'attempt' : 'attempts'}.${run.lastError ? ` ${run.lastError}` : ''}`;
    case 'cancelled':
      return `Stopped ${when(run.finishedAtUtc)}.`;
    case 'completed':
      return `Finished ${when(run.finishedAtUtc)}.`;
    default:
      return `At "${step}".`;
  }
}

function RunLog({ runId }: { runId: string }) {
  const query = useAutomationRun(runId);
  if (query.isPending) {
    return <div className="flex items-center gap-2 py-3 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" /> Reading the log…</div>;
  }
  if (query.isError) {
    return <LoadFailed what="This run's log" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} className="p-4" />;
  }
  const steps = query.data?.steps ?? [];
  if (steps.length === 0) return <p className="py-2 text-sm text-slate-500">Nothing is logged for this run.</p>;
  return (
    <ol className="space-y-2 border-l border-slate-200 pl-4" aria-label="Run log">
      {steps.map((entry) => {
        const outcome = OUTCOME[entry.outcome] ?? { label: entry.outcome, tone: 'text-slate-600' };
        return (
          <li key={entry.id} className="text-sm">
            <div className="flex flex-wrap items-baseline gap-x-2">
              <span className="font-medium text-slate-800 dark:text-slate-100">{STEP_LABELS[entry.stepType] ?? entry.stepType}</span>
              <span className={cn('text-xs font-semibold', outcome.tone)}>{outcome.label}</span>
              {entry.attempt > 1 && <span className="text-xs text-slate-500">attempt {entry.attempt}</span>}
              <span className="text-xs text-slate-400">{when(entry.atUtc)}</span>
            </div>
            <p className="text-slate-600 dark:text-slate-300">{entry.summary}</p>
            {entry.error && <p className="text-xs text-red-700">{entry.error}</p>}
            {entry.emailStatus && <p className="text-xs text-slate-500">Email: {entry.emailStatus}</p>}
            {entry.taskId && <Link to={`/tasks/${entry.taskId}`} className="text-xs text-indigo-700 underline">Open the task</Link>}
          </li>
        );
      })}
    </ol>
  );
}

/**
 * What the rules did: each run, where it stands (waiting, being tried again, failed,
 * finished), and the log of its steps. Whoever manages the workspace's settings can try a
 * failed run again or stop one that has not finished.
 */
export function RunsPanel({
  rules, ruleId, onRuleChange, canAct,
}: {
  rules: AutomationRule[];
  /** One rule's runs; every rule's when empty. */
  ruleId: string;
  onRuleChange: (ruleId: string) => void;
  /** May try a run again or stop it. */
  canAct: boolean;
}) {
  const [status, setStatus] = useState('');
  const [open, setOpen] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);
  const query: RunQuery = { ...(ruleId ? { ruleId } : {}), ...(status ? { status } : {}) };
  const runs = useAutomationRuns(query);
  const empty = RUN_FILTERS.find((f) => f.value === status)?.empty ?? 'Nothing has set these rules off yet.';

  const act = async (run: AutomationRun, what: 'retry' | 'cancel') => {
    if (what === 'cancel' && !confirm(`Stop this run of "${run.ruleName}"? What it was still to do will not be done.`)) return;
    setBusy(run.id);
    try {
      if (what === 'retry') {
        await retryAutomationRun(run.id);
        toast.success('Tried again. The log says how it went.');
      } else {
        await cancelAutomationRun(run.id);
        toast.success('Run stopped.');
      }
      setOpen(run.id);
    } catch (err) {
      // The server says why: it finished meanwhile, or another runner is on it.
      toast.error(reason(err, what === 'retry' ? 'The run could not be tried again.' : 'The run could not be stopped.'));
    } finally {
      setBusy(null);
      void runs.refetch();
    }
  };

  return (
    <section aria-labelledby="automation-runs" className="mt-8">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h2 id="automation-runs" className="text-base font-semibold text-slate-900 dark:text-slate-100">Runs</h2>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex flex-wrap gap-1" role="group" aria-label="Show runs">
            {RUN_FILTERS.map((f) => (
              <button
                key={f.value || 'all'}
                type="button"
                aria-pressed={status === f.value}
                onClick={() => setStatus(f.value)}
                className={cn(
                  'h-8 rounded-md border px-2.5 text-xs font-semibold',
                  status === f.value ? 'border-amber-400 bg-amber-50 text-amber-900' : 'border-slate-200 bg-white text-slate-600 hover:border-slate-300',
                )}
              >
                {f.label}
              </button>
            ))}
          </div>
          <select aria-label="Rule" className={SELECT_CLASS} value={ruleId} onChange={(e) => onRuleChange(e.target.value)}>
            <option value="">Every rule</option>
            {rules.map((r) => <option key={r.id} value={r.id}>{r.name}</option>)}
          </select>
        </div>
      </div>

      {runs.isPending ? (
        <div className="flex items-center justify-center py-10 text-slate-500"><Loader2 className="h-5 w-5 animate-spin" /></div>
      ) : runs.isError && !runs.data ? (
        <LoadFailed what="Runs" error={runs.error} onRetry={() => void runs.refetch()} retrying={runs.isFetching} />
      ) : (runs.data ?? []).length === 0 ? (
        <p className="rounded-2xl border border-dashed border-slate-200 bg-white py-8 text-center text-sm text-slate-500 dark:border-slate-700 dark:bg-slate-900">{empty}</p>
      ) : (
        <ul className="space-y-2">
          {(runs.data ?? []).map((run) => {
            const s = STATUS[run.status] ?? { label: run.status, tone: 'bg-slate-100 text-slate-600' };
            const expanded = open === run.id;
            return (
              <li key={run.id} className="rounded-xl border border-slate-200 bg-white p-3 shadow-sm dark:border-slate-700 dark:bg-slate-900">
                <div className="flex flex-wrap items-start gap-3">
                  <button
                    type="button"
                    onClick={() => setOpen(expanded ? null : run.id)}
                    aria-expanded={expanded}
                    aria-label={`${expanded ? 'Hide' : 'Show'} the log of ${run.ruleName} for ${run.recordName ?? 'its record'}`}
                    className="mt-0.5 rounded p-0.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                  >
                    {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                  </button>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <span className={cn('rounded-full px-2 py-0.5 text-xs font-semibold', s.tone)}>{s.label}</span>
                      <span className="font-medium text-slate-900 dark:text-slate-100">{run.ruleName}</span>
                      <span className="text-sm text-slate-500">·</span>
                      {run.recordLink ? (
                        <Link to={run.recordLink} className="text-sm text-indigo-700 underline">{run.recordName ?? 'Open the record'}</Link>
                      ) : (
                        <span className="text-sm text-slate-600">{run.recordName ?? 'A record no longer there'}</span>
                      )}
                    </div>
                    <p className="mt-0.5 text-sm text-slate-600 dark:text-slate-300">{standing(run)}</p>
                    <p className="text-xs text-slate-400">{TRIGGER_LABELS[run.trigger] ?? run.trigger} · started {when(run.startedAtUtc)}</p>
                  </div>
                  {canAct && run.status === 'failed' && (
                    <button type="button" disabled={busy === run.id} onClick={() => act(run, 'retry')}
                      className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-slate-300 px-2.5 text-xs font-semibold text-slate-700 hover:border-amber-400 disabled:opacity-60">
                      <RotateCw className={cn('h-3.5 w-3.5', busy === run.id && 'motion-safe:animate-spin')} aria-hidden /> Try again
                    </button>
                  )}
                  {canAct && ACTIVE.has(run.status) && (
                    <button type="button" disabled={busy === run.id} onClick={() => act(run, 'cancel')}
                      className="inline-flex h-8 items-center gap-1.5 rounded-lg border border-slate-300 px-2.5 text-xs font-semibold text-slate-700 hover:border-red-300 hover:text-red-700 disabled:opacity-60">
                      <Square className="h-3.5 w-3.5" aria-hidden /> Stop
                    </button>
                  )}
                </div>
                {expanded && <div className="mt-3 pl-7"><RunLog runId={run.id} /></div>}
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}

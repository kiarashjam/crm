// How long the workspace keeps personal data: each rule with its period (all off until the
// workspace sets one, on the Settings tab), what it would remove if it ran now, what it keeps
// although the period is over and why, a way to run it now, and the log of runs. The daily
// run does the same on its own once a period is set.

import { useState } from 'react';
import { Loader2, Play } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { LoadFailed } from '@/app/components/LoadFailed';
import { runRetention, type RetentionRule, type RetentionRun } from '@/app/api/privacy';
import { useRetentionPreview, useRetentionRuns } from '@/app/hooks/queries';
import { cn } from '@/app/components/ui/utils';

const RULE_TITLE: Record<RetentionRule['rule'], string> = {
  leads: 'Leads nobody worked on',
  members: 'Members who left',
  mail: 'The text of sent emails',
  details: 'Names in the audit trail, and details around requests',
};

const STATUS: Record<RetentionRun['status'], { label: string; tone: string }> = {
  running: { label: 'Running', tone: 'bg-sky-100 text-sky-800' },
  succeeded: { label: 'Done', tone: 'bg-emerald-100 text-emerald-800' },
  partly_failed: { label: 'Partly failed', tone: 'bg-amber-100 text-amber-900' },
  failed: { label: 'Failed', tone: 'bg-rose-100 text-rose-800' },
};

function when(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

function period(rule: RetentionRule): string {
  if (!rule.period) return 'Off';
  const unit = rule.unit === 'years' ? 'year' : 'month';
  return `After ${rule.period} ${unit}${rule.period === 1 ? '' : 's'}`;
}

export function RetentionPanel({ onOpenSettings }: { onOpenSettings: () => void }) {
  const preview = useRetentionPreview();
  const runs = useRetentionRuns();
  const [asking, setAsking] = useState(false);
  const [running, setRunning] = useState(false);

  const run = async () => {
    setRunning(true);
    try {
      const done = await runRetention();
      toast.success(done.status === 'succeeded' ? 'Retention run done' : `The retention run ${STATUS[done.status]?.label.toLowerCase() ?? done.status}`);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The retention run did not start');
    } finally {
      setRunning(false);
      setAsking(false);
    }
  };

  return (
    <div className="max-w-4xl space-y-5">
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="retention-rules">
        <h3 id="retention-rules" className="font-semibold text-slate-900">What the retention settings remove</h3>
        <p className="mb-3 text-sm text-slate-600">
          Each rule is off until a period is set on the{' '}
          <button type="button" className="text-indigo-700 underline" onClick={onOpenSettings}>Settings</button> tab. Once one is set, a
          run early each morning applies it; the counts below are what a run would remove now.
        </p>
        {preview.isPending ? (
          <p className="flex items-center gap-2 text-sm text-slate-600" role="status">
            <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> Counting…
          </p>
        ) : preview.isError ? (
          <LoadFailed what="The retention preview" error={preview.error} onRetry={() => void preview.refetch()} retrying={preview.isFetching} />
        ) : (
          <>
            <ul className="divide-y divide-slate-100 rounded-xl border border-slate-200">
              {preview.data.rules.map((rule) => (
                <li key={rule.rule} className="grid gap-1 px-3 py-3 text-sm sm:grid-cols-[1fr_auto] sm:gap-4">
                  <div>
                    <p className="font-medium text-slate-900">{RULE_TITLE[rule.rule] ?? rule.rule}</p>
                    <p className="text-slate-600">{rule.what}</p>
                  </div>
                  <div className="sm:text-right">
                    <p className={cn('font-semibold', rule.period ? 'text-slate-900' : 'text-slate-400')}>{period(rule)}</p>
                    {rule.period ? (
                      <p className="text-slate-600">{rule.count === 0 ? 'Nothing to remove now' : `${rule.count} to remove now`}</p>
                    ) : null}
                  </div>
                </li>
              ))}
            </ul>
            {preview.data.kept.length > 0 && (
              <ul className="mt-3 list-disc space-y-1 pl-5 text-sm text-slate-600">
                {preview.data.kept.map((k) => <li key={k}>{k}</li>)}
              </ul>
            )}
            <div className="mt-4 flex flex-wrap items-center gap-2">
              {asking ? (
                <>
                  <span className="text-sm text-slate-700">Remove what is listed above now? It cannot be undone.</span>
                  <Button type="button" variant="destructive" onClick={() => void run()} disabled={running}>
                    {running && <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" />}
                    Run now
                  </Button>
                  <Button type="button" variant="outline" onClick={() => setAsking(false)} disabled={running}>Cancel</Button>
                </>
              ) : (
                <Button type="button" variant="outline" onClick={() => setAsking(true)} disabled={!preview.data.anythingOn}>
                  <Play className="h-4 w-4" aria-hidden="true" /> Run now…
                </Button>
              )}
              {!preview.data.anythingOn && <span className="text-sm text-slate-500">No period is set, so there is nothing to run.</span>}
            </div>
          </>
        )}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="retention-runs">
        <h3 id="retention-runs" className="mb-3 font-semibold text-slate-900">Runs</h3>
        {runs.isPending ? (
          <p className="text-sm text-slate-600" role="status">Loading the runs…</p>
        ) : runs.isError ? (
          <LoadFailed what="The runs" error={runs.error} onRetry={() => void runs.refetch()} retrying={runs.isFetching} />
        ) : runs.data.length === 0 ? (
          <p className="text-sm text-slate-600">No run yet.</p>
        ) : (
          <ul className="divide-y divide-slate-100">
            {runs.data.map((r) => (
              <li key={r.id} className="py-2 text-sm">
                <div className="flex flex-wrap items-center gap-2">
                  <span className={cn('rounded-md px-1.5 py-0.5 text-xs font-semibold', STATUS[r.status]?.tone ?? STATUS.running.tone)}>
                    {STATUS[r.status]?.label ?? r.status}
                  </span>
                  <span className="text-slate-900">{when(r.startedAtUtc)}</span>
                  <span className="text-slate-500">
                    {r.trigger === 'manual' ? `run by ${r.startedByName ?? 'someone'}` : 'the daily run'}
                  </span>
                </div>
                <p className="text-slate-600">
                  {r.leadsDeleted} {r.leadsDeleted === 1 ? 'lead' : 'leads'} deleted, {r.membersAnonymised} former{' '}
                  {r.membersAnonymised === 1 ? 'member' : 'members'} anonymised, {r.messagesPurged} {r.messagesPurged === 1 ? 'email' : 'emails'} and
                  copies cleared, {r.detailsPurged} {r.detailsPurged === 1 ? 'detail' : 'details'} removed
                </p>
                {r.failures && <p className="text-rose-700">{r.failures}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>
    </div>
  );
}

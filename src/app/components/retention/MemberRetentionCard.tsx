// A member's at-risk score on their page: the score and its level, each sign it is made of with
// the sentence that says why, when it was worked out, and the complaints and concerns staff have
// recorded about them. club.members works the score out again and records, resolves or removes
// a concern (removing also takes records.delete); everyone else reads it. Nothing shows while
// the API is not connected: the demo data has no scores.

import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Activity, CheckCircle2, Loader2, MessageSquareWarning, RotateCw, Trash2 } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import { PERMISSION } from '@/app/api/organizations';
import {
  addConcern, deleteConcern, recomputeMemberRisk, resolveConcern, RISK_LEVEL_LABELS, type Concern, type ConcernKind, type MemberRisk,
} from '@/app/api/retention';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { Button } from '@/app/components/ui/button';
import { Textarea } from '@/app/components/ui/textarea';
import { cn } from '@/app/components/ui/utils';
import { useMemberConcerns, useMemberRisk } from '@/app/hooks/queries';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { useFormat } from '@/app/i18n';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

const LEVEL_STYLE: Record<MemberRisk['level'], string> = {
  low: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  watch: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  at_risk: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
};

export function MemberRetentionCard({ memberId }: { memberId: string }) {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const f = useFormat();
  const risk = useMemberRisk(memberId);
  const concerns = useMemberConcerns(memberId);
  const [kind, setKind] = useState<ConcernKind>('complaint');
  const [note, setNote] = useState('');

  const recompute = useMutation({
    mutationFn: () => recomputeMemberRisk(memberId),
    onSuccess: (saved) => {
      client.setQueryData(queryKeys.retention.member(scope, memberId), saved);
      if (saved.notes.length > 0) saved.notes.forEach((n) => toast.success(n));
      else toast.success(`At-risk score: ${saved.score} (${RISK_LEVEL_LABELS[saved.level].toLowerCase()}).`);
    },
    onError: (err) => toast.error(message(err, 'The score could not be worked out.')),
  });
  const refreshConcerns = () => client.invalidateQueries({ queryKey: queryKeys.retention.concerns(scope, memberId) });
  const add = useMutation({
    mutationFn: () => addConcern(memberId, kind, note),
    onSuccess: () => {
      setNote('');
      void refreshConcerns();
      toast.success(`${kind === 'complaint' ? 'Complaint' : 'Concern'} recorded. It counts in their score from its next working out.`);
    },
    onError: (err) => toast.error(message(err, 'It could not be recorded.')),
  });
  const resolve = useMutation({
    mutationFn: ({ concern, resolution }: { concern: Concern; resolution: string }) => resolveConcern(memberId, concern.id, resolution),
    onSuccess: () => { void refreshConcerns(); toast.success('Marked resolved.'); },
    onError: (err) => toast.error(message(err, 'It could not be marked resolved.')),
  });
  const remove = useMutation({
    mutationFn: (concern: Concern) => deleteConcern(memberId, concern.id),
    onSuccess: () => { void refreshConcerns(); toast.success('Removed.'); },
    onError: (err) => toast.error(message(err, 'It could not be removed.')),
  });

  if (servesDemoData()) return null;
  const r = risk.data;
  const list = concerns.data ?? [];
  const open = list.filter((c) => !c.resolvedAtUtc);

  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm lg:col-span-2">
      <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
        <h3 className="inline-flex items-center gap-2 font-semibold text-slate-900">
          <Activity className="h-4 w-4" aria-hidden /> At-risk score
        </h3>
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <Button type="button" variant="outline" size="sm" onClick={() => recompute.mutate()} disabled={recompute.isPending}>
            {recompute.isPending ? <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden /> : <RotateCw className="h-3.5 w-3.5" aria-hidden />}
            Work it out again
          </Button>
        </WriteOnly>
      </div>

      {risk.isError ? (
        <LoadFailed what="Their at-risk score" error={risk.error} onRetry={() => { void risk.refetch(); }} retrying={risk.isFetching} />
      ) : risk.isLoading ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
      ) : !r ? (
        <p className="text-sm text-slate-600">
          Not scored yet. Every active member is scored each morning, from their visits, spending, what they owe, events and concerns
          against their own past.
        </p>
      ) : (
        <div className="space-y-3">
          <div className="flex flex-wrap items-baseline gap-3">
            <span className="text-3xl font-bold tabular-nums text-slate-900" data-testid="risk-score">{r.score}</span>
            <span className="text-sm text-slate-500">of 100</span>
            <span className={cn('rounded-full px-2.5 py-0.5 text-xs font-semibold', LEVEL_STYLE[r.level])}>{RISK_LEVEL_LABELS[r.level]}</span>
            <span className="text-xs text-slate-500">
              Worked out {f.dateTime(r.computedAtUtc)}. At risk from {r.atRiskThreshold}, watch from {r.watchThreshold}.
            </span>
          </div>
          {r.atRiskSinceUtc && r.level === 'at_risk' && (
            <p className="text-xs text-rose-700">
              At risk since {f.date(r.atRiskSinceUtc)}.
              {r.saveActionsAtUtc ? ` The club's save actions ran on ${f.date(r.saveActionsAtUtc)}.` : ''}
            </p>
          )}
          <ul className="divide-y divide-slate-100 rounded-xl border border-slate-100">
            {r.factors.map((factor) => (
              <li key={factor.key} className="flex items-start gap-3 p-3 text-sm">
                <div className="w-28 shrink-0">
                  <p className="font-medium text-slate-800">{factor.label}</p>
                  <div className="mt-1 h-1.5 w-full overflow-hidden rounded-full bg-slate-100" aria-hidden>
                    <div className={cn('h-full rounded-full', factor.points > 0 ? 'bg-rose-400' : 'bg-slate-200')}
                      style={{ width: `${Math.round(Math.min(1, Math.max(0, factor.strength)) * 100)}%` }} />
                  </div>
                </div>
                <p className="flex-1 text-slate-600">{factor.detail}</p>
                <span className={cn('shrink-0 tabular-nums text-xs font-semibold', factor.points > 0 ? 'text-rose-700' : 'text-slate-400')}
                  title={`Weighs ${factor.weight}`}>
                  {factor.points > 0 ? `+${factor.points}` : '0'}
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <div className="mt-5">
        <h4 className="mb-2 inline-flex items-center gap-2 text-sm font-semibold text-slate-900">
          <MessageSquareWarning className="h-4 w-4" aria-hidden /> Complaints and concerns
          {open.length > 0 && <span className="rounded-full bg-rose-50 px-2 py-0.5 text-xs text-rose-700">{open.length} open</span>}
        </h4>
        {concerns.isError ? (
          <LoadFailed what="Their complaints and concerns" error={concerns.error} onRetry={() => { void concerns.refetch(); }} retrying={concerns.isFetching} />
        ) : concerns.isLoading ? (
          <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
        ) : list.length === 0 ? (
          <p className="text-sm text-slate-500">None recorded.</p>
        ) : (
          <ul className="space-y-2">
            {list.map((c) => (
              <li key={c.id} className={cn('rounded-xl border p-3 text-sm', c.resolvedAtUtc ? 'border-slate-100 bg-slate-50' : 'border-rose-100 bg-rose-50/40')}>
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="text-xs font-semibold uppercase tracking-wide text-slate-500">
                    {c.kind === 'complaint' ? 'Complaint' : 'Concern'} · {f.date(c.recordedAtUtc)}{c.recordedByName ? ` · ${c.recordedByName}` : ''}
                  </span>
                  <WriteOnly permission={PERMISSION.ClubMembers}>
                    <div className="flex gap-1">
                      {!c.resolvedAtUtc && (
                        <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-xs" disabled={resolve.isPending}
                          onClick={() => {
                            const resolution = window.prompt('How was it resolved? (optional)', '');
                            if (resolution !== null) resolve.mutate({ concern: c, resolution });
                          }}>
                          <CheckCircle2 className="h-3.5 w-3.5" aria-hidden /> Resolved
                        </Button>
                      )}
                      <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
                        <Button type="button" size="sm" variant="ghost" className="h-7 px-2 text-xs" aria-label="Remove" disabled={remove.isPending}
                          onClick={() => { if (window.confirm('Remove this for good? Resolving it keeps the record of it.')) remove.mutate(c); }}>
                          <Trash2 className="h-3.5 w-3.5" aria-hidden />
                        </Button>
                      </WriteOnly>
                    </div>
                  </WriteOnly>
                </div>
                <p className="mt-1 text-slate-800">{c.note}</p>
                {c.resolvedAtUtc && (
                  <p className="mt-1 text-xs text-slate-500">
                    Resolved {f.date(c.resolvedAtUtc)}{c.resolvedByName ? ` by ${c.resolvedByName}` : ''}{c.resolution ? `: ${c.resolution}` : '.'}
                  </p>
                )}
              </li>
            ))}
          </ul>
        )}
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <form
            className="mt-3 space-y-2"
            onSubmit={(e) => {
              e.preventDefault();
              if (note.trim()) add.mutate();
            }}
          >
            <div className="flex gap-2 text-sm" role="radiogroup" aria-label="Kind">
              {(['complaint', 'concern'] as const).map((k) => (
                <label key={k} className="inline-flex items-center gap-1">
                  <input type="radio" name={`concern-kind-${memberId}`} value={k} checked={kind === k} onChange={() => setKind(k)} />
                  {k === 'complaint' ? 'Complaint' : 'Concern'}
                </label>
              ))}
            </div>
            <Textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={2000} rows={2}
              placeholder="What happened, in a sentence or two" aria-label="Complaint or concern" />
            <Button type="submit" size="sm" disabled={!note.trim() || add.isPending}>
              {add.isPending && <Loader2 className="h-3.5 w-3.5 animate-spin" aria-hidden />} Record
            </Button>
          </form>
        </WriteOnly>
      </div>
    </div>
  );
}

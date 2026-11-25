// Who an audience reaches, as the server counted it: how many the filters chose, how many get
// it, and who is left out and why. The same figures for a campaign and an event's invitations.

import { Loader2, RefreshCw, Users } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { leftOutCounts, members, reasonLabel, type MemberAudience } from '@/app/api/memberAudience';

export function AudienceSummary({ audience, loading, error, onRetry, noun = 'email' }: {
  audience?: MemberAudience | null;
  loading?: boolean;
  /** Why the count did not load; shown with a way to try again. */
  error?: string | null;
  onRetry?: () => void;
  /** What each member gets: "email", "invitation". */
  noun?: string;
}) {
  if (error) {
    return (
      <div role="alert" className="flex flex-wrap items-center gap-2 rounded-lg border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-800">
        <span className="min-w-0 flex-1">Who it reaches could not be worked out. {error}</span>
        {onRetry && (
          <Button type="button" size="sm" variant="ghost" onClick={onRetry} className="h-7 px-2 text-rose-700">
            <RefreshCw className="h-3.5 w-3.5" aria-hidden /> Retry
          </Button>
        )}
      </div>
    );
  }
  if (!audience) {
    return (
      <p className="flex items-center gap-2 text-sm text-slate-500" aria-live="polite">
        <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Working out who it reaches…
      </p>
    );
  }
  const reasons = leftOutCounts(audience);
  const leftOut = audience.chosen - audience.willReceive;
  return (
    <div className="space-y-2 text-sm text-slate-700" aria-live="polite" aria-busy={loading || undefined}>
      <p className="flex flex-wrap items-center gap-x-2">
        <Users className="h-4 w-4 text-fuchsia-700" aria-hidden />
        <span>
          <strong>{members(audience.chosen)}</strong> chosen by the filters.{' '}
          <strong data-testid="audience-will-receive">{audience.willReceive.toLocaleString()}</strong> {noun}{audience.willReceive === 1 ? '' : 's'} would go.
        </span>
        {loading && <Loader2 className="h-3.5 w-3.5 animate-spin text-slate-400" aria-label="Counting again" />}
      </p>
      {audience.view && <p className="text-xs text-slate-500">Starting from the saved view “{audience.view.name}”.</p>}
      {reasons.length > 0 && (
        <div>
          <p className="text-xs font-medium text-slate-600">{members(leftOut)} left out:</p>
          <ul className="mt-1 flex flex-wrap gap-1.5" aria-label="Left out, by reason">
            {reasons.map((r) => (
              <li key={r.reason} className="rounded-full bg-amber-50 px-2 py-0.5 text-xs text-amber-900 ring-1 ring-inset ring-amber-200">
                {r.count.toLocaleString()} {r.label}
              </li>
            ))}
          </ul>
        </div>
      )}
      {(audience.recipients.length > 0 || audience.leftOut.length > 0) && (
        <details className="text-xs">
          <summary className="cursor-pointer text-slate-600">Who that is</summary>
          <div className="mt-1 grid gap-3 sm:grid-cols-2">
            {audience.recipients.length > 0 && (
              <div>
                <p className="font-medium text-slate-700">Gets it</p>
                <ul className="mt-0.5 space-y-0.5">
                  {audience.recipients.map((p) => (
                    <li key={p.memberId} className="truncate">{p.name}{p.email ? <span className="text-slate-500"> · {p.email}</span> : null}</li>
                  ))}
                  {audience.willReceive > audience.recipients.length && (
                    <li className="text-slate-500">and {members(audience.willReceive - audience.recipients.length)} more</li>
                  )}
                </ul>
              </div>
            )}
            {audience.leftOut.length > 0 && (
              <div>
                <p className="font-medium text-slate-700">Left out</p>
                <ul className="mt-0.5 space-y-0.5">
                  {audience.leftOut.map((p) => (
                    <li key={p.memberId} className="truncate">{p.name} <span className="text-slate-500">· {reasonLabel(p.reason)}</span></li>
                  ))}
                </ul>
              </div>
            )}
          </div>
        </details>
      )}
    </div>
  );
}

import { Users, TriangleAlert } from 'lucide-react';
import { useMoney } from '@/app/hooks/useMoney';
import type { PipelineValueByAssignee } from '@/app/api/reporting';
import { LoadFailed, RetryButton } from '@/app/components/LoadFailed';

interface TeamPerformanceProps {
  members: PipelineValueByAssignee[];
  loaded: boolean;
  failed: boolean;
  /** Read the figures again; shown with the failure. */
  onRetry?: () => void;
  /** A retry is on its way. */
  retrying?: boolean;
  /**
   * The figures on screen are the last ones read: a refresh failed since. Said in a
   * slim strip above them, as they may be out of date.
   */
  stale?: boolean;
}

function Shell({ children, notice }: { children: React.ReactNode; notice?: React.ReactNode }) {
  return (
    <section className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
      <div className="px-6 py-4 border-b border-slate-100 flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-500 to-teal-500 flex items-center justify-center">
          <Users className="w-5 h-5 text-white" />
        </div>
        <div>
          <h2 className="text-base font-bold text-slate-900">Team Performance</h2>
          <p className="text-xs text-slate-500">Open pipeline value by team member</p>
        </div>
      </div>
      <div className="p-6">{notice}{children}</div>
    </section>
  );
}

export function TeamPerformance({ members, loaded, failed, onRetry, retrying, stale = false }: TeamPerformanceProps) {
  // In the workspace's number format, like every other amount in the app.
  const money = useMoney();
  const staleNote = stale && onRetry
    ? <LoadFailed stale what="The team's figures" onRetry={onRetry} retrying={retrying} className="mb-4" />
    : null;
  // Until the scope fix in ReportingService this panel could only ever show the
  // signed-in user's own deals, because the query filtered on UserId even inside
  // an organisation. It is now genuinely the team.
  if (failed) {
    return (
      <Shell>
        <div className="flex items-start gap-2.5 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3">
          <TriangleAlert className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" aria-hidden />
          <p className="flex-1 text-sm text-amber-900">
            <strong className="font-semibold">Could not load team figures.</strong>{' '}
            An empty list here would have looked like a team with no deals.
          </p>
          {onRetry && <RetryButton onRetry={onRetry} retrying={retrying} />}
        </div>
      </Shell>
    );
  }

  if (!loaded) {
    return (
      <Shell>
        <div className="space-y-3" aria-busy="true">
          <p className="sr-only" role="status">Loading team figures…</p>
          {[0, 1, 2].map((i) => (
            <div key={i} className="flex items-center gap-4 rounded-xl bg-slate-50 p-3">
              <div className="h-10 w-10 shrink-0 animate-pulse rounded-full bg-slate-200" />
              <div className="flex-1 space-y-2">
                <div className="h-3 w-32 animate-pulse rounded bg-slate-200" />
                <div className="h-2.5 w-16 animate-pulse rounded bg-slate-100" />
              </div>
            </div>
          ))}
        </div>
      </Shell>
    );
  }

  if (members.length === 0) {
    return (
      <Shell notice={staleNote}>
        <p className="py-4 text-center text-sm text-slate-500">
          No open deals assigned yet.
        </p>
      </Shell>
    );
  }

  return (
    <Shell notice={staleNote}>
      <div className="space-y-3">
        {members.map((member) => (
          <div
            // One row per person per currency, so the key needs both.
            key={`${member.assigneeUserId || 'unassigned'}-${member.currency}`}
            className="flex items-center gap-4 p-3 bg-slate-50 rounded-xl hover:bg-slate-100 transition-colors"
          >
            <div className="w-10 h-10 rounded-full bg-gradient-to-br from-slate-600 to-slate-700 flex items-center justify-center text-white font-semibold text-sm">
              {member.assigneeName.charAt(0).toUpperCase()}
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-slate-900 truncate">{member.assigneeName}</p>
              <p className="text-xs text-slate-500">
                {member.dealCount} deal{member.dealCount === 1 ? '' : 's'}
              </p>
            </div>
            <p className="text-sm font-bold text-emerald-700">
              {money.format(member.value, member.currency)}
            </p>
          </div>
        ))}
      </div>
    </Shell>
  );
}

export default TeamPerformance;

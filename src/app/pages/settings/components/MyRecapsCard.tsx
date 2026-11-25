// Settings › Notifications: the signed-in person's own recap emails in this workspace. Shown to
// people who see all deals (the recaps are the whole team's figures); each switch saves at once,
// unlike the personal settings above, which the page's Save keeps. Nothing in demo mode or
// outside a workspace.

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Loader2, Newspaper } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import { RECAP_FREQUENCIES, setMyRecaps, type RecapChange } from '@/app/api/recaps';
import { LoadFailed } from '@/app/components/LoadFailed';
import { useMyRecaps } from '@/app/hooks/queries/recaps';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { ToggleSwitch } from './NotificationsSection';

export function MyRecapsCard() {
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const inWorkspace = scope.orgId !== null && !servesDemoData();
  const mine = useMyRecaps(inWorkspace);

  const save = useMutation({
    mutationFn: (change: RecapChange) => setMyRecaps(change),
    onSuccess: (saved) => {
      client.setQueryData(queryKeys.recaps.mine(scope), saved);
      void client.invalidateQueries({ queryKey: queryKeys.recaps.recipients(scope) });
      toast.success('Saved.');
    },
    onError: (err) => toast.error(err instanceof Error && err.message ? err.message : 'The recap could not be switched.'),
  });

  if (!inWorkspace) return null;

  const header = (
    <div className="flex items-center gap-2 mb-4">
      <Newspaper className="w-5 h-5 text-slate-500" aria-hidden="true" />
      <h3 className="font-semibold text-slate-900">Recap emails</h3>
    </div>
  );

  if (mine.isError && !mine.data) {
    return (
      <div className="pt-6 border-t border-slate-200">
        {header}
        <LoadFailed what="Your recap emails" error={mine.error} onRetry={() => void mine.refetch()} retrying={mine.isFetching} />
      </div>
    );
  }
  const data = mine.data;
  if (!data) {
    return (
      <div className="pt-6 border-t border-slate-200">
        {header}
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
      </div>
    );
  }
  if (!data.eligible) {
    return (
      <div className="pt-6 border-t border-slate-200">
        {header}
        <p className="text-sm text-slate-600">
          The recap emails are the whole team&apos;s figures, so they go to people who see all deals, which your role in this workspace does not.
        </p>
      </div>
    );
  }

  return (
    <div className="pt-6 border-t border-slate-200">
      {header}
      <p className="mb-3 text-sm text-slate-600">
        This workspace&apos;s sales funnel and club, each figure beside the period before, in your language, from 7:00 in its time
        zone ({data.timeZone}). Nothing to report, nothing sent. These switches save as you change them.
      </p>
      <div className="space-y-3">
        {RECAP_FREQUENCIES.map((f) => (
          <ToggleSwitch
            key={f.key}
            comingSoon={false}
            checked={data[f.key]}
            disabled={save.isPending}
            onChange={(on) => save.mutate({ [f.key]: on })}
            label={`${f.label} recap`}
            description={f.when}
          />
        ))}
      </div>
    </div>
  );
}

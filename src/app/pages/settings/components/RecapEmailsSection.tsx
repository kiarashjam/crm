// Settings › Recap emails: who in the workspace receives the daily, weekly and monthly recap,
// for those who manage the settings. Only people who see all deals are listed: a recap is the
// whole team's figures. Each switch saves at once; each person can switch theirs in
// Settings › Notifications too, and what they chose shows here.

import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';
import { Info, Loader2, Newspaper } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import { orgCan, PERMISSION, PRESET_LABELS } from '@/app/api/organizations';
import { RECAP_FREQUENCIES, setRecapRecipient, type RecapChange, type RecapRecipient, type RecapRecipients } from '@/app/api/recaps';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Switch } from '@/app/components/ui/switch';
import { useRecapRecipients } from '@/app/hooks/queries/recaps';
import { queryKeys } from '@/app/hooks/queries/queryKeys';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';

export function RecapEmailsSection() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const canManage = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  const recipients = useRecapRecipients(canManage && !servesDemoData());

  const save = useMutation({
    mutationFn: ({ person, change }: { person: RecapRecipient; change: RecapChange }) => setRecapRecipient(person.userId, change),
    onSuccess: (saved) => {
      client.setQueryData<RecapRecipients | null>(queryKeys.recaps.recipients(scope), (list) =>
        list ? { ...list, people: list.people.map((p) => (p.userId === saved.userId ? saved : p)) } : list);
      // The person's own switches, in case it was the signed-in person's.
      void client.invalidateQueries({ queryKey: queryKeys.recaps.mine(scope) });
      toast.success('Saved.');
    },
    onError: (err) => toast.error(err instanceof Error && err.message ? err.message : 'The recap could not be switched.'),
  });

  const header = (
    <div>
      <h2 className="flex items-center gap-2 text-xl font-semibold text-slate-900">
        <Newspaper className="h-5 w-5 text-orange-600" aria-hidden="true" />
        Recap emails
      </h2>
      <p className="mt-1 text-sm text-slate-600">
        A daily, weekly and monthly email of the sales funnel and the club: new leads by source and referral, invitations to
        meet, meetings, contracts and deposits, conversions, the pipeline by phase and, with the club on, visits, events, members
        at risk and renewals. Each figure stands beside the period before.
      </p>
    </div>
  );

  if (!currentOrg) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="text-sm text-slate-600">Recap emails belong to a workspace. Open one to set them up.</p>
      </div>
    );
  }
  if (!canManage) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="text-sm text-slate-600">
          Those who manage this workspace&apos;s settings choose who receives the recaps. You can switch your own in Notifications.
        </p>
      </div>
    );
  }
  if (servesDemoData()) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="text-sm text-slate-600">Demo data sends no email, so there are no recaps to set up.</p>
      </div>
    );
  }
  if (recipients.isError && !recipients.data) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <LoadFailed what="Who receives the recaps" error={recipients.error} onRetry={() => void recipients.refetch()} retrying={recipients.isFetching} />
      </div>
    );
  }
  const data = recipients.data;
  if (!data) {
    return (
      <div className="space-y-6 p-6">
        {header}
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading…</p>
      </div>
    );
  }

  const busyFor = save.isPending ? save.variables?.person.userId : undefined;

  return (
    <div className="space-y-6 p-6">
      {header}

      <section aria-labelledby="recaps-when" className="rounded-xl border border-slate-200 bg-slate-50 p-4">
        <h3 id="recaps-when" className="font-semibold text-slate-900">When they go</h3>
        <ul className="mt-2 space-y-1 text-sm text-slate-700">
          {RECAP_FREQUENCIES.map((f) => (
            <li key={f.key}><span className="font-medium">{f.label}:</span> {f.when}</li>
          ))}
        </ul>
        <p className="mt-2 text-xs text-slate-500">
          In the workspace&apos;s time zone ({data.timeZone}), in each person&apos;s own language, from the workspace&apos;s sender. A period
          with nothing to report sends nothing. The first recap after switching one on is the next period that ends.
        </p>
      </section>

      <section aria-labelledby="recaps-who" className="space-y-3">
        <h3 id="recaps-who" className="font-semibold text-slate-900">Who receives them</h3>
        {data.people.length === 0 ? (
          <p className="text-sm text-slate-600">Nobody here sees all deals yet.</p>
        ) : (
          <div className="overflow-x-auto rounded-xl border border-slate-200">
            <table className="w-full text-sm">
              <thead className="bg-slate-50 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">
                <tr>
                  <th scope="col" className="px-4 py-2">Person</th>
                  {RECAP_FREQUENCIES.map((f) => <th key={f.key} scope="col" className="px-4 py-2 text-center">{f.label}</th>)}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100">
                {data.people.map((person) => (
                  <tr key={person.userId}>
                    <td className="px-4 py-3">
                      <p className="font-medium text-slate-900">{person.name}</p>
                      <p className="text-xs text-slate-500">{person.email} · {PRESET_LABELS[person.preset] ?? person.preset}</p>
                    </td>
                    {RECAP_FREQUENCIES.map((f) => (
                      <td key={f.key} className="px-4 py-3 text-center">
                        <Switch
                          checked={person[f.key]}
                          disabled={busyFor === person.userId}
                          onCheckedChange={(on) => save.mutate({ person, change: { [f.key]: on } })}
                          aria-label={`${f.label} recap for ${person.name}`}
                        />
                      </td>
                    ))}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <p className="flex items-start gap-2 text-xs text-slate-500">
          <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" />
          <span>
            Only people who see all deals can receive a recap: it is the whole team&apos;s figures.
            {data.notEligible > 0 && ` ${data.notEligible === 1 ? '1 other person' : `${data.notEligible} others`} on the team ${data.notEligible === 1 ? 'does' : 'do'} not.`}
            {' '}To let someone read everything without changing or exporting anything, give them the Read-only preset on the Team page.
          </span>
        </p>
      </section>
    </div>
  );
}

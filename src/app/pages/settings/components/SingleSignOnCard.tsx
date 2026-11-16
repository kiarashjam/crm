// The workspace's single sign-on requirement: the owner can require the team to sign in with
// Google or Microsoft, for the members whose address is at the workspace's own domains (or
// for everyone but the owner when none are listed). Sessions that began another way are then
// refused by this workspace at once; they keep working in the members' other workspaces.
// The owner is never held to it, so a provider that stops working cannot lock the workspace out.

import { useEffect, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { getOrganizationSso, updateOrganizationSso, type OrganizationSso } from '@/app/api/signIn';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';
import { workspacePrefix } from '@/app/hooks/queries/queryKeys';
import { LoadFailed } from '@/app/components/LoadFailed';

const field = 'w-full h-11 px-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none disabled:bg-slate-50 disabled:text-slate-600';

/** "pavillon46.ch, p46.ch" as the list the server takes. */
function parseDomains(text: string): string[] {
  return text.split(/[\s,;]+/).map((d) => d.trim()).filter(Boolean);
}

export function SingleSignOnCard() {
  const org = useOrgOptional();
  const currentOrg = org?.currentOrg ?? null;
  const scope = useWorkspaceScope();
  const client = useQueryClient();
  // Whoever may see the workspace's settings or team; the server lets only the owner change it.
  const canSee = Boolean(currentOrg) && !servesDemoData()
    && (currentOrg!.isOwner || orgCan(currentOrg!, PERMISSION.ManageSettings) || orgCan(currentOrg!, PERMISSION.ManageMembers));
  const key = [...workspacePrefix(scope), 'organization-sso', currentOrg?.id ?? null] as const;
  const policy = useQuery({ queryKey: key, queryFn: () => getOrganizationSso(currentOrg!.id), enabled: canSee });
  const [domains, setDomains] = useState('');

  useEffect(() => {
    if (policy.data) setDomains(policy.data.domains.join(', '));
  }, [policy.data]);

  const save = useMutation({
    mutationFn: (change: { required?: boolean; domains?: string[] }) => updateOrganizationSso(currentOrg!.id, change),
    onSuccess: (saved: OrganizationSso, change) => {
      client.setQueryData(key, saved);
      void org?.refreshOrgs();
      toast.success(change.required === undefined
        ? 'The workspace’s domains are saved.'
        : saved.required ? 'Single sign-on is now required here.' : 'Single sign-on is no longer required here.');
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'The setting could not be saved.'),
  });

  if (!canSee) return null;

  const data = policy.data;
  const held = data?.members.filter((m) => m.applies) ?? [];
  const linked = held.filter((m) => m.hasSignedInWithSso).length;

  return (
    <section aria-labelledby="workspace-sso" className="pt-6 border-t border-slate-200">
      <h3 id="workspace-sso" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <ShieldCheck className="w-4 h-4" aria-hidden />
        Single sign-on
      </h3>
      <p className="text-sm text-slate-600 mb-4">
        Require the team to sign in with Google or Microsoft to work in this workspace. Sessions signed in with a password or a
        passkey are then refused here at once, and keep working in people&apos;s other workspaces. The owner is never held to it.
      </p>

      {policy.isError ? (
        <LoadFailed what="The single sign-on setting" error={policy.error} onRetry={() => void policy.refetch()} retrying={policy.isFetching} />
      ) : !data ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : (
        <div className="space-y-4">
          {data.providers.length === 0 && !data.required && (
            <p className="rounded-lg bg-amber-50 p-3 text-sm text-amber-900" role="status">
              This server offers no single sign-on yet, so it cannot be required. Whoever runs Cadence sets up Google or Microsoft sign-in first.
            </p>
          )}
          <label className="flex items-start gap-3">
            <input
              type="checkbox"
              className="mt-1 h-4 w-4"
              checked={data.required}
              disabled={!data.canChange || save.isPending || (!data.required && data.providers.length === 0)}
              onChange={(e) => save.mutate({ required: e.target.checked, domains: parseDomains(domains) })}
            />
            <span>
              <span className="block text-sm font-medium text-slate-900">Require single sign-on</span>
              <span className="block text-xs text-slate-600">
                {data.required
                  ? `Required for ${held.length} member${held.length === 1 ? '' : 's'}; ${linked} of them ${linked === 1 ? 'has' : 'have'} signed in with Google or Microsoft before.`
                  : 'Off: everyone signs in as they do now.'}
              </span>
            </span>
          </label>

          <form
            onSubmit={(e) => { e.preventDefault(); save.mutate({ domains: parseDomains(domains) }); }}
            className="space-y-2"
          >
            <label htmlFor="sso-domains" className="block text-sm font-medium text-slate-700">The workspace&apos;s email domains</label>
            <div className="flex gap-2">
              <input
                id="sso-domains"
                value={domains}
                disabled={!data.canChange}
                onChange={(e) => setDomains(e.target.value)}
                placeholder="pavillon46.ch"
                className={field}
              />
              {data.canChange && (
                <button type="submit" disabled={save.isPending} className="h-11 shrink-0 px-4 rounded-xl border border-slate-300 font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60">
                  Save
                </button>
              )}
            </div>
            <p className="text-xs text-slate-600">
              With domains, the requirement holds members whose address is at one of them, and others (an outside accountant) keep
              signing in as before. Without, it holds every member but the owner.
            </p>
          </form>

          {data.required && held.length > 0 && (
            <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200 text-sm" aria-label="Members it holds">
              {held.map((m) => (
                <li key={m.userId} className="flex items-center justify-between gap-2 p-3">
                  <span className="min-w-0 truncate">{m.name} <span className="text-slate-500">({m.email})</span></span>
                  <span className={`shrink-0 rounded-full px-2 py-0.5 text-xs ${m.hasSignedInWithSso ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-600'}`}>
                    {m.hasSignedInWithSso ? 'Signed in with SSO' : 'Not yet'}
                  </span>
                </li>
              ))}
            </ul>
          )}

          {!data.canChange && <p className="text-xs text-slate-500">Only the workspace&apos;s owner can change this.</p>}
        </div>
      )}
    </section>
  );
}

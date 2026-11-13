// The Google and Microsoft accounts the signed-in person signs in with. One is linked the
// first time the person signs in with it and the provider vouches for their address.

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { LogIn } from 'lucide-react';
import { toast } from 'sonner';
import { getSignInOptions, getSsoLinks, unlinkSso, type SsoLink } from '@/app/api/signIn';
import { servesDemoData } from '@/app/api/apiClient';
import { getCurrentUser } from '@/app/lib/auth';
import { LoadFailed } from '@/app/components/LoadFailed';

const PROVIDER_NAMES: Record<string, string> = { google: 'Google', microsoft: 'Microsoft' };

export function SsoLinksCard() {
  const client = useQueryClient();
  const userId = getCurrentUser()?.id ?? 'signed-out';
  const key = ['account', userId, 'sso-links'] as const;
  const options = useQuery({ queryKey: ['sign-in-options'], queryFn: getSignInOptions, staleTime: 5 * 60_000, enabled: !servesDemoData() });
  const links = useQuery({ queryKey: key, queryFn: getSsoLinks, enabled: !servesDemoData() });

  const unlink = useMutation({
    mutationFn: (link: SsoLink) => unlinkSso(link.id),
    onSuccess: (_, link) => {
      toast.success(`${PROVIDER_NAMES[link.provider] ?? link.provider} no longer signs you in.`);
      void client.invalidateQueries({ queryKey: key });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'It could not be unlinked.'),
  });

  if (servesDemoData()) return null;
  // Nothing to show where the server offers no single sign-on and nothing was ever linked.
  if (links.data?.length === 0 && options.data && options.data.sso.length === 0) return null;

  return (
    <section aria-labelledby="sso-links-heading" className="p-6 border border-slate-200 rounded-xl space-y-3">
      <div>
        <h3 id="sso-links-heading" className="font-semibold text-slate-900 flex items-center gap-2">
          <LogIn className="w-5 h-5 text-slate-600" aria-hidden />
          Google and Microsoft sign-in
        </h3>
        <p className="text-sm text-slate-600 mt-1">
          The accounts you sign in with. One is added the first time you choose it on the sign-in page and it confirms your address.
        </p>
      </div>
      {links.isError ? (
        <LoadFailed what="Your sign-in accounts" error={links.error} onRetry={() => void links.refetch()} retrying={links.isFetching} />
      ) : links.isPending ? (
        <p className="text-sm text-slate-500">Loading…</p>
      ) : links.data.length === 0 ? (
        <p className="text-sm text-slate-600">
          None yet. To add one, sign out and choose{' '}
          {(options.data?.sso.length ? options.data.sso : [{ id: 'google', name: 'Google' }]).map((p) => `“Continue with ${p.name}”`).join(' or ')}
          {' '}on the sign-in page.
        </p>
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
          {links.data.map((link) => (
            <li key={link.id} className="flex flex-wrap items-center justify-between gap-2 p-3">
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-900">{PROVIDER_NAMES[link.provider] ?? link.provider}</p>
                <p className="text-xs text-slate-500">{link.email}</p>
              </div>
              <button type="button" disabled={unlink.isPending} onClick={() => unlink.mutate(link)} className="text-sm text-slate-600 hover:text-red-600">
                Unlink
              </button>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

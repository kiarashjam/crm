// Shown in place of a workspace's pages when the workspace requires single sign-on and this
// session began another way (a password or a passkey). The server refuses every request into
// the workspace from such a session (Sso.Required); this says why and offers the way in,
// rather than a page of failed lists. The session goes on working in the person's other
// workspaces.

import { Link, useLocation } from 'react-router-dom';
import { useQuery } from '@tanstack/react-query';
import { ShieldCheck } from 'lucide-react';
import { useState } from 'react';
import { getSignInOptions } from '@/app/api/signIn';
import type { Organization } from '@/app/api/organizations';
import { SsoButtons } from './AlternativeSignIn';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export function SsoRequiredGate({ org }: { org: Organization }) {
  const location = useLocation();
  const options = useQuery({ queryKey: ['sign-in-options'], queryFn: getSignInOptions, staleTime: 5 * 60_000, retry: 1 });
  const [error, setError] = useState<string | null>(null);
  const providers = options.data?.sso ?? [];

  return (
    <main id={MAIN_CONTENT_ID} tabIndex={-1} className="flex min-h-screen items-center justify-center bg-gradient-subtle px-[var(--page-padding)]">
      <div className="w-full max-w-md rounded-2xl border border-slate-200/80 bg-white p-8 shadow-sm">
        <ShieldCheck className="h-8 w-8 text-orange-600" aria-hidden />
        <h1 className="mt-3 text-lg font-semibold text-slate-900">{org.name} asks you to sign in with single sign-on</h1>
        <p className="mt-2 text-sm text-slate-600">
          Its owner requires the team to sign in with Google or Microsoft to work here. You are signed in another way,
          which still works in your other workspaces.
        </p>
        <div className="mt-6">
          {options.isPending ? null : providers.length > 0 ? (
            <SsoButtons providers={providers} returnTo={location.pathname} onError={setError} />
          ) : (
            <p className="text-sm text-amber-800" role="status">
              This server offers no single sign-on at the moment. Ask the workspace&apos;s owner, who can always sign in, to check it.
            </p>
          )}
          {error && <p className="mt-3 text-sm text-red-700" role="alert">{error}</p>}
        </div>
        <Link to="/organizations" className="mt-6 inline-block text-sm font-medium text-orange-700 hover:text-orange-800">
          Choose another workspace
        </Link>
      </div>
    </main>
  );
}

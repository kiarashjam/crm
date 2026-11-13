// The signed-in person's passkeys: add one on this device, see when each was last used,
// remove one they no longer have. A passkey signs in without the password, and stands in
// for the authenticator code where 2FA is on.

import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { KeyRound, Loader2, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { addPasskey, getSignInOptions, listPasskeys, passkeysSupported, removePasskey, type Passkey } from '@/app/api/signIn';
import { servesDemoData } from '@/app/api/apiClient';
import { getCurrentUser } from '@/app/lib/auth';
import { LoadFailed } from '@/app/components/LoadFailed';

const field = 'w-full h-11 px-3 border border-slate-300 rounded-xl bg-white text-slate-900 placeholder:text-slate-400 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none';

/** A name for a passkey made on this device, from what the browser says it is. */
function deviceName(): string {
  const agent = typeof navigator === 'undefined' ? '' : navigator.userAgent;
  if (/iPhone/.test(agent)) return 'iPhone';
  if (/iPad/.test(agent)) return 'iPad';
  if (/Android/.test(agent)) return 'Android phone';
  if (/Macintosh/.test(agent)) return 'Mac';
  if (/Windows/.test(agent)) return 'Windows PC';
  return 'Passkey';
}

function when(iso: string | null): string {
  if (!iso) return 'never';
  return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
}

export function PasskeysCard() {
  const client = useQueryClient();
  const userId = getCurrentUser()?.id ?? 'signed-out';
  const key = ['account', userId, 'passkeys'] as const;
  const options = useQuery({ queryKey: ['sign-in-options'], queryFn: getSignInOptions, staleTime: 5 * 60_000, enabled: !servesDemoData() });
  const passkeys = useQuery({ queryKey: key, queryFn: listPasskeys, enabled: !servesDemoData() });
  const [name, setName] = useState(deviceName);
  const [password, setPassword] = useState('');
  const [confirming, setConfirming] = useState<string | null>(null);

  const add = useMutation({
    mutationFn: () => addPasskey(name.trim() || deviceName(), password),
    onSuccess: (added) => {
      setPassword('');
      toast.success(`Passkey “${added.name}” added. You can sign in with it now.`);
      void client.invalidateQueries({ queryKey: key });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'The passkey could not be added.'),
  });

  const remove = useMutation({
    mutationFn: (passkey: Passkey) => removePasskey(passkey.id),
    onSuccess: (_, passkey) => {
      setConfirming(null);
      toast.success(`Passkey “${passkey.name}” removed.`);
      void client.invalidateQueries({ queryKey: key });
    },
    onError: (err) => toast.error(err instanceof Error ? err.message : 'The passkey could not be removed.'),
  });

  // The demo has no account to sign in to.
  if (servesDemoData()) return null;

  return (
    <section aria-labelledby="passkeys-heading" className="p-6 border border-slate-200 rounded-xl space-y-4">
      <div>
        <h3 id="passkeys-heading" className="font-semibold text-slate-900 flex items-center gap-2">
          <KeyRound className="w-5 h-5 text-slate-600" aria-hidden />
          Passkeys
        </h3>
        <p className="text-sm text-slate-600 mt-1">
          Sign in with your fingerprint, face or device PIN instead of a password. Where two-factor sign-in is on, a passkey
          can stand in for the code. The key stays on your device or in your password manager; Cadence keeps only its public half.
        </p>
      </div>

      {options.data && !options.data.passkeys ? (
        <p className="text-sm text-slate-600" role="status">Passkeys are not set up on this server.</p>
      ) : passkeys.isError ? (
        <LoadFailed what="Your passkeys" error={passkeys.error} onRetry={() => void passkeys.refetch()} retrying={passkeys.isFetching} />
      ) : passkeys.isPending ? (
        <p className="text-sm text-slate-500">Loading your passkeys…</p>
      ) : (
        <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200" aria-label="Your passkeys">
          {passkeys.data.length === 0 && <li className="p-3 text-sm text-slate-600">No passkeys yet.</li>}
          {passkeys.data.map((passkey) => (
            <li key={passkey.id} className="flex flex-wrap items-center justify-between gap-2 p-3">
              <div className="min-w-0">
                <p className="text-sm font-medium text-slate-900">
                  {passkey.name}
                  {passkey.synced && <span className="ml-2 rounded-full bg-slate-100 px-2 py-0.5 text-xs font-normal text-slate-600">Synced</span>}
                </p>
                <p className="text-xs text-slate-500">Added {when(passkey.createdAtUtc)} · last used {when(passkey.lastUsedAtUtc)}</p>
              </div>
              {confirming === passkey.id ? (
                <span className="flex items-center gap-2 text-sm">
                  <span className="text-slate-700">Remove it?</span>
                  <button type="button" disabled={remove.isPending} onClick={() => remove.mutate(passkey)} className="font-semibold text-red-600 hover:text-red-700">
                    Remove
                  </button>
                  <button type="button" onClick={() => setConfirming(null)} className="text-slate-600 hover:text-slate-800">Cancel</button>
                </span>
              ) : (
                <button type="button" onClick={() => setConfirming(passkey.id)} className="inline-flex items-center gap-1 text-sm text-slate-600 hover:text-red-600" aria-label={`Remove passkey ${passkey.name}`}>
                  <Trash2 className="h-4 w-4" aria-hidden /> Remove
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {options.data?.passkeys && (passkeysSupported() ? (
        <form
          className="grid gap-3 sm:grid-cols-[1fr_1fr_auto] sm:items-end"
          onSubmit={(e) => { e.preventDefault(); if (!add.isPending) add.mutate(); }}
        >
          <div>
            <label htmlFor="passkey-name" className="block text-sm font-medium text-slate-700 mb-1">Name</label>
            <input id="passkey-name" value={name} maxLength={60} onChange={(e) => setName(e.target.value)} className={field} />
          </div>
          <div>
            <label htmlFor="passkey-password" className="block text-sm font-medium text-slate-700 mb-1">Your password</label>
            <input
              id="passkey-password"
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className={field}
              autoComplete="current-password"
              placeholder="Not needed if your account has none"
            />
          </div>
          <button type="submit" disabled={add.isPending} className="h-11 px-5 bg-orange-600 hover:bg-orange-700 disabled:opacity-60 text-white font-medium rounded-xl inline-flex items-center justify-center gap-2">
            {add.isPending && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
            Add a passkey
          </button>
        </form>
      ) : (
        <p className="text-sm text-slate-600">This browser cannot use passkeys. Try a current version of Chrome, Edge, Safari or Firefox.</p>
      ))}
    </section>
  );
}

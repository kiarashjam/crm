// The sign-in page's ways in besides the password: Google or Microsoft (single sign-on)
// when the server is set up for them, and a passkey when the server and the browser can
// use one. Nothing shows for a way the server does not offer, so no button promises a
// sign-in that cannot happen.

import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { KeyRound, Loader2 } from 'lucide-react';
import { getSignInOptions, passkeysSupported, signInWithPasskey, startSso, type SsoProvider } from '@/app/api/signIn';
import type { LoginResponse } from '@/app/api/auth';
import type { Language } from '@/app/i18n/languages';
import { useT } from '@/app/i18n';

/** The providers' own marks, drawn small and plain so they read as theirs. */
function ProviderMark({ id }: { id: string }) {
  if (id === 'microsoft') {
    return (
      <svg viewBox="0 0 21 21" className="h-4 w-4" aria-hidden>
        <rect x="1" y="1" width="9" height="9" fill="#f25022" />
        <rect x="11" y="1" width="9" height="9" fill="#7fba00" />
        <rect x="1" y="11" width="9" height="9" fill="#00a4ef" />
        <rect x="11" y="11" width="9" height="9" fill="#ffb900" />
      </svg>
    );
  }
  return (
    <svg viewBox="0 0 48 48" className="h-4 w-4" aria-hidden>
      <path fill="#FFC107" d="M43.6 20.5H42V20H24v8h11.3C33.7 32.7 29.3 36 24 36c-6.6 0-12-5.4-12-12s5.4-12 12-12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 12.9 4 4 12.9 4 24s8.9 20 20 20 20-8.9 20-20c0-1.3-.1-2.4-.4-3.5z" />
      <path fill="#FF3D00" d="M6.3 14.7l6.6 4.8C14.7 15.1 19 12 24 12c3.1 0 5.8 1.2 7.9 3.1l5.7-5.7C34 6.1 29.3 4 24 4 16.3 4 9.7 8.3 6.3 14.7z" />
      <path fill="#4CAF50" d="M24 44c5.2 0 9.9-2 13.4-5.2l-6.2-5.2C29.2 35.1 26.7 36 24 36c-5.3 0-9.7-3.3-11.3-8l-6.5 5C9.5 39.6 16.2 44 24 44z" />
      <path fill="#1976D2" d="M43.6 20.5H42V20H24v8h11.3c-.8 2.2-2.2 4.2-4.1 5.6l6.2 5.2C37 39.2 44 34 44 24c0-1.3-.1-2.4-.4-3.5z" />
    </svg>
  );
}

const buttonCls =
  'flex h-11 w-full items-center justify-center gap-2 rounded-xl border border-slate-300 bg-white text-sm font-semibold text-slate-700 transition-colors hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2';

/** The single sign-on buttons on their own (the sign-in page, and a workspace asking for them). */
export function SsoButtons({ providers, returnTo, language, onError }: {
  providers: SsoProvider[];
  /** Where to come back to in the app once signed in. */
  returnTo?: string | null;
  /** The language picked on the sign-in page, kept for the account once back (undefined: none was). */
  language?: Language | null;
  onError?: (message: string) => void;
}) {
  const { t } = useT();
  const [leaving, setLeaving] = useState<string | null>(null);
  if (providers.length === 0) return null;

  const go = async (provider: SsoProvider) => {
    setLeaving(provider.id);
    try {
      await startSso(provider.id, returnTo, language);
    } catch (err) {
      setLeaving(null);
      onError?.(err instanceof Error ? err.message : t('login.ssoCouldNotStart'));
    }
  };

  return (
    <div className="space-y-2">
      {providers.map((provider) => (
        <button key={provider.id} type="button" className={buttonCls} disabled={leaving !== null} onClick={() => void go(provider)}>
          {leaving === provider.id ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <ProviderMark id={provider.id} />}
          {t('login.continueWith', { provider: provider.name })}
        </button>
      ))}
    </div>
  );
}

export interface AlternativeSignInProps {
  /** A session or the two-factor step, as the password sign-in hands back. */
  onSignedIn: (response: LoginResponse) => void;
  onError: (message: string) => void;
  /** Offer a passkey too (signing in; creating an account has none yet). */
  withPasskey?: boolean;
  returnTo?: string | null;
  /** The language picked on the sign-in page, which single sign-on carries through the provider (undefined: none was). */
  language?: Language | null;
}

export function AlternativeSignIn({ onSignedIn, onError, withPasskey = true, returnTo, language }: AlternativeSignInProps) {
  const { t } = useT();
  const options = useQuery({ queryKey: ['sign-in-options'], queryFn: getSignInOptions, staleTime: 5 * 60_000, retry: 1 });
  const [busy, setBusy] = useState(false);

  // Without the server's answer the password still works: the other ways simply do not show.
  const sso = options.data?.sso ?? [];
  const passkey = withPasskey && Boolean(options.data?.passkeys) && passkeysSupported();
  if (sso.length === 0 && !passkey) return null;

  const withKey = async () => {
    setBusy(true);
    try {
      onSignedIn(await signInWithPasskey());
    } catch (err) {
      onError(err instanceof Error ? err.message : t('login.passkeyFailed'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-2" aria-label={t('login.otherWays')}>
      <SsoButtons providers={sso} returnTo={returnTo} language={language} onError={onError} />
      {passkey && (
        <button type="button" className={buttonCls} disabled={busy} onClick={() => void withKey()}>
          {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <KeyRound className="h-4 w-4" aria-hidden />}
          {t('login.passkey')}
        </button>
      )}
    </div>
  );
}

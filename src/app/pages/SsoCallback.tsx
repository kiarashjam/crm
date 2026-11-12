// Where single sign-on lands: the API sends the browser here from Google or Microsoft with a
// one-time code (or the reason it failed) in the address fragment, which never reaches a
// server log. The code is traded, with this tab's secret, for the session; when the account
// has two-factor sign-in on, the sign-in page asks for the code or a passkey next.

import { useEffect, useRef, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { AlertCircle, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { completeSso, readSsoLanding, ssoErrorMessage } from '@/app/api/signIn';
import { messages } from '@/app/api/messages';
import { setSession } from '@/app/lib/auth';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function SsoCallback() {
  const navigate = useNavigate();
  const [failure, setFailure] = useState<string | null>(null);
  // The code works once: a second run (React's development double effect) must not spend it again.
  const started = useRef(false);

  useEffect(() => {
    if (started.current) return;
    started.current = true;
    const { code, error } = readSsoLanding(window.location.hash);
    // The code is spent below; it should not stay in the address or the history.
    window.history.replaceState(window.history.state, '', window.location.pathname);
    if (error || !code) {
      setFailure(ssoErrorMessage(error));
      return;
    }
    completeSso(code)
      .then(({ response, returnTo }) => {
        if (response.requiresTwoFactor && response.twoFactorToken) {
          navigate('/login', { replace: true, state: { twoFactorToken: response.twoFactorToken, returnTo } });
          return;
        }
        if (!response.token || !response.user) throw new Error(ssoErrorMessage('server_error'));
        setSession(response.token, response.user);
        toast.success(messages.auth.signedIn);
        navigate(returnTo ?? '/organizations', { replace: true });
      })
      .catch((err: unknown) => setFailure(err instanceof Error && err.message ? err.message : ssoErrorMessage('server_error')));
  }, [navigate]);

  return (
    <main id={MAIN_CONTENT_ID} tabIndex={-1} className="flex min-h-screen items-center justify-center bg-gradient-subtle px-[var(--page-padding)]">
      <div className="w-full max-w-md rounded-2xl border border-slate-200/80 bg-white p-8 text-center shadow-sm">
        {failure ? (
          <>
            <AlertCircle className="mx-auto h-8 w-8 text-red-500" aria-hidden />
            <h1 className="mt-3 text-lg font-semibold text-slate-900">You are not signed in</h1>
            <p className="mt-2 text-sm text-slate-600" role="alert">{failure}</p>
            <Link
              to="/login"
              replace
              className="mt-6 inline-flex h-10 items-center justify-center rounded-lg bg-gradient-to-r from-orange-600 to-orange-500 px-4 text-sm font-semibold text-white hover:from-orange-500 hover:to-orange-400"
            >
              Back to sign in
            </Link>
          </>
        ) : (
          <>
            <Loader2 className="mx-auto h-8 w-8 animate-spin text-orange-600" aria-hidden />
            <h1 className="mt-3 text-lg font-semibold text-slate-900">Signing you in…</h1>
          </>
        )}
      </div>
    </main>
  );
}

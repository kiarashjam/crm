import { Link, useSearchParams } from 'react-router-dom';
import { useEffect, useRef, useState } from 'react';
import { ArrowLeft, CheckCircle2, Loader2, MailWarning } from 'lucide-react';
import { toast } from 'sonner';
import { resendVerificationEmail, verifyEmail } from '@/app/api/auth';
import { isUsingRealApi } from '@/app/api/apiClient';
import { getCurrentUser, updateCurrentUser } from '@/app/lib/auth';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

type State =
  | { kind: 'working' }
  | { kind: 'done' }
  | { kind: 'failed'; message: string };

/**
 * Where the confirmation email's link lands. Public: the link is often opened on a
 * phone where nobody is signed in, and the token in it is the proof.
 */
export default function VerifyEmail() {
  const [searchParams] = useSearchParams();
  const token = (searchParams.get('token') ?? '').trim();
  const user = getCurrentUser();
  const signedIn = !!user && user.id !== 'demo';
  const [state, setState] = useState<State>(() => {
    if (!isUsingRealApi()) return { kind: 'failed', message: 'Confirming an email address needs the app to be connected to the Cadence server.' };
    if (!token) return { kind: 'failed', message: 'This link is missing its code. Open the link from the email again.' };
    return { kind: 'working' };
  });
  const [sending, setSending] = useState(false);
  // The token works once; a second request (React's development double effect) would fail.
  const sent = useRef(false);

  useEffect(() => {
    if (state.kind !== 'working' || sent.current) return;
    sent.current = true;
    verifyEmail(token)
      .then(() => {
        if (signedIn) updateCurrentUser({ emailVerified: true });
        setState({ kind: 'done' });
      })
      .catch((e: unknown) => {
        setState({ kind: 'failed', message: e instanceof Error ? e.message : 'This link could not be used.' });
      });
  }, [state.kind, token, signedIn]);

  const resend = async () => {
    if (sending) return;
    setSending(true);
    try {
      await resendVerificationEmail();
      toast.success(`We sent a new confirmation link to ${user?.email ?? 'your address'}.`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not send a new link.');
    } finally {
      setSending(false);
    }
  };

  const alreadyConfirmed = signedIn && user?.emailVerified === true;

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 dark:from-slate-900 dark:to-slate-950 flex flex-col">
      <header className="w-full px-[var(--page-padding)] py-[var(--header-block-padding-y)]" role="banner">
        <Link
          to={signedIn ? '/organizations' : '/login'}
          className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg"
        >
          <ArrowLeft className="w-5 h-5" aria-hidden />
          {signedIn ? 'Back to Cadence' : 'Back to sign in'}
        </Link>
      </header>

      <main
        id={MAIN_CONTENT_ID}
        className="flex-1 flex items-center justify-center px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
        tabIndex={-1}
      >
        <div className="w-full max-w-md bg-white rounded-2xl shadow-sm border border-slate-200 p-8 text-center" role="status" aria-live="polite">
          {state.kind === 'working' && (
            <>
              <Loader2 className="w-10 h-10 mx-auto mb-4 animate-spin text-orange-600" aria-hidden />
              <h1 className="text-xl font-bold text-slate-900">Confirming your email address…</h1>
            </>
          )}

          {state.kind === 'done' && (
            <>
              <CheckCircle2 className="w-12 h-12 mx-auto mb-4 text-emerald-600" aria-hidden />
              <h1 className="text-xl font-bold text-slate-900 mb-2">Your email address is confirmed</h1>
              <p className="text-slate-600 mb-6">
                You can now join workspaces you are invited to. If an invitation brought you here, open its link
                from the email again to accept it.
              </p>
              <Link
                to={signedIn ? '/organizations' : '/login'}
                className="inline-flex h-11 items-center justify-center rounded-xl bg-orange-600 px-6 font-semibold text-white hover:bg-orange-500"
              >
                {signedIn ? 'Continue' : 'Sign in'}
              </Link>
            </>
          )}

          {state.kind === 'failed' && (
            <>
              <MailWarning className="w-12 h-12 mx-auto mb-4 text-amber-600" aria-hidden />
              <h1 className="text-xl font-bold text-slate-900 mb-2">
                {alreadyConfirmed ? 'Your email address is already confirmed' : 'This link did not work'}
              </h1>
              <p className="text-slate-600 mb-6">
                {alreadyConfirmed
                  ? 'Nothing more to do here.'
                  : signedIn
                    ? `${state.message} You can send yourself a new link.`
                    : `${state.message} Sign in, and send yourself a new link from the banner at the top of the page.`}
              </p>
              {signedIn && !alreadyConfirmed && isUsingRealApi() ? (
                <button
                  type="button"
                  onClick={() => void resend()}
                  disabled={sending}
                  className="inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-orange-600 px-6 font-semibold text-white hover:bg-orange-500 disabled:opacity-60"
                >
                  {sending && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
                  {sending ? 'Sending…' : 'Send a new link'}
                </button>
              ) : (
                <Link
                  to={signedIn ? '/organizations' : '/login'}
                  className="inline-flex h-11 items-center justify-center rounded-xl bg-orange-600 px-6 font-semibold text-white hover:bg-orange-500"
                >
                  {signedIn ? 'Continue' : 'Sign in'}
                </Link>
              )}
            </>
          )}
        </div>
      </main>
    </div>
  );
}

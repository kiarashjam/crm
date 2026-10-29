import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import { useState } from 'react';
import { ArrowLeft, Loader2, MailCheck, UserPlus } from 'lucide-react';
import { toast } from 'sonner';
import { acceptInvite } from '@/app/api/organizations';
import { resendVerificationEmail } from '@/app/api/auth';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { useOrg } from '@/app/contexts/OrgContext';
import { clearSession, getCurrentUser } from '@/app/lib/auth';
import { INVITE_ACCEPT_PATH } from '@/app/lib/inviteLink';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmailVerificationBanner from '@/app/components/EmailVerificationBanner';

/**
 * Where an invitation email's link lands. Accepting needs the token in that link —
 * it is the only proof the person accepting received the email — so this page is the
 * one way in. It asks before joining rather than joining on load, so a link preview
 * or a mail scanner opening the page does nothing.
 */
export default function AcceptInvite() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const token = (searchParams.get('token') ?? '').trim();
  const { refreshOrgs, setCurrentOrg } = useOrg();
  const user = getCurrentUser();
  const [busy, setBusy] = useState(false);
  const [problem, setProblem] = useState<{ message: string; needsConfirmation: boolean } | null>(null);
  const [sending, setSending] = useState(false);

  const accept = async () => {
    if (busy || !token) return;
    setBusy(true);
    setProblem(null);
    try {
      const accepted = await acceptInvite(token);
      if (!accepted) throw new Error('The invitation could not be accepted.');
      await refreshOrgs();
      setCurrentOrg(accepted.organizationId);
      toast.success(`You have joined ${accepted.organizationName}`);
      navigate('/dashboard', { replace: true });
    } catch (e) {
      setProblem({
        message: e instanceof Error ? e.message : 'The invitation could not be accepted.',
        needsConfirmation: e instanceof ApiError && e.status === 403,
      });
    } finally {
      setBusy(false);
    }
  };

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

  const switchAccount = () => {
    clearSession();
    navigate('/login', {
      replace: true,
      state: { from: { pathname: INVITE_ACCEPT_PATH, search: `?token=${encodeURIComponent(token)}` } },
    });
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 dark:from-slate-900 dark:to-slate-950 flex flex-col">
      <header className="w-full px-[var(--page-padding)] py-[var(--header-block-padding-y)]" role="banner">
        <Link
          to="/organizations"
          className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg"
        >
          <ArrowLeft className="w-5 h-5" aria-hidden />
          Your workspaces
        </Link>
      </header>
      <EmailVerificationBanner />

      <main
        id={MAIN_CONTENT_ID}
        className="flex-1 flex items-center justify-center px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
        tabIndex={-1}
      >
        <div className="w-full max-w-md bg-white rounded-2xl shadow-sm border border-slate-200 p-8 text-center">
          <UserPlus className="w-12 h-12 mx-auto mb-4 text-orange-600" aria-hidden />
          <h1 className="text-xl font-bold text-slate-900 mb-2">Join a workspace</h1>

          {servesDemoData() || !token ? (
            <p className="text-slate-600">
              {servesDemoData()
                ? 'Invitations are for real accounts. Sign in with the address the invitation was sent to.'
                : 'This link is missing its code. Open the link from the invitation email again.'}
            </p>
          ) : (
            <>
              <p className="text-slate-600 mb-6">
                You are signed in as <strong>{user?.email}</strong>. The invitation must have been sent to this
                address.
              </p>

              {problem && (
                <div className="mb-6 rounded-xl border border-amber-200 bg-amber-50 p-4 text-left text-sm text-amber-900 dark:border-amber-900 dark:text-amber-100" role="alert">
                  <p>{problem.message}</p>
                  {problem.needsConfirmation && (
                    <p className="mt-2">
                      After you open the confirmation link, come back to this page and accept again.
                    </p>
                  )}
                </div>
              )}

              <button
                type="button"
                onClick={() => void accept()}
                disabled={busy}
                className="w-full inline-flex h-11 items-center justify-center gap-2 rounded-xl bg-orange-600 px-6 font-semibold text-white hover:bg-orange-500 disabled:opacity-60"
              >
                {busy && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />}
                {busy ? 'Joining…' : 'Accept invitation'}
              </button>

              {problem?.needsConfirmation && (
                <button
                  type="button"
                  onClick={() => void resend()}
                  disabled={sending}
                  className="mt-3 w-full inline-flex h-11 items-center justify-center gap-2 rounded-xl border-2 border-slate-200 px-6 font-medium text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                >
                  <MailCheck className="h-4 w-4" aria-hidden />
                  {sending ? 'Sending…' : 'Send a new confirmation link'}
                </button>
              )}

              <button
                type="button"
                onClick={switchAccount}
                className="mt-4 text-sm font-medium text-orange-700 hover:text-orange-800"
              >
                Use a different account
              </button>
            </>
          )}
        </div>
      </main>
    </div>
  );
}

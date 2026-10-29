import { useEffect, useRef, useState } from 'react';
import { Loader2, MailWarning } from 'lucide-react';
import { toast } from 'sonner';
import { me, resendVerificationEmail } from '@/app/api/auth';
import { isUsingRealApi } from '@/app/api/apiClient';
import { getCurrentUser, isDemoMode, updateCurrentUser } from '@/app/lib/auth';

/**
 * Full-width banner shown while the signed-in account has not confirmed its email
 * address. Invisible otherwise.
 *
 * An unconfirmed account works in its own workspace, but the server will not let it
 * accept an invitation, ask to join, or be added by email, because anyone can register
 * any address. Saying so here is better than letting the person find out from a refusal.
 */
export default function EmailVerificationBanner() {
  const [user, setUser] = useState(getCurrentUser);
  const [sending, setSending] = useState(false);
  const checked = useRef(false);
  const applies = !!user && isUsingRealApi() && !isDemoMode();
  const known = user?.emailVerified;

  // Ask the server once, and only when the answer could be "not confirmed": a session
  // saved before the server reported it, or one confirmed since (perhaps on a phone).
  useEffect(() => {
    if (checked.current || !applies || known === true) return;
    checked.current = true;
    me()
      .then((fresh) => {
        if (typeof fresh?.emailVerified !== 'boolean') return;
        updateCurrentUser({ emailVerified: fresh.emailVerified });
        setUser(getCurrentUser());
      })
      .catch(() => {});
  }, [applies, known]);

  if (!user || !applies || known !== false) return null;

  const resend = async () => {
    if (sending) return;
    setSending(true);
    try {
      await resendVerificationEmail();
      toast.success(`We sent a new confirmation link to ${user.email}.`);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not send a new link.');
    } finally {
      setSending(false);
    }
  };

  return (
    <div
      className="w-full border-b border-sky-200/80 bg-sky-50 px-[var(--page-padding)] py-2 dark:border-sky-900 dark:bg-sky-950/40"
      role="status"
      aria-live="polite"
    >
      <div className="flex flex-wrap items-center justify-center gap-3 text-sm font-medium text-sky-900 dark:text-sky-100">
        <MailWarning className="h-4 w-4 shrink-0 text-sky-700" aria-hidden />
        <span className="max-w-3xl text-center">
          Confirm your email address, <strong>{user.email}</strong>, to join other workspaces. Open the link we
          emailed you.
        </span>
        <button
          type="button"
          onClick={() => void resend()}
          disabled={sending}
          className="inline-flex items-center gap-1 rounded-md border border-sky-300 bg-white/70 px-2 py-1 text-xs font-semibold text-sky-900 transition-colors hover:bg-white disabled:opacity-60 dark:border-sky-800 dark:bg-transparent dark:text-sky-100"
        >
          {sending && <Loader2 className="h-3 w-3 animate-spin" aria-hidden />}
          {sending ? 'Sending…' : 'Send a new link'}
        </button>
      </div>
    </div>
  );
}

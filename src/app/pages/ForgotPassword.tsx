import { Link } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { ArrowLeft, Sparkles } from 'lucide-react';
import { toast } from 'sonner';
import { requestPasswordReset, messages } from '@/app/api';
import { isUsingRealApi } from '@/app/api/apiClient';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function ForgotPassword() {
  const [email, setEmail] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const canSubmit = useMemo(() => email.trim().length > 3 && email.includes('@'), [email]);

  const handleSubmit = async () => {
    if (!canSubmit) return;
    if (!isUsingRealApi()) {
      toast.error(messages.auth.passwordResetRequiresApi);
      return;
    }
    setSubmitting(true);
    try {
      await requestPasswordReset(email.trim());
      toast.success(messages.auth.passwordResetEmailSent);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : messages.errors.generic);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 dark:from-slate-900 dark:to-slate-950 flex flex-col">
      <header className="w-full px-[var(--page-padding)] py-[var(--header-block-padding-y)]" role="banner">
        <Link
          to="/login"
          className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg"
          aria-label="Back to sign in"
        >
          <ArrowLeft className="w-5 h-5" aria-hidden />
          Back to sign in
        </Link>
      </header>

      <main
        id={MAIN_CONTENT_ID}
        className="flex-1 flex items-center justify-center px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
        tabIndex={-1}
      >
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div
              className="w-16 h-16 bg-gradient-to-br from-orange-500 to-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-lg shadow-orange-500/25 ring-1 ring-orange-400/20"
              aria-hidden
            >
              <Sparkles className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Forgot password</h1>
            <p className="text-slate-600">
              Enter your account email and we will send you a link to choose a new password.
            </p>
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
            <div className="mb-6">
              <label className="block text-sm font-medium text-slate-700 mb-2">Email</label>
              <input
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                placeholder="you@company.com"
                autoComplete="email"
                inputMode="email"
                onKeyDown={(e) => {
                  if (e.key === 'Enter') void handleSubmit();
                }}
              />
            </div>

            <button
              type="button"
              disabled={!canSubmit || submitting}
              onClick={() => void handleSubmit()}
              className="w-full bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed text-white font-semibold py-4 px-6 rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
            >
              {submitting ? 'Sending…' : 'Send reset link'}
            </button>

            <p className="text-center text-sm text-slate-500 mt-6">
              Remember your password?{' '}
              <Link to="/login" className="text-orange-600 hover:text-orange-700 font-semibold focus-visible:underline">
                Sign in
              </Link>
            </p>
          </div>
        </div>
      </main>
    </div>
  );
}

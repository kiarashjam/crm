import { Link, useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { ArrowLeft, Sparkles, Play } from 'lucide-react';
import { toast } from 'sonner';
import { login, loginWithTwoFactor, register, messages } from '@/app/api';
import { setSession, setDemoUser } from '@/app/lib/auth';
import { getApiBaseUrl } from '@/app/api/apiClient';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/app/components/ui/input-otp';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';

export default function Login() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const [requires2fa, setRequires2fa] = useState(false);
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [lastError, setLastError] = useState<string | null>(null);

  const canSubmit = useMemo(() => {
    if (requires2fa) return code.replace(/\D/g, '').length === 6 && !!twoFactorToken;
    if (!email.trim() || !password) return false;
    if (mode === 'register') return !!name.trim();
    return true;
  }, [requires2fa, code, twoFactorToken, email, password, mode, name]);

  const handleSubmit = async () => {
    if (!canSubmit) return;
    setLastError(null);
    setSubmitting(true);
    try {
      if (requires2fa) {
        const res = await loginWithTwoFactor(twoFactorToken!, code);
        if (!res.token || !res.user) throw new Error('Login failed');
        setSession(res.token, res.user);
        toast.success(messages.auth.signedIn);
        navigate('/dashboard', { replace: true });
        return;
      }

      const res =
        mode === 'register'
          ? await register(name.trim(), email.trim(), password)
          : await login(email.trim(), password);

      if (res.requiresTwoFactor && res.twoFactorToken) {
        setRequires2fa(true);
        setTwoFactorToken(res.twoFactorToken);
        toast.message(messages.auth.twoFactorCodeRequired);
        return;
      }

      if (!res.token || !res.user) throw new Error('Login failed');
      setSession(res.token, res.user);
      toast.success(mode === 'register' ? messages.auth.accountCreated : messages.auth.signedIn);
      navigate('/dashboard', { replace: true });
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      setLastError(msg);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-b from-orange-50/80 to-slate-50 flex flex-col">
      <header className="w-full px-[var(--page-padding)] py-6" role="banner">
        <Link to="/" className="flex items-center gap-2 text-slate-600 hover:text-slate-900 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2 rounded-lg" aria-label="Back to home">
          <ArrowLeft className="w-5 h-5" aria-hidden />
          Back to home
        </Link>
      </header>

      <main id={MAIN_CONTENT_ID} className="flex-1 flex items-center justify-center px-[var(--page-padding)] py-12" tabIndex={-1}>
        <div className="w-full max-w-md">
          <div className="text-center mb-8">
            <div className="w-16 h-16 bg-orange-600 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-lg" aria-hidden>
              <Sparkles className="w-10 h-10 text-white" />
            </div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight mb-2">Welcome back</h1>
            <p className="text-slate-600">
              {requires2fa ? 'Enter your 2FA code to continue' : 'Sign in to start creating AI-powered copy'}
            </p>
          </div>

          <div className="bg-white rounded-2xl shadow-sm border border-slate-200 p-8">
            {!requires2fa ? (
              <>
                <div className="flex gap-2 mb-6">
                  <button
                    type="button"
                    onClick={() => setMode('login')}
                    className={`flex-1 h-10 rounded-lg border font-medium ${
                      mode === 'login' ? 'border-orange-500 bg-orange-50 text-orange-700' : 'border-slate-200 text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    Sign in
                  </button>
                  <button
                    type="button"
                    onClick={() => setMode('register')}
                    className={`flex-1 h-10 rounded-lg border font-medium ${
                      mode === 'register' ? 'border-orange-500 bg-orange-50 text-orange-700' : 'border-slate-200 text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    Create account
                  </button>
                </div>

                {mode === 'register' && (
                  <div className="mb-4">
                    <label className="block text-sm font-medium text-slate-700 mb-2">Name</label>
                    <input
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                      placeholder="Jane Doe"
                      autoComplete="name"
                    />
                  </div>
                )}

                <div className="mb-4">
                  <label className="block text-sm font-medium text-slate-700 mb-2">Email</label>
                  <input
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                    placeholder="you@company.com"
                    autoComplete="email"
                    inputMode="email"
                  />
                </div>

                <div className="mb-6">
                  <label className="block text-sm font-medium text-slate-700 mb-2">Password</label>
                  <input
                    type="password"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full h-11 px-4 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none transition-colors"
                    autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
                  />
                </div>

                <button
                  type="button"
                  disabled={!canSubmit || submitting}
                  onClick={handleSubmit}
                  className="w-full bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed text-white font-semibold py-4 px-6 rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  {submitting ? 'Please wait…' : mode === 'register' ? 'Create account' : 'Sign in'}
                </button>

                <p className="mt-4 text-center">
                  <Link to="/help" className="text-sm text-slate-500 hover:text-orange-600 focus-visible:underline">
                    Forgot password?
                  </Link>
                </p>

                {!getApiBaseUrl() && (
                  <>
                    <div className="flex items-center gap-4 my-6" role="presentation" aria-hidden>
                      <div className="flex-1 border-t border-slate-200" />
                      <span className="text-slate-500 text-sm">or</span>
                      <div className="flex-1 border-t border-slate-200" />
                    </div>
                    <button
                      type="button"
                      onClick={() => {
                        setDemoUser({ name: 'Demo User', email: 'demo@example.com' });
                        toast.success(messages.auth.demoMode);
                        navigate('/dashboard', { replace: true });
                      }}
                      className="w-full bg-emerald-600 hover:bg-emerald-500 text-white font-semibold py-4 px-6 rounded-xl flex items-center justify-center gap-2 transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2"
                    >
                      <Play className="w-5 h-5" aria-hidden />
                      Try demo (no backend)
                    </button>
                  </>
                )}
              </>
            ) : (
              <>
                <div className="mb-6">
                  <label className="block text-sm font-medium text-slate-700 mb-2">Authentication code</label>
                  <InputOTP maxLength={6} value={code} onChange={setCode}>
                    <InputOTPGroup>
                      {Array.from({ length: 6 }).map((_, i) => (
                        <InputOTPSlot key={i} index={i} />
                      ))}
                    </InputOTPGroup>
                  </InputOTP>
                </div>

                <button
                  type="button"
                  disabled={!canSubmit || submitting}
                  onClick={handleSubmit}
                  className="w-full bg-orange-600 hover:bg-orange-500 disabled:bg-slate-200 disabled:text-slate-500 disabled:cursor-not-allowed text-white font-semibold py-4 px-6 rounded-xl transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                >
                  {submitting ? 'Verifying…' : 'Verify & sign in'}
                </button>

                <button
                  type="button"
                  onClick={() => {
                    setRequires2fa(false);
                    setTwoFactorToken(null);
                    setCode('');
                  }}
                  className="w-full mt-3 bg-white hover:bg-slate-50 border-2 border-slate-200 text-slate-700 font-semibold py-3 px-6 rounded-xl transition-colors"
                >
                  Back
                </button>
              </>
            )}

            <p className="text-center text-sm text-slate-500 mt-6">
              Connect your CRM in one click after signing in
            </p>

            <nav className="flex justify-center gap-4 mt-6 text-sm" aria-label="Legal links">
              <Link to="/privacy" className="text-orange-600 hover:text-orange-700 focus-visible:underline">
                Privacy Policy
              </Link>
              <span className="text-slate-300" aria-hidden>•</span>
              <Link to="/terms" className="text-orange-600 hover:text-orange-700 focus-visible:underline">
                Terms of Service
              </Link>
            </nav>
          </div>

          <p className="text-center text-slate-600 mt-6">
            Having trouble?{' '}
            <Link to="/help" className="text-orange-600 hover:text-orange-700 font-semibold underline-offset-2 hover:underline focus-visible:underline">
              See help
            </Link>
          </p>
        </div>
      </main>
    </div>
  );
}

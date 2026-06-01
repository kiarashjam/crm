import { Link, useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import {
  ArrowLeft, ArrowRight, Sparkles, Play, Mail, Lock, User as UserIcon,
  Eye, EyeOff, Loader2, AlertCircle, ShieldCheck, Workflow, BarChart3, Bot,
} from 'lucide-react';
import { toast } from 'sonner';
import { login, loginWithTwoFactor, register, messages } from '@/app/api';
import { setSession, setDemoUser } from '@/app/lib/auth';
import { isUsingRealApi } from '@/app/api/apiClient';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/app/components/ui/input-otp';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { cn } from '@/app/components/ui/utils';

const HIGHLIGHTS = [
  { icon: Bot, title: 'AI that writes with you', body: 'Draft emails, follow-ups and notes in your voice.' },
  { icon: Workflow, title: 'Sequences & automation', body: 'Put follow-ups on autopilot so nothing slips.' },
  { icon: BarChart3, title: 'Pipeline & forecasting', body: 'See what’s real and what’s closing, at a glance.' },
];

export default function Login() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [requires2fa, setRequires2fa] = useState(false);
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(null);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);

  const canSubmit = useMemo(() => {
    if (requires2fa) return code.replace(/\D/g, '').length === 6 && !!twoFactorToken;
    if (!email.trim() || !password) return false;
    if (mode === 'register') return !!name.trim();
    return true;
  }, [requires2fa, code, twoFactorToken, email, password, mode, name]);

  const handleSubmit = async () => {
    if (!canSubmit || submitting) return;
    setError(null);
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
      navigate(isUsingRealApi() ? '/organizations' : '/dashboard', { replace: true });
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      setError(msg);
      toast.error(msg);
    } finally {
      setSubmitting(false);
    }
  };

  const startDemo = () => {
    setDemoUser({ name: 'Demo User', email: 'demo@example.com' });
    toast.success(messages.auth.demoMode);
    navigate('/organizations', { replace: true });
  };

  const switchMode = (next: 'login' | 'register') => { setMode(next); setError(null); };

  return (
    <div className="min-h-screen bg-gradient-subtle lg:grid lg:grid-cols-[1.05fr_0.95fr]">
      {/* ---------- Brand panel (lg+) ---------- */}
      <aside className="relative hidden overflow-hidden bg-gradient-to-br from-orange-600 via-orange-500 to-amber-500 p-12 text-white lg:flex lg:flex-col lg:justify-between">
        {/* decorative depth */}
        <div aria-hidden className="pointer-events-none absolute -top-24 -left-24 h-96 w-96 rounded-full bg-white/15 blur-3xl" />
        <div aria-hidden className="pointer-events-none absolute -bottom-28 -right-16 h-[28rem] w-[28rem] rounded-full bg-rose-500/30 blur-3xl" />
        <div aria-hidden className="pointer-events-none absolute inset-0 opacity-[0.07] [background-image:radial-gradient(circle,white_1px,transparent_1px)] [background-size:22px_22px]" />

        <div className="relative">
          <Link to="/" className="inline-flex items-center gap-2 text-white/90 transition-colors hover:text-white" aria-label="Cadence home">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 ring-1 ring-white/30 backdrop-blur">
              <Sparkles className="h-5 w-5" />
            </span>
            <span className="text-lg font-bold tracking-tight">Cadence</span>
          </Link>
        </div>

        <div className="relative max-w-md">
          <h2 className="text-4xl font-bold leading-tight tracking-tight">
            The CRM that writes with you.
          </h2>
          <p className="mt-4 text-lg text-white/85">
            Capture leads, draft outreach with AI, and keep every deal moving — all in one calm, fast workspace.
          </p>
          <ul className="mt-10 space-y-5">
            {HIGHLIGHTS.map((h) => (
              <li key={h.title} className="flex items-start gap-4">
                <span className="mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                  <h.icon className="h-5 w-5" />
                </span>
                <div>
                  <p className="font-semibold">{h.title}</p>
                  <p className="text-sm text-white/80">{h.body}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>

        <div className="relative flex items-center gap-3 text-sm text-white/80">
          <ShieldCheck className="h-4 w-4" />
          Secure sign-in · Your data stays yours
        </div>
      </aside>

      {/* ---------- Form panel ---------- */}
      <div className="flex min-h-screen flex-col lg:min-h-0">
        <header className="px-[var(--page-padding)] py-[var(--header-block-padding-y)]" role="banner">
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-lg text-sm text-slate-500 transition-colors hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
            aria-label="Back to home"
          >
            <ArrowLeft className="h-4 w-4" aria-hidden /> Back to home
          </Link>
        </header>

        <main id={MAIN_CONTENT_ID} className="flex flex-1 items-center justify-center px-[var(--page-padding)] pb-10" tabIndex={-1}>
          <div className="w-full max-w-md">
            {/* Mobile logo */}
            <Link to="/" className="mb-6 flex items-center justify-center gap-2 lg:hidden" aria-hidden>
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-orange-600 text-white shadow-lg shadow-orange-500/25">
                <Sparkles className="h-5 w-5" />
              </span>
              <span className="text-lg font-bold tracking-tight text-slate-900">Cadence</span>
            </Link>

            <div className="mb-7 text-center lg:text-left">
              <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
                {requires2fa ? 'Two-factor verification' : mode === 'register' ? 'Create your account' : 'Welcome back'}
              </h1>
              <p className="mt-2 text-slate-500">
                {requires2fa
                  ? 'Enter the 6-digit code from your authenticator app.'
                  : mode === 'register'
                    ? 'Start qualifying and closing leads in minutes.'
                    : 'Sign in to pick up right where you left off.'}
              </p>
            </div>

            <div className="rounded-2xl border border-slate-200/80 bg-white p-6 shadow-[0_2px_4px_rgba(15,23,42,0.04),0_18px_40px_-24px_rgba(15,23,42,0.2)] sm:p-8">
              {!requires2fa ? (
                <form
                  onSubmit={(e) => { e.preventDefault(); handleSubmit(); }}
                  className="space-y-4"
                >
                  {/* Segmented mode toggle */}
                  <div className="grid grid-cols-2 gap-1 rounded-xl bg-slate-100 p-1" role="tablist" aria-label="Authentication mode">
                    {(['login', 'register'] as const).map((m) => (
                      <button
                        key={m}
                        type="button"
                        role="tab"
                        aria-selected={mode === m}
                        onClick={() => switchMode(m)}
                        className={cn(
                          'h-9 rounded-lg text-sm font-medium transition-all',
                          mode === m ? 'bg-white text-slate-900 shadow-sm' : 'text-slate-500 hover:text-slate-700',
                        )}
                      >
                        {m === 'login' ? 'Sign in' : 'Create account'}
                      </button>
                    ))}
                  </div>

                  {error && (
                    <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700" role="alert">
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
                      <span>{error}</span>
                    </div>
                  )}

                  {mode === 'register' && (
                    <Field label="Name" htmlFor="name" icon={<UserIcon className="h-4 w-4" />}>
                      <input
                        id="name"
                        value={name}
                        onChange={(e) => { setName(e.target.value); setError(null); }}
                        className={inputCls}
                        placeholder="Jane Doe"
                        autoComplete="name"
                        autoFocus
                      />
                    </Field>
                  )}

                  <Field label="Email" htmlFor="email" icon={<Mail className="h-4 w-4" />}>
                    <input
                      id="email"
                      type="email"
                      value={email}
                      onChange={(e) => { setEmail(e.target.value); setError(null); }}
                      className={inputCls}
                      placeholder="you@company.com"
                      autoComplete="email"
                      inputMode="email"
                      autoFocus={mode === 'login'}
                    />
                  </Field>

                  <Field
                    label="Password"
                    htmlFor="password"
                    icon={<Lock className="h-4 w-4" />}
                    action={mode === 'login' ? (
                      <Link to="/forgot-password" className="text-xs font-medium text-orange-600 hover:text-orange-700 focus-visible:underline">
                        Forgot?
                      </Link>
                    ) : undefined}
                  >
                    <input
                      id="password"
                      type={showPassword ? 'text' : 'password'}
                      value={password}
                      onChange={(e) => { setPassword(e.target.value); setError(null); }}
                      className={cn(inputCls, 'pr-11')}
                      autoComplete={mode === 'register' ? 'new-password' : 'current-password'}
                      placeholder="••••••••"
                    />
                    <button
                      type="button"
                      onClick={() => setShowPassword((s) => !s)}
                      aria-label={showPassword ? 'Hide password' : 'Show password'}
                      className="absolute right-3 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-400 hover:text-slate-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500"
                      tabIndex={-1}
                    >
                      {showPassword ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
                    </button>
                  </Field>

                  <button
                    type="submit"
                    disabled={!canSubmit || submitting}
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-orange-600 to-orange-500 font-semibold text-white shadow-lg shadow-orange-500/25 transition-all hover:from-orange-500 hover:to-orange-400 disabled:cursor-not-allowed disabled:from-slate-200 disabled:to-slate-200 disabled:text-slate-400 disabled:shadow-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                  >
                    {submitting ? (
                      <><Loader2 className="h-4 w-4 animate-spin" /> Please wait…</>
                    ) : (
                      <>{mode === 'register' ? 'Create account' : 'Sign in'} <ArrowRight className="h-4 w-4" /></>
                    )}
                  </button>

                  <div className="flex items-center gap-3 py-1" role="presentation" aria-hidden>
                    <div className="h-px flex-1 bg-slate-200" />
                    <span className="text-xs font-medium uppercase tracking-wider text-slate-400">or</span>
                    <div className="h-px flex-1 bg-slate-200" />
                  </div>

                  <button
                    type="button"
                    onClick={startDemo}
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 font-semibold text-emerald-700 transition-colors hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2"
                  >
                    <Play className="h-4 w-4" aria-hidden /> Explore the demo
                  </button>
                  <p className="text-center text-xs text-slate-400">No account needed — sample data, saved in your browser.</p>
                </form>
              ) : (
                <form onSubmit={(e) => { e.preventDefault(); handleSubmit(); }} className="space-y-5">
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">Authentication code</label>
                    <InputOTP maxLength={6} value={code} onChange={setCode} autoFocus containerClassName="justify-center">
                      <InputOTPGroup>
                        {Array.from({ length: 6 }).map((_, i) => (
                          <InputOTPSlot key={i} index={i} />
                        ))}
                      </InputOTPGroup>
                    </InputOTP>
                  </div>

                  {error && (
                    <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2.5 text-sm text-red-700" role="alert">
                      <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
                      <span>{error}</span>
                    </div>
                  )}

                  <button
                    type="submit"
                    disabled={!canSubmit || submitting}
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl bg-gradient-to-r from-orange-600 to-orange-500 font-semibold text-white shadow-lg shadow-orange-500/25 transition-all hover:from-orange-500 hover:to-orange-400 disabled:cursor-not-allowed disabled:from-slate-200 disabled:to-slate-200 disabled:text-slate-400 disabled:shadow-none focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                  >
                    {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> Verifying…</> : 'Verify & sign in'}
                  </button>

                  <button
                    type="button"
                    onClick={() => { setRequires2fa(false); setTwoFactorToken(null); setCode(''); setError(null); }}
                    className="h-11 w-full rounded-xl border border-slate-200 font-medium text-slate-600 transition-colors hover:bg-slate-50"
                  >
                    Back
                  </button>
                </form>
              )}
            </div>

            <nav className="mt-6 flex items-center justify-center gap-3 text-xs text-slate-400" aria-label="Legal links">
              <Link to="/privacy" className="hover:text-slate-600 focus-visible:underline">Privacy</Link>
              <span aria-hidden>·</span>
              <Link to="/terms" className="hover:text-slate-600 focus-visible:underline">Terms</Link>
              <span aria-hidden>·</span>
              <Link to="/help" className="hover:text-slate-600 focus-visible:underline">Help</Link>
            </nav>
          </div>
        </main>
      </div>
    </div>
  );
}

const inputCls =
  'h-11 w-full rounded-xl border border-slate-300 bg-white px-3.5 pl-10 text-slate-900 outline-none transition-colors placeholder:text-slate-400 focus:border-orange-500 focus:ring-2 focus:ring-orange-500/20';

/** Labeled field with a leading icon (and optional right-aligned action like “Forgot?”). */
function Field({
  label, htmlFor, icon, action, children,
}: {
  label: string; htmlFor: string; icon: React.ReactNode; action?: React.ReactNode; children: React.ReactNode;
}) {
  return (
    <div>
      <div className="mb-1.5 flex items-center justify-between">
        <label htmlFor={htmlFor} className="text-sm font-medium text-slate-700">{label}</label>
        {action}
      </div>
      <div className="relative">
        <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400" aria-hidden>{icon}</span>
        {children}
      </div>
    </div>
  );
}

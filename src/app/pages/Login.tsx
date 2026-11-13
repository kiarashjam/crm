import { Link, useLocation, useNavigate } from 'react-router-dom';
import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import {
  ArrowLeft, ArrowRight, Sparkles, Play, Mail, Lock, User as UserIcon,
  Eye, EyeOff, Loader2, AlertCircle, ShieldCheck, Workflow, BarChart3, Bot,
  AlertTriangle, WifiOff, Info, ShieldAlert, ServerCrash, Clock, RefreshCw,
} from 'lucide-react';
import { toast } from 'sonner';
import { login, loginWithTwoFactor, register, messages } from '@/app/api';
import { setSession, setDemoUser } from '@/app/lib/auth';
import { isUsingRealApi } from '@/app/api/apiClient';
import { InputOTP, InputOTPGroup, InputOTPSlot } from '@/app/components/ui/input-otp';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogDescription, DialogFooter,
} from '@/app/components/ui/dialog';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { cn } from '@/app/components/ui/utils';
import { invitationToResume } from '@/app/lib/inviteLink';
import { BrandName } from '@/app/components/BrandName';
import { useT, type Language, type MessageKey } from '@/app/i18n';
import { LanguageSwitcher } from '@/app/i18n/LanguageSwitcher';
import { AlternativeSignIn } from '@/app/components/auth/AlternativeSignIn';
import {
  getSignInOptions, keepWorkspaceFor, passkeySecondFactor, passkeysSupported, safeReturnTo, type KeptWorkspace,
} from '@/app/api/signIn';
import type { LoginResponse } from '@/app/api/auth';

const HIGHLIGHTS: { icon: typeof Bot; title: MessageKey; body: MessageKey }[] = [
  { icon: Bot, title: 'login.highlight1Title', body: 'login.highlight1Body' },
  { icon: Workflow, title: 'login.highlight2Title', body: 'login.highlight2Body' },
  { icon: BarChart3, title: 'login.highlight3Title', body: 'login.highlight3Body' },
];

export default function Login() {
  const navigate = useNavigate();
  const location = useLocation();
  const { t, choose } = useT();
  // Back from single sign-on with two-factor sign-in on: the code (or a passkey) is next,
  // with the language picked here before leaving for the provider, if one was.
  const fromSso = location.state as {
    twoFactorToken?: string; returnTo?: string | null; kept?: KeptWorkspace | null; language?: Language | null;
  } | null;
  // A language picked on this page is the person's latest choice: it goes onto their account
  // once they are signed in, whichever way in they take. One they did not touch leaves
  // whatever the account has. Chosen again through the app's language, not saved behind its
  // back: the account's answer to the question the app asks as the session starts is older,
  // and would switch the page back.
  const [pickedLanguage, setPickedLanguage] = useState<Language | null | undefined>(fromSso?.language);
  const keepPickedLanguage = () => {
    if (pickedLanguage !== undefined) void choose(pickedLanguage);
  };
  // An invitation link opened while signed out: go back to it, token and all.
  const resumeInvitation = invitationToResume(location.state);
  const afterSignIn = resumeInvitation ?? safeReturnTo(fromSso?.returnTo);
  // A passkey in place of the code only where the server takes passkeys (the same answer
  // the other ways in below read) and this browser can use one.
  const signInOptions = useQuery({ queryKey: ['sign-in-options'], queryFn: getSignInOptions, staleTime: 5 * 60_000, retry: 1 });
  const passkeyInsteadOfCode = Boolean(signInOptions.data?.passkeys) && passkeysSupported();
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  const [requires2fa, setRequires2fa] = useState(Boolean(fromSso?.twoFactorToken));
  const [twoFactorToken, setTwoFactorToken] = useState<string | null>(fromSso?.twoFactorToken ?? null);
  const [code, setCode] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [errorModal, setErrorModal] = useState<{ message: string; was2fa: boolean } | null>(null);

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
        keepPickedLanguage();
        keepWorkspaceFor(res.user, fromSso?.kept);
        toast.success(messages.auth.signedIn);
        navigate(afterSignIn ?? '/dashboard', { replace: true });
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
      keepPickedLanguage();
      toast.success(mode === 'register' ? messages.auth.accountCreated : messages.auth.signedIn);
      navigate(resumeInvitation ?? (isUsingRealApi() ? '/organizations' : '/dashboard'), { replace: true });
    } catch (e) {
      const msg = e instanceof Error ? e.message : messages.errors.generic;
      setError(msg);
      // Surface a detailed, explained failure dialog (what happened + why).
      setErrorModal({ message: msg, was2fa: requires2fa });
    } finally {
      setSubmitting(false);
    }
  };

  /** A sign-in another way (a passkey, or a passkey in place of the code) handed back a session or the 2FA step. */
  const signedInAnotherWay = (res: LoginResponse) => {
    if (res.requiresTwoFactor && res.twoFactorToken) {
      setRequires2fa(true);
      setTwoFactorToken(res.twoFactorToken);
      toast.message(messages.auth.twoFactorCodeRequired);
      return;
    }
    if (!res.token || !res.user) {
      setError(messages.errors.generic);
      return;
    }
    setSession(res.token, res.user);
    keepPickedLanguage();
    keepWorkspaceFor(res.user, fromSso?.kept);
    toast.success(messages.auth.signedIn);
    navigate(afterSignIn ?? (isUsingRealApi() ? '/organizations' : '/dashboard'), { replace: true });
  };

  const answerWithPasskey = async () => {
    if (!twoFactorToken || submitting) return;
    setError(null);
    setSubmitting(true);
    try {
      signedInAnotherWay(await passkeySecondFactor(twoFactorToken));
    } catch (e) {
      setError(e instanceof Error ? e.message : messages.errors.generic);
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
          <Link to="/" className="inline-flex items-center gap-2 text-white/90 transition-colors hover:text-white" aria-label={t('login.homeAria')}>
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-white/20 ring-1 ring-white/30 backdrop-blur">
              <Sparkles className="h-5 w-5" />
            </span>
            <BrandName tagline onColor className="text-lg font-bold tracking-tight" />
          </Link>
        </div>

        <div className="relative max-w-md">
          <h2 className="text-4xl font-bold leading-tight tracking-tight">
            {t('login.brandHeading')}
          </h2>
          <p className="mt-4 text-lg text-white/85">
            {t('login.brandText')}
          </p>
          <ul className="mt-10 space-y-5">
            {HIGHLIGHTS.map((h) => (
              <li key={h.title} className="flex items-start gap-4">
                <span className="mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-white/15 ring-1 ring-white/25 backdrop-blur">
                  <h.icon className="h-5 w-5" />
                </span>
                <div>
                  <p className="font-semibold">{t(h.title)}</p>
                  <p className="text-sm text-white/80">{t(h.body)}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>

        <div className="relative flex items-center gap-3 text-sm text-white/80">
          <ShieldCheck className="h-4 w-4" />
          {t('login.secure')}
        </div>
      </aside>

      {/* ---------- Form panel ---------- */}
      <div className="flex min-h-screen flex-col lg:min-h-0">
        <header className="flex items-center justify-between gap-3 px-[var(--page-padding)] py-[var(--header-block-padding-y)]" role="banner">
          <Link
            to="/"
            className="inline-flex items-center gap-2 rounded-lg text-sm text-slate-500 transition-colors hover:text-slate-900 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
            aria-label={t('login.backHome')}
          >
            <ArrowLeft className="h-4 w-4" aria-hidden /> {t('login.backHome')}
          </Link>
          {/* The language, before anyone signs in: applied at once, kept in this browser, and on the account after sign-in. */}
          <LanguageSwitcher onChosen={setPickedLanguage} />
        </header>

        <main id={MAIN_CONTENT_ID} className="flex flex-1 items-center justify-center px-[var(--page-padding)] pb-10" tabIndex={-1}>
          <div className="w-full max-w-md">
            {/* Mobile logo */}
            <Link to="/" className="mb-6 flex items-center justify-center gap-2 lg:hidden" aria-hidden tabIndex={-1}>
              <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-gradient-to-br from-orange-500 to-orange-600 text-white shadow-lg shadow-orange-500/25">
                <Sparkles className="h-5 w-5" />
              </span>
              <BrandName className="text-lg font-bold tracking-tight text-slate-900" />
            </Link>

            <div className="mb-7 text-center lg:text-left">
              <h1 className="text-2xl font-bold tracking-tight text-slate-900 sm:text-3xl">
                {requires2fa ? t('login.title2fa') : mode === 'register' ? t('login.titleRegister') : t('login.titleLogin')}
              </h1>
              <p className="mt-2 text-slate-500">
                {requires2fa
                  ? t('login.lead2fa')
                  : mode === 'register'
                    ? t('login.leadRegister')
                    : t('login.leadLogin')}
              </p>
            </div>

            <div className="rounded-2xl border border-slate-200/80 bg-white p-6 shadow-[0_2px_4px_rgba(15,23,42,0.04),0_18px_40px_-24px_rgba(15,23,42,0.2)] sm:p-8">
              {!requires2fa ? (
                <form
                  onSubmit={(e) => { e.preventDefault(); handleSubmit(); }}
                  className="space-y-4"
                >
                  {/* Segmented mode toggle */}
                  <div className="grid grid-cols-2 gap-1 rounded-xl bg-slate-100 p-1" role="tablist" aria-label={t('login.modeLabel')}>
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
                        {m === 'login' ? t('login.signIn') : t('login.createAccount')}
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
                    <Field label={t('login.name')} htmlFor="name" icon={<UserIcon className="h-4 w-4" />}>
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

                  <Field label={t('login.email')} htmlFor="email" icon={<Mail className="h-4 w-4" />}>
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
                    label={t('login.password')}
                    htmlFor="password"
                    icon={<Lock className="h-4 w-4" />}
                    action={mode === 'login' ? (
                      <Link to="/forgot-password" className="text-xs font-medium text-orange-700 hover:text-orange-800 focus-visible:underline">
                        {t('login.forgot')}
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
                      aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
                      className="absolute right-3 top-1/2 -translate-y-1/2 rounded-md p-1 text-slate-500 hover:text-slate-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500"
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
                      <><Loader2 className="h-4 w-4 animate-spin" /> {t('login.pleaseWait')}</>
                    ) : (
                      <>{mode === 'register' ? t('login.createAccount') : t('login.signIn')} <ArrowRight className="h-4 w-4" /></>
                    )}
                  </button>

                  <div className="flex items-center gap-3 py-1" role="presentation" aria-hidden>
                    <div className="h-px flex-1 bg-slate-200" />
                    <span className="text-xs font-medium uppercase tracking-wider text-slate-500">{t('login.or')}</span>
                    <div className="h-px flex-1 bg-slate-200" />
                  </div>

                  <AlternativeSignIn
                    onSignedIn={signedInAnotherWay}
                    onError={setError}
                    withPasskey={mode === 'login'}
                    returnTo={afterSignIn}
                    language={pickedLanguage}
                  />

                  <button
                    type="button"
                    onClick={startDemo}
                    className="flex h-12 w-full items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 font-semibold text-emerald-700 transition-colors hover:bg-emerald-100 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-emerald-500 focus-visible:ring-offset-2"
                  >
                    <Play className="h-4 w-4" aria-hidden /> {t('login.demo')}
                  </button>
                  <p className="text-center text-xs text-slate-500">{t('login.demoNote')}</p>
                </form>
              ) : (
                <form onSubmit={(e) => { e.preventDefault(); handleSubmit(); }} className="space-y-5">
                  <div>
                    <label className="mb-2 block text-sm font-medium text-slate-700">{t('login.code')}</label>
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
                    {submitting ? <><Loader2 className="h-4 w-4 animate-spin" /> {t('login.verifying')}</> : t('login.verify')}
                  </button>

                  {passkeyInsteadOfCode && (
                    <button
                      type="button"
                      onClick={() => void answerWithPasskey()}
                      disabled={submitting}
                      className="h-11 w-full rounded-xl border border-slate-200 font-medium text-slate-700 transition-colors hover:bg-slate-50 disabled:opacity-60"
                    >
                      Use a passkey instead
                    </button>
                  )}

                  <button
                    type="button"
                    onClick={() => { setRequires2fa(false); setTwoFactorToken(null); setCode(''); setError(null); }}
                    className="h-11 w-full rounded-xl border border-slate-200 font-medium text-slate-600 transition-colors hover:bg-slate-50"
                  >
                    {t('login.back')}
                  </button>
                </form>
              )}
            </div>

            <nav className="mt-6 flex items-center justify-center gap-3 text-xs text-slate-500" aria-label={t('login.legal')}>
              <Link to="/privacy" className="hover:text-slate-600 focus-visible:underline">{t('login.privacy')}</Link>
              <span aria-hidden>·</span>
              <Link to="/terms" className="hover:text-slate-600 focus-visible:underline">{t('login.terms')}</Link>
              <span aria-hidden>·</span>
              <Link to="/help" className="hover:text-slate-600 focus-visible:underline">{t('login.help')}</Link>
            </nav>
          </div>
        </main>
      </div>

      <AuthErrorDialog
        info={errorModal}
        onClose={() => setErrorModal(null)}
        onRetry={() => { setErrorModal(null); document.getElementById(requires2fa ? 'otp-input' : 'email')?.focus(); }}
        onDemo={() => { setErrorModal(null); startDemo(); }}
      />
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
        <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" aria-hidden>{icon}</span>
        {children}
      </div>
    </div>
  );
}

// ---------- Auth error explanation ----------

type AuthErrorKind = 'credentials' | 'network' | 'config' | 'twofactor' | 'ratelimit' | 'server' | 'generic';

/** Best-effort classification of an auth failure from its message/status text. */
function classifyAuthError(raw: string, was2fa: boolean): AuthErrorKind {
  const m = (raw || '').toLowerCase();
  if (was2fa && /(code|2fa|two-factor|token|expired|invalid)/.test(m)) return 'twofactor';
  if (/(vite_api_url|not set|demo)/.test(m)) return 'config';
  if (/(failed to fetch|networkerror|network error|load failed|err_|connection|timeout|fetch)/.test(m)) return 'network';
  if (/(429|too many|rate limit)/.test(m)) return 'ratelimit';
  if (/(401|403|unauthorized|forbidden|invalid|incorrect|credential|password|no account|not found|email)/.test(m)) return 'credentials';
  if (/(500|502|503|504|server error|internal)/.test(m)) return 'server';
  return 'generic';
}

const AUTH_ERROR_COPY: Record<AuthErrorKind, { icon: React.ElementType; tone: string }> = {
  credentials: { icon: Lock, tone: 'bg-amber-100 text-amber-700' },
  network: { icon: WifiOff, tone: 'bg-rose-100 text-rose-600' },
  config: { icon: Info, tone: 'bg-blue-100 text-blue-600' },
  twofactor: { icon: ShieldAlert, tone: 'bg-violet-100 text-violet-600' },
  ratelimit: { icon: Clock, tone: 'bg-orange-100 text-orange-700' },
  server: { icon: ServerCrash, tone: 'bg-rose-100 text-rose-600' },
  generic: { icon: AlertTriangle, tone: 'bg-slate-100 text-slate-600' },
};

/** Explained, dismissible failure dialog: what happened, why it happens, and the raw detail. */
function AuthErrorDialog({
  info, onClose, onRetry, onDemo,
}: {
  info: { message: string; was2fa: boolean } | null;
  onClose: () => void;
  onRetry: () => void;
  onDemo: () => void;
}) {
  const { t } = useT();
  const kind = info ? classifyAuthError(info.message, info.was2fa) : 'generic';
  const copy = AUTH_ERROR_COPY[kind];
  const Icon = copy.icon;
  const words = (part: 'title' | 'what' | 'how') => t(`login.err.${kind}.${part}` as MessageKey);

  return (
    <Dialog open={!!info} onOpenChange={(o) => { if (!o) onClose(); }}>
      <DialogContent className="sm:max-w-[460px]">
        <DialogHeader>
          <div className="mb-1 flex items-center gap-3">
            <span className={cn('flex h-11 w-11 shrink-0 items-center justify-center rounded-xl', copy.tone)}>
              <Icon className="h-5 w-5" />
            </span>
            <DialogTitle className="text-left text-lg">{words('title')}</DialogTitle>
          </div>
          <DialogDescription className="text-left text-slate-600">{words('what')}</DialogDescription>
        </DialogHeader>

        <div className="rounded-xl border border-slate-200 bg-slate-50/70 p-3">
          <p className="mb-1 text-xs font-semibold uppercase tracking-wider text-slate-500">{t('login.why')}</p>
          <p className="text-sm text-slate-600">{words('how')}</p>
        </div>

        {info?.message && (
          <details className="group rounded-xl border border-slate-200 bg-white">
            <summary className="flex cursor-pointer list-none items-center justify-between px-3 py-2 text-xs font-medium text-slate-500 hover:text-slate-700">
              {t('login.technical')}
              <span className="text-slate-500 transition-transform group-open:rotate-180">⌄</span>
            </summary>
            <pre className="overflow-x-auto whitespace-pre-wrap break-words border-t border-slate-100 px-3 py-2 font-mono text-[11px] leading-relaxed text-slate-500">
              {info.message}
            </pre>
          </details>
        )}

        <DialogFooter className="gap-2 sm:gap-2">
          {kind === 'config' ? (
            <>
              <button type="button" onClick={onClose} className="h-10 rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-600 hover:bg-slate-50">{t('login.close')}</button>
              <button type="button" onClick={onDemo} className="inline-flex h-10 items-center justify-center gap-1.5 rounded-lg bg-emerald-600 px-4 text-sm font-semibold text-white hover:bg-emerald-500">
                <Play className="h-4 w-4" /> {t('login.demo')}
              </button>
            </>
          ) : (
            <>
              {kind === 'credentials' && (
                <Link to="/forgot-password" onClick={onClose} className="inline-flex h-10 items-center justify-center rounded-lg border border-slate-200 px-4 text-sm font-medium text-slate-600 hover:bg-slate-50">
                  {t('login.resetPassword')}
                </Link>
              )}
              <button type="button" onClick={onRetry} className="inline-flex h-10 items-center justify-center gap-1.5 rounded-lg bg-gradient-to-r from-orange-600 to-orange-500 px-4 text-sm font-semibold text-white hover:from-orange-500 hover:to-orange-400">
                <RefreshCw className="h-4 w-4" /> {t('login.tryAgain')}
              </button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

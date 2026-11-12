import { FormEvent, useEffect, useRef, useState } from 'react';
import { Link, useParams, useSearchParams } from 'react-router-dom';
import { getPublicCaptureForm, submitPublicCapture, PublicCaptureError, type MarketingConsentMode } from '@/app/api/leadOps';
import { PublicPage } from '@/app/components/PublicPage';
import { usePageLanguage, useT } from '@/app/i18n';

/**
 * The public form a stranger fills in on the club's website.
 *
 * Everything on this page is seen by someone who is not a user of the CRM, so it
 * says nothing about the CRM: it used to tell visitors "Your details become a lead
 * in Cadence". It also used to show the same "This form is not active" for a
 * network blip as for a dead link, keep the fields editable on a dead link, and
 * ignore the thank-you message the workspace configured.
 *
 * It reads in the visitor's language (one they picked here, the ?lang= on the link, their
 * browser's, else the workspace's), and says which with the form, so the thank-you and the
 * consent record are in the words they read. The workspace's own words stay as written.
 */
type FormState = 'loading' | 'ready' | 'missing' | 'unreachable';

export default function PublicCapture() {
  const { token = '' } = useParams();
  const [search] = useSearchParams();
  const { t, tx, language } = useT();
  const [state, setState] = useState<FormState>('loading');
  // The form's name as the workspace wrote it; until then, or without one, the standard heading.
  const [title, setTitle] = useState<string | null>(null);
  const [clubLanguage, setClubLanguage] = useState<string | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [company, setCompany] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [country, setCountry] = useState('');
  const [website, setWebsite] = useState('');
  // The marketing box: never ticked in advance. Whether it shows, and whether it must be
  // ticked, is the workspace's privacy setting.
  const [consentMode, setConsentMode] = useState<MarketingConsentMode>('off');
  const [consentText, setConsentText] = useState('');
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const thanksRef = useRef<HTMLHeadingElement>(null);
  const loaded = useRef(false);

  usePageLanguage(search.get('lang'));
  usePageLanguage(clubLanguage, 'club');

  // Asked again when the page turns to another language, so the standard checkbox sentence
  // is in it; what was typed stays, and only the first load shows "Loading…".
  useEffect(() => {
    let cancelled = false;
    if (!loaded.current) setState('loading');
    getPublicCaptureForm(token, language)
      .then((form) => {
        if (cancelled) return;
        loaded.current = true;
        setTitle(form.name?.trim() || null);
        setClubLanguage(form.language ?? null);
        const mode = form.marketingConsent ?? 'off';
        setConsentMode(mode === 'optional' || mode === 'required' ? mode : 'off');
        setConsentText(form.marketingConsentText?.trim() || '');
        setState('ready');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // Only a 404 means the link is dead. Anything else is worth retrying.
        loaded.current = false;
        setState(err instanceof PublicCaptureError && err.status === 404 ? 'missing' : 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt, language]);

  // Move focus to the thank-you, so a screen reader announces it and keyboard
  // focus is not left on a button that no longer exists.
  useEffect(() => {
    if (done) thanksRef.current?.focus();
  }, [done]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const message = await submitPublicCapture(token, {
        name, email, phone, company, jobTitle, country, website,
        ...(consentMode === 'off' ? {} : { marketingConsent: consent }),
        language,
      });
      setDone(message ?? t('capture.thanks'));
    } catch (err) {
      const status = err instanceof PublicCaptureError ? err.status : 0;
      setError(
        status === 429 ? t('capture.errorBusy')
          : status === 400 && consentMode === 'required' && !consent ? t('capture.errorConsentRequired')
          : status === 400 ? t('capture.errorInvalid')
          : status === 404 ? t('apply.errorGone')
          : t('apply.errorNetwork'),
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <PublicPage>
        {done ? (
          <div className="text-center">
            <h1 ref={thanksRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              {t('public.thanksTitle')}
            </h1>
            <p className="mt-2 text-sm text-slate-700">{done}</p>
          </div>
        ) : state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t('public.formMissingTitle')}</h1>
            <p className="mt-2 text-sm text-slate-600">{t('public.formMissingText')}</p>
          </>
        ) : state === 'unreachable' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">{t('public.formUnreachableTitle')}</h1>
            <p className="mt-2 text-sm text-slate-600">{t('common.checkConnection')}</p>
            <button
              type="button"
              onClick={() => setAttempt((n) => n + 1)}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              {t('common.tryAgain')}
            </button>
          </>
        ) : (
          <form onSubmit={submit} aria-busy={state === 'loading' || busy}>
            <h1 className="text-xl font-semibold text-slate-900">{state === 'loading' ? t('common.loading') : title ?? t('capture.titleDefault')}</h1>
            <p className="mt-1 text-sm text-slate-500">{t('capture.intro')}</p>
            <fieldset disabled={state !== 'ready' || busy} className="mt-6 space-y-3">
              <Field label={t('capture.name')} value={name} onChange={setName} autoComplete="name" maxLength={256} required />
              <Field label={t('capture.email')} value={email} onChange={setEmail} type="email" autoComplete="email" maxLength={256} required />
              <Field label={t('capture.phone')} value={phone} onChange={setPhone} type="tel" autoComplete="tel" maxLength={64} />
              <Field label={t('capture.company')} value={company} onChange={setCompany} autoComplete="organization" maxLength={200} />
              <Field label={t('capture.jobTitle')} value={jobTitle} onChange={setJobTitle} autoComplete="organization-title" maxLength={200} />
              <Field label={t('capture.country')} value={country} onChange={setCountry} autoComplete="country-name" maxLength={80} />
              {/* Honeypot: hidden from people and from assistive technology. A bot
                  that fills every field fills this one, and nothing is saved. */}
              <div aria-hidden="true" className="absolute left-[-10000px] top-auto h-px w-px overflow-hidden">
                <label>
                  Website
                  <input tabIndex={-1} autoComplete="off" value={website} onChange={(e) => setWebsite(e.target.value)} />
                </label>
              </div>
              {consentMode !== 'off' && (
                <label className="flex items-start gap-2 pt-1 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={consent}
                    required={consentMode === 'required'}
                    onChange={(e) => setConsent(e.target.checked)}
                    className="mt-0.5 h-4 w-4 rounded border-slate-300"
                  />
                  <span>
                    {consentText || t('capture.consentDefault')}
                    {consentMode === 'required' && <span className="text-red-600" aria-hidden="true"> *</span>}
                  </span>
                </label>
              )}
              <p className="text-xs text-slate-500">
                {tx('public.privacyLine', {
                  link: (words) => <Link to={`/privacy?form=${encodeURIComponent(token)}`} className="underline">{words}</Link>,
                })}
              </p>
              <p role="alert" className="min-h-0 text-sm text-red-600">{error}</p>
              <button
                type="submit"
                className="h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
              >
                {busy ? t('common.sending') : t('capture.submit')}
              </button>
            </fieldset>
          </form>
        )}
    </PublicPage>
  );
}

function Field({
  label, value, onChange, type = 'text', required = false, autoComplete, maxLength,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
  autoComplete?: string;
  maxLength?: number;
}) {
  return (
    <label className="block text-sm text-slate-700">
      {label}
      {required && <span className="text-red-600" aria-hidden="true"> *</span>}
      <input
        required={required}
        type={type}
        value={value}
        autoComplete={autoComplete}
        maxLength={maxLength}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200"
      />
    </label>
  );
}

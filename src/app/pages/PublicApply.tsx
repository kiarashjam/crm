import { FormEvent, useEffect, useRef, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import {
  getPublicApplicationForm,
  submitPublicApplication,
  PublicApplicationError,
  type PublicApplicationForm,
} from '@/app/api/applications';

/**
 * The club's membership application, for someone who is not a user of the app: no sign-in,
 * the link is the whole of it. What it sends becomes a Submitted application, linked to the
 * applicant's lead (or a new one), and their answer to news by email is kept as a consent
 * record with the sentence they were shown. A dead link says so; a network failure offers a
 * retry rather than looking like a dead link.
 */
type FormState = 'loading' | 'ready' | 'missing' | 'unreachable';

export default function PublicApply() {
  const { token = '' } = useParams();
  const [state, setState] = useState<FormState>('loading');
  const [form, setForm] = useState<PublicApplicationForm | null>(null);
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [firstName, setFirstName] = useState('');
  const [lastName, setLastName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [tier, setTier] = useState('');
  const [occupation, setOccupation] = useState('');
  const [referredBy, setReferredBy] = useState('');
  const [reason, setReason] = useState('');
  const [consent, setConsent] = useState(false);
  const [website, setWebsite] = useState('');
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const thanksRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let cancelled = false;
    setState('loading');
    getPublicApplicationForm(token)
      .then((loaded) => {
        if (cancelled) return;
        setForm(loaded);
        setState('ready');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // Only a 404 means the link is dead (or the club closed the form).
        setState(err instanceof PublicApplicationError && err.status === 404 ? 'missing' : 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt]);

  useEffect(() => {
    if (done) thanksRef.current?.focus();
  }, [done]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const message = await submitPublicApplication(token, {
        firstName, lastName, email, phone: phone || undefined, requestedTier: tier || undefined,
        occupation: occupation || undefined, referredByName: referredBy || undefined, reasonForJoining: reason || undefined,
        marketingConsent: consent, website,
      });
      setDone(message ?? 'Thank you for applying. The membership committee will be in touch.');
    } catch (err) {
      const status = err instanceof PublicApplicationError ? err.status : 0;
      setError(
        status === 429 ? 'A lot of applications are arriving right now. Please try again in a minute.'
          : status === 400 ? 'Please check your email address, and that each answer is not too long.'
          : status === 404 ? 'This form is no longer available.'
          : 'That could not be sent. Please check your connection and try again.',
      );
    } finally {
      setBusy(false);
    }
  };

  const heading = form ? `Apply to ${form.clubName}` : 'Membership application';

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <div className="mx-auto max-w-lg rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {done ? (
          <div className="text-center">
            <h1 ref={thanksRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">Thank you</h1>
            <p className="mt-2 text-sm text-slate-700">{done}</p>
          </div>
        ) : state === 'missing' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">This form is no longer available</h1>
            <p className="mt-2 text-sm text-slate-600">The link may be out of date. Please contact the club directly.</p>
          </>
        ) : state === 'unreachable' ? (
          <>
            <h1 className="text-xl font-semibold text-slate-900">This form could not be loaded</h1>
            <p className="mt-2 text-sm text-slate-600">Please check your connection.</p>
            <button
              type="button"
              onClick={() => setAttempt((n) => n + 1)}
              className="mt-4 h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
            >
              Try again
            </button>
          </>
        ) : (
          <form onSubmit={submit} aria-busy={state === 'loading' || busy}>
            <h1 className="text-xl font-semibold text-slate-900">{state === 'loading' ? 'Loading…' : heading}</h1>
            {form?.intro && <p className="mt-1 whitespace-pre-line text-sm text-slate-600">{form.intro}</p>}
            <fieldset disabled={state !== 'ready' || busy} className="mt-6 space-y-3">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <Field label="First name" value={firstName} onChange={setFirstName} autoComplete="given-name" maxLength={100} required />
                <Field label="Last name" value={lastName} onChange={setLastName} autoComplete="family-name" maxLength={100} required />
              </div>
              <Field label="Email" value={email} onChange={setEmail} type="email" autoComplete="email" maxLength={256} required />
              <Field label="Phone" value={phone} onChange={setPhone} type="tel" autoComplete="tel" maxLength={64} />
              {form && form.tiers.length > 0 && (
                <label className="block text-sm text-slate-700">
                  Membership
                  <select
                    value={tier}
                    onChange={(e) => setTier(e.target.value)}
                    className="mt-1 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200"
                  >
                    <option value="">Not sure yet</option>
                    {form.tiers.map((t) => (
                      <option key={t.name} value={t.name}>{t.name}</option>
                    ))}
                  </select>
                </label>
              )}
              <Field label="Occupation" value={occupation} onChange={setOccupation} autoComplete="organization-title" maxLength={200} />
              <Field label="Referred by a member" value={referredBy} onChange={setReferredBy} maxLength={200} />
              <label className="block text-sm text-slate-700">
                Why would you like to join?
                <textarea
                  value={reason}
                  maxLength={2000}
                  rows={4}
                  onChange={(e) => setReason(e.target.value)}
                  className="mt-1 w-full rounded-lg border border-slate-200 px-3 py-2 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200"
                />
              </label>
              {form && form.marketingConsent !== 'off' && (
                <label className="flex items-start gap-2 text-sm text-slate-700">
                  <input
                    type="checkbox"
                    checked={consent}
                    required={form.marketingConsent === 'required'}
                    onChange={(e) => setConsent(e.target.checked)}
                    className="mt-1 h-4 w-4 rounded border-slate-300"
                  />
                  <span>
                    {form.consentText}
                    {form.marketingConsent === 'required' && <span className="text-red-600" aria-hidden="true"> *</span>}
                  </span>
                </label>
              )}
              <p className="text-xs text-slate-500">
                How your details are used, and your rights over them:{' '}
                <Link to={`/privacy?form=${encodeURIComponent(token)}`} className="underline">privacy notice</Link>.
              </p>
              {/* Honeypot: hidden from people and from assistive technology. A bot that
                  fills every field fills this one, and nothing is saved. */}
              <div aria-hidden="true" className="absolute left-[-10000px] top-auto h-px w-px overflow-hidden">
                <label>
                  Website
                  <input tabIndex={-1} autoComplete="off" value={website} onChange={(e) => setWebsite(e.target.value)} />
                </label>
              </div>
              <p role="alert" className="min-h-0 text-sm text-red-600">{error}</p>
              <button
                type="submit"
                className="h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 focus-visible:ring-offset-2"
              >
                {busy ? 'Sending…' : 'Send my application'}
              </button>
            </fieldset>
          </form>
        )}
      </div>
    </main>
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

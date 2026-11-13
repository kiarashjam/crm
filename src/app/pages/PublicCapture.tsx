import { FormEvent, useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getPublicCaptureForm, submitPublicCapture, PublicCaptureError } from '@/app/api/leadOps';
import { ESTABLISHMENT_TYPES, leadKindsMode, type LeadKindsMode } from '@/app/lib/leadKinds';

/**
 * The public form a stranger fills in on the club's website.
 *
 * Everything on this page is seen by someone who is not a user of the CRM, so it
 * says nothing about the CRM: it used to tell visitors "Your details become a lead
 * in Cadence". It also used to show the same "This form is not active" for a
 * network blip as for a dead link, keep the fields editable on a dead link, and
 * ignore the thank-you message the workspace configured.
 */
type FormState = 'loading' | 'ready' | 'missing' | 'unreachable';
type Kind = 'person' | 'establishment';

export default function PublicCapture() {
  const { token = '' } = useParams();
  const [state, setState] = useState<FormState>('loading');
  const [title, setTitle] = useState('Tell us about you');
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [company, setCompany] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [country, setCountry] = useState('');
  const [website, setWebsite] = useState('');
  // A workspace whose leads are establishments asks for the place, then who is writing.
  const [mode, setMode] = useState<LeadKindsMode>('person');
  const [kind, setKind] = useState<Kind>('person');
  const [placeName, setPlaceName] = useState('');
  const [placeType, setPlaceType] = useState('');
  const [street, setStreet] = useState('');
  const [postalCode, setPostalCode] = useState('');
  const [city, setCity] = useState('');
  const [businessWebsite, setBusinessWebsite] = useState('');
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  const thanksRef = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    let cancelled = false;
    setState('loading');
    getPublicCaptureForm(token)
      .then((form) => {
        if (cancelled) return;
        if (form.name?.trim()) setTitle(form.name.trim());
        const m = leadKindsMode(form.leadKinds);
        setMode(m);
        setKind(m === 'establishment' ? 'establishment' : 'person');
        setState('ready');
      })
      .catch((err: unknown) => {
        if (cancelled) return;
        // Only a 404 means the link is dead. Anything else is worth retrying.
        setState(err instanceof PublicCaptureError && err.status === 404 ? 'missing' : 'unreachable');
      });
    return () => {
      cancelled = true;
    };
  }, [token, attempt]);

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
      const place = kind === 'establishment';
      const message = await submitPublicCapture(token, {
        name, email, phone, jobTitle, country, website,
        // A business's own name is the establishment's; "Company" is asked of a person only.
        ...(place ? {} : { company }),
        // A people-only form sends what it always did.
        ...(mode === 'person' ? {} : { kind }),
        ...(place
          ? { establishmentName: placeName, establishmentType: placeType || undefined, addressLine: street, postalCode, city, businessWebsite }
          : {}),
      });
      setDone(message ?? "Thanks. We'll be in touch.");
    } catch (err) {
      const status = err instanceof PublicCaptureError ? err.status : 0;
      setError(
        status === 429 ? 'A lot of messages are arriving right now. Please try again in a minute.'
          : status === 400 ? (kind === 'establishment'
            ? 'Please check the establishment’s name, the email address, and that each field is not too long.'
            : 'Please check the email address and that each field is not too long.')
          : status === 404 ? 'This form is no longer available.'
          : 'That could not be sent. Please check your connection and try again.',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <div className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        {done ? (
          <div className="text-center">
            <h1 ref={thanksRef} tabIndex={-1} className="text-xl font-semibold text-slate-900 focus:outline-none">
              Thank you
            </h1>
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
            <h1 className="text-xl font-semibold text-slate-900">{state === 'loading' ? 'Loading…' : title}</h1>
            <p className="mt-1 text-sm text-slate-500">
              {kind === 'establishment'
                ? 'Tell us about your establishment and we will get back to you.'
                : 'Leave your details and we will get back to you.'}
            </p>
            <fieldset disabled={state !== 'ready' || busy} className="mt-6 space-y-3">
              {mode === 'both' && (
                <div role="radiogroup" aria-label="You are writing" className="grid grid-cols-2 gap-2">
                  {([['person', 'For myself'], ['establishment', 'For a business']] as const).map(([value, label]) => (
                    <button
                      key={value}
                      type="button"
                      role="radio"
                      aria-checked={kind === value}
                      onClick={() => setKind(value)}
                      className={`h-10 rounded-xl border text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-500 ${
                        kind === value ? 'border-slate-900 bg-slate-900 text-white' : 'border-slate-200 bg-white text-slate-700 hover:border-slate-300'
                      }`}
                    >
                      {label}
                    </button>
                  ))}
                </div>
              )}
              {kind === 'establishment' ? (
                <>
                  <Field label="Establishment name" value={placeName} onChange={setPlaceName} autoComplete="organization" maxLength={256} required />
                  <label className="block text-sm text-slate-700">
                    Type
                    <select
                      value={placeType}
                      onChange={(e) => setPlaceType(e.target.value)}
                      className="mt-1 h-10 w-full rounded-lg border border-slate-200 bg-white px-3 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200"
                    >
                      <option value="">Choose…</option>
                      {ESTABLISHMENT_TYPES.map((t) => <option key={t.value} value={t.value}>{t.label}</option>)}
                    </select>
                  </label>
                  <Field label="Street and number" value={street} onChange={setStreet} autoComplete="street-address" maxLength={256} />
                  <div className="grid grid-cols-[7rem_minmax(0,1fr)] gap-3">
                    <Field label="Postal code" value={postalCode} onChange={setPostalCode} autoComplete="postal-code" maxLength={20} />
                    <Field label="City" value={city} onChange={setCity} autoComplete="address-level2" maxLength={120} />
                  </div>
                  <Field label="Country" value={country} onChange={setCountry} autoComplete="country-name" maxLength={80} />
                  <Field label="Establishment website" value={businessWebsite} onChange={setBusinessWebsite} autoComplete="url" maxLength={256} />
                  <p className="pt-2 text-sm font-medium text-slate-900">Your details</p>
                  <Field label="Your name" value={name} onChange={setName} autoComplete="name" maxLength={256} required />
                  <Field label="Your role" value={jobTitle} onChange={setJobTitle} autoComplete="organization-title" maxLength={200} />
                  <Field label="Email" value={email} onChange={setEmail} type="email" autoComplete="email" maxLength={256} required />
                  <Field label="Phone" value={phone} onChange={setPhone} type="tel" autoComplete="tel" maxLength={64} />
                </>
              ) : (
                <>
              <Field label="Name" value={name} onChange={setName} autoComplete="name" maxLength={256} required />
              <Field label="Email" value={email} onChange={setEmail} type="email" autoComplete="email" maxLength={256} required />
              <Field label="Phone" value={phone} onChange={setPhone} type="tel" autoComplete="tel" maxLength={64} />
              <Field label="Company" value={company} onChange={setCompany} autoComplete="organization" maxLength={200} />
              <Field label="Job title" value={jobTitle} onChange={setJobTitle} autoComplete="organization-title" maxLength={200} />
              <Field label="Country" value={country} onChange={setCountry} autoComplete="country-name" maxLength={80} />
                </>
              )}
              {/* Honeypot: hidden from people and from assistive technology. A bot
                  that fills every field fills this one, and nothing is saved. */}
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
                {busy ? 'Sending…' : 'Send'}
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

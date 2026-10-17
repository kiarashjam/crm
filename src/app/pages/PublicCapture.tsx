import { FormEvent, useEffect, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getPublicCaptureForm, submitPublicCapture } from '@/app/api/leadOps';

export default function PublicCapture() {
  const { token = '' } = useParams();
  const [title, setTitle] = useState('Tell us about you');
  const [done, setDone] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [missing, setMissing] = useState(false);
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [jobTitle, setJobTitle] = useState('');
  const [country, setCountry] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    getPublicCaptureForm(token)
      .then((form) => setTitle(form.name))
      .catch(() => setMissing(true));
  }, [token]);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await submitPublicCapture(token, { name, email, phone, jobTitle, country });
      setDone('Thanks. Someone will reply shortly.');
    } catch {
      setError('That form could not be saved. Check the email and try again.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <main className="min-h-screen bg-slate-50 px-4 py-16">
      <form onSubmit={submit} className="mx-auto max-w-md rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h1 className="text-xl font-semibold text-slate-900">{title}</h1>
        <p className="mt-1 text-sm text-slate-500">Your details become a lead in Cadence.</p>
        {missing && <p className="mt-4 text-sm text-red-600">This form is not active.</p>}
        {done ? (
          <p className="mt-6 text-sm text-slate-800">{done}</p>
        ) : (
          <div className="mt-6 space-y-3">
            <Field label="Name" value={name} onChange={setName} required />
            <Field label="Email" value={email} onChange={setEmail} type="email" required />
            <Field label="Phone" value={phone} onChange={setPhone} />
            <Field label="Job title" value={jobTitle} onChange={setJobTitle} />
            <Field label="Country" value={country} onChange={setCountry} />
            {error && <p className="text-sm text-red-600">{error}</p>}
            <button
              type="submit"
              disabled={busy || missing}
              className="h-10 w-full rounded-xl bg-slate-900 text-sm font-medium text-white disabled:opacity-60"
            >
              {busy ? 'Sending…' : 'Send'}
            </button>
          </div>
        )}
      </form>
    </main>
  );
}

function Field({
  label, value, onChange, type = 'text', required = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  type?: string;
  required?: boolean;
}) {
  return (
    <label className="block text-sm text-slate-700">
      {label}
      <input
        required={required}
        type={type}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        className="mt-1 h-10 w-full rounded-lg border border-slate-200 px-3"
      />
    </label>
  );
}

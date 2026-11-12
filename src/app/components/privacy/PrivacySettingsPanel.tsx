// The workspace's privacy settings: who answers for its data (printed on the privacy notice its
// forms link to), whether its public forms ask for marketing consent and in what words, and
// how long it keeps personal data (every period off until set). Saved with the version read,
// so two people changing them at once do not overwrite each other.

import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import { LoadFailed } from '@/app/components/LoadFailed';
import { ApiError } from '@/app/api/apiClient';
import { savePrivacySettings, type MarketingConsentMode, type PrivacySettings } from '@/app/api/privacy';
import { usePrivacySettings } from '@/app/hooks/queries';

interface Form {
  controllerName: string;
  controllerAddress: string;
  controllerEmail: string;
  dpoContact: string;
  marketingConsent: MarketingConsentMode;
  marketingConsentText: string;
  leadRetentionMonths: string;
  memberRetentionYears: string;
  mailRetentionMonths: string;
  detailRetentionMonths: string;
}

const text = (v?: string | null) => v ?? '';
const count = (v?: number | null) => (v == null ? '' : String(v));

const formOf = (s: PrivacySettings): Form => ({
  controllerName: text(s.controllerName),
  controllerAddress: text(s.controllerAddress),
  controllerEmail: text(s.controllerEmail),
  dpoContact: text(s.dpoContact),
  marketingConsent: s.marketingConsent,
  marketingConsentText: s.marketingConsentText,
  leadRetentionMonths: count(s.leadRetentionMonths),
  memberRetentionYears: count(s.memberRetentionYears),
  mailRetentionMonths: count(s.mailRetentionMonths),
  detailRetentionMonths: count(s.detailRetentionMonths),
});

/** A period as the server takes it: empty is off. */
function periodOf(value: string): number | null {
  const n = Number.parseInt(value.trim(), 10);
  return Number.isFinite(n) ? n : null;
}

const CONSENT: { mode: MarketingConsentMode; label: string; hint: string }[] = [
  { mode: 'optional', label: 'Optional', hint: 'The box shows, unticked; the form can be sent either way. The usual choice.' },
  {
    mode: 'required',
    label: 'Required',
    hint: 'The form cannot be sent without ticking it. Only for a form whose purpose is the newsletter itself: consent made a condition of something else (an enquiry, an application) is not freely given (GDPR art. 7(4)).',
  },
  { mode: 'off', label: 'Off', hint: 'No box, and no consent is recorded. Nobody who fills in a form is added to marketing.' },
];

const PERIODS: { key: keyof Form; label: string; unit: string; max: number; hint: string }[] = [
  { key: 'leadRetentionMonths', label: 'Delete leads nobody worked on after', unit: 'months', max: 120, hint: 'Never-converted leads with nothing done on them for this long.' },
  { key: 'memberRetentionYears', label: 'Anonymise members who left after', unit: 'years', max: 30, hint: 'Resigned, lapsed or cancelled, nothing on their account for this long; their money stays in the books under a pseudonym.' },
  { key: 'mailRetentionMonths', label: 'Remove the text of sent emails after', unit: 'months', max: 120, hint: 'Who, when and whether it arrived stay.' },
  { key: 'detailRetentionMonths', label: 'Remove names from the audit trail after', unit: 'months', max: 120, hint: 'And webhook payloads, raw till tickets, reply subjects and notifications.' },
];

export function PrivacySettingsPanel() {
  const query = usePrivacySettings();
  const settings = query.data;
  const [form, setForm] = useState<Form | null>(null);
  const [saving, setSaving] = useState(false);

  // The form starts from what the server keeps, and again after each save it answers.
  useEffect(() => {
    if (settings) setForm(formOf(settings));
  }, [settings]);

  if (query.isError && !settings) {
    return <LoadFailed what="The privacy settings" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />;
  }
  if (!settings || !form) {
    return (
      <p className="flex items-center gap-2 text-sm text-slate-600" role="status">
        <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" /> Loading the privacy settings…
      </p>
    );
  }

  const set = (patch: Partial<Form>) => setForm({ ...form, ...patch });
  const textChanged = form.marketingConsentText.trim() !== settings.marketingConsentText.trim();

  const save = async () => {
    setSaving(true);
    try {
      await savePrivacySettings({
        controllerName: form.controllerName.trim() || null,
        controllerAddress: form.controllerAddress.trim() || null,
        controllerEmail: form.controllerEmail.trim() || null,
        dpoContact: form.dpoContact.trim() || null,
        marketingConsent: form.marketingConsent,
        // The standard words stay the standard words (and follow when they improve) unless changed here.
        marketingConsentText: settings.marketingConsentTextIsDefault && !textChanged ? null : form.marketingConsentText.trim() || null,
        leadRetentionMonths: periodOf(form.leadRetentionMonths),
        memberRetentionYears: periodOf(form.memberRetentionYears),
        mailRetentionMonths: periodOf(form.mailRetentionMonths),
        detailRetentionMonths: periodOf(form.detailRetentionMonths),
      }, settings.version);
      toast.success('Privacy settings saved');
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'Could not save the privacy settings');
      if (err instanceof ApiError && err.status === 409) void query.refetch();
    } finally {
      setSaving(false);
    }
  };

  return (
    <form
      className="max-w-3xl space-y-5"
      onSubmit={(e) => {
        e.preventDefault();
        void save();
      }}
    >
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="font-semibold text-slate-900">Who answers for the data</h3>
        <p className="mb-4 text-sm text-slate-600">
          The club or company responsible for what it records (the controller), as the privacy notice its forms link to names it.
          Empty, the notice uses the workspace&apos;s name and says the details are missing.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          <div className="space-y-1">
            <Label htmlFor="pv-name">Name</Label>
            <Input id="pv-name" value={form.controllerName} maxLength={200} onChange={(e) => set({ controllerName: e.target.value })} />
          </div>
          <div className="space-y-1">
            <Label htmlFor="pv-email">Email for privacy requests</Label>
            <Input id="pv-email" type="email" value={form.controllerEmail} maxLength={256} onChange={(e) => set({ controllerEmail: e.target.value })} />
          </div>
          <div className="space-y-1 sm:col-span-2">
            <Label htmlFor="pv-address">Postal address</Label>
            <Textarea id="pv-address" rows={3} value={form.controllerAddress} maxLength={500} onChange={(e) => set({ controllerAddress: e.target.value })} />
          </div>
          <div className="space-y-1 sm:col-span-2">
            <Label htmlFor="pv-dpo">Data protection contact (optional)</Label>
            <Input id="pv-dpo" value={form.dpoContact} maxLength={300} onChange={(e) => set({ dpoContact: e.target.value })} placeholder="A data protection officer or advisor, or an EU representative" />
          </div>
        </div>
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <fieldset>
          <legend className="font-semibold text-slate-900">Marketing consent on the public forms</legend>
          <p className="mb-3 text-sm text-slate-600">
            The enquiry form and the membership application. Each answer is recorded with the form and the time it was given; the
            box is never ticked in advance.
          </p>
          <div className="space-y-2">
            {CONSENT.map((c) => (
              <label key={c.mode} className="flex items-start gap-2 text-sm">
                <input
                  type="radio"
                  name="pv-consent"
                  className="mt-1 h-4 w-4"
                  checked={form.marketingConsent === c.mode}
                  onChange={() => set({ marketingConsent: c.mode })}
                />
                <span>
                  <span className="font-medium text-slate-900">{c.label}</span>
                  <span className="block text-slate-600">{c.hint}</span>
                </span>
              </label>
            ))}
          </div>
        </fieldset>
        {form.marketingConsent !== 'off' && (
          <div className="mt-4 space-y-1">
            <Label htmlFor="pv-consent-text">The box&apos;s words on the enquiry form</Label>
            <Textarea id="pv-consent-text" rows={2} maxLength={500} value={form.marketingConsentText} onChange={(e) => set({ marketingConsentText: e.target.value })} />
            <p className="text-xs text-slate-500">
              Say what they will get and that they can change their mind; the privacy notice prints them too. The membership
              application shows the words set on its own form.
            </p>
          </div>
        )}
      </section>

      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="font-semibold text-slate-900">How long personal data is kept</h3>
        <p className="mb-4 text-sm text-slate-600">
          Every period is off until set, and kept data stays until someone deletes it. Charges, payments and VAT stay ten years
          whatever is set here (Swiss Code of Obligations, art. 958f), under a pseudonym once the person is gone. The Retention tab
          shows what a period would remove before it runs.
        </p>
        <div className="grid gap-4 sm:grid-cols-2">
          {PERIODS.map((p) => (
            <div key={p.key} className="space-y-1">
              <Label htmlFor={`pv-${p.key}`}>{p.label}</Label>
              <div className="flex items-center gap-2">
                <Input
                  id={`pv-${p.key}`}
                  type="number"
                  inputMode="numeric"
                  min={1}
                  max={p.max}
                  value={form[p.key] as string}
                  placeholder="Off"
                  onChange={(e) => set({ [p.key]: e.target.value } as Partial<Form>)}
                  className="w-28"
                />
                <span className="text-sm text-slate-600">{p.unit}</span>
              </div>
              <p className="text-xs text-slate-500">{p.hint}</p>
            </div>
          ))}
        </div>
      </section>

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={saving}>
          {saving && <Loader2 className="h-4 w-4 motion-safe:animate-spin" aria-hidden="true" />}
          Save
        </Button>
        {settings.updatedAtUtc && (
          <span className="text-xs text-slate-500">
            Last changed {new Date(settings.updatedAtUtc).toLocaleString()}
            {settings.updatedByName ? ` by ${settings.updatedByName}` : ''}
          </span>
        )}
      </div>
    </form>
  );
}

// What the workspace works in: the currency new deals are priced in, how amounts are
// written, and the time zone its days and months begin in for reports, forecasts and
// the day-by-day lists.
//
// These were missing altogether, so a Swiss club's deals without a currency showed as
// dollars and its months began at UTC midnight. They belong to the workspace, not to
// each person, so the owner or a manager sets them here and everyone sees the same.

import { useEffect, useState } from 'react';
import { Globe2 } from 'lucide-react';
import { toast } from 'sonner';
import { useOrg } from '@/app/contexts/OrgContext';
import { isOrgAdmin, updateOrganizationSettings } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { currencyOptions, formatMoney, regionalOf, type Regional } from '@/app/lib/money';

/**
 * The number formats offered, by the locale that writes them: the club's own first. A
 * workspace in another keeps it. Month and day names stay in each viewer's language,
 * as the rest of the app is in English.
 */
const LOCALES: { code: string; name: string }[] = [
  { code: 'de-CH', name: 'German (Switzerland)' },
  { code: 'fr-CH', name: 'French (Switzerland)' },
  { code: 'it-CH', name: 'Italian (Switzerland)' },
  { code: 'en-CH', name: 'English (Switzerland)' },
  { code: 'de-DE', name: 'German (Germany)' },
  { code: 'de-AT', name: 'German (Austria)' },
  { code: 'fr-FR', name: 'French (France)' },
  { code: 'it-IT', name: 'Italian (Italy)' },
  { code: 'en-GB', name: 'English (United Kingdom)' },
  { code: 'en-US', name: 'English (United States)' },
];

/** Every IANA zone this browser knows, with the workspace's own first if it is not among them. */
function timeZones(current: string): string[] {
  let zones: string[] = [];
  try {
    const intl = Intl as unknown as { supportedValuesOf?: (key: string) => string[] };
    zones = intl.supportedValuesOf?.('timeZone') ?? [];
  } catch {
    zones = [];
  }
  if (zones.length === 0) zones = ['Europe/Zurich', 'Europe/Berlin', 'Europe/Paris', 'Europe/London', 'America/New_York', 'UTC'];
  return zones.includes(current) ? zones : [current, ...zones];
}

function preview(r: Regional): { money: string; date: string } {
  let date: string;
  try {
    date = new Date().toLocaleString(undefined, { timeZone: r.timeZone, dateStyle: 'full', timeStyle: 'short' });
  } catch {
    date = '';
  }
  return { money: formatMoney(85500.5, r.currency, { locale: r.locale }), date };
}

/** 85500.5 as a locale writes it, to tell the formats apart in the list. */
function sample(locale: string): string {
  try {
    return new Intl.NumberFormat(locale, { minimumFractionDigits: 2 }).format(85500.5);
  } catch {
    return '';
  }
}

const field = 'w-full h-11 px-3 border border-slate-300 rounded-xl bg-white text-slate-900 focus:ring-2 focus:ring-orange-500/20 focus:border-orange-500 outline-none disabled:bg-slate-50 disabled:text-slate-600';

export function RegionalSettingsCard() {
  const { currentOrg, refreshOrgs } = useOrg();
  const saved = regionalOf(currentOrg);
  const [form, setForm] = useState<Regional>(saved);
  const [saving, setSaving] = useState(false);
  // Show what the server kept after a save, and the other workspace's after a switch.
  // Keyed on the values, so reading the list again for another reason keeps an edit.
  const orgId = currentOrg?.id;
  useEffect(() => {
    setForm({ currency: saved.currency, locale: saved.locale, timeZone: saved.timeZone });
  }, [orgId, saved.currency, saved.locale, saved.timeZone]);

  // Demo workspaces have no server to keep them; they work in the defaults.
  if (!currentOrg || servesDemoData()) return null;
  const canEdit = isOrgAdmin(currentOrg);
  const changed = form.currency !== saved.currency || form.locale !== saved.locale || form.timeZone !== saved.timeZone;
  const shown = preview(form);
  const locales = LOCALES.some((l) => l.code === form.locale) ? LOCALES : [{ code: form.locale, name: form.locale }, ...LOCALES];

  const save = async () => {
    setSaving(true);
    try {
      await updateOrganizationSettings(currentOrg.id, form);
      await refreshOrgs();
      toast.success('Workspace currency, number format and time zone saved.');
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The settings were not saved.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="regional-settings" className="pt-6 border-t border-slate-200">
      <h3 id="regional-settings" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <Globe2 className="w-4 h-4" aria-hidden />
        Currency, number format and time zone
      </h3>
      <p className="text-sm text-slate-600 mb-4">
        New deals are priced in this currency unless someone picks another. Deals keep the currency they were
        priced in: changing it converts nothing. Amounts are written in the number format, and reports,
        forecasts, deal close dates and the activity lists count days and months in the time zone. Club pages
        keep the club currency set in Hospitality settings.
      </p>
      <div className="grid gap-4 sm:grid-cols-3">
        <label className="block text-sm font-medium text-slate-700">
          Currency
          <select
            className={`${field} mt-1.5`}
            value={form.currency}
            disabled={!canEdit || saving}
            onChange={(e) => setForm((f) => ({ ...f, currency: e.target.value }))}
          >
            {currencyOptions(form.currency).map((c) => (
              <option key={c.code} value={c.code}>{c.name === c.code ? c.code : `${c.code} · ${c.name}`}</option>
            ))}
          </select>
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Number format
          <select
            className={`${field} mt-1.5`}
            value={form.locale}
            disabled={!canEdit || saving}
            onChange={(e) => setForm((f) => ({ ...f, locale: e.target.value }))}
          >
            {locales.map((l) => <option key={l.code} value={l.code}>{l.name} · {sample(l.code)}</option>)}
          </select>
        </label>
        <label className="block text-sm font-medium text-slate-700">
          Time zone
          <select
            className={`${field} mt-1.5`}
            value={form.timeZone}
            disabled={!canEdit || saving}
            onChange={(e) => setForm((f) => ({ ...f, timeZone: e.target.value }))}
          >
            {timeZones(form.timeZone).map((z) => <option key={z} value={z}>{z.replace(/_/g, ' ')}</option>)}
          </select>
        </label>
      </div>
      <p className="mt-3 text-xs text-slate-500" aria-live="polite">
        Amounts look like <span className="font-medium text-slate-700">{shown.money}</span>
        {shown.date && <>; it is now <span className="font-medium text-slate-700">{shown.date}</span> there</>}.
      </p>
      {canEdit ? (
        <button
          type="button"
          onClick={save}
          disabled={!changed || saving}
          className="mt-4 h-10 px-5 bg-orange-600 hover:bg-orange-700 disabled:opacity-50 text-white font-medium rounded-xl transition-colors"
        >
          {saving ? 'Saving…' : 'Save'}
        </button>
      ) : (
        <p className="mt-4 text-xs text-slate-500">Only the owner or a manager can change these.</p>
      )}
    </section>
  );
}

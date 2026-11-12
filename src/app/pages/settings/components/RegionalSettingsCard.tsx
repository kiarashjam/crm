// What the workspace works in: the currency new deals are priced in, how amounts and dates
// are written, the time zone its days and months begin in for reports, forecasts and the
// day-by-day lists, and how its CSV exports split their columns.
//
// These were missing altogether, so a Swiss club's deals without a currency showed as
// dollars and its months began at UTC midnight. They belong to the workspace, not to
// each person, so whoever manages its settings sets them here and everyone sees the same.

import { useEffect, useState, type ReactNode } from 'react';
import { Globe2 } from 'lucide-react';
import { toast } from 'sonner';
import { useOrg } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION, updateOrganizationSettings } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { currencyOptions, formatMoney, regionalOf, type Regional } from '@/app/lib/money';
import { dateLocaleFor, useT, type MessageKey } from '@/app/i18n';

/**
 * The number and date formats offered, by the locale that writes them: the club's own first.
 * A workspace in another keeps it. Month and day names are in each person's own language;
 * the order of a date and the way numbers are grouped are the format's.
 */
const LOCALES = ['de-CH', 'fr-CH', 'it-CH', 'en-CH', 'de-DE', 'de-AT', 'fr-FR', 'it-IT', 'en-GB', 'en-US'] as const;

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

function preview(r: Regional, dateLocale: string): { money: string; date: string; now: string } {
  let date: string;
  let now: string;
  try {
    date = new Date().toLocaleDateString(dateLocale, { timeZone: r.timeZone, day: '2-digit', month: '2-digit', year: 'numeric' });
    now = new Date().toLocaleString(dateLocale, { timeZone: r.timeZone, dateStyle: 'full', timeStyle: 'short' });
  } catch {
    date = '';
    now = '';
  }
  return { money: formatMoney(85500.5, r.currency, { locale: r.locale }), date, now };
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

interface Form extends Regional {
  csvSemicolon: boolean;
}

const bold = (words: ReactNode) => <span className="font-medium text-slate-700">{words}</span>;

export function RegionalSettingsCard() {
  const { currentOrg, refreshOrgs } = useOrg();
  const { t, tx, language } = useT();
  const regional = regionalOf(currentOrg);
  const saved: Form = { ...regional, csvSemicolon: currentOrg?.csvSemicolon === true };
  const [form, setForm] = useState<Form>(saved);
  const [saving, setSaving] = useState(false);
  // Show what the server kept after a save, and the other workspace's after a switch.
  // Keyed on the values, so reading the list again for another reason keeps an edit.
  const orgId = currentOrg?.id;
  useEffect(() => {
    setForm({ currency: saved.currency, locale: saved.locale, timeZone: saved.timeZone, csvSemicolon: saved.csvSemicolon });
  }, [orgId, saved.currency, saved.locale, saved.timeZone, saved.csvSemicolon]);

  // Demo workspaces have no server to keep them; they work in the defaults.
  if (!currentOrg || servesDemoData()) return null;
  const canEdit = Boolean(currentOrg) && orgCan(currentOrg, PERMISSION.ManageSettings);
  const changed = form.currency !== saved.currency || form.locale !== saved.locale || form.timeZone !== saved.timeZone
    || form.csvSemicolon !== saved.csvSemicolon;
  const shown = preview(form, dateLocaleFor(language, form.locale));
  const locales: string[] = (LOCALES as readonly string[]).includes(form.locale) ? [...LOCALES] : [form.locale, ...LOCALES];
  const localeName = (code: string) => ((LOCALES as readonly string[]).includes(code) ? t(`regional.locale.${code}` as MessageKey) : code);

  const save = async () => {
    // Only what changed. The server refuses a whole save over any one field it cannot
    // read, and a Windows host without ICU cannot read the IANA zone at all, so sending
    // the unchanged Europe/Zurich with a new currency kept the currency from saving.
    const changes: Partial<Form> = {};
    if (form.currency !== saved.currency) changes.currency = form.currency;
    if (form.locale !== saved.locale) changes.locale = form.locale;
    if (form.timeZone !== saved.timeZone) changes.timeZone = form.timeZone;
    if (form.csvSemicolon !== saved.csvSemicolon) changes.csvSemicolon = form.csvSemicolon;
    setSaving(true);
    try {
      await updateOrganizationSettings(currentOrg.id, changes);
      await refreshOrgs();
      toast.success(t('regional.saved'));
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : t('regional.notSaved'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <section aria-labelledby="regional-settings" className="pt-6 border-t border-slate-200">
      <h3 id="regional-settings" className="font-semibold text-slate-900 mb-1 flex items-center gap-2">
        <Globe2 className="w-4 h-4" aria-hidden />
        {t('regional.title')}
      </h3>
      <p className="text-sm text-slate-600 mb-4">{t('regional.intro')}</p>
      <div className="grid gap-4 sm:grid-cols-3">
        <label className="block text-sm font-medium text-slate-700">
          {t('regional.currency')}
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
          {t('regional.locale')}
          <select
            className={`${field} mt-1.5`}
            value={form.locale}
            disabled={!canEdit || saving}
            onChange={(e) => setForm((f) => ({ ...f, locale: e.target.value }))}
          >
            {locales.map((code) => <option key={code} value={code}>{localeName(code)} · {sample(code)}</option>)}
          </select>
        </label>
        <label className="block text-sm font-medium text-slate-700">
          {t('regional.timeZone')}
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
        {tx('regional.preview', { money: shown.money, date: shown.date, b: bold })}
        {shown.now && <> {tx('regional.previewNow', { now: shown.now, b: bold })}</>}
      </p>
      <label className="mt-4 block text-sm font-medium text-slate-700 sm:max-w-xl">
        {t('regional.csv')}
        <select
          className={`${field} mt-1.5`}
          value={form.csvSemicolon ? 'semicolon' : 'comma'}
          disabled={!canEdit || saving}
          onChange={(e) => setForm((f) => ({ ...f, csvSemicolon: e.target.value === 'semicolon' }))}
        >
          <option value="comma">{t('regional.csvComma')}</option>
          <option value="semicolon">{t('regional.csvSemicolon')}</option>
        </select>
      </label>
      <p className="mt-1.5 text-xs text-slate-500 sm:max-w-xl">{t('regional.csvHint')}</p>
      {canEdit ? (
        <button
          type="button"
          onClick={save}
          disabled={!changed || saving}
          className="mt-4 h-10 px-5 bg-orange-600 hover:bg-orange-700 disabled:opacity-50 text-white font-medium rounded-xl transition-colors"
        >
          {saving ? t('common.saving') : t('common.save')}
        </button>
      ) : (
        <p className="mt-4 text-xs text-slate-500">{t('regional.needsManage')}</p>
      )}
    </section>
  );
}

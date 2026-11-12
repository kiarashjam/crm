// The language picker: on the sign-in page, on the public pages and in Settings.
//
// Each language is named in its own words, so someone who cannot read the page as it
// is can still find theirs.

import { Globe } from 'lucide-react';
import { useId } from 'react';
import { useT } from './I18nProvider';
import { LANGUAGES, LANGUAGE_NAMES, type Language } from './languages';

interface LanguageSwitcherProps {
  /** Offer "Automatic" (follow the workspace and the browser), as Settings does. */
  withAutomatic?: boolean;
  /** Called with what was picked, after it is applied (the sign-in page keeps it for the account). */
  onChosen?: (language: Language | null) => void;
  className?: string;
  /** Show the label beside the picker rather than for screen readers only. */
  showLabel?: boolean;
}

export function LanguageSwitcher({ withAutomatic = false, onChosen, className, showLabel = false }: LanguageSwitcherProps) {
  const { t, language, source, choose } = useT();
  const id = useId();
  const value = withAutomatic && source !== 'chosen' ? '' : language;
  return (
    <div className={className ?? 'inline-flex items-center gap-1.5 text-sm text-slate-600'}>
      <Globe className="h-4 w-4 shrink-0" aria-hidden />
      <label htmlFor={id} className={showLabel ? 'font-medium text-slate-700' : 'sr-only'}>
        {t('language.label')}
      </label>
      <select
        id={id}
        value={value}
        onChange={(e) => {
          const picked = (e.target.value || null) as Language | null;
          void choose(picked).then(() => onChosen?.(picked));
        }}
        className="h-9 rounded-lg border border-slate-200 bg-white px-2 text-sm text-slate-800 focus:border-slate-400 focus:outline-none focus:ring-2 focus:ring-slate-200"
      >
        {withAutomatic && <option value="">{t('language.automatic')}</option>}
        {LANGUAGES.map((code) => (
          <option key={code} value={code} lang={code}>{LANGUAGE_NAMES[code]}</option>
        ))}
      </select>
    </div>
  );
}

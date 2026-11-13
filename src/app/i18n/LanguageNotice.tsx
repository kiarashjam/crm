// Said once to someone signed in whose app is now in their workspace's language (or their
// browser's) without their having chosen it: everyone used the app in English before it had
// languages, so nobody is left wondering where the English went. Any choice, closing the
// notice included, is kept on their account and the notice does not come back.

import { X } from 'lucide-react';
import { useT } from './I18nProvider';
import { LANGUAGES, LANGUAGE_NAMES } from './languages';

export function LanguageNotice() {
  const { t, language, suggestChoice, choose } = useT();
  if (!suggestChoice) return null;
  return (
    <div role="status" className="flex flex-wrap items-center gap-x-3 gap-y-2 border-b border-sky-200 bg-sky-50 px-4 py-2 text-sm text-sky-900">
      <span>{t('language.notice')}</span>
      <span className="flex flex-wrap gap-1.5">
        {LANGUAGES.map((code) => (
          <button
            key={code}
            type="button"
            lang={code}
            aria-pressed={code === language}
            onClick={() => void choose(code)}
            className={
              code === language
                ? 'rounded-md bg-sky-900 px-2.5 py-1 text-xs font-medium text-white'
                : 'rounded-md border border-sky-300 bg-white px-2.5 py-1 text-xs font-medium text-sky-900 hover:bg-sky-100'
            }
          >
            {LANGUAGE_NAMES[code]}
          </button>
        ))}
      </span>
      <button
        type="button"
        onClick={() => void choose(language)}
        aria-label={t('language.noticeDismiss')}
        className="ml-auto rounded-md p-1 text-sky-700 hover:bg-sky-100"
      >
        <X className="h-4 w-4" aria-hidden />
      </button>
    </div>
  );
}

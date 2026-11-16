// The app's languages: an in-house layer, small enough to read in one sitting.
//
//   const { t } = useT();
//   t('apply.heading', { club: 'Pavillon 46' })        // "Apply to Pavillon 46"
//   t('common.exportedRows', { count: 3 })             // "Exported 3 rows"
//
// Catalogues live in ./locales/{en,de,fr}, one file per area; English is the source and
// catalogues.test.ts fails when German or French misses a key, a placeholder or a plural.

export { I18nProvider, useT, usePageLanguage, translate, currentDateLocale, type I18nValue, type PageLanguageKind } from './I18nProvider';
export { useFormat, makeFormats, type Formats } from './useFormat';
export { LANGUAGES, LANGUAGE_NAMES, DEFAULT_LANGUAGE, asLanguage, resolveLanguage, dateLocaleFor, type Language } from './languages';
export { loadCatalogue, type MessageKey } from './catalog';
export type { MessageParams } from './format';

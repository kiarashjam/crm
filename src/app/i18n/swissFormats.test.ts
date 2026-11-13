// Dates written by the shared helpers and the pages follow the person's language and the
// workspace's region once the app's provider has set them: a Swiss club's front desk on an
// English (US) laptop reads 31.12.2026, not 12/31/2026. Outside the provider (most tests)
// they keep the browser's own locale, as before.

import { afterEach, describe, expect, it } from 'vitest';
import { currentDateLocale, currentNumberLocale, setCurrentLanguage } from './current';
import { formatLongDate, getDateGroup } from '@/app/utils/dateFormatters';
import { count } from '@/app/pages/import/importKinds';

// Noon UTC is the 31st in every time zone a test machine is likely to run in.
const NEW_YEARS_EVE = '2026-12-31T12:00:00Z';

afterEach(() => setCurrentLanguage({ language: 'en', numberLocale: undefined, dateLocale: undefined }));

describe('formats outside the app', () => {
  it('leave the browser to choose, as they always did', () => {
    expect(currentDateLocale()).toBeUndefined();
    expect(currentNumberLocale()).toBeUndefined();
    expect(formatLongDate(NEW_YEARS_EVE)).toBe(new Date(NEW_YEARS_EVE).toLocaleDateString(undefined, { year: 'numeric', month: 'long', day: 'numeric' }));
  });
});

describe('formats inside a Swiss workspace', () => {
  it('write the day before the month, with month names in the reader’s language', () => {
    setCurrentLanguage({ language: 'en', numberLocale: 'de-CH', dateLocale: 'en-CH' });
    expect(formatLongDate(NEW_YEARS_EVE)).toBe('31 December 2026');
    expect(new Date(NEW_YEARS_EVE).toLocaleDateString(currentDateLocale(), { day: '2-digit', month: '2-digit', year: 'numeric' })).toBe('31.12.2026');

    setCurrentLanguage({ language: 'fr', numberLocale: 'de-CH', dateLocale: 'fr-CH' });
    expect(formatLongDate(NEW_YEARS_EVE)).toBe('31 décembre 2026');
    setCurrentLanguage({ language: 'de', numberLocale: 'de-CH', dateLocale: 'de-CH' });
    expect(formatLongDate(NEW_YEARS_EVE)).toBe('31. Dezember 2026');
    expect(getDateGroup('2001-03-15T12:00:00Z')).toBe('März 2001');
  });

  it('group the thousands of a count the workspace’s way', () => {
    setCurrentLanguage({ language: 'en', numberLocale: 'de-CH', dateLocale: 'en-CH' });
    // de-CH groups with an apostrophe, typographic or straight by ICU version.
    expect(count(12345, { one: 'contact', many: 'contacts' })).toMatch(/^12.345 contacts$/);
    expect(count(12345, { one: 'contact', many: 'contacts' })).not.toMatch(/^12,345/);
  });
});

// The language the app is in right now, for code that is not a component: a toast from an
// API helper, a date formatted by a plain function. The provider keeps it up to date; outside
// one (most tests) it is English.
//
// Kept apart from the provider so that low-level modules (api/messages.ts) can read it without
// importing React or the workspace context.

import { messageFor, type MessageKey } from './catalog';
import { formatMessage, type MessageParams } from './format';
import { DEFAULT_LANGUAGE, type Language } from './languages';

interface Current {
  language: Language;
  /** Undefined while nothing has set it: number helpers then keep the browser's locale, as before. */
  numberLocale: string | undefined;
  /** Undefined while nothing has set it: date helpers then keep the browser's locale, as before. */
  dateLocale: string | undefined;
}

let current: Current = { language: DEFAULT_LANGUAGE, numberLocale: undefined, dateLocale: undefined };

/** Called by the provider whenever the language or the workspace's formats change. */
export function setCurrentLanguage(next: Current): void {
  current = next;
}

/** The app's current language. */
export function currentLanguage(): Language {
  return current.language;
}

/** The words for a key in the app's current language, for code outside components. */
export function translate(key: MessageKey, params?: MessageParams): string {
  return formatMessage(current.language, messageFor(current.language, key), params, current.numberLocale ?? current.language);
}

/**
 * The locale dates are written in right now (the person's language with the workspace's
 * region: "fr-CH"), for code that formats a date without a hook: pages that call
 * toLocaleDateString, the shared date helpers. Undefined outside the app's provider (most
 * tests), which keeps the browser's own locale, as before.
 */
export function currentDateLocale(): string | undefined {
  return current.dateLocale;
}

/**
 * The locale numbers are written in right now: the workspace's number format ("de-CH"
 * groups thousands with an apostrophe). Undefined outside the app's provider, as above.
 */
export function currentNumberLocale(): string | undefined {
  return current.numberLocale;
}

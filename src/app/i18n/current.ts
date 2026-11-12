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
  numberLocale: string;
  /** Undefined while nothing has set it: date helpers then keep the browser's locale, as before. */
  dateLocale: string | undefined;
}

let current: Current = { language: DEFAULT_LANGUAGE, numberLocale: DEFAULT_LANGUAGE, dateLocale: undefined };

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
  return formatMessage(current.language, messageFor(current.language, key), params, current.numberLocale);
}

/** The locale dates are written in right now, for date helpers outside components. */
export function currentDateLocale(): string | undefined {
  return current.dateLocale;
}

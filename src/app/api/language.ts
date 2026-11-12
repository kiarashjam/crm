// The language a person chose for the app, kept on their account so it follows them
// to every browser they sign in from. Null means they have not chosen: the app then
// follows their workspace's locale, then their browser (see i18n/languages.ts).

import { authFetchJson, servesDemoData } from './apiClient';
import type { Language } from '@/app/i18n/languages';

export interface LanguageChoice {
  language: Language | null;
}

/** The signed-in person's choice, or null when they have made none. The demo has no account to keep one. */
export async function getMyLanguage(options: { signal?: AbortSignal } = {}): Promise<Language | null> {
  if (servesDemoData()) return null;
  const res = await authFetchJson<LanguageChoice>('/api/settings/language', { signal: options.signal });
  return res?.language ?? null;
}

/** Keep the choice on the account; null goes back to following the workspace and the browser. */
export async function saveMyLanguage(language: Language | null): Promise<Language | null> {
  if (servesDemoData()) return language;
  const res = await authFetchJson<LanguageChoice>('/api/settings/language', {
    method: 'PUT',
    body: JSON.stringify({ language }),
  });
  return res?.language ?? null;
}

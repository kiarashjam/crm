// The message catalogues: English bundled with the app, German and French fetched the
// first time someone reads them, so nobody downloads two languages they never use.
//
// English is the source: its keys are the keys, and its words are what a key shows
// while another language is still on its way, or if one is ever missing a key (the
// catalogue test stops that from shipping).

import { en } from './locales/en';
import type { Language } from './languages';
import type { Message } from './format';

export type Catalogue = typeof en;
export type MessageKey = keyof Catalogue;

/** A translation of an English section: every key, no others, each with its words. */
export type Translation<T> = { [K in keyof T]: Message };

const loaded = new Map<Language, Record<string, Message>>([['en', en]]);
const loading = new Map<Language, Promise<Record<string, Message>>>();

/** The catalogue when it is already here; undefined while it still has to be fetched. */
export function catalogueIfLoaded(language: Language): Record<string, Message> | undefined {
  return loaded.get(language);
}

/** Fetches a language's catalogue once; later calls get the same one. */
export function loadCatalogue(language: Language): Promise<Record<string, Message>> {
  const ready = loaded.get(language);
  if (ready) return Promise.resolve(ready);
  let pending = loading.get(language);
  if (!pending) {
    const fetched: Promise<Record<string, Message>> =
      language === 'de' ? import('./locales/de').then((m) => m.de)
        : language === 'fr' ? import('./locales/fr').then((m) => m.fr)
          : Promise.resolve(en);
    pending = fetched.then((catalogue) => {
      loaded.set(language, catalogue);
      loading.delete(language);
      return catalogue;
    }, (err: unknown) => {
      // A failed download (offline) is tried again on the next switch.
      loading.delete(language);
      throw err;
    });
    loading.set(language, pending);
  }
  return pending;
}

/** The words for a key: the language's, else English's, else the key itself. */
export function messageFor(language: Language, key: string): Message {
  return loaded.get(language)?.[key] ?? (en as Record<string, Message>)[key] ?? key;
}

// The languages the app is written in, and how one is picked for a person.
//
// The UI was English only. A Swiss club's members read German or French, and so do most
// of the people working its front desk, so the app now speaks all three. Which one a
// person sees is, in order: the one they chose (kept on their account, and in this
// browser for the sign-in page and the public pages), the language of the workspace's
// locale ("fr-CH" reads French), the browser's, and English.
//
// Numbers and money keep the workspace's own format (Settings, Regional): a French
// colleague in a de-CH workspace reads the same "CHF 1’234.50" as everyone else. Dates
// are written in the person's language with the workspace's region, so month names are
// theirs and the order is Swiss: fr-CH, de-CH, en-CH.

export const LANGUAGES = ['en', 'de', 'fr'] as const;

export type Language = (typeof LANGUAGES)[number];

export const DEFAULT_LANGUAGE: Language = 'en';

/** Each language in its own words, as a language picker shows them. */
export const LANGUAGE_NAMES: Record<Language, string> = {
  en: 'English',
  de: 'Deutsch',
  fr: 'Français',
};

/** One of ours from a language or locale name ("de-CH", "fr", "FR_ch"), or null for any other ("it-CH"). */
export function asLanguage(raw?: string | null): Language | null {
  const code = String(raw ?? '').trim().toLowerCase().split(/[-_]/)[0] ?? '';
  return (LANGUAGES as readonly string[]).includes(code) ? (code as Language) : null;
}

/** The first of the browser's languages we have, or null. */
export function browserLanguage(): Language | null {
  if (typeof navigator === 'undefined') return null;
  const list = navigator.languages?.length ? navigator.languages : [navigator.language];
  for (const name of list) {
    const found = asLanguage(name);
    if (found) return found;
  }
  return null;
}

/** Where the language a person sees came from. */
export type LanguageSource = 'chosen' | 'page' | 'workspace' | 'browser' | 'club' | 'default';

export interface LanguageInputs {
  /** What the person chose: on their account, or in this browser. */
  chosen?: string | null;
  /**
   * What a public page knows about its reader: a member's own language on their
   * unsubscribe or pay link, the ?lang= a club's website put on its form's link.
   */
  page?: string | null;
  /** The workspace's locale ("de-CH"), for someone signed in. */
  workspaceLocale?: string | null;
  /** The browser's language; read from the browser when left out. */
  browser?: string | null;
  /**
   * The language of the club behind a public page (from its locale), for a visitor whose
   * browser speaks none of ours: an Italian browser on a Lausanne club's form reads French.
   * It says nothing about the visitor, so their browser comes first.
   */
  club?: string | null;
}

/**
 * The language to show and where it came from: the person's choice, then what the
 * page knows about them, then their workspace's locale, then their browser, then the
 * club's language on a public page, then English. A value that is not one of ours
 * ("it-CH") is passed over.
 */
export function resolveLanguage(inputs: LanguageInputs): { language: Language; source: LanguageSource } {
  const chosen = asLanguage(inputs.chosen);
  if (chosen) return { language: chosen, source: 'chosen' };
  const page = asLanguage(inputs.page);
  if (page) return { language: page, source: 'page' };
  const workspace = asLanguage(inputs.workspaceLocale);
  if (workspace) return { language: workspace, source: 'workspace' };
  const browser = inputs.browser !== undefined ? asLanguage(inputs.browser) : browserLanguage();
  if (browser) return { language: browser, source: 'browser' };
  const club = asLanguage(inputs.club);
  if (club) return { language: club, source: 'club' };
  return { language: DEFAULT_LANGUAGE, source: 'default' };
}

/** The region of a locale ("CH" from "de-CH"), or null when it names none. */
export function regionOf(locale?: string | null): string | null {
  const parts = String(locale ?? '').trim().split(/[-_]/);
  const region = parts.find((p, i) => i > 0 && /^([A-Za-z]{2}|\d{3})$/.test(p));
  return region ? region.toUpperCase() : null;
}

function known(locale: string): boolean {
  try {
    return Intl.DateTimeFormat.supportedLocalesOf([locale]).length > 0;
  } catch {
    return false;
  }
}

/**
 * The locale dates are written in: the person's language with the workspace's region
 * ("fr" in a de-CH workspace writes fr-CH), so month and day names are in their
 * language and the order is the region's. Without a region, the language alone.
 */
export function dateLocaleFor(language: Language, workspaceLocale?: string | null): string {
  const region = regionOf(workspaceLocale);
  if (region) {
    const combined = `${language}-${region}`;
    if (known(combined)) return combined;
  }
  return language;
}

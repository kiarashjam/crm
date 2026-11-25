// The public website's constants: where it lives, what it is called, its languages.
//
// https://cadencee.ai answers with a 301 to https://www.cadencee.ai, so the www host is the
// one every canonical, hreflang, sitemap and og:url names.

export const SITE_URL = 'https://www.cadencee.ai';
export const SITE_NAME = 'CadenceE';
export const SITE_LOGO_PATH = '/logo-512.png';

/** The marketing site's languages, in the order the language switch shows them. */
export const SITE_LANGS = ['en', 'fr', 'de'] as const;
export type SiteLang = (typeof SITE_LANGS)[number];
export const DEFAULT_SITE_LANG: SiteLang = 'en';

export const SITE_LANG_NAMES: Record<SiteLang, string> = {
  en: 'English',
  fr: 'Français',
  de: 'Deutsch',
};

/** Open Graph locales: the site is written for Switzerland. */
export const OG_LOCALES: Record<SiteLang, string> = {
  en: 'en_GB',
  fr: 'fr_CH',
  de: 'de_CH',
};

export const OG_IMAGE_WIDTH = 1200;
export const OG_IMAGE_HEIGHT = 630;

/**
 * Where "Request a demo" and "Contact us" write to, from VITE_CONTACT_EMAIL at build time.
 * Empty until the owner sets one: the buttons then lead to the help page instead of a
 * made-up address.
 */
export const CONTACT_EMAIL: string = (import.meta.env?.VITE_CONTACT_EMAIL ?? '').trim();

/** An absolute URL on the canonical host, from a path ("/features", "/"). */
export function absoluteUrl(path: string): string {
  if (/^https?:\/\//.test(path)) return path;
  const clean = path.startsWith('/') ? path : `/${path}`;
  return clean === '/' ? `${SITE_URL}/` : `${SITE_URL}${clean}`;
}

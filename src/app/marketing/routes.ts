// The public website's route manifest: the one list the router, the head manager, the
// prerender, the sitemap and the OG images all read. Adding a page is adding an entry
// here, its words in ./meta.ts and ./content/{en,fr,de}.ts (docs/SEO.md walks through it).

import { SITE_LANGS, type SiteLang } from './site';

export const MARKETING_PAGE_IDS = [
  'home',
  'features',
  'private-clubs',
  'membership',
  'hospitality',
  'sales',
  'e-signature',
  'swiss',
  'billing',
  'email',
  'ai',
  'pricing',
] as const;

export type MarketingPageId = (typeof MARKETING_PAGE_IDS)[number];

/** home: a language's front page; hub: the features overview; landing: one topic; pricing: the price (the launch offer). */
export type MarketingPageKind = 'home' | 'hub' | 'landing' | 'pricing';

export interface MarketingPageDef {
  id: MarketingPageId;
  kind: MarketingPageKind;
  /** The page's address in each language. Slugs are in the page's own language. */
  paths: Record<SiteLang, string>;
  /** When the page's words last changed (YYYY-MM-DD): the sitemap's lastmod. Bump it with the copy. */
  lastmod: string;
}

export const MARKETING_PAGES: readonly MarketingPageDef[] = [
  { id: 'home', kind: 'home', lastmod: '2026-11-25', paths: { en: '/', fr: '/fr', de: '/de' } },
  { id: 'features', kind: 'hub', lastmod: '2026-11-25', paths: { en: '/features', fr: '/fr/fonctionnalites', de: '/de/funktionen' } },
  { id: 'private-clubs', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/crm-for-private-clubs', fr: '/fr/crm-club-prive', de: '/de/crm-fuer-privatclubs' } },
  { id: 'membership', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/membership-management-software', fr: '/fr/logiciel-gestion-membres', de: '/de/mitgliederverwaltung-software' } },
  { id: 'hospitality', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/crm-for-hospitality', fr: '/fr/crm-hotellerie-restauration', de: '/de/crm-gastronomie-hotellerie' } },
  { id: 'sales', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/sales-crm', fr: '/fr/crm-commercial', de: '/de/vertriebs-crm' } },
  { id: 'e-signature', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/e-signature-contracts', fr: '/fr/contrats-signature-electronique', de: '/de/vertraege-elektronische-signatur' } },
  { id: 'swiss', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/swiss-crm', fr: '/fr/crm-suisse', de: '/de/schweizer-crm' } },
  { id: 'billing', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/club-billing-and-payments', fr: '/fr/facturation-paiements-club', de: '/de/club-abrechnung-zahlungen' } },
  { id: 'email', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/email-automation-crm', fr: '/fr/crm-automatisation-email', de: '/de/crm-e-mail-automatisierung' } },
  { id: 'ai', kind: 'landing', lastmod: '2026-11-25', paths: { en: '/ai-crm-assistant', fr: '/fr/assistant-ia-crm', de: '/de/ki-crm-assistent' } },
  { id: 'pricing', kind: 'pricing', lastmod: '2026-11-25', paths: { en: '/pricing', fr: '/fr/tarifs', de: '/de/preise' } },
];

/** The topic pages, in the order the navigation and the hub list them. */
export const LANDING_PAGE_IDS = MARKETING_PAGES.filter((p) => p.kind === 'landing').map((p) => p.id);

export interface PublicPageDef {
  path: string;
  /** Whether search engines may index it: /login yes, password and email links no. */
  index: boolean;
  /** Listed in the sitemap. */
  sitemap: boolean;
  /** Its HTML is written at build with this page's head (the body is left to the app). */
  prerender: boolean;
  lastmod: string;
}

/** The app's public pages outside the marketing site: they get a head, not a landing page. */
export const PUBLIC_PAGES: readonly PublicPageDef[] = [
  { path: '/login', index: true, sitemap: false, prerender: true, lastmod: '2026-11-25' },
  { path: '/help', index: true, sitemap: true, prerender: true, lastmod: '2026-11-25' },
  { path: '/privacy', index: true, sitemap: true, prerender: true, lastmod: '2026-11-25' },
  { path: '/terms', index: true, sitemap: true, prerender: true, lastmod: '2026-11-25' },
  { path: '/forgot-password', index: false, sitemap: false, prerender: true, lastmod: '2026-11-25' },
  { path: '/reset-password', index: false, sitemap: false, prerender: true, lastmod: '2026-11-25' },
  { path: '/verify-email', index: false, sitemap: false, prerender: true, lastmod: '2026-11-25' },
];

/** "/features/" → "/features"; the root stays "/". Query and hash are dropped. */
export function normalizePath(pathname: string): string {
  let path = (pathname || '/').split(/[?#]/)[0] || '/';
  if (!path.startsWith('/')) path = `/${path}`;
  path = path.replace(/\/{2,}/g, '/');
  if (path.length > 1 && path.endsWith('/')) path = path.slice(0, -1);
  return path;
}

export interface MarketingMatch {
  page: MarketingPageDef;
  lang: SiteLang;
}

const BY_PATH = new Map<string, MarketingMatch>();
for (const page of MARKETING_PAGES) {
  for (const lang of SITE_LANGS) BY_PATH.set(page.paths[lang], { page, lang });
}

/** The marketing page at a path, in which language; null for every other path. */
export function matchMarketingPath(pathname: string): MarketingMatch | null {
  return BY_PATH.get(normalizePath(pathname)) ?? null;
}

export function marketingPage(id: MarketingPageId): MarketingPageDef {
  const page = MARKETING_PAGES.find((p) => p.id === id);
  if (!page) throw new Error(`Unknown marketing page: ${id}`);
  return page;
}

export function marketingPath(id: MarketingPageId, lang: SiteLang): string {
  return marketingPage(id).paths[lang];
}

export function publicPage(pathname: string): PublicPageDef | null {
  const path = normalizePath(pathname);
  return PUBLIC_PAGES.find((p) => p.path === path) ?? null;
}

/** The language a path is written in, from its prefix: "/fr/…" French, "/de/…" German, else English. */
export function langOfPath(pathname: string): SiteLang {
  const path = normalizePath(pathname);
  for (const lang of SITE_LANGS) {
    if (lang !== 'en' && (path === `/${lang}` || path.startsWith(`/${lang}/`))) return lang;
  }
  return 'en';
}

/** Every address the prerender writes a page for: the marketing pages in each language, then the public ones. */
export function prerenderPaths(): string[] {
  const paths: string[] = [];
  for (const page of MARKETING_PAGES) for (const lang of SITE_LANGS) paths.push(page.paths[lang]);
  for (const page of PUBLIC_PAGES) if (page.prerender) paths.push(page.path);
  return paths;
}

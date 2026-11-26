// The head of every page: title, description, canonical, robots, Open Graph, Twitter,
// hreflang and JSON-LD, worked out from the path alone.
//
// One function (headForPath) serves both the browser (SeoHead applies it on every route
// change) and the build (the prerender writes it into each page's HTML), so what a crawler
// reads without JavaScript and what the app shows are the same.
//
//   marketing pages   index, canonical on www, hreflang en/fr/de/x-default, OG image
//   /login, /help, …  index (or noindex for password and email links), no hreflang
//   everything else   the app: noindex, nofollow; the title is left to the page

import {
  breadcrumbJsonLd,
  graphJsonLd,
  organizationJsonLd,
  serializeJsonLd,
  softwareApplicationJsonLd,
  websiteJsonLd,
  type JsonLd,
} from './jsonld';
import { isLaunchOfferActive } from '@/app/lib/launchOffer';
import { PAGE_META, PUBLIC_PAGE_META, SITE_STRINGS, pageMeta } from './meta';
import {
  marketingPath,
  matchMarketingPath,
  publicPage,
  type MarketingMatch,
  type PublicPageDef,
} from './routes';
import {
  OG_IMAGE_HEIGHT,
  OG_IMAGE_WIDTH,
  OG_LOCALES,
  SITE_LANGS,
  SITE_NAME,
  absoluteUrl,
  type SiteLang,
} from './site';

export type HeadKind = 'marketing' | 'public' | 'app' | 'notfound';

export interface HeadMeta {
  name?: string;
  property?: string;
  content: string;
}

export interface HeadLink {
  rel: string;
  href: string;
  hreflang?: string;
}

export interface HeadData {
  kind: HeadKind;
  /** The document title; null leaves it to the page (the app's own pages set theirs). */
  title: string | null;
  /** <html lang>; null leaves it to the app's language. */
  lang: SiteLang | null;
  metas: HeadMeta[];
  links: HeadLink[];
  jsonLd: JsonLd[];
}

export const ROBOTS_INDEX = 'index, follow, max-image-preview:large, max-snippet:-1';
export const ROBOTS_NOINDEX = 'noindex, nofollow';
/** A page not to list that still leads somewhere worth following (the 404 page, password links). */
export const ROBOTS_NOINDEX_FOLLOW = 'noindex, follow';

export const DEFAULT_OG_IMAGE = '/og/default.png';

/** Pages whose OG image carries the launch offer while it runs (a second image, -offer.png). */
export const OFFER_OG_PAGES = ['home', 'pricing'] as const;

export function ogImagePath(id: string, lang: SiteLang, offer = false): string {
  return offer ? `/og/${id}-${lang}-offer.png` : `/og/${id}-${lang}.png`;
}

interface SocialInput {
  title: string;
  description: string;
  url: string;
  image: string;
  imageAlt: string;
  lang: SiteLang;
  type?: 'website' | 'article';
}

function socialMetas(input: SocialInput): HeadMeta[] {
  const image = absoluteUrl(input.image);
  return [
    { property: 'og:type', content: input.type ?? 'website' },
    { property: 'og:site_name', content: SITE_NAME },
    { property: 'og:title', content: input.title },
    { property: 'og:description', content: input.description },
    { property: 'og:url', content: input.url },
    { property: 'og:image', content: image },
    { property: 'og:image:type', content: 'image/png' },
    { property: 'og:image:width', content: String(OG_IMAGE_WIDTH) },
    { property: 'og:image:height', content: String(OG_IMAGE_HEIGHT) },
    { property: 'og:image:alt', content: input.imageAlt },
    { property: 'og:locale', content: OG_LOCALES[input.lang] },
    ...SITE_LANGS.filter((l) => l !== input.lang).map((l) => ({ property: 'og:locale:alternate', content: OG_LOCALES[l] })),
    { name: 'twitter:card', content: 'summary_large_image' },
    { name: 'twitter:title', content: input.title },
    { name: 'twitter:description', content: input.description },
    { name: 'twitter:image', content: image },
    { name: 'twitter:image:alt', content: input.imageAlt },
  ];
}

/** The hreflang set of a marketing page: each language, and English as x-default. */
export function hreflangLinks(match: MarketingMatch): HeadLink[] {
  const links: HeadLink[] = SITE_LANGS.map((lang) => ({
    rel: 'alternate',
    hreflang: lang,
    href: absoluteUrl(match.page.paths[lang]),
  }));
  links.push({ rel: 'alternate', hreflang: 'x-default', href: absoluteUrl(match.page.paths.en) });
  return links;
}

/** Breadcrumbs of a marketing page: Home, then the page (none on a home page). */
export function breadcrumbsFor(match: MarketingMatch): { name: string; path: string }[] {
  if (match.page.kind === 'home') return [];
  const meta = PAGE_META[match.lang];
  return [
    { name: meta.home.nav, path: marketingPath('home', match.lang) },
    { name: meta[match.page.id].nav, path: match.page.paths[match.lang] },
  ];
}

function marketingHead(match: MarketingMatch, now: Date | number): HeadData {
  const { page, lang } = match;
  const meta = pageMeta(lang, page.id, now);
  const url = absoluteUrl(page.paths[lang]);
  const offer = isLaunchOfferActive(now);
  const pricingPath = marketingPath('pricing', lang);
  const jsonLd: JsonLd[] = [];
  if (page.kind === 'home') {
    jsonLd.push(graphJsonLd([organizationJsonLd(), websiteJsonLd(), softwareApplicationJsonLd(lang, meta.description, now, pricingPath)]));
  } else if (page.kind === 'hub' || page.kind === 'pricing' || page.offerOnly) {
    jsonLd.push(graphJsonLd([softwareApplicationJsonLd(lang, meta.description, now, pricingPath), organizationJsonLd()]));
  }
  const crumbs = breadcrumbsFor(match);
  if (crumbs.length) jsonLd.push(breadcrumbJsonLd(crumbs));
  return {
    kind: 'marketing',
    title: meta.title,
    lang,
    metas: [
      { name: 'description', content: meta.description },
      // A page about the launch offer is not to be found once the offer has ended.
      { name: 'robots', content: page.offerOnly && !offer ? ROBOTS_NOINDEX_FOLLOW : ROBOTS_INDEX },
      ...socialMetas({
        title: meta.title,
        description: meta.description,
        url,
        image: ogImagePath(page.id, lang, offer && (OFFER_OG_PAGES as readonly string[]).includes(page.id)),
        imageAlt: `${SITE_NAME}: ${meta.nav}`,
        lang,
      }),
    ],
    links: [{ rel: 'canonical', href: url }, ...hreflangLinks(match)],
    jsonLd,
  };
}

function publicHead(page: PublicPageDef): HeadData {
  const meta = PUBLIC_PAGE_META[page.path] ?? { title: SITE_NAME, description: '' };
  const url = absoluteUrl(page.path);
  const metas: HeadMeta[] = [];
  if (meta.description) metas.push({ name: 'description', content: meta.description });
  metas.push({ name: 'robots', content: page.index ? ROBOTS_INDEX : ROBOTS_NOINDEX_FOLLOW });
  if (page.index) {
    metas.push(...socialMetas({
      title: meta.title,
      description: meta.description,
      url,
      image: DEFAULT_OG_IMAGE,
      imageAlt: SITE_NAME,
      lang: 'en',
    }));
  }
  return {
    kind: 'public',
    title: meta.title,
    // The app's own language decides what these pages are written in.
    lang: null,
    metas,
    links: page.index ? [{ rel: 'canonical', href: url }] : [],
    jsonLd: [],
  };
}

/** The app's pages, and the public links with a token in them (/sign/…, /pay/…): never listed. */
export function appHead(): HeadData {
  return {
    kind: 'app',
    title: null,
    lang: null,
    metas: [{ name: 'robots', content: ROBOTS_NOINDEX }],
    links: [],
    jsonLd: [],
  };
}

export function notFoundHead(lang: SiteLang): HeadData {
  return {
    kind: 'notfound',
    title: SITE_STRINGS[lang].notFoundTitle,
    lang,
    metas: [{ name: 'robots', content: ROBOTS_NOINDEX_FOLLOW }],
    links: [],
    jsonLd: [],
  };
}

/**
 * The head of the page at a path, at a moment (the launch offer's words and Offer data
 * only while it runs). Paths that are neither marketing nor public read as the app's.
 */
export function headForPath(pathname: string, now: Date | number = Date.now()): HeadData {
  const match = matchMarketingPath(pathname);
  if (match) return marketingHead(match, now);
  const page = publicPage(pathname);
  if (page) return publicHead(page);
  return appHead();
}

// ---------------------------------------------------------------------------
// Writing a head: as HTML at build, into the document in the browser.
// Every tag the head manager owns carries data-head, so it can replace exactly those.

export const MANAGED_ATTR = 'data-head';

function escapeHtml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/** The head as HTML, for the prerendered pages. */
export function renderHeadHtml(head: HeadData, fallbackTitle = SITE_NAME): string {
  const lines: string[] = [];
  lines.push(`<title>${escapeHtml(head.title ?? fallbackTitle)}</title>`);
  for (const meta of head.metas) {
    const key = meta.name ? `name="${escapeHtml(meta.name)}"` : `property="${escapeHtml(meta.property ?? '')}"`;
    lines.push(`<meta ${key} content="${escapeHtml(meta.content)}" ${MANAGED_ATTR} />`);
  }
  for (const link of head.links) {
    const lang = link.hreflang ? ` hreflang="${escapeHtml(link.hreflang)}"` : '';
    lines.push(`<link rel="${escapeHtml(link.rel)}"${lang} href="${escapeHtml(link.href)}" ${MANAGED_ATTR} />`);
  }
  for (const data of head.jsonLd) {
    lines.push(`<script type="application/ld+json" ${MANAGED_ATTR}>${serializeJsonLd(data)}</script>`);
  }
  return lines.join('\n    ');
}

/**
 * Puts a head into the document: the title and <html lang> when the head names them, and
 * its tags in place of the ones the last head (or the prerendered HTML) left.
 */
export function applyHead(doc: Document, head: HeadData): void {
  if (head.title !== null) doc.title = head.title;
  if (head.lang) doc.documentElement.lang = head.lang;
  doc.head.querySelectorAll(`[${MANAGED_ATTR}]`).forEach((el) => el.remove());
  const fragment = doc.createDocumentFragment();
  for (const meta of head.metas) {
    const el = doc.createElement('meta');
    if (meta.name) el.setAttribute('name', meta.name);
    if (meta.property) el.setAttribute('property', meta.property);
    el.setAttribute('content', meta.content);
    el.setAttribute(MANAGED_ATTR, '');
    fragment.appendChild(el);
  }
  for (const link of head.links) {
    const el = doc.createElement('link');
    el.setAttribute('rel', link.rel);
    if (link.hreflang) el.setAttribute('hreflang', link.hreflang);
    el.setAttribute('href', link.href);
    el.setAttribute(MANAGED_ATTR, '');
    fragment.appendChild(el);
  }
  for (const data of head.jsonLd) {
    const el = doc.createElement('script');
    el.setAttribute('type', 'application/ld+json');
    el.setAttribute(MANAGED_ATTR, '');
    el.textContent = serializeJsonLd(data);
    fragment.appendChild(el);
  }
  doc.head.appendChild(fragment);
}

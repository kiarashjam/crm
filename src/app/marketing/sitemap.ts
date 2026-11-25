// sitemap.xml, from the route manifest: every marketing page in every language with its
// hreflang alternates, and the public pages worth listing. Written at build by the prerender.

import { MARKETING_PAGES, PUBLIC_PAGES, type MarketingPageDef, type PublicPageDef } from './routes';
import { SITE_LANGS, absoluteUrl } from './site';

export interface SitemapEntry {
  loc: string;
  lastmod: string;
  alternates: { hreflang: string; href: string }[];
}

function escapeXml(value: string): string {
  return value
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&apos;');
}

export function sitemapEntries(
  pages: readonly MarketingPageDef[] = MARKETING_PAGES,
  publicPages: readonly PublicPageDef[] = PUBLIC_PAGES,
): SitemapEntry[] {
  const entries: SitemapEntry[] = [];
  for (const page of pages) {
    const alternates = [
      ...SITE_LANGS.map((lang) => ({ hreflang: lang, href: absoluteUrl(page.paths[lang]) })),
      { hreflang: 'x-default', href: absoluteUrl(page.paths.en) },
    ];
    for (const lang of SITE_LANGS) {
      entries.push({ loc: absoluteUrl(page.paths[lang]), lastmod: page.lastmod, alternates });
    }
  }
  for (const page of publicPages) {
    if (page.sitemap && page.index) entries.push({ loc: absoluteUrl(page.path), lastmod: page.lastmod, alternates: [] });
  }
  return entries;
}

export function buildSitemapXml(entries: SitemapEntry[] = sitemapEntries()): string {
  const lines = [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9" xmlns:xhtml="http://www.w3.org/1999/xhtml">',
  ];
  for (const entry of entries) {
    lines.push('  <url>');
    lines.push(`    <loc>${escapeXml(entry.loc)}</loc>`);
    lines.push(`    <lastmod>${escapeXml(entry.lastmod)}</lastmod>`);
    for (const alt of entry.alternates) {
      lines.push(`    <xhtml:link rel="alternate" hreflang="${escapeXml(alt.hreflang)}" href="${escapeXml(alt.href)}" />`);
    }
    lines.push('  </url>');
  }
  lines.push('</urlset>');
  return `${lines.join('\n')}\n`;
}

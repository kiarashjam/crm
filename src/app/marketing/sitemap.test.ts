import { describe, expect, it } from 'vitest';
import { buildSitemapXml, sitemapEntries } from './sitemap';
import { MARKETING_PAGES, PUBLIC_PAGES, type MarketingPageDef } from './routes';
import { SITE_LANGS } from './site';

const DURING_OFFER = Date.parse('2026-11-26T12:00:00Z');
const AFTER_OFFER = Date.parse('2027-01-01T00:00:00+01:00');

describe('sitemap from the route manifest', () => {
  it('lists every marketing page in every language, and the public pages marked for it', () => {
    const entries = sitemapEntries(MARKETING_PAGES, PUBLIC_PAGES, DURING_OFFER);
    const marketing = MARKETING_PAGES.length * SITE_LANGS.length;
    const listedPublic = PUBLIC_PAGES.filter((p) => p.sitemap && p.index).length;
    expect(entries).toHaveLength(marketing + listedPublic);
    // 13 pages × 3 languages, and /help, /privacy, /terms.
    expect(entries).toHaveLength(42);
    const locs = entries.map((e) => e.loc);
    expect(new Set(locs).size).toBe(locs.length);
    expect(locs).toContain('https://www.cadencee.ai/');
    expect(locs).toContain('https://www.cadencee.ai/fr/crm-club-prive');
    expect(locs).toContain('https://www.cadencee.ai/de/preise');
    expect(locs).toContain('https://www.cadencee.ai/privacy');
    // Not indexed, so not listed.
    expect(locs).not.toContain('https://www.cadencee.ai/login');
    expect(locs).not.toContain('https://www.cadencee.ai/reset-password');
    for (const loc of locs) expect(loc).toMatch(/^https:\/\/www\.cadencee\.ai\//);
  });

  it('lists the pages about the launch offer only while it runs', () => {
    const free = ['https://www.cadencee.ai/free-crm', 'https://www.cadencee.ai/fr/crm-gratuit', 'https://www.cadencee.ai/de/kostenloses-crm'];
    const during = sitemapEntries(MARKETING_PAGES, PUBLIC_PAGES, DURING_OFFER).map((e) => e.loc);
    for (const loc of free) expect(during).toContain(loc);
    // The last second of 31 December 2026 in Zurich still lists them.
    expect(sitemapEntries(MARKETING_PAGES, PUBLIC_PAGES, AFTER_OFFER - 1).map((e) => e.loc)).toContain(free[0]);

    const after = sitemapEntries(MARKETING_PAGES, PUBLIC_PAGES, AFTER_OFFER);
    expect(after).toHaveLength(39);
    for (const loc of free) expect(after.map((e) => e.loc)).not.toContain(loc);
    for (const entry of after) {
      for (const alt of entry.alternates) expect(alt.href).not.toMatch(/free-crm|crm-gratuit|kostenloses-crm/);
    }
    expect(buildSitemapXml(after).match(/<loc>/g)).toHaveLength(39);
    expect(buildSitemapXml(sitemapEntries(MARKETING_PAGES, PUBLIC_PAGES, DURING_OFFER)).match(/<loc>/g)).toHaveLength(42);
  });

  it('gives each language version the full hreflang set, x-default English', () => {
    const entry = sitemapEntries().find((e) => e.loc === 'https://www.cadencee.ai/de/vertriebs-crm');
    expect(entry?.alternates).toEqual([
      { hreflang: 'en', href: 'https://www.cadencee.ai/sales-crm' },
      { hreflang: 'fr', href: 'https://www.cadencee.ai/fr/crm-commercial' },
      { hreflang: 'de', href: 'https://www.cadencee.ai/de/vertriebs-crm' },
      { hreflang: 'x-default', href: 'https://www.cadencee.ai/sales-crm' },
    ]);
  });

  it('writes well-formed XML with a lastmod for every URL', () => {
    const xml = buildSitemapXml();
    const doc = new DOMParser().parseFromString(xml, 'application/xml');
    expect(doc.querySelector('parsererror')).toBeNull();
    const urls = [...doc.getElementsByTagName('url')];
    expect(urls.length).toBe(sitemapEntries().length);
    for (const url of urls) {
      expect(url.getElementsByTagName('loc')[0]?.textContent).toMatch(/^https:/);
      expect(url.getElementsByTagName('lastmod')[0]?.textContent).toMatch(/^\d{4}-\d{2}-\d{2}$/);
    }
    expect(xml).toContain('xmlns:xhtml="http://www.w3.org/1999/xhtml"');
  });

  it('builds from any manifest and escapes what it writes', () => {
    const page: MarketingPageDef = {
      id: 'features',
      kind: 'hub',
      lastmod: '2027-02-03',
      paths: { en: '/a&b', fr: '/fr/a', de: '/de/a' },
    };
    const xml = buildSitemapXml(sitemapEntries([page], []));
    expect(xml).toContain('<loc>https://www.cadencee.ai/a&amp;b</loc>');
    expect(xml).toContain('<lastmod>2027-02-03</lastmod>');
    expect(xml.match(/<url>/g)).toHaveLength(3);
  });

  it('dates every manifest entry', () => {
    for (const page of [...MARKETING_PAGES, ...PUBLIC_PAGES]) expect(page.lastmod).toMatch(/^\d{4}-\d{2}-\d{2}$/);
  });
});

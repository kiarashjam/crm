import { describe, expect, it } from 'vitest';
import { buildSitemapXml, sitemapEntries } from './sitemap';
import { MARKETING_PAGES, PUBLIC_PAGES, type MarketingPageDef } from './routes';
import { SITE_LANGS } from './site';

describe('sitemap from the route manifest', () => {
  it('lists every marketing page in every language, and the public pages marked for it', () => {
    const entries = sitemapEntries();
    const marketing = MARKETING_PAGES.length * SITE_LANGS.length;
    const listedPublic = PUBLIC_PAGES.filter((p) => p.sitemap && p.index).length;
    expect(entries).toHaveLength(marketing + listedPublic);
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

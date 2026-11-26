import { describe, expect, it } from 'vitest';
import de from './content/de';
import en from './content/en';
import fr from './content/fr';
import { breadcrumbJsonLd, faqPageJsonLd, serializeJsonLd, softwareApplicationJsonLd, stripLinkMarkup, type JsonLd } from './jsonld';
import { MARKETING_PAGES } from './routes';
import { headForPath } from './seo';
import { SITE_LANGS } from './site';

const DURING_OFFER = Date.parse('2026-12-31T22:59:59Z'); // 23:59:59 in Zurich on 31 December 2026
const AFTER_OFFER = Date.parse('2026-12-31T23:00:00Z'); // midnight in Zurich: 1 January 2027

/** Every node of a JSON-LD document, @graph members included. */
function nodes(doc: JsonLd): JsonLd[] {
  const graph = doc['@graph'];
  return Array.isArray(graph) ? (graph as JsonLd[]) : [doc];
}

function allHeadJsonLd(now: number): JsonLd[] {
  const docs: JsonLd[] = [];
  for (const page of MARKETING_PAGES) for (const lang of SITE_LANGS) docs.push(...headForPath(page.paths[lang], now).jsonLd);
  return docs;
}

describe('JSON-LD', () => {
  it('serializes to JSON that parses back unchanged, with a schema.org context', () => {
    for (const doc of allHeadJsonLd(DURING_OFFER)) {
      const text = serializeJsonLd(doc);
      expect(text).not.toMatch(/<\/script/i);
      expect(JSON.parse(text)).toEqual(doc);
      expect(doc['@context']).toBe('https://schema.org');
    }
  });

  it('describes the organisation, the website and the software on each home page', () => {
    for (const lang of SITE_LANGS) {
      const home = headForPath(MARKETING_PAGES[0]!.paths[lang], DURING_OFFER).jsonLd.flatMap(nodes);
      const types = home.map((n) => n['@type']);
      expect(types).toEqual(expect.arrayContaining(['Organization', 'WebSite', 'SoftwareApplication']));
      const org = home.find((n) => n['@type'] === 'Organization')!;
      expect(org.name).toBe('CadenceE');
      expect(org.url).toBe('https://www.cadencee.ai/');
      expect((org.logo as JsonLd).url).toBe('https://www.cadencee.ai/logo-512.png');
    }
  });

  it('never claims a rating or a review, and is a web business application', () => {
    const app = softwareApplicationJsonLd('en', 'x', AFTER_OFFER);
    expect(app.applicationCategory).toBe('BusinessApplication');
    expect(app.operatingSystem).toBe('Web');
    for (const doc of allHeadJsonLd(DURING_OFFER)) {
      const text = JSON.stringify(doc);
      expect(text).not.toMatch(/aggregateRating|"review"|ratingValue/);
    }
  });

  it('offers the software free until 31 December 2026, and drops the offer after', () => {
    const during = softwareApplicationJsonLd('en', 'x', DURING_OFFER);
    expect(during.offers).toEqual({
      '@type': 'Offer',
      price: '0',
      priceCurrency: 'CHF',
      priceValidUntil: '2026-12-31',
      availability: 'https://schema.org/InStock',
      url: 'https://www.cadencee.ai/pricing',
    });
    expect(softwareApplicationJsonLd('en', 'x', AFTER_OFFER).offers).toBeUndefined();
    const pricingAfter = JSON.stringify(headForPath('/pricing', AFTER_OFFER).jsonLd);
    expect(pricingAfter).not.toContain('"Offer"');
    expect(JSON.stringify(headForPath('/fr/tarifs', DURING_OFFER).jsonLd)).toContain('https://www.cadencee.ai/fr/tarifs');
  });

  it('gives every subpage a breadcrumb trail of absolute URLs, numbered from 1', () => {
    for (const page of MARKETING_PAGES) {
      for (const lang of SITE_LANGS) {
        const crumbs = headForPath(page.paths[lang], DURING_OFFER).jsonLd.filter((d) => d['@type'] === 'BreadcrumbList');
        if (page.kind === 'home') {
          expect(crumbs).toHaveLength(0);
          continue;
        }
        expect(crumbs).toHaveLength(1);
        const items = crumbs[0]!.itemListElement as JsonLd[];
        // A guide sits under the guides' index: Home › Guides › the guide.
        expect(items.map((i) => i.position)).toEqual(page.kind === 'guide' ? [1, 2, 3] : [1, 2]);
        for (const item of items) expect(item.item).toMatch(/^https:\/\/www\.cadencee\.ai\//);
      }
    }
    expect(breadcrumbJsonLd([{ name: 'Home', path: '/' }]).itemListElement).toEqual([
      { '@type': 'ListItem', position: 1, name: 'Home', item: 'https://www.cadencee.ai/' },
    ]);
  });

  it('builds a valid FAQPage from every page FAQ, links read as their words', () => {
    for (const content of [en, fr, de]) {
      for (const page of Object.values(content.pages)) {
        const faq = faqPageJsonLd(page!.faq.items, 'en');
        expect(faq['@type']).toBe('FAQPage');
        const questions = faq.mainEntity as JsonLd[];
        expect(questions.length).toBe(page!.faq.items.length);
        for (const q of questions) {
          expect(q['@type']).toBe('Question');
          expect(typeof q.name).toBe('string');
          const answer = q.acceptedAnswer as JsonLd;
          expect(answer['@type']).toBe('Answer');
          expect(answer.text).not.toMatch(/\]\(|page:/);
        }
        expect(JSON.parse(serializeJsonLd(faq))).toEqual(faq);
      }
    }
    expect(stripLinkMarkup('See [billing](page:billing) and [sign in](/login).')).toBe('See billing and sign in.');
  });

  it('describes every guide as an Article by CadenceE, with its dates, in its language', () => {
    for (const page of MARKETING_PAGES.filter((p) => p.kind === 'guide')) {
      for (const lang of SITE_LANGS) {
        const head = headForPath(page.paths[lang], DURING_OFFER);
        const article = head.jsonLd.find((d) => d['@type'] === 'Article');
        expect(article, page.paths[lang]).toBeDefined();
        expect(article!.inLanguage).toBe(lang);
        expect(article!.datePublished).toBe(page.published);
        expect(article!.dateModified).toBe(page.lastmod);
        expect(article!.url).toBe(`https://www.cadencee.ai${page.paths[lang]}`);
        expect(String(article!.headline)).not.toMatch(/\| CadenceE$/);
        expect((article!.publisher as JsonLd).name).toBe('CadenceE');
        expect(head.metas.find((m) => m.property === 'og:type')?.content).toBe('article');
      }
    }
  });
});

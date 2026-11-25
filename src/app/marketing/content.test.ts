import { describe, expect, it } from 'vitest';
import de from './content/de';
import en from './content/en';
import fr from './content/fr';
import type { LanguageContent, PageContent } from './content/types';
import { PAGE_META, PUBLIC_PAGE_META, pageMeta } from './meta';
import { MARKETING_PAGES, MARKETING_PAGE_IDS } from './routes';
import { SITE_LANGS, type SiteLang } from './site';

const CONTENT: Record<SiteLang, LanguageContent> = { en, fr, de };
const DURING_OFFER = Date.parse('2026-11-25T12:00:00Z');
const AFTER_OFFER = Date.parse('2027-01-02T12:00:00Z');

/** Words a reader sees in the page's own content (not the header and footer). */
function words(page: PageContent): number {
  const texts: string[] = [];
  const walk = (value: unknown) => {
    if (typeof value === 'string') texts.push(value.replace(/\]\([^)]*\)/g, ']'));
    else if (Array.isArray(value)) value.forEach(walk);
    else if (value && typeof value === 'object') for (const [key, v] of Object.entries(value)) if (key !== 'related' && key !== 'icon' && key !== 'link' && key !== 'afterOffer') walk(v);
  };
  walk(page);
  return texts.join(' ').split(/\s+/).filter((w) => /\p{L}/u.test(w)).length;
}

const fullH1 = (page: PageContent) => `${page.h1}${page.h1Accent ? ` ${page.h1Accent}` : ''}`;

describe('titles and descriptions', () => {
  for (const [label, now] of [['during the launch offer', DURING_OFFER], ['after it', AFTER_OFFER]] as const) {
    for (const lang of SITE_LANGS) {
      it(`are unique and within limits in ${lang}, ${label}`, () => {
        const metas = MARKETING_PAGE_IDS.map((id) => pageMeta(lang, id, now));
        const titles = metas.map((m) => m.title);
        const descriptions = metas.map((m) => m.description);
        expect(new Set(titles).size).toBe(titles.length);
        expect(new Set(descriptions).size).toBe(descriptions.length);
        for (const m of metas) {
          expect(m.title.length, m.title).toBeLessThanOrEqual(60);
          expect(m.title.length, m.title).toBeGreaterThanOrEqual(25);
          expect(m.description.length, m.description).toBeLessThanOrEqual(155);
          expect(m.description.length, m.description).toBeGreaterThanOrEqual(90);
          expect(m.title).toContain('CadenceE');
        }
      });
    }
  }

  it('differ between languages for the same page', () => {
    for (const id of MARKETING_PAGE_IDS) {
      const titles = SITE_LANGS.map((lang) => PAGE_META[lang][id].title);
      expect(new Set(titles).size, id).toBe(SITE_LANGS.length);
    }
  });

  it('mention the offer only while it runs', () => {
    for (const lang of SITE_LANGS) {
      for (const id of MARKETING_PAGE_IDS) {
        const after = pageMeta(lang, id, AFTER_OFFER);
        expect(`${after.title} ${after.description}`).not.toMatch(/2026/);
      }
    }
    expect(pageMeta('en', 'home', DURING_OFFER).description).toMatch(/Free until end of 2026/);
    expect(pageMeta('en', 'pricing', DURING_OFFER).title).toMatch(/Free Until 31 December 2026/);
  });

  it('cover the public pages too, uniquely', () => {
    const all = Object.values(PUBLIC_PAGE_META);
    expect(new Set(all.map((m) => m.title)).size).toBe(all.length);
    for (const m of all) expect(m.title.length).toBeLessThanOrEqual(60);
  });
});

describe('page content', () => {
  for (const lang of SITE_LANGS) {
    it(`exists for every page in ${lang}, with one distinct H1 each`, () => {
      const h1s: string[] = [];
      for (const page of MARKETING_PAGES) {
        const content = CONTENT[lang].pages[page.id];
        if (lang === 'en' && page.id === 'home') {
          expect(content).toBeUndefined(); // the animated Homepage
          continue;
        }
        expect(content, `${lang}/${page.id}`).toBeDefined();
        h1s.push(fullH1(content!));
      }
      expect(new Set(h1s).size).toBe(h1s.length);
    });

    it(`is substantial and well formed in ${lang}`, () => {
      for (const page of MARKETING_PAGES) {
        const content = CONTENT[lang].pages[page.id];
        if (!content) continue;
        const count = words(content);
        expect(count, `${lang}/${page.id}: ${count} words`).toBeGreaterThanOrEqual(600);
        expect(count, `${lang}/${page.id}: ${count} words`).toBeLessThanOrEqual(1400);
        expect(content.faq.items.length).toBeGreaterThanOrEqual(5);
        expect(content.faq.items.length).toBeLessThanOrEqual(8);
        expect(content.steps.items.length).toBeGreaterThanOrEqual(3);
        expect(content.features.items.length).toBeGreaterThanOrEqual(6);
        expect(content.related).not.toContain(page.id);
        for (const related of content.related) expect(MARKETING_PAGE_IDS).toContain(related);
      }
    });
  }

  it('says nothing it cannot back: no ratings, no customer counts, no certifications', () => {
    const all = JSON.stringify([en, fr, de, PAGE_META]);
    for (const claim of [/\d+(\.\d+)?\s*\/\s*5\b/, /\b\d{1,3}[,’']?\d{3}\+/, /SOC ?2/i, /ISO ?27001/i, /#1\b/, /\bstars?\b/i, /99\.9/]) {
      expect(all).not.toMatch(claim);
    }
  });

  it('writes German the Swiss way, without ß', () => {
    expect(JSON.stringify(de)).not.toContain('ß');
    expect(JSON.stringify(PAGE_META.de)).not.toContain('ß');
  });
});

// The guides: long enough to be worth reading and to rank, well formed, linked into the site,
// one distinct H1 each, and only claims the site can back.

import { describe, expect, it } from 'vitest';
import de from './content/de';
import en from './content/en';
import fr from './content/fr';
import type { GuideContent } from './content/guides/types';
import type { LanguageContent } from './content/types';
import { PAGE_META } from './meta';
import { GUIDE_PAGE_IDS, LANDING_PAGE_IDS, MARKETING_PAGES, MARKETING_PAGE_IDS, marketingPage } from './routes';
import { SITE_LANGS, type SiteLang } from './site';

const CONTENT: Record<SiteLang, LanguageContent> = { en, fr, de };

function texts(guide: GuideContent): string[] {
  const out: string[] = [];
  const walk = (value: unknown) => {
    if (typeof value === 'string') out.push(value);
    else if (Array.isArray(value)) value.forEach(walk);
    else if (value && typeof value === 'object') for (const [key, v] of Object.entries(value)) if (key !== 'id' && key !== 'readingMinutes') walk(v);
  };
  walk(guide);
  return out;
}

function wordCount(guide: GuideContent): number {
  return texts(guide).map((t) => t.replace(/\]\([^)]*\)/g, ']')).join(' ').split(/\s+/).filter((w) => /\p{L}/u.test(w)).length;
}

function links(guide: GuideContent): string[] {
  return texts(guide).flatMap((t) => [...t.matchAll(/\]\(([^)]+)\)/g)].map((m) => m[1] ?? ''));
}

describe('the guides', () => {
  it('are in the manifest, each with topics that are real pages', () => {
    expect(GUIDE_PAGE_IDS.length).toBe(8);
    for (const id of GUIDE_PAGE_IDS) {
      const page = marketingPage(id);
      expect(page.published, id).toMatch(/^\d{4}-\d{2}-\d{2}$/);
      expect(page.topics?.length, id).toBeGreaterThan(0);
      for (const topic of page.topics ?? []) expect(MARKETING_PAGE_IDS).toContain(topic);
    }
  });

  for (const lang of SITE_LANGS) {
    describe(`in ${lang}`, () => {
      for (const id of GUIDE_PAGE_IDS) {
        it(`${id} is written, substantial and well formed`, () => {
          const guide = CONTENT[lang].guides[id];
          expect(JSON.stringify(guide), 'still a placeholder').not.toMatch(/Placeholder/i);
          expect(JSON.stringify(PAGE_META[lang][id]), 'meta still a placeholder').not.toMatch(/Placeholder|\bguide-[a-z-]+ (en|fr|de)\b/i);
          const count = wordCount(guide);
          expect(count, `${id} has ${count} words`).toBeGreaterThanOrEqual(1000);
          expect(guide.keyTakeaways.length).toBeGreaterThanOrEqual(3);
          expect(guide.keyTakeaways.length).toBeLessThanOrEqual(6);
          expect(guide.sections.length).toBeGreaterThanOrEqual(5);
          const anchors = guide.sections.map((s) => s.id);
          expect(new Set(anchors).size).toBe(anchors.length);
          for (const anchor of anchors) expect(anchor).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
          expect(guide.faq.items.length).toBeGreaterThanOrEqual(4);
          // About 200 words a minute, give or take.
          expect(guide.readingMinutes).toBeGreaterThanOrEqual(Math.floor(count / 260));
          expect(guide.readingMinutes).toBeLessThanOrEqual(Math.ceil(count / 160));
        });

        it(`${id} links into the site: at least three pages, one of them a topic page, none unknown`, () => {
          const guide = CONTENT[lang].guides[id];
          const targets = links(guide);
          const pages = targets.filter((t) => t.startsWith('page:')).map((t) => t.slice(5));
          for (const page of pages) expect(MARKETING_PAGE_IDS, `unknown page in a link: ${page}`).toContain(page);
          expect(pages).not.toContain(id);
          expect(new Set(pages).size).toBeGreaterThanOrEqual(3);
          expect(pages.some((p) => (LANDING_PAGE_IDS as string[]).includes(p) || p === 'features' || p === 'pricing' || p === 'free-crm')).toBe(true);
          for (const t of targets.filter((t) => !t.startsWith('page:'))) expect(t, 'an address on the site').toMatch(/^\/[a-z0-9/?=&-]*$/);
        });
      }

      it('give every guide its own H1, distinct from every other page', () => {
        const h1s = [
          ...MARKETING_PAGES.flatMap((p) => {
            const c = CONTENT[lang].pages[p.id];
            return c ? [`${c.h1}${c.h1Accent ? ` ${c.h1Accent}` : ''}`] : [];
          }),
          ...GUIDE_PAGE_IDS.map((id) => CONTENT[lang].guides[id].h1),
        ];
        expect(new Set(h1s).size).toBe(h1s.length);
      });
    });
  }

  it('say nothing they cannot back: no ratings, no customer counts, no certifications', () => {
    const all = JSON.stringify([en.guides, fr.guides, de.guides]);
    for (const claim of [/\d+(\.\d+)?\s*\/\s*5\b/, /\b\d{1,3}[,’']?\d{3}\+/, /SOC ?2/i, /ISO ?27001/i, /#1\b/, /\bstars?\b/i, /99\.9/, /\bcertified\b/i]) {
      expect(all).not.toMatch(claim);
    }
  });

  it('write German the Swiss way, without ß', () => {
    expect(JSON.stringify(de.guides)).not.toContain('ß');
  });
});

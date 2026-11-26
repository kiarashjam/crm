// The free-CRM page exists for the launch offer: found, listed and linked while it runs
// (until 31 December 2026), noindex, unlisted and unlinked once it has ended, with words
// that say so. And the French and German home H1s say "free" only while the offer runs.

import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { MarketingFooter, footerProduct } from './components/MarketingFooter';
import de from './content/de';
import en from './content/en';
import fr from './content/fr';
import type { LanguageContent } from './content/types';
import { buildLlmsTxt } from './llms';
import { ogImageSpecs } from './og';
import MarketingPageDe from './pages/MarketingPageDe';
import MarketingPageEn from './pages/MarketingPageEn';
import MarketingPageFr from './pages/MarketingPageFr';
import { LANDING_PAGE_IDS, MARKETING_PAGES, marketingPage } from './routes';
import { OFFER_OG_PAGES, ROBOTS_INDEX, ROBOTS_NOINDEX_FOLLOW, headForPath } from './seo';
import { SITE_LANGS, type SiteLang } from './site';

const DURING = Date.parse('2026-12-31T22:30:00Z'); // 23:30 on 31 December in Zurich
const AFTER = Date.parse('2027-01-01T00:00:01+01:00');
const PATHS: Record<SiteLang, string> = { en: '/free-crm', fr: '/fr/crm-gratuit', de: '/de/kostenloses-crm' };
const PAGE = { en: MarketingPageEn, fr: MarketingPageFr, de: MarketingPageDe };
const CONTENT: Record<SiteLang, LanguageContent> = { en, fr, de };

const robots = (path: string, now: number) => headForPath(path, now).metas.find((m) => m.name === 'robots')?.content;
const ogImage = (path: string, now: number) => headForPath(path, now).metas.find((m) => m.property === 'og:image')?.content;

function at(when: number) {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(when);
}

afterEach(() => {
  vi.useRealTimers();
});

describe('the free-CRM page in the manifest and the head', () => {
  it('is an offer-only landing page, at its address in each language, kept out of the topic grids', () => {
    const page = marketingPage('free-crm');
    expect(page.kind).toBe('landing');
    expect(page.offerOnly).toBe(true);
    expect(page.paths).toEqual(PATHS);
    expect(LANDING_PAGE_IDS).not.toContain('free-crm');
    // The only page about the offer; every other landing page is still a topic.
    expect(MARKETING_PAGES.filter((p) => p.offerOnly).map((p) => p.id)).toEqual(['free-crm']);
    expect(LANDING_PAGE_IDS).toHaveLength(MARKETING_PAGES.filter((p) => p.kind === 'landing').length - 1);
  });

  it('is indexed while the offer runs and noindex, follow once it has ended', () => {
    for (const lang of SITE_LANGS) {
      expect(robots(PATHS[lang], DURING)).toBe(ROBOTS_INDEX);
      expect(robots(PATHS[lang], AFTER)).toBe(ROBOTS_NOINDEX_FOLLOW);
      expect(ROBOTS_NOINDEX_FOLLOW).toBe('noindex, follow');
      // Still its own canonical and hreflang set, so the redirect-free URL keeps working.
      expect(headForPath(PATHS[lang], AFTER).links.find((l) => l.rel === 'canonical')?.href).toBe(`https://www.cadencee.ai${PATHS[lang]}`);
    }
    // The topic pages stay indexed after the offer.
    expect(robots('/sales-crm', AFTER)).toBe(ROBOTS_INDEX);
    expect(robots('/pricing', AFTER)).toBe(ROBOTS_INDEX);
  });

  it('is titled for "free CRM" searches while the offer runs, and neutrally after', () => {
    expect(headForPath('/free-crm', DURING).title).toBe('Free CRM Software: Every Feature, No Credit Card | CadenceE');
    expect(headForPath('/fr/crm-gratuit', DURING).title).toBe('Logiciel CRM gratuit, tout inclus, sans carte | CadenceE');
    expect(headForPath('/de/kostenloses-crm', DURING).title).toBe('CRM-Software kostenlos: alle Funktionen inklusive | CadenceE');
    for (const lang of SITE_LANGS) {
      const head = headForPath(PATHS[lang], AFTER);
      expect(`${head.title} ${head.metas.find((m) => m.name === 'description')?.content}`).not.toMatch(/2026|credit card|carte|Kreditkarte/i);
      // The Offer data goes with the offer.
      expect(JSON.stringify(headForPath(PATHS[lang], DURING).jsonLd)).toContain('"Offer"');
      expect(JSON.stringify(head.jsonLd)).not.toContain('"Offer"');
    }
  });

  it('shows the offer image while the offer runs, the plain one after, and both images say the right words', () => {
    expect(ogImage('/free-crm', DURING)).toBe('https://www.cadencee.ai/og/free-crm-en-offer.png');
    expect(ogImage('/de/kostenloses-crm', AFTER)).toBe('https://www.cadencee.ai/og/free-crm-de.png');
    const specs = ogImageSpecs();
    for (const lang of SITE_LANGS) {
      const words = CONTENT[lang].pages['free-crm']!;
      const plain = specs.find((s) => s.path === `/og/free-crm-${lang}.png`);
      const offer = specs.find((s) => s.path === `/og/free-crm-${lang}-offer.png`);
      expect(plain).toMatchObject({ title: words.afterOffer?.h1, accent: words.afterOffer?.h1Accent });
      expect(plain?.badge).toBeUndefined();
      expect(offer).toMatchObject({ title: words.h1, accent: words.h1Accent });
      expect(offer?.badge).toMatch(/2026/);
    }
  });

  it('draws an offer image for every page whose words change when the offer ends', () => {
    for (const lang of SITE_LANGS) {
      for (const [id, words] of Object.entries(CONTENT[lang].pages)) {
        if (words?.afterOffer) expect(OFFER_OG_PAGES as readonly string[], `${lang}/${id}`).toContain(id);
      }
    }
  });

  it('is in llms.txt only while the offer runs', () => {
    const during = buildLlmsTxt(DURING);
    expect(during).toContain('(https://www.cadencee.ai/free-crm): Free CRM software');
    expect(during).toContain('https://www.cadencee.ai/fr/crm-gratuit');
    expect(during).toContain('https://www.cadencee.ai/de/kostenloses-crm');
    expect(buildLlmsTxt(AFTER)).not.toMatch(/free-crm|crm-gratuit|kostenloses-crm/);
  });
});

describe('the free-CRM page as people see it', () => {
  it('says "free CRM" in its H1 while the offer runs, links the demo, and swaps in its after-offer words from 2027', () => {
    const DURING_H1: Record<SiteLang, RegExp> = { en: /^Free CRM software/, fr: /^Un CRM gratuit/, de: /^Kostenloses CRM/ };
    const AFTER_H1: Record<SiteLang, RegExp> = { en: /launch offer has ended$/, fr: /est terminée$/, de: /ist beendet$/ };
    for (const lang of SITE_LANGS) {
      const Page = PAGE[lang];
      at(DURING);
      const during = render(
        <MemoryRouter initialEntries={[PATHS[lang]]}>
          <Page id="free-crm" />
        </MemoryRouter>,
      );
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(screen.getByRole('heading', { level: 1 }).textContent).toMatch(DURING_H1[lang]);
      const demo = during.container.querySelector('a[href="/login?demo=1"]');
      expect(demo?.getAttribute('data-track')).toBe('cta-try');
      expect(during.container.querySelector('script[type="application/ld+json"]')?.textContent).toContain('FAQPage');
      during.unmount();

      at(AFTER);
      const after = render(
        <MemoryRouter initialEntries={[PATHS[lang]]}>
          <Page id="free-crm" />
        </MemoryRouter>,
      );
      expect(screen.getByRole('heading', { level: 1 }).textContent).toMatch(AFTER_H1[lang]);
      expect(after.container.textContent).not.toMatch(/not a cut-down|pas une version bridée|keine abgespeckte|Start free|Commencer gratuitement|Kostenlos starten/);
      after.unmount();
    }
  });

  it('is linked from the footer and the pricing page only while the offer runs', () => {
    expect(footerProduct(true)).toEqual(['features', 'pricing', 'free-crm', 'ai', 'e-signature', 'email', 'billing']);
    expect(footerProduct(false)).not.toContain('free-crm');

    for (const lang of SITE_LANGS) {
      at(DURING);
      const footer = render(
        <MemoryRouter>
          <MarketingFooter lang={lang} />
        </MemoryRouter>,
      );
      const link = footer.container.querySelector(`a[href="${PATHS[lang]}"]`);
      expect(link?.getAttribute('data-track')).toBe('nav-free-crm');
      expect(link?.closest('[data-launch-offer]')).not.toBeNull();
      footer.unmount();

      const Page = PAGE[lang];
      const pricing = render(
        <MemoryRouter>
          <Page id="pricing" />
        </MemoryRouter>,
      );
      // Its card and a FAQ answer; the card shows the offer's description, not the after-offer one.
      expect(pricing.container.querySelectorAll(`main a[href="${PATHS[lang]}"]`).length).toBeGreaterThanOrEqual(2);
      expect(pricing.container.textContent).toMatch(/Free until end of 2026|Gratuit jusqu’à fin 2026|Kostenlos bis Ende 2026/);
      pricing.unmount();

      at(AFTER);
      const footerAfter = render(
        <MemoryRouter>
          <MarketingFooter lang={lang} />
        </MemoryRouter>,
      );
      expect(footerAfter.container.querySelector(`a[href="${PATHS[lang]}"]`)).toBeNull();
      footerAfter.unmount();
      const pricingAfter = render(
        <MemoryRouter>
          <Page id="pricing" />
        </MemoryRouter>,
      );
      expect(pricingAfter.container.querySelector(`a[href="${PATHS[lang]}"]`)).toBeNull();
      pricingAfter.unmount();
    }
  });
});

describe('the French and German home pages', () => {
  it('say the CRM is free in their H1 while the offer runs, and not after', () => {
    const FREE: Record<'fr' | 'de', RegExp> = { fr: /gratuit/i, de: /kostenlos/i };
    for (const lang of ['fr', 'de'] as const) {
      const Page = PAGE[lang];
      at(DURING);
      const during = render(
        <MemoryRouter initialEntries={[lang === 'fr' ? '/fr' : '/de']}>
          <Page id="home" />
        </MemoryRouter>,
      );
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      expect(screen.getByRole('heading', { level: 1 }).textContent).toMatch(FREE[lang]);
      during.unmount();

      at(AFTER);
      const after = render(
        <MemoryRouter initialEntries={[lang === 'fr' ? '/fr' : '/de']}>
          <Page id="home" />
        </MemoryRouter>,
      );
      expect(screen.getAllByRole('heading', { level: 1 })).toHaveLength(1);
      const h1 = screen.getByRole('heading', { level: 1 }).textContent;
      expect(h1).not.toMatch(FREE[lang]);
      expect(h1).toBe(lang === 'fr' ? 'Le CRM suisse qui gère votre club et vos ventes' : 'Das Schweizer CRM für Ihren Club und Ihren Vertrieb');
      after.unmount();
    }
  });

  it('draw the free H1 on their offer OG image and the plain one on the other', () => {
    const specs = ogImageSpecs();
    expect(specs.find((s) => s.path === '/og/home-fr-offer.png')?.title).toMatch(/gratuit/);
    expect(specs.find((s) => s.path === '/og/home-fr.png')?.title).not.toMatch(/gratuit/);
    expect(specs.find((s) => s.path === '/og/home-de-offer.png')?.title).toMatch(/kostenlose/);
    expect(specs.find((s) => s.path === '/og/home-de.png')?.title).not.toMatch(/kostenlos/);
    expect(specs.find((s) => s.path === '/og/home-en-offer.png')?.accent).toBe('One free Swiss CRM.');
    expect(specs.find((s) => s.path === '/og/home-en.png')?.accent).toBe('One Swiss CRM.');
  });
});

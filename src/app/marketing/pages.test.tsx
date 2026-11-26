// Every public page rendered as the prerender renders it: one H1, its FAQ data, tagged calls
// to action, and no internal link to an address that is not a page.

import { describe, expect, it } from 'vitest';
import { renderToString } from 'react-dom/server';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, StaticRouter } from 'react-router-dom';
import Homepage from '@/app/pages/Homepage';
import MarketingPageDe from './pages/MarketingPageDe';
import MarketingPageEn from './pages/MarketingPageEn';
import MarketingPageFr from './pages/MarketingPageFr';
import { NotFound } from './NotFound';
import { MARKETING_PAGES, PUBLIC_PAGES, matchMarketingPath } from './routes';
import { SITE_LANGS } from './site';

const BY_LANG = { en: MarketingPageEn, fr: MarketingPageFr, de: MarketingPageDe };

function renderPath(path: string): string {
  const match = matchMarketingPath(path)!;
  const Page = BY_LANG[match.lang];
  const element = match.page.id === 'home' && match.lang === 'en' ? <Homepage /> : <Page id={match.page.id} />;
  return renderToString(<StaticRouter location={path}>{element}</StaticRouter>);
}

const PAGES = MARKETING_PAGES.flatMap((page) => SITE_LANGS.map((lang) => page.paths[lang]));
const KNOWN = new Set([...PAGES, ...PUBLIC_PAGES.map((p) => p.path)]);

describe('rendered public pages', () => {
  const rendered = PAGES.map((path) => [path, renderPath(path)] as const);

  it('each have exactly one H1', () => {
    for (const [path, html] of rendered) expect(html.match(/<h1[\s>]/g)?.length, path).toBe(1);
  });

  it('link only to pages that exist', () => {
    for (const [path, html] of rendered) {
      for (const [, href] of html.matchAll(/href="([^"]+)"/g)) {
        if (!href!.startsWith('/')) {
          expect(href, `${path} → ${href}`).toMatch(/^(https:\/\/|mailto:|#)/);
          continue;
        }
        const target = href!.replace(/&amp;/g, '&').split(/[?#]/)[0]!;
        expect(KNOWN.has(target), `${path} links to ${href}`).toBe(true);
      }
    }
  });

  it('carry the FAQPage structured data of their questions', () => {
    for (const [path, html] of rendered) {
      const blocks = [...html.matchAll(/<script type="application\/ld\+json">([\s\S]*?)<\/script>/g)].map((m) => JSON.parse(m[1]!));
      const faq = blocks.find((b) => b['@type'] === 'FAQPage');
      expect(faq, path).toBeDefined();
      expect(faq.mainEntity.length).toBeGreaterThanOrEqual(5);
    }
  });

  it('tag their calls to action and navigation for the click counts', () => {
    for (const [path, html] of rendered) {
      expect(html, path).toContain('data-track="cta-signup"');
      expect(html, path).toContain('data-track="cta-login"');
      expect(html, path).toContain('data-track="cta-try"');
      expect(html, path).toContain('href="/login?demo=1"');
      expect(html, path).toContain('data-track="cta-contact"');
      expect(html, path).toContain('data-track="nav-features"');
      expect(html, path).toContain('data-track="nav-pricing"');
    }
  });

  it('link every language version from the language switch', () => {
    const html = renderPath('/fr/crm-suisse');
    expect(html).toContain('href="/swiss-crm"');
    expect(html).toContain('href="/de/schweizer-crm"');
    expect(html).toMatch(/hreflang="de"/i);
  });

  it('render the 404 page in the address language, noindex', () => {
    render(
      <MemoryRouter initialEntries={['/de/gibt-es-nicht']}>
        <NotFound />
      </MemoryRouter>,
    );
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Diese Seite gibt es nicht');
    expect(document.title).toBe('Seite nicht gefunden | CadenceE');
    expect(document.querySelector('meta[name="robots"]')?.getAttribute('content')).toMatch(/^noindex/);
  });
});

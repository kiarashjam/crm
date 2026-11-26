import { describe, expect, it, beforeEach } from 'vitest';
import { render } from '@testing-library/react';
import { MemoryRouter, useNavigate } from 'react-router-dom';
import { act } from 'react';
import { applyHead, headForPath, notFoundHead, renderHeadHtml, ROBOTS_INDEX, ROBOTS_NOINDEX, type HeadData } from './seo';
import { SeoHead } from './SeoHead';
import { MARKETING_PAGES, matchMarketingPath, normalizePath, langOfPath } from './routes';
import { SITE_LANGS } from './site';

const BEFORE_END = Date.parse('2026-11-25T12:00:00Z');
const AFTER_END = Date.parse('2027-01-02T12:00:00Z');

const meta = (head: HeadData, key: string) =>
  head.metas.find((m) => m.name === key || m.property === key)?.content;
const link = (head: HeadData, rel: string) => head.links.find((l) => l.rel === rel && !l.hreflang)?.href;

describe('headForPath', () => {
  it('gives the home page its title, a canonical on www with a trailing slash, and index', () => {
    const head = headForPath('/', BEFORE_END);
    expect(head.kind).toBe('marketing');
    // "Free" leads the title while the launch offer runs, and is gone after it.
    expect(head.title).toBe('Free Swiss CRM for Clubs and Sales Teams | CadenceE');
    expect(headForPath('/', AFTER_END).title).toBe('CadenceE – Swiss CRM for Private Clubs and Sales Teams');
    expect(headForPath('/fr', BEFORE_END).title).toBe('CRM gratuit pour clubs et équipes de vente | CadenceE');
    expect(headForPath('/de', BEFORE_END).title).toBe('Kostenloses CRM für Clubs und Vertrieb | CadenceE');
    expect(link(head, 'canonical')).toBe('https://www.cadencee.ai/');
    expect(meta(head, 'robots')).toBe(ROBOTS_INDEX);
    expect(head.lang).toBe('en');
  });

  it('gives a landing page its own title, canonical without query or trailing slash, and hreflang', () => {
    const head = headForPath('/crm-for-private-clubs/?utm_source=x', BEFORE_END);
    expect(head.title).toBe("CRM for Private Members' Clubs | CadenceE");
    expect(link(head, 'canonical')).toBe('https://www.cadencee.ai/crm-for-private-clubs');
    const alternates = head.links.filter((l) => l.hreflang).map((l) => [l.hreflang, l.href]);
    expect(alternates).toEqual([
      ['en', 'https://www.cadencee.ai/crm-for-private-clubs'],
      ['fr', 'https://www.cadencee.ai/fr/crm-club-prive'],
      ['de', 'https://www.cadencee.ai/de/crm-fuer-privatclubs'],
      ['x-default', 'https://www.cadencee.ai/crm-for-private-clubs'],
    ]);
  });

  it('writes French and German pages in their language, with an absolute og:image', () => {
    const fr = headForPath('/fr/crm-suisse', BEFORE_END);
    expect(fr.lang).toBe('fr');
    expect(fr.title).toMatch(/CRM suisse/);
    expect(meta(fr, 'og:locale')).toBe('fr_CH');
    expect(meta(fr, 'og:image')).toBe('https://www.cadencee.ai/og/swiss-fr.png');
    const de = headForPath('/de/schweizer-crm', BEFORE_END);
    expect(de.lang).toBe('de');
    expect(meta(de, 'twitter:card')).toBe('summary_large_image');
  });

  it('indexes /login but not the password and email links, which keep no canonical', () => {
    const login = headForPath('/login');
    expect(login.kind).toBe('public');
    expect(meta(login, 'robots')).toBe(ROBOTS_INDEX);
    expect(link(login, 'canonical')).toBe('https://www.cadencee.ai/login');
    for (const path of ['/forgot-password', '/reset-password', '/verify-email']) {
      const head = headForPath(path);
      expect(meta(head, 'robots')).toMatch(/^noindex/);
      expect(link(head, 'canonical')).toBeUndefined();
    }
  });

  it('marks every app route noindex and leaves its title to the page', () => {
    for (const path of ['/dashboard', '/leads', '/leads/42', '/settings', '/sign/abc', '/pay/xyz', '/member-portal/p46']) {
      const head = headForPath(path);
      expect(head.kind).toBe('app');
      expect(head.title).toBeNull();
      expect(meta(head, 'robots')).toBe(ROBOTS_NOINDEX);
      expect(head.links).toEqual([]);
    }
  });

  it('gives the 404 page noindex in the address language', () => {
    expect(meta(notFoundHead('de'), 'robots')).toMatch(/^noindex/);
    expect(notFoundHead('de').title).toMatch(/nicht gefunden/);
    expect(langOfPath('/fr/nope')).toBe('fr');
    expect(langOfPath('/french')).toBe('en');
  });

  it('knows every manifest path, with or without a trailing slash', () => {
    for (const page of MARKETING_PAGES) {
      for (const lang of SITE_LANGS) {
        expect(matchMarketingPath(page.paths[lang])?.page.id).toBe(page.id);
        expect(matchMarketingPath(`${page.paths[lang]}/`)?.lang).toBe(lang);
      }
    }
    expect(normalizePath('//features//')).toBe('/features');
  });
});

describe('renderHeadHtml', () => {
  it('escapes attribute values and marks every tag as managed', () => {
    const html = renderHeadHtml({
      kind: 'public',
      title: 'A <b> & "c"',
      lang: null,
      metas: [{ name: 'description', content: 'x "y" <z>' }],
      links: [{ rel: 'canonical', href: 'https://www.cadencee.ai/a?b=1&c=2' }],
      jsonLd: [{ '@context': 'https://schema.org', name: '</script><script>alert(1)</script>' }],
    });
    expect(html).toContain('<title>A &lt;b&gt; &amp; &quot;c&quot;</title>');
    expect(html).toContain('content="x &quot;y&quot; &lt;z&gt;" data-head');
    expect(html).toContain('href="https://www.cadencee.ai/a?b=1&amp;c=2"');
    expect(html).not.toContain('</script><script>');
  });
});

describe('applyHead', () => {
  beforeEach(() => {
    document.head.innerHTML = '<meta name="description" content="old" data-head /><link rel="canonical" href="https://old" data-head /><meta name="keep" content="me" />';
    document.title = 'Old';
  });

  it('replaces the managed tags, sets title and lang, and keeps the rest', () => {
    applyHead(document, headForPath('/features', BEFORE_END));
    expect(document.title).toBe('CadenceE Features: Leads, Members, Billing, Email and AI');
    expect(document.documentElement.lang).toBe('en');
    expect(document.querySelectorAll('link[rel="canonical"]')).toHaveLength(1);
    expect(document.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe('https://www.cadencee.ai/features');
    expect(document.querySelectorAll('meta[name="description"]')).toHaveLength(1);
    expect(document.querySelector('meta[name="keep"]')).not.toBeNull();
    expect(document.querySelectorAll('link[hreflang]')).toHaveLength(4);
    const ld = [...document.querySelectorAll('script[type="application/ld+json"]')].map((s) => JSON.parse(s.textContent ?? ''));
    expect(ld.length).toBeGreaterThan(0);
  });

  it('on an app route leaves the title and adds noindex only', () => {
    applyHead(document, headForPath('/leads'));
    expect(document.title).toBe('Old');
    expect(document.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(ROBOTS_NOINDEX);
    expect(document.querySelector('link[rel="canonical"]')).toBeNull();
    expect(document.querySelector('meta[name="description"]')).toBeNull();
  });
});

describe('SeoHead', () => {
  function GoTo({ to }: { to: string }) {
    const navigate = useNavigate();
    (globalThis as { __goto?: () => void }).__goto = () => navigate(to);
    return null;
  }

  it('follows the route: marketing head, then the app title back when leaving for the app', async () => {
    render(
      <MemoryRouter initialEntries={['/sales-crm']}>
        <SeoHead />
        <GoTo to="/dashboard" />
      </MemoryRouter>,
    );
    expect(document.title).toBe('Sales CRM with Lead Tracking and AI Sales Writer | CadenceE');
    expect(document.querySelector('link[rel="canonical"]')?.getAttribute('href')).toBe('https://www.cadencee.ai/sales-crm');
    await act(async () => {
      (globalThis as { __goto?: () => void }).__goto?.();
    });
    expect(document.title).toBe('CadenceE');
    expect(document.querySelector('meta[name="robots"]')?.getAttribute('content')).toBe(ROBOTS_NOINDEX);
    expect(document.querySelector('link[rel="canonical"]')).toBeNull();
  });
});

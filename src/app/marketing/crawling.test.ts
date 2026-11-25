// robots.txt and the Static Web Apps routing, checked against the route manifest and
// App.tsx: no public page closed to crawlers or caught by an app rewrite, every app route
// served by the SPA shell on a deep link or a reload.

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { spaRouteSegments, staticWebAppConfig, type SwaConfig } from '../../../scripts/seo/swa-config.mjs';
import { MARKETING_PAGES, PUBLIC_PAGES, prerenderPaths } from './routes';
import { SITE_LANGS } from './site';

const root = process.cwd();
const robots = readFileSync(path.join(root, 'public/robots.txt'), 'utf8');
const appSource = readFileSync(path.join(root, 'src/app/App.tsx'), 'utf8');
const baseConfig = JSON.parse(readFileSync(path.join(root, 'public/staticwebapp.config.json'), 'utf8')) as SwaConfig;

const publicPaths = [
  ...MARKETING_PAGES.flatMap((p) => SITE_LANGS.map((l) => p.paths[l])),
  ...PUBLIC_PAGES.map((p) => p.path),
];

/** The robots.txt rules that apply to every crawler, as Google and Bing read them ("*" and "$"). */
function disallows(): RegExp[] {
  return robots
    .split('\n')
    .map((line) => line.trim())
    .filter((line) => line.startsWith('Disallow:'))
    .map((line) => line.slice('Disallow:'.length).trim())
    .filter(Boolean)
    .map((rule) => {
      const anchored = rule.endsWith('$');
      const body = (anchored ? rule.slice(0, -1) : rule).replace(/[.+?^{}()|[\]\\]/g, '\\$&').replace(/\*/g, '.*');
      return new RegExp(`^${body}${anchored ? '$' : ''}`);
    });
}

const blocked = (url: string) => disallows().some((rule) => rule.test(url));

/** Static Web Apps route matching: exact, or "/x/*" for everything below /x. */
function matches(route: string, url: string): boolean {
  if (route.endsWith('/*')) return url.startsWith(route.slice(0, -1));
  if (route.endsWith('*')) return url.startsWith(route.slice(0, -1));
  return url === route;
}

describe('robots.txt', () => {
  it('leaves every public page open to crawlers', () => {
    for (const p of publicPaths) expect(blocked(p), p).toBe(false);
    for (const p of ['/', '/assets/index-abc.js', '/og/home-en.png', '/sitemap.xml', '/llms.txt']) expect(blocked(p), p).toBe(false);
  });

  it('closes the API and the app', () => {
    for (const p of ['/api/leads', '/dashboard', '/leads', '/leads/12', '/members', '/members/3', '/settings', '/sign/abc', '/pay/x', '/app.html']) {
      expect(blocked(p), p).toBe(true);
    }
  });

  it('names the sitemap on the canonical host', () => {
    expect(robots).toMatch(/^Sitemap: https:\/\/www\.cadencee\.ai\/sitemap\.xml$/m);
    expect(robots).toMatch(/^User-agent: \*$/m);
  });
});

describe('Static Web Apps routing', () => {
  const segments = spaRouteSegments(appSource, prerenderPaths());
  const config = staticWebAppConfig(baseConfig, segments, prerenderPaths());
  const rewrites = (config.routes ?? []).filter((r) => r.rewrite === '/app.html');
  const pageRewrites = (config.routes ?? []).filter((r) => r.rewrite?.endsWith('/index.html'));
  const appPaths = [...appSource.matchAll(/\bpath="([^"]+)"/g)].map((m) => m[1]!).filter((p) => p !== '*' && p !== '/');

  it('serves every app route from App.tsx with the SPA shell, on a deep link or a reload', () => {
    const prerendered = new Set(prerenderPaths());
    for (const route of appPaths) {
      const url = route.replace(/:[^/]+/g, 'x1');
      if (prerendered.has(url)) continue; // a prerendered page (/login, /help, …) is a file
      const rule = rewrites.find((r) => matches(r.route, url));
      expect(rule?.rewrite, url).toBe('/app.html');
    }
  });

  it('never sends a public page to the app shell: each is its own prerendered file, at its address', () => {
    for (const p of [...publicPaths, '/', '/robots.txt', '/sitemap.xml']) {
      expect(rewrites.find((r) => matches(r.route, p))?.route, p).toBeUndefined();
    }
    for (const p of prerenderPaths().filter((x) => x !== '/')) {
      const first = (config.routes ?? []).find((r) => matches(r.route, p));
      expect(first?.rewrite, p).toBe(`${p}/index.html`);
    }
    expect(pageRewrites.length).toBe(prerenderPaths().length - 1);
  });

  it('answers unknown addresses with a real 404 and the 404 page, with no fallback left', () => {
    expect(config.navigationFallback).toBeUndefined();
    expect(config.responseOverrides?.['404']).toEqual({ rewrite: '/404.html', statusCode: 404 });
    for (const p of ['/pricingx', '/membersx', '/fr/inconnu', '/foo/bar']) {
      expect(rewrites.find((r) => matches(r.route, p))?.route, p).toBeUndefined();
    }
  });

  it('keeps the committed config working without the prerender: a fallback that skips files', () => {
    expect(baseConfig.navigationFallback?.rewrite).toBe('/index.html');
    const exclude = baseConfig.navigationFallback?.exclude?.join(' ') ?? '';
    for (const ext of ['txt', 'xml', 'png', 'js', 'css']) expect(exclude).toContain(ext);
    expect(exclude).toContain('/og/*');
  });

  it('marks app routes noindex and lets only the embeddable forms be framed', () => {
    for (const rule of rewrites) expect(rule.headers?.['X-Robots-Tag']).toBe('noindex, nofollow');
    const capture = rewrites.find((r) => r.route === '/capture/*');
    expect(capture?.headers?.['Content-Security-Policy']).not.toContain('frame-ancestors');
    expect(baseConfig.globalHeaders?.['Content-Security-Policy']).toContain("frame-ancestors 'self'");
  });

  it('caches hashed assets for a year and revalidates pages', () => {
    const assets = (config.routes ?? []).find((r) => r.route === '/assets/*');
    expect(assets?.headers?.['Cache-Control']).toContain('immutable');
    expect(baseConfig.globalHeaders?.['Cache-Control']).toBe('no-cache');
  });
});

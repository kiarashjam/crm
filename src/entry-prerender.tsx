// The build-time renderer of the public site (scripts/seo/prerender.mjs builds this with
// Vite's SSR mode and calls it once per page). It renders the same components the app
// does, with React's renderToString, so the static HTML crawlers and link previews read is
// the page itself; in the browser the app then takes over from main.tsx.
//
// Only the marketing pages are rendered here. They import nothing that needs a browser at
// module level, which marketing/prerender.test.tsx keeps true.

import type { ReactElement } from 'react';
import { renderToString } from 'react-dom/server';
import { StaticRouter } from 'react-router-dom';
import SkipLink from '@/app/components/SkipLink';
import { NotFound } from '@/app/marketing/NotFound';
import MarketingPageDe from '@/app/marketing/pages/MarketingPageDe';
import MarketingPageEn from '@/app/marketing/pages/MarketingPageEn';
import MarketingPageFr from '@/app/marketing/pages/MarketingPageFr';
import { matchMarketingPath, normalizePath, prerenderPaths, publicPage } from '@/app/marketing/routes';
import { appHead, headForPath, notFoundHead, renderHeadHtml, type HeadData } from '@/app/marketing/seo';
import { buildLlmsTxt } from '@/app/marketing/llms';
import { ogImageSpecs } from '@/app/marketing/og';
import { buildSitemapXml } from '@/app/marketing/sitemap';
import type { SiteLang } from '@/app/marketing/site';
import Homepage from '@/app/pages/Homepage';

export { prerenderPaths, buildSitemapXml, buildLlmsTxt, ogImageSpecs };

const BY_LANG = { en: MarketingPageEn, fr: MarketingPageFr, de: MarketingPageDe } as const;

/** The source module of each public page, for <link rel="modulepreload"> of its chunk. */
const MODULE_OF_LANG: Record<SiteLang, string> = {
  en: 'src/app/marketing/pages/MarketingPageEn.tsx',
  fr: 'src/app/marketing/pages/MarketingPageFr.tsx',
  de: 'src/app/marketing/pages/MarketingPageDe.tsx',
};

const PUBLIC_MODULES: Record<string, string> = {
  '/login': 'src/app/pages/Login.tsx',
  '/forgot-password': 'src/app/pages/ForgotPassword.tsx',
  '/reset-password': 'src/app/pages/ResetPassword.tsx',
};

export interface RenderedPage {
  path: string;
  /** <html lang>. */
  lang: string;
  /** The head between the seo:head markers. */
  headHtml: string;
  /** What goes inside <div id="root">; empty where the app renders the page itself. */
  bodyHtml: string;
  /** The source module whose chunk the page needs first, if any. */
  module: string | null;
  head: HeadData;
}

function render(path: string, element: ReactElement): string {
  return renderToString(
    <StaticRouter location={path}>
      <SkipLink />
      {element}
    </StaticRouter>,
  );
}

/** One public page at its path, as of `now` (the launch offer shows only before 2027). */
export function renderPage(rawPath: string, now: number = Date.now()): RenderedPage {
  const path = normalizePath(rawPath);
  const head = headForPath(path, now);
  const match = matchMarketingPath(path);
  if (match) {
    const isHome = match.page.id === 'home' && match.lang === 'en';
    const Page = BY_LANG[match.lang];
    const element = isHome ? <Homepage /> : <Page id={match.page.id} />;
    return {
      path,
      lang: match.lang,
      headHtml: renderHeadHtml(head),
      bodyHtml: render(path, element),
      module: isHome ? 'src/app/pages/Homepage.tsx' : MODULE_OF_LANG[match.lang],
      head,
    };
  }
  if (publicPage(path)) {
    return { path, lang: 'en', headHtml: renderHeadHtml(head), bodyHtml: '', module: PUBLIC_MODULES[path] ?? null, head };
  }
  throw new Error(`Not a public page: ${path}`);
}

/** The page every unknown address answers with, under a 404 status. */
export function renderNotFound(): RenderedPage {
  const head = notFoundHead('en');
  return {
    path: '/404',
    lang: 'en',
    headHtml: renderHeadHtml(head),
    bodyHtml: render('/404', <NotFound lang="en" />),
    module: null,
    head,
  };
}

/** The app's own pages' shell (app.html): no content, never indexed. */
export function renderAppShell(): RenderedPage {
  const head = appHead();
  return { path: '/app.html', lang: 'en', headHtml: renderHeadHtml(head), bodyHtml: '', module: null, head };
}

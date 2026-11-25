// The public pages: split out of the main bundle, and fetched before the app first renders
// when the address is one of them (main.tsx), so the app replaces the prerendered HTML with
// the same page at once instead of a loading screen.

import { Suspense, useEffect } from 'react';
import { Route } from 'react-router-dom';
import { PageLoader } from '@/app/components/PageLoader';
import { lazyWithPreload } from '@/app/lib/lazyWithPreload';
import { MARKETING_PAGES, matchMarketingPath, normalizePath } from '@/app/marketing/routes';
import { SITE_LANGS, type SiteLang } from '@/app/marketing/site';

export const Homepage = lazyWithPreload(() => import('@/app/pages/Homepage'));
export const Login = lazyWithPreload(() => import('@/app/pages/Login'));
export const ForgotPassword = lazyWithPreload(() => import('@/app/pages/ForgotPassword'));
export const ResetPassword = lazyWithPreload(() => import('@/app/pages/ResetPassword'));

/** One chunk per language: its words and the page template. */
const MARKETING_PAGE_BY_LANG = {
  en: lazyWithPreload(() => import('@/app/marketing/pages/MarketingPageEn')),
  fr: lazyWithPreload(() => import('@/app/marketing/pages/MarketingPageFr')),
  de: lazyWithPreload(() => import('@/app/marketing/pages/MarketingPageDe')),
} satisfies Record<SiteLang, unknown>;

/** The marketing pages' routes, from the route manifest. "/" is the Homepage's own route. */
export function marketingRoutes() {
  const routes = [];
  for (const page of MARKETING_PAGES) {
    for (const lang of SITE_LANGS) {
      if (page.id === 'home' && lang === 'en') continue;
      const Page = MARKETING_PAGE_BY_LANG[lang];
      routes.push(
        <Route
          key={`${page.id}-${lang}`}
          path={page.paths[lang]}
          element={
            <Suspense fallback={<PageLoader />}>
              <Page id={page.id} />
            </Suspense>
          }
        />,
      );
    }
  }
  return routes;
}

/** Fetches the code of the public page at a path, if it is one; resolves at once otherwise. */
export function preloadRoute(pathname: string): Promise<unknown> {
  const match = matchMarketingPath(pathname);
  if (match) {
    return match.page.id === 'home' && match.lang === 'en'
      ? Homepage.preload()
      : MARKETING_PAGE_BY_LANG[match.lang].preload();
  }
  switch (normalizePath(pathname)) {
    case '/login':
      return Login.preload();
    case '/forgot-password':
      return ForgotPassword.preload();
    case '/reset-password':
      return ResetPassword.preload();
    default:
      return Promise.resolve();
  }
}

/**
 * Fetches a public page's code as soon as someone shows the intent to open it (the pointer
 * over a link, focus on it, a finger on it), so the click lands on a page that is ready.
 * One listener for the whole document; renders nothing.
 */
export function LinkPrefetch() {
  useEffect(() => {
    const onIntent = (event: Event) => {
      const link = (event.target as Element | null)?.closest?.('a[href^="/"]') as HTMLAnchorElement | null;
      if (link && link.origin === window.location.origin) void preloadRoute(link.pathname).catch(() => undefined);
    };
    const options = { passive: true, capture: true } as const;
    document.addEventListener('pointerover', onIntent, options);
    document.addEventListener('focusin', onIntent, options);
    document.addEventListener('touchstart', onIntent, options);
    return () => {
      document.removeEventListener('pointerover', onIntent, options);
      document.removeEventListener('focusin', onIntent, options);
      document.removeEventListener('touchstart', onIntent, options);
    };
  }, []);
  return null;
}

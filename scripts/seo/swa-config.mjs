// Azure Static Web Apps routing for the prerendered site.
//
// public/staticwebapp.config.json (what `vite build` alone ships) keeps a navigationFallback:
// every unknown address gets the SPA, as before, so a build without the prerender still
// works. The prerender replaces it with explicit routes: each top-level segment of the
// app's routes (read from src/app/App.tsx, so a new route is covered without anyone
// remembering) is rewritten to the SPA shell, the prerendered pages are served as files,
// and anything else is a real 404 with the prerendered 404 page.
//
// Each segment gets two exact rules, "/leads" and "/leads/*", never a prefix such as
// "/members*", which would also catch "/membership-management-software".

/** Segments whose public forms a club may embed in its own website: framing allowed there. */
export const EMBEDDABLE_SEGMENTS = ['capture', 'apply', 'member-portal'];

/** The SPA shell the app's routes are rewritten to. */
export const APP_SHELL = '/app.html';

/**
 * The top-level segments of the app's routes, as SWA route rules need them: route paths
 * are read from App.tsx (path="/leads/:id" → "leads"); a segment whose own page is
 * prerendered (/login, /help, …) needs only its sub-paths, if it has any.
 */
export function spaRouteSegments(appSource, prerenderedPaths) {
  const prerendered = new Set(prerenderedPaths);
  const paths = [...appSource.matchAll(/\bpath="([^"]+)"/g)].map((m) => m[1]).filter((p) => p.startsWith('/') && p !== '/');
  const bySegment = new Map();
  for (const p of paths) {
    const segment = p.split('/')[1];
    if (!segment || segment.startsWith(':') || segment === '*') continue;
    const entry = bySegment.get(segment) ?? { exact: false, deeper: false };
    if (p === `/${segment}`) entry.exact = true;
    else entry.deeper = true;
    bySegment.set(segment, entry);
  }
  const rules = [];
  for (const [segment, { exact, deeper }] of [...bySegment.entries()].sort(([a], [b]) => a.localeCompare(b))) {
    const needsExact = exact && !prerendered.has(`/${segment}`);
    if (needsExact || deeper) rules.push({ segment, exact: needsExact, deeper });
  }
  return rules;
}

function frameableHeaders(globalHeaders) {
  const csp = globalHeaders?.['Content-Security-Policy'];
  if (!csp) return undefined;
  const withoutFrameAncestors = csp
    .split(';')
    .map((part) => part.trim())
    .filter((part) => part && !part.startsWith('frame-ancestors'))
    .join('; ');
  return { 'Content-Security-Policy': withoutFrameAncestors };
}

/**
 * The deployed config: the committed one without the fallback, each prerendered page at its
 * own address, the app's routes on the shell, and a real 404 for everything else.
 *
 * Each prerendered page is an explicit rewrite to its file (/features → /features/index.html),
 * so it is served at the address without a slash whatever the server does with folders:
 * the emulator answers a folder with a redirect to its trailing slash.
 */
export function staticWebAppConfig(base, segments, prerenderedPaths = []) {
  const { navigationFallback: _fallback, ...rest } = base;
  const embeddable = frameableHeaders(base.globalHeaders);
  const pageRoutes = prerenderedPaths
    .filter((p) => p !== '/')
    .map((p) => ({ route: p, rewrite: `${p}/index.html` }));
  const appRoutes = [];
  for (const { segment, exact, deeper } of segments) {
    // The app's pages and token links are never indexed: said in the response too, not only the HTML.
    const headers = {
      headers: {
        'X-Robots-Tag': 'noindex, nofollow',
        ...(embeddable && EMBEDDABLE_SEGMENTS.includes(segment) ? embeddable : {}),
      },
    };
    if (exact) appRoutes.push({ route: `/${segment}`, rewrite: APP_SHELL, ...headers });
    if (deeper) appRoutes.push({ route: `/${segment}/*`, rewrite: APP_SHELL, ...headers });
  }
  return {
    ...rest,
    routes: [...(base.routes ?? []), ...pageRoutes, ...appRoutes],
    responseOverrides: {
      ...(base.responseOverrides ?? {}),
      404: { rewrite: '/404.html', statusCode: 404 },
    },
  };
}

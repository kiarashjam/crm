// Warming the destination's code chunk while the launch animation plays.
//
// Every page after the launcher is `React.lazy`, so `navigate()` used to unmount
// the overlay straight into a Suspense fallback: a striking dark transition
// handing off to a spinner. The animation is a second of wall-clock we are
// spending anyway — spending it fetching the chunk the user is about to need
// means the overlay usually gives way to the page itself.
//
// Vite keys chunks by module specifier, so calling `import('@/app/pages/Leads')`
// here resolves to the very same module instance `React.lazy` awaits in App.tsx.
// It is a warm cache hit, not a second copy.
//
// This table restates part of the route table, which is the kind of duplication
// that drifts. It is allowed here because the failure mode is benign and
// symmetric: a missing or stale entry means no prefetch and a brief loader —
// exactly today's behaviour — and never a wrong page or a broken navigation.
// `routeChunkKey` is exported separately so a test can pin the matching without
// pulling every page module into the test run.

const DETAIL = /^\/(leads|deals|tasks|contacts|companies)\/[^/]+$/;

/**
 * Which entry in the loader table a path belongs to, or null when nothing here
 * covers it. Pure and total, so it can be tested without any dynamic import.
 */
export function routeChunkKey(path: string): string | null {
  const clean = (path.split('?')[0] ?? '').split('#')[0] ?? '';
  if (!clean.startsWith('/')) return null;

  // Sub-pages first: /leads/import must not be read as a lead id.
  const exact = clean.replace(/\/+$/, '') || '/';
  if (exact === '/leads/import') return 'leads/import';
  if (exact === '/leads/webhook') return 'leads/webhook';

  const detail = DETAIL.exec(exact);
  if (detail) return `${detail[1]}/detail`;

  const seg = exact.split('/').filter(Boolean);
  if (seg.length !== 1) return null;
  return seg[0]!;
}

const LOADERS: Record<string, () => Promise<unknown>> = {
  dashboard: () => import('@/app/pages/Dashboard'),
  leads: () => import('@/app/pages/Leads'),
  'leads/detail': () => import('@/app/pages/LeadDetailPage'),
  'leads/import': () => import('@/app/pages/LeadImport'),
  'leads/webhook': () => import('@/app/pages/LeadWebhook'),
  deals: () => import('@/app/pages/Pipeline'),
  'deals/detail': () => import('@/app/pages/DealDetail'),
  tasks: () => import('@/app/pages/Tasks'),
  'tasks/detail': () => import('@/app/pages/TaskDetail'),
  contacts: () => import('@/app/pages/Contacts'),
  'contacts/detail': () => import('@/app/pages/ContactDetail'),
  companies: () => import('@/app/pages/Companies'),
  'companies/detail': () => import('@/app/pages/CompanyDetail'),
  reports: () => import('@/app/pages/Reports'),
  activities: () => import('@/app/pages/Activities'),
  sequences: () => import('@/app/pages/Sequences'),
  settings: () => import('@/app/pages/Settings'),
  team: () => import('@/app/pages/Team'),
  send: () => import('@/app/pages/SendToCrm'),
  templates: () => import('@/app/pages/Templates'),
  history: () => import('@/app/pages/History'),
};

/** Every key the loader table can serve, for the coverage test. */
export const PREFETCHABLE = Object.keys(LOADERS);

/**
 * Start loading the code for `path`. Fire and forget: a rejection here must never
 * surface, because failing to prefetch is not a failure — the router will fetch
 * it again when it navigates, and report that failure itself.
 */
export function prefetchRoute(path: string): void {
  const key = routeChunkKey(path);
  if (!key) return;
  const load = LOADERS[key];
  if (!load) return;
  try {
    void load().catch(() => {});
  } catch {
    // A synchronous throw from the import factory. Same story: not our problem.
  }
}

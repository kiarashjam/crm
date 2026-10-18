import { useSyncExternalStore } from 'react';
import { matchPath, useLocation } from 'react-router-dom';
import { HardDrive } from 'lucide-react';
import { servesDemoData } from '@/app/api/apiClient';
import { isLocalOnly, subscribeLocalOnly } from '@/app/api/localOnly';

/**
 * API resources behind each module that the backend doesn't serve yet. When a
 * request to one of them 404s, `apiWithFallback` saves to this browser instead.
 */
const MODULE_RESOURCES = {
  applications: ['applications'],
  attachments: ['attachments'],
  audit: ['audit'],
  automations: ['automations'],
  campaigns: ['campaigns'],
  charges: ['charges', 'payments'],
  communications: ['communications'],
  customFields: ['custom-fields'],
  donations: ['donations'],
  email: ['email'],
  events: ['events'],
  giftCards: ['gift-cards'],
  hospitalitySettings: ['hospitality'],
  households: ['households', 'household-members'],
  loyalty: ['loyalty'],
  members: ['members'],
  products: ['products', 'deals/line-items'],
  reservations: ['reservations', 'resources'],
  subscriptions: ['subscriptions'],
  tabs: ['tabs'],
  visits: ['visits'],
} as const;

type LocalModule = keyof typeof MODULE_RESOURCES;

/**
 * Pages that read or write those modules, from their imports (components they
 * render included). When a page starts using another local-only module, add it
 * here. More specific paths come first, since `matchPath` takes the first hit.
 */
const ROUTE_MODULES: [pattern: string, modules: LocalModule[]][] = [
  ['/leads/:id', ['attachments', 'customFields', 'email']],
  ['/deals/:id', ['attachments', 'customFields', 'products']],
  ['/contacts/:id', ['attachments', 'customFields', 'email']],
  ['/companies/:id', ['attachments', 'customFields']],
  ['/settings', ['customFields']],
  ['/automations', ['automations']],
  ['/audit', ['audit']],
  ['/members/import', ['members']],
  ['/members/:id', ['charges', 'communications', 'giftCards', 'loyalty', 'members', 'reservations', 'visits']],
  ['/members', ['members']],
  ['/events/:id', ['events', 'loyalty', 'members']],
  ['/events', ['events']],
  ['/reservations', ['reservations']],
  ['/loyalty', ['loyalty', 'members']],
  ['/applications', ['applications', 'members']],
  ['/charges', ['charges', 'members']],
  ['/visits', ['members', 'visits']],
  ['/gift-cards', ['giftCards', 'members']],
  ['/communications', ['communications', 'members']],
  ['/portal', ['charges', 'events', 'loyalty', 'members', 'reservations', 'visits']],
  ['/households', ['households', 'members']],
  ['/subscriptions', ['members', 'subscriptions']],
  ['/campaigns', ['campaigns', 'communications', 'members']],
  ['/donations', ['donations', 'members']],
  ['/calendar', ['events', 'reservations']],
  ['/statements', ['charges', 'members']],
  ['/club-reports', ['charges', 'events', 'loyalty', 'members', 'visits']],
  ['/tabs', ['members', 'tabs']],
  ['/pos', ['charges', 'giftCards', 'loyalty', 'members', 'tabs', 'visits']],
  ['/hospitality-settings', ['hospitalitySettings', 'members', 'visits']],
  ['/hospitality', ['applications', 'charges', 'events', 'members', 'reservations', 'subscriptions', 'tabs', 'visits']],
];

/** The resources the page at `pathname` depends on that may be local-only. */
export function localResourcesFor(pathname: string): string[] {
  const entry = ROUTE_MODULES.find(([pattern]) => matchPath({ path: pattern, end: true }, pathname));
  return entry ? entry[1].flatMap((m) => MODULE_RESOURCES[m]) : [];
}

/**
 * Full-width notice shown when something on the current page has been saved in
 * this browser because the backend has no endpoint for it yet. It appears only
 * after such a save or load has actually happened, and never with demo data,
 * where everything is local by design.
 */
export default function LocalOnlyNotice() {
  const { pathname } = useLocation();
  const show = useSyncExternalStore(
    subscribeLocalOnly,
    () => !servesDemoData() && isLocalOnly(localResourcesFor(pathname)),
  );
  if (!show) return null;

  return (
    <div
      className="w-full bg-amber-50 border-b border-amber-200/80 py-2 px-[var(--page-padding)]"
      role="status"
      aria-live="polite"
    >
      <div className="flex items-center justify-center gap-3 text-sm font-medium text-amber-900">
        <HardDrive className="h-4 w-4 shrink-0 text-amber-700" aria-hidden />
        <span className="max-w-4xl text-left">
          Some entries on this page are saved in this browser only. Your team can&apos;t see them yet, and clearing
          browser data deletes them.
        </span>
      </div>
    </div>
  );
}

// Which slice of the sales plan belongs on the page that is open.

export type PageScope =
  | { mode: 'hidden' }
  | { mode: 'focus' }
  | { mode: 'list'; list: 'Leads' | 'Deals' | 'Tasks' | 'Contacts' | 'Companies' | 'Sequences' | 'Automations' | 'Duplicates' }
  | { mode: 'record'; href: string };

const LISTS: { prefix: string; list: Extract<PageScope, { mode: 'list' }>['list'] }[] = [
  { prefix: '/leads', list: 'Leads' },
  { prefix: '/deals', list: 'Deals' },
  { prefix: '/tasks', list: 'Tasks' },
  { prefix: '/contacts', list: 'Contacts' },
  { prefix: '/companies', list: 'Companies' },
  { prefix: '/sequences', list: 'Sequences' },
  { prefix: '/automations', list: 'Automations' },
  { prefix: '/duplicates', list: 'Duplicates' },
];

/**
 * Pages that live UNDER a record prefix but are not records.
 *
 * `/leads/routing` arrived after this list did and was read as a lead whose id
 * is "routing": the panel searched for it, found nothing, and showed an empty
 * "Plan / This record has no saved plan." on the routing page. A test now reads
 * the routes in App.tsx so the next such page cannot slip past the same way.
 */
export const NOT_RECORDS: Readonly<Record<string, readonly string[]>> = {
  '/leads': ['import', 'routing', 'webhook'],
};

/**
 * Screens where a sales queue has no place: the till at the bar, and the preview
 * of what a club MEMBER sees. Both render the staff header, so without this the
 * panel sat on top of them listing leads and deals.
 */
export const HIDDEN_ON: readonly string[] = ['/pos', '/portal'];

export function pageScope(pathname: string): PageScope {
  const path = (pathname.split('?')[0] || '/').replace(/\/+$/, '') || '/';
  if (HIDDEN_ON.some((hidden) => path === hidden || path.startsWith(`${hidden}/`))) return { mode: 'hidden' };
  for (const { prefix, list } of LISTS) {
    if (path === prefix) return { mode: 'list', list };
    if (!path.startsWith(`${prefix}/`)) continue;
    const rest = path.slice(prefix.length + 1);
    if (NOT_RECORDS[prefix]?.includes(rest)) return { mode: 'list', list };
    if (rest.length > 0 && !rest.includes('/')) return { mode: 'record', href: path };
  }
  return { mode: 'focus' };
}

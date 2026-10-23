// Which slice of the sales plan belongs on the page that is open.

export type PageScope =
  | { mode: 'hidden' }
  | { mode: 'focus' }
  | { mode: 'list'; list: 'Leads' | 'Deals' | 'Tasks' | 'Contacts' | 'Companies' | 'Sequences' | 'Automations' | 'Duplicates' | 'Team' | 'Members' | 'Applications' | 'Charges' | 'Reservations' | 'Subscriptions' | 'Campaigns' | 'Events' | 'Activities' | 'Calendar' | 'Households' | 'Loyalty' | 'Visits' | 'Gift cards' | 'Tabs' | 'Communications' | 'Donations' | 'Statements' | 'POS' | 'Club reports' | 'Audit' | 'History' }
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
  { prefix: '/team', list: 'Team' },
  { prefix: '/members', list: 'Members' },
  { prefix: '/applications', list: 'Applications' },
  { prefix: '/charges', list: 'Charges' },
  { prefix: '/reservations', list: 'Reservations' },
  { prefix: '/subscriptions', list: 'Subscriptions' },
  { prefix: '/campaigns', list: 'Campaigns' },
  { prefix: '/events', list: 'Events' },
  { prefix: '/activities', list: 'Activities' },
  { prefix: '/calendar', list: 'Calendar' },
  { prefix: '/households', list: 'Households' },
  { prefix: '/loyalty', list: 'Loyalty' },
  { prefix: '/visits', list: 'Visits' },
  { prefix: '/gift-cards', list: 'Gift cards' },
  { prefix: '/tabs', list: 'Tabs' },
  { prefix: '/communications', list: 'Communications' },
  { prefix: '/donations', list: 'Donations' },
  { prefix: '/statements', list: 'Statements' },
  { prefix: '/pos', list: 'POS' },
  { prefix: '/club-reports', list: 'Club reports' },
  { prefix: '/audit', list: 'Audit' },
  { prefix: '/history', list: 'History' },
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
  '/members': ['import'],
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

type ListName = Extract<PageScope, { mode: 'list' }>['list'];

/** Whether a suggestion belongs on the list that is open. Lead rows keep their phase name. */
export function itemOnList(item: { href: string; list: string }, list: ListName): boolean {
  if (list === 'Leads') return item.href.startsWith('/leads/') || item.list === 'Leads';
  if (list === 'Deals') return item.href.startsWith('/deals/');
  if (list === 'Calendar') return item.list === 'Reservations' || item.list === 'Events';
  if (list === 'Team') return item.list === 'Team';
  if (list === 'Statements') return item.list === 'Charges';
  if (list === 'POS') return item.list === 'Tabs';
  if (list === 'Club reports') return item.list === 'Members' || item.list === 'Charges' || item.list === 'Reservations' || item.list === 'Subscriptions';
  return item.list === list;
}

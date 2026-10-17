// Which slice of the sales plan belongs on the page that is open.

export type PageScope =
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

export function pageScope(pathname: string): PageScope {
  const path = (pathname.split('?')[0] || '/').replace(/\/+$/, '') || '/';
  for (const { prefix, list } of LISTS) {
    if (path === prefix) return { mode: 'list', list };
    if (!path.startsWith(`${prefix}/`)) continue;
    if (path === '/leads/import' || path === '/leads/webhook') return { mode: 'list', list: 'Leads' };
    const rest = path.slice(prefix.length + 1);
    if (rest.length > 0 && !rest.includes('/')) return { mode: 'record', href: path };
  }
  return { mode: 'focus' };
}

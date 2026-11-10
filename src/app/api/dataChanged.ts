// "Something was saved" — one signal for every write in the app.
//
// Views that summarise records drawn from many lists (the sales agent's next
// steps, above all) cannot know which page changed what. Rather than threading a
// refresh callback through sixty pages, every successful write announces itself
// here, from the two places all writes pass through: `authFetch` for the real API
// and `createMockStore` for demo mode. Listeners decide for themselves whether
// and when to reload.
//
// The signal says which kinds of record the write may have changed ("leads",
// "tasks", …), never what they now look like — so a listener can never act on a
// half-understood change, only re-read the truth. It used to say nothing at all,
// and so every listener re-read everything: logging one call made the sales agent
// download eight whole lists. Now a listener re-reads only the kinds that changed.

const EVENT = 'cadence:data-changed';

export interface DataChange {
  /**
   * The kinds of record the write may have changed, each named as its cached copies
   * are (see `resourcesChangedBy`). Null when that is not known: everything may have.
   */
  resources: readonly string[] | null;
}

const ANYTHING: DataChange = { resources: null };

/** Say that a write just succeeded. Safe to call anywhere, including outside a browser. */
export function announceDataChanged(change: DataChange = ANYTHING): void {
  if (typeof window === 'undefined') return;
  window.dispatchEvent(new CustomEvent<DataChange>(EVENT, { detail: change }));
}

/** Listen for writes. Returns the unsubscribe function. */
export function onDataChanged(listener: (change: DataChange) => void): () => void {
  if (typeof window === 'undefined') return () => {};
  const handler = (event: Event) => listener((event as CustomEvent<DataChange | undefined>).detail ?? ANYTHING);
  window.addEventListener(EVENT, handler);
  return () => window.removeEventListener(EVENT, handler);
}

/** Whether a change may have touched any of these kinds of record. */
export function touches(change: DataChange, resources: readonly string[]): boolean {
  return change.resources === null || resources.some((resource) => change.resources!.includes(resource));
}

/**
 * Writes that change no CRM record: signing in and security, notification
 * read-marks, the AI writer's drafts, personal settings, lead routing setup
 * (rules, queues, territories, deadline, forms — they shape future leads, not
 * today's), and an import's reading of its file and dry run, which are POSTs only
 * because they carry the file. Announcing them would make every listener re-read
 * for nothing — the Settings page, for one, POSTs to /api/auth/2fa/setup every time
 * it opens.
 */
const NOT_RECORD_WRITES = [
  '/api/auth/', '/api/notifications', '/api/copy', '/api/settings', '/api/lead-ops/',
  '/api/imports/analyze', '/api/imports/preview',
];

/** …except these, under an excluded prefix, which do change records. */
const RECORD_WRITES_UNDER_EXCLUDED = ['/api/lead-ops/bulk'];

function pathnameOf(path: string): string {
  let pathname = path;
  try {
    if (/^https?:/i.test(path)) pathname = new URL(path).pathname;
  } catch {
    // Not a URL after all; use it as given.
  }
  return (pathname.split(/[?#]/)[0] ?? '').toLowerCase();
}

/** Whether a successful request with this method and path changed records. */
export function changesRecords(method: string | undefined, path: string): boolean {
  const m = (method ?? 'GET').toUpperCase();
  if (m !== 'POST' && m !== 'PUT' && m !== 'PATCH' && m !== 'DELETE') return false;
  const lower = pathnameOf(path);
  if (RECORD_WRITES_UNDER_EXCLUDED.some((prefix) => lower.startsWith(prefix))) return true;
  return !NOT_RECORD_WRITES.some((prefix) => lower.startsWith(prefix));
}

// ── what a write changes ─────────────────────────────────────────────────────

/** The records a lead, contact, company or deal links to, which a merge or a conversion moves. */
const LINKED = ['leads', 'contacts', 'companies', 'deals', 'tasks', 'activities', 'sequences', 'duplicates', 'work-queue', 'reports'];

/**
 * What a write under /api/<base> may change, beyond its own records. The server
 * does more than store the row it is sent, and a list showing any of these would
 * otherwise go on showing the old figures:
 *
 * - A lead's save adds or renames its person's contact; automations that run on it
 *   add tasks, enrol it in sequences and log what they send.
 * - An activity is each record's latest touch, can mark a lead answered (and so its
 *   status), and ends a sequence when it is a reply.
 * - A deal shows on its company's row and its contact's; a contact's name and a
 *   company's show on the deals and contacts linked to them.
 * - A deal moved to a stage, won or lost runs the workspace's automations, which can
 *   add a task; so does a countersigned contract, which wins its deal and moves its
 *   lead's status on.
 * - Removing a member hands their open leads, deals and tasks to someone else, and
 *   the workspace's currency and time zone decide how its figures are counted.
 * - Work-queue and report figures count leads, deals and tasks.
 *
 * A base not listed changes only its own records (the club's members, charges,
 * visits…), which is why checking someone in no longer reloads the sales lists.
 * Add a base here when the server starts changing other records on its writes.
 */
const CHANGES: Readonly<Record<string, readonly string[]>> = {
  leads: ['leads', 'contacts', 'tasks', 'activities', 'sequences', 'duplicates', 'work-queue', 'reports'],
  activities: ['activities', 'leads', 'contacts', 'deals', 'sequences', 'work-queue', 'reports'],
  deals: ['deals', 'companies', 'contacts', 'activities', 'tasks', 'work-queue', 'reports'],
  contacts: ['contacts', 'companies', 'deals', 'leads', 'duplicates', 'reports'],
  companies: ['companies', 'contacts', 'deals', 'duplicates', 'reports'],
  tasks: ['tasks', 'work-queue', 'reports'],
  sequences: ['sequences', 'leads', 'work-queue'],
  email: ['activities', 'leads', 'contacts', 'deals', 'sequences'],
  contracts: ['contracts', 'leads', 'deals', 'activities', 'tasks', 'work-queue', 'reports'],
  imports: ['leads', 'contacts', 'companies', 'deals', 'members', 'duplicates', 'work-queue', 'reports'],
  'lead-ops': ['leads', 'contacts', 'tasks', 'activities', 'work-queue', 'reports'],
  pipelines: ['pipelines', 'deals', 'reports'],
  dealstages: ['pipelines', 'deals', 'reports'],
  leadstatuses: ['lead-lookups', 'leads', 'reports'],
  leadsources: ['lead-lookups', 'leads', 'reports'],
  organizations: ['team', 'organizations', 'leads', 'deals', 'tasks', 'work-queue', 'reports'],
  invites: ['team'],
  joinrequests: ['team'],
  teams: ['team', 'reports'],
  duplicates: ['duplicates'],
};

/** Which records an activity is about, from the body it was saved with. */
function activityLinks(body: unknown): string[] | null {
  if (typeof body !== 'string') return null;
  try {
    const parsed = JSON.parse(body) as { leadId?: unknown; contactId?: unknown; dealId?: unknown } | null;
    if (!parsed || typeof parsed !== 'object') return null;
    const links: string[] = [];
    if (parsed.leadId) links.push('leads', 'sequences');
    if (parsed.contactId) links.push('contacts', 'sequences');
    if (parsed.dealId) links.push('deals');
    return links.length ? links : null;
  } catch {
    return null;
  }
}

/**
 * The kinds of record a successful write may have changed, as cached copies name
 * them. `body` is the request's body, read only to see which records a new
 * activity is about: a call logged on a lead changes that lead, not every deal.
 */
export function resourcesChangedBy(method: string | undefined, path: string, body?: unknown): string[] {
  const segments = pathnameOf(path).split('/').filter(Boolean);
  if (segments[0] === 'api') segments.shift();
  const base = segments[0] ?? '';
  const last = segments[segments.length - 1] ?? '';
  // A merge moves everything linked to the record merged away; a conversion makes
  // a contact, a company and a deal from the lead.
  if (last === 'merge' || (base === 'leads' && last === 'convert')) return [...LINKED];
  if (base === 'activities' && (method ?? 'GET').toUpperCase() === 'POST') {
    const links = activityLinks(body);
    if (links) return [...new Set(['activities', ...links, 'work-queue', 'reports'])];
  }
  return [...(CHANGES[base] ?? [base])];
}

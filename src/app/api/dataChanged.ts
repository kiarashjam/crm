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
 * it opens. A privacy lookup, an erasure's dry run and an export are POSTs too (they
 * carry an email address, which does not belong in a URL), and change no record: an
 * export only writes its line in the audit trail. Asking the assistant changes nothing
 * either: a change it proposes is made by the person's confirm, through its own endpoint.
 */
const NOT_RECORD_WRITES = [
  '/api/auth/', '/api/notifications', '/api/copy', '/api/settings', '/api/lead-ops/',
  '/api/imports/analyze', '/api/imports/preview',
  '/api/privacy/find', '/api/privacy/export', '/api/privacy/erasure/preview',
  '/api/agent/chat',
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
 * What merging two club members changes: every record that carried the duplicate's id
 * (visits, points, the ledger's charges, payments and tabs, bookings, gift cards,
 * consent, messages…) is the kept member's now, so is their statement, and the pair
 * leaves the duplicates list.
 */
const MEMBER_MERGE = [
  'members', 'campaigns', 'communications', 'duplicates', 'visits', 'loyalty', 'charges', 'payments', 'statements',
  'tabs', 'reservations', 'events', 'gift-cards', 'donations', 'subscriptions', 'households', 'applications',
  'payment-profile', 'online-payments', 'bank', 'sales', 'pos', 'member-codes', 'club-reports',
  'retention', 'renewals', 'referrals', 'corporate', 'private-events',
];

/**
 * What erasing a person changes, and a retention run, which erases people too: their leads
 * and contacts go, the deals and tasks kept lose their name, their timeline, enrolments and
 * messages go, their member account is anonymised with the money on it kept under a
 * pseudonym, their bookings and places on event lists are cancelled, their subscriptions
 * stopped, their household left, the automation runs about them go with their logs (a
 * retention run also removes the logs of runs that finished long ago), and the erasure is
 * logged.
 */
const PERSON_ERASURE = [...new Set([
  ...LINKED, ...MEMBER_MERGE, 'contracts', 'activities', 'communications', 'campaigns', 'automations', 'privacy', 'club-accounts',
])];

/**
 * What a household write changes: the household, what a member's page says of it (whose
 * account their bills go on), the relationships cards of the contacts and leads in it, and
 * for a merge, the duplicates list.
 */
const HOUSEHOLD_WRITE = ['households', 'members', 'contacts', 'leads', 'duplicates'];

/**
 * What charging a member's saved card changes: when Stripe answers at once, the payment is
 * on the ledger before the answer comes back (settling charges, moving the balance and the
 * statement, perhaps putting back a member the billing lapsed), and the attempt is listed.
 */
const SAVED_CARD_CHARGE = ['payment-profile', 'online-payments', 'payments', 'charges', 'members', 'statements', 'club-reports'];

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
 * - The club's money is one ledger: a charge, a payment, a tab's close or a bill moves
 *   the member's balance (on the members list) and their statement, a payment settles
 *   charges, and closing a tab earns loyalty points. A balance set by hand on a member
 *   is an entry too, and a member's merge moves the duplicate's entries.
 * - A change to an event's list can post a ticket or a no-show fee, take one back (a
 *   payment for it becoming credit), give a member their check-in points, and email the
 *   next in line (logged in their communications). A booking can post or give back a
 *   deposit and a no-show fee; a resource's settings show on its bookings.
 * - Club Reports and Today at the Club (club-reports) are summed on the server from the
 *   ledger, the members, check-ins, bookings, tabs, gift cards and points: every write
 *   that moves one of those refreshes them.
 * - A member added, a booking made, a place taken on an event's list and an approval run
 *   the workspace's automations too, which can add a task.
 *
 * A base not listed changes only its own records (the club's donations, resources…),
 * which is why checking someone in does not reload the sales lists. Add a base here
 * when the server starts changing other records on its writes.
 */
const CHANGES: Readonly<Record<string, readonly string[]>> = {
  leads: ['leads', 'contacts', 'tasks', 'activities', 'sequences', 'duplicates', 'work-queue', 'reports'],
  activities: ['activities', 'leads', 'contacts', 'deals', 'sequences', 'work-queue', 'reports'],
  deals: ['deals', 'companies', 'contacts', 'activities', 'tasks', 'work-queue', 'reports'],
  contacts: ['contacts', 'companies', 'deals', 'leads', 'duplicates', 'reports', 'club-accounts', 'private-events'],
  companies: ['companies', 'contacts', 'deals', 'duplicates', 'reports', 'club-accounts', 'corporate', 'private-events'],
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
  // The workspace's currency and time zone decide how the club's reports are counted too.
  organizations: ['team', 'organizations', 'leads', 'deals', 'tasks', 'work-queue', 'reports', 'club-reports'],
  invites: ['team'],
  joinrequests: ['team'],
  teams: ['team', 'reports'],
  duplicates: ['duplicates'],
  // A balance set by hand on a member is an entry on the ledger, a member's tier or
  // status changes who a campaign reaches, and a member added can start their dues
  // subscription (due today, so billing now has it to bill) and be charged their initiation fee.
  // Their renewal date and status are what the renewals board and the at-risk list read, and
  // their name shows on referrals.
  members: ['members', 'charges', 'payments', 'statements', 'campaigns', 'subscriptions', 'billing', 'club-reports', 'tasks',
    'renewals', 'retention', 'referrals'],
  // A void or a payment can put back a member the billing lapsed (members).
  // What referred members are charged is the referrals report's figures; a subscription
  // period billed and paid is a renewal done (the renewals board).
  charges: ['charges', 'members', 'statements', 'club-reports', 'referrals', 'renewals', 'club-accounts', 'corporate', 'private-events'],
  payments: ['payments', 'charges', 'members', 'statements', 'club-reports', 'renewals', 'club-accounts', 'corporate', 'private-events'],
  tabs: ['tabs', 'charges', 'members', 'loyalty', 'statements', 'club-reports'],
  subscriptions: ['subscriptions', 'charges', 'members', 'statements', 'billing', 'club-reports', 'renewals', 'club-accounts', 'corporate'],
  // A check-in writes the points it earned to the loyalty ledger.
  visits: ['visits', 'loyalty', 'club-reports'],
  // Club Reports and Today at the Club count what is left on gift cards and the points
  // members hold, so a card spent or a point given changes them too.
  'gift-cards': ['gift-cards', 'club-reports'],
  loyalty: ['loyalty', 'club-reports'],
  // A tier renamed in the catalogue is renamed on every member who has it.
  catalog: ['catalog', 'members', 'club-reports'],
  // Billing posts charges, moves subscriptions on and members' balances, and is a run; its
  // reminders and monthly statements are emailed, each logged in the member's communications.
  billing: ['billing', 'charges', 'subscriptions', 'members', 'statements', 'communications', 'club-reports', 'renewals', 'club-accounts', 'corporate'],
  hospitality: ['hospitality'],
  events: ['events', 'charges', 'payments', 'members', 'statements', 'loyalty', 'communications', 'club-reports', 'tasks'],
  reservations: ['reservations', 'charges', 'payments', 'members', 'statements', 'club-reports', 'tasks'],
  resources: ['resources', 'reservations', 'private-events'],
  // A client's account opened.
  'club-accounts': ['club-accounts', 'corporate'],
  // A corporate membership opens the company's account and can start its dues; a seat changed
  // decides whose account a nominee's charges go on; ending it stops the dues.
  'corporate-memberships': ['corporate', 'club-accounts', 'subscriptions', 'charges', 'members', 'statements', 'billing', 'club-reports'],
  // A private event holds its spaces as bookings; its deposit and invoice are charges on the
  // client's account (a deposit given back is credit); its contract is drafted on its deal.
  'private-events': ['private-events', 'reservations', 'charges', 'payments', 'club-accounts', 'corporate', 'members', 'statements',
    'club-reports', 'contracts', 'deals'],
  // Sending a campaign writes a message per member into the communications log.
  campaigns: ['campaigns', 'communications'],
  // An approval makes the member, starts their dues, charges the initiation fee, joins the
  // household, records their consent and queues the welcome (logged in their communications);
  // the lead's and the contact's pages show the application and the member.
  // An approval of a referred applicant gives their referrer the club's bonus: points, or a credit on the ledger.
  applications: ['applications', 'members', 'charges', 'payments', 'subscriptions', 'statements', 'communications', 'households', 'billing', 'leads', 'contacts', 'campaigns', 'club-reports', 'tasks',
    'referrals', 'loyalty', 'renewals'],
  households: HOUSEHOLD_WRITE,
  // The payment settings decide what the money screens offer (pay online, the QR-bill).
  'payment-settings': ['payment-settings'],
  // A bank file's matched transfers, and one allocated by hand, are payments on the ledger:
  // they settle charges, move balances and statements, and can put back a lapsed member.
  bank: ['bank', 'payments', 'charges', 'members', 'statements', 'club-reports'],
  // A sale posts its charges and what was paid at the till, moves the balance and the
  // statement, and earns its points; the club's reports sum all of those.
  sales: ['sales', 'charges', 'payments', 'members', 'statements', 'loyalty', 'club-reports'],
  // An import, a match or a retry posts tickets as sales; the settings and the connection
  // decide what the POS screens show. A match also remembers the POS customer as one of the
  // member's codes.
  pos: ['pos', 'sales', 'charges', 'payments', 'members', 'statements', 'loyalty', 'member-codes', 'club-reports'],
  // Inviting every member to the member portal emails them, each logged in their communications.
  'member-portal': ['member-portal', 'communications'],
  // The member portal's settings say whether the workspace also hands members to its own portal.
  onboarding: ['onboarding', 'member-portal'],
  // The club's retention and referral rules (what the referrals card offers, the board's
  // threshold), and a scoring run, which can make tasks and queue emails (logged in the
  // members' communications) and notes the renewals coming up.
  retention: ['retention', 'referrals', 'renewals', 'tasks', 'work-queue', 'communications'],
  // A membership marked renewed moves the member's renewal date on.
  renewals: ['renewals', 'members', 'club-reports'],
};

/** What working a member's score out again changes: a crossing into at risk makes a task and queues an email. */
const RISK_RECOMPUTE = ['retention', 'renewals', 'tasks', 'work-queue', 'communications'];

/** What a referral bonus given by hand changes: points on the loyalty ledger, or a credit on the house account. */
const REFERRAL_BONUS = ['referrals', 'loyalty', 'payments', 'members', 'statements', 'club-reports'];

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
  if (base === 'members' && last === 'merge') return [...MEMBER_MERGE];
  // A consent answer changes who a marketing campaign reaches, and nothing on the
  // member's account, so it leaves the money lists alone.
  if (base === 'members' && last === 'consent') return ['members', 'campaigns'];
  // Emailing a statement or a pay link writes a line in the member's communications log, nothing more.
  if (base === 'members' && last === 'email') return ['communications'];
  // A member's saved card and dues on it: a pay link is made, a card removed, dues on file
  // turned on or off, or the card charged (a payment on the ledger when Stripe answers at once).
  if (base === 'members' && segments.length >= 3) {
    const sub = segments[2];
    // Inviting a member to the member portal emails them (logged in their communications);
    // signing them out of it changes nothing else.
    if (sub === 'portal') return ['member-portal', 'communications'];
    // A member's codes at the POS are theirs alone; matching a ticket remembers one too (under /pos).
    if (sub === 'codes') return ['member-codes'];
    if (sub === 'card' && last === 'charge') return [...SAVED_CARD_CHARGE];
    if (sub === 'card' || sub === 'autopay' || sub === 'pay-link') return ['payment-profile'];
    // Their score worked out again, the concerns recorded about them, who referred them, and
    // their referrer's bonus.
    if (sub === 'risk') return [...RISK_RECOMPUTE];
    if (sub === 'concerns') return ['retention'];
    if (sub === 'referrer') return ['referrals', 'members'];
    if (sub === 'referral-bonus') return [...REFERRAL_BONUS];
  }
  // Deleting a member detaches their places on event lists and their bookings, which stay
  // under the name they were made in: a screen still showing the member on one would send
  // the id of someone who is gone. They leave their household too, and a household nobody
  // is left in goes with them.
  if (base === 'members' && segments.length === 2 && (method ?? 'GET').toUpperCase() === 'DELETE') {
    return [...(CHANGES.members ?? ['members']), 'events', 'reservations', 'payment-profile', 'households', 'corporate', 'private-events'];
  }
  // Households merge on their own: the people move to the household kept.
  if (base === 'households') return [...HOUSEHOLD_WRITE];
  // An erasure, or a retention run, reaches every record the person had; saving the privacy
  // settings changes only them.
  if (base === 'privacy' && (last === 'erasure' || last === 'run')) return [...PERSON_ERASURE];
  // A spouse or partner recorded on a contact's or a lead's page is a household's couple,
  // and a tie between two of one household is held by it.
  if ((base === 'contacts' || base === 'leads') && segments.includes('relationships')) {
    return [...new Set([...(CHANGES[base] ?? [base]), 'households', 'members'])];
  }
  // Countersigning a contract approves the membership application of its lead or deal: the
  // member is made or linked, their dues start, the initiation fee is charged, they join
  // the household and are sent the welcome (logged in their communications).
  if (base === 'contracts' && last === 'countersign') {
    return [...new Set([...(CHANGES.contracts ?? []), ...(CHANGES.applications ?? []), 'private-events', 'reservations'])];
  }
  // Handing a deal's contact to the member portal notes it on the deal's and the contact's
  // timelines, and can add them to the club's members.
  if (base === 'deals' && last === 'onboarding') return [...(CHANGES.deals ?? []), 'members', 'club-reports'];
  // A merge moves everything linked to the record merged away; a conversion makes
  // a contact, a company and a deal from the lead.
  if (last === 'merge' && (base === 'companies' || base === 'contacts')) {
    return [...new Set([...LINKED, 'club-accounts', 'corporate', 'private-events', 'charges', 'payments', 'subscriptions', 'members', 'statements', 'households'])];
  }
  if (last === 'merge' || (base === 'leads' && last === 'convert')) return [...LINKED];
  if (base === 'activities' && (method ?? 'GET').toUpperCase() === 'POST') {
    const links = activityLinks(body);
    if (links) return [...new Set(['activities', ...links, 'work-queue', 'reports'])];
  }
  return [...(CHANGES[base] ?? [base])];
}

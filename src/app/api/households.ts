// Households: who belongs together, one primary each, and whose bills roll up to them.
//
// The one family model for the club and the CRM. A household's people are members,
// contacts, or a member and their contact; the server keeps one household per person and
// one primary per household. A member whose link rolls up has their charges, closed tabs
// and dues posted to the primary's account by the server's ledger, each saying who ran it
// up; switching it changes what is posted from then on, never what was, and takes
// money.manage. Every change to a household's people is checked against the household's
// version, so a change made from a page opened before someone else's is refused (409).
//
// Demo data keeps households in this browser, with the same rules where they are cheap to
// keep (one primary, one household per member); the demo's charges are not rolled up.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, servesDemoData, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getMembers, type Member } from './members';
import { downloadFile } from '@/app/lib/exportCsv';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/** A person's relation to the household's primary; the primary is "Primary". */
export type HouseholdRole = 'Primary' | 'Spouse' | 'Partner' | 'Child' | 'Dependent' | 'Parent' | 'Sibling' | 'Other';

export const HOUSEHOLD_ROLES: HouseholdRole[] = ['Primary', 'Spouse', 'Partner', 'Child', 'Dependent', 'Parent', 'Sibling', 'Other'];

/** The roles someone other than the primary can have. */
export const MEMBER_ROLES: HouseholdRole[] = HOUSEHOLD_ROLES.filter((r) => r !== 'Primary');

/** One person in a household: a member, a contact, or a member and their contact. */
export interface HouseholdPerson {
  linkId: string;
  memberId?: string;
  contactId?: string;
  /** The lead the contact came from. */
  leadId?: string;
  name: string;
  email?: string;
  role: HouseholdRole;
  isPrimary: boolean;
  /** Their charges, tabs and dues go on the primary's account. */
  rollUpBilling: boolean;
  tier?: string;
  memberStatus?: string;
  duesAmount?: number;
  duesFrequency?: string;
  /** Their own account's balance (negative while they owe). */
  houseBalance?: number;
  isArchived?: boolean;
  source?: string;
  addedAtUtc?: string;
}

export interface Household {
  id: string;
  name: string;
  notes?: string;
  createdAtUtc: string;
  primaryLinkId?: string;
  primaryMemberId?: string;
  primaryName?: string;
  /** The primary first, then in the order they joined. */
  people: HouseholdPerson[];
  /** How many of its people are club members. */
  members: number;
  currency?: string;
  version?: string;
}

// ── demo data ────────────────────────────────────────────────────────────────

const day = 86_400_000;

/** What the demo keeps: the household and its links; names and dues are read from the members. */
interface StoredHousehold {
  id: string;
  name: string;
  notes?: string;
  createdAtUtc: string;
  links: { linkId: string; memberId: string; role: HouseholdRole; isPrimary: boolean; rollUpBilling: boolean; addedAtUtc: string }[];
}

const seed: StoredHousehold[] = [
  {
    id: 'hh-seed-1',
    name: 'Hartwell',
    createdAtUtc: new Date(Date.now() - 400 * day).toISOString(),
    notes: 'Anniversary March 14.',
    links: [{ linkId: 'hm-seed-1', memberId: 'mem-seed-1', role: 'Primary', isPrimary: true, rollUpBilling: false, addedAtUtc: new Date(Date.now() - 400 * day).toISOString() }],
  },
  {
    id: 'hh-seed-2',
    name: 'Okafor',
    createdAtUtc: new Date(Date.now() - 180 * day).toISOString(),
    links: [
      { linkId: 'hm-seed-2', memberId: 'mem-seed-2', role: 'Primary', isPrimary: true, rollUpBilling: false, addedAtUtc: new Date(Date.now() - 180 * day).toISOString() },
      { linkId: 'hm-seed-3', memberId: 'mem-seed-6', role: 'Spouse', isPrimary: false, rollUpBilling: true, addedAtUtc: new Date(Date.now() - 170 * day).toISOString() },
    ],
  },
];

const store = createMockStore<StoredHousehold>({
  storageKey: 'crm.mock.households.v2',
  seed,
  idOf: (h) => h.id,
});

function personOf(link: StoredHousehold['links'][number], members: Map<string, Member>): HouseholdPerson {
  const m = members.get(link.memberId);
  return {
    linkId: link.linkId,
    memberId: link.memberId,
    name: m ? `${m.firstName} ${m.lastName}`.trim() : 'A former member',
    email: m?.email,
    role: link.role,
    isPrimary: link.isPrimary,
    rollUpBilling: link.rollUpBilling,
    tier: m?.tier,
    memberStatus: m?.status,
    duesAmount: m?.duesAmount,
    duesFrequency: m?.duesFrequency,
    houseBalance: m?.houseAccountBalance,
    source: 'club',
    addedAtUtc: link.addedAtUtc,
  };
}

function shapeOf(h: StoredHousehold, members: Map<string, Member>): Household {
  const people = [...h.links]
    .sort((a, b) => Number(b.isPrimary) - Number(a.isPrimary) || a.addedAtUtc.localeCompare(b.addedAtUtc))
    .map((l) => personOf(l, members));
  const primary = people.find((p) => p.isPrimary);
  return {
    id: h.id,
    name: h.name,
    notes: h.notes,
    createdAtUtc: h.createdAtUtc,
    primaryLinkId: primary?.linkId,
    primaryMemberId: primary?.memberId,
    primaryName: primary?.name,
    people,
    members: people.length,
  };
}

async function demoList(): Promise<Household[]> {
  const members = new Map((await getMembers()).map((m) => [m.id, m]));
  return [...store.list()].sort((a, b) => a.name.localeCompare(b.name)).map((h) => shapeOf(h, members));
}

async function demoOne(id: string): Promise<Household> {
  const found = (await demoList()).find((h) => h.id === id);
  if (!found) throw new Error('That household is no longer there.');
  return found;
}

function demoHouseholdOf(memberId: string): StoredHousehold | undefined {
  return store.list().find((h) => h.links.some((l) => l.memberId === memberId));
}

function demoRefuseIfInOne(memberId: string): void {
  const theirs = demoHouseholdOf(memberId);
  if (theirs) throw new Error(`They are already in the ${theirs.name} household. A person is in one household.`);
}

// ── reads ────────────────────────────────────────────────────────────────────

export async function getHouseholds(options: RequestOptions = {}): Promise<Household[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Household[]>('/api/households', { signal: options.signal });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(100);
      return demoList();
    },
  );
}

export async function getHousehold(id: string, options: RequestOptions = {}): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${id}`, { signal: options.signal }),
    () => demoOne(id),
  );
}

/** The household a member is in, or null. */
export async function getMemberHousehold(memberId: string, options: RequestOptions = {}): Promise<Household | null> {
  return apiWithFallback(
    async () => (await authFetchJson<{ household: Household | null }>(`/api/households/of-member/${memberId}`, { signal: options.signal }))?.household ?? null,
    async () => {
      const theirs = demoHouseholdOf(memberId);
      return theirs ? demoOne(theirs.id) : null;
    },
  );
}

// ── writes ───────────────────────────────────────────────────────────────────

export interface HouseholdInput {
  name: string;
  /** Its primary: a member, or (someone not yet a member) a contact. */
  primaryMemberId?: string;
  primaryContactId?: string;
  notes?: string;
}

export async function createHousehold(input: HouseholdInput): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>('/api/households', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(140);
      if (!input.primaryMemberId) throw new Error('Pick the member who heads the household.');
      demoRefuseIfInOne(input.primaryMemberId);
      const now = new Date().toISOString();
      const created = store.add({
        id: mockId('hh'),
        name: input.name,
        notes: input.notes,
        createdAtUtc: now,
        links: [{ linkId: mockId('hm'), memberId: input.primaryMemberId, role: 'Primary', isPrimary: true, rollUpBilling: false, addedAtUtc: now }],
      });
      return demoOne(created.id);
    },
  );
}

/** Renames the household or changes its notes. */
export async function updateHousehold(id: string, patch: { name?: string; notes?: string; version?: string }): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(100);
      store.update(id, { ...(patch.name !== undefined ? { name: patch.name } : {}), ...(patch.notes !== undefined ? { notes: patch.notes } : {}) });
      return demoOne(id);
    },
  );
}

/** Deletes the household; the members and contacts in it stay. */
export async function deleteHousehold(id: string): Promise<void> {
  await apiWithFallback(
    async () => {
      await ensureOk(await authFetch(`/api/households/${id}`, { method: 'DELETE' }));
    },
    async () => {
      await delay(80);
      store.remove(id);
    },
  );
}

export interface HouseholdPersonInput {
  memberId?: string;
  contactId?: string;
  role: HouseholdRole;
  /** Money: needs money.manage. */
  rollUpBilling?: boolean;
  /** The household's version the change was made from. */
  version?: string;
}

export async function addPersonToHousehold(householdId: string, input: HouseholdPersonInput): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${householdId}/members`, { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(120);
      if (!input.memberId) throw new Error('Pick a member.');
      demoRefuseIfInOne(input.memberId);
      const h = store.list().find((x) => x.id === householdId);
      if (!h) throw new Error('That household is no longer there.');
      store.update(householdId, {
        links: [...h.links, {
          linkId: mockId('hm'), memberId: input.memberId, role: input.role, isPrimary: false,
          rollUpBilling: input.rollUpBilling ?? false, addedAtUtc: new Date().toISOString(),
        }],
      });
      return demoOne(householdId);
    },
  );
}

/** Changes someone's role, or whether their bills roll up to the primary (money.manage). */
export async function updateHouseholdPerson(
  householdId: string,
  linkId: string,
  patch: { role?: HouseholdRole; rollUpBilling?: boolean; version?: string },
): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${householdId}/members/${linkId}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(100);
      const h = store.list().find((x) => x.id === householdId);
      if (!h) throw new Error('That household is no longer there.');
      store.update(householdId, {
        links: h.links.map((l) => (l.linkId !== linkId ? l : {
          ...l,
          ...(patch.role !== undefined && !l.isPrimary ? { role: patch.role } : {}),
          ...(patch.rollUpBilling !== undefined && !l.isPrimary ? { rollUpBilling: patch.rollUpBilling } : {}),
        })),
      });
      return demoOne(householdId);
    },
  );
}

export async function removeHouseholdPerson(householdId: string, linkId: string, version?: string): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${householdId}/members/${linkId}`, {
      method: 'DELETE',
      headers: version ? { 'If-Match': `"${version}"` } : undefined,
    }),
    async () => {
      await delay(80);
      const h = store.list().find((x) => x.id === householdId);
      if (!h) throw new Error('That household is no longer there.');
      if (h.links.find((l) => l.linkId === linkId)?.isPrimary) {
        throw new Error('The primary cannot leave: make someone else the primary first, or delete the household.');
      }
      store.update(householdId, { links: h.links.filter((l) => l.linkId !== linkId) });
      return demoOne(householdId);
    },
  );
}

/** The role the old primary takes: the new primary's relation to them, turned round. */
export function inverseRole(role: HouseholdRole): HouseholdRole {
  switch (role) {
    case 'Child':
    case 'Dependent':
      return 'Parent';
    case 'Parent':
      return 'Child';
    case 'Primary':
      return 'Other';
    default:
      return role;
  }
}

/** Makes this person the household's primary; the old primary steps down in the same change. */
export async function makeHouseholdPrimary(householdId: string, linkId: string, version?: string): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${householdId}/primary`, { method: 'POST', body: JSON.stringify({ linkId, version }) }),
    async () => {
      await delay(100);
      const h = store.list().find((x) => x.id === householdId);
      if (!h) throw new Error('That household is no longer there.');
      const next = h.links.find((l) => l.linkId === linkId);
      if (!next) throw new Error('They are no longer in this household.');
      store.update(householdId, {
        links: h.links.map((l) => {
          if (l.linkId === linkId) return { ...l, isPrimary: true, role: 'Primary' as HouseholdRole, rollUpBilling: false };
          if (l.isPrimary) return { ...l, isPrimary: false, role: inverseRole(next.role), rollUpBilling: false };
          return l;
        }),
      });
      return demoOne(householdId);
    },
  );
}

/** Folds duplicate households into the one kept, on the server in one save. */
export async function mergeHouseholds(keepId: string, duplicateIds: string[]): Promise<Household> {
  return apiWithFallback(
    () => authFetchJson<Household>(`/api/households/${keepId}/merge`, { method: 'POST', body: JSON.stringify({ duplicateIds }) }),
    async () => {
      await delay(120);
      const keep = store.list().find((h) => h.id === keepId);
      if (!keep) throw new Error('The household to keep is no longer there.');
      const links = [...keep.links];
      const inKeep = new Set(links.map((l) => l.memberId));
      let notes = keep.notes ?? '';
      for (const id of duplicateIds) {
        const other = store.list().find((h) => h.id === id);
        if (!other || other.id === keepId) continue;
        for (const link of other.links) {
          if (inKeep.has(link.memberId)) continue;
          links.push({ ...link, isPrimary: false, role: link.isPrimary ? 'Other' : link.role });
          inKeep.add(link.memberId);
        }
        const text = other.notes?.trim();
        if (text && !notes.includes(text)) notes = notes.trim() ? `${notes.trimEnd()}\n\n${text}` : text;
        store.remove(id);
      }
      store.update(keepId, { links, notes: notes || undefined });
      return demoOne(keepId);
    },
  );
}

/**
 * Demo data only, after two members became one (the server does this in the member merge):
 * the member kept stays in their household, the duplicate leaves theirs, and a household
 * the duplicate headed passes to the first to join it that is left, or goes when empty.
 */
export function demoMoveMemberLinks(keepId: string, dropIds: string[]): void {
  for (const dropId of dropIds) {
    const h = store.list().find((x) => x.links.some((l) => l.memberId === dropId));
    if (!h) continue;
    const keepIsIn = store.list().some((x) => x.links.some((l) => l.memberId === keepId));
    let links = keepIsIn
      ? h.links.filter((l) => l.memberId !== dropId)
      : h.links.map((l) => (l.memberId === dropId ? { ...l, memberId: keepId } : l));
    if (links.length > 0 && !links.some((l) => l.isPrimary)) {
      const first = [...links].sort((a, b) => a.addedAtUtc.localeCompare(b.addedAtUtc))[0]!;
      links = links.map((l) => (l.linkId === first.linkId ? { ...l, isPrimary: true, role: 'Primary' as HouseholdRole, rollUpBilling: false } : l));
    }
    if (links.length === 0) store.remove(h.id);
    else store.update(h.id, { links });
  }
}

/**
 * Downloads the household's statement for [fromUtc, toUtc): the primary's account, where
 * the bills that roll up land, each line saying whose it was. Demo data prints the page.
 */
export async function downloadHouseholdStatement(householdId: string, fromUtc: string, toUtc: string): Promise<void> {
  if (servesDemoData()) {
    window.print();
    return;
  }
  const res = await authFetch(
    `/api/households/${householdId}/statement.pdf?from=${encodeURIComponent(fromUtc)}&to=${encodeURIComponent(toUtc)}`);
  await ensureOk(res);
  const header = res.headers.get('Content-Disposition') ?? '';
  const star = /filename\*=UTF-8''([^;]+)/i.exec(header)?.[1];
  const name = star ? decodeURIComponent(star) : (/filename="?([^";]+)"?/i.exec(header)?.[1] ?? 'household-statement.pdf');
  downloadFile(await res.blob(), name);
}

// ── figures ──────────────────────────────────────────────────────────────────

/** A member's dues as a monthly figure. */
export function monthlyDues(amount: number | undefined, frequency: string | undefined): number {
  if (!amount) return 0;
  switch (frequency) {
    case 'Monthly':
      return amount;
    case 'Quarterly':
      return amount / 3;
    case 'SemiAnnual':
      return amount / 6;
    default:
      return amount / 12;
  }
}

/** What the household's members pay a month, and what their own accounts stand at. */
export function householdTotals(h: Household): { duesMonthly: number; balance: number } {
  let duesMonthly = 0;
  let balance = 0;
  for (const p of h.people) {
    if (!p.memberId) continue;
    duesMonthly += monthlyDues(p.duesAmount, p.duesFrequency);
    balance += p.houseBalance ?? 0;
  }
  return { duesMonthly: Math.round(duesMonthly * 100) / 100, balance: Math.round(balance * 100) / 100 };
}

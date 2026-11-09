import type { Contact, PagedResult, PaginationParams } from './types';
import { mockContacts, mockLeads } from './mockData';
import { servesDemoData, authFetchJson, authFetch, ensureOk } from './apiClient';
import { downloadCsv, downloadFile } from '@/app/lib/exportCsv';
import { createMockStore, mockId } from './mockStore';

const contactStore = createMockStore<Contact>({
  storageKey: 'crm.mock.contacts.v1',
  seed: mockContacts,
  idOf: (c) => c.id,
});

function getStored(): Contact[] | null {
  return contactStore.list();
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const LEADS_KEY = 'crm.mock.leads.v1';

function normalizeName(value?: string | null): string {
  return (value ?? '').trim().toLowerCase().split(/\s+/).filter(Boolean).join(' ');
}

function digits(value?: string | null): string {
  return (value ?? '').replace(/\D/g, '');
}

function readDemoLeads(): Array<{ id: string; name: string; email: string; phone?: string; companyId?: string; description?: string }> {
  if (typeof localStorage === 'undefined') return [];
  try {
    const raw = localStorage.getItem(LEADS_KEY);
    const parsed = raw ? JSON.parse(raw) : null;
    if (Array.isArray(parsed)) return parsed;
  } catch {
    return [];
  }
  return mockLeads;
}

/** Demo mode: every lead with a name and email also exists as a contact. */
export function ensureDemoPersonContacts(): void {
  const leads = readDemoLeads();
  const contacts = contactStore.list();
  for (const lead of leads) {
    const name = lead.name?.trim() ?? '';
    const email = lead.email?.trim() ?? '';
    if (!name || !email) continue;
    if (contacts.some((contact) => !contact.isArchived && contact.convertedFromLeadId === lead.id)) continue;
    const byEmail = contacts.find(
      (contact) => !contact.isArchived && contact.email.trim().toLowerCase() === email.toLowerCase(),
    );
    if (byEmail) {
      if (!byEmail.convertedFromLeadId) {
        const updated = contactStore.update(byEmail.id, { convertedFromLeadId: lead.id });
        if (updated) {
          const index = contacts.findIndex((contact) => contact.id === byEmail.id);
          if (index >= 0) contacts[index] = updated;
        }
      }
      continue;
    }
    contacts.unshift(contactStore.add({
      id: mockId('contact'),
      name,
      email,
      phone: lead.phone,
      companyId: lead.companyId,
      description: lead.description,
      convertedFromLeadId: lead.id,
      isArchived: false,
      createdAtUtc: new Date().toISOString(),
    }));
  }
}

function comparison(field: string, yours?: string | null, theirs?: string | null, same = false): ContactFieldComparison {
  return {
    field,
    yours: yours?.trim() ? yours.trim() : null,
    theirs: theirs?.trim() ? theirs.trim() : null,
    same,
  };
}

function isDemoLookalike(contact: Contact, other: Contact): boolean {
  const sameName = normalizeName(contact.name).length > 1 && normalizeName(contact.name) === normalizeName(other.name);
  const sameEmail = contact.email.trim().length > 0 && contact.email.trim().toLowerCase() === other.email.trim().toLowerCase();
  const contactPhone = digits(contact.phone);
  const samePhone = contactPhone.length >= 7 && contactPhone === digits(other.phone);
  return (sameEmail ? 2 : 0) + (sameName ? 1 : 0) + (samePhone ? 1 : 0) >= 2;
}

function reassignStoredContact(key: string, fromId: string, toId: string): void {
  if (typeof localStorage === 'undefined') return;
  try {
    const raw = localStorage.getItem(key);
    if (!raw) return;
    const list = JSON.parse(raw);
    if (!Array.isArray(list)) return;
    let changed = false;
    for (const row of list) {
      if (row && typeof row === 'object' && row.contactId === fromId) {
        row.contactId = toId;
        changed = true;
      }
    }
    if (changed) localStorage.setItem(key, JSON.stringify(list));
  } catch {
    // Demo storage can be missing. The real API moves these records itself.
  }
}

export interface ApiContact {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  jobTitle?: string | null;
  companyId?: string | null;
  lastActivityAtUtc?: string | null;
  convertedFromLeadId?: string | null;
  convertedAtUtc?: string | null;
  isArchived?: boolean;
  doNotContact?: boolean;
  preferredContactMethod?: string | null;
  companyName?: string | null;
  description?: string | null;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
}

export function mapContact(d: ApiContact): Contact {
  return {
    id: d.id,
    name: d.name,
    email: d.email,
    phone: d.phone ?? undefined,
    jobTitle: d.jobTitle ?? undefined,
    companyId: d.companyId ?? undefined,
    lastActivityAtUtc: d.lastActivityAtUtc ?? undefined,
    convertedFromLeadId: d.convertedFromLeadId ?? undefined,
    convertedAtUtc: d.convertedAtUtc ?? undefined,
    isArchived: d.isArchived ?? false,
    doNotContact: d.doNotContact ?? false,
    preferredContactMethod: d.preferredContactMethod ?? undefined,
    companyName: d.companyName ?? undefined,
    description: d.description ?? undefined,
    createdAtUtc: d.createdAtUtc ?? undefined,
    updatedAtUtc: d.updatedAtUtc ?? undefined,
  };
}

interface ApiPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export type ContactStatus = 'active' | 'archived' | 'all';
/** recent: activity in the last 30 days. quiet: none in the last 90 days, or none at all. */
export type ContactActivity = 'recent' | 'quiet';
export type ContactSortField = 'name' | 'email' | 'lastActivity' | 'createdAt';

/**
 * What the contacts list asks for. The server applies all of it to the whole workspace
 * before it takes a page; the page used to filter and sort the twenty contacts it had.
 */
export interface ContactListFilters {
  search?: string;
  /** Active by default. */
  status?: ContactStatus;
  companyId?: string;
  /** true: only contacts at a company; false: only those at none. */
  hasCompany?: boolean;
  activity?: ContactActivity;
  sortBy?: ContactSortField;
  sortDir?: 'asc' | 'desc';
}

/** The figures above the contacts list, over the whole workspace. All but `archived` count active contacts. */
export interface ContactStats {
  total: number;
  withCompany: number;
  withPhone: number;
  activeLast30Days: number;
  quietOver90Days: number;
  addedLast7Days: number;
  archived: number;
}

const DAY_MS = 24 * 60 * 60 * 1000;

function contactListQuery(filters: ContactListFilters): URLSearchParams {
  const query = new URLSearchParams();
  if (filters.search?.trim()) query.set('search', filters.search.trim());
  if (filters.status) query.set('status', filters.status);
  if (filters.companyId) query.set('companyId', filters.companyId);
  if (filters.hasCompany !== undefined) query.set('hasCompany', String(filters.hasCompany));
  if (filters.activity) query.set('activity', filters.activity);
  if (filters.sortBy) query.set('sortBy', filters.sortBy);
  if (filters.sortDir) query.set('sortDir', filters.sortDir);
  return query;
}

/** Demo mode: the same filters and order as the server, over this browser's contacts. */
function demoContactList(filters: ContactListFilters): Contact[] {
  ensureDemoPersonContacts();
  const now = Date.now();
  const last = (c: Contact) => (c.lastActivityAtUtc ? new Date(c.lastActivityAtUtc).getTime() : null);
  const status = filters.status ?? 'active';
  let rows = (getStored() ?? [...mockContacts]).filter((c) =>
    status === 'all' ? true : status === 'archived' ? !!c.isArchived : !c.isArchived);
  if (filters.companyId) rows = rows.filter((c) => c.companyId === filters.companyId);
  else if (filters.hasCompany !== undefined) rows = rows.filter((c) => !!c.companyId === filters.hasCompany);
  if (filters.search?.trim()) {
    const q = filters.search.trim().toLowerCase();
    rows = rows.filter((c) => c.name.toLowerCase().includes(q) || c.email.toLowerCase().includes(q)
      || (c.phone ?? '').includes(q) || (c.jobTitle ?? '').toLowerCase().includes(q));
  }
  if (filters.activity === 'recent') rows = rows.filter((c) => (last(c) ?? 0) >= now - 30 * DAY_MS);
  if (filters.activity === 'quiet') rows = rows.filter((c) => (last(c) ?? 0) < now - 90 * DAY_MS);
  const dir = filters.sortDir === 'desc' ? -1 : 1;
  const time = (v?: string) => (v ? new Date(v).getTime() : 0);
  const compare: Record<ContactSortField, (a: Contact, b: Contact) => number> = {
    name: (a, b) => a.name.localeCompare(b.name),
    email: (a, b) => a.email.localeCompare(b.email),
    lastActivity: (a, b) => (last(a) ?? -1) - (last(b) ?? -1),
    createdAt: (a, b) => time(a.createdAtUtc) - time(b.createdAtUtc),
  };
  return [...rows].sort((a, b) => dir * compare[filters.sortBy ?? 'name'](a, b) || a.name.localeCompare(b.name));
}

/** Get contacts with pagination, filters and order (real API or mock). */
export async function getContactsPaged(
  params: PaginationParams & ContactListFilters & { includeArchived?: boolean } = {}
): Promise<PagedResult<Contact>> {
  const { page = 1, pageSize = 20, includeArchived = false, ...filters } = params;
  const status = filters.status ?? (includeArchived ? 'all' : undefined);

  if (!servesDemoData()) {
    const queryParams = contactListQuery({ ...filters, status });
    queryParams.set('page', String(page));
    queryParams.set('pageSize', String(pageSize));
    const result = await authFetchJson<ApiPagedResult<ApiContact>>(`/api/contacts?${queryParams}`);
    return {
      items: Array.isArray(result?.items) ? result.items.map(mapContact) : [],
      totalCount: result?.totalCount ?? 0,
      page: result?.page ?? page,
      pageSize: result?.pageSize ?? pageSize,
      totalPages: result?.totalPages ?? 0,
      hasNextPage: result?.hasNextPage ?? false,
      hasPreviousPage: result?.hasPreviousPage ?? false,
    };
  }

  // Mock implementation with client-side pagination
  await delay(300);
  const stored = demoContactList({ ...filters, status });
  const totalCount = stored.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const startIndex = (page - 1) * pageSize;
  const items = stored.slice(startIndex, startIndex + pageSize);

  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages,
    hasNextPage: page < totalPages,
    hasPreviousPage: page > 1,
  };
}

/** The figures above the contacts list, counted over the whole workspace. */
export async function getContactStats(): Promise<ContactStats> {
  if (!servesDemoData()) {
    const stats = await authFetchJson<ContactStats>('/api/contacts/stats');
    return {
      total: stats?.total ?? 0,
      withCompany: stats?.withCompany ?? 0,
      withPhone: stats?.withPhone ?? 0,
      activeLast30Days: stats?.activeLast30Days ?? 0,
      quietOver90Days: stats?.quietOver90Days ?? 0,
      addedLast7Days: stats?.addedLast7Days ?? 0,
      archived: stats?.archived ?? 0,
    };
  }
  const all = demoContactList({ status: 'all' });
  const active = all.filter((c) => !c.isArchived);
  const now = Date.now();
  const last = (c: Contact) => (c.lastActivityAtUtc ? new Date(c.lastActivityAtUtc).getTime() : 0);
  return {
    total: active.length,
    withCompany: active.filter((c) => c.companyId).length,
    withPhone: active.filter((c) => c.phone?.trim()).length,
    activeLast30Days: active.filter((c) => last(c) >= now - 30 * DAY_MS).length,
    quietOver90Days: active.filter((c) => last(c) < now - 90 * DAY_MS).length,
    addedLast7Days: active.filter((c) => c.createdAtUtc && new Date(c.createdAtUtc).getTime() >= now - 7 * DAY_MS).length,
    archived: all.length - active.length,
  };
}

/** How many rows an export holds, and how many matched (the server writes at most 50,000). */
export interface ExportResult {
  exported: number;
  total: number;
}

/** Read an export's counts from its headers; a server from before them sends none. */
export function exportCounts(res: Response): ExportResult | null {
  const exported = Number(res.headers.get('X-Exported-Count'));
  const total = Number(res.headers.get('X-Total-Count'));
  return res.headers.has('X-Exported-Count') && Number.isFinite(exported)
    ? { exported, total: Number.isFinite(total) && res.headers.has('X-Total-Count') ? total : exported }
    : null;
}

/** Every contact the list shows with these filters, as a CSV download: all of them, not one page. */
export async function exportContacts(filters: ContactListFilters): Promise<ExportResult | null> {
  const day = new Date().toISOString().slice(0, 10);
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/export?${contactListQuery(filters)}`);
    await ensureOk(res);
    downloadFile(await res.blob(), `contacts-${day}.csv`);
    return exportCounts(res);
  }
  const rows = demoContactList(filters);
  downloadCsv(`contacts-${day}`, rows, [
    { header: 'Name', value: (c) => c.name },
    { header: 'Email', value: (c) => c.email },
    { header: 'Phone', value: (c) => c.phone },
    { header: 'Job title', value: (c) => c.jobTitle },
    { header: 'Company', value: (c) => c.companyName },
    { header: 'Do not contact', value: (c) => (c.doNotContact ? 'Yes' : 'No') },
    { header: 'Archived', value: (c) => (c.isArchived ? 'Yes' : 'No') },
    { header: 'Last activity', value: (c) => c.lastActivityAtUtc },
    { header: 'Added', value: (c) => c.createdAtUtc },
  ]);
  return { exported: rows.length, total: rows.length };
}

/** Get all contacts (real API or mock) - non-paginated for backward compatibility. */
export async function getContacts(includeArchived = false): Promise<Contact[]> {
  if (!servesDemoData()) {
    const list = await authFetchJson<ApiContact[]>(`/api/contacts/all?includeArchived=${includeArchived}`);
    return Array.isArray(list) ? list.map(mapContact) : [];
  }
  await delay(300);
  ensureDemoPersonContacts();
  const stored = getStored();
  return (stored ?? [...mockContacts]).filter((contact) => includeArchived || !contact.isArchived);
}

/** Search contacts by name or email (non-paginated, for backward compatibility). */
export async function searchContacts(query: string, includeArchived = false): Promise<Contact[]> {
  if (!servesDemoData()) {
    const q = query?.trim() ? encodeURIComponent(query.trim()) : '';
    const list = await authFetchJson<ApiContact[]>(`/api/contacts/search?q=${q}&includeArchived=${includeArchived}`);
    return Array.isArray(list) ? list.map(mapContact) : [];
  }
  await delay(200);
  const contacts = await getContacts();
  const qLower = query.trim().toLowerCase();
  if (!qLower) return contacts;
  return contacts.filter(
    (c) =>
      c.name.toLowerCase().includes(qLower) ||
      c.email.toLowerCase().includes(qLower)
  );
}

/** Create a contact. Returns { contact, error } */
export async function createContact(params: {
  name: string;
  email: string;
  phone?: string;
  jobTitle?: string;
  companyId?: string;
  description?: string;
  /** The person asked not to be contacted. */
  doNotContact?: boolean;
}): Promise<{ contact: Contact | null; error: string | null }> {
  if (!servesDemoData()) {
    const res = await authFetch('/api/contacts', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    if (res.ok) {
      const data = await res.json() as ApiContact;
      return { contact: mapContact(data), error: null };
    }
    try {
      const err = await res.json() as { error?: string };
      return { contact: null, error: err.error ?? 'Failed to create contact' };
    } catch {
      return { contact: null, error: 'Failed to create contact' };
    }
  }
  await delay(200);
  const created: Contact = {
    id: mockId('contact'),
    name: params.name,
    email: params.email,
    phone: params.phone,
    jobTitle: params.jobTitle,
    companyId: params.companyId,
    description: params.description,
    doNotContact: params.doNotContact ?? false,
    isArchived: false,
  };
  contactStore.add(created);
  return { contact: created, error: null };
}

/** Update a contact. Returns { contact, error } */
export async function updateContact(
  id: string,
  params: Partial<{
    name: string;
    email: string;
    phone: string;
    jobTitle: string;
    companyId: string;
    description: string;
    doNotContact: boolean;
    preferredContactMethod: string;
  }>
): Promise<{ contact: Contact | null; error: string | null }> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    if (res.ok) {
      const data = await res.json() as ApiContact;
      return { contact: mapContact(data), error: null };
    }
    try {
      const err = await res.json() as { error?: string };
      return { contact: null, error: err.error ?? 'Failed to update contact' };
    } catch {
      return { contact: null, error: 'Failed to update contact' };
    }
  }
  await delay(200);
  const updated = contactStore.update(id, params as Partial<Contact>);
  return updated
    ? { contact: updated, error: null }
    : { contact: null, error: 'Contact not found' };
}

/** Delete a contact (hard delete). */
export async function deleteContact(id: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return contactStore.remove(id);
}

/** Archive a contact (soft delete). */
export async function archiveContact(id: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/${id}/archive`, { method: 'POST' });
    return res.status === 204;
  }
  await delay(200);
  return contactStore.update(id, { isArchived: true } as Partial<Contact>) !== null;
}

/** Unarchive a contact. */
export async function unarchiveContact(id: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/${id}/unarchive`, { method: 'POST' });
    return res.status === 204;
  }
  await delay(200);
  return contactStore.update(id, { isArchived: false } as Partial<Contact>) !== null;
}

export interface ContactFieldComparison {
  field: string;
  yours: string | null;
  theirs: string | null;
  same: boolean;
}

export interface ContactLookalike {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  fields: ContactFieldComparison[];
}

/** Load one contact. */
export async function getContactById(id: string): Promise<Contact | null> {
  if (!servesDemoData()) {
    const data = await authFetchJson<ApiContact>(`/api/contacts/${id}`);
    return data ? mapContact(data) : null;
  }
  await delay(150);
  return contactStore.byId(id) ?? null;
}

export async function getContactLookalikes(id: string): Promise<ContactLookalike[]> {
  if (!servesDemoData()) {
    const list = await authFetchJson<ContactLookalike[]>(`/api/contacts/${id}/lookalikes`);
    return Array.isArray(list) ? list : [];
  }
  ensureDemoPersonContacts();
  const contacts = contactStore.list();
  const contact = contacts.find((row) => row.id === id);
  if (!contact || contact.isArchived) return [];
  return contacts
    .filter((other) => other.id !== id && !other.isArchived && isDemoLookalike(contact, other))
    .slice(0, 5)
    .map((other) => ({
      id: other.id,
      name: other.name,
      email: other.email,
      phone: other.phone ?? null,
      fields: [
        comparison('Name', contact.name, other.name, normalizeName(contact.name) === normalizeName(other.name)),
        comparison('Email', contact.email, other.email, contact.email.trim().toLowerCase() === other.email.trim().toLowerCase()),
        comparison('Phone', contact.phone, other.phone, digits(contact.phone).length >= 7 && digits(contact.phone) === digits(other.phone)),
      ],
    }));
}

export async function mergePersonContacts(keepId: string, otherContactId: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/contacts/${keepId}/merge`, {
      method: 'POST',
      body: JSON.stringify({ otherContactId }),
    });
    return res.status === 204;
  }
  if (keepId === otherContactId) return false;
  const keep = contactStore.byId(keepId);
  const drop = contactStore.byId(otherContactId);
  if (!keep || !drop || keep.isArchived || drop.isArchived) return false;
  reassignStoredContact('crm.mock.deals.v1', otherContactId, keepId);
  reassignStoredContact('crm.mock.tasks.v1', otherContactId, keepId);
  reassignStoredContact('crm.mock.activities.v1', otherContactId, keepId);
  contactStore.update(keepId, {
    phone: keep.phone || drop.phone,
    companyId: keep.companyId || drop.companyId,
    jobTitle: keep.jobTitle || drop.jobTitle,
    convertedFromLeadId: keep.convertedFromLeadId || drop.convertedFromLeadId,
  });
  return contactStore.update(otherContactId, { isArchived: true, convertedFromLeadId: undefined }) !== null;
}

/** Check if an email already exists. */
export async function checkEmailExists(email: string, excludeId?: string): Promise<boolean> {
  if (!servesDemoData()) {
    const params = new URLSearchParams({ email });
    if (excludeId) params.append('excludeId', excludeId);
    const res = await authFetchJson<{ exists: boolean }>(`/api/contacts/check-email?${params}`);
    return res?.exists ?? false;
  }
  return false;
}

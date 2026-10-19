import type { Contact, PagedResult, PaginationParams } from './types';
import { mockContacts, mockLeads } from './mockData';
import { servesDemoData, authFetchJson, authFetch } from './apiClient';
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

interface ApiContact {
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

function mapContact(d: ApiContact): Contact {
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

/** Get contacts with pagination and optional search (real API or mock). */
export async function getContactsPaged(
  params: PaginationParams & { includeArchived?: boolean; companyId?: string } = {}
): Promise<PagedResult<Contact>> {
  const { page = 1, pageSize = 20, search, includeArchived = false, companyId } = params;
  
  if (!servesDemoData()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      includeArchived: String(includeArchived),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
    }
    if (companyId) {
      queryParams.set('companyId', companyId);
    }
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
  ensureDemoPersonContacts();
  let stored = (getStored() ?? [...mockContacts]).filter((contact) => includeArchived || !contact.isArchived);
  
  // Apply search filter
  if (search?.trim()) {
    const qLower = search.trim().toLowerCase();
    stored = stored.filter(
      (c) =>
        c.name.toLowerCase().includes(qLower) ||
        c.email.toLowerCase().includes(qLower)
    );
  }
  
  // Apply pagination
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

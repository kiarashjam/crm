import type { Contact, PagedResult, PaginationParams } from './types';
import { mockContacts } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

const STORAGE_KEY = 'crm_contacts';

function getStored(): Contact[] | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as Contact[];
  } catch {
    return null;
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
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
  params: PaginationParams & { includeArchived?: boolean } = {}
): Promise<PagedResult<Contact>> {
  const { page = 1, pageSize = 20, search, includeArchived = false } = params;
  
  if (isUsingRealApi()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      includeArchived: String(includeArchived),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
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
  let stored = getStored() ?? [...mockContacts];
  
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
  if (isUsingRealApi()) {
    const list = await authFetchJson<ApiContact[]>(`/api/contacts/all?includeArchived=${includeArchived}`);
    return Array.isArray(list) ? list.map(mapContact) : [];
  }
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockContacts];
}

/** Search contacts by name or email (non-paginated, for backward compatibility). */
export async function searchContacts(query: string, includeArchived = false): Promise<Contact[]> {
  if (isUsingRealApi()) {
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
}): Promise<{ contact: Contact | null; error: string | null }> {
  if (isUsingRealApi()) {
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
  return { contact: null, error: 'Mock mode - contact creation disabled' };
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
    doNotContact: boolean;
    preferredContactMethod: string;
  }>
): Promise<{ contact: Contact | null; error: string | null }> {
  if (isUsingRealApi()) {
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
  return { contact: null, error: 'Mock mode - contact update disabled' };
}

/** Delete a contact (hard delete). */
export async function deleteContact(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/contacts/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

/** Archive a contact (soft delete). */
export async function archiveContact(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/contacts/${id}/archive`, { method: 'POST' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

/** Unarchive a contact. */
export async function unarchiveContact(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/contacts/${id}/unarchive`, { method: 'POST' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

/** Check if an email already exists. */
export async function checkEmailExists(email: string, excludeId?: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const params = new URLSearchParams({ email });
    if (excludeId) params.append('excludeId', excludeId);
    const res = await authFetchJson<{ exists: boolean }>(`/api/contacts/check-email?${params}`);
    return res?.exists ?? false;
  }
  return false;
}

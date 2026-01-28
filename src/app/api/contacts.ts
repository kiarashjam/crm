import type { Contact } from './types';
import { mockContacts } from './mockData';
import { isUsingRealApi, authFetchJson } from './apiClient';

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

function mapContact(d: { id: string; name: string; email: string; companyId?: string | null }): Contact {
  return { id: d.id, name: d.name, email: d.email, companyId: d.companyId ?? undefined };
}

/** Get all contacts (real API or mock). */
export async function getContacts(): Promise<Contact[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; name: string; email: string; companyId?: string | null }[]>('/api/contacts');
    return Array.isArray(list) ? list.map(mapContact) : [];
  }
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockContacts];
}

/** Search contacts by name or email. */
export async function searchContacts(query: string): Promise<Contact[]> {
  if (isUsingRealApi()) {
    const q = query?.trim() ? encodeURIComponent(query.trim()) : '';
    const list = await authFetchJson<{ id: string; name: string; email: string; companyId?: string | null }[]>(`/api/contacts/search?q=${q}`);
    return Array.isArray(list) ? list.map(mapContact) : [];
  }
  await delay(200);
  const contacts = await getContacts();
  const q = query.trim().toLowerCase();
  if (!q) return contacts;
  return contacts.filter(
    (c) =>
      c.name.toLowerCase().includes(q) ||
      c.email.toLowerCase().includes(q)
  );
}

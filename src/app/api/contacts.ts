import type { Contact } from './types';
import { mockContacts } from './mockData';

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

function setStored(contacts: Contact[]) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(contacts));
  } catch {
    // ignore
  }
}

/** Get all contacts (demo: merge mock + any stored overrides). */
export async function getContacts(): Promise<Contact[]> {
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockContacts];
}

/** Search contacts by name or email. */
export async function searchContacts(query: string): Promise<Contact[]> {
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

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

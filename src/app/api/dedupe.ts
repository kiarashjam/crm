// Duplicate detection + merge for contacts and companies.
//
// Detection runs client-side over the loaded records (works in both modes).
// Merge uses a dedicated backend endpoint when available; in demo mode it
// fills empty fields on the primary from the duplicates, then deletes them.

import { authFetch, servesDemoData } from './apiClient';
import { getContacts, updateContact, deleteContact } from './contacts';
import { getCompanies, updateCompany, deleteCompany } from './companies';
import type { Contact, Company } from './types';

export interface DuplicateGroup<T> {
  key: string;
  reason: string;
  records: T[];
}

const norm = (s?: string) => (s ?? '').trim().toLowerCase();

function pushTo<T>(map: Map<string, T[]>, key: string, value: T) {
  const list = map.get(key);
  if (list) list.push(value);
  else map.set(key, [value]);
}

/** Same grouping the Duplicates page uses. Pure, so the sales list can share it. */
export function duplicateContactGroups<T extends { id: string; name?: string | null; email?: string | null }>(contacts: T[]): DuplicateGroup<T>[] {
  const groups: DuplicateGroup<Contact>[] = [];
  const grouped = new Set<string>();

  const byEmail = new Map<string, Contact[]>();
  for (const c of contacts) {
    const e = norm(c.email);
    if (e) pushTo(byEmail, e, c);
  }
  for (const [e, list] of byEmail) {
    if (list.length > 1) {
      groups.push({ key: `email:${e}`, reason: 'Same email', records: list });
      list.forEach((c) => grouped.add(c.id));
    }
  }

  const byName = new Map<string, Contact[]>();
  for (const c of contacts) {
    if (grouped.has(c.id)) continue;
    const n = norm(c.name);
    if (n) pushTo(byName, n, c);
  }
  for (const [n, list] of byName) {
    if (list.length > 1) groups.push({ key: `name:${n}`, reason: 'Same name', records: list });
  }
  return groups;
}

export async function findDuplicateContacts(): Promise<DuplicateGroup<Contact>[]> {
  return duplicateContactGroups(await getContacts());
}

/** Same grouping the Duplicates page uses. Pure, so the sales list can share it. */
export function duplicateCompanyGroups<T extends { id: string; name?: string | null; domain?: string | null; website?: string | null }>(companies: T[]): DuplicateGroup<T>[] {
  const groups: DuplicateGroup<Company>[] = [];
  const grouped = new Set<string>();

  const byDomain = new Map<string, Company[]>();
  for (const c of companies) {
    const d = norm(c.domain || c.website).replace(/^https?:\/\//, '').replace(/^www\./, '').replace(/\/.*$/, '');
    if (d) pushTo(byDomain, d, c);
  }
  for (const [d, list] of byDomain) {
    if (list.length > 1) {
      groups.push({ key: `domain:${d}`, reason: 'Same domain', records: list });
      list.forEach((c) => grouped.add(c.id));
    }
  }

  const byName = new Map<string, Company[]>();
  for (const c of companies) {
    if (grouped.has(c.id)) continue;
    const n = norm(c.name);
    if (n) pushTo(byName, n, c);
  }
  for (const [n, list] of byName) {
    if (list.length > 1) groups.push({ key: `name:${n}`, reason: 'Same name', records: list });
  }
  return groups;
}

export async function findDuplicateCompanies(): Promise<DuplicateGroup<Company>[]> {
  return duplicateCompanyGroups(await getCompanies());
}

export async function mergeContacts(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  if (servesDemoData()) return mergeContactsLocal(primaryId, duplicateIds);
  for (const otherId of duplicateIds) {
    const res = await authFetch(`/api/contacts/${primaryId}/merge`, {
      method: 'POST',
      body: JSON.stringify({ otherContactId: otherId }),
    });
    if (!(res.status === 204 || res.ok)) return false;
  }
  return true;
}

async function mergeContactsLocal(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  const contacts = await getContacts();
  const primary = contacts.find((c) => c.id === primaryId);
  if (!primary) return false;
  const dupes = contacts.filter((c) => duplicateIds.includes(c.id));
  const filled: Partial<Pick<Contact, 'phone' | 'jobTitle' | 'companyId' | 'description' | 'preferredContactMethod'>> = {};
  (['phone', 'jobTitle', 'companyId', 'description', 'preferredContactMethod'] as const).forEach((f) => {
    if (!primary[f]) {
      const src = dupes.find((d) => d[f]);
      if (src && src[f]) filled[f] = src[f] as string;
    }
  });
  if (Object.keys(filled).length) await updateContact(primaryId, filled);
  for (const id of duplicateIds) await deleteContact(id);
  return true;
}

export async function mergeCompanies(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  if (servesDemoData()) return mergeCompaniesLocal(primaryId, duplicateIds);
  for (const otherId of duplicateIds) {
    const res = await authFetch(`/api/companies/${primaryId}/merge`, {
      method: 'POST',
      body: JSON.stringify({ otherCompanyId: otherId }),
    });
    if (!(res.status === 204 || res.ok)) return false;
  }
  return true;
}

async function mergeCompaniesLocal(primaryId: string, duplicateIds: string[]): Promise<boolean> {
  const companies = await getCompanies();
  const primary = companies.find((c) => c.id === primaryId);
  if (!primary) return false;
  const dupes = companies.filter((c) => duplicateIds.includes(c.id));
  const filled: Partial<Pick<Company, 'domain' | 'industry' | 'size' | 'description' | 'website' | 'location'>> = {};
  (['domain', 'industry', 'size', 'description', 'website', 'location'] as const).forEach((f) => {
    if (!primary[f]) {
      const src = dupes.find((d) => d[f]);
      if (src && src[f]) filled[f] = src[f] as string;
    }
  });
  if (Object.keys(filled).length) await updateCompany(primaryId, filled);
  for (const id of duplicateIds) await deleteCompany(id);
  return true;
}

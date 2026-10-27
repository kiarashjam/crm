import { servesDemoData, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getContacts } from './contacts';
import { getLeadById } from './leads';

/** What the other contact is to this one. */
export type RelationshipType =
  | 'spouse'
  | 'partner'
  | 'parent'
  | 'child'
  | 'sibling'
  | 'family'
  | 'friend'
  | 'colleague'
  | 'other';

/** In the order the picker offers them: the couple first. */
export const RELATIONSHIP_TYPES: { value: RelationshipType; label: string }[] = [
  { value: 'spouse', label: 'Spouse' },
  { value: 'partner', label: 'Partner' },
  { value: 'parent', label: 'Parent' },
  { value: 'child', label: 'Child' },
  { value: 'sibling', label: 'Sibling' },
  { value: 'family', label: 'Other family' },
  { value: 'friend', label: 'Friend' },
  { value: 'colleague', label: 'Colleague' },
  { value: 'other', label: 'Other' },
];

export function relationshipLabel(type: string): string {
  return RELATIONSHIP_TYPES.find((t) => t.value === type)?.label ?? type;
}

/** A spouse or partner: the two people the couples package is for. A contact has one at most. */
export function isCouple(type: string): boolean {
  return type === 'spouse' || type === 'partner';
}

/** One relationship, seen from the contact it was read for: the related contact is their `type`. */
export interface ContactRelationship {
  id: string;
  type: RelationshipType;
  relatedContactId: string;
  relatedContactName: string;
  relatedContactEmail?: string | null;
  relatedContactIsArchived: boolean;
  /** The lead the related contact came from, when there is one. */
  relatedLeadId?: string | null;
  createdAtUtc: string;
}

export interface ContactRelationshipList {
  /** The contact the relationships hang off. Null for a lead with no contact yet. */
  contactId: string | null;
  items: ContactRelationship[];
}

/** Whose relationships: a contact, or a lead (which works on the lead's own contact). */
export type RelationshipOwner = { contactId: string } | { leadId: string };

function basePath(owner: RelationshipOwner): string {
  return 'leadId' in owner ? `/api/leads/${owner.leadId}/relationships` : `/api/contacts/${owner.contactId}/relationships`;
}

// ---- demo mode ---------------------------------------------------------------

interface StoredRelationship {
  id: string;
  contactId: string;
  relatedContactId: string;
  type: RelationshipType;
  createdAtUtc: string;
}

const relationshipStore = createMockStore<StoredRelationship>({
  storageKey: 'crm.mock.contactRelationships.v1',
  seed: [],
  idOf: (row) => row.id,
});

const inverse = (type: RelationshipType): RelationshipType =>
  type === 'parent' ? 'child' : type === 'child' ? 'parent' : type;

async function demoContactId(owner: RelationshipOwner): Promise<string | null> {
  if ('contactId' in owner) return owner.contactId;
  const lead = await getLeadById(owner.leadId);
  if (!lead) return null;
  const contacts = await getContacts();
  const email = lead.email?.trim().toLowerCase();
  return contacts.find((c) => c.convertedFromLeadId === lead.id)?.id
    ?? (email ? contacts.find((c) => !c.isArchived && c.email?.trim().toLowerCase() === email)?.id : undefined)
    ?? null;
}

async function demoList(contactId: string): Promise<ContactRelationship[]> {
  const contacts = await getContacts();
  return relationshipStore
    .list()
    .filter((r) => r.contactId === contactId || r.relatedContactId === contactId)
    .map((r) => {
      const forward = r.contactId === contactId;
      const other = contacts.find((c) => c.id === (forward ? r.relatedContactId : r.contactId));
      return {
        id: r.id,
        type: forward ? r.type : inverse(r.type),
        relatedContactId: other?.id ?? (forward ? r.relatedContactId : r.contactId),
        relatedContactName: other?.name ?? 'Unknown contact',
        relatedContactEmail: other?.email ?? null,
        relatedContactIsArchived: !!other?.isArchived,
        relatedLeadId: other?.convertedFromLeadId ?? null,
        createdAtUtc: r.createdAtUtc,
      };
    })
    .sort(byCoupleThenName);
}

function demoCheck(contactId: string, relatedContactId: string, type: RelationshipType, exceptId?: string): void {
  if (relatedContactId === contactId) throw new Error('A contact cannot be related to themselves');
  const rows = relationshipStore.list().filter((r) => r.id !== exceptId);
  const involves = (id: string) => (r: StoredRelationship) => r.contactId === id || r.relatedContactId === id;
  if (rows.some((r) => involves(contactId)(r) && involves(relatedContactId)(r)))
    throw new Error('These two are already related. Change that relationship instead');
  if (isCouple(type) && rows.some((r) => isCouple(r.type) && (involves(contactId)(r) || involves(relatedContactId)(r))))
    throw new Error('One of them already has a spouse or partner recorded. Remove that one first');
}

// ---- API ---------------------------------------------------------------------

export function byCoupleThenName(a: ContactRelationship, b: ContactRelationship): number {
  const rank = (r: ContactRelationship) => (isCouple(r.type) ? 0 : 1);
  return rank(a) - rank(b) || a.relatedContactName.localeCompare(b.relatedContactName);
}

export async function getRelationships(owner: RelationshipOwner): Promise<ContactRelationshipList> {
  if (!servesDemoData()) {
    const list = await authFetchJson<ContactRelationshipList>(basePath(owner));
    return { contactId: list?.contactId ?? null, items: Array.isArray(list?.items) ? list.items : [] };
  }
  const contactId = await demoContactId(owner);
  return { contactId, items: contactId ? await demoList(contactId) : [] };
}

export async function addRelationship(
  owner: RelationshipOwner,
  relatedContactId: string,
  type: RelationshipType,
): Promise<ContactRelationship> {
  if (!servesDemoData()) {
    return authFetchJson<ContactRelationship>(basePath(owner), {
      method: 'POST',
      body: JSON.stringify({ relatedContactId, type }),
    });
  }
  const contactId = await demoContactId(owner);
  if (!contactId) throw new Error('This lead needs a name and an email before relationships can be recorded');
  demoCheck(contactId, relatedContactId, type);
  const row = relationshipStore.add({ id: mockId(), contactId, relatedContactId, type, createdAtUtc: new Date().toISOString() });
  return (await demoList(contactId)).find((r) => r.id === row.id)!;
}

export async function updateRelationship(
  owner: RelationshipOwner,
  relationshipId: string,
  relatedContactId: string,
  type: RelationshipType,
): Promise<ContactRelationship> {
  if (!servesDemoData()) {
    return authFetchJson<ContactRelationship>(`${basePath(owner)}/${relationshipId}`, {
      method: 'PUT',
      body: JSON.stringify({ relatedContactId, type }),
    });
  }
  const contactId = await demoContactId(owner);
  if (!contactId) throw new Error('The relationship was not found');
  demoCheck(contactId, relatedContactId, type, relationshipId);
  relationshipStore.update(relationshipId, { contactId, relatedContactId, type });
  return (await demoList(contactId)).find((r) => r.id === relationshipId)!;
}

export async function removeRelationship(owner: RelationshipOwner, relationshipId: string): Promise<void> {
  if (!servesDemoData()) {
    await authFetchJson<void>(`${basePath(owner)}/${relationshipId}`, { method: 'DELETE' });
    return;
  }
  relationshipStore.remove(relationshipId);
}

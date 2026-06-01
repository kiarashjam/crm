// Custom fields.
//
// Lets each workspace extend the built-in entities (lead/contact/company/deal)
// with their own fields. Definitions live per entity type; values are stored
// per record. The backend persists both; demo mode keeps them in localStorage.

import { isUsingRealApi, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CustomFieldEntity = 'lead' | 'contact' | 'company' | 'deal';
export type CustomFieldType = 'text' | 'number' | 'date' | 'select' | 'checkbox' | 'url';

export interface CustomFieldDef {
  id: string;
  entityType: CustomFieldEntity;
  key: string;
  label: string;
  type: CustomFieldType;
  options?: string[];
  required?: boolean;
  order: number;
}

export type CustomFieldValue = string | number | boolean | null;
export type CustomFieldValues = Record<string, CustomFieldValue>;

interface ValuesRow {
  id: string; // `${entityType}:${recordId}`
  entityType: CustomFieldEntity;
  recordId: string;
  values: CustomFieldValues;
}

const defStore = createMockStore<CustomFieldDef>({
  storageKey: 'crm.mock.customFieldDefs.v1',
  seed: [
    { id: 'cf-seed-1', entityType: 'contact', key: 'linkedin', label: 'LinkedIn', type: 'url', order: 0 },
    { id: 'cf-seed-2', entityType: 'lead', key: 'budget', label: 'Budget', type: 'number', order: 0 },
    { id: 'cf-seed-3', entityType: 'deal', key: 'source_channel', label: 'Source channel', type: 'select', options: ['Inbound', 'Outbound', 'Referral', 'Event'], order: 0 },
  ],
  idOf: (d) => d.id,
});

const valuesStore = createMockStore<ValuesRow>({
  storageKey: 'crm.mock.customFieldValues.v1',
  seed: [],
  idOf: (r) => r.id,
});

function slugify(label: string): string {
  return label.toLowerCase().trim().replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'field';
}

// ---- Definitions ----

export async function getFieldDefinitions(entityType?: CustomFieldEntity): Promise<CustomFieldDef[]> {
  if (isUsingRealApi()) {
    const q = entityType ? `?entityType=${entityType}` : '';
    const res = await authFetchJson<CustomFieldDef[]>(`/api/custom-fields${q}`);
    return Array.isArray(res) ? res : [];
  }
  await delay(120);
  return defStore
    .list()
    .filter((d) => !entityType || d.entityType === entityType)
    .sort((a, b) => a.order - b.order);
}

export interface CustomFieldInput {
  entityType: CustomFieldEntity;
  label: string;
  type: CustomFieldType;
  options?: string[];
  required?: boolean;
}

export async function createFieldDefinition(input: CustomFieldInput): Promise<CustomFieldDef | null> {
  if (isUsingRealApi()) {
    return authFetchJson<CustomFieldDef>('/api/custom-fields', { method: 'POST', body: JSON.stringify(input) });
  }
  await delay(150);
  const existing = defStore.list().filter((d) => d.entityType === input.entityType);
  return defStore.add({
    id: mockId('cf'),
    entityType: input.entityType,
    key: `${slugify(input.label)}_${Math.random().toString(36).slice(2, 6)}`,
    label: input.label,
    type: input.type,
    options: input.options,
    required: input.required,
    order: existing.length,
  });
}

export async function updateFieldDefinition(id: string, patch: Partial<CustomFieldInput>): Promise<CustomFieldDef | null> {
  if (isUsingRealApi()) {
    return authFetchJson<CustomFieldDef>(`/api/custom-fields/${id}`, { method: 'PUT', body: JSON.stringify(patch) });
  }
  await delay(120);
  return defStore.update(id, patch);
}

export async function deleteFieldDefinition(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/custom-fields/${id}`, { method: 'DELETE' });
    return res.status === 204 || res.ok;
  }
  await delay(120);
  return defStore.remove(id);
}

// ---- Values ----

export async function getFieldValues(entityType: CustomFieldEntity, recordId: string): Promise<CustomFieldValues> {
  if (isUsingRealApi()) {
    try {
      const res = await authFetchJson<CustomFieldValues>(`/api/custom-fields/values?entityType=${entityType}&recordId=${recordId}`);
      return res ?? {};
    } catch {
      return {};
    }
  }
  await delay(80);
  return valuesStore.byId(`${entityType}:${recordId}`)?.values ?? {};
}

export async function saveFieldValues(entityType: CustomFieldEntity, recordId: string, values: CustomFieldValues): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch('/api/custom-fields/values', {
      method: 'PUT',
      body: JSON.stringify({ entityType, recordId, values }),
    });
    return res.ok;
  }
  await delay(120);
  const id = `${entityType}:${recordId}`;
  if (valuesStore.byId(id)) valuesStore.update(id, { values });
  else valuesStore.add({ id, entityType, recordId, values });
  return true;
}

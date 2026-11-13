// Custom fields.
//
// Lets each workspace extend its leads, contacts, companies, deals and club members with
// fields of its own. Definitions live per kind of record; each value is kept on the server
// as a typed row of its own, so lists can filter and sort by it (see flexFilters.ts). Demo
// mode keeps both in this browser.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CustomFieldEntity = 'lead' | 'contact' | 'company' | 'deal' | 'member';
export type CustomFieldType = 'text' | 'number' | 'money' | 'date' | 'select' | 'multiselect' | 'checkbox' | 'url' | 'link';

/** The kinds of record a link field can point at. */
export const LINK_TARGETS: CustomFieldEntity[] = ['lead', 'contact', 'company', 'deal', 'member'];

/** An amount and its currency, as a money field holds it. */
export interface MoneyValue {
  amount: number;
  currency: string;
}

/** Another record, as a link field holds it: its id, and its name when the server could read it. */
export interface LinkValue {
  id: string;
  name?: string | null;
}

export type CustomFieldValue = string | number | boolean | null | string[] | MoneyValue | LinkValue;
export type CustomFieldValues = Record<string, CustomFieldValue>;

export interface CustomFieldDef {
  id: string;
  entityType: CustomFieldEntity;
  key: string;
  label: string;
  type: CustomFieldType;
  options?: string[] | null;
  required?: boolean;
  order: number;
  /** What a new record starts with; null for none. */
  defaultValue?: CustomFieldValue;
  /** A money field's currency. */
  currency?: string | null;
  /** The kind of record a link field points at. */
  linkEntityType?: CustomFieldEntity | null;
}

export interface ValuesRow {
  id: string; // `${entityType}:${recordId}`
  entityType: CustomFieldEntity;
  recordId: string;
  values: CustomFieldValues;
}

/** Most records one batch read asks about, as the server takes them. */
export const VALUES_BATCH_SIZE = 500;

/** The sample fields demo mode starts with. Before stores were scoped, real browsers got them too. */
export const DEMO_FIELD_DEFS: readonly CustomFieldDef[] = [
  { id: 'cf-seed-1', entityType: 'contact', key: 'linkedin', label: 'LinkedIn', type: 'url', order: 0 },
  { id: 'cf-seed-2', entityType: 'lead', key: 'budget', label: 'Budget', type: 'number', order: 0 },
  { id: 'cf-seed-3', entityType: 'deal', key: 'source_channel', label: 'Source channel', type: 'select', options: ['Inbound', 'Outbound', 'Referral', 'Event'], order: 0 },
];

const defStore = createMockStore<CustomFieldDef>({
  storageKey: 'crm.mock.customFieldDefs.v1',
  seed: [...DEMO_FIELD_DEFS],
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

/** Whether a value is no value: nothing, blank text, no options picked. */
export function isEmptyValue(value: CustomFieldValue | undefined): boolean {
  if (value === null || value === undefined) return true;
  if (typeof value === 'string') return value.trim() === '';
  if (Array.isArray(value)) return value.length === 0;
  return false;
}

/** A value as one line of text, for a list's cell: "1200.00 CHF", "Golf, Wine", "Yes". */
export function valueText(field: Pick<CustomFieldDef, 'type'>, value: CustomFieldValue | undefined): string {
  if (isEmptyValue(value)) return '';
  if (Array.isArray(value)) return value.join(', ');
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (typeof value === 'object' && value !== null) {
    if ('amount' in value) return `${Number(value.amount).toFixed(2)} ${value.currency ?? ''}`.trim();
    if ('id' in value) return value.name ?? value.id;
  }
  if (field.type === 'checkbox') return value ? 'Yes' : 'No';
  return String(value);
}

// ---- Definitions ----

export async function getFieldDefinitions(entityType?: CustomFieldEntity, req?: RequestOptions): Promise<CustomFieldDef[]> {
  return apiWithFallback(
    async () => {
      const q = entityType ? `?entityType=${entityType}` : '';
      const res = await authFetchJson<CustomFieldDef[]>(`/api/custom-fields${q}`, req);
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(120);
      return defStore.list().filter((d) => !entityType || d.entityType === entityType).sort((a, b) => a.order - b.order);
    },
  );
}

export interface CustomFieldInput {
  entityType: CustomFieldEntity;
  label: string;
  type: CustomFieldType;
  options?: string[];
  required?: boolean;
  defaultValue?: CustomFieldValue;
  currency?: string | null;
  linkEntityType?: CustomFieldEntity | null;
}

export async function createFieldDefinition(input: CustomFieldInput): Promise<CustomFieldDef | null> {
  return apiWithFallback(
    () => authFetchJson<CustomFieldDef>('/api/custom-fields', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
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
        defaultValue: input.defaultValue ?? null,
        currency: input.currency ?? null,
        linkEntityType: input.linkEntityType ?? null,
        order: existing.length,
      });
    },
  );
}

/**
 * Change a field: only what is sent changes. A default sent as null is taken off. A field
 * holding values cannot change its kind (the server answers with a conflict).
 */
export async function updateFieldDefinition(id: string, patch: Partial<Omit<CustomFieldInput, 'entityType'>>): Promise<CustomFieldDef | null> {
  return apiWithFallback(
    () => authFetchJson<CustomFieldDef>(`/api/custom-fields/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => { await delay(120); return defStore.update(id, patch as Partial<CustomFieldDef>); },
  );
}

/** Put one kind of record's fields in this order: every one of them, by id. */
export async function reorderFieldDefinitions(entityType: CustomFieldEntity, ids: string[]): Promise<CustomFieldDef[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<CustomFieldDef[]>('/api/custom-fields/order', { method: 'PUT', body: JSON.stringify({ entityType, ids }) });
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(80);
      ids.forEach((fieldId, order) => defStore.update(fieldId, { order }));
      return defStore.list().filter((d) => d.entityType === entityType).sort((a, b) => a.order - b.order);
    },
  );
}

export async function deleteFieldDefinition(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/custom-fields/${id}`, { method: 'DELETE' }); await ensureOk(res); return true; },
    async () => { await delay(120); return defStore.remove(id); },
  );
}

// ---- Values ----

export async function getFieldValues(entityType: CustomFieldEntity, recordId: string, req?: RequestOptions): Promise<CustomFieldValues> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<CustomFieldValues>(`/api/custom-fields/values?entityType=${entityType}&recordId=${recordId}`, req);
      return res ?? {};
    },
    async () => { await delay(80); return valuesStore.byId(`${entityType}:${recordId}`)?.values ?? {}; },
  );
}

/**
 * The values of many records at once, for a list's columns: asked in batches of
 * {@link VALUES_BATCH_SIZE}, as the server takes them. Records the caller cannot see, and
 * records with no values, are not in the answer.
 */
export async function getFieldValuesBatch(entityType: CustomFieldEntity, recordIds: string[], req?: RequestOptions): Promise<Record<string, CustomFieldValues>> {
  const ids = [...new Set(recordIds)];
  if (ids.length === 0) return {};
  return apiWithFallback(
    async () => {
      const result: Record<string, CustomFieldValues> = {};
      for (let i = 0; i < ids.length; i += VALUES_BATCH_SIZE) {
        const slice = ids.slice(i, i + VALUES_BATCH_SIZE);
        const res = await authFetchJson<Record<string, CustomFieldValues>>('/api/custom-fields/values/batch', {
          method: 'POST', body: JSON.stringify({ entityType, recordIds: slice }), signal: req?.signal,
        });
        Object.assign(result, res ?? {});
      }
      return result;
    },
    async () => {
      const result: Record<string, CustomFieldValues> = {};
      for (const id of ids) {
        const row = valuesStore.byId(`${entityType}:${id}`);
        if (row) result[id] = row.values;
      }
      return result;
    },
  );
}

/**
 * Save a record's values. Send only the fields that changed: a field left out keeps its
 * value, so two people editing different fields of one record do not undo each other.
 * Null empties a field.
 */
export async function saveFieldValues(entityType: CustomFieldEntity, recordId: string, values: CustomFieldValues): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch('/api/custom-fields/values', { method: 'PUT', body: JSON.stringify({ entityType, recordId, values }) }); await ensureOk(res); return true; },
    async () => {
      await delay(120);
      const id = `${entityType}:${recordId}`;
      const current = valuesStore.byId(id)?.values ?? {};
      const merged: CustomFieldValues = { ...current };
      for (const [key, value] of Object.entries(values)) {
        if (isEmptyValue(value)) delete merged[key];
        else merged[key] = value;
      }
      if (valuesStore.byId(id)) valuesStore.update(id, { values: merged });
      else valuesStore.add({ id, entityType, recordId, values: merged });
      return true;
    },
  );
}

/** The keys whose value differs between two copies of a record's values, with the new value (null where it was emptied). */
export function changedValues(before: CustomFieldValues, after: CustomFieldValues): CustomFieldValues {
  const changed: CustomFieldValues = {};
  for (const key of new Set([...Object.keys(before), ...Object.keys(after)])) {
    const was = isEmptyValue(before[key]) ? null : before[key];
    const now = isEmptyValue(after[key]) ? null : after[key];
    if (JSON.stringify(was) !== JSON.stringify(now)) changed[key] = now ?? null;
  }
  return changed;
}

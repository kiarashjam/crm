// Tags: one coloured list per workspace, put on its leads, contacts, companies, deals and
// club members. Anyone who edits records can make a tag and put it on records; renaming,
// recolouring and deleting one is for whoever manages the workspace's settings. Demo mode
// keeps them in this browser.

import { apiWithFallback, authFetch, authFetchJson, ensureOk, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import type { CustomFieldEntity } from './customFields';

/** The kinds of record that carry tags: the same as carry custom fields. */
export type TagEntity = CustomFieldEntity;

/** The colours a tag can have; the server keeps the name, the page draws it. */
export const TAG_COLORS = ['slate', 'red', 'orange', 'amber', 'lime', 'green', 'teal', 'sky', 'blue', 'indigo', 'violet', 'pink'] as const;
export type TagColor = (typeof TAG_COLORS)[number];

export interface Tag {
  id: string;
  name: string;
  color: TagColor;
  /** How many records carry it. */
  uses: number;
}

export interface BulkTagResult {
  records: number;
  added: number;
  removed: number;
  skipped: number;
}

/** Most records one batch read asks about, as the server takes them. */
export const TAGS_BATCH_SIZE = 500;

interface RecordTagRow {
  id: string; // `${entityType}:${recordId}`
  tagIds: string[];
}

const tagStore = createMockStore<Tag>({ storageKey: 'crm.mock.tags.v1', seed: [], idOf: (t) => t.id });
const recordTagStore = createMockStore<RecordTagRow>({ storageKey: 'crm.mock.recordTags.v1', seed: [], idOf: (r) => r.id });

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function demoUses(): Map<string, number> {
  const uses = new Map<string, number>();
  for (const row of recordTagStore.list()) for (const id of row.tagIds) uses.set(id, (uses.get(id) ?? 0) + 1);
  return uses;
}

function demoSet(entityType: TagEntity, recordId: string, tagIds: string[]) {
  const id = `${entityType}:${recordId}`;
  if (recordTagStore.byId(id)) recordTagStore.update(id, { tagIds });
  else recordTagStore.add({ id, tagIds });
}

/** The workspace's tags, by name, with how many records carry each. */
export async function getTags(req?: RequestOptions): Promise<Tag[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<Tag[]>('/api/tags', req);
      return Array.isArray(res) ? res : [];
    },
    async () => {
      const uses = demoUses();
      return tagStore.list().map((t) => ({ ...t, uses: uses.get(t.id) ?? 0 })).sort((a, b) => a.name.localeCompare(b.name));
    },
  );
}

export async function createTag(input: { name: string; color?: TagColor }): Promise<Tag> {
  return apiWithFallback(
    () => authFetchJson<Tag>('/api/tags', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(80);
      const name = input.name.trim();
      if (tagStore.list().some((t) => t.name.toLowerCase() === name.toLowerCase())) throw new Error(`There is already a tag called ${name}.`);
      return tagStore.add({ id: mockId('tag'), name, color: input.color ?? 'slate', uses: 0 });
    },
  );
}

export async function updateTag(id: string, patch: { name?: string; color?: TagColor }): Promise<Tag | null> {
  return apiWithFallback(
    () => authFetchJson<Tag>(`/api/tags/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => { await delay(80); return tagStore.update(id, patch); },
  );
}

/** Delete a tag: it comes off every record that carries it. */
export async function deleteTag(id: string): Promise<boolean> {
  return apiWithFallback(
    async () => { const res = await authFetch(`/api/tags/${id}`, { method: 'DELETE' }); await ensureOk(res); return true; },
    async () => {
      await delay(80);
      for (const row of recordTagStore.list()) if (row.tagIds.includes(id)) recordTagStore.update(row.id, { tagIds: row.tagIds.filter((t) => t !== id) });
      return tagStore.remove(id);
    },
  );
}

/** The ids of the tags one record carries. */
export async function getRecordTags(entityType: TagEntity, recordId: string, req?: RequestOptions): Promise<string[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<string[]>(`/api/tags/records?entityType=${entityType}&recordId=${recordId}`, req);
      return Array.isArray(res) ? res : [];
    },
    async () => recordTagStore.byId(`${entityType}:${recordId}`)?.tagIds ?? [],
  );
}

/** Make these the tags a record carries: tags left out come off. Answers the ids it carries now. */
export async function setRecordTags(entityType: TagEntity, recordId: string, tagIds: string[]): Promise<string[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<string[]>('/api/tags/records', { method: 'PUT', body: JSON.stringify({ entityType, recordId, tagIds }) });
      return Array.isArray(res) ? res : tagIds;
    },
    async () => { await delay(60); demoSet(entityType, recordId, [...new Set(tagIds)]); return [...new Set(tagIds)]; },
  );
}

/**
 * The tags of many records at once, for a list: asked in batches of {@link TAGS_BATCH_SIZE}.
 * Records the caller cannot see, and records with no tags, are not in the answer.
 */
export async function getRecordTagsBatch(entityType: TagEntity, recordIds: string[], req?: RequestOptions): Promise<Record<string, string[]>> {
  const ids = [...new Set(recordIds)];
  if (ids.length === 0) return {};
  return apiWithFallback(
    async () => {
      const result: Record<string, string[]> = {};
      for (let i = 0; i < ids.length; i += TAGS_BATCH_SIZE) {
        const slice = ids.slice(i, i + TAGS_BATCH_SIZE);
        const res = await authFetchJson<Record<string, string[]>>('/api/tags/records/batch', {
          method: 'POST', body: JSON.stringify({ entityType, recordIds: slice }), signal: req?.signal,
        });
        Object.assign(result, res ?? {});
      }
      return result;
    },
    async () => {
      const result: Record<string, string[]> = {};
      for (const id of ids) {
        const tags = recordTagStore.byId(`${entityType}:${id}`)?.tagIds ?? [];
        if (tags.length) result[id] = tags;
      }
      return result;
    },
  );
}

/** Put tags on, and take tags off, many records of one kind at once. */
export async function bulkTag(input: { entityType: TagEntity; recordIds: string[]; add?: string[]; remove?: string[] }): Promise<BulkTagResult> {
  return apiWithFallback(
    () => authFetchJson<BulkTagResult>('/api/tags/bulk', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      let added = 0;
      let removed = 0;
      for (const recordId of new Set(input.recordIds)) {
        const current = new Set(recordTagStore.byId(`${input.entityType}:${recordId}`)?.tagIds ?? []);
        for (const tag of input.add ?? []) if (!current.has(tag)) { current.add(tag); added++; }
        for (const tag of input.remove ?? []) if (current.delete(tag)) removed++;
        demoSet(input.entityType, recordId, [...current]);
      }
      return { records: new Set(input.recordIds).size, added, removed, skipped: 0 };
    },
  );
}

// The workspace's own fields, its tags and its saved views, as cached queries keyed by
// workspace; and a list page's custom values and tags for the records it shows.

import { useQuery } from '@tanstack/react-query';
import {
  getFieldDefinitions, getFieldValues, getFieldValuesBatch, getRecordTags, getRecordTagsBatch, getSavedViews, getTags,
  type CustomFieldDef, type CustomFieldEntity, type CustomFieldValues, type SavedViewList, type Tag, type TagEntity,
} from '@/app/api';
import { queryKeys } from './queryKeys';
import { timed } from './queryClient';
import { useWorkspaceScope } from './useWorkspaceScope';

const NO_FIELDS: CustomFieldDef[] = [];
const NO_TAGS: Tag[] = [];

/** The workspace's custom fields of one kind of record, in their order. */
export function useFieldDefinitions(entity: CustomFieldEntity, enabled = true) {
  const ws = useWorkspaceScope();
  const query = useQuery({
    queryKey: queryKeys.customFields.definitions(ws, entity),
    queryFn: ({ signal }) => timed(signal, (s) => getFieldDefinitions(entity, { signal: s })),
    enabled,
  });
  return { ...query, fields: query.data ?? NO_FIELDS };
}

/** One record's custom values. */
export function useFieldValues(entity: CustomFieldEntity, recordId: string, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.customFields.values(ws, entity, recordId),
    queryFn: ({ signal }) => timed(signal, (s) => getFieldValues(entity, recordId, { signal: s })),
    enabled: enabled && !!recordId,
  });
}

/** The workspace's tags, by name. */
export function useTags(enabled = true) {
  const ws = useWorkspaceScope();
  const query = useQuery({
    queryKey: queryKeys.tags.all(ws),
    queryFn: ({ signal }) => timed(signal, (s) => getTags({ signal: s })),
    enabled,
  });
  return { ...query, tags: query.data ?? NO_TAGS };
}

/** The ids of the tags one record carries. */
export function useRecordTags(entity: TagEntity, recordId: string, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.tags.record(ws, entity, recordId),
    queryFn: ({ signal }) => timed(signal, (s) => getRecordTags(entity, recordId, { signal: s })),
    enabled: enabled && !!recordId,
  });
}

/** The views of a list the person can open. */
export function useSavedViews(list: SavedViewList, enabled = true) {
  const ws = useWorkspaceScope();
  return useQuery({
    queryKey: queryKeys.savedViews.list(ws, list),
    queryFn: ({ signal }) => timed(signal, (s) => getSavedViews(list, { signal: s })),
    enabled,
  });
}

const NO_VALUES: Record<string, CustomFieldValues> = {};
const NO_RECORD_TAGS: Record<string, string[]> = {};

/**
 * The custom values and tags of the records a list shows, for its columns: one batch read
 * of each for the page, not one per row. Nothing is read while `enabled` is false (no
 * column needs them) or there are no records.
 */
export function useFlexCells(entity: CustomFieldEntity, recordIds: readonly string[], enabled = true) {
  const ws = useWorkspaceScope();
  const ids = [...recordIds].sort();
  const on = enabled && ids.length > 0;
  const values = useQuery({
    queryKey: queryKeys.customFields.batch(ws, entity, ids),
    queryFn: ({ signal }) => timed(signal, (s) => getFieldValuesBatch(entity, ids, { signal: s })),
    enabled: on,
  });
  const tags = useQuery({
    queryKey: queryKeys.tags.batch(ws, entity, ids),
    queryFn: ({ signal }) => timed(signal, (s) => getRecordTagsBatch(entity, ids, { signal: s })),
    enabled: on,
  });
  return {
    values: values.data ?? NO_VALUES,
    tags: tags.data ?? NO_RECORD_TAGS,
    isError: values.isError || tags.isError,
    isPending: on && (values.isPending || tags.isPending),
    refetch: () => { void values.refetch(); void tags.refetch(); },
  };
}

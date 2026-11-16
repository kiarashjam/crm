// An export's columns for its records' custom fields and tags: one per field, in the
// workspace's order, then Tags (when the workspace has any), read in batches of 500 as the
// server takes them.

import {
  getFieldDefinitions, getFieldValuesBatch, getRecordTagsBatch, getTags, valueText,
  type CustomFieldEntity, type CustomFieldValues,
} from '@/app/api';

export interface FlexExportColumns {
  headers: string[];
  /** A record's cells in those columns, empty ones for a record with nothing in them. */
  cells: (recordId: string) => string[];
}

export const NO_FLEX_COLUMNS: FlexExportColumns = { headers: [], cells: () => [] };

export async function flexExportColumns(entity: CustomFieldEntity, recordIds: string[]): Promise<FlexExportColumns> {
  const [fields, tags] = await Promise.all([getFieldDefinitions(entity), getTags()]);
  if (fields.length === 0 && tags.length === 0) return NO_FLEX_COLUMNS;
  const [values, recordTags] = await Promise.all([
    fields.length > 0 ? getFieldValuesBatch(entity, recordIds) : Promise.resolve<Record<string, CustomFieldValues>>({}),
    tags.length > 0 ? getRecordTagsBatch(entity, recordIds) : Promise.resolve<Record<string, string[]>>({}),
  ]);
  const names = new Map(tags.map((t) => [t.id, t.name]));
  return {
    headers: [...fields.map((f) => f.label), ...(tags.length > 0 ? ['Tags'] : [])],
    cells: (id) => [
      ...fields.map((f) => valueText(f, values[id]?.[f.key])),
      ...(tags.length > 0
        ? [(recordTags[id] ?? []).map((t) => names.get(t)).filter((n): n is string => !!n).sort((a, b) => a.localeCompare(b)).join(', ')]
        : []),
    ],
  };
}

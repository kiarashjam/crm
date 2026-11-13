import { useMemo } from 'react';
import { valueText, type CustomFieldEntity } from '@/app/api';
import { useFieldDefinitions, useFlexCells, useTags } from '@/app/hooks/queries';
import type { ViewPreference } from '@/app/lib/viewPreferences';
import type { RecordColumn } from '@/app/components/views/RecordViews';
import { TagChips } from './TagChip';

/** The column id of a custom field: "cf:tier", as a saved view and a sort name it. */
export const fieldColumnId = (key: string) => `cf:${key}`;
export const TAGS_COLUMN = 'tags';

/**
 * A list's columns for its records' custom fields and tags, to add to its own: off until
 * the person turns them on (the tags one on when the workspace has tags). Their values are
 * read for the page shown in one batch each, and only while one of them is shown.
 */
export function useFlexColumns<T>(entity: CustomFieldEntity, items: readonly T[], getId: (item: T) => string, view: ViewPreference) {
  const { fields } = useFieldDefinitions(entity);
  const { tags } = useTags();
  const anyShown = fields.some((f) => view.shows(fieldColumnId(f.key), false)) || view.shows(TAGS_COLUMN, tags.length > 0);
  const ids = useMemo(() => items.map(getId), [items, getId]);
  const cells = useFlexCells(entity, ids, anyShown && (fields.length > 0 || tags.length > 0));

  return useMemo<RecordColumn<T>[]>(() => {
    const failed = cells.isError ? <span className="text-xs text-red-700">—</span> : null;
    const columns: RecordColumn<T>[] = fields.map((f) => ({
      id: fieldColumnId(f.key),
      label: f.label,
      byDefault: false,
      numeric: f.type === 'number' || f.type === 'money',
      cell: (item: T) => failed ?? (valueText(f, cells.values[getId(item)]?.[f.key]) || '—'),
    }));
    if (tags.length > 0) {
      columns.push({
        id: TAGS_COLUMN,
        label: 'Tags',
        byDefault: true,
        cell: (item: T) => failed ?? <TagChips ids={cells.tags[getId(item)] ?? []} tags={tags} />,
      });
    }
    return columns;
  }, [fields, tags, cells.values, cells.tags, cells.isError, getId]);
}

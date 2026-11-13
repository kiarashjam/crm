import { useMemo } from 'react';
import { isEmptyValue, valueText, type CustomFieldDef, type CustomFieldEntity, type CustomFieldValue } from '@/app/api';
import { useFieldDefinitions, useFlexCells, useTags } from '@/app/hooks/queries';
import { useMoney } from '@/app/hooks/useMoney';
import { currentDateLocale, currentNumberLocale } from '@/app/i18n/current';
import type { ViewPreference } from '@/app/lib/viewPreferences';
import type { RecordColumn } from '@/app/components/views/RecordViews';
import { TagChips } from './TagChip';

/**
 * A value as a list's cell shows it: money in the workspace's way of writing amounts (as every
 * other amount on the page), numbers and dates in its locale; the rest as an export writes it.
 */
export function cellText(
  field: Pick<CustomFieldDef, 'type'>, value: CustomFieldValue | undefined, formatMoney: (amount: number, currency?: string) => string,
): string {
  if (isEmptyValue(value)) return '';
  if (field.type === 'money' && value && typeof value === 'object' && 'amount' in value && Number.isFinite(Number(value.amount))) {
    return formatMoney(Number(value.amount), value.currency || undefined);
  }
  if (field.type === 'number' && typeof value === 'number') return value.toLocaleString(currentNumberLocale(), { maximumFractionDigits: 8 });
  if (field.type === 'date' && typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    // A day with no time: written as that day wherever the reader is.
    const day = new Date(`${value}T00:00:00Z`);
    if (!Number.isNaN(day.getTime())) return day.toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'UTC' });
  }
  return valueText(field, value);
}

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
  const money = useMoney();
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
      cell: (item: T) => failed ?? (cellText(f, cells.values[getId(item)]?.[f.key], money.format) || '—'),
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
  }, [fields, tags, cells.values, cells.tags, cells.isError, getId, money.format]);
}

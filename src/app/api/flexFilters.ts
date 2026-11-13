// A list's conditions on its records' tags and custom fields, as the server reads them.
//
// The lists (contacts, companies, leads, the deals board, members) take them in their query
// string, and run them over every record before they take a page:
//
//   tag=<id>            repeated: records with any of these tags (all of them with tagMatch=all)
//   notTag=<id>         repeated: records with none of these
//   field=key:op:value  repeated: each must hold — tier:is:Gold, budget:gte:1000,
//                       joined:between:2026-01-01..2026-06-30, interests:any:Golf|Wine,
//                       vip:true, notes:empty
//   sortBy=cf:<key>     with the list's sortDir: by a field, records without a value last
//
// The page keeps the same parameters in its own address, so a filtered list survives a
// reload, can be shared, and is what a saved view keeps.

import type { CustomFieldDef, CustomFieldType } from './customFields';

export interface FlexFilters {
  /** Tag ids: records with any of them, or all of them with `tagMatch: 'all'`. */
  tags?: string[];
  tagMatch?: 'any' | 'all';
  /** Tag ids: records with none of them. */
  notTags?: string[];
  /** Conditions, each "key:op[:value]", all of which must hold. */
  fields?: string[];
}

/** The prefix of a sort by a custom field: "cf:tier". */
export const FIELD_SORT_PREFIX = 'cf:';

export function hasFlexFilters(filters: FlexFilters | undefined): boolean {
  return !!filters && ((filters.tags?.length ?? 0) > 0 || (filters.notTags?.length ?? 0) > 0 || (filters.fields?.length ?? 0) > 0);
}

/** Add the conditions to a list's query string. */
export function appendFlexFilters(query: URLSearchParams, filters: FlexFilters | undefined): URLSearchParams {
  if (!filters) return query;
  for (const tag of filters.tags ?? []) query.append('tag', tag);
  if ((filters.tags?.length ?? 0) > 1 && filters.tagMatch === 'all') query.set('tagMatch', 'all');
  for (const tag of filters.notTags ?? []) query.append('notTag', tag);
  for (const field of filters.fields ?? []) query.append('field', field);
  return query;
}

/** The conditions a page's address holds, under the same names the server reads. */
export function readFlexParams(params: URLSearchParams): FlexFilters {
  const tags = params.getAll('tag').filter(Boolean);
  const notTags = params.getAll('notTag').filter(Boolean);
  const fields = params.getAll('field').filter(Boolean);
  return {
    ...(tags.length ? { tags } : {}),
    ...(tags.length > 1 && params.get('tagMatch') === 'all' ? { tagMatch: 'all' as const } : {}),
    ...(notTags.length ? { notTags } : {}),
    ...(fields.length ? { fields } : {}),
  };
}

/** The page's address with these conditions in place of the ones it had, back on the first page. */
export function writeFlexParams(params: URLSearchParams, filters: FlexFilters): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const name of ['tag', 'notTag', 'tagMatch', 'field']) next.delete(name);
  appendFlexFilters(next, filters);
  if (next.has('page')) next.set('page', '1');
  return next;
}

// ── field conditions ─────────────────────────────────────────────────────────

export interface FieldOp {
  op: string;
  label: string;
  /** What the value is: none, one, a range, or several of the field's options. */
  takes: 'none' | 'one' | 'range' | 'many';
}

const EMPTY: FieldOp[] = [
  { op: 'set', label: 'has a value', takes: 'none' },
  { op: 'empty', label: 'is empty', takes: 'none' },
];

const TEXT: FieldOp[] = [
  { op: 'contains', label: 'contains', takes: 'one' },
  { op: 'is', label: 'is', takes: 'one' },
  { op: 'not', label: 'is not', takes: 'one' },
  ...EMPTY,
];

const CHOICE: FieldOp[] = [
  { op: 'is', label: 'is', takes: 'one' },
  { op: 'not', label: 'is not', takes: 'one' },
  { op: 'any', label: 'is any of', takes: 'many' },
  { op: 'none', label: 'is none of', takes: 'many' },
  ...EMPTY,
];

const NUMBER: FieldOp[] = [
  { op: 'gte', label: 'is at least', takes: 'one' },
  { op: 'lte', label: 'is at most', takes: 'one' },
  { op: 'gt', label: 'is over', takes: 'one' },
  { op: 'lt', label: 'is under', takes: 'one' },
  { op: 'eq', label: 'equals', takes: 'one' },
  { op: 'between', label: 'is between', takes: 'range' },
  ...EMPTY,
];

const DATE: FieldOp[] = [
  { op: 'after', label: 'is after', takes: 'one' },
  { op: 'before', label: 'is before', takes: 'one' },
  { op: 'on', label: 'is on', takes: 'one' },
  { op: 'between', label: 'is between', takes: 'range' },
  ...EMPTY,
];

const CHECKBOX: FieldOp[] = [
  { op: 'true', label: 'is ticked', takes: 'none' },
  { op: 'false', label: 'is not ticked', takes: 'none' },
];

const MULTI: FieldOp[] = [
  { op: 'any', label: 'has any of', takes: 'many' },
  { op: 'all', label: 'has all of', takes: 'many' },
  { op: 'none', label: 'has none of', takes: 'many' },
  ...EMPTY,
];

/** The comparisons a field of this kind offers in a list's filters. */
export function opsFor(type: CustomFieldType): FieldOp[] {
  switch (type) {
    case 'number':
    case 'money': return NUMBER;
    case 'date': return DATE;
    case 'checkbox': return CHECKBOX;
    case 'select': return CHOICE;
    case 'multiselect': return MULTI;
    // A link is filtered by whether it is set: its record is picked on the record's page.
    case 'link': return EMPTY;
    default: return TEXT;
  }
}

/** A condition as the server reads it: "tier:is:Gold", "budget:between:100..500", "vip:true". */
export function fieldCondition(key: string, op: string, value?: string | string[] | [string, string]): string {
  const spec = Object.values({ TEXT, CHOICE, NUMBER, DATE, CHECKBOX, MULTI, EMPTY }).flat().find((o) => o.op === op);
  if (!spec || spec.takes === 'none' || value === undefined) return `${key}:${op}`;
  if (spec.takes === 'range' && Array.isArray(value)) return `${key}:${op}:${value[0] ?? ''}..${value[1] ?? ''}`;
  if (Array.isArray(value)) return `${key}:${op}:${value.join('|')}`;
  return `${key}:${op}:${value}`;
}

export interface ParsedCondition {
  key: string;
  op: string;
  value?: string;
  /** A between's upper end. */
  to?: string;
  /** The options of any of / none of / all of. */
  values: string[];
}

/** A condition read back, to show it; null when it cannot be read. */
export function parseFieldCondition(raw: string): ParsedCondition | null {
  const first = raw.indexOf(':');
  if (first <= 0) return null;
  const key = raw.slice(0, first);
  const rest = raw.slice(first + 1);
  const second = rest.indexOf(':');
  const op = (second < 0 ? rest : rest.slice(0, second)).toLowerCase();
  if (!op) return null;
  const value = second < 0 ? undefined : rest.slice(second + 1);
  if (op === 'between' && value?.includes('..')) {
    const [low, high] = value.split('..', 2);
    return { key, op, value: low, to: high, values: [] };
  }
  return { key, op, value, values: value ? value.split('|').filter(Boolean) : [] };
}

/** A condition in words, for its chip: "Tier is Gold", "Budget is between 100 and 500". */
export function describeCondition(raw: string, fields: CustomFieldDef[]): string {
  const parsed = parseFieldCondition(raw);
  if (!parsed) return raw;
  const field = fields.find((f) => f.key === parsed.key);
  const label = field?.label ?? parsed.key;
  const spec = opsFor(field?.type ?? 'text').find((o) => o.op === parsed.op)
    ?? Object.values({ TEXT, CHOICE, NUMBER, DATE, CHECKBOX, MULTI }).flat().find((o) => o.op === parsed.op);
  const words = spec?.label ?? parsed.op;
  if (!spec || spec.takes === 'none') return `${label} ${words}`;
  if (spec.takes === 'range') return `${label} ${words} ${parsed.value ?? ''} and ${parsed.to ?? ''}`;
  if (spec.takes === 'many') return `${label} ${words} ${parsed.values.join(', ')}`;
  return `${label} ${words} ${parsed.value ?? ''}`;
}

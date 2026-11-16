import { useMemo, useState } from 'react';
import { ArrowDownUp, Filter, Plus, Tag as TagIcon, X } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';
import {
  describeCondition, fieldCondition, hasFlexFilters, opsFor, readFlexParams, writeFlexParams,
  type CustomFieldDef, type CustomFieldEntity, type FlexFilters,
} from '@/app/api';
import { useFieldDefinitions, useTags } from '@/app/hooks/queries';
import { TagChip } from './TagChip';

interface FlexFilterBarProps {
  entity: CustomFieldEntity;
  /** The page's address parameters, where its filters live. */
  params: URLSearchParams;
  onChange: (next: URLSearchParams) => void;
  /** The custom field the list is sorted by, if it is one. */
  sortField?: string | null;
  sortDir?: 'asc' | 'desc';
  /** Sort by a custom field, or null to go back to the list's own order. Left out: no sort control. */
  onSortField?: (key: string | null, dir: 'asc' | 'desc') => void;
  className?: string;
}

const selectClass = 'mt-1 h-9 w-full rounded-md border border-slate-200 bg-white px-2 text-sm';

/**
 * A list's filters on its records' tags and custom fields, and its order by a field: they
 * live in the page's address under the names the server reads, so the server applies them
 * to every record, a filtered list survives a reload, and a saved view keeps them.
 */
export function FlexFilterBar({ entity, params, onChange, sortField, sortDir = 'asc', onSortField, className }: FlexFilterBarProps) {
  const { fields } = useFieldDefinitions(entity);
  const { tags } = useTags();
  const filters = useMemo(() => readFlexParams(params), [params]);
  const set = (next: FlexFilters) => onChange(writeFlexParams(params, next));

  if (fields.length === 0 && tags.length === 0) return null;
  const tagName = (id: string) => tags.find((t) => t.id === id);

  return (
    <div className={cn('flex flex-wrap items-center gap-2', className)} aria-label="Filters on tags and custom fields">
      {tags.length > 0 && <TagFilter filters={filters} onChange={set} />}
      {fields.length > 0 && <FieldFilter fields={fields} onAdd={(c) => set({ ...filters, fields: [...(filters.fields ?? []), c] })} />}
      {onSortField && fields.length > 0 && (
        <label className="flex items-center gap-1 text-xs text-slate-500">
          <ArrowDownUp className="h-3.5 w-3.5" aria-hidden />
          <span className="sr-only">Sort by a custom field</span>
          <select
            aria-label="Sort by a custom field"
            className="h-8 rounded-md border border-slate-200 bg-white px-2 text-xs"
            value={sortField ? `${sortField}:${sortDir}` : ''}
            onChange={(e) => {
              if (!e.target.value) { onSortField(null, 'asc'); return; }
              const [key, dir] = e.target.value.split(':');
              onSortField(key ?? null, dir === 'desc' ? 'desc' : 'asc');
            }}
          >
            <option value="">Sort by a field…</option>
            {fields.flatMap((f) => [
              <option key={`${f.key}:asc`} value={`${f.key}:asc`}>{f.label} ↑</option>,
              <option key={`${f.key}:desc`} value={`${f.key}:desc`}>{f.label} ↓</option>,
            ])}
          </select>
        </label>
      )}

      {/* What is applied, each taken off with its cross. */}
      {(filters.tags ?? []).map((id) => {
        const tag = tagName(id);
        return tag ? (
          <span key={`has-${id}`} className="flex items-center gap-1 text-xs text-slate-500">
            {filters.tagMatch === 'all' ? 'with all of' : 'with'}
            <TagChip tag={tag} onRemove={() => set({ ...filters, tags: filters.tags?.filter((t) => t !== id) })} />
          </span>
        ) : null;
      })}
      {(filters.notTags ?? []).map((id) => {
        const tag = tagName(id);
        return tag ? (
          <span key={`not-${id}`} className="flex items-center gap-1 text-xs text-slate-500">
            without <TagChip tag={tag} onRemove={() => set({ ...filters, notTags: filters.notTags?.filter((t) => t !== id) })} />
          </span>
        ) : null;
      })}
      {(filters.fields ?? []).map((raw) => (
        <span key={raw} className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2 py-0.5 text-xs text-indigo-800 ring-1 ring-inset ring-indigo-200">
          {describeCondition(raw, fields)}
          <button type="button" aria-label={`Remove the filter ${describeCondition(raw, fields)}`} className="rounded-full p-0.5 hover:bg-indigo-100"
            onClick={() => set({ ...filters, fields: filters.fields?.filter((f) => f !== raw) })}>
            <X className="h-3 w-3" aria-hidden />
          </button>
        </span>
      ))}
      {hasFlexFilters(filters) && (
        <Button variant="link" size="sm" className="h-auto p-0 text-xs" onClick={() => set({})}>Clear tag and field filters</Button>
      )}
    </div>
  );
}

/** Tags a record must have, or must not have, and whether it needs all of them. */
function TagFilter({ filters, onChange }: { filters: FlexFilters; onChange: (next: FlexFilters) => void }) {
  const { tags } = useTags();
  const has = new Set(filters.tags ?? []);
  const not = new Set(filters.notTags ?? []);
  const count = has.size + not.size;

  const choose = (id: string, state: 'has' | 'not' | 'any') => {
    const nextHas = new Set(has);
    const nextNot = new Set(not);
    nextHas.delete(id);
    nextNot.delete(id);
    if (state === 'has') nextHas.add(id);
    if (state === 'not') nextNot.add(id);
    onChange({ ...filters, tags: [...nextHas], notTags: [...nextNot], tagMatch: nextHas.size > 1 ? filters.tagMatch : undefined });
  };

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs">
          <TagIcon className="h-3.5 w-3.5" aria-hidden /> Tags{count > 0 ? ` (${count})` : ''}
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-72 p-3" align="start">
        <ul className="max-h-64 space-y-1.5 overflow-y-auto" aria-label="Filter by tag">
          {tags.map((t) => {
            const state = has.has(t.id) ? 'has' : not.has(t.id) ? 'not' : 'any';
            return (
              <li key={t.id} className="flex items-center justify-between gap-2">
                <TagChip tag={t} />
                <div className="flex overflow-hidden rounded-md border border-slate-200 text-[11px]" role="group" aria-label={`Filter on ${t.name}`}>
                  {(['has', 'not'] as const).map((s) => (
                    <button
                      key={s}
                      type="button"
                      aria-pressed={state === s}
                      onClick={() => choose(t.id, state === s ? 'any' : s)}
                      className={cn('px-2 py-0.5', state === s ? 'bg-indigo-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-50')}
                    >
                      {s === 'has' ? 'With' : 'Without'}
                    </button>
                  ))}
                </div>
              </li>
            );
          })}
        </ul>
        {has.size > 1 && (
          <label className="mt-3 flex items-center gap-2 border-t border-slate-100 pt-2 text-xs text-slate-600">
            <input
              type="checkbox"
              checked={filters.tagMatch === 'all'}
              onChange={(e) => onChange({ ...filters, tagMatch: e.target.checked ? 'all' : undefined })}
            />
            Only records with all of these tags
          </label>
        )}
      </PopoverContent>
    </Popover>
  );
}

/** A condition on one custom field, built from its kind's comparisons. */
function FieldFilter({ fields, onAdd }: { fields: CustomFieldDef[]; onAdd: (condition: string) => void }) {
  const [open, setOpen] = useState(false);
  const [key, setKey] = useState(fields[0]?.key ?? '');
  const field = fields.find((f) => f.key === key) ?? fields[0];
  const ops = field ? opsFor(field.type) : [];
  const [op, setOp] = useState(ops[0]?.op ?? '');
  const spec = ops.find((o) => o.op === op) ?? ops[0];
  const [value, setValue] = useState('');
  const [to, setTo] = useState('');
  const [picked, setPicked] = useState<string[]>([]);

  const pickField = (next: string) => {
    setKey(next);
    const nextField = fields.find((f) => f.key === next);
    setOp(nextField ? opsFor(nextField.type)[0]?.op ?? '' : '');
    setValue(''); setTo(''); setPicked([]);
  };

  const ready = !!field && !!spec && (
    spec.takes === 'none'
    || (spec.takes === 'one' && value.trim() !== '')
    || (spec.takes === 'range' && value.trim() !== '' && to.trim() !== '')
    || (spec.takes === 'many' && picked.length > 0));

  const add = () => {
    if (!field || !spec || !ready) return;
    onAdd(spec.takes === 'none' ? fieldCondition(field.key, spec.op)
      : spec.takes === 'range' ? fieldCondition(field.key, spec.op, [value.trim(), to.trim()])
        : spec.takes === 'many' ? fieldCondition(field.key, spec.op, picked)
          : fieldCondition(field.key, spec.op, value.trim()));
    setValue(''); setTo(''); setPicked([]);
    setOpen(false);
  };

  const inputType = field?.type === 'number' || field?.type === 'money' ? 'number' : field?.type === 'date' ? 'date' : 'text';
  const choices = field?.options ?? [];

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs">
          <Filter className="h-3.5 w-3.5" aria-hidden /> Field filter
        </Button>
      </PopoverTrigger>
      <PopoverContent className="w-80 space-y-2 p-3" align="start">
        <div>
          <Label htmlFor="flex-field" className="text-xs">Field</Label>
          <select id="flex-field" className={selectClass} value={field?.key ?? ''} onChange={(e) => pickField(e.target.value)}>
            {fields.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
          </select>
        </div>
        <div>
          <Label htmlFor="flex-op" className="text-xs">Comparison</Label>
          <select id="flex-op" className={selectClass} value={spec?.op ?? ''} onChange={(e) => setOp(e.target.value)}>
            {ops.map((o) => <option key={o.op} value={o.op}>{o.label}</option>)}
          </select>
        </div>
        {spec?.takes === 'one' && (
          <div>
            <Label htmlFor="flex-value" className="text-xs">Value</Label>
            {field?.type === 'select' ? (
              <select id="flex-value" className={selectClass} value={value} onChange={(e) => setValue(e.target.value)}>
                <option value="">Pick…</option>
                {choices.map((c) => <option key={c} value={c}>{c}</option>)}
              </select>
            ) : (
              <Input id="flex-value" type={inputType} value={value} onChange={(e) => setValue(e.target.value)} className="mt-1 h-9" />
            )}
          </div>
        )}
        {spec?.takes === 'range' && (
          <div className="flex items-end gap-2">
            <div className="flex-1">
              <Label htmlFor="flex-from" className="text-xs">From</Label>
              <Input id="flex-from" type={inputType} value={value} onChange={(e) => setValue(e.target.value)} className="mt-1 h-9" />
            </div>
            <div className="flex-1">
              <Label htmlFor="flex-to" className="text-xs">To</Label>
              <Input id="flex-to" type={inputType} value={to} onChange={(e) => setTo(e.target.value)} className="mt-1 h-9" />
            </div>
          </div>
        )}
        {spec?.takes === 'many' && (
          <fieldset className="space-y-1">
            <legend className="text-xs font-medium">Options</legend>
            {choices.map((c) => (
              <label key={c} className="flex items-center gap-2 text-sm">
                <input type="checkbox" checked={picked.includes(c)}
                  onChange={(e) => setPicked(e.target.checked ? [...picked, c] : picked.filter((p) => p !== c))} />
                {c}
              </label>
            ))}
          </fieldset>
        )}
        <Button size="sm" className="w-full gap-1" disabled={!ready} onClick={add}>
          <Plus className="h-3.5 w-3.5" aria-hidden /> Add filter
        </Button>
      </PopoverContent>
    </Popover>
  );
}

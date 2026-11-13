import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { SlidersHorizontal, Loader2, Check, Search, X } from 'lucide-react';
import { toast } from 'sonner';
import { useQueryClient } from '@tanstack/react-query';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Button } from '@/app/components/ui/button';
import { Checkbox } from '@/app/components/ui/checkbox';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { getMembers } from '@/app/api/members';
import {
  changedValues, globalSearch, isEmptyValue, saveFieldValues, PERMISSION,
  type CustomFieldEntity, type CustomFieldDef, type CustomFieldValues, type CustomFieldValue, type LinkValue, type MoneyValue,
} from '@/app/api';
import { queryKeys, useFieldDefinitions, useFieldValues, useWorkspaceScope } from '@/app/hooks/queries';
import { useMay } from '@/app/hooks/useMay';

interface CustomFieldsCardProps {
  entityType: CustomFieldEntity;
  recordId: string;
  /** Optional wrapper class to fit the host page's card styling. */
  className?: string;
}

const RECORD_PATHS: Record<CustomFieldEntity, string> = {
  lead: '/leads', contact: '/contacts', company: '/companies', deal: '/deals', member: '/members',
};

const NO_VALUES: CustomFieldValues = {};

/**
 * This workspace's custom fields for a record, to read and change. Renders nothing when
 * no fields are defined for the kind of record, so it is safe on any detail page. Saving
 * sends only the fields that changed, so two people editing different fields of one record
 * do not undo each other.
 */
export default function CustomFieldsCard({ entityType, recordId, className }: CustomFieldsCardProps) {
  const may = useMay();
  // A member's values are club work; any other record's, records.edit — as the server asks.
  const canEdit = may(entityType === 'member' ? PERMISSION.ClubMembers : PERMISSION.EditRecords);
  const ws = useWorkspaceScope();
  const client = useQueryClient();
  const definitions = useFieldDefinitions(entityType);
  const stored = useFieldValues(entityType, recordId, definitions.fields.length > 0);
  const initial = stored.data ?? NO_VALUES;
  const [values, setValues] = useState<CustomFieldValues>(initial);
  const [saving, setSaving] = useState(false);

  // A fresh read (after a save elsewhere, or the first one) replaces what is shown, unless
  // the person is in the middle of changing it.
  const [editing, setEditing] = useState(false);
  useEffect(() => { if (!editing) setValues(initial); }, [initial, editing]);

  const changes = useMemo(() => changedValues(initial, values), [initial, values]);
  const dirty = Object.keys(changes).length > 0;
  const missing = definitions.fields.filter((f) => f.required && isEmptyValue(values[f.key]) && !isEmptyValue(initial[f.key]));

  const setField = (key: string, value: CustomFieldValue) => {
    setEditing(true);
    setValues((prev) => ({ ...prev, [key]: value }));
  };

  const save = async () => {
    setSaving(true);
    try {
      await saveFieldValues(entityType, recordId, changes);
      const merged = { ...initial };
      for (const [key, value] of Object.entries(changes)) {
        if (isEmptyValue(value)) delete merged[key];
        else merged[key] = value;
      }
      client.setQueryData(queryKeys.customFields.values(ws, entityType, recordId), merged);
      setEditing(false);
      toast.success('Custom fields saved');
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The custom fields could not be saved');
    } finally {
      setSaving(false);
    }
  };

  const failed = definitions.isError || stored.isError;
  const loading = definitions.isPending || (definitions.fields.length > 0 && stored.isPending);

  // Nothing defined for this kind of record: no empty card.
  if (!loading && !failed && definitions.fields.length === 0) return null;

  return (
    <div className={className ?? 'rounded-2xl border border-slate-200 bg-white p-5 shadow-sm'}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-700">
          <SlidersHorizontal className="h-4 w-4 text-indigo-500" /> Custom fields
        </h3>
        {dirty && canEdit && (
          <div className="flex items-center gap-1">
            <Button size="sm" variant="ghost" onClick={() => { setValues(initial); setEditing(false); }} disabled={saving} className="h-7 text-xs">
              Undo
            </Button>
            <Button size="sm" onClick={save} disabled={saving || missing.length > 0} className="h-7 gap-1 text-xs">
              {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />} Save
            </Button>
          </div>
        )}
      </div>
      {failed ? (
        <div className="py-3 text-sm text-slate-500" role="alert">
          The custom fields could not be loaded.{' '}
          <Button variant="link" size="sm" className="h-auto p-0" onClick={() => { void definitions.refetch(); void stored.refetch(); }}>
            Try again
          </Button>
        </div>
      ) : loading ? (
        <div className="py-4 text-center text-slate-500"><Loader2 className="mx-auto h-4 w-4 animate-spin" aria-label="Loading custom fields" /></div>
      ) : (
        <div className="space-y-3">
          {definitions.fields.map((f) => (
            <FieldEditor key={f.id} field={f} value={values[f.key]} disabled={!canEdit || saving} onChange={(v) => setField(f.key, v)} />
          ))}
          {missing.length > 0 && (
            <p className="text-xs text-red-700" role="alert">
              {missing.map((f) => f.label).join(', ')} {missing.length === 1 ? 'is' : 'are'} required and cannot be emptied.
            </p>
          )}
        </div>
      )}
    </div>
  );
}

function FieldLabel({ field, htmlFor }: { field: CustomFieldDef; htmlFor?: string }) {
  return (
    <Label htmlFor={htmlFor} className="text-xs text-slate-500">
      {field.label}{field.required && <span className="text-red-700" aria-label="required"> *</span>}
    </Label>
  );
}

function FieldEditor({ field: f, value: v, disabled, onChange }: {
  field: CustomFieldDef; value: CustomFieldValue | undefined; disabled: boolean; onChange: (value: CustomFieldValue) => void;
}) {
  const id = `cf-${f.id}`;
  switch (f.type) {
    case 'checkbox':
      return (
        <label className="mt-1 flex items-center gap-2 text-sm text-slate-700">
          <Checkbox checked={v === true} disabled={disabled} onCheckedChange={(c) => onChange(c === true)} />
          {f.label}{f.required && <span className="text-red-700" aria-label="required"> *</span>}
        </label>
      );
    case 'select':
      return (
        <div>
          <FieldLabel field={f} htmlFor={id} />
          <Select value={typeof v === 'string' ? v : ''} onValueChange={(val) => onChange(val)} disabled={disabled}>
            <SelectTrigger id={id} className="mt-1 h-9"><SelectValue placeholder="Select…" /></SelectTrigger>
            <SelectContent>
              {(f.options ?? []).map((opt) => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
            </SelectContent>
          </Select>
          {!f.required && typeof v === 'string' && v && !disabled && (
            <button type="button" className="mt-1 text-xs text-slate-500 hover:underline" onClick={() => onChange(null)}>Clear</button>
          )}
        </div>
      );
    case 'multiselect': {
      const picked = Array.isArray(v) ? v : [];
      return (
        <fieldset>
          <legend><FieldLabel field={f} /></legend>
          <div className="mt-1 flex flex-wrap gap-x-4 gap-y-1">
            {(f.options ?? []).map((opt) => (
              <label key={opt} className="flex items-center gap-1.5 text-sm text-slate-700">
                <Checkbox
                  checked={picked.includes(opt)}
                  disabled={disabled}
                  onCheckedChange={(c) => onChange(c === true ? [...picked, opt] : picked.filter((p) => p !== opt))}
                />
                {opt}
              </label>
            ))}
          </div>
        </fieldset>
      );
    }
    case 'money': {
      const money = v && typeof v === 'object' && 'amount' in v ? (v as MoneyValue) : null;
      const currency = money?.currency ?? f.currency ?? 'CHF';
      return (
        <div>
          <FieldLabel field={f} htmlFor={id} />
          <div className="mt-1 flex items-center gap-2">
            <Input
              id={id}
              type="number"
              step="0.01"
              inputMode="decimal"
              value={money ? String(money.amount) : ''}
              disabled={disabled}
              onChange={(e) => onChange(e.target.value === '' ? null : { amount: Number(e.target.value), currency })}
              className="h-9"
            />
            <span className="text-sm text-slate-500">{currency}</span>
          </div>
        </div>
      );
    }
    case 'link':
      return (
        <div>
          <FieldLabel field={f} />
          <LinkPicker field={f} value={v && typeof v === 'object' && 'id' in v ? (v as LinkValue) : null} disabled={disabled} onChange={onChange} />
        </div>
      );
    default:
      return (
        <div>
          <FieldLabel field={f} htmlFor={id} />
          <Input
            id={id}
            type={f.type === 'number' ? 'number' : f.type === 'date' ? 'date' : f.type === 'url' ? 'url' : 'text'}
            value={v == null || typeof v === 'object' ? '' : String(v)}
            disabled={disabled}
            onChange={(e) => onChange(f.type === 'number' ? (e.target.value === '' ? null : Number(e.target.value)) : e.target.value)}
            className="mt-1 h-9"
            placeholder={f.type === 'url' ? 'https://…' : ''}
          />
        </div>
      );
  }
}

/** Another record of the workspace, picked by searching its name. */
function LinkPicker({ field, value, disabled, onChange }: {
  field: CustomFieldDef; value: LinkValue | null; disabled: boolean; onChange: (value: CustomFieldValue) => void;
}) {
  const target = field.linkEntityType ?? 'contact';
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const [hits, setHits] = useState<LinkValue[]>([]);
  const [searching, setSearching] = useState(false);

  useEffect(() => {
    if (!open || query.trim().length < 2) { setHits([]); return; }
    let cancelled = false;
    setSearching(true);
    const timer = setTimeout(() => {
      const q = query.trim().toLowerCase();
      const found: Promise<LinkValue[]> = target === 'member'
        ? getMembers().then((members) => members
          .filter((m) => `${m.firstName} ${m.lastName}`.toLowerCase().includes(q))
          .slice(0, 10).map((m) => ({ id: m.id, name: `${m.firstName} ${m.lastName}`.trim() })))
        : globalSearch(query).then((r) => {
          const rows = target === 'lead' ? r.leads : target === 'company' ? r.companies : target === 'deal' ? r.deals : r.contacts;
          return rows.slice(0, 10).map((row) => ({ id: row.id, name: row.name ?? '' }));
        });
      found.then((rows) => { if (!cancelled) setHits(rows); })
        .catch(() => { if (!cancelled) setHits([]); })
        .finally(() => { if (!cancelled) setSearching(false); });
    }, 250);
    return () => { cancelled = true; clearTimeout(timer); };
  }, [open, query, target]);

  return (
    <div className="mt-1 flex items-center gap-2">
      {value ? (
        <Link to={`${RECORD_PATHS[target]}/${value.id}`} className="truncate text-sm text-indigo-700 hover:underline">{value.name || 'Open the record'}</Link>
      ) : (
        <span className="text-sm text-slate-400">None</span>
      )}
      {!disabled && (
        <>
          <Popover open={open} onOpenChange={setOpen}>
            <PopoverTrigger asChild>
              <Button variant="outline" size="sm" className="h-7 gap-1 text-xs"><Search className="h-3 w-3" aria-hidden /> {value ? 'Change' : 'Pick'}</Button>
            </PopoverTrigger>
            <PopoverContent className="w-72 p-2" align="start">
              <Input autoFocus value={query} onChange={(e) => setQuery(e.target.value)} placeholder={`Search ${target}s…`} className="h-8" />
              <ul className="mt-2 max-h-56 overflow-y-auto" aria-label="Matching records">
                {searching && <li className="px-2 py-1 text-xs text-slate-500">Searching…</li>}
                {!searching && query.trim().length >= 2 && hits.length === 0 && <li className="px-2 py-1 text-xs text-slate-500">Nothing found.</li>}
                {hits.map((hit) => (
                  <li key={hit.id}>
                    <button type="button" className="w-full rounded px-2 py-1 text-left text-sm hover:bg-slate-100"
                      onClick={() => { onChange(hit); setOpen(false); setQuery(''); }}>
                      {hit.name || hit.id}
                    </button>
                  </li>
                ))}
              </ul>
            </PopoverContent>
          </Popover>
          {value && !field.required && (
            <Button variant="ghost" size="sm" className="h-7 w-7 p-0" onClick={() => onChange(null)} aria-label={`Clear ${field.label}`}>
              <X className="h-3.5 w-3.5" aria-hidden />
            </Button>
          )}
        </>
      )}
    </div>
  );
}

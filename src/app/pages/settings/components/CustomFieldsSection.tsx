import { useState } from 'react';
import { SlidersHorizontal, Plus, Trash2, Loader2, ArrowUp, ArrowDown, Pencil, Check, X } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Checkbox } from '@/app/components/ui/checkbox';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import {
  createFieldDefinition, deleteFieldDefinition, reorderFieldDefinitions, updateFieldDefinition, LINK_TARGETS, PERMISSION,
  type CustomFieldEntity, type CustomFieldType, type CustomFieldDef, type CustomFieldValue,
} from '@/app/api';
import { useFieldDefinitions } from '@/app/hooks/queries';
import { useModules } from '@/app/hooks/useModules';
import { useMay } from '@/app/hooks/useMay';
import { useRegional } from '@/app/hooks/useMoney';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { TagsSection } from './TagsSection';

const ENTITIES: { id: CustomFieldEntity; label: string; one: string }[] = [
  { id: 'lead', label: 'Leads', one: 'lead' },
  { id: 'contact', label: 'Contacts', one: 'contact' },
  { id: 'company', label: 'Companies', one: 'company' },
  { id: 'deal', label: 'Deals', one: 'deal' },
  { id: 'member', label: 'Members', one: 'member' },
];

const TYPES: { id: CustomFieldType; label: string }[] = [
  { id: 'text', label: 'Text' },
  { id: 'number', label: 'Number' },
  { id: 'money', label: 'Money' },
  { id: 'date', label: 'Date' },
  { id: 'url', label: 'URL' },
  { id: 'select', label: 'Dropdown' },
  { id: 'multiselect', label: 'Multiple choice' },
  { id: 'checkbox', label: 'Checkbox' },
  { id: 'link', label: 'Link to a record' },
];

const CURRENCIES = ['CHF', 'EUR', 'USD', 'GBP'];

/** The currencies a money field can be in here: the common ones, and the workspace's own first. */
const currenciesFor = (workspace: string) => [workspace, ...CURRENCIES.filter((c) => c !== workspace)];

const hasOptions = (type: CustomFieldType) => type === 'select' || type === 'multiselect';
/** The kinds a default can be typed for here; a link's or a money field's is set on a record instead. */
const takesDefault = (type: CustomFieldType) => ['text', 'number', 'date', 'url', 'select', 'checkbox'].includes(type);

function parseOptions(raw: string): string[] {
  return raw.split(',').map((o) => o.trim()).filter(Boolean);
}

/** A default as typed, as the field holds it; undefined for none. */
function defaultOf(type: CustomFieldType, raw: string, ticked: boolean): CustomFieldValue | undefined {
  if (type === 'checkbox') return ticked ? true : undefined;
  if (!raw.trim()) return undefined;
  if (type === 'number') return Number(raw);
  return raw.trim();
}

export function CustomFieldsSection() {
  const modules = useModules();
  const may = useMay();
  const canManage = may(PERMISSION.ManageSettings);
  const entities = ENTITIES.filter((e) => (e.id === 'member' ? modules.club : true));
  const [entity, setEntity] = useState<CustomFieldEntity>('lead');
  const { fields: defs, isPending, isError, refetch } = useFieldDefinitions(entity);
  usePageLoading(isPending);
  const [label, setLabel] = useState('');
  const [type, setType] = useState<CustomFieldType>('text');
  const [options, setOptions] = useState('');
  const [required, setRequired] = useState(false);
  // A money field is in the workspace's currency unless someone picks another, as its deals are
  // (read when the form is used, so a workspace that loads after the page still counts).
  const workspaceCurrency = useRegional().currency;
  const [pickedCurrency, setPickedCurrency] = useState<string | null>(null);
  const currency = pickedCurrency ?? workspaceCurrency;
  const [linkTo, setLinkTo] = useState<CustomFieldEntity>('contact');
  const [defaultText, setDefaultText] = useState('');
  const [defaultTicked, setDefaultTicked] = useState(false);
  const [creating, setCreating] = useState(false);
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);

  const reset = () => {
    setLabel(''); setOptions(''); setRequired(false); setType('text'); setDefaultText(''); setDefaultTicked(false);
    setPickedCurrency(null);
  };

  const add = async () => {
    if (!label.trim()) { toast.error('Field label is required'); return; }
    if (hasOptions(type) && parseOptions(options).length === 0) { toast.error('Add at least one option'); return; }
    setCreating(true);
    try {
      const res = await createFieldDefinition({
        entityType: entity,
        label: label.trim(),
        type,
        required,
        options: hasOptions(type) ? parseOptions(options) : undefined,
        currency: type === 'money' ? currency : undefined,
        linkEntityType: type === 'link' ? linkTo : undefined,
        defaultValue: takesDefault(type) ? defaultOf(type, defaultText, defaultTicked) : undefined,
      });
      if (!res) { toast.error('Failed to create field'); return; }
      toast.success('Custom field added');
      reset();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to create field');
    } finally {
      setCreating(false);
    }
  };

  const remove = async (f: CustomFieldDef) => {
    if (!confirm(`Delete the field "${f.label}"? Its value on every ${ENTITIES.find((e) => e.id === f.entityType)?.one ?? 'record'} goes with it.`)) return;
    setBusy(true);
    try {
      await deleteFieldDefinition(f.id);
      toast.success('Field deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to delete');
    } finally {
      setBusy(false);
    }
  };

  const move = async (index: number, by: -1 | 1) => {
    const order = defs.map((d) => d.id);
    const [moved] = order.splice(index, 1);
    if (!moved) return;
    order.splice(index + by, 0, moved);
    setBusy(true);
    try {
      await reorderFieldDefinitions(entity, order);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The fields could not be put in order');
      void refetch();
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-semibold text-slate-900">
          <SlidersHorizontal className="h-5 w-5 text-indigo-600" /> Custom fields
        </h2>
        <p className="mt-1 text-sm text-slate-500">
          Add your own fields to {entities.map((e) => e.label.toLowerCase()).join(', ')}. Lists can show them as columns, filter and sort by them, and automations can test them.
        </p>
      </div>

      {/* Entity switcher */}
      <div className="flex flex-wrap gap-2" role="tablist" aria-label="Kind of record">
        {entities.map((e) => (
          <button
            key={e.id}
            type="button"
            role="tab"
            aria-selected={entity === e.id}
            onClick={() => { setEntity(e.id); setEditingId(null); }}
            className={cn(
              'rounded-lg px-3 py-1.5 text-sm font-medium transition-colors',
              entity === e.id ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200',
            )}
          >
            {e.label}
          </button>
        ))}
      </div>

      {/* Existing fields */}
      <div className="rounded-2xl border border-slate-200 bg-white">
        <div className="border-b border-slate-100 px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-500">
          Fields on {ENTITIES.find((e) => e.id === entity)?.label}
        </div>
        {isError ? (
          <div className="px-4 py-6 text-center text-sm text-slate-500" role="alert">
            The fields could not be loaded.{' '}
            <Button variant="link" size="sm" className="h-auto p-0" onClick={() => void refetch()}>Try again</Button>
          </div>
        ) : isPending ? (
          <div className="py-8 text-center text-slate-500"><Loader2 className="mx-auto h-5 w-5 animate-spin" /></div>
        ) : defs.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-500">No custom fields yet.</p>
        ) : (
          <ul className="divide-y divide-slate-50">
            {defs.map((f, i) => (
              <li key={f.id} className="px-4 py-3">
                {editingId === f.id ? (
                  <FieldEditForm field={f} onDone={() => setEditingId(null)} />
                ) : (
                  <div className="flex items-center justify-between gap-3">
                    <div className="min-w-0">
                      <span className="text-sm font-medium text-slate-800">{f.label}</span>
                      {f.required && <span className="ml-2 text-xs text-red-700">required</span>}
                      <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-500">
                        {TYPES.find((t) => t.id === f.type)?.label ?? f.type}
                        {f.type === 'money' && f.currency ? ` · ${f.currency}` : ''}
                        {f.type === 'link' && f.linkEntityType ? ` · ${f.linkEntityType}` : ''}
                      </span>
                      {hasOptions(f.type) && f.options && (
                        <span className="ml-2 truncate text-xs text-slate-500">{f.options.join(', ')}</span>
                      )}
                      {f.defaultValue !== null && f.defaultValue !== undefined && f.defaultValue !== '' && (
                        <span className="ml-2 text-xs text-slate-500">starts as {String(f.defaultValue)}</span>
                      )}
                    </div>
                    {canManage && (
                      <div className="flex shrink-0 items-center gap-0.5">
                        <button type="button" onClick={() => void move(i, -1)} disabled={busy || i === 0} aria-label={`Move ${f.label} up`}
                          className="rounded p-1.5 text-slate-500 hover:bg-slate-100 disabled:opacity-30">
                          <ArrowUp className="h-4 w-4" />
                        </button>
                        <button type="button" onClick={() => void move(i, 1)} disabled={busy || i === defs.length - 1} aria-label={`Move ${f.label} down`}
                          className="rounded p-1.5 text-slate-500 hover:bg-slate-100 disabled:opacity-30">
                          <ArrowDown className="h-4 w-4" />
                        </button>
                        <button type="button" onClick={() => setEditingId(f.id)} aria-label={`Edit ${f.label}`}
                          className="rounded p-1.5 text-slate-500 hover:bg-slate-100">
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button type="button" onClick={() => void remove(f)} disabled={busy} aria-label={`Delete ${f.label}`}
                          className="rounded p-1.5 text-slate-500 hover:bg-red-50 hover:text-red-600">
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </div>
                    )}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Add field */}
      {canManage && (
        <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4">
          <h3 className="mb-3 text-sm font-semibold text-slate-700">Add a field</h3>
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <Label htmlFor="cf-label">Label</Label>
              <Input id="cf-label" value={label} onChange={(e) => setLabel(e.target.value)} placeholder="e.g. Contract value" className="mt-1 bg-white" />
            </div>
            <div>
              <Label htmlFor="cf-type">Type</Label>
              <Select value={type} onValueChange={(v) => setType(v as CustomFieldType)}>
                <SelectTrigger id="cf-type" className="mt-1 bg-white"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {TYPES.map((t) => <SelectItem key={t.id} value={t.id}>{t.label}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            {hasOptions(type) && (
              <div className="sm:col-span-2">
                <Label htmlFor="cf-options">Options (comma-separated)</Label>
                <Input id="cf-options" value={options} onChange={(e) => setOptions(e.target.value)} placeholder="Small, Medium, Large" className="mt-1 bg-white" />
              </div>
            )}
            {type === 'money' && (
              <div>
                <Label htmlFor="cf-currency">Currency</Label>
                <Select value={currency} onValueChange={setPickedCurrency}>
                  <SelectTrigger id="cf-currency" className="mt-1 bg-white"><SelectValue /></SelectTrigger>
                  <SelectContent>{currenciesFor(workspaceCurrency).map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}</SelectContent>
                </Select>
              </div>
            )}
            {type === 'link' && (
              <div>
                <Label htmlFor="cf-link">Links to</Label>
                <Select value={linkTo} onValueChange={(v) => setLinkTo(v as CustomFieldEntity)}>
                  <SelectTrigger id="cf-link" className="mt-1 bg-white"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    {LINK_TARGETS.filter((t) => t !== 'member' || modules.club).map((t) => (
                      <SelectItem key={t} value={t}>{ENTITIES.find((e) => e.id === t)?.label ?? t}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            )}
            {takesDefault(type) && (
              type === 'checkbox' ? (
                <label className="flex items-center gap-2 self-end text-sm text-slate-600">
                  <Checkbox checked={defaultTicked} onCheckedChange={(c) => setDefaultTicked(c === true)} /> Ticked on new records
                </label>
              ) : (
                <div>
                  <Label htmlFor="cf-default">Default for new records (optional)</Label>
                  {type === 'select' ? (
                    <Select value={defaultText || '__none'} onValueChange={(v) => setDefaultText(v === '__none' ? '' : v)}>
                      <SelectTrigger id="cf-default" className="mt-1 bg-white"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="__none">None</SelectItem>
                        {parseOptions(options).map((o) => <SelectItem key={o} value={o}>{o}</SelectItem>)}
                      </SelectContent>
                    </Select>
                  ) : (
                    <Input id="cf-default" type={type === 'number' ? 'number' : type === 'date' ? 'date' : 'text'} value={defaultText}
                      onChange={(e) => setDefaultText(e.target.value)} className="mt-1 bg-white" />
                  )}
                </div>
              )
            )}
          </div>
          <div className="mt-3 flex items-center justify-between">
            <label className="flex items-center gap-2 text-sm text-slate-600">
              <Checkbox checked={required} onCheckedChange={(c) => setRequired(Boolean(c))} /> Required
            </label>
            <Button onClick={add} disabled={creating} className="gap-1.5">
              {creating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} Add field
            </Button>
          </div>
        </div>
      )}

      <TagsSection />
    </div>
  );
}

/** A field's label, options, required and default, changed in place. Its kind stays: values are kept in it. */
function FieldEditForm({ field, onDone }: { field: CustomFieldDef; onDone: () => void }) {
  const [label, setLabel] = useState(field.label);
  const [options, setOptions] = useState((field.options ?? []).join(', '));
  const [required, setRequired] = useState(!!field.required);
  const [defaultText, setDefaultText] = useState(field.defaultValue === null || field.defaultValue === undefined || typeof field.defaultValue === 'object' ? '' : String(field.defaultValue));
  const [defaultTicked, setDefaultTicked] = useState(field.defaultValue === true);
  const [saving, setSaving] = useState(false);

  const save = async () => {
    if (!label.trim()) { toast.error('Field label is required'); return; }
    setSaving(true);
    try {
      const nextDefault = takesDefault(field.type) ? defaultOf(field.type, defaultText, defaultTicked) : undefined;
      await updateFieldDefinition(field.id, {
        label: label.trim(),
        required,
        ...(hasOptions(field.type) ? { options: parseOptions(options) } : {}),
        // Null takes the default off; left out, it stays as it is.
        ...(takesDefault(field.type) ? { defaultValue: nextDefault ?? null } : {}),
      });
      toast.success('Field saved');
      onDone();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The field could not be saved');
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="grid gap-3 sm:grid-cols-2" aria-label={`Edit ${field.label}`}>
      <div>
        <Label htmlFor={`edit-label-${field.id}`}>Label</Label>
        <Input id={`edit-label-${field.id}`} value={label} onChange={(e) => setLabel(e.target.value)} className="mt-1" />
      </div>
      {hasOptions(field.type) && (
        <div>
          <Label htmlFor={`edit-options-${field.id}`}>Options (comma-separated)</Label>
          <Input id={`edit-options-${field.id}`} value={options} onChange={(e) => setOptions(e.target.value)} className="mt-1" />
        </div>
      )}
      {takesDefault(field.type) && (
        field.type === 'checkbox' ? (
          <label className="flex items-center gap-2 text-sm text-slate-600">
            <Checkbox checked={defaultTicked} onCheckedChange={(c) => setDefaultTicked(c === true)} /> Ticked on new records
          </label>
        ) : (
          <div>
            <Label htmlFor={`edit-default-${field.id}`}>Default for new records</Label>
            <Input id={`edit-default-${field.id}`} type={field.type === 'number' ? 'number' : field.type === 'date' ? 'date' : 'text'}
              value={defaultText} onChange={(e) => setDefaultText(e.target.value)} className="mt-1" />
          </div>
        )
      )}
      <div className="flex items-center justify-between sm:col-span-2">
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <Checkbox checked={required} onCheckedChange={(c) => setRequired(Boolean(c))} /> Required
        </label>
        <div className="flex gap-2">
          <Button variant="ghost" size="sm" onClick={onDone} disabled={saving} className="gap-1"><X className="h-4 w-4" /> Cancel</Button>
          <Button size="sm" onClick={save} disabled={saving} className="gap-1">
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />} Save
          </Button>
        </div>
      </div>
    </div>
  );
}

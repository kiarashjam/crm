import { useEffect, useState } from 'react';
import { SlidersHorizontal, Plus, Trash2, Loader2 } from 'lucide-react';
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
  getFieldDefinitions, createFieldDefinition, deleteFieldDefinition,
  type CustomFieldEntity, type CustomFieldType, type CustomFieldDef,
} from '@/app/api';

const ENTITIES: { id: CustomFieldEntity; label: string }[] = [
  { id: 'lead', label: 'Leads' },
  { id: 'contact', label: 'Contacts' },
  { id: 'company', label: 'Companies' },
  { id: 'deal', label: 'Deals' },
];

const TYPES: { id: CustomFieldType; label: string }[] = [
  { id: 'text', label: 'Text' },
  { id: 'number', label: 'Number' },
  { id: 'date', label: 'Date' },
  { id: 'url', label: 'URL' },
  { id: 'select', label: 'Dropdown' },
  { id: 'checkbox', label: 'Checkbox' },
];

export function CustomFieldsSection() {
  const [entity, setEntity] = useState<CustomFieldEntity>('lead');
  const [defs, setDefs] = useState<CustomFieldDef[]>([]);
  const [loading, setLoading] = useState(true);
  const [label, setLabel] = useState('');
  const [type, setType] = useState<CustomFieldType>('text');
  const [options, setOptions] = useState('');
  const [required, setRequired] = useState(false);
  const [creating, setCreating] = useState(false);

  const load = (e: CustomFieldEntity) => {
    setLoading(true);
    getFieldDefinitions(e).then(setDefs).catch(() => setDefs([])).finally(() => setLoading(false));
  };

  useEffect(() => { load(entity); }, [entity]);

  const add = async () => {
    if (!label.trim()) { toast.error('Field label is required'); return; }
    if (type === 'select' && !options.trim()) { toast.error('Add at least one dropdown option'); return; }
    setCreating(true);
    try {
      const res = await createFieldDefinition({
        entityType: entity,
        label: label.trim(),
        type,
        required,
        options: type === 'select' ? options.split(',').map((o) => o.trim()).filter(Boolean) : undefined,
      });
      if (!res) { toast.error('Failed to create field'); return; }
      toast.success('Custom field added');
      setLabel(''); setOptions(''); setRequired(false); setType('text');
      load(entity);
    } finally {
      setCreating(false);
    }
  };

  const remove = async (f: CustomFieldDef) => {
    if (!confirm(`Delete custom field "${f.label}"?`)) return;
    setDefs((prev) => prev.filter((d) => d.id !== f.id));
    const ok = await deleteFieldDefinition(f.id);
    if (ok) toast.success('Field deleted'); else { toast.error('Failed to delete'); load(entity); }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="flex items-center gap-2 text-lg font-semibold text-slate-900">
          <SlidersHorizontal className="h-5 w-5 text-indigo-600" /> Custom fields
        </h2>
        <p className="mt-1 text-sm text-slate-500">Add your own fields to leads, contacts, companies and deals.</p>
      </div>

      {/* Entity switcher */}
      <div className="flex flex-wrap gap-2">
        {ENTITIES.map((e) => (
          <button
            key={e.id}
            type="button"
            onClick={() => setEntity(e.id)}
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
        <div className="border-b border-slate-100 px-4 py-3 text-xs font-semibold uppercase tracking-wider text-slate-400">
          Fields on {ENTITIES.find((e) => e.id === entity)?.label}
        </div>
        {loading ? (
          <div className="py-8 text-center text-slate-400"><Loader2 className="mx-auto h-5 w-5 animate-spin" /></div>
        ) : defs.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-400">No custom fields yet.</p>
        ) : (
          <ul className="divide-y divide-slate-50">
            {defs.map((f) => (
              <li key={f.id} className="flex items-center justify-between gap-3 px-4 py-3">
                <div className="min-w-0">
                  <span className="text-sm font-medium text-slate-800">{f.label}</span>
                  {f.required && <span className="ml-2 text-xs text-red-500">required</span>}
                  <span className="ml-2 rounded bg-slate-100 px-1.5 py-0.5 text-[11px] font-medium text-slate-500">
                    {TYPES.find((t) => t.id === f.type)?.label ?? f.type}
                  </span>
                  {f.type === 'select' && f.options && (
                    <span className="ml-2 truncate text-xs text-slate-400">{f.options.join(', ')}</span>
                  )}
                </div>
                <button type="button" onClick={() => remove(f)} className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600">
                  <Trash2 className="h-4 w-4" />
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Add field */}
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
          {type === 'select' && (
            <div className="sm:col-span-2">
              <Label htmlFor="cf-options">Options (comma-separated)</Label>
              <Input id="cf-options" value={options} onChange={(e) => setOptions(e.target.value)} placeholder="Small, Medium, Large" className="mt-1 bg-white" />
            </div>
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
    </div>
  );
}

import { useEffect, useMemo, useState } from 'react';
import { SlidersHorizontal, Loader2, Check } from 'lucide-react';
import { toast } from 'sonner';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Button } from '@/app/components/ui/button';
import { Checkbox } from '@/app/components/ui/checkbox';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import {
  getFieldDefinitions, getFieldValues, saveFieldValues,
  type CustomFieldEntity, type CustomFieldDef, type CustomFieldValues, type CustomFieldValue,
} from '@/app/api';

interface CustomFieldsCardProps {
  entityType: CustomFieldEntity;
  recordId: string;
  /** Optional wrapper class to fit the host page's card styling. */
  className?: string;
}

/**
 * Renders this workspace's custom fields for a record and lets the user edit
 * them. Renders nothing when no fields are defined for the entity type, so it's
 * safe to drop onto any detail page.
 */
export default function CustomFieldsCard({ entityType, recordId, className }: CustomFieldsCardProps) {
  const [defs, setDefs] = useState<CustomFieldDef[]>([]);
  const [values, setValues] = useState<CustomFieldValues>({});
  const [initial, setInitial] = useState<CustomFieldValues>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    Promise.all([getFieldDefinitions(entityType), getFieldValues(entityType, recordId)])
      .then(([d, v]) => {
        if (cancelled) return;
        setDefs(d);
        setValues(v);
        setInitial(v);
      })
      .catch(() => { if (!cancelled) setDefs([]); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [entityType, recordId]);

  const dirty = useMemo(() => JSON.stringify(values) !== JSON.stringify(initial), [values, initial]);

  const setField = (key: string, value: CustomFieldValue) => setValues((prev) => ({ ...prev, [key]: value }));

  const save = async () => {
    setSaving(true);
    try {
      const ok = await saveFieldValues(entityType, recordId, values);
      if (ok) { setInitial(values); toast.success('Custom fields saved'); }
      else toast.error('Failed to save');
    } finally {
      setSaving(false);
    }
  };

  // Nothing defined for this entity → render nothing (no empty card clutter).
  if (!loading && defs.length === 0) return null;

  return (
    <div className={className ?? 'rounded-2xl border border-slate-200 bg-white p-5 shadow-sm'}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-700">
          <SlidersHorizontal className="h-4 w-4 text-indigo-500" /> Custom fields
        </h3>
        {dirty && (
          <Button size="sm" onClick={save} disabled={saving} className="h-7 gap-1 text-xs">
            {saving ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Check className="h-3.5 w-3.5" />} Save
          </Button>
        )}
      </div>
      {loading ? (
        <div className="py-4 text-center text-slate-400"><Loader2 className="mx-auto h-4 w-4 animate-spin" /></div>
      ) : (
        <div className="space-y-3">
          {defs.map((f) => {
            const v = values[f.key];
            return (
              <div key={f.id}>
                {f.type !== 'checkbox' && (
                  <Label htmlFor={`cf-${f.id}`} className="text-xs text-slate-500">
                    {f.label}{f.required && <span className="text-red-500"> *</span>}
                  </Label>
                )}
                {f.type === 'select' ? (
                  <Select value={v == null ? '' : String(v)} onValueChange={(val) => setField(f.key, val)}>
                    <SelectTrigger id={`cf-${f.id}`} className="mt-1 h-9"><SelectValue placeholder="Select…" /></SelectTrigger>
                    <SelectContent>
                      {(f.options ?? []).map((opt) => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
                    </SelectContent>
                  </Select>
                ) : f.type === 'checkbox' ? (
                  <label className="mt-1 flex items-center gap-2 text-sm text-slate-700">
                    <Checkbox checked={Boolean(v)} onCheckedChange={(c) => setField(f.key, Boolean(c))} />
                    {f.label}
                  </label>
                ) : (
                  <Input
                    id={`cf-${f.id}`}
                    type={f.type === 'number' ? 'number' : f.type === 'date' ? 'date' : f.type === 'url' ? 'url' : 'text'}
                    value={v == null ? '' : String(v)}
                    onChange={(e) => setField(f.key, f.type === 'number' ? (e.target.value === '' ? null : Number(e.target.value)) : e.target.value)}
                    className="mt-1 h-9"
                    placeholder={f.type === 'url' ? 'https://…' : ''}
                  />
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

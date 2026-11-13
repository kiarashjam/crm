import { useState } from 'react';
import { Tag as TagIcon, Plus, Trash2, Loader2, Pencil, Check, X } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { cn } from '@/app/components/ui/utils';
import { createTag, deleteTag, updateTag, PERMISSION, TAG_COLORS, type Tag, type TagColor } from '@/app/api';
import { useTags } from '@/app/hooks/queries';
import { useMay } from '@/app/hooks/useMay';
import { TagChip, TAG_SWATCH_CLASSES } from '@/app/components/flex/TagChip';

function ColorPicker({ value, onChange, idPrefix }: { value: TagColor; onChange: (color: TagColor) => void; idPrefix: string }) {
  return (
    <div className="flex flex-wrap gap-1.5" role="radiogroup" aria-label="Colour">
      {TAG_COLORS.map((color) => (
        <button
          key={color}
          id={`${idPrefix}-${color}`}
          type="button"
          role="radio"
          aria-checked={value === color}
          aria-label={color}
          onClick={() => onChange(color)}
          className={cn(
            'h-6 w-6 rounded-full ring-offset-2 transition-shadow', TAG_SWATCH_CLASSES[color],
            value === color ? 'ring-2 ring-slate-700' : 'hover:ring-2 hover:ring-slate-300',
          )}
        />
      ))}
    </div>
  );
}

/**
 * The workspace's tags: one list for leads, contacts, companies, deals and members. Anyone
 * who edits records can make one; renaming, recolouring and deleting one changes everyone's
 * records, so is for whoever manages the workspace's settings.
 */
export function TagsSection() {
  const may = useMay();
  const canManage = may(PERMISSION.ManageSettings);
  const canCreate = may([PERMISSION.EditRecords, PERMISSION.ClubMembers]);
  const { tags, isPending, isError, refetch } = useTags();
  const [name, setName] = useState('');
  const [color, setColor] = useState<TagColor>('slate');
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<Tag | null>(null);
  const [busy, setBusy] = useState(false);

  const add = async () => {
    if (!name.trim()) { toast.error('A tag needs a name'); return; }
    setCreating(true);
    try {
      await createTag({ name: name.trim(), color });
      toast.success('Tag added');
      setName(''); setColor('slate');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The tag could not be added');
    } finally {
      setCreating(false);
    }
  };

  const save = async () => {
    if (!editing) return;
    if (!editing.name.trim()) { toast.error('A tag needs a name'); return; }
    setBusy(true);
    try {
      await updateTag(editing.id, { name: editing.name.trim(), color: editing.color });
      toast.success('Tag saved');
      setEditing(null);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The tag could not be saved');
    } finally {
      setBusy(false);
    }
  };

  const remove = async (tag: Tag) => {
    const on = tag.uses === 1 ? 'the 1 record that carries it' : `the ${tag.uses} records that carry it`;
    if (!confirm(`Delete the tag "${tag.name}"? It comes off ${on}.`)) return;
    setBusy(true);
    try {
      await deleteTag(tag.id);
      toast.success('Tag deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The tag could not be deleted');
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="space-y-4 pt-2" aria-labelledby="tags-heading">
      <div>
        <h2 id="tags-heading" className="flex items-center gap-2 text-lg font-semibold text-slate-900">
          <TagIcon className="h-5 w-5 text-indigo-600" /> Tags
        </h2>
        <p className="mt-1 text-sm text-slate-500">One list of coloured tags for leads, contacts, companies, deals and members. Lists filter by them, and automations can test them.</p>
      </div>

      <div className="rounded-2xl border border-slate-200 bg-white">
        {isError ? (
          <div className="px-4 py-6 text-center text-sm text-slate-500" role="alert">
            The tags could not be loaded.{' '}
            <Button variant="link" size="sm" className="h-auto p-0" onClick={() => void refetch()}>Try again</Button>
          </div>
        ) : isPending ? (
          <div className="py-8 text-center text-slate-500"><Loader2 className="mx-auto h-5 w-5 animate-spin" /></div>
        ) : tags.length === 0 ? (
          <p className="px-4 py-6 text-center text-sm text-slate-500">No tags yet.</p>
        ) : (
          <ul className="divide-y divide-slate-50" aria-label="Tags">
            {tags.map((tag) => (
              <li key={tag.id} className="px-4 py-3">
                {editing?.id === tag.id ? (
                  <div className="space-y-2">
                    <div className="flex items-center gap-2">
                      <Label htmlFor={`tag-name-${tag.id}`} className="sr-only">Name</Label>
                      <Input id={`tag-name-${tag.id}`} value={editing.name} onChange={(e) => setEditing({ ...editing, name: e.target.value })} className="h-8 max-w-xs" />
                      <Button size="sm" onClick={save} disabled={busy} className="h-8 gap-1"><Check className="h-4 w-4" /> Save</Button>
                      <Button size="sm" variant="ghost" onClick={() => setEditing(null)} disabled={busy} className="h-8 gap-1"><X className="h-4 w-4" /> Cancel</Button>
                    </div>
                    <ColorPicker value={editing.color} onChange={(c) => setEditing({ ...editing, color: c })} idPrefix={`tag-${tag.id}`} />
                  </div>
                ) : (
                  <div className="flex items-center justify-between gap-3">
                    <div className="flex min-w-0 items-center gap-2">
                      <TagChip tag={tag} />
                      <span className="text-xs text-slate-500">{tag.uses === 1 ? '1 record' : `${tag.uses} records`}</span>
                    </div>
                    {canManage && (
                      <div className="flex shrink-0 items-center gap-0.5">
                        <button type="button" onClick={() => setEditing(tag)} aria-label={`Edit ${tag.name}`} className="rounded p-1.5 text-slate-500 hover:bg-slate-100">
                          <Pencil className="h-4 w-4" />
                        </button>
                        <button type="button" onClick={() => void remove(tag)} disabled={busy} aria-label={`Delete ${tag.name}`}
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

      {canCreate && (
        <div className="rounded-2xl border border-slate-200 bg-slate-50/60 p-4">
          <h3 className="mb-3 text-sm font-semibold text-slate-700">Add a tag</h3>
          <div className="flex flex-wrap items-end gap-3">
            <div>
              <Label htmlFor="tag-new-name">Name</Label>
              <Input id="tag-new-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. VIP" className="mt-1 w-56 bg-white" />
            </div>
            <ColorPicker value={color} onChange={setColor} idPrefix="tag-new" />
            <Button onClick={add} disabled={creating} className="gap-1.5">
              {creating ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} Add tag
            </Button>
          </div>
        </div>
      )}
    </section>
  );
}

import { useState } from 'react';
import { Loader2, Tags } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { cn } from '@/app/components/ui/utils';
import { bulkTag, PERMISSION, type TagEntity } from '@/app/api';
import { useTags } from '@/app/hooks/queries';
import { useMay } from '@/app/hooks/useMay';
import { TagChip } from './TagChip';

interface BulkTagButtonProps {
  entityType: TagEntity;
  /** The records the change is for: those chosen, or those the list shows. */
  recordIds: string[];
  /** What the records are, for the button and the dialog: "the 20 contacts on this page". */
  what: string;
  className?: string;
}

/** Most records the server changes in one go. */
const MAX_RECORDS = 1000;

/**
 * Put tags on, and take tags off, many records at once. Records the person cannot see are
 * left alone and counted; running the same change twice changes nothing more.
 */
export function BulkTagButton({ entityType, recordIds, what, className }: BulkTagButtonProps) {
  const may = useMay();
  const { tags } = useTags();
  const [open, setOpen] = useState(false);
  const [add, setAdd] = useState<Set<string>>(new Set());
  const [remove, setRemove] = useState<Set<string>>(new Set());
  const [saving, setSaving] = useState(false);
  if (!may(entityType === 'member' ? PERMISSION.ClubMembers : PERMISSION.EditRecords) || tags.length === 0 || recordIds.length === 0) return null;

  /** A tag picked to put on is no longer to take off, and the other way round. */
  const pick = (id: string, side: 'add' | 'remove') => {
    const nextAdd = new Set(add);
    const nextRemove = new Set(remove);
    const [target, other] = side === 'add' ? [nextAdd, nextRemove] : [nextRemove, nextAdd];
    if (target.has(id)) target.delete(id);
    else { target.add(id); other.delete(id); }
    setAdd(nextAdd);
    setRemove(nextRemove);
  };

  const run = async () => {
    setSaving(true);
    try {
      const result = await bulkTag({ entityType, recordIds: recordIds.slice(0, MAX_RECORDS), add: [...add], remove: [...remove] });
      const parts = [`${result.added} tag${result.added === 1 ? '' : 's'} put on`, `${result.removed} taken off`];
      toast.success(`${result.records} record${result.records === 1 ? '' : 's'} changed: ${parts.join(', ')}${result.skipped ? ` (${result.skipped} left alone)` : ''}`);
      setOpen(false);
      setAdd(new Set()); setRemove(new Set());
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The tags could not be changed');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <Button variant="outline" size="sm" className={cn('h-8 gap-1.5 text-xs', className)} onClick={() => setOpen(true)}>
        <Tags className="h-3.5 w-3.5" aria-hidden /> Tag {what}
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Tag {what}</DialogTitle>
            <DialogDescription>
              Pick the tags to put on and the tags to take off{recordIds.length > MAX_RECORDS ? `; the first ${MAX_RECORDS.toLocaleString()} are changed` : ''}.
            </DialogDescription>
          </DialogHeader>
          <ul className="max-h-72 space-y-1.5 overflow-y-auto" aria-label="Tags to change">
            {tags.map((t) => (
              <li key={t.id} className="flex items-center justify-between gap-2">
                <TagChip tag={t} />
                <div className="flex overflow-hidden rounded-md border border-slate-200 text-xs" role="group" aria-label={`Change ${t.name}`}>
                  <button type="button" aria-pressed={add.has(t.id)} onClick={() => pick(t.id, 'add')}
                    className={cn('px-2 py-0.5', add.has(t.id) ? 'bg-emerald-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-50')}>
                    Put on
                  </button>
                  <button type="button" aria-pressed={remove.has(t.id)} onClick={() => pick(t.id, 'remove')}
                    className={cn('px-2 py-0.5', remove.has(t.id) ? 'bg-red-600 text-white' : 'bg-white text-slate-600 hover:bg-slate-50')}>
                    Take off
                  </button>
                </div>
              </li>
            ))}
          </ul>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setOpen(false)} disabled={saving}>Cancel</Button>
            <Button onClick={run} disabled={saving || (add.size === 0 && remove.size === 0)} className="gap-1.5">
              {saving && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />} Change tags
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

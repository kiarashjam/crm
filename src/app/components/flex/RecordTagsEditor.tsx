import { useMemo, useState } from 'react';
import { Check, Loader2, Plus, Tag as TagIcon } from 'lucide-react';
import { toast } from 'sonner';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/app/components/ui/button';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from '@/app/components/ui/command';
import { cn } from '@/app/components/ui/utils';
import { createTag, setRecordTags, PERMISSION, type Tag, type TagEntity } from '@/app/api';
import { queryKeys, useRecordTags, useTags, useWorkspaceScope } from '@/app/hooks/queries';
import { useMay } from '@/app/hooks/useMay';
import { TagChip } from './TagChip';

interface RecordTagsEditorProps {
  entityType: TagEntity;
  recordId: string;
  className?: string;
}

/**
 * The tags on one record: shown as chips, taken off with their cross, put on (or made, when
 * the workspace has none of that name) from a searchable list. Who may not change the
 * record sees them without the controls.
 */
export default function RecordTagsEditor({ entityType, recordId, className }: RecordTagsEditorProps) {
  const may = useMay();
  // A member's tags are club work; any other record's, records.edit — as the server asks.
  const canEdit = may(entityType === 'member' ? PERMISSION.ClubMembers : PERMISSION.EditRecords);
  const ws = useWorkspaceScope();
  const client = useQueryClient();
  const { tags: allTags, isError: tagsFailed, refetch: refetchTags } = useTags();
  const record = useRecordTags(entityType, recordId);
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [saving, setSaving] = useState(false);

  const carried = useMemo(() => record.data ?? [], [record.data]);
  const byId = useMemo(() => new Map(allTags.map((t) => [t.id, t])), [allTags]);
  const shown = carried.map((id) => byId.get(id)).filter((t): t is Tag => !!t);
  const typed = search.trim();
  const exact = allTags.some((t) => t.name.toLowerCase() === typed.toLowerCase());

  const save = async (next: string[]) => {
    const key = queryKeys.tags.record(ws, entityType, recordId);
    const before = carried;
    client.setQueryData(key, next);
    setSaving(true);
    try {
      const saved = await setRecordTags(entityType, recordId, next);
      client.setQueryData(key, saved);
    } catch (err) {
      client.setQueryData(key, before);
      toast.error(err instanceof Error && err.message ? err.message : 'The tags could not be saved');
      void record.refetch();
    } finally {
      setSaving(false);
    }
  };

  const toggle = (id: string) => void save(carried.includes(id) ? carried.filter((t) => t !== id) : [...carried, id]);

  const make = async () => {
    if (!typed) return;
    setSaving(true);
    try {
      const tag = await createTag({ name: typed });
      client.setQueryData<Tag[]>(queryKeys.tags.all(ws), (current) => [...(current ?? []), tag].sort((a, b) => a.name.localeCompare(b.name)));
      setSearch('');
      await save([...carried, tag.id]);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The tag could not be made');
    } finally {
      setSaving(false);
    }
  };

  if (record.isError || tagsFailed) {
    return (
      <div className={cn('flex items-center gap-2 text-sm text-slate-500', className)} role="alert">
        <TagIcon className="h-4 w-4" aria-hidden /> The tags could not be loaded.
        <Button variant="link" size="sm" className="h-auto p-0" onClick={() => { void record.refetch(); void refetchTags(); }}>Try again</Button>
      </div>
    );
  }

  return (
    <div className={cn('flex flex-wrap items-center gap-1.5', className)} aria-label="Tags">
      <TagIcon className="h-4 w-4 text-slate-400" aria-hidden />
      {record.isPending ? (
        <Loader2 className="h-3.5 w-3.5 animate-spin text-slate-400" aria-label="Loading tags" />
      ) : shown.length === 0 && !canEdit ? (
        <span className="text-xs text-slate-400">No tags</span>
      ) : (
        shown.map((t) => <TagChip key={t.id} tag={t} onRemove={canEdit && !saving ? () => toggle(t.id) : undefined} />)
      )}
      {canEdit && !record.isPending && (
        <Popover open={open} onOpenChange={(next) => { setOpen(next); if (!next) setSearch(''); }}>
          <PopoverTrigger asChild>
            <Button variant="ghost" size="sm" className="h-6 gap-1 px-2 text-xs text-slate-500" disabled={saving}>
              <Plus className="h-3 w-3" aria-hidden /> Tag
            </Button>
          </PopoverTrigger>
          <PopoverContent className="w-64 p-0" align="start">
            <Command>
              <CommandInput placeholder="Find or make a tag…" value={search} onValueChange={setSearch} />
              <CommandList>
                <CommandEmpty>{typed ? 'No tag of that name yet.' : 'No tags yet.'}</CommandEmpty>
                <CommandGroup>
                  {allTags.map((t) => (
                    <CommandItem key={t.id} value={t.name} onSelect={() => toggle(t.id)}>
                      <Check className={cn('mr-2 h-4 w-4', carried.includes(t.id) ? 'opacity-100' : 'opacity-0')} aria-hidden />
                      <TagChip tag={t} />
                    </CommandItem>
                  ))}
                </CommandGroup>
                {typed && !exact && (
                  <CommandGroup>
                    <CommandItem value={`make ${typed}`} onSelect={() => void make()}>
                      <Plus className="mr-2 h-4 w-4" aria-hidden /> Make the tag “{typed}”
                    </CommandItem>
                  </CommandGroup>
                )}
              </CommandList>
            </Command>
          </PopoverContent>
        </Popover>
      )}
    </div>
  );
}

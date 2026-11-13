import { useEffect, useRef, useState } from 'react';
import { Bookmark, Check, Loader2, Star, Trash2, Users } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Checkbox } from '@/app/components/ui/checkbox';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import {
  DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuLabel, DropdownMenuSeparator, DropdownMenuTrigger,
} from '@/app/components/ui/dropdown-menu';
import {
  createSavedView, deleteSavedView, paramsFromQuery, queryFromParams, setDefaultView, updateSavedView,
  type SavedView, type SavedViewList,
} from '@/app/api';
import { useSavedViews } from '@/app/hooks/queries';
import { useMay } from '@/app/hooks/useMay';
import type { ViewPreference } from '@/app/lib/viewPreferences';
import type { ColumnChoice } from '@/app/components/views/ViewSwitcher';

interface SavedViewsMenuProps {
  list: SavedViewList;
  /** The page's address parameters (its filters and order), and how to change them. */
  params: URLSearchParams;
  onChange: (next: URLSearchParams) => void;
  /** The page's columns and which are shown, so a view keeps and restores them. */
  view?: ViewPreference;
  columns?: ColumnChoice[];
}

/** Whether the address holds anything a view would keep: filters or an order. */
function hasQuery(params: URLSearchParams): boolean {
  return Object.keys(queryFromParams(params)).length > 0;
}

/**
 * A list's saved views: a name for its filters, order and columns, the person's own or
 * shared with the workspace. Opening the list with no filters in its address opens it with
 * the person's default view, once.
 */
export function SavedViewsMenu({ list, params, onChange, view, columns }: SavedViewsMenuProps) {
  const may = useMay();
  // Sharing a view takes a role that changes records, as the server asks.
  const canShare = may();
  const views = useSavedViews(list);
  const [saving, setSaving] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [name, setName] = useState('');
  const [shared, setShared] = useState(false);
  const applied = useRef(false);

  const shownColumns = () => (columns && view ? columns.filter((c) => c.required || view.shows(c.id, c.byDefault ?? true)).map((c) => c.id) : null);

  const apply = (saved: SavedView) => {
    onChange(paramsFromQuery(saved.query, params));
    if (saved.columns && view && columns) {
      const on = new Set(saved.columns);
      view.setColumns(Object.fromEntries(columns.filter((c) => !c.required).map((c) => [c.id, on.has(c.id)])));
    }
  };

  // The person's default view, when the list is opened bare: once, so it never fights them.
  useEffect(() => {
    if (applied.current || !views.data) return;
    applied.current = true;
    const fallback = views.data.find((v) => v.isDefault);
    if (fallback && !hasQuery(params)) apply(fallback);
    // eslint-disable-next-line react-hooks/exhaustive-deps -- once, when the views first arrive
  }, [views.data]);

  const save = async () => {
    if (!name.trim()) { toast.error('A view needs a name'); return; }
    setSaving(true);
    try {
      await createSavedView({ list, name: name.trim(), shared: canShare && shared, query: queryFromParams(params), columns: shownColumns() });
      toast.success(`Saved the view “${name.trim()}”`);
      setDialogOpen(false);
      setName(''); setShared(false);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The view could not be saved');
    } finally {
      setSaving(false);
    }
  };

  const run = async (what: () => Promise<unknown>, done: string) => {
    try {
      await what();
      toast.success(done);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'That did not work');
    }
  };

  const mine = (views.data ?? []).filter((v) => v.mine);
  const others = (views.data ?? []).filter((v) => !v.mine);

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="sm" className="h-8 gap-1.5 text-xs">
            <Bookmark className="h-3.5 w-3.5" aria-hidden /> Views
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="w-72">
          {views.isError ? (
            <DropdownMenuItem onSelect={() => void views.refetch()}>The views could not be loaded. Try again</DropdownMenuItem>
          ) : views.isPending ? (
            <div className="flex justify-center py-2"><Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-label="Loading views" /></div>
          ) : (
            <>
              {mine.length === 0 && others.length === 0 && <DropdownMenuLabel className="text-xs font-normal text-slate-500">No saved views yet.</DropdownMenuLabel>}
              {[...mine, ...others].map((v) => (
                <DropdownMenuItem key={v.id} onSelect={() => apply(v)} className="flex items-center justify-between gap-2">
                  <span className="flex min-w-0 items-center gap-1.5">
                    {v.isDefault && <Star className="h-3.5 w-3.5 shrink-0 fill-amber-400 text-amber-400" aria-label="Opens with this view" />}
                    <span className="truncate">{v.name}</span>
                    {v.shared && <Users className="h-3 w-3 shrink-0 text-slate-400" aria-label="Shared" />}
                  </span>
                  {!v.mine && v.ownerName && <span className="shrink-0 text-[11px] text-slate-400">{v.ownerName}</span>}
                </DropdownMenuItem>
              ))}
              <DropdownMenuSeparator />
              <DropdownMenuItem onSelect={() => setDialogOpen(true)}>Save these filters as a view…</DropdownMenuItem>
              {mine.map((v) => (
                <DropdownMenuItem key={`update-${v.id}`}
                  onSelect={() => void run(() => updateSavedView(v.id, { query: queryFromParams(params), columns: shownColumns(), version: v.version }), `Updated “${v.name}”`)}>
                  <Check className="mr-2 h-3.5 w-3.5" aria-hidden /> Update “{v.name}” to these filters
                </DropdownMenuItem>
              ))}
              {(views.data ?? []).map((v) => (
                <DropdownMenuItem key={`default-${v.id}`}
                  onSelect={() => void run(() => setDefaultView(list, v.isDefault ? null : v.id), v.isDefault ? 'The list opens as it comes' : `The list opens with “${v.name}”`)}>
                  <Star className="mr-2 h-3.5 w-3.5" aria-hidden /> {v.isDefault ? `Stop opening with “${v.name}”` : `Open the list with “${v.name}”`}
                </DropdownMenuItem>
              ))}
              {mine.map((v) => (
                <DropdownMenuItem key={`delete-${v.id}`} className="text-red-600 focus:text-red-600"
                  onSelect={() => { if (confirm(`Delete the view “${v.name}”?`)) void run(() => deleteSavedView(v.id), 'View deleted'); }}>
                  <Trash2 className="mr-2 h-3.5 w-3.5" aria-hidden /> Delete “{v.name}”
                </DropdownMenuItem>
              ))}
            </>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Save this view</DialogTitle>
            <DialogDescription>Its filters, order and columns, under a name, to open again from the Views menu.</DialogDescription>
          </DialogHeader>
          <div className="space-y-3">
            <div>
              <Label htmlFor="view-name">Name</Label>
              <Input id="view-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Gold members in Lausanne" className="mt-1" />
            </div>
            {canShare && (
              <label className="flex items-center gap-2 text-sm text-slate-600">
                <Checkbox checked={shared} onCheckedChange={(c) => setShared(c === true)} /> Share with the whole workspace
              </label>
            )}
          </div>
          <DialogFooter>
            <Button variant="ghost" onClick={() => setDialogOpen(false)} disabled={saving}>Cancel</Button>
            <Button onClick={save} disabled={saving} className="gap-1.5">
              {saving && <Loader2 className="h-4 w-4 animate-spin" aria-hidden />} Save view
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}

import { useEffect, useState } from 'react';
import { Loader2, Pencil, Plus, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/app/components/ui/dialog';
import {
  BOOKING_MODES,
  RESOURCE_KINDS,
  addUsualResources,
  createResource,
  deleteResource,
  updateResource,
  type BookingMode,
  type Resource,
  type ResourceKind,
} from '@/app/api/reservations';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';

interface ResourceForm {
  name: string;
  kind: ResourceKind;
  capacity: string;
  bookingMode: BookingMode;
  depositAmount: string;
  noShowFee: string;
  description: string;
  active: boolean;
}

/** What a kind of space is usually booked as, as the server assumes when it is left out. */
function usualMode(kind: ResourceKind): BookingMode {
  return kind === 'Dining' || kind === 'Lounge' || kind === 'Studio' ? 'Shared' : 'Exclusive';
}

function blank(): ResourceForm {
  return { name: '', kind: 'Meeting', capacity: '8', bookingMode: 'Exclusive', depositAmount: '0', noShowFee: '0', description: '', active: true };
}

function formOf(r: Resource): ResourceForm {
  return {
    name: r.name,
    kind: r.kind,
    capacity: String(r.capacity),
    bookingMode: r.bookingMode ?? usualMode(r.kind),
    depositAmount: String(r.depositAmount ?? 0),
    noShowFee: String(r.noShowFee ?? 0),
    description: r.description ?? '',
    active: r.active !== false,
  };
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/**
 * The club's bookable spaces, for whoever runs the club's members: what each is, how many it
 * holds, whether it takes one booking at a time or bookings up to its capacity, and the deposit
 * and no-show fee it asks. One with bookings is made not bookable rather than deleted.
 */
export default function ResourcesDialog({
  open,
  onOpenChange,
  resources,
  currency,
  money,
  onSaved,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  resources: Resource[];
  currency: string;
  money: (n: number) => string;
  onSaved: () => void | Promise<void>;
}) {
  // The resource being edited, 'new' for one being added, or null for the list.
  const [editing, setEditing] = useState<Resource | 'new' | null>(null);
  const [form, setForm] = useState<ResourceForm>(blank());
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) setEditing(null);
  }, [open]);

  const startNew = () => {
    setForm(blank());
    setEditing('new');
  };

  const startEdit = (r: Resource) => {
    setForm(formOf(r));
    setEditing(r);
  };

  const save = async () => {
    if (!form.name.trim()) {
      toast.error('Give the resource a name');
      return;
    }
    const capacity = Number(form.capacity);
    if (!Number.isFinite(capacity) || capacity < 1) {
      toast.error('Capacity is at least 1');
      return;
    }
    const fields = {
      name: form.name.trim(),
      kind: form.kind,
      capacity: Math.floor(capacity),
      bookingMode: form.bookingMode,
      depositAmount: Number(form.depositAmount) || 0,
      noShowFee: Number(form.noShowFee) || 0,
      // Sent empty, a description is cleared.
      description: form.description.trim(),
      active: form.active,
    };
    setBusy(true);
    try {
      if (editing === 'new') {
        await createResource(fields);
        toast.success(`${fields.name} added`);
      } else if (editing) {
        // From the copy the form opened with: another edit since is refused, a booking is not one.
        await updateResource(editing.id, { ...fields, version: editing.version });
        toast.success(`${fields.name} saved`);
      }
      setEditing(null);
      await onSaved();
    } catch (err) {
      toast.error(errorText(err, 'Could not save the resource'));
    } finally {
      setBusy(false);
    }
  };

  const remove = async (r: Resource) => {
    if (!confirm(`Delete ${r.name}?`)) return;
    setBusy(true);
    try {
      await deleteResource(r.id);
      toast.success(`${r.name} deleted`);
      await onSaved();
    } catch (err) {
      // One with bookings is refused, saying to make it not bookable instead.
      toast.error(errorText(err, 'Could not delete it'));
    } finally {
      setBusy(false);
    }
  };

  const addUsual = async () => {
    setBusy(true);
    try {
      await addUsualResources();
      toast.success('The usual spaces are added');
      await onSaved();
    } catch (err) {
      toast.error(errorText(err, 'Could not add them'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{editing === 'new' ? 'New resource' : editing ? `Edit ${editing.name}` : 'Resources'}</DialogTitle>
          <DialogDescription>
            A space booked one at a time takes one booking at a time; a shared one takes bookings until the people in them reach its capacity.
          </DialogDescription>
        </DialogHeader>

        {editing ? (
          <div className="grid grid-cols-1 gap-4 py-2 md:grid-cols-2">
            <div className="md:col-span-2">
              <Label htmlFor="res-name">Name</Label>
              <Input id="res-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div>
              <Label htmlFor="res-kind">Kind</Label>
              <select
                id="res-kind"
                value={form.kind}
                onChange={(e) => {
                  const kind = e.target.value as ResourceKind;
                  // A new resource takes what its kind is usually booked as; an existing one keeps its setting.
                  setForm({ ...form, kind, bookingMode: editing === 'new' ? usualMode(kind) : form.bookingMode });
                }}
                className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
              >
                {RESOURCE_KINDS.map((k) => (
                  <option key={k} value={k}>
                    {k}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <Label htmlFor="res-mode">Booked</Label>
              <select
                id="res-mode"
                value={form.bookingMode}
                onChange={(e) => setForm({ ...form, bookingMode: e.target.value as BookingMode })}
                className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
              >
                {BOOKING_MODES.map((m) => (
                  <option key={m} value={m}>
                    {m === 'Exclusive' ? 'One booking at a time' : 'Shared, up to its capacity'}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <Label htmlFor="res-capacity">Capacity (people)</Label>
              <Input id="res-capacity" type="number" min="1" value={form.capacity} onChange={(e) => setForm({ ...form, capacity: e.target.value })} />
            </div>
            <div className="flex items-end gap-2 pb-2">
              <input
                id="res-active"
                type="checkbox"
                checked={form.active}
                onChange={(e) => setForm({ ...form, active: e.target.checked })}
                className="h-4 w-4"
              />
              <Label htmlFor="res-active">Takes bookings</Label>
            </div>
            <div>
              <Label htmlFor="res-deposit">Deposit ({currency})</Label>
              <Input id="res-deposit" type="number" min="0" step="0.01" value={form.depositAmount} onChange={(e) => setForm({ ...form, depositAmount: e.target.value })} />
              <p className="mt-1 text-xs text-slate-500">Asked when a member books; given back when they come or cancel. 0 is none.</p>
            </div>
            <div>
              <Label htmlFor="res-noshow">No-show fee ({currency})</Label>
              <Input id="res-noshow" type="number" min="0" step="0.01" value={form.noShowFee} onChange={(e) => setForm({ ...form, noShowFee: e.target.value })} />
              <p className="mt-1 text-xs text-slate-500">Posted to the member's account when a booking is marked a no-show. 0 is none.</p>
            </div>
            <div className="md:col-span-2">
              <Label htmlFor="res-description">Description</Label>
              <Input id="res-description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            </div>
          </div>
        ) : resources.length === 0 ? (
          <div className="py-6 text-center text-sm text-slate-600">
            <p>No resources yet.</p>
            <Button className="mt-3" size="sm" onClick={addUsual} disabled={busy}>
              {busy ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : null}
              Add the usual spaces
            </Button>
          </div>
        ) : (
          <ul className="max-h-[50vh] divide-y divide-slate-100 overflow-y-auto py-1">
            {resources.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-3 py-2">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-slate-900">
                    {r.name}
                    {r.active === false && <span className="ml-2 text-xs font-normal text-slate-500">not bookable</span>}
                  </p>
                  <p className="text-xs text-slate-500">
                    {r.kind} · {r.bookingMode === 'Shared' ? `shared, up to ${r.capacity}` : `one booking at a time, holds ${r.capacity}`}
                    {(r.depositAmount ?? 0) > 0 && ` · deposit ${money(r.depositAmount ?? 0)}`}
                    {(r.noShowFee ?? 0) > 0 && ` · no-show fee ${money(r.noShowFee ?? 0)}`}
                  </p>
                </div>
                <div className="flex shrink-0 items-center gap-1">
                  <button
                    type="button"
                    onClick={() => startEdit(r)}
                    className="rounded p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-700"
                    aria-label={`Edit ${r.name}`}
                  >
                    <Pencil className="h-4 w-4" />
                  </button>
                  <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
                    <button
                      type="button"
                      onClick={() => remove(r)}
                      disabled={busy}
                      className="rounded p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50"
                      aria-label={`Delete ${r.name}`}
                    >
                      <Trash2 className="h-4 w-4" />
                    </button>
                  </WriteOnly>
                </div>
              </li>
            ))}
          </ul>
        )}

        <DialogFooter>
          {editing ? (
            <>
              <Button variant="outline" onClick={() => setEditing(null)} disabled={busy}>
                Back
              </Button>
              <Button onClick={save} disabled={busy}>
                {busy ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : null}
                {editing === 'new' ? 'Add' : 'Save'}
              </Button>
            </>
          ) : (
            <>
              <Button variant="outline" onClick={() => onOpenChange(false)}>
                Close
              </Button>
              <Button onClick={startNew}>
                <Plus className="h-4 w-4" />
                New resource
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

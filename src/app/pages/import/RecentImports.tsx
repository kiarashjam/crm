// The workspace's latest imports of one kind, each with a way to undo it.
//
// Undoing shows first what it would remove and what it keeps and why, from the
// server's own plan: a lead someone has called since the import stays, with its
// contact and its company. Nothing is removed until the person confirms.

import { useCallback, useEffect, useState } from 'react';
import { History, Loader2, RotateCcw } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import { listImports, previewUndo, undoImport, type ImportBatch, type ImportKind, type ImportUndo } from '@/app/api/imports';
import { IMPORT_KINDS, count } from './importKinds';

const KEPT_SHOWN = 10;

/** "798 leads, the 798 contacts made for them and 12 companies". */
export function removalSummary(kind: ImportKind, undo: Pick<ImportUndo, 'leads' | 'contacts' | 'companies'>): string {
  const parts: string[] = [];
  if (undo.leads) parts.push(count(undo.leads, IMPORT_KINDS.lead));
  if (undo.contacts) {
    parts.push(kind === 'lead'
      ? `the ${count(undo.contacts, IMPORT_KINDS.contact)} made for them`
      : count(undo.contacts, IMPORT_KINDS.contact));
  }
  if (undo.companies) parts.push(count(undo.companies, IMPORT_KINDS.company));
  if (parts.length <= 1) return parts[0] ?? '';
  return `${parts.slice(0, -1).join(', ')} and ${parts[parts.length - 1]}`;
}

function when(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? '' : d.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
}

interface UndoDialogProps {
  kind: ImportKind;
  batch: { id: string; fileName: string } | null;
  onClose: () => void;
  onUndone: (undo: ImportUndo) => void;
}

/** Asks before undoing an import, showing what would go and what stays. */
export function UndoImportDialog({ kind, batch, onClose, onUndone }: UndoDialogProps) {
  const [plan, setPlan] = useState<ImportUndo | null>(null);
  const [loading, setLoading] = useState(false);
  const [undoing, setUndoing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setPlan(null);
    setError(null);
    if (!batch) return;
    let current = true;
    setLoading(true);
    previewUndo(batch.id)
      .then((p) => { if (current) setPlan(p); })
      .catch((e: unknown) => { if (current) setError(e instanceof Error ? e.message : 'Could not check what undoing would remove'); })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [batch]);

  const total = plan ? plan.leads + plan.contacts + plan.companies : 0;

  const confirm = async () => {
    if (!batch) return;
    setUndoing(true);
    try {
      const done = await undoImport(batch.id);
      const removed = removalSummary(kind, done);
      toast.success(removed ? `Removed ${removed}` : 'Nothing was left to remove');
      onUndone(done);
    } catch (e) {
      toast.error(e instanceof Error ? e.message : 'Could not undo the import');
    } finally {
      setUndoing(false);
    }
  };

  return (
    <AlertDialog open={batch !== null} onOpenChange={(open) => { if (!open && !undoing) onClose(); }}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Undo the import of {batch?.fileName}?</AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3 text-sm text-slate-600">
              {loading && (
                <p className="flex items-center gap-2" role="status">
                  <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Checking what it would remove…
                </p>
              )}
              {error && <p className="text-red-700">{error}</p>}
              {plan && (
                <>
                  <p>
                    {total > 0
                      ? <>This removes <strong>{removalSummary(kind, plan)}</strong> that the import added.</>
                      : 'Nothing the import added can be removed now.'}
                  </p>
                  {plan.kept.length > 0 && (
                    <div>
                      <p>These stay, because they were changed or worked on since, or something that stays needs them:</p>
                      <ul className="mt-1 max-h-48 list-disc space-y-0.5 overflow-y-auto pl-5">
                        {plan.kept.slice(0, KEPT_SHOWN).map((k) => (
                          <li key={`${k.kind}:${k.id}`}>
                            <span className="font-medium text-slate-800">{k.label}</span> ({k.kind}): {k.reason}
                          </li>
                        ))}
                      </ul>
                      {plan.kept.length > KEPT_SHOWN && <p className="mt-1">…and {plan.kept.length - KEPT_SHOWN} more.</p>}
                    </div>
                  )}
                  <p className="text-slate-500">Changes the import made to records that were already here are not undone.</p>
                </>
              )}
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel disabled={undoing}>Keep it</AlertDialogCancel>
          <Button
            onClick={confirm}
            disabled={!plan || total === 0 || undoing}
            className="bg-red-600 hover:bg-red-500"
          >
            {undoing ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : <RotateCcw className="h-4 w-4" aria-hidden />}
            Undo import
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

interface RecentImportsProps {
  kind: ImportKind;
  /** Changes when an import or an undo elsewhere on the page should be shown. */
  refreshKey: number;
}

export default function RecentImports({ kind, refreshKey }: RecentImportsProps) {
  const info = IMPORT_KINDS[kind];
  const [batches, setBatches] = useState<ImportBatch[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [undoing, setUndoing] = useState<ImportBatch | null>(null);
  const [reloads, setReloads] = useState(0);

  const load = useCallback(() => setReloads((n) => n + 1), []);

  useEffect(() => {
    let current = true;
    listImports(kind)
      .then((list) => { if (current) { setBatches(list); setFailed(false); } })
      .catch(() => { if (current) setFailed(true); });
    return () => { current = false; };
  }, [kind, refreshKey, reloads]);

  if (failed) {
    return (
      <section className="rounded-xl border border-slate-200 bg-white p-6 text-sm text-slate-600">
        Your past imports could not be loaded. <button type="button" className="underline" onClick={load}>Try again</button>
      </section>
    );
  }
  if (!batches || batches.length === 0) return null;

  return (
    <section className="rounded-xl border border-slate-200 bg-white" aria-labelledby="recent-imports">
      <h3 id="recent-imports" className="flex items-center gap-2 border-b border-slate-100 px-6 py-4 font-semibold text-slate-800">
        <History className="h-4 w-4 text-slate-500" aria-hidden /> Recent imports of {info.many}
      </h3>
      <ul className="divide-y divide-slate-100">
        {batches.map((b) => (
          <li key={b.id} className="flex flex-wrap items-center justify-between gap-3 px-6 py-3">
            <div className="min-w-0">
              <p className="truncate font-medium text-slate-800">{b.fileName}</p>
              <p className="text-sm text-slate-500">
                {when(b.startedAtUtc)} by {b.importedBy} · added {b.created.toLocaleString()}
                {b.updated > 0 && <> · updated {b.updated.toLocaleString()}</>}
                {b.skipped > 0 && <> · skipped {b.skipped.toLocaleString()}</>}
                {b.failed > 0 && <> · {b.failed.toLocaleString()} not imported</>}
              </p>
              {!b.finishedAtUtc && (
                <p className="text-sm text-amber-700">
                  Not finished: it is still running, or the server stopped part way through. If it stopped, undo it and import the file again.
                </p>
              )}
              {b.undoneAtUtc && (
                <p className="text-sm text-slate-500">
                  Undone {when(b.undoneAtUtc)}, {b.removed.toLocaleString()} removed
                  {b.remaining > 0 && <>; {b.remaining.toLocaleString()} kept, because they were changed or worked on</>}
                </p>
              )}
            </div>
            {b.canUndo && (
              <Button variant="outline" size="sm" className="gap-1.5" onClick={() => setUndoing(b)}>
                <RotateCcw className="h-4 w-4" aria-hidden /> {b.undoneAtUtc ? 'Undo again' : 'Undo'}
              </Button>
            )}
          </li>
        ))}
      </ul>
      <UndoImportDialog
        kind={kind}
        batch={undoing}
        onClose={() => setUndoing(null)}
        onUndone={() => { setUndoing(null); load(); }}
      />
    </section>
  );
}

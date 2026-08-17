// Bulk reconcile: bring every lead's status back into line with its pipeline.
//
// Auto-sync handles this going forward, but it only runs at the moment of an
// edit, so leads whose steps were recorded before it existed — or that arrived by
// import or webhook — sit at a stale status forever. The per-lead drift strip
// surfaces one at a time, which does not scale to a whole book of leads.
//
// Two deliberate choices:
//
//  · It fetches ALL leads rather than reading the page the user happens to be
//    on. "Find them and update their status" means all of them, and a fix that
//    silently covered 20 of 200 would be worse than none.
//  · It does NOT record these writes as manual picks. A manual pick puts a lead
//    into suggest-mode until the pipeline moves past it; recording a bulk
//    reconcile that way would park the entire book in suggest-mode and break the
//    automation this exists to restore.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Loader2, ArrowRight, AlertTriangle, CheckCircle2, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';
import { getLeads, updateLead } from '@/app/api/leads';
import type { Lead } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';
import { Button } from '@/app/components/ui/button';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import type { StatusOption, CanonicalStage } from '../leadStatusSync';
import {
  findStatusFixes, safeFixes, summariseFixes, groupFixesByTarget, type LeadStatusFix,
} from '../bulkStatusReconcile';

const isGuid = (s?: string) =>
  !!s && /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(s);

interface Props {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  statusOptions: StatusOption[];
  statusesLoaded: boolean;
  overrides?: Partial<Record<CanonicalStage, string>>;
  /** Lets the list behind the dialog reflect the new statuses without a reload. */
  onApplied?: (updated: Lead[]) => void;
}

export function StatusReconcileDialog({
  open, onOpenChange, statusOptions, statusesLoaded, overrides, onApplied,
}: Props) {
  const [scanning, setScanning] = useState(false);
  const [applying, setApplying] = useState(false);
  const [scanFailed, setScanFailed] = useState(false);
  const [fixes, setFixes] = useState<LeadStatusFix[]>([]);
  const [chosen, setChosen] = useState<Set<string>>(new Set());
  const [scanned, setScanned] = useState(0);

  const scan = useCallback(async () => {
    setScanning(true);
    setScanFailed(false);
    try {
      const all = await getLeads();
      const leads = Array.isArray(all) ? all : [];
      setScanned(leads.length);
      const found = findStatusFixes({ leads, statusOptions, statusesLoaded, overrides });
      setFixes(found);
      // Only the unambiguous forward moves start ticked. Everything else is
      // listed but must be chosen deliberately.
      setChosen(new Set(safeFixes(found).map((f) => f.lead.id)));
    } catch {
      // An empty result and a failed fetch look identical on screen, and "0 leads
      // need fixing" is the more reassuring of the two — so say which happened.
      setScanFailed(true);
      setFixes([]);
      setChosen(new Set());
    } finally {
      setScanning(false);
    }
  }, [statusOptions, statusesLoaded, overrides]);

  useEffect(() => {
    if (open && statusesLoaded) void scan();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- rescan only on open
  }, [open, statusesLoaded]);

  const summary = useMemo(() => summariseFixes(fixes), [fixes]);
  const targets = useMemo(
    () => groupFixesByTarget(fixes.filter((f) => chosen.has(f.lead.id))),
    [fixes, chosen],
  );

  const toggle = (id: string) => setChosen((prev) => {
    const next = new Set(prev);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  const apply = async () => {
    const todo = fixes.filter((f) => chosen.has(f.lead.id));
    if (todo.length === 0) return;
    setApplying(true);
    const updated: Lead[] = [];
    let failed = 0;

    // Sequential on purpose: a few hundred parallel PUTs is a self-inflicted
    // denial of service on the API, and a partial failure is easier to report
    // honestly when the order is known.
    for (const f of todo) {
      try {
        const res = await updateLead(f.lead.id, {
          status: f.to,
          ...(isGuid(f.toId) ? { leadStatusId: f.toId } : {}),
        });
        updated.push({ ...f.lead, ...(res ?? {}), status: f.to });
      } catch {
        failed += 1;
      }
    }

    setApplying(false);
    if (updated.length > 0) onApplied?.(updated);
    if (failed === 0) {
      toast.success(`Updated ${updated.length} lead${updated.length === 1 ? '' : 's'}`);
      onOpenChange(false);
    } else {
      // Never claim a clean run when part of it failed.
      toast.error(`Updated ${updated.length}, but ${failed} failed. Re-run to retry those.`);
      await scan();
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Match statuses to the pipeline</DialogTitle>
          <DialogDescription>
            Every lead whose status disagrees with the steps recorded on it. From now on the
            status moves by itself when you edit a step — this is for the leads that were
            worked before that, or that arrived by import.
          </DialogDescription>
        </DialogHeader>

        {scanning ? (
          <div className="flex items-center justify-center gap-2 py-12 text-sm text-slate-500">
            <Loader2 className="h-4 w-4 animate-spin" /> Checking every lead…
          </div>
        ) : scanFailed ? (
          <div className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 px-4 py-3 text-sm text-rose-800">
            <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
            <span>
              <strong>Could not read your leads.</strong> This is not the same as having nothing
              to fix — try again rather than assuming everything is in order.
            </span>
          </div>
        ) : fixes.length === 0 ? (
          <div className="flex items-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
            <CheckCircle2 className="h-4 w-4 shrink-0" />
            <span>
              All {scanned} lead{scanned === 1 ? '' : 's'} already match their pipeline. Nothing to do.
            </span>
          </div>
        ) : (
          <>
            <div className="flex flex-wrap items-center gap-3 rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-xs">
              <span className="font-semibold text-slate-700">
                {summary.total} of {scanned} leads out of step
              </span>
              <span className="text-emerald-700">{summary.safe} straightforward</span>
              {summary.needsReview > 0 && (
                <span className="text-amber-700">{summary.needsReview} need a decision</span>
              )}
              <button
                type="button"
                onClick={() => void scan()}
                className="ml-auto inline-flex items-center gap-1 rounded-lg px-2 py-1 font-semibold text-slate-500 hover:bg-slate-200 hover:text-slate-800"
              >
                <RefreshCw className="h-3 w-3" /> Re-check
              </button>
            </div>

            <div className="max-h-[46vh] overflow-y-auto rounded-xl border border-slate-200">
              <ul className="divide-y divide-slate-100">
                {fixes.map((f) => {
                  const ticked = chosen.has(f.lead.id);
                  return (
                    <li key={f.lead.id}>
                      <label className={cn(
                        'flex cursor-pointer items-start gap-3 px-3 py-2.5 transition-colors hover:bg-slate-50',
                        ticked && 'bg-indigo-50/40',
                      )}>
                        <input
                          type="checkbox"
                          checked={ticked}
                          onChange={() => toggle(f.lead.id)}
                          className="mt-1 h-4 w-4 shrink-0 rounded border-slate-300 text-indigo-600"
                        />
                        <span className="min-w-0 flex-1">
                          <span className="flex flex-wrap items-center gap-x-2 gap-y-1">
                            <span className="truncate text-sm font-semibold text-slate-800">{f.lead.name}</span>
                            <span className="inline-flex items-center gap-1.5 text-xs">
                              <span className="rounded-md bg-slate-100 px-1.5 py-0.5 font-medium text-slate-500">
                                {f.from || '—'}
                              </span>
                              <ArrowRight className="h-3 w-3 text-slate-400" />
                              <span className="rounded-md bg-emerald-50 px-1.5 py-0.5 font-semibold text-emerald-700">
                                {f.to}
                              </span>
                            </span>
                            {!f.advances && (
                              <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-800">
                                Moves backwards
                              </span>
                            )}
                            {f.parked && (
                              <span className="rounded-full bg-amber-100 px-2 py-0.5 text-[10px] font-bold text-amber-800">
                                Deliberately set
                              </span>
                            )}
                          </span>
                          <span className="mt-0.5 block text-[11px] text-slate-500">{f.because}</span>
                        </span>
                      </label>
                    </li>
                  );
                })}
              </ul>
            </div>

            {targets.length > 0 && (
              <p className="text-[11px] leading-relaxed text-slate-500">
                <strong className="text-slate-700">Will set:</strong>{' '}
                {targets.map((t) => `${t.count} → ${t.to}`).join(' · ')}
              </p>
            )}
          </>
        )}

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={applying}>
            Close
          </Button>
          {fixes.length > 0 && (
            <Button onClick={() => void apply()} disabled={applying || chosen.size === 0}>
              {applying && <Loader2 className="mr-1.5 h-4 w-4 animate-spin" />}
              Update {chosen.size} lead{chosen.size === 1 ? '' : 's'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

export default StatusReconcileDialog;

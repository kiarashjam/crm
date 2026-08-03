// Shared glue between the pipeline editors and the pure sync logic.
//
// Owns the parts that must not be reimplemented per editor: the request
// sequence guard, the toast + Undo, and reading prefs / per-lead memory. The
// decision itself lives in `leadStatusSync.ts` and the write in
// `saveLeadPipeline.ts`.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'sonner';
import type { Lead } from '@/app/api/types';
import type { LeadPipeline } from './leadPipeline';
import {
  planStatusSync,
  statusDrift,
  previewStatusChange,
  type StatusOption,
  type StatusSyncPlan,
  type StatusDrift,
} from './leadStatusSync';
import {
  loadStatusSyncPrefs,
  getLeadSyncMeta,
  onStatusSyncChange,
  setStatusSyncEnabled,
} from './leadStatusSyncStore';
import { saveLeadPipeline, undoAutoStatus } from './saveLeadPipeline';

export interface UseLeadStatusSyncArgs {
  statusOptions: StatusOption[];
  /** False until the org's real status list has arrived. */
  statusesLoaded: boolean;
}

export interface SavePipelineOutcome {
  ok: boolean;
  lead?: Lead;
  plan: StatusSyncPlan;
  /** True when this response was superseded by a newer save and should be ignored. */
  stale: boolean;
}

export function useLeadStatusSync({ statusOptions, statusesLoaded }: UseLeadStatusSyncArgs) {
  const [prefs, setPrefs] = useState(() => loadStatusSyncPrefs());
  useEffect(() => onStatusSyncChange(() => setPrefs(loadStatusSyncPrefs())), []);

  // Monotonic sequence per lead. The inline popover fires one PUT per field
  // change, so without this a late response from an earlier edit can overwrite
  // the UI with stale values mid-typing.
  const seqRef = useRef<Map<string, number>>(new Map());

  const enabled = prefs.enabled;
  const overrides = prefs.overrides;

  /** What auto-sync would do right now, without writing anything. */
  const plan = useCallback(
    (lead: Pick<Lead, 'id' | 'status' | 'isConverted'>, pipeline: LeadPipeline): StatusSyncPlan =>
      planStatusSync({
        pipeline,
        currentStatus: lead.status ?? '',
        statusOptions,
        statusesLoaded,
        isConverted: lead.isConverted,
        enabled,
        lastAutoStatus: getLeadSyncMeta(lead.id).lastAutoStatus,
        overrides,
      }),
    [statusOptions, statusesLoaded, enabled, overrides],
  );

  /** Standing disagreement between status and pipeline, computed from state. */
  const drift = useCallback(
    (lead: Pick<Lead, 'status' | 'isConverted'>, pipeline: LeadPipeline): StatusDrift | null =>
      statusDrift({
        pipeline,
        currentStatus: lead.status ?? '',
        statusOptions,
        statusesLoaded,
        isConverted: lead.isConverted,
        enabled,
        overrides,
      }),
    [statusOptions, statusesLoaded, enabled, overrides],
  );

  /** The status a pending edit would produce, for inline hints on the control. */
  const preview = useCallback(
    (
      lead: Pick<Lead, 'id' | 'status' | 'isConverted'>,
      base: LeadPipeline,
      patch: Partial<LeadPipeline>,
    ): string | null =>
      previewStatusChange(base, patch, {
        currentStatus: lead.status ?? '',
        statusOptions,
        statusesLoaded,
        isConverted: lead.isConverted,
        enabled,
        lastAutoStatus: getLeadSyncMeta(lead.id).lastAutoStatus,
        overrides,
      })?.name ?? null,
    [statusOptions, statusesLoaded, enabled, overrides],
  );

  /**
   * Persist a pipeline edit and any implied status change, then announce it.
   *
   * The toast fires only after the PUT resolves truthy — announcing a change the
   * server refused is worse than staying quiet. `onApplied` lets the caller
   * merge the server's lead into its own state.
   */
  const save = useCallback(
    async (
      lead: Pick<Lead, 'id' | 'status' | 'isConverted' | 'name'>,
      pipeline: LeadPipeline,
      opts?: {
        log?: { subject: string; body?: string };
        onApplied?: (updated: Lead) => void;
        /** Suppress the toast (e.g. when the caller renders its own feedback). */
        quiet?: boolean;
      },
    ): Promise<SavePipelineOutcome> => {
      const seq = (seqRef.current.get(lead.id) ?? 0) + 1;
      seqRef.current.set(lead.id, seq);

      const result = await saveLeadPipeline({
        lead,
        pipeline,
        statusOptions,
        statusesLoaded,
        enabled,
        lastAutoStatus: getLeadSyncMeta(lead.id).lastAutoStatus,
        overrides,
        log: opts?.log,
      });

      // A newer save started while this one was in flight — drop the response so
      // it cannot snap the UI back to superseded values.
      const stale = (seqRef.current.get(lead.id) ?? 0) !== seq;
      if (stale) return { ok: result.ok, plan: result.plan, stale: true };

      if (!result.ok) {
        toast.error('Could not save the pipeline change');
        return { ok: false, plan: result.plan, stale: false };
      }

      if (result.lead) opts?.onApplied?.(result.lead);

      if (!opts?.quiet && result.plan.kind === 'apply') {
        const { to, from, derived } = result.plan;
        // Undo target is the status from before this burst of edits, so undoing
        // after five rapid popover commits returns the user to where they began.
        const undoTo = getLeadSyncMeta(lead.id).undoBase ?? from;
        let live = true;
        toast.success(`Status → ${to.name}`, {
          description: derived.conflicts.length
            ? `${derived.because}. Overrode: ${derived.conflicts.join('; ')}.`
            : derived.because,
          action: undoTo && undoTo !== to.name
            ? {
                label: 'Undo',
                onClick: () => {
                  // Invalidate on dismiss so a stale handle cannot resurrect an
                  // old status minutes later.
                  if (!live) return;
                  void undoAutoStatus(lead.id, undoTo, statusOptions).then((rev) => {
                    if (rev) opts?.onApplied?.(rev);
                  });
                },
              }
            : undefined,
          onDismiss: () => { live = false; },
          onAutoClose: () => { live = false; },
        });
      }

      return { ok: true, lead: result.lead, plan: result.plan, stale: false };
    },
    [statusOptions, statusesLoaded, enabled, overrides],
  );

  return useMemo(
    () => ({ enabled, setEnabled: setStatusSyncEnabled, plan, drift, preview, save }),
    [enabled, plan, drift, preview, save],
  );
}

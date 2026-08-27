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
  loadAllSyncMeta,
  recordAutoStatus,
  onStatusSyncChange,
  setStatusSyncEnabled,
} from './leadStatusSyncStore';
import { saveLeadPipeline } from './saveLeadPipeline';

export interface UseLeadStatusSyncArgs {
  statusOptions: StatusOption[];
  /** False until the org's real status list has arrived. */
  statusesLoaded: boolean;
  /** The list arrived EMPTY, or the request failed — so it is never arriving.
   *  Distinguished from "still loading" so the UI can say which it is. */
  statusesUnavailable?: boolean;
}

export interface SavePipelineOutcome {
  ok: boolean;
  lead?: Lead;
  plan: StatusSyncPlan;
  /** True when this response was superseded by a newer save and should be ignored. */
  stale: boolean;
}

export function useLeadStatusSync({
  statusOptions, statusesLoaded, statusesUnavailable,
}: UseLeadStatusSyncArgs) {
  const [prefs, setPrefs] = useState(() => loadStatusSyncPrefs());
  const [refreshToken, setRefreshToken] = useState(0);
  useEffect(() => onStatusSyncChange(() => {
    setRefreshToken((n) => n + 1);
    // The store fires one event for prefs AND per-lead meta writes, so compare
    // before replacing: handing back a fresh object on every meta write would
    // invalidate this hook's whole API (and every consumer's memo) needlessly.
    setPrefs((prev) => {
      const next = loadStatusSyncPrefs();
      const same = prev.enabled === next.enabled
        && JSON.stringify(prev.overrides) === JSON.stringify(next.overrides);
      return same ? prev : next;
    });
  }), []);

  // Monotonic sequence per lead. The inline popover fires one PUT per field
  // change, so without this a late response from an earlier edit can overwrite
  // the UI with stale values mid-typing.
  const seqRef = useRef<Map<string, number>>(new Map());

  const enabled = prefs.enabled;
  const overrides = prefs.overrides;

  // LeadPipelineTracker calls `preview` once per chip during render (17 call
  // sites). Reading the whole meta map per call meant 17 synchronous
  // JSON.parse-es per render on the keystroke path, so snapshot it once and let
  // the callbacks close over it. Refreshed by the subscription above.
  const metaSnapshot = useMemo(() => loadAllSyncMeta(), [prefs, refreshToken]);
  const lastAutoFor = useCallback(
    (leadId: string) => metaSnapshot[leadId]?.lastAutoStatus,
    [metaSnapshot],
  );

  /** What auto-sync would do right now, without writing anything. */
  const plan = useCallback(
    (lead: Pick<Lead, 'id' | 'status' | 'isConverted'>, pipeline: LeadPipeline): StatusSyncPlan =>
      planStatusSync({
        pipeline,
        currentStatus: lead.status ?? '',
        statusOptions,
        statusesLoaded,
        statusesUnavailable,
        isConverted: lead.isConverted,
        enabled,
        lastAutoStatus: lastAutoFor(lead.id),
        overrides,
      }),
    [statusOptions, statusesLoaded, statusesUnavailable, enabled, overrides, lastAutoFor],
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
        // No `enabled`: drift describes the data, so it stays visible when
        // auto-sync is off — that is when the strip is the only way to fix it.
        overrides,
      }),
    [statusOptions, statusesLoaded, overrides],
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
        statusesUnavailable,
        isConverted: lead.isConverted,
        enabled,
        lastAutoStatus: lastAutoFor(lead.id),
        overrides,
      })?.name ?? null,
    [statusOptions, statusesLoaded, statusesUnavailable, enabled, overrides, lastAutoFor],
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
        statusesUnavailable,
        enabled,
        lastAutoStatus: lastAutoFor(lead.id),
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

      // Recorded here, not in saveLeadPipeline: only this layer knows the
      // response was not superseded.
      if (result.plan.kind === 'apply') {
        recordAutoStatus(lead.id, {
          from: result.previousStatus,
          to: result.plan.to.name,
          rule: result.plan.derived.rule,
          because: result.plan.derived.because,
        });
      }

      if (result.lead) opts?.onApplied?.(result.lead);

      if (!opts?.quiet && result.plan.kind === 'apply') {
        const { to, derived } = result.plan;
        // No Undo. It used to offer one, which made sense while the status was
        // also a field somebody could set: undo meant "put my choice back". The
        // status is now derived, so the only thing an undo could do is write a
        // status the pipeline does not support — which the very next derivation
        // would overwrite. The real undo is to correct the step, and the toast
        // says which step it was.
        toast.success(`Status → ${to.name}`, {
          description: derived.conflicts.length
            ? `${derived.because}. Overrode: ${derived.conflicts.join('; ')}.`
            : derived.because,
        });
      }

      return { ok: true, lead: result.lead, plan: result.plan, stale: false };
    },
    [statusOptions, statusesLoaded, statusesUnavailable, enabled, overrides, lastAutoFor],
  );

  return useMemo(
    () => ({ enabled, setEnabled: setStatusSyncEnabled, plan, drift, preview, save }),
    [enabled, plan, drift, preview, save],
  );
}

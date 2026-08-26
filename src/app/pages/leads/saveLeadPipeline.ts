// THE single write path for lead pipeline edits.
//
// All three interactive editors (LeadPipelineTracker via LeadDetailPage,
// SalesTrackerCard, InlineSalesEditorPopover) call `saveLeadPipeline` so they
// cannot diverge on what a click means, what gets logged, or whether the status
// moves. Do not call `updateLead({ pipelineState })` directly from a component.
//
// Pipeline and status travel in ONE PUT: a split write can leave the server with
// a phase recorded but the status not moved (or vice versa), which is exactly
// the inconsistency this feature exists to remove.

import { updateLead } from '@/app/api/leads';
import { createActivity } from '@/app/api/activities';
import type { Lead } from '@/app/api/types';
import { serializePipeline, type LeadPipeline } from './leadPipeline';
import {
  planStatusSync,
  type StatusOption,
  type StatusSyncPlan,
  type CanonicalStage,
} from './leadStatusSync';

export interface SavePipelineArgs {
  lead: Pick<Lead, 'id' | 'status' | 'isConverted'>;
  /** The complete next pipeline (not a patch). */
  pipeline: LeadPipeline;
  statusOptions: StatusOption[];
  statusesLoaded: boolean;
  enabled: boolean;
  lastAutoStatus?: string;
  overrides?: Partial<Record<CanonicalStage, string>>;
  /** Activity line describing the pipeline edit itself, if the caller wants one. */
  log?: { subject: string; body?: string };
}

export interface SavePipelineResult {
  ok: boolean;
  /** The server's updated lead, when the PUT succeeded. */
  lead?: Lead;
  /** What auto-sync decided. Callers use this to render the toast / strip. */
  plan: StatusSyncPlan;
  /** Status the lead had before this save — the Undo target. */
  previousStatus: string;
}

/**
 * Persist a pipeline edit, applying any status change the pipeline now implies.
 *
 * Never throws: failures come back as `ok: false` so the caller can roll its
 * optimistic update back. The toast is intentionally the caller's job, and must
 * only fire on `ok === true` — announcing a change the server refused is worse
 * than staying quiet.
 */
export async function saveLeadPipeline(args: SavePipelineArgs): Promise<SavePipelineResult> {
  const { lead, pipeline, log } = args;
  const previousStatus = lead.status ?? '';

  const plan = planStatusSync({
    pipeline,
    currentStatus: previousStatus,
    statusOptions: args.statusOptions,
    statusesLoaded: args.statusesLoaded,
    isConverted: lead.isConverted,
    enabled: args.enabled,
    lastAutoStatus: args.lastAutoStatus,
    overrides: args.overrides,
  });

  const patch: Parameters<typeof updateLead>[1] = {
    pipelineState: serializePipeline(pipeline),
  };
  if (plan.kind === 'apply') {
    patch.status = plan.to.name;
    // Manual status picks send `leadStatusId` alongside `status`; auto-sync must
    // too, or the server keeps a status id that contradicts the status name.
    if (plan.to.id) patch.leadStatusId = plan.to.id;
  }

  let updated: Lead | null = null;
  try {
    updated = await updateLead(lead.id, patch);
  } catch {
    return { ok: false, plan, previousStatus };
  }
  if (!updated) return { ok: false, plan, previousStatus };

  // NB: `lastAutoStatus` is deliberately NOT recorded here. Only the caller
  // knows whether this response was superseded by a newer save, and recording
  // it for a stale response would leave the memory out of step with the status
  // actually on the lead — which the next edit would read as a manual change.
  // `useLeadStatusSync` records it after its sequence check.

  // Activity writes are best-effort: the timeline is an audit convenience, and a
  // failed log must never make a successful save look broken.
  const activities: { subject: string; body?: string }[] = [];
  if (log) activities.push(log);
  if (plan.kind === 'apply') activities.push(plan.activity);
  for (const entry of activities) {
    void createActivity({
      type: 'system',
      subject: entry.subject,
      body: entry.body,
      leadId: lead.id,
    }).catch(() => { /* non-fatal */ });
  }

  return { ok: true, lead: updated, plan, previousStatus };
}


// THE single write path for lead pipeline edits.
//
// All three interactive editors (LeadPipelineTracker via LeadDetailPage,
// SalesTrackerCard, InlineSalesEditorPopover) call `saveLeadPipeline` so they
// cannot diverge on what a click means, what gets logged, or whether the status
// moves. Do not call `updateLead({ pipelineState })` directly from a component.
//
// Only the pipeline is sent. The server works the status out from it (see
// LeadStatusDerivation.cs) and returns the lead with the status it settled on,
// which is what the editors then show. This browser used to decide the status
// and send it along, so anything else that wrote a pipeline (an e-signed
// contract, a merge, an integration) left the status behind. The local plan is
// still computed, but only to explain the server's answer in the toast.
//
// And only the steps the edit changed are sent (see pipelinePatch); the server
// merges them into the pipeline it has. Sending the whole pipeline wrote this
// tab's copy over the stored one, so a step recorded meanwhile elsewhere, such as
// the contract steps an e-signature writes, was lost.

import { updateLead } from '@/app/api/leads';
import { createActivity } from '@/app/api/activities';
import type { Lead } from '@/app/api/types';
import { pipelinePatch, type LeadPipeline } from './leadPipeline';
import {
  planStatusSync,
  classifyStatusLabel,
  buildActivity,
  type StatusOption,
  type StatusSyncPlan,
  type CanonicalStage,
  type ResolvedStatus,
} from './leadStatusSync';

export interface SavePipelineArgs {
  lead: Pick<Lead, 'id' | 'status' | 'isConverted'>;
  /** The complete next pipeline (not a patch). */
  pipeline: LeadPipeline;
  /** The pipeline the edit was made on. Only what differs from it is sent. */
  base: LeadPipeline;
  statusOptions: StatusOption[];
  statusesLoaded: boolean;
  /** The status list arrived empty / failed, so it is never arriving. Only
   *  affects the SkipReason of the local preview. */
  statusesUnavailable?: boolean;
  lastAutoStatus?: string;
  overrides?: Partial<Record<CanonicalStage, string>>;
  /** Activity line describing the pipeline edit itself, if the caller wants one. */
  log?: { subject: string; body?: string };
}

export interface SavePipelineResult {
  ok: boolean;
  /** The server's updated lead, when the PUT succeeded. */
  lead?: Lead;
  /** What happened to the status, as the server decided it. Callers use this to
   *  render the toast / strip. Before the server answers, it is the preview. */
  plan: StatusSyncPlan;
  /** Status the lead had before this save. */
  previousStatus: string;
}

/**
 * The plan that describes what the server did, given what this browser expected.
 *
 * The server's status is the truth. When it matches the preview, the preview's
 * explanation stands. When the server moved the status somewhere else (its
 * status list was newer than ours, or this tab was stale), the move is reported
 * as the server made it, still explained by the step that caused it. When the
 * server kept the status, nothing is announced.
 */
export function planFromServer(
  preview: StatusSyncPlan,
  previousStatus: string,
  server: Pick<Lead, 'status' | 'leadStatusId'>,
): StatusSyncPlan {
  const after = server.status ?? '';
  if (after === previousStatus) {
    return preview.kind === 'apply'
      ? { kind: 'skip', reason: 'server_kept', derived: preview.derived }
      : preview;
  }

  if (preview.kind === 'apply' && preview.to.name === after) {
    return { ...preview, to: { ...preview.to, id: server.leadStatusId ?? preview.to.id } };
  }

  const to: ResolvedStatus = {
    name: after,
    id: server.leadStatusId,
    match: 'exact',
    canonical: classifyStatusLabel(after) ?? preview.derived.stage,
  };
  return {
    kind: 'apply',
    from: previousStatus,
    to,
    derived: preview.derived,
    activity: buildActivity(previousStatus, to, preview.derived),
  };
}

/**
 * Persist a pipeline edit and report the status the server settled on.
 *
 * Never throws: failures come back as `ok: false` so the caller can roll its
 * optimistic update back. The toast is intentionally the caller's job, and must
 * only fire on `ok === true` — announcing a change the server refused is worse
 * than staying quiet.
 */
export async function saveLeadPipeline(args: SavePipelineArgs): Promise<SavePipelineResult> {
  const { lead, pipeline, log } = args;
  const previousStatus = lead.status ?? '';

  const preview = planStatusSync({
    pipeline,
    currentStatus: previousStatus,
    statusOptions: args.statusOptions,
    statusesLoaded: args.statusesLoaded,
    statusesUnavailable: args.statusesUnavailable,
    isConverted: lead.isConverted,
    lastAutoStatus: args.lastAutoStatus,
    overrides: args.overrides,
  });

  let updated: Lead | null = null;
  try {
    updated = await updateLead(lead.id, { pipelineState: JSON.stringify(pipelinePatch(args.base, pipeline)) });
  } catch {
    return { ok: false, plan: preview, previousStatus };
  }
  if (!updated) return { ok: false, plan: preview, previousStatus };

  // NB: `lastAutoStatus` is deliberately NOT recorded here. Only the caller
  // knows whether this response was superseded by a newer save, and recording
  // it for a stale response would leave the memory out of step with the status
  // actually on the lead. `useLeadStatusSync` records it after its sequence check.

  // Best-effort: the timeline is an audit convenience, and a failed log must
  // never make a successful save look broken. The entry explaining a status move
  // is the server's to write, since it is the server that moved it.
  if (log) {
    void createActivity({
      type: 'system',
      subject: log.subject,
      body: log.body,
      leadId: lead.id,
    }).catch(() => { /* non-fatal */ });
  }

  return { ok: true, lead: updated, plan: planFromServer(preview, previousStatus, updated), previousStatus };
}

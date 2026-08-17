// Finding — and fixing — every lead whose status disagrees with its pipeline.
//
// Auto-sync moves the status when someone edits a step, and it works. But it only
// ever runs at the moment of an edit, which leaves three populations of lead
// permanently out of step:
//
//   · leads whose pipeline was recorded BEFORE auto-sync existed;
//   · leads whose pipeline arrived by import or webhook rather than by a click;
//   · leads edited in the brief window after page load before the org's status
//     list has arrived, where `planStatusSync` correctly skips (it must not guess
//     against a list it has not seen) and nothing retries afterwards.
//
// The per-lead drift strip already surfaces this on the detail page, but reaching
// it means opening every lead in turn. For a whole book of leads that is not a fix.
//
// Everything here delegates to `statusDrift` — the same function the strip uses.
// Re-deriving "is this lead out of step" would be a fourth copy of a rule that has
// already drifted three times in this codebase.

import type { Lead } from '@/app/api/types';
import { parsePipeline } from './leadPipeline';
import {
  statusDrift, isStickyStatus, classifyStatusLabel, STAGE_TIER,
  type StatusOption, type CanonicalStage, type StatusDrift,
} from './leadStatusSync';

export interface LeadStatusFix {
  lead: Lead;
  /** Status the lead has now. */
  from: string;
  /** Status its pipeline implies — always a name from the org's own list. */
  to: string;
  /** Status id to write alongside the name, when the option is a real record. */
  toId?: string;
  /** Why, in words, for the review list. */
  because: string;
  /**
   * True when this is forward movement on the org's ladder. Backward moves are
   * offered but never pre-selected: a status that has run AHEAD of the pipeline is
   * usually somebody deliberately marking something early, and silently demoting
   * it would be the automation overruling them.
   */
  advances: boolean;
  /**
   * True when the current status looks deliberately parked — Do not contact, On
   * hold, or a label we cannot classify at all. Never pre-selected.
   */
  parked: boolean;
}

export interface ReconcileArgs {
  leads: Lead[];
  statusOptions: StatusOption[];
  statusesLoaded: boolean;
  overrides?: Partial<Record<CanonicalStage, string>>;
}

const tierOf = (label: string): number | null => {
  const stage = label ? classifyStatusLabel(label) : 'new';
  return stage ? STAGE_TIER[stage] : null;
};

/**
 * Every lead whose status disagrees with its pipeline.
 *
 * Converted leads are excluded by `statusDrift` itself — their status belongs to
 * the deal now. Leads with no pipeline recorded produce no drift, so an untouched
 * book of business is correctly reported as clean rather than as hundreds of
 * "fixes" that would all write New over New.
 */
export function findStatusFixes(args: ReconcileArgs): LeadStatusFix[] {
  if (!args.statusesLoaded || args.statusOptions.length === 0) return [];

  const out: LeadStatusFix[] = [];
  for (const lead of args.leads) {
    const drift: StatusDrift | null = statusDrift({
      pipeline: parsePipeline(lead.pipelineState),
      currentStatus: lead.status ?? '',
      statusOptions: args.statusOptions,
      statusesLoaded: true,
      isConverted: lead.isConverted,
      overrides: args.overrides,
    });
    if (!drift) continue;

    const from = drift.currentStatus;
    const fromTier = tierOf(from);
    const toTier = STAGE_TIER[drift.suggested.canonical];

    out.push({
      lead,
      from,
      to: drift.suggested.name,
      toId: drift.suggested.id,
      because: drift.derived.because,
      // A terminal is off the ladder, so moving to one is never a regression.
      advances: drift.derived.terminal
        || (toTier !== null && fromTier !== null && toTier > fromTier),
      parked: isStickyStatus(from) || (!!from && classifyStatusLabel(from) === null),
    });
  }
  return out;
}

/**
 * The fixes safe to apply without asking about each one: forward movement on a
 * status nobody has deliberately parked.
 *
 * This is the set the button pre-selects. Everything else stays in the list,
 * unticked, so it is visible and deliberate rather than hidden.
 */
export function safeFixes(fixes: LeadStatusFix[]): LeadStatusFix[] {
  return fixes.filter((f) => f.advances && !f.parked);
}

export interface FixSummary {
  total: number;
  safe: number;
  needsReview: number;
}

export function summariseFixes(fixes: LeadStatusFix[]): FixSummary {
  const safe = safeFixes(fixes).length;
  return { total: fixes.length, safe, needsReview: fixes.length - safe };
}

/** Group by target status, for a compact "what will change" preview. */
export function groupFixesByTarget(fixes: LeadStatusFix[]): { to: string; count: number }[] {
  const m = new Map<string, number>();
  for (const f of fixes) m.set(f.to, (m.get(f.to) ?? 0) + 1);
  return [...m.entries()]
    .map(([to, count]) => ({ to, count }))
    .sort((a, b) => b.count - a.count);
}

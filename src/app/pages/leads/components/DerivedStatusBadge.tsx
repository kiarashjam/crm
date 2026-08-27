// The lead's status, shown as the result it is rather than a field to fill in.
//
// The status used to be a picker. It is now derived from the 5-phase pipeline and
// nothing else, so a picker would be a control that either fights the derivation
// or silently gets overwritten by it — and both are worse than no control.
//
// What replaces it has to answer the question the picker used to answer badly:
// "why does this say Contract Sent?" So the badge carries its own cause, and the
// place to change it is the step that produced it.

import { Lock, TriangleAlert } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { classifyStatusLabel, deriveStage } from '../leadStatusSync';
import type { LeadPipeline } from '../leadPipeline';

/** Tone per canonical stage family, so the colour means the same thing everywhere. */
function toneFor(status: string, terminal: boolean, stale: boolean): string {
  // A status that disagrees with its own pipeline is not any of the confident
  // colours: it is a warning, and painting it emerald would say "signed" about a
  // lead whose tracker says nothing of the kind.
  //
  // DASHED, not merely amber. Amber is already the settled colour for a contacted
  // lead, so an amber warning was indistinguishable from an ordinary one — the
  // test that asserted "a disagreement looks different" is what caught it. No
  // other tone uses a dashed border, so this one cannot be confused with any of
  // them even at a glance.
  if (stale) return 'border-dashed border-amber-500 bg-amber-50 text-amber-900';
  if (terminal) return 'border-slate-200 bg-slate-100 text-slate-600';
  const s = status.trim().toLowerCase();
  if (s.includes('signed') || s.includes('deposit') || s.includes('won') || s.includes('deal')) {
    return 'border-emerald-200 bg-emerald-50 text-emerald-700';
  }
  if (s.includes('contract') || s.includes('signature') || s.includes('qualified')) {
    return 'border-indigo-200 bg-indigo-50 text-indigo-700';
  }
  if (s.includes('contact') || s.includes('connected') || s.includes('progress')) {
    return 'border-amber-200 bg-amber-50 text-amber-700';
  }
  if (s.includes('lost') || s.includes('unqualified') || s.includes('rejected')) {
    return 'border-slate-200 bg-slate-100 text-slate-600';
  }
  return 'border-blue-200 bg-blue-50 text-blue-700';
}

export interface DerivedStatusBadgeProps {
  status: string;
  pipeline: LeadPipeline;
  className?: string;
  /** Hides the padlock where the surrounding copy already says it is automatic. */
  showLock?: boolean;
  /**
   * This workspace has no lead statuses configured, so there is nothing for the
   * pipeline to set the status TO and the write is held on every edit.
   *
   * Without this the badge went stale and told the user to "reconcile them from
   * the tracker below" — advice that cannot be followed, because the tracker has
   * no status to reconcile against either. Naming the real cause is the whole
   * point of the badge carrying one.
   */
  statusesUnavailable?: boolean;
}

/**
 * A read-only status badge that says what set it.
 *
 * The cause goes in `title` as well as in the accessible name: "why is this
 * Qualified" is asked by hovering, and answering it in a tooltip is what stops
 * the missing picker reading as a missing feature.
 */
export function DerivedStatusBadge({
  status, pipeline, className, showLock = true, statusesUnavailable = false,
}: DerivedStatusBadgeProps) {
  const derived = deriveStage(pipeline);

  // Whether the status on the lead actually MATCHES what the pipeline derives.
  //
  // It usually does, because every pipeline edit writes it. But it can lag: an
  // imported lead, a colleague's edit this browser has not caught up with, or an
  // organisation whose status list has not loaded yet, which deliberately holds
  // auto-sync back. Rendering the badge is what showed the cost of not checking —
  // it said "New" under a tooltip claiming the pipeline had set it to Profile
  // Rejected. A badge that misreports its own cause is worse than one with no
  // tooltip at all.
  const shownStage = status ? classifyStatusLabel(status) : 'new';
  const stale = derived.phase > 0 && shownStage !== derived.stage;

  const cause = derived.phase === 0
    ? 'No pipeline steps recorded yet, so this is the lead\u2019s starting status.'
    : statusesUnavailable
      ? `The sales pipeline records ${derived.because} (Phase ${derived.phase}), but this `
        + 'workspace has no lead statuses set up, so there is nothing to move the status to. '
        + 'Add them in Settings \u2192 Lead statuses and it will catch up on the next step you log.'
      : stale
        ? `This says "${status}", but the sales pipeline records ${derived.because} `
          + `(Phase ${derived.phase}). Reconcile them from the tracker below.`
        : `Set automatically from the sales pipeline: ${derived.because} (Phase ${derived.phase}). `
          + 'Change it by editing that step.';

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1.5 rounded-lg border px-2.5 py-1 text-xs font-bold',
        'shadow-sm ring-1 ring-black/5',
        toneFor(status, derived.terminal, stale),
        className,
      )}
      title={cause}
    >
      {showLock && (stale
        ? <TriangleAlert className="h-3 w-3 opacity-70" aria-hidden />
        : <Lock className="h-3 w-3 opacity-50" aria-hidden />)}
      {status || 'No status'}
      {/* The cause, for anyone not using a pointer. `title` alone is invisible to
          a screen reader on a non-interactive element. */}
      <span className="sr-only">. {cause}</span>
    </span>
  );
}

export default DerivedStatusBadge;

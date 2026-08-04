// Explains — and where necessary resolves — the relationship between a lead's
// status and its pipeline.
//
// Deliberately computed from the CURRENT (status, pipeline) pair rather than
// from the last edit, so it survives a reload, catches drift a colleague caused
// on another machine, and covers the cases auto-sync declines to write
// (regressions, no-shows, cleared pipelines, deliberately parked statuses).

import { useState } from 'react';
import { Wand2, Info, Check, X, Loader2 } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import type { StatusDrift, SuggestReason } from '../leadStatusSync';
import { describeSuggestReason } from '../leadStatusSync';

interface Props {
  /** Standing disagreement, or null when status and pipeline already agree. */
  drift: StatusDrift | null;
  /** Why auto-sync declined to write it, when it declined. */
  reasons?: SuggestReason[];
  enabled: boolean;
  onToggleEnabled: (next: boolean) => void;
  /** Apply the suggested status. */
  onApply: (statusName: string) => Promise<void> | void;
  /** Dismiss this suggestion without changing anything. */
  onDismiss?: () => void;
  disabled?: boolean;
  className?: string;
}

export function StatusSyncStrip({
  drift, reasons, enabled, onToggleEnabled, onApply, onDismiss, disabled, className,
}: Props) {
  const [busy, setBusy] = useState(false);

  const apply = async () => {
    if (!drift) return;
    setBusy(true);
    try {
      await onApply(drift.suggested.name);
    } finally {
      setBusy(false);
    }
  };

  // Nothing to reconcile — just state how the feature is behaving.
  if (!drift) {
    return (
      <div className={cn(
        'flex flex-wrap items-center justify-between gap-2 rounded-xl border border-slate-200 bg-white/70 px-3 py-2',
        className,
      )}>
        <span className="inline-flex items-center gap-2 text-xs text-slate-500">
          <Wand2 className={cn('h-3.5 w-3.5', enabled ? 'text-indigo-500' : 'text-slate-300')} />
          {enabled
            ? 'Lead status follows the pipeline automatically.'
            : 'Automatic status updates are off for you.'}
        </span>
        <button
          type="button"
          onClick={() => onToggleEnabled(!enabled)}
          className="text-[11px] font-semibold text-indigo-600 hover:text-indigo-800"
        >
          {enabled ? 'Turn off' : 'Turn on'}
        </button>
      </div>
    );
  }

  const current = drift.currentStatus || '—';
  const target = drift.suggested.name;

  return (
    <div className={cn(
      'rounded-xl border border-amber-200 bg-amber-50/80 px-3 py-2.5',
      className,
    )}>
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div className="flex min-w-0 items-start gap-2">
          <Info className="mt-0.5 h-4 w-4 shrink-0 text-amber-600" />
          <div className="min-w-0">
            <p className="text-xs font-semibold text-amber-900">
              The pipeline is at <strong>{drift.derived.because}</strong>, but the status says &ldquo;{current}&rdquo;.
            </p>
            {/* Every applicable hold is listed: showing only the first strands
                the user on "I released that one and nothing happened". */}
            {reasons && reasons.length > 0 && (
              <ul className="mt-1 space-y-0.5">
                {reasons.map((r) => (
                  <li key={r} className="text-[11px] text-amber-700">
                    · {describeSuggestReason(r, current)}
                  </li>
                ))}
              </ul>
            )}
            {/* Without this the strip prompts for a change while offering no
                clue why nothing happened on its own. */}
            {!enabled && (
              <p className="mt-1 text-[11px] text-amber-700">
                · Automatic status updates are off for you, so this is yours to apply.
              </p>
            )}
            {drift.derived.conflicts.length > 0 && (
              <p className="mt-1 text-[11px] text-amber-700">
                Overrides: {drift.derived.conflicts.join('; ')}.
              </p>
            )}
          </div>
        </div>

        {/* Buttons name the OUTCOME, so they can be read without the sentence. */}
        <div className="flex shrink-0 items-center gap-1.5">
          <button
            type="button"
            disabled={disabled || busy}
            onClick={() => void apply()}
            className="inline-flex items-center gap-1 rounded-lg bg-amber-600 px-2.5 py-1 text-[11px] font-semibold text-white hover:bg-amber-700 disabled:opacity-50"
          >
            {busy ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3" />}
            Set to {target}
          </button>
          {!enabled && (
            <button
              type="button"
              onClick={() => onToggleEnabled(true)}
              className="rounded-lg border border-amber-300 bg-white px-2.5 py-1 text-[11px] font-semibold text-amber-800 hover:bg-amber-50"
            >
              Turn on
            </button>
          )}
          {onDismiss && (
            <button
              type="button"
              disabled={disabled || busy}
              onClick={onDismiss}
              className="inline-flex items-center gap-1 rounded-lg border border-amber-300 bg-white px-2.5 py-1 text-[11px] font-semibold text-amber-800 hover:bg-amber-50 disabled:opacity-50"
            >
              <X className="h-3 w-3" />
              Keep {current}
            </button>
          )}
        </div>
      </div>
    </div>
  );
}

export default StatusSyncStrip;

import { useId } from 'react';
import { cn } from '@/app/components/ui/utils';
import { toLocalInputValue } from '@/app/lib/dateInputs';
import {
  DIRECTION_LABELS,
  MAX_DURATION_MINUTES,
  OUTCOME_LABELS,
  hasDirection,
  hasDuration,
  outcomesFor,
  type ActivityFactsDraft,
} from '@/app/lib/activityFacts';

const field = 'h-9 w-full rounded-lg border border-slate-200 bg-white px-2.5 text-sm text-slate-800 focus:border-emerald-400 focus:outline-none focus:ring-2 focus:ring-emerald-500/20 dark:border-slate-600 dark:bg-slate-800 dark:text-slate-100';
const label = 'mb-1 block text-[11px] font-semibold uppercase tracking-wide text-slate-500';

/**
 * When an activity happened, and for a call or meeting how it went, how long it took
 * and who reached out. Only what the kind of activity can carry is shown: a note asks
 * for nothing but its time.
 */
export function ActivityFactsFields({ type, value, onChange, className, isEdit = false }: {
  type: string;
  value: ActivityFactsDraft;
  onChange: (next: ActivityFactsDraft) => void;
  className?: string;
  /** Editing a saved activity: an empty time keeps the one it has, rather than meaning now. */
  isEdit?: boolean;
}) {
  const id = useId();
  const outcomes = outcomesFor(type);
  const set = (patch: Partial<ActivityFactsDraft>) => onChange({ ...value, ...patch });
  return (
    <div className={cn('grid grid-cols-2 gap-2 sm:grid-cols-4', className)}>
      <div className="col-span-2">
        <label htmlFor={`${id}-when`} className={label}>When</label>
        <input
          id={`${id}-when`}
          type="datetime-local"
          value={value.when}
          max={toLocalInputValue(new Date().toISOString())}
          onChange={(e) => set({ when: e.target.value })}
          aria-describedby={`${id}-when-hint`}
          className={field}
        />
        <span id={`${id}-when-hint`} className="sr-only">
          {isEdit ? 'When it happened.' : 'Leave empty for now.'}
        </span>
      </div>
      {hasDirection(type) && (
        <div>
          <label htmlFor={`${id}-direction`} className={label}>Direction</label>
          <select
            id={`${id}-direction`}
            value={value.direction}
            onChange={(e) => set({ direction: e.target.value as ActivityFactsDraft['direction'] })}
            className={field}
          >
            <option value="">Not said</option>
            <option value="outbound">{DIRECTION_LABELS.outbound}</option>
            <option value="inbound">{DIRECTION_LABELS.inbound}</option>
          </select>
        </div>
      )}
      {outcomes.length > 0 && (
        <div>
          <label htmlFor={`${id}-outcome`} className={label}>Outcome</label>
          <select id={`${id}-outcome`} value={value.outcome} onChange={(e) => set({ outcome: e.target.value })} className={field}>
            <option value="">Not said</option>
            {outcomes.map((outcome) => <option key={outcome} value={outcome}>{OUTCOME_LABELS[outcome] ?? outcome}</option>)}
          </select>
        </div>
      )}
      {hasDuration(type) && (
        <div>
          <label htmlFor={`${id}-duration`} className={label}>Minutes</label>
          <input
            id={`${id}-duration`}
            type="number"
            inputMode="numeric"
            min={0}
            max={MAX_DURATION_MINUTES}
            step={1}
            value={value.duration}
            onChange={(e) => set({ duration: e.target.value })}
            className={field}
          />
        </div>
      )}
    </div>
  );
}

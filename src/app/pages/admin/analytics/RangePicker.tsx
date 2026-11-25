// The period: Today, 7, 30 or 90 days, or two days picked. A row of buttons like the
// Reports page's "Whose", and the app's own date fields for a custom period.

import { CalendarRange } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { DateInput } from '@/app/components/DateInput';
import { RANGE_PRESETS, rangeFor, type RangePreset, type RangeSelection } from './range';

export function RangePicker({ selection, onChange }: { selection: RangeSelection; onChange: (next: RangeSelection) => void }) {
  const pick = (preset: RangePreset) => {
    if (preset === selection.preset) return;
    if (preset === 'custom') {
      // Start the custom days from the period being looked at.
      const current = rangeFor(selection);
      onChange({ preset, customFrom: selection.customFrom || current?.from || '', customTo: selection.customTo || current?.to || '' });
    } else {
      onChange({ ...selection, preset });
    }
  };
  const custom = selection.preset === 'custom';
  const invalid = custom && selection.customFrom !== '' && selection.customTo !== '' && rangeFor(selection) === null;
  return (
    <div className="flex flex-wrap items-end gap-x-3 gap-y-2">
      <div role="group" aria-label="Period" className="flex h-9 max-w-full overflow-x-auto rounded-xl border border-slate-200 bg-white p-0.5 dark:border-white/10 dark:bg-white/5">
        {RANGE_PRESETS.map((p) => (
          <button
            key={p.value}
            type="button"
            aria-pressed={selection.preset === p.value}
            onClick={() => pick(p.value)}
            className={cn(
              'inline-flex shrink-0 items-center gap-1 rounded-[10px] px-2.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 sm:px-3',
              selection.preset === p.value
                // gray, not slate: the app's dark theme repaints slate-backed pills as cards.
                ? 'bg-gray-900 text-white shadow-sm dark:bg-white dark:text-gray-900'
                : 'text-slate-600 hover:bg-slate-50 hover:text-slate-900 dark:text-slate-300 dark:hover:bg-white/10 dark:hover:text-white',
            )}
          >
            {p.value === 'custom' && <CalendarRange className="h-3.5 w-3.5" aria-hidden />}
            {p.label}
          </button>
        ))}
      </div>
      {custom && (
        <div className="flex flex-wrap items-end gap-2">
          <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
            From
            <span className="w-[9.5rem] font-normal normal-case tracking-normal">
              <DateInput value={selection.customFrom} onChange={(v) => onChange({ ...selection, customFrom: v })} className="h-9" aria-label="From" />
            </span>
          </label>
          <label className="flex flex-col gap-1 text-[11px] font-semibold uppercase tracking-wide text-slate-500">
            To
            <span className="w-[9.5rem] font-normal normal-case tracking-normal">
              <DateInput value={selection.customTo} onChange={(v) => onChange({ ...selection, customTo: v })} className="h-9" aria-label="To" />
            </span>
          </label>
        </div>
      )}
      {invalid && <p role="alert" className="w-full text-xs text-amber-700 dark:text-amber-300">Pick an end day on or after the start day.</p>}
    </div>
  );
}

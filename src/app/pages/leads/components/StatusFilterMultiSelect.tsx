import { Check, ChevronDown, Tag, X } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';

interface StatusFilterMultiSelectProps {
  /** Available status names. */
  options: string[];
  /** Currently selected statuses. Empty array = "all". */
  selected: string[];
  onChange: (next: string[]) => void;
}

const DOT_COLORS: Record<string, string> = {
  New: 'bg-blue-500',
  Contacted: 'bg-amber-500',
  Qualified: 'bg-emerald-500',
  Lost: 'bg-slate-400',
};

/**
 * Multi-select status filter for the leads page. Empty selection means "all
 * statuses". Renders a popover with a checkbox list plus quick All / Clear
 * actions, matching the dark filter-panel styling used elsewhere on the page.
 */
export function StatusFilterMultiSelect({ options, selected, onChange }: StatusFilterMultiSelectProps) {
  const toggle = (status: string) => {
    if (selected.includes(status)) {
      onChange(selected.filter((s) => s !== status));
    } else {
      onChange([...selected, status]);
    }
  };

  const label =
    selected.length === 0
      ? 'All statuses'
      : selected.length === 1
        ? selected[0]
        : `${selected.length} statuses`;

  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          className="flex h-10 w-full min-w-[160px] items-center justify-between gap-2 rounded-lg border border-white/10 bg-white/10 px-3 text-sm text-white transition-colors hover:bg-white/15 focus:outline-none focus-visible:ring-2 focus-visible:ring-orange-400/40"
          aria-label="Filter by status"
        >
          <span className="flex items-center gap-2 truncate">
            <span
              className={cn(
                'w-2 h-2 rounded-full shrink-0',
                selected.length === 1 ? (DOT_COLORS[selected[0]!] ?? 'bg-slate-400') : 'bg-slate-300',
              )}
            />
            <span className="truncate">{label}</span>
          </span>
          <ChevronDown className="w-4 h-4 shrink-0 opacity-70" />
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-60 p-2">
        <div className="flex items-center justify-between px-1 pb-1.5">
          <span className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-slate-400">
            <Tag className="w-3 h-3" />
            Status
          </span>
          {selected.length > 0 && (
            <button
              type="button"
              onClick={() => onChange([])}
              className="inline-flex items-center gap-1 text-xs font-medium text-slate-500 hover:text-slate-700"
            >
              <X className="w-3 h-3" />
              Clear
            </button>
          )}
        </div>
        <div className="max-h-64 overflow-y-auto">
          {options.map((status) => {
            const active = selected.includes(status);
            return (
              <button
                key={status}
                type="button"
                onClick={() => toggle(status)}
                className={cn(
                  'flex w-full items-center justify-between gap-2 rounded-md px-2 py-1.5 text-sm transition-colors',
                  active ? 'bg-indigo-50 text-indigo-900 font-medium' : 'text-slate-700 hover:bg-slate-50',
                )}
              >
                <span className="flex items-center gap-2">
                  <span className={cn('w-2 h-2 rounded-full', DOT_COLORS[status] ?? 'bg-slate-400')} />
                  {status}
                </span>
                <span
                  className={cn(
                    'flex h-4 w-4 items-center justify-center rounded border transition-colors',
                    active ? 'border-indigo-500 bg-indigo-500 text-white' : 'border-slate-300',
                  )}
                >
                  {active && <Check className="w-3 h-3" />}
                </span>
              </button>
            );
          })}
        </div>
      </PopoverContent>
    </Popover>
  );
}

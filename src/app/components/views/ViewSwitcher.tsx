// The control on every list page that changes how its records are shown.
//
// A row of layouts — cards, list, table, board, whichever the page offers — with a
// highlight that slides to the one chosen, and beside it the view options: the same
// layouts with words, roomy or compact, which columns, and a way back to how the
// page comes. Every choice is saved for the person (see lib/viewPreferences).

import { useId, type ElementType } from 'react';
import { motion } from 'motion/react';
import { Check, Columns3, LayoutGrid, LayoutList, RotateCcw, Rows2, Rows3, SlidersHorizontal, Table2 } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Switch } from '@/app/components/ui/switch';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import type { ViewDensity, ViewLayout, ViewPreference } from '@/app/lib/viewPreferences';

export const LAYOUT_INFO: Record<ViewLayout, { label: string; icon: ElementType; hint: string }> = {
  grid: { label: 'Cards', icon: LayoutGrid, hint: 'Big, colourful cards' },
  list: { label: 'List', icon: LayoutList, hint: 'One row each, easy to scan' },
  table: { label: 'Table', icon: Table2, hint: 'Columns side by side' },
  board: { label: 'Board', icon: Columns3, hint: 'Columns by stage' },
};

const DENSITY: { value: ViewDensity; label: string; icon: ElementType }[] = [
  { value: 'comfortable', label: 'Roomy', icon: Rows2 },
  { value: 'compact', label: 'Compact', icon: Rows3 },
];

export interface ColumnChoice {
  id: string;
  label: string;
  /** Whether it shows until the person says otherwise. */
  byDefault?: boolean;
  /** The record's name: always shown. */
  required?: boolean;
}

interface Props {
  view: ViewPreference;
  /** Columns the person may show or hide, in table and list layouts. */
  columns?: ColumnChoice[];
  className?: string;
}

export function ViewSwitcher({ view, columns, className }: Props) {
  const reduce = useMotionPreference();
  const pill = useId();
  const choosable = (columns ?? []).filter((c) => !c.required);
  const columnsApply = choosable.length > 0 && (view.layout === 'table' || view.layout === 'list');

  return (
    <div className={cn('flex shrink-0 items-center gap-1.5', className)}>
      <div role="group" aria-label="Layout" className="relative flex items-center rounded-xl border border-slate-200/80 bg-slate-100/70 p-1 shadow-inner">
        {view.layouts.map((layout) => {
          const { label, icon: Icon } = LAYOUT_INFO[layout];
          const on = view.layout === layout;
          return (
            <button
              key={layout}
              type="button"
              aria-pressed={on}
              aria-label={`Show as ${label.toLowerCase()}`}
              title={label}
              onClick={() => view.setLayout(layout)}
              className={cn(
                'relative flex h-8 items-center gap-1.5 rounded-lg px-2.5 text-sm font-medium transition-colors duration-200 outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50',
                on ? 'text-slate-900' : 'text-slate-500 hover:text-slate-800',
              )}
              data-testid={`view-${layout}`}
            >
              {on && (
                <motion.span
                  layoutId={reduce ? undefined : `view-pill-${pill}`}
                  className="absolute inset-0 rounded-lg bg-white shadow-sm ring-1 ring-slate-200/80"
                  transition={{ type: 'spring', stiffness: 500, damping: 34 }}
                />
              )}
              <motion.span
                className="relative"
                animate={on && !reduce ? { scale: [1, 1.2, 1], rotate: [0, -8, 0] } : { scale: 1, rotate: 0 }}
                transition={{ duration: 0.35 }}
              >
                <Icon className="h-4 w-4" aria-hidden />
              </motion.span>
              <span className="relative hidden xl:inline">{label}</span>
            </button>
          );
        })}
      </div>

      <Popover>
        <PopoverTrigger asChild>
          <button
            type="button"
            aria-label="View options"
            title="View options"
            className={cn(
              'group relative flex h-10 w-10 items-center justify-center rounded-xl border border-slate-200/80 bg-white text-slate-500 shadow-sm transition-[color,box-shadow,border-color,scale] duration-200 outline-none hover:border-slate-300 hover:text-slate-800 hover:shadow active:scale-95 focus-visible:ring-[3px] focus-visible:ring-ring/50 data-[state=open]:border-orange-300 data-[state=open]:text-orange-600',
            )}
            data-testid="view-options"
          >
            <SlidersHorizontal className="h-4 w-4 transition-transform duration-300 group-hover:rotate-90 group-data-[state=open]:rotate-90" aria-hidden />
            {view.customised && <span className="absolute -top-0.5 -right-0.5 h-2.5 w-2.5 rounded-full bg-orange-500 ring-2 ring-white" aria-hidden />}
          </button>
        </PopoverTrigger>
        <PopoverContent aria-label="Saved views" align="end" className="w-80 overflow-hidden rounded-2xl p-0">
          <div className="border-b border-slate-100 bg-gradient-to-r from-orange-50 via-pink-50 to-violet-50 px-4 py-3">
            <p className="text-sm font-semibold text-slate-900">View options</p>
            <p className="mt-0.5 flex items-center gap-1 text-xs text-slate-500">
              <Check className="h-3 w-3 text-emerald-500" aria-hidden />
              Saved for you, on every device
            </p>
          </div>

          <div className="space-y-4 p-4">
            <section>
              <h3 className="mb-2 text-[11px] font-semibold tracking-wide text-slate-500 uppercase">Layout</h3>
              <div role="radiogroup" aria-label="Layout" className={cn('grid gap-2', view.layouts.length > 2 ? 'grid-cols-2' : 'grid-cols-2')}>
                {view.layouts.map((layout) => {
                  const { label, icon: Icon, hint } = LAYOUT_INFO[layout];
                  const on = view.layout === layout;
                  return (
                    <button
                      key={layout}
                      type="button"
                      role="radio"
                      aria-checked={on}
                      onClick={() => view.setLayout(layout)}
                      className={cn(
                        'flex items-start gap-2 rounded-xl border p-2.5 text-left transition-[border-color,background-color,box-shadow,scale] duration-200 outline-none active:scale-[0.97] focus-visible:ring-[3px] focus-visible:ring-ring/50',
                        on ? 'border-orange-300 bg-orange-50/70 shadow-sm' : 'border-slate-200 hover:border-slate-300 hover:bg-slate-50',
                      )}
                    >
                      <span className={cn('mt-0.5 flex h-7 w-7 shrink-0 items-center justify-center rounded-lg transition-colors', on ? 'bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-sm' : 'bg-slate-100 text-slate-500')}>
                        <Icon className="h-3.5 w-3.5" aria-hidden />
                      </span>
                      <span className="min-w-0">
                        <span className="block text-sm font-medium text-slate-800">{label}</span>
                        <span className="block text-[11px] leading-tight text-slate-500">{hint}</span>
                      </span>
                    </button>
                  );
                })}
              </div>
            </section>

            <section>
              <h3 className="mb-2 text-[11px] font-semibold tracking-wide text-slate-500 uppercase">Spacing</h3>
              <div role="radiogroup" aria-label="Spacing" className="relative grid grid-cols-2 rounded-xl bg-slate-100 p-1">
                {DENSITY.map(({ value, label, icon: Icon }) => {
                  const on = view.density === value;
                  return (
                    <button
                      key={value}
                      type="button"
                      role="radio"
                      aria-checked={on}
                      onClick={() => view.setDensity(value)}
                      className={cn('relative flex h-8 items-center justify-center gap-1.5 rounded-lg text-sm font-medium transition-colors outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50', on ? 'text-slate-900' : 'text-slate-500 hover:text-slate-800')}
                    >
                      {on && (
                        <motion.span
                          layoutId={reduce ? undefined : `density-pill-${pill}`}
                          className="absolute inset-0 rounded-lg bg-white shadow-sm"
                          transition={{ type: 'spring', stiffness: 500, damping: 34 }}
                        />
                      )}
                      <Icon className="relative h-4 w-4" aria-hidden />
                      <span className="relative">{label}</span>
                    </button>
                  );
                })}
              </div>
            </section>

            {columnsApply && (
              <section>
                <h3 className="mb-2 text-[11px] font-semibold tracking-wide text-slate-500 uppercase">Columns</h3>
                <ul className="max-h-48 space-y-0.5 overflow-y-auto pr-1">
                  {choosable.map((column) => {
                    const shown = view.shows(column.id, column.byDefault ?? true);
                    const id = `${pill}-col-${column.id}`;
                    return (
                      <li key={column.id} className="flex items-center justify-between gap-3 rounded-lg px-2 py-1.5 transition-colors hover:bg-slate-50">
                        <label htmlFor={id} className="flex-1 cursor-pointer text-sm text-slate-700">{column.label}</label>
                        <Switch id={id} checked={shown} onCheckedChange={(on) => view.setColumn(column.id, on)} />
                      </li>
                    );
                  })}
                </ul>
              </section>
            )}
          </div>

          <div className="flex items-center justify-between border-t border-slate-100 bg-slate-50/70 px-4 py-2.5">
            <span className="text-xs text-slate-500">{view.customised ? 'Your own view' : 'As the page comes'}</span>
            <button
              type="button"
              onClick={view.reset}
              disabled={!view.customised}
              className="flex items-center gap-1 rounded-md px-1.5 py-1 text-xs font-medium text-slate-500 transition-colors hover:text-slate-800 disabled:pointer-events-none disabled:opacity-40"
            >
              <RotateCcw className="h-3 w-3" aria-hidden />
              Reset
            </button>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}

export default ViewSwitcher;

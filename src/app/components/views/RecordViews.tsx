// The list and table layouts every list page shares, and the motion between layouts.
//
// A page describes its records once — a name, a line under it, and its columns —
// and gets a list (one row each, the important columns beside the name) and a
// table (every column the person chose, side by side). Its own cards stay its own.
// Rows open the record, by mouse or keyboard, and rise in one after another.

import type { ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import { ChevronRight } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import type { ViewLayout, ViewPreference } from '@/app/lib/viewPreferences';
import type { ColumnChoice } from './ViewSwitcher';

const EASE = [0.16, 1, 0.3, 1] as const;
/** Rows past this many appear together: a long list should not take seconds to arrive. */
const STAGGERED = 14;

export interface RecordColumn<T> extends ColumnChoice {
  cell: (item: T) => ReactNode;
  /** Right-aligned, for amounts and counts. */
  numeric?: boolean;
  className?: string;
}

/** The choices the view options offer for these columns. */
export function columnChoices<T>(columns: RecordColumn<T>[]): ColumnChoice[] {
  return columns.map(({ id, label, byDefault, required }) => ({ id, label, byDefault, required }));
}

interface Shared<T> {
  items: T[];
  view: ViewPreference;
  getKey: (item: T) => string;
  /** Where a record opens. */
  href?: (item: T) => string;
  /** Or what opening it does, when it is not a page of its own. */
  onOpen?: (item: T) => void;
  /** Buttons at the end of each row. Clicking them does not open the record. */
  actions?: (item: T) => ReactNode;
  /** Rows to mark, as when several are chosen for a bulk change. */
  selected?: (item: T) => boolean;
  /** A label naming the records, for screen readers: "Leads". */
  label: string;
}

function useOpen<T>({ href, onOpen }: Pick<Shared<T>, 'href' | 'onOpen'>) {
  const navigate = useNavigate();
  if (!href && !onOpen) return undefined;
  return (item: T) => (onOpen ? onOpen(item) : href && navigate(href(item)));
}

const stopping = (event: React.SyntheticEvent) => event.stopPropagation();

/** Every column the person chose, side by side. */
export function RecordTable<T>({ items, view, getKey, href, onOpen, actions, selected, label, columns }: Shared<T> & { columns: RecordColumn<T>[] }) {
  const reduce = useMotionPreference();
  const open = useOpen({ href, onOpen });
  const shown = columns.filter((c) => c.required || view.shows(c.id, c.byDefault ?? true));
  const compact = view.density === 'compact';
  const pad = compact ? 'px-3 py-1.5' : 'px-4 py-3';

  return (
    <div className="overflow-hidden rounded-2xl border border-slate-200/80 bg-white shadow-sm">
      <div className="overflow-x-auto">
        <table className={cn('w-full', compact ? 'text-[13px]' : 'text-sm')} aria-label={label}>
          <thead className="sticky top-0 z-10 bg-slate-50/95 backdrop-blur">
            <tr className="border-b border-slate-200/80 text-left text-xs font-semibold tracking-wide text-slate-500 uppercase">
              {shown.map((c) => (
                <th key={c.id} scope="col" className={cn(pad, 'whitespace-nowrap', c.numeric && 'text-right', c.className)}>{c.label}</th>
              ))}
              {(actions || open) && <th scope="col" className={cn(pad, 'w-px')}><span className="sr-only">Actions</span></th>}
            </tr>
          </thead>
          <tbody>
            {items.map((item, i) => {
              const isSelected = selected?.(item) ?? false;
              return (
                <motion.tr
                  key={getKey(item)}
                  className={cn(
                    'group border-b border-slate-100 transition-colors duration-150 last:border-b-0',
                    open && 'cursor-pointer hover:bg-orange-50/50',
                    isSelected && 'bg-orange-50/70',
                  )}
                  onClick={open ? () => open(item) : undefined}
                  initial={reduce ? false : { opacity: 0, y: 6 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ duration: 0.3, ease: EASE, delay: reduce ? 0 : Math.min(i, STAGGERED) * 0.025 }}
                >
                  {shown.map((c, ci) => (
                    <td key={c.id} className={cn(pad, 'align-middle', c.numeric && 'text-right tabular-nums', ci === 0 ? 'font-medium text-slate-900' : 'text-slate-600', c.className)}>
                      {c.cell(item)}
                    </td>
                  ))}
                  {(actions || open) && (
                    <td className={cn(pad, 'text-right whitespace-nowrap')} onClick={stopping}>
                      <div className="flex items-center justify-end gap-1">
                        {actions?.(item)}
                        {/* The keyboard's way in: cells may hold their own controls, so the row's
                            name is not a button itself. */}
                        {open && (
                          <button
                            type="button"
                            onClick={() => open(item)}
                            aria-label="Open"
                            className="flex h-7 w-7 items-center justify-center rounded-lg text-slate-300 outline-none transition-colors hover:bg-orange-100 hover:text-orange-600 focus-visible:ring-[3px] focus-visible:ring-ring/50 group-hover:text-orange-500"
                          >
                            <ChevronRight className="h-4 w-4 transition-transform duration-200 group-hover:translate-x-0.5" aria-hidden />
                          </button>
                        )}
                      </div>
                    </td>
                  )}
                </motion.tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}

/** One row per record: its picture, its name, a line under it, and a few columns beside. */
export function RecordList<T>({
  items, view, getKey, href, onOpen, actions, selected, label, columns = [], leading, title, subtitle,
}: Shared<T> & {
  leading?: (item: T) => ReactNode;
  title: (item: T) => ReactNode;
  subtitle?: (item: T) => ReactNode;
  /** Shown beside the name on wider screens, as far as the person turned them on. */
  columns?: RecordColumn<T>[];
}) {
  const reduce = useMotionPreference();
  const open = useOpen({ href, onOpen });
  const compact = view.density === 'compact';
  const meta = columns.filter((c) => !c.required && view.shows(c.id, c.byDefault ?? true)).slice(0, 4);
  const trailing = !!(actions || open);
  // Every row shares the list's columns (CSS subgrid), so the columns line up down
  // the list however wide one row's buttons are. Below md there are no side columns.
  const tracks = (withMeta: boolean) => [
    leading ? 'auto' : null,
    'minmax(0,1fr)',
    ...(withMeta ? meta.map((c) => (c.numeric ? '5.5rem' : 'minmax(0,11rem)')) : []),
    trailing ? 'auto' : null,
  ].filter(Boolean).join(' ');

  return (
    <ul
      className={cn('grid grid-cols-[var(--list-cols)] gap-x-3 md:grid-cols-[var(--list-cols-md)]', compact ? 'gap-y-1.5' : 'gap-y-2.5')}
      style={{ '--list-cols': tracks(false), '--list-cols-md': tracks(true) } as React.CSSProperties}
      aria-label={label}
    >
      {items.map((item, i) => {
        const isSelected = selected?.(item) ?? false;
        return (
          <motion.li
            key={getKey(item)}
            className="col-span-full grid grid-cols-subgrid"
            initial={reduce ? false : { opacity: 0, x: -10 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ duration: 0.3, ease: EASE, delay: reduce ? 0 : Math.min(i, STAGGERED) * 0.03 }}
          >
            <div
              className={cn(
                'group col-span-full grid grid-cols-subgrid items-center rounded-2xl border bg-white shadow-sm transition-[border-color,box-shadow,translate] duration-200',
                compact ? 'px-3 py-2' : 'px-4 py-3',
                open && 'cursor-pointer hover:-translate-y-px hover:border-orange-200 hover:shadow-md',
                isSelected ? 'border-orange-300 bg-orange-50/60' : 'border-slate-200/80',
              )}
              onClick={open ? () => open(item) : undefined}
            >
              {leading && <div className={cn('shrink-0', compact && '[&>*]:scale-90')}>{leading(item)}</div>}
              <div className="min-w-0">
                {open ? (
                  <button
                    type="button"
                    onClick={(e) => { e.stopPropagation(); open(item); }}
                    className="block max-w-full truncate rounded text-left font-semibold text-slate-900 outline-none group-hover:text-orange-700 focus-visible:ring-[3px] focus-visible:ring-ring/50"
                  >
                    {title(item)}
                  </button>
                ) : (
                  <div className="truncate font-semibold text-slate-900">{title(item)}</div>
                )}
                {subtitle && <div className={cn('truncate text-slate-500', compact ? 'text-xs' : 'text-sm')}>{subtitle(item)}</div>}
              </div>
              {meta.map((c) => (
                <div key={c.id} className={cn('hidden min-w-0 md:block', c.numeric && 'text-right', c.className)}>
                  <div className="text-[10px] font-semibold tracking-wide text-slate-500 uppercase">{c.label}</div>
                  <div className={cn('truncate text-slate-700', compact ? 'text-xs' : 'text-sm', c.numeric && 'tabular-nums')}>{c.cell(item)}</div>
                </div>
              ))}
              {(actions || open) && (
                <div className="flex items-center justify-end gap-1" onClick={stopping}>
                  {actions?.(item)}
                  {open && <ChevronRight className="h-4 w-4 text-slate-300 transition-transform duration-200 group-hover:translate-x-0.5 group-hover:text-orange-500" aria-hidden />}
                </div>
              )}
            </div>
          </motion.li>
        );
      })}
    </ul>
  );
}

/** One layout giving way to the next. */
export function ViewTransition({ layout, children, className }: { layout: ViewLayout; children: ReactNode; className?: string }) {
  const reduce = useMotionPreference();
  return (
    <AnimatePresence mode="wait" initial={false}>
      <motion.div
        key={layout}
        className={className}
        initial={reduce ? { opacity: 0 } : { opacity: 0, y: 10, scale: 0.99 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={reduce ? { opacity: 0 } : { opacity: 0, y: -6, scale: 0.995 }}
        transition={{ duration: reduce ? 0.1 : 0.22, ease: EASE }}
        data-layout={layout}
      >
        {children}
      </motion.div>
    </AnimatePresence>
  );
}

const AVATARS = [
  'from-orange-500 to-pink-500',
  'from-violet-500 to-fuchsia-500',
  'from-sky-500 to-indigo-500',
  'from-emerald-500 to-teal-500',
  'from-amber-400 to-orange-500',
  'from-rose-500 to-pink-500',
  'from-cyan-500 to-blue-500',
];

/** A record's initials in a colour that is always the same for the same name. */
export function InitialsAvatar({ name, size = 'md', className }: { name: string | null | undefined; size?: 'sm' | 'md'; className?: string }) {
  const text = (name ?? '').trim();
  const words = text.split(/[\s\-_.]+/).filter(Boolean);
  const initials = ((words[0]?.[0] ?? '?') + (words.length > 1 ? words[words.length - 1]?.[0] ?? '' : '')).toUpperCase();
  let hash = 0;
  for (const ch of text) hash = (hash * 31 + ch.charCodeAt(0)) | 0;
  return (
    <span
      aria-hidden
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-xl bg-gradient-to-br font-bold text-white shadow-sm transition-transform duration-300 group-hover:scale-105 group-hover:-rotate-3',
        size === 'sm' ? 'h-7 w-7 text-[11px]' : 'h-10 w-10 text-sm',
        AVATARS[Math.abs(hash) % AVATARS.length],
        className,
      )}
    >
      {initials}
    </span>
  );
}

/** A card grid's spacing for the chosen density. */
export function gridDensity(view: ViewPreference, roomy: string, compact: string): string {
  return view.density === 'compact' ? compact : roomy;
}

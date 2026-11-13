import { X } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import type { Tag, TagColor } from '@/app/api';

/** Each tag colour's chip, light enough to read dark text on, in both themes' palettes. */
export const TAG_CHIP_CLASSES: Record<TagColor, string> = {
  slate: 'bg-slate-100 text-slate-700 ring-slate-200',
  red: 'bg-red-100 text-red-800 ring-red-200',
  orange: 'bg-orange-100 text-orange-800 ring-orange-200',
  amber: 'bg-amber-100 text-amber-900 ring-amber-200',
  lime: 'bg-lime-100 text-lime-900 ring-lime-200',
  green: 'bg-green-100 text-green-800 ring-green-200',
  teal: 'bg-teal-100 text-teal-800 ring-teal-200',
  sky: 'bg-sky-100 text-sky-800 ring-sky-200',
  blue: 'bg-blue-100 text-blue-800 ring-blue-200',
  indigo: 'bg-indigo-100 text-indigo-800 ring-indigo-200',
  violet: 'bg-violet-100 text-violet-800 ring-violet-200',
  pink: 'bg-pink-100 text-pink-800 ring-pink-200',
};

/** The swatch a colour picker shows for each colour. */
export const TAG_SWATCH_CLASSES: Record<TagColor, string> = {
  slate: 'bg-slate-400', red: 'bg-red-500', orange: 'bg-orange-500', amber: 'bg-amber-400', lime: 'bg-lime-500', green: 'bg-green-500',
  teal: 'bg-teal-500', sky: 'bg-sky-500', blue: 'bg-blue-500', indigo: 'bg-indigo-500', violet: 'bg-violet-500', pink: 'bg-pink-500',
};

interface TagChipProps {
  tag: Pick<Tag, 'name' | 'color'>;
  /** Shows a button that takes the tag off. */
  onRemove?: () => void;
  className?: string;
}

/** A tag, in its colour. */
export function TagChip({ tag, onRemove, className }: TagChipProps) {
  return (
    <span
      className={cn(
        'inline-flex max-w-[12rem] items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset',
        TAG_CHIP_CLASSES[tag.color] ?? TAG_CHIP_CLASSES.slate,
        className,
      )}
    >
      <span className="truncate">{tag.name}</span>
      {onRemove && (
        <button
          type="button"
          onClick={(e) => { e.stopPropagation(); onRemove(); }}
          className="-mr-0.5 rounded-full p-0.5 opacity-70 outline-none hover:opacity-100 focus-visible:ring-2 focus-visible:ring-current"
          aria-label={`Remove ${tag.name}`}
        >
          <X className="h-3 w-3" aria-hidden />
        </button>
      )}
    </span>
  );
}

/** The tags of a record, by id, as chips: those the workspace no longer has are left out. */
export function TagChips({ ids, tags, max = 3 }: { ids: readonly string[]; tags: readonly Tag[]; max?: number }) {
  const byId = new Map(tags.map((t) => [t.id, t]));
  const shown = ids.map((id) => byId.get(id)).filter((t): t is Tag => !!t);
  if (shown.length === 0) return <span className="text-slate-400">—</span>;
  return (
    <span className="flex flex-wrap items-center gap-1">
      {shown.slice(0, max).map((t) => <TagChip key={t.id} tag={t} />)}
      {shown.length > max && <span className="text-xs text-slate-500">+{shown.length - max}</span>}
    </span>
  );
}

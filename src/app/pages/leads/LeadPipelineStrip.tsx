import type { ReactNode } from 'react';
import { Reveal } from '@/app/components/motion/PageEnter';
import { cn } from '@/app/components/ui/utils';
import type { LeadStats } from '@/app/api';

interface LeadPipelineStripProps {
  stats: LeadStats;
  activeOn: boolean;
  convertedOn: boolean;
  qualifiedOn: boolean;
  qualifiedFilterable: boolean;
  onActive: () => void;
  onConverted: () => void;
  onQualified: () => void;
}

/** Share of the pipeline, clamped so a bad count cannot paint past the track. */
function portion(part: number, total: number): number {
  if (total <= 0 || part <= 0) return 0;
  return Math.min(100, (part / total) * 100);
}

function formatCount(value: number): string {
  return value.toLocaleString();
}

const TONE = {
  sky: { dot: 'bg-sky-500', bar: 'bg-sky-500', wash: 'bg-sky-50' },
  orange: { dot: 'bg-[#ff7a59]', bar: 'bg-[#ff7a59]', wash: 'bg-[#fff6f2]' },
  teal: { dot: 'bg-teal-600', bar: 'bg-teal-600', wash: 'bg-teal-50' },
  slate: { dot: 'bg-slate-400', bar: 'bg-slate-500', wash: 'bg-slate-50' },
  emerald: { dot: 'bg-emerald-600', bar: 'bg-emerald-600', wash: 'bg-emerald-50' },
} as const;

type Tone = keyof typeof TONE;

function Meter({ share, tone }: { share: number; tone: Tone }) {
  return (
    <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-slate-100" aria-hidden>
      <div
        className={cn(
          'h-full rounded-full motion-reduce:transition-none transition-[width] duration-500 ease-out',
          TONE[tone].bar,
        )}
        style={{ width: `${share}%` }}
      />
    </div>
  );
}

function Figure({
  label,
  value,
  hint,
  tone,
  share,
  emphasize,
  pressed,
  onClick,
  actionLabel,
}: {
  label: string;
  value: string;
  hint: string;
  tone: Tone;
  share: number;
  /** Warm the cell when the figure is the one that needs attention. */
  emphasize?: boolean;
  pressed?: boolean;
  onClick?: () => void;
  actionLabel?: string;
}) {
  const body = (
    <>
      <span className={cn('absolute inset-x-0 top-0 h-1', TONE[tone].bar, pressed && 'h-1.5')} aria-hidden />
      <div className="flex items-center justify-between gap-2">
        <span className="text-[11px] font-semibold uppercase tracking-[0.14em] text-slate-500">
          {label}
        </span>
        {pressed ? (
          <span className="rounded-full bg-slate-900 px-1.5 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-white">
            On
          </span>
        ) : (
          <span className={cn('h-2 w-2 rounded-full', TONE[tone].dot)} aria-hidden />
        )}
      </div>
      <p className="mt-2 text-[1.75rem] font-semibold leading-none tabular-nums tracking-[-0.03em] text-slate-950">
        {value}
      </p>
      <p className="mt-1.5 text-[12px] leading-snug text-slate-600">
        {pressed ? 'Filtering the list' : hint}
      </p>
      <Meter share={share} tone={tone} />
    </>
  );

  const className = cn(
    'relative flex h-full min-h-[128px] flex-col justify-center bg-white px-4 py-4 text-left transition-colors duration-200 motion-reduce:transition-none',
    emphasize && !pressed && TONE[tone].wash,
    onClick && 'cursor-pointer focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-[#ff7a59]',
    onClick && !pressed && 'hover:bg-slate-50',
    pressed && TONE[tone].wash,
  );

  return (
    <li className="min-w-0">
      {onClick ? (
        <button
          type="button"
          onClick={onClick}
          aria-pressed={pressed}
          aria-label={actionLabel}
          className={cn(className, 'w-full')}
        >
          {body}
        </button>
      ) : (
        <div className={className}>{body}</div>
      )}
    </li>
  );
}

function Legend({ color, children }: { color: string; children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <span className={cn('h-1.5 w-1.5 rounded-full', color)} aria-hidden />
      {children}
    </span>
  );
}

/**
 * One pipeline reading, not six competing cards.
 * The total leads. Active and converted are the split of that total.
 * Hot, this week, and qualified sit beside it as context, and the three
 * figures that map to list filters are real buttons.
 */
export function LeadPipelineStrip({
  stats,
  activeOn,
  convertedOn,
  qualifiedOn,
  qualifiedFilterable,
  onActive,
  onConverted,
  onQualified,
}: LeadPipelineStripProps) {
  const total = stats.total;
  const activeShare = portion(stats.active, total);
  const convertedShare = portion(stats.converted, total);
  const hotShare = portion(stats.hotLeads, total);
  const weekShare = portion(stats.thisWeek, total);
  const qualifiedShare = portion(stats.qualified, total);
  const rate = Math.max(0, Math.min(100, stats.conversionRate));

  const convertedHint = stats.converted === 0
    ? 'None became deals'
    : `${formatCount(stats.converted)} became ${stats.converted === 1 ? 'a deal' : 'deals'}`;

  return (
    <Reveal
      as="section"
      index={1}
      aria-label="Lead summary"
      className="mb-8 flex overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-[0_1px_2px_rgba(15,23,42,0.05),0_16px_40px_-24px_rgba(15,23,42,0.35)]"
    >
      <div className="w-1 shrink-0 bg-[#ff7a59]" aria-hidden />

      <div className="flex min-w-0 flex-1 flex-col xl:flex-row">
        <div className="flex flex-col justify-center border-b border-slate-200 bg-[linear-gradient(180deg,#fff8f5_0%,#ffffff_72%)] px-5 py-5 sm:px-6 xl:w-60 xl:shrink-0 xl:border-b-0 xl:border-r">
          <p className="text-[11px] font-semibold uppercase tracking-[0.16em] text-slate-500">
            Pipeline
          </p>
          <p className="mt-2 text-5xl font-semibold leading-none tabular-nums tracking-[-0.045em] text-slate-950">
            {formatCount(total)}
          </p>
          <p className="mt-2 text-sm text-slate-600">Total leads</p>

          <div
            className="mt-5 flex h-2 overflow-hidden rounded-full bg-slate-100"
            role="img"
            aria-label={`${formatCount(stats.active)} active and ${formatCount(stats.converted)} converted out of ${formatCount(total)} leads`}
          >
            <div
              className="h-full bg-sky-500 motion-reduce:transition-none transition-[width] duration-500 ease-out"
              style={{ width: `${activeShare}%` }}
            />
            <div
              className="h-full bg-teal-600 motion-reduce:transition-none transition-[width] duration-500 ease-out"
              style={{ width: `${convertedShare}%` }}
            />
          </div>
          <p className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-[11px] text-slate-600">
            <Legend color="bg-sky-500">{formatCount(stats.active)} active</Legend>
            <Legend color="bg-teal-600">{formatCount(stats.converted)} converted</Legend>
          </p>
        </div>

        <ul className="grid flex-1 grid-cols-2 gap-px bg-slate-200 sm:grid-cols-3 xl:grid-cols-5 [&>li:last-child]:col-span-2 xl:[&>li:last-child]:col-span-1">
          <Figure
            label="Active"
            value={formatCount(stats.active)}
            hint="Still open"
            tone="sky"
            share={activeShare}
            pressed={activeOn}
            onClick={onActive}
            actionLabel={
              activeOn
                ? `Active, ${formatCount(stats.active)}. Filter is on. Activate to show every lead.`
                : `Active, ${formatCount(stats.active)}. Still open. Activate to filter the list.`
            }
          />
          <Figure
            label="Hot"
            value={formatCount(stats.hotLeads)}
            hint="Score of 70 or more"
            tone="orange"
            share={hotShare}
            emphasize={stats.hotLeads > 0}
          />
          <Figure
            label="Converted"
            value={`${formatCount(rate)}%`}
            hint={convertedHint}
            tone="teal"
            share={rate}
            pressed={convertedOn}
            onClick={onConverted}
            actionLabel={
              convertedOn
                ? `Converted, ${formatCount(rate)} percent. Filter is on. Activate to show every lead.`
                : `Converted, ${formatCount(rate)} percent. ${convertedHint}. Activate to filter the list.`
            }
          />
          <Figure
            label="This week"
            value={formatCount(stats.thisWeek)}
            hint="Added in the last 7 days"
            tone="slate"
            share={weekShare}
          />
          <Figure
            label="Qualified"
            value={formatCount(stats.qualified)}
            hint="Qualified or further"
            tone="emerald"
            share={qualifiedShare}
            pressed={qualifiedOn}
            onClick={qualifiedFilterable ? onQualified : undefined}
            actionLabel={
              qualifiedOn
                ? `Qualified, ${formatCount(stats.qualified)}. Filter is on. Activate to clear it.`
                : `Qualified, ${formatCount(stats.qualified)}. Qualified or further. Activate to filter the list.`
            }
          />
        </ul>
      </div>
    </Reveal>
  );
}

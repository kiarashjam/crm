// Pieces the Welcome & portal settings are built from: a card per subject with its icon and
// a one-line description, a status pill, and an "i" button that opens the longer explanation
// only for whoever wants it, so the page itself stays short.

import { useId, type ComponentType, type ReactNode } from 'react';
import { Info } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { cn } from '@/app/components/ui/utils';

export type CardTone = 'orange' | 'pink' | 'violet' | 'sky' | 'emerald';

const TILE: Record<CardTone, string> = {
  orange: 'bg-orange-50 text-orange-600 ring-orange-100 dark:bg-orange-500/10 dark:text-orange-300 dark:ring-orange-500/20',
  pink: 'bg-pink-50 text-pink-600 ring-pink-100 dark:bg-pink-500/10 dark:text-pink-300 dark:ring-pink-500/20',
  violet: 'bg-violet-50 text-violet-600 ring-violet-100 dark:bg-violet-500/10 dark:text-violet-300 dark:ring-violet-500/20',
  sky: 'bg-sky-50 text-sky-600 ring-sky-100 dark:bg-sky-500/10 dark:text-sky-300 dark:ring-sky-500/20',
  emerald: 'bg-emerald-50 text-emerald-600 ring-emerald-100 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/20',
};

/** An icon in a soft tile of its card's colour. */
export function IconTile({ icon: Icon, tone, size = 'md' }: { icon: ComponentType<{ className?: string }>; tone: CardTone; size?: 'sm' | 'md' }) {
  return (
    <span
      aria-hidden
      className={cn(
        'flex shrink-0 items-center justify-center ring-1',
        size === 'md' ? 'h-10 w-10 rounded-xl' : 'h-8 w-8 rounded-lg',
        TILE[tone],
      )}
    >
      <Icon className={size === 'md' ? 'h-5 w-5' : 'h-4 w-4'} />
    </span>
  );
}

/** One subject of a settings page: icon, title, one line about it, what it is set to, and its fields. */
export function SettingsCard({ id, icon, tone, title, description, aside, children, className }: {
  id?: string;
  icon: ComponentType<{ className?: string }>;
  tone: CardTone;
  title: ReactNode;
  description?: ReactNode;
  aside?: ReactNode;
  children?: ReactNode;
  className?: string;
}) {
  const heading = useId();
  return (
    <section id={id} aria-labelledby={heading} className={cn('scroll-mt-6 rounded-2xl border border-slate-200 bg-white p-4 shadow-sm sm:p-5', className)}>
      <header className="flex flex-wrap items-center gap-3">
        <IconTile icon={icon} tone={tone} />
        {/* Room for the words first: on a narrow screen what it is set to goes on the next line, not beside them. */}
        <div className="min-w-0 flex-1 basis-52">
          <h3 id={heading} className="flex items-center gap-1 font-semibold text-slate-900">{title}</h3>
          {description && <p className="text-sm text-slate-500">{description}</p>}
        </div>
        {aside && <div className="flex items-center gap-2 max-sm:pl-[52px]">{aside}</div>}
      </header>
      {children && <div className="mt-4">{children}</div>}
    </section>
  );
}

export type PillTone = 'good' | 'warn' | 'idle' | 'info';

const PILL: Record<PillTone, string> = {
  good: 'bg-emerald-50 text-emerald-700 ring-emerald-200 dark:bg-emerald-500/10 dark:text-emerald-300 dark:ring-emerald-500/25',
  warn: 'bg-amber-50 text-amber-700 ring-amber-200 dark:bg-amber-500/10 dark:text-amber-300 dark:ring-amber-500/25',
  idle: 'bg-slate-100 text-slate-600 ring-slate-200 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10',
  info: 'bg-sky-50 text-sky-700 ring-sky-200 dark:bg-sky-500/10 dark:text-sky-300 dark:ring-sky-500/25',
};

const DOT: Record<PillTone, string> = {
  good: 'bg-emerald-500',
  warn: 'bg-amber-500',
  idle: 'bg-slate-400',
  info: 'bg-sky-500',
};

/** A short state, "Connected" or "Off", with a dot of its colour. */
export function StatusPill({ tone, children, className }: { tone: PillTone; children: ReactNode; className?: string }) {
  return (
    <span className={cn('inline-flex items-center gap-1.5 whitespace-nowrap rounded-full px-2.5 py-0.5 text-xs font-semibold ring-1', PILL[tone], className)}>
      <span aria-hidden className={cn('h-1.5 w-1.5 rounded-full', DOT[tone])} />
      {children}
    </span>
  );
}

/** The longer explanation of a field, opened by its "i" button rather than printed under it. */
export function HelpTip({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <Popover>
      <PopoverTrigger asChild>
        <button
          type="button"
          aria-label={label}
          className={cn(
            'inline-flex h-7 w-7 shrink-0 items-center justify-center rounded-full text-slate-400 transition-colors hover:bg-slate-100 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
            className,
          )}
        >
          <Info className="h-3.5 w-3.5" aria-hidden />
        </button>
      </PopoverTrigger>
      <PopoverContent side="top" align="start" className="w-80 max-w-[calc(100vw-2rem)] rounded-xl text-sm font-normal leading-relaxed text-slate-600">
        {children}
      </PopoverContent>
    </Popover>
  );
}

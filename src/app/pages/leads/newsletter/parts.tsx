// Small pieces the newsletter's dialog is built from: its steps, a chip that toggles, a
// person's row, a problem said in place, a two-way switch and the sandboxed preview.

import { useEffect, useRef, useState, type ComponentType, type ReactNode } from 'react';
import { Check, XCircle } from 'lucide-react';
import { InitialsAvatar } from '@/app/components/views';
import { previewDocument } from '@/app/components/onboarding/EmailPreviewFrame';
import { cn } from '@/app/components/ui/utils';
import type { NewsletterPerson } from '@/app/api/leadNewsletter';
import { reasonInfo, statusColors } from './audience';

export interface StepInfo {
  label: string;
  icon: ComponentType<{ className?: string }>;
}

/** The three steps, each a button back to it once reached; the one shown is the current step. */
export function StepIndicator({ steps, step, reachable, done, onGo, className }: {
  steps: StepInfo[];
  step: number;
  /** The furthest step that may be opened. */
  reachable: number;
  /** All of them done: the newsletter went. */
  done?: boolean;
  onGo: (step: number) => void;
  className?: string;
}) {
  return (
    <nav aria-label="Steps" className={className}>
      <ol className="flex items-center gap-1.5 sm:gap-2">
        {steps.map((s, i) => {
          const current = i === step && !done;
          const complete = done || i < step;
          return (
            <li key={s.label} className="flex items-center gap-1.5 sm:gap-2">
              <button
                type="button"
                onClick={() => onGo(i)}
                disabled={done || i > reachable || i === step}
                aria-current={current ? 'step' : undefined}
                aria-label={`Step ${i + 1}: ${s.label}${complete ? ', done' : ''}`}
                className={cn(
                  'group inline-flex items-center gap-2 rounded-full py-1 pl-1 pr-3 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
                  current
                    ? 'bg-orange-50 text-orange-700 ring-1 ring-orange-200 dark:bg-orange-500/15 dark:text-orange-200 dark:ring-orange-500/30'
                    : complete
                      ? 'text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-white/5'
                      : 'text-slate-400 dark:text-slate-500',
                  'disabled:cursor-default',
                )}
              >
                <span
                  aria-hidden
                  className={cn(
                    'flex h-7 w-7 items-center justify-center rounded-full text-xs transition',
                    current
                      ? 'bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-sm shadow-orange-500/30'
                      : complete
                        ? 'bg-emerald-500 text-white'
                        : 'bg-slate-100 text-slate-400 dark:bg-white/5 dark:text-slate-500',
                  )}
                >
                  {complete && !current ? <Check className="h-3.5 w-3.5" /> : <s.icon className="h-3.5 w-3.5" />}
                </span>
                <span className="max-sm:sr-only">{s.label}</span>
              </button>
              {i < steps.length - 1 && (
                <span aria-hidden className={cn('h-px w-4 sm:w-8', complete ? 'bg-emerald-300 dark:bg-emerald-500/50' : 'bg-slate-200 dark:bg-white/10')} />
              )}
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

/** A choice that is on or off, shown as a chip. */
export function ToggleChip({ pressed, onClick, children, dot, icon: Icon, className, label }: {
  pressed: boolean;
  onClick: () => void;
  children: ReactNode;
  /** A status's colour, as a dot. */
  dot?: string;
  icon?: ComponentType<{ className?: string }>;
  className?: string;
  /** What a screen reader hears, when the chip's words are not enough. */
  label?: string;
}) {
  return (
    <button
      type="button"
      aria-pressed={pressed}
      aria-label={label}
      onClick={onClick}
      className={cn(
        'inline-flex min-h-9 items-center gap-1.5 rounded-full border px-3 py-1.5 text-sm font-medium transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400 active:scale-[.97] motion-reduce:transition-none',
        pressed
          // Gray, not slate: the app's dark theme repaints bg-white and bg-slate-* pills as cards.
          ? 'border-gray-900 bg-gray-900 text-white shadow-sm dark:border-orange-400 dark:bg-orange-500 dark:text-white'
          : 'border-slate-200 bg-white text-slate-500 hover:border-slate-300 hover:text-slate-800 dark:border-white/10 dark:bg-white/5 dark:text-slate-400 dark:hover:text-slate-100',
        className,
      )}
    >
      {dot && <span aria-hidden className={cn('h-2 w-2 shrink-0 rounded-full', dot, pressed ? 'ring-1 ring-white/70' : 'opacity-50')} />}
      {Icon && <Icon className="h-3.5 w-3.5 shrink-0" aria-hidden />}
      <span className={cn(!pressed && dot && 'line-through decoration-slate-300 dark:decoration-slate-600')}>{children}</span>
      {pressed && <Check className="h-3.5 w-3.5 shrink-0 opacity-80" aria-hidden />}
    </button>
  );
}

/** One person: their initials, name and email, and their status or why they are left out. */
export function PersonRow({ person, leftOut = false }: { person: NewsletterPerson; leftOut?: boolean }) {
  const reason = leftOut ? reasonInfo(person.reason) : null;
  const colors = statusColors(person.status);
  return (
    <li className="flex items-center gap-3 px-3 py-2">
      <InitialsAvatar name={person.name} size="sm" className={cn(leftOut && 'opacity-60 grayscale')} />
      <div className="min-w-0 flex-1">
        <p className={cn('truncate text-sm font-medium text-slate-800 dark:text-slate-100', leftOut && 'text-slate-500 dark:text-slate-400')}>{person.name}</p>
        <p className="truncate text-xs text-slate-500 dark:text-slate-400">{person.email || '—'}</p>
      </div>
      {reason ? (
        <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-600 dark:bg-white/5 dark:text-slate-300">
          <reason.icon className="h-3 w-3" aria-hidden />
          {reason.label}
        </span>
      ) : (
        <span className="hidden shrink-0 items-center gap-1.5 text-[11px] text-slate-500 sm:inline-flex dark:text-slate-400">
          <span aria-hidden className={cn('h-1.5 w-1.5 rounded-full', colors.dot)} />
          {person.status}
        </span>
      )}
    </li>
  );
}

/** What the server said went wrong, where it went wrong. */
export function ProblemNote({ children, className, action }: { children: ReactNode; className?: string; action?: ReactNode }) {
  return (
    <div
      role="alert"
      className={cn(
        'flex items-start gap-2.5 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-800 dark:border-rose-500/30 dark:bg-rose-500/10 dark:text-rose-200',
        className,
      )}
    >
      <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-rose-500" aria-hidden />
      <div className="min-w-0 flex-1 break-words">{children}</div>
      {action}
    </div>
  );
}

/** Two or three choices side by side, one of them on. */
export function Segmented<T extends string>({ value, options, onChange, label, className }: {
  value: T;
  options: { id: T; label: string; icon?: ComponentType<{ className?: string }> }[];
  onChange: (next: T) => void;
  label: string;
  className?: string;
}) {
  return (
    <div role="group" aria-label={label} className={cn('inline-flex rounded-lg bg-slate-100 p-0.5 dark:bg-white/5', className)}>
      {options.map((o) => (
        <button
          key={o.id}
          type="button"
          aria-pressed={value === o.id}
          onClick={() => onChange(o.id)}
          className={cn(
            'inline-flex items-center gap-1.5 rounded-md px-2.5 py-1.5 text-xs font-semibold transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-400',
            value === o.id
              ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white'
              : 'text-slate-500 hover:text-slate-800 dark:text-slate-400 dark:hover:text-slate-100',
          )}
        >
          {o.icon && <o.icon className="h-3.5 w-3.5" aria-hidden />}
          {o.label}
        </button>
      ))}
    </div>
  );
}

const DESKTOP_WIDTH = 640;
const MOBILE_WIDTH = 375;
const FRAME_HEIGHT = 640;

/**
 * The email as the server rendered it, in a sandbox that allows nothing: no script, no form,
 * no link opens, and nothing in it can reach this page. It scrolls inside; at desktop width it
 * is made smaller to fit the column, and at a phone's width its phone layout shows.
 */
export function PreviewFrame({ html, device }: { html: string; device: 'desktop' | 'mobile' }) {
  const box = useRef<HTMLDivElement>(null);
  const [room, setRoom] = useState<number | null>(null);
  useEffect(() => {
    const el = box.current;
    if (!el || typeof ResizeObserver !== 'function') return;
    const observer = new ResizeObserver(([entry]) => setRoom(entry?.contentRect.width ?? null));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);
  const width = device === 'mobile' ? MOBILE_WIDTH : DESKTOP_WIDTH;
  const inset = device === 'mobile' ? 32 : 0;
  const scale = room ? Math.min(1, Math.max(0.3, (room - inset) / width)) : 1;
  const height = device === 'mobile' ? 667 : FRAME_HEIGHT;
  return (
    <div ref={box} className={cn('overflow-hidden', device === 'mobile' && 'bg-slate-100 py-4 dark:bg-slate-900')}>
      <div className="mx-auto" style={{ width: width * scale, height: height * scale }}>
        <iframe
          title="Email preview"
          sandbox=""
          srcDoc={previewDocument(html)}
          style={{ width, height, transform: scale < 1 ? `scale(${scale})` : undefined, transformOrigin: 'top left' }}
          className={cn('block border-0 bg-white', device === 'mobile' && 'rounded-[1.75rem] shadow-lg ring-4 ring-slate-900/80 dark:ring-white/20')}
        />
      </div>
    </div>
  );
}

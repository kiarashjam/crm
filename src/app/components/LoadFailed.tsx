import { AlertTriangle, RotateCw } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';

interface LoadFailedProps {
  /** What could not be read, as a sentence starts: "Tasks", "These figures". */
  what: string;
  /** Why, when the server said (a refusal reads as a sentence). */
  error?: unknown;
  onRetry: () => void;
  /** A retry is on its way. */
  retrying?: boolean;
  /**
   * An earlier copy is still on screen: a slim strip above it, rather than a card in
   * place of the list.
   */
  stale?: boolean;
  className?: string;
}

function reasonOf(error: unknown): string | null {
  return error instanceof Error && error.message && error.message !== 'Failed to fetch' ? error.message : null;
}

/**
 * A read that failed, said as one, with a way to try again. Pages used to leave their
 * lists empty when a read failed, which looks exactly like having nothing: a rep saw
 * no tasks and decided nothing was due.
 */
export function LoadFailed({ what, error, onRetry, retrying = false, stale = false, className }: LoadFailedProps) {
  const reason = reasonOf(error);
  const button = (
    <button
      type="button"
      onClick={onRetry}
      disabled={retrying}
      className="inline-flex h-9 shrink-0 items-center gap-1.5 rounded-lg border border-slate-300 px-3 text-sm font-semibold text-slate-700 hover:border-[var(--brand-primary)] disabled:opacity-60 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 dark:border-slate-600 dark:text-slate-100"
    >
      <RotateCw className={cn('h-4 w-4', retrying && 'motion-safe:animate-spin')} aria-hidden="true" />
      {retrying ? 'Trying again…' : 'Try again'}
    </button>
  );

  if (stale) {
    return (
      <div role="status" className={cn('flex flex-wrap items-center justify-between gap-3 rounded-xl border border-amber-200 bg-amber-50 px-4 py-2 text-sm text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100', className)}>
        <p>
          {what} could not be refreshed, so this is the last copy read.
          {reason ? ` ${reason}` : ''}
        </p>
        {button}
      </div>
    );
  }

  return (
    <div role="alert" className={cn('rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm dark:border-slate-700 dark:bg-slate-800', className)}>
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-xl bg-amber-50 text-amber-700 ring-1 ring-amber-100 dark:bg-amber-900/30 dark:text-amber-300 dark:ring-amber-800/50">
        <AlertTriangle className="h-6 w-6" aria-hidden="true" />
      </div>
      <p className="mt-4 text-base font-semibold text-slate-900 dark:text-slate-100">{what} could not be loaded.</p>
      <p className="mx-auto mt-1 max-w-md text-sm text-slate-600 dark:text-slate-400">
        {reason ?? 'The server could not be reached, or did not answer.'}
      </p>
      <div className="mt-5 flex justify-center">{button}</div>
    </div>
  );
}

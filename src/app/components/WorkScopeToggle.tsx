import { cn } from '@/app/components/ui/utils';
import type { WorkScope } from '@/app/lib/workScope';

/**
 * Mine or the team's: two buttons, the chosen one pressed. "Mine" is what is given to
 * me, or made by me and given to nobody (see WorkScope on the server).
 */
export function WorkScopeToggle({ value, onChange, mineLabel = 'Mine', teamLabel = 'Team', className }: {
  value: WorkScope;
  onChange: (scope: WorkScope) => void;
  mineLabel?: string;
  teamLabel?: string;
  className?: string;
}) {
  const option = (scope: WorkScope, label: string) => (
    <button
      type="button"
      aria-pressed={value === scope}
      onClick={() => value !== scope && onChange(scope)}
      className={cn(
        'h-8 rounded-md px-3 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[var(--brand-primary)]',
        value === scope
          ? 'bg-white text-slate-900 shadow-sm dark:bg-slate-700 dark:text-white'
          : 'text-slate-600 hover:text-slate-900 dark:text-slate-300 dark:hover:text-white',
      )}
    >
      {label}
    </button>
  );
  return (
    <div role="group" aria-label="Whose work" className={cn('inline-flex shrink-0 items-center gap-0.5 rounded-lg bg-slate-100 p-0.5 dark:bg-slate-800', className)}>
      {option('mine', mineLabel)}
      {option('team', teamLabel)}
    </div>
  );
}

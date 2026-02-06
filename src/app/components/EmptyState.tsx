import { Link } from 'react-router-dom';
import type { LucideIcon } from 'lucide-react';

interface EmptyStateProps {
  icon: LucideIcon;
  title: string;
  description?: string;
  actionLabel?: string;
  actionHref?: string;
  onAction?: () => void;
  /** Optional second CTA (e.g. "Import from CRM") */
  secondaryActionLabel?: string;
  secondaryActionHref?: string;
  onSecondaryAction?: () => void;
  className?: string;
  /** Color variant: 'orange' (primary) or 'teal' (secondary). Defaults to 'orange'. */
  variant?: 'orange' | 'teal';
}

const variantStyles = {
  orange: {
    icon: 'bg-orange-50 dark:bg-orange-900/30 text-orange-600 dark:text-orange-400 ring-orange-100 dark:ring-orange-800/50',
    button: 'bg-orange-600 hover:bg-orange-500 focus-visible:ring-orange-500 dark:bg-orange-500 dark:hover:bg-orange-400',
    ring: 'focus-visible:ring-orange-500',
  },
  teal: {
    icon: 'bg-teal-50 dark:bg-teal-900/30 text-teal-600 dark:text-teal-400 ring-teal-100 dark:ring-teal-800/50',
    button: 'bg-teal-600 hover:bg-teal-500 focus-visible:ring-teal-500 dark:bg-teal-500 dark:hover:bg-teal-400',
    ring: 'focus-visible:ring-teal-500',
  },
};

export default function EmptyState({
  icon: Icon,
  title,
  description,
  actionLabel,
  actionHref,
  onAction,
  secondaryActionLabel,
  secondaryActionHref,
  onSecondaryAction,
  className = '',
  variant = 'orange',
}: EmptyStateProps) {
  const hasPrimary = actionLabel && (actionHref || onAction);
  const hasSecondary = secondaryActionLabel && (secondaryActionHref || onSecondaryAction);
  const styles = variantStyles[variant];
  return (
    <div
      className={`rounded-2xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-10 text-center sm:p-14 shadow-sm ${className}`}
      role="status"
      aria-label={title}
    >
      <div className={`mx-auto flex h-16 w-16 items-center justify-center rounded-2xl ring-1 ${styles.icon}`}>
        <Icon className="h-8 w-8" aria-hidden />
      </div>
      <h3 className="mt-5 text-lg font-semibold text-slate-900 dark:text-slate-100">{title}</h3>
      {description && <p className="mt-2 text-sm text-slate-600 dark:text-slate-400 max-w-sm mx-auto leading-relaxed">{description}</p>}
      {(hasPrimary || hasSecondary) && (
        <div className="mt-8 flex flex-wrap items-center justify-center gap-3">
          {hasPrimary && (
            actionHref ? (
              <Link
                to={actionHref}
                className={`inline-flex items-center justify-center rounded-xl px-6 py-3 text-sm font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 transition-colors ${styles.button}`}
              >
                {actionLabel}
              </Link>
            ) : (
              <button
                type="button"
                onClick={onAction}
                className={`inline-flex items-center justify-center rounded-xl px-6 py-3 text-sm font-semibold text-white shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 transition-colors ${styles.button}`}
              >
                {actionLabel}
              </button>
            )
          )}
          {hasSecondary && (
            secondaryActionHref ? (
              <Link
                to={secondaryActionHref}
                className={`inline-flex items-center justify-center rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 px-6 py-3 text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 transition-colors ${styles.ring}`}
              >
                {secondaryActionLabel}
              </Link>
            ) : (
              <button
                type="button"
                onClick={onSecondaryAction}
                className={`inline-flex items-center justify-center rounded-xl border border-slate-300 dark:border-slate-600 bg-white dark:bg-slate-700 px-6 py-3 text-sm font-semibold text-slate-700 dark:text-slate-200 hover:bg-slate-50 dark:hover:bg-slate-600 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2 transition-colors ${styles.ring}`}
              >
                {secondaryActionLabel}
              </button>
            )
          )}
        </div>
      )}
    </div>
  );
}

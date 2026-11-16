// Small pieces the company, contact and private event screens share: a card's frame, a read that
// failed said so with a retry, a refusal's words, and money in the club's currency.

import type { ReactNode } from 'react';
import { Building2, Loader2 } from 'lucide-react';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { cn } from '@/app/components/ui/utils';

/** What a refusal says, or the fallback. */
export function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/** Money in the given currency, written as the workspace writes it. */
export function useClubAmount(): (amount: number, currency?: string) => string {
  const { locale } = useRegional();
  return (amount, currency) => formatClubMoney(amount, currency, locale);
}

export const fieldClass = 'h-9 w-full rounded-md border border-input bg-input-background px-3 text-sm';

export function CardShell({ id, title, icon, children, className, action }: {
  id: string;
  title: string;
  icon?: ReactNode;
  children: ReactNode;
  className?: string;
  action?: ReactNode;
}) {
  return (
    <section
      aria-labelledby={id}
      className={cn('overflow-hidden rounded-2xl border border-amber-200/70 bg-gradient-to-br from-amber-50/60 via-white to-white shadow-sm dark:border-slate-700 dark:from-slate-800 dark:via-slate-800 dark:to-slate-800', className)}
    >
      <div className="flex items-center justify-between gap-2 border-b border-amber-100/80 px-4 py-2.5 dark:border-slate-700">
        <h3 id={id} className="flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-amber-800 dark:text-amber-300">
          {icon ?? <Building2 className="h-3.5 w-3.5" aria-hidden="true" />}
          {title}
        </h3>
        {action}
      </div>
      <div className="space-y-4 p-4 text-sm">{children}</div>
    </section>
  );
}

export function Loading({ what }: { what: string }) {
  return (
    <p className="flex items-center gap-2 text-slate-500" role="status">
      <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> Loading {what}…
    </p>
  );
}

/** A read that failed, said in a line with a way to try again (a card's own words, not a page's). */
export function Failed({ what, error, onRetry, retrying }: { what: string; error?: unknown; onRetry: () => void; retrying: boolean }) {
  const reason = error instanceof Error && error.message && error.message !== 'Failed to fetch' ? ` ${error.message}` : '';
  return (
    <p role="alert" className="text-sm text-slate-600 dark:text-slate-300">
      {what} could not be loaded.{reason}{' '}
      <button type="button" onClick={onRetry} disabled={retrying} className="font-medium text-amber-800 underline disabled:opacity-50 dark:text-amber-300">
        {retrying ? 'Trying again…' : 'Try again'}
      </button>
    </p>
  );
}

export function SectionTitle({ children }: { children: ReactNode }) {
  return <p className="mb-1.5 text-[11px] font-semibold uppercase tracking-wider text-slate-500">{children}</p>;
}

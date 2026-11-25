// What someone who is not a platform admin sees at /admin/analytics: a polite "not for
// you", never an error, and a way back.

import { Link } from 'react-router-dom';
import { Lock } from 'lucide-react';

export function NotAvailable() {
  return (
    <section aria-labelledby="sa-not-available" className="mx-auto mt-6 max-w-lg rounded-2xl border border-slate-200 bg-white p-8 text-center shadow-sm sm:mt-12">
      <span aria-hidden className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-slate-100 text-slate-500 ring-1 ring-slate-200 dark:bg-white/5 dark:text-slate-300 dark:ring-white/10">
        <Lock className="h-5 w-5" />
      </span>
      <h1 id="sa-not-available" className="mt-4 text-lg font-semibold text-slate-900">Site analytics is not available</h1>
      <p className="mx-auto mt-1 max-w-sm text-sm text-slate-500">
        This page is for the CadenceE team&apos;s platform admins. If you think you should see it, ask one of them to give you access.
      </p>
      <Link
        to="/dashboard"
        className="mt-5 inline-flex h-9 items-center rounded-lg border border-slate-300 px-3 text-sm font-semibold text-slate-700 hover:border-[var(--brand-primary)] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-orange-500 dark:border-slate-600 dark:text-slate-100"
      >
        Back to the dashboard
      </Link>
    </section>
  );
}

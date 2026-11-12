// The log of erasures: when, who did it, their reference, and how much went where. It names
// nobody, by design: the person erased is only their number.

import { LoadFailed } from '@/app/components/LoadFailed';
import { useErasures } from '@/app/hooks/queries';
import { currentDateLocale } from '@/app/i18n/current';

function when(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function ErasureLogPanel() {
  const erasures = useErasures();
  if (erasures.isPending) return <p className="text-sm text-slate-600" role="status">Loading the log…</p>;
  if (erasures.isError) {
    return <LoadFailed what="The log of erasures" error={erasures.error} onRetry={() => void erasures.refetch()} retrying={erasures.isFetching} />;
  }
  if (erasures.data.length === 0) {
    return <p className="max-w-4xl rounded-2xl border border-slate-200 bg-white p-5 text-sm text-slate-600 shadow-sm">Nobody has been erased yet.</p>;
  }
  return (
    <ul className="max-w-4xl divide-y divide-slate-100 rounded-2xl border border-slate-200 bg-white shadow-sm">
      {erasures.data.map((e) => {
        const summary = Object.entries(e.summary ?? {}).filter(([, n]) => n > 0);
        return (
          <li key={e.number} className="px-5 py-3 text-sm">
            <p className="text-slate-900">
              <span className="font-semibold">Erasure {e.number}</span>
              <span className="text-slate-500"> · {when(e.erasedAtUtc)} · </span>
              {e.trigger === 'retention' ? 'by the retention settings' : `at their request, by ${e.erasedByName ?? 'someone'}`}
              {e.reference && <span className="text-slate-500"> · reference {e.reference}</span>}
            </p>
            {summary.length > 0 && (
              <p className="text-slate-600">{summary.map(([area, n]) => `${area}: ${n}`).join(' · ')}</p>
            )}
          </li>
        );
      })}
    </ul>
  );
}

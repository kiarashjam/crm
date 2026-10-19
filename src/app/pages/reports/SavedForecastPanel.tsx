// This month's forecast as it stood when a manager saved it.
//
// The live forecast above moves whenever a deal does. A saved one doesn't, which is
// what a month's commitment needs: what was forecast on the 1st, to set against what
// closed. Saving and reading are for the owner and managers, because the saved lines
// count private deals and name their owners.

import { useEffect, useState } from 'react';
import { toast } from 'sonner';
import { getForecastSnapshot, saveForecastSnapshot, type ForecastSnapshotRow } from '@/app/api/reporting';

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function amount(value: number, currency: string): string {
  try {
    return value.toLocaleString(undefined, { style: 'currency', currency, maximumFractionDigits: 0 });
  } catch {
    return `${Math.round(value).toLocaleString()} ${currency}`;
  }
}

export function SavedForecastPanel() {
  const [rows, setRows] = useState<ForecastSnapshotRow[] | null>(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    getForecastSnapshot().then(setRows).catch(() => setRows([]));
  }, []);

  const save = async () => {
    setSaving(true);
    try {
      setRows(await saveForecastSnapshot());
      toast.success('Saved this month’s forecast. Later edits will not change it.');
    } catch (err) {
      toast.error(reason(err, 'The forecast could not be saved.'));
    } finally {
      setSaving(false);
    }
  };

  const savedAt = rows?.[0]?.savedAtUtc;

  return (
    <div className="mt-3 rounded-2xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-slate-800">Saved forecast for this month</h3>
          <p className="mt-0.5 text-[11px] text-slate-400">
            {savedAt
              ? `Saved ${new Date(savedAt).toLocaleString()}. Edits to deals since then don’t change it.`
              : 'The weighted total by owner and currency, kept as it is when you save it.'}
          </p>
        </div>
        <button
          type="button"
          disabled={saving}
          onClick={save}
          className="h-9 rounded-xl bg-slate-900 px-3 text-sm font-medium text-white disabled:opacity-60"
        >
          {saving ? 'Saving…' : savedAt ? 'Save again' : 'Save this month’s forecast'}
        </button>
      </div>
      {rows && rows.length > 0 && (
        <div className="mt-3 overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead>
              <tr className="text-[11px] uppercase tracking-wide text-slate-400">
                <th className="py-1.5 pr-3 font-semibold">Owner</th>
                <th className="py-1.5 pr-3 text-right font-semibold">Weighted</th>
                <th className="py-1.5 pr-3 text-right font-semibold">Open</th>
                <th className="py-1.5 text-right font-semibold">Without probability</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.id} className="border-t border-slate-100">
                  <td className="py-1.5 pr-3 text-slate-700">{r.assigneeName}</td>
                  <td className="py-1.5 pr-3 text-right font-medium text-slate-900">{amount(r.weightedValue, r.currency)}</td>
                  <td className="py-1.5 pr-3 text-right text-slate-600">{amount(r.openValue, r.currency)}</td>
                  <td className="py-1.5 text-right text-slate-500">{r.missing}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

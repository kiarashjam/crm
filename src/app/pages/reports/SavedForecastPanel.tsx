// A month's forecast as managers saved it, set against what was actually won.
//
// The live forecast moves whenever a deal does. A saved one doesn't, which is what
// a month's commitment needs: what was forecast on the 1st, to set against what
// closed. Saving used to replace the month's earlier save, so the commitment made
// on the 1st was gone by the 30th; every save is now kept and can be picked here.
// A save counts the deals expected to close in that month, by their close date.
// Saving and reading are for the owner and managers, because the saved lines count
// private deals and name their owners.

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { toast } from 'sonner';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import {
  getForecastSnapshots, saveForecastSnapshot, type ForecastSnapshotSet,
} from '@/app/api/reporting';
import { downloadCsv } from '@/app/lib/exportCsv';
import { CsvButton } from './ReportUI';
import { savedForecastTable } from './reportCsv';
import { addMonths, monthLabel, monthOf } from './reportFilters';
import { useMoney } from '@/app/hooks/useMoney';

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

interface Line {
  owner: string;
  currency: string;
  weighted: number;
  expected: number;
  won: number;
  wonCount: number;
}

/** The saved lines beside what was won, one row per owner and currency. */
export function compareWithActual(set: ForecastSnapshotSet): Line[] {
  const lines = new Map<string, Line>();
  const line = (owner: string, currency: string) => {
    const k = `${owner}|${currency}`;
    const l = lines.get(k) ?? { owner, currency, weighted: 0, expected: 0, won: 0, wonCount: 0 };
    lines.set(k, l);
    return l;
  };
  for (const r of set.rows) {
    const l = line(r.assigneeName, r.currency);
    l.weighted += r.weightedValue;
    l.expected += r.openValue;
  }
  for (const a of set.actual) {
    const l = line(a.assigneeName, a.currency);
    l.won += a.wonValue;
    l.wonCount += a.wonCount;
  }
  return [...lines.values()].sort((a, b) => a.currency.localeCompare(b.currency) || a.owner.localeCompare(b.owner));
}

export function SavedForecastPanel() {
  // The month a save is for is the workspace's, as the server counts it: in a browser
  // behind the club's clock, the 1st there was still last month here.
  const money = useMoney();
  const { locale, timeZone } = money;
  const amount = (value: number, currency: string) => money.format(value, currency, 'never');
  const thisMonth = monthOf(new Date(), timeZone);
  const [month, setMonth] = useState(thisMonth);
  const [snapshotId, setSnapshotId] = useState<string | undefined>(undefined);
  const [set, setSet] = useState<ForecastSnapshotSet | null>(null);
  const [failed, setFailed] = useState(false);
  const [saving, setSaving] = useState(false);

  const [year, monthNumber] = month.split('-').map(Number) as [number, number];
  const latest = useRef(0);

  const load = useCallback(() => {
    // Stepping through the months asks for each in turn, and the answers can come
    // back out of order: only the one for the month now shown may fill the panel,
    // or a slow answer for September showed its saves under August.
    const ticket = ++latest.current;
    setFailed(false);
    getForecastSnapshots(year, monthNumber, snapshotId)
      .then((s) => { if (ticket === latest.current) setSet(s); })
      .catch(() => { if (ticket === latest.current) setFailed(true); });
  }, [year, monthNumber, snapshotId]);

  const showMonth = (by: number) => {
    setSnapshotId(undefined);
    // The saves on show are the other month's, so they go now rather than when
    // this month's arrive.
    setSet(null);
    setMonth(addMonths(month, by));
  };

  useEffect(() => { load(); }, [load]);

  // A forecast is made before or during its month. A month already over is shown
  // for its record, not saved again in hindsight.
  const canSave = month >= thisMonth;

  const save = async () => {
    setSaving(true);
    try {
      const saved = await saveForecastSnapshot(year, monthNumber);
      setSnapshotId(saved.snapshotId ?? undefined);
      setSet(saved);
      toast.success(saved.saves.length > 1
        ? `Saved. The ${saved.saves.length - 1} earlier save${saved.saves.length === 2 ? ' is' : 's are'} kept.`
        : 'Saved. Later edits to deals will not change it.');
    } catch (err) {
      toast.error(reason(err, 'The forecast could not be saved.'));
    } finally {
      setSaving(false);
    }
  };

  const chosen = set?.saves.find((s) => s.snapshotId === set.snapshotId) ?? null;
  const lines = useMemo(() => (set ? compareWithActual(set) : []), [set]);
  const extra = useMemo(() => {
    if (!set) return null;
    const overdue = set.rows.reduce((s, r) => s + r.overdueCount, 0);
    const undated = set.rows.reduce((s, r) => s + r.noCloseDateCount, 0);
    const missing = set.rows.reduce((s, r) => s + r.missing, 0);
    const unreadable = set.rows.reduce((s, r) => s + r.unreadableCount, 0);
    return { overdue, undated, missing, unreadable };
  }, [set]);

  return (
    <div className="mt-3 rounded-2xl border border-slate-200 bg-white p-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h3 className="text-sm font-bold text-slate-800">Saved forecast</h3>
          <p className="mt-0.5 text-[11px] text-slate-500">
            Deals expected to close in the month, weighted by probability, as they stood when saved — beside what was won.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex items-center rounded-xl border border-slate-200">
            {/* Held while a save is on its way: its answer is the month it was saved
                for, and arriving under another month it was shown as that one's. */}
            <button type="button" aria-label="Previous month" onClick={() => showMonth(-1)} disabled={saving}
              className="h-9 w-8 rounded-l-xl text-slate-500 hover:bg-slate-50 disabled:opacity-50"><ChevronLeft className="mx-auto h-4 w-4" /></button>
            <span className="min-w-[88px] px-1 text-center text-sm font-medium text-slate-700">{monthLabel(month, locale)}</span>
            <button type="button" aria-label="Next month" onClick={() => showMonth(1)} disabled={saving}
              className="h-9 w-8 rounded-r-xl text-slate-500 hover:bg-slate-50 disabled:opacity-50"><ChevronRight className="mx-auto h-4 w-4" /></button>
          </div>
          {canSave && (
            <button
              type="button"
              disabled={saving}
              onClick={save}
              className="h-9 rounded-xl bg-slate-900 px-3 text-sm font-medium text-white disabled:opacity-60"
            >
              {saving ? 'Saving…' : set && set.saves.length > 0 ? 'Save again (keeps the earlier one)' : `Save ${monthLabel(month, locale)}’s forecast`}
            </button>
          )}
        </div>
      </div>

      {failed && (
        <p className="mt-3 text-sm text-rose-700">
          The saved forecasts could not be loaded. <button type="button" className="underline" onClick={load}>Try again</button>
        </p>
      )}

      {set && set.saves.length === 0 && !failed && (
        <p className="mt-3 text-[12px] text-slate-500">
          Nothing saved for {monthLabel(month, locale)}.{canSave ? ' Saving keeps the forecast as it stands now, to compare with what closes.' : ''}
        </p>
      )}

      {set && set.saves.length > 0 && (
        <>
          <div className="mt-3 flex flex-wrap items-center gap-2 text-[12px] text-slate-600">
            <label htmlFor="forecast-save" className="font-medium">Save shown</label>
            <select
              id="forecast-save"
              value={set.snapshotId ?? ''}
              onChange={(e) => setSnapshotId(e.target.value)}
              className="h-8 rounded-lg border border-slate-200 bg-white px-2 text-[12px]"
            >
              {set.saves.map((s, i) => (
                <option key={s.snapshotId} value={s.snapshotId}>
                  {new Date(s.savedAtUtc).toLocaleString(locale, { timeZone, dateStyle: 'medium', timeStyle: 'short' })} by {s.savedByName}{i === set.saves.length - 1 ? ' (first)' : ''}
                </option>
              ))}
            </select>
            <CsvButton
              what="this saved forecast"
              onClick={() => {
                const t = savedForecastTable(set.rows, chosen?.savedByName ?? '');
                downloadCsv(`cadence-${t.name}-${set.month}`, t.rows, t.columns);
              }}
            />
          </div>
          {chosen && !chosen.byCloseDate && (
            <p className="mt-2 rounded-lg bg-amber-50 px-3 py-2 text-[11.5px] text-amber-900">
              Saved before forecasts went by close date: these lines count every deal that was open, whatever month it was expected to close in.
            </p>
          )}
          <div className="mt-3 overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead>
                <tr className="text-[11px] uppercase tracking-wide text-slate-500">
                  <th className="py-1.5 pr-3 font-semibold">Owner</th>
                  <th className="py-1.5 pr-3 text-right font-semibold">Forecast (weighted)</th>
                  <th className="py-1.5 pr-3 text-right font-semibold">Expected value</th>
                  <th className="py-1.5 text-right font-semibold">Won in {monthLabel(month, locale)}</th>
                </tr>
              </thead>
              <tbody>
                {lines.map((l) => (
                  <tr key={`${l.owner}|${l.currency}`} className="border-t border-slate-100">
                    <td className="py-1.5 pr-3 text-slate-700">{l.owner}</td>
                    <td className="py-1.5 pr-3 text-right font-medium text-slate-900">{amount(l.weighted, l.currency)}</td>
                    <td className="py-1.5 pr-3 text-right text-slate-600">{amount(l.expected, l.currency)}</td>
                    <td className="py-1.5 text-right text-emerald-700">
                      {amount(l.won, l.currency)}{l.wonCount > 0 ? ` · ${l.wonCount}` : ''}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {extra && (extra.overdue > 0 || extra.undated > 0 || extra.missing > 0 || extra.unreadable > 0) && (
            <p className="mt-2 text-[11px] text-slate-500">
              Not in the forecast:{' '}
              {[
                extra.missing > 0 && `${extra.missing} expected that month with no probability`,
                extra.overdue > 0 && `${extra.overdue} still open past an earlier close date`,
                extra.undated > 0 && `${extra.undated} with no close date`,
                extra.unreadable > 0 && `${extra.unreadable} whose value could not be read`,
              ].filter(Boolean).join(' · ')}.
            </p>
          )}
        </>
      )}
    </div>
  );
}

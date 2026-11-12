// The accounting journal for a period: the ledger's entries as double-entry bookings in the
// club's accounts, previewed here with what each account moved by, and downloaded as the
// semicolon CSV Bexio and Abacus import.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Download, Eye, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { DateInput } from '@/app/components/DateInput';
import { Label } from '@/app/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { downloadJournalCsv, getJournal, type Journal, type JournalLayout } from '@/app/api/payments';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { todayKeyIn } from '@/app/lib/zonedTime';
import { lastMonth, periodIn } from './journalPeriod';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export function JournalExportPanel({ canTake }: { canTake: boolean }) {
  const { timeZone, locale } = useRegional();
  const [period, setPeriod] = useState(() => lastMonth(todayKeyIn(timeZone)));
  const [layout, setLayout] = useState<JournalLayout>('gross');
  const [busy, setBusy] = useState<null | 'preview' | 'csv'>(null);
  const [journal, setJournal] = useState<Journal | null>(null);

  if (!canTake) {
    return (
      <p className="max-w-3xl rounded-2xl border border-slate-200 bg-white p-6 text-sm text-slate-600 shadow-sm">
        Only someone who takes payments exports the accounting journal.
      </p>
    );
  }

  const range = periodIn(period.from, period.to, timeZone);

  const preview = async () => {
    if (!range) return;
    setBusy('preview');
    try {
      setJournal(await getJournal(range.fromUtc, range.toUtc, layout));
    } catch (err) {
      toast.error(message(err, 'Could not read the journal'));
    } finally {
      setBusy(null);
    }
  };

  const download = async () => {
    if (!range) return;
    setBusy('csv');
    try {
      await downloadJournalCsv(range.fromUtc, range.toUtc, layout);
    } catch (err) {
      toast.error(message(err, 'Could not download the journal'));
    } finally {
      setBusy(null);
    }
  };

  const money = (amount: number) => formatClubMoney(amount, journal?.currency || 'CHF', locale);

  return (
    <div className="space-y-6">
      <section className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
        <h3 className="mb-1 font-semibold text-slate-900">Accounting journal</h3>
        <p className="mb-4 text-sm text-slate-500">
          Every charge, payment, refund, credit and gift card movement in the period, as a booking that debits one account and
          credits another, in the accounts set in{' '}
          <Link to="/hospitality-settings?tab=payments" className="font-medium underline">Club settings, Payments</Link>.
          Consecutive periods add up to the whole, so a month exported after the last one fits on.
        </p>
        <div className="grid gap-4 sm:grid-cols-3">
          <div>
            <Label htmlFor="journal-from" className="text-xs">From</Label>
            <DateInput id="journal-from" value={period.from} onChange={(day) => { setPeriod({ ...period, from: day }); setJournal(null); }} />
          </div>
          <div>
            <Label htmlFor="journal-to" className="text-xs">To (the whole day)</Label>
            <DateInput id="journal-to" value={period.to} onChange={(day) => { setPeriod({ ...period, to: day }); setJournal(null); }} />
          </div>
          <div>
            <Label className="text-xs">VAT</Label>
            <Select value={layout} onValueChange={(v) => { setLayout(v as JournalLayout); setJournal(null); }}>
              <SelectTrigger aria-label="Layout"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="gross">Gross, with VAT codes (Bexio, Abacus)</SelectItem>
                <SelectItem value="net">Net, VAT on its own line</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        {!range && <p className="mt-3 text-sm text-rose-700">The period must start on or before the day it ends.</p>}
        <div className="mt-4 flex flex-wrap gap-2">
          <Button type="button" variant="outline" onClick={() => void preview()} disabled={!range || busy !== null}>
            {busy === 'preview' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Eye className="mr-1 h-4 w-4" />} Preview
          </Button>
          <Button type="button" onClick={() => void download()} disabled={!range || busy !== null}>
            {busy === 'csv' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Download className="mr-1 h-4 w-4" />} Download CSV
          </Button>
        </div>
        <details className="mt-4 text-sm text-slate-600">
          <summary className="cursor-pointer font-medium text-slate-700">The file</summary>
          <p className="mt-2">
            UTF-8 with a byte-order mark, fields separated by semicolons, dates as dd.MM.yyyy, amounts with a full stop and always
            positive (the debited account gives the direction). Columns: Date; Document; Debit; Credit; Amount; Currency; VatCode;
            VatRate; VatAmount; Text; Member; Type; EntryId.
          </p>
          <p className="mt-2">
            Bexio: Accounting, Manual entries, Import. Abacus: FIBU, Buchungen importieren. Map the columns once; each program
            keeps the mapping. With the gross layout, set your program&apos;s VAT codes in the accounts first.
          </p>
        </details>
      </section>

      {journal && (
        <section aria-label="Journal preview" className="space-y-4">
          <p className="text-sm text-slate-600">
            {journal.rows.length} {journal.rows.length === 1 ? 'booking' : 'bookings'} in {journal.currency}.
          </p>
          {journal.accounts.length > 0 && (
            <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
              <table className="w-full text-sm">
                <thead className="border-b border-slate-200 bg-slate-50">
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3">Account</th>
                    <th className="px-4 py-3 text-right">Debits</th>
                    <th className="px-4 py-3 text-right">Credits</th>
                    <th className="px-4 py-3 text-right">Moved by</th>
                  </tr>
                </thead>
                <tbody>
                  {journal.accounts.map((a) => (
                    <tr key={a.account} className="border-b border-slate-100">
                      <td className="px-4 py-2 font-mono">{a.account}</td>
                      <td className="px-4 py-2 text-right">{money(a.debits)}</td>
                      <td className="px-4 py-2 text-right">{money(a.credits)}</td>
                      <td className="px-4 py-2 text-right font-medium">{money(a.net)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {journal.rows.length > 0 && (
            <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
              <table className="w-full text-sm">
                <thead className="border-b border-slate-200 bg-slate-50">
                  <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                    <th className="px-4 py-3">Date</th>
                    <th className="px-4 py-3">Document</th>
                    <th className="px-4 py-3">Debit</th>
                    <th className="px-4 py-3">Credit</th>
                    <th className="px-4 py-3 text-right">Amount</th>
                    <th className="px-4 py-3">Text</th>
                  </tr>
                </thead>
                <tbody>
                  {journal.rows.slice(0, 50).map((r) => (
                    <tr key={`${r.entryId}-${r.type}-${r.debit}-${r.credit}`} className="border-b border-slate-100">
                      <td className="whitespace-nowrap px-4 py-2 text-slate-500">{r.date}</td>
                      <td className="px-4 py-2 font-mono text-xs">{r.document}</td>
                      <td className="px-4 py-2 font-mono">{r.debit}</td>
                      <td className="px-4 py-2 font-mono">{r.credit}</td>
                      <td className="px-4 py-2 text-right">{money(r.amount)}</td>
                      <td className="px-4 py-2 text-slate-700">{r.text}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
              {journal.rows.length > 50 && (
                <p className="px-4 py-2 text-xs text-slate-500">The first 50 of {journal.rows.length}; the CSV has them all.</p>
              )}
            </div>
          )}
        </section>
      )}
    </div>
  );
}

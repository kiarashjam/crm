// Who brings members in, on Club Reports: each member who referred others, how many they brought
// in (in the period and ever), what those members were charged in the period (dues apart from
// everything else, per currency), the applicants they proposed or seconded, and the bonuses
// they were given. Summed on the server; exported as the server's spreadsheet for whoever may
// export (records.export).

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { Download, Handshake, Loader2 } from 'lucide-react';
import { amountIn, downloadReferralReportCsv, type ReferralReportRange } from '@/app/api/retention';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { useReferralReport } from '@/app/hooks/queries';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';

export function ReferralsReportCard({ range, canExport }: { range: ReferralReportRange | null; canExport: boolean }) {
  const { locale } = useRegional();
  const query = useReferralReport(range);
  const [exporting, setExporting] = useState(false);
  const report = query.data;
  const money = (amount: number, currency: string) => formatClubMoney(amount, currency, locale);

  const exportCsv = async () => {
    if (!range) return;
    setExporting(true);
    try {
      await downloadReferralReportCsv(range);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The spreadsheet could not be made.');
    } finally {
      setExporting(false);
    }
  };

  return (
    <section className="mt-6 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-labelledby="referrals-report">
      <div className="mb-1 flex flex-wrap items-center justify-between gap-2">
        <h2 id="referrals-report" className="inline-flex items-center gap-2 font-semibold text-slate-900">
          <Handshake className="h-4 w-4" aria-hidden /> Who brings members in
        </h2>
        {canExport && (
          <Button type="button" variant="outline" size="sm" onClick={() => void exportCsv()} disabled={exporting || !report}>
            <Download className="h-4 w-4" aria-hidden /> {exporting ? 'Exporting…' : 'Export referrals'}
          </Button>
        )}
      </div>
      <p className="mb-3 text-xs text-slate-500">
        Members who referred others, and what the members they brought in were charged in the period. Proposed and seconded count the applicants approved in it.
      </p>
      {query.isError && !report ? (
        <LoadFailed what="The referrals report" error={query.error} onRetry={() => { void query.refetch(); }} retrying={query.isFetching} />
      ) : !report ? (
        <p className="flex items-center gap-2 text-sm text-slate-500"><Loader2 className="h-4 w-4 animate-spin" aria-hidden /> Loading…</p>
      ) : report.referrers.length === 0 ? (
        <p className="text-sm text-slate-500">No member has referred, proposed or seconded anyone yet.</p>
      ) : (
        <>
          <p className="mb-3 text-sm text-slate-600">
            {report.totals.referrers} {report.totals.referrers === 1 ? 'member' : 'members'} brought in {report.totals.referredAllTime},{' '}
            {report.totals.referredInPeriod} of them in the period ({report.totals.membersJoinedInPeriod} joined in all), who were charged{' '}
            <span className="font-semibold text-slate-900">{money(amountIn(report.totals.revenue, report.currency), report.currency)}</span>.
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <caption className="sr-only">Members who brought others in</caption>
              <thead>
                <tr className="text-left text-xs text-slate-500">
                  <th scope="col" className="py-1 pr-3 font-medium">Member</th>
                  <th scope="col" className="py-1 pr-3 text-right font-medium">Brought in</th>
                  <th scope="col" className="py-1 pr-3 text-right font-medium">Dues</th>
                  <th scope="col" className="py-1 pr-3 text-right font-medium">Spending</th>
                  <th scope="col" className="py-1 pr-3 text-right font-medium">Revenue</th>
                  <th scope="col" className="py-1 pr-3 text-right font-medium">Proposed / seconded</th>
                  <th scope="col" className="py-1 text-right font-medium">Bonuses</th>
                </tr>
              </thead>
              <tbody>
                {report.referrers.map((r) => {
                  const credit = amountIn(r.bonusCredit, report.currency);
                  return (
                    <tr key={r.memberId} className="border-t border-slate-100 align-top">
                      <th scope="row" className="py-1.5 pr-3 text-left font-normal">
                        <Link to={`/members/${r.memberId}`} className="font-medium text-slate-900 hover:underline">{r.name}</Link>
                        {r.members.length > 0 && (
                          <span className="block text-xs text-slate-500">{r.members.map((m) => m.name).join(', ')}</span>
                        )}
                      </th>
                      <td className="py-1.5 pr-3 text-right">{r.referredInPeriod} <span className="text-xs text-slate-500">of {r.referredAllTime}</span></td>
                      <td className="py-1.5 pr-3 text-right">{money(amountIn(r.dues, report.currency), report.currency)}</td>
                      <td className="py-1.5 pr-3 text-right">{money(amountIn(r.spend, report.currency), report.currency)}</td>
                      <td className="py-1.5 pr-3 text-right font-semibold text-slate-900">{money(amountIn(r.revenue, report.currency), report.currency)}</td>
                      <td className="py-1.5 pr-3 text-right">{r.proposed} / {r.seconded}</td>
                      <td className="py-1.5 text-right text-xs text-slate-600">
                        {[r.bonusPoints > 0 ? `${r.bonusPoints} points` : '', credit > 0 ? money(credit, report.currency) : ''].filter(Boolean).join(', ') || '—'}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}

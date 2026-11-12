// Club Reports: the club's money, members, visits and events for a period, summed on the
// server from the ledger (GET /api/club-reports).
//
// This page downloaded every member, charge, visit and loyalty entry and added them up
// here. Revenue was what fully paid charges came to, so dues paid in three instalments
// showed nothing for two months and everything in the third; everything was in dollars;
// and when a request failed it showed zeros, which read as a dead month. Now: cash
// revenue is the money received when it came, accrual revenue what was charged (voids
// in their own period), receivables are aged by days past due, and gift cards, points
// and subscriptions are what the club owes or will bill. Amounts are in the workspace's
// currency, with any other currency listed apart, never added in. A failed read says so.

import { useMemo, useState } from 'react';
import {
  BarChart3,
  CalendarHeart,
  Coins,
  Crown,
  Download,
  Gift,
  HandCoins,
  Loader2,
  Receipt,
  Repeat2,
  ScanLine,
  Sparkles,
  TrendingUp,
  Wallet,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { LoadFailed } from '@/app/components/LoadFailed';
import { Button } from '@/app/components/ui/button';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { useClubReport } from '@/app/hooks/queries/club';
import { useRegional } from '@/app/hooks/useMoney';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { orgCan, PERMISSION } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import {
  downloadClubReportCsv,
  type ClubMoneyFlow,
  type ClubReport,
  type ClubReportGrain,
  type ClubReportPeriod,
} from '@/app/api/clubReports';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { customPeriod, dateInputValue, periodFor, PERIOD_LABELS, type PeriodPreset } from '@/app/pages/reports/reportFilters';
import { currentDateLocale, currentNumberLocale } from '@/app/i18n/current';

type GrainChoice = 'auto' | ClubReportGrain;

const PRESETS: PeriodPreset[] = ['this-month', 'last-month', 'last-30', 'last-90', 'this-quarter', 'this-year', 'last-year', 'custom'];

/** A period's first day (yyyy-MM-dd, the workspace's calendar) as its column says it. */
function periodLabel(period: ClubReportPeriod, grain: ClubReportGrain): string {
  const [y, m, d] = period.start.split('-').map(Number) as [number, number, number];
  const day = new Date(y, m - 1, d);
  if (grain === 'month') return day.toLocaleDateString(currentDateLocale(), { month: 'short', year: '2-digit' });
  const short = day.toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short' });
  return grain === 'week' ? `Wk ${short}` : short;
}

function percent(rate: number | null): string {
  return rate == null ? '—' : `${Math.round(rate * 100)}%`;
}

export default function HospitalityReports() {
  const regional = useRegional();
  const org = useOrgOptional();
  const canExport = orgCan(org?.currentOrg ?? null, PERMISSION.ExportRecords) && !servesDemoData();
  const [preset, setPreset] = useState<PeriodPreset>('this-year');
  const [customFrom, setCustomFrom] = useState(() => dateInputValue(new Date(Date.now() - 29 * 86_400_000), regional.timeZone));
  const [customTo, setCustomTo] = useState(() => dateInputValue(new Date(), regional.timeZone));
  const [grain, setGrain] = useState<GrainChoice>('auto');
  const [exporting, setExporting] = useState(false);

  // The period between the workspace's midnights; a custom one that ends before it starts is not asked.
  const period = useMemo(
    () => (preset === 'custom' ? customPeriod(customFrom, customTo, regional.timeZone) : periodFor(preset, regional.timeZone)),
    [preset, customFrom, customTo, regional.timeZone],
  );
  const range = useMemo(() => (period ? {
    fromUtc: period.from.toISOString(),
    toUtc: period.to.toISOString(),
    ...(grain === 'auto' ? {} : { grain }),
  } : null), [period, grain]);
  const query = useClubReport(range);
  const report = query.data;
  usePageLoading(query.isPending);

  const money = (amount: number, currency: string) => formatClubMoney(amount, currency, regional.locale);

  const exportCsv = async () => {
    if (!range) return;
    setExporting(true);
    try {
      await downloadClubReportCsv(range);
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : 'The spreadsheet could not be made.');
    } finally {
      setExporting(false);
    }
  };

  const controls = (
    <div className="flex flex-wrap items-end gap-3 mb-6" role="group" aria-label="Period">
      <label className="text-sm font-medium text-slate-700">
        Period
        <select
          className="mt-1 block h-10 rounded-xl border border-slate-300 bg-white px-3 text-sm"
          value={preset}
          onChange={(e) => setPreset(e.target.value as PeriodPreset)}
        >
          {PRESETS.map((p) => <option key={p} value={p}>{PERIOD_LABELS[p]}</option>)}
        </select>
      </label>
      {preset === 'custom' && (
        <>
          <label className="text-sm font-medium text-slate-700">
            From
            <input type="date" className="mt-1 block h-10 rounded-xl border border-slate-300 bg-white px-3 text-sm" value={customFrom} onChange={(e) => setCustomFrom(e.target.value)} />
          </label>
          <label className="text-sm font-medium text-slate-700">
            To
            <input type="date" className="mt-1 block h-10 rounded-xl border border-slate-300 bg-white px-3 text-sm" value={customTo} onChange={(e) => setCustomTo(e.target.value)} />
          </label>
        </>
      )}
      <label className="text-sm font-medium text-slate-700">
        By
        <select
          className="mt-1 block h-10 rounded-xl border border-slate-300 bg-white px-3 text-sm"
          value={grain}
          onChange={(e) => setGrain(e.target.value as GrainChoice)}
        >
          <option value="auto">As suits the period</option>
          <option value="day">Day</option>
          <option value="week">Week</option>
          <option value="month">Month</option>
        </select>
      </label>
      {!period && <p role="alert" className="text-sm text-amber-800">Pick an end on or after the start.</p>}
    </div>
  );

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={BarChart3}
            tone="emerald"
            title="Club Reports"
            subtitle={report
              ? `Money received and charged, receivables, gift cards, points and events. Amounts in ${report.currency}; any other currency is listed apart, never added in.`
              : 'Money received and charged, receivables, gift cards, points and events.'}
            actions={canExport ? (
              <Button type="button" variant="outline" className={heroSecondary} onClick={() => void exportCsv()} disabled={exporting || !report || !range}>
                <Download className="h-4 w-4" aria-hidden="true" /> {exporting ? 'Exporting…' : 'Export CSV'}
              </Button>
            ) : undefined}
            stats={report ? headline(report, money) : undefined}
          />

          {controls}

          {query.isPending ? (
            <div className="flex items-center justify-center py-20 text-slate-500" role="status">
              <Loader2 className="h-6 w-6 animate-spin" aria-hidden="true" /> <span className="sr-only">Loading the figures</span>
            </div>
          ) : query.isError && !report ? (
            <LoadFailed what="The club's figures" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />
          ) : report ? (
            <>
              {query.isError && (
                <LoadFailed stale what="The club's figures" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} className="mb-4" />
              )}
              <ReportBody report={report} money={money} />
            </>
          ) : null}
        </main>
      </PageTransition>
    </div>
  );
}

type Money = (amount: number, currency: string) => string;

/** The workspace's currency's figure in a per-currency list, or nothing. */
function own<T extends { currency: string }>(rows: T[], currency: string): T | undefined {
  return rows.find((r) => r.currency === currency);
}

function headline(report: ClubReport, money: Money) {
  const c = report.currency;
  const cash = own(report.cash, c)?.net ?? 0;
  const accrual = own(report.accrual, c)?.net ?? 0;
  const owed = own(report.receivables, c)?.total ?? 0;
  const mrr = own(report.mrr, c)?.monthly ?? 0;
  return [
    { label: 'Cash revenue', hint: 'received, less paid back', value: cash, icon: Wallet, tone: 'emerald' as const, format: (n: number) => money(n, c) },
    { label: 'Accrual revenue', hint: 'charged, less voided', value: accrual, icon: Receipt, tone: 'indigo' as const, format: (n: number) => money(n, c) },
    { label: 'Receivables', hint: 'owed at the end', value: owed, icon: HandCoins, tone: 'rose' as const, format: (n: number) => money(n, c) },
    { label: 'MRR', hint: 'subscriptions now', value: mrr, icon: Repeat2, tone: 'violet' as const, format: (n: number) => money(n, c) },
  ];
}

function ReportBody({ report, money }: { report: ClubReport; money: Money }) {
  const c = report.currency;
  const others = (rows: { currency: string }[]) => rows.filter((r) => r.currency !== c).map((r) => r.currency);
  const otherCurrencies = [...new Set([...others(report.cash), ...others(report.accrual), ...others(report.receivables), ...others(report.giftCards)])];
  const giftCards = own(report.giftCards, c);
  const ageing = own(report.receivables, c);
  const kinds = report.accrualByKind.filter((k) => k.currency === c && k.net !== 0);
  const kindTotal = kinds.reduce((s, k) => s + Math.max(0, k.net), 0);
  const events = report.events;
  const maxJoined = Math.max(1, ...report.members.joinedByPeriod);
  const activeTotal = Math.max(1, report.members.activeByTier.reduce((s, t) => s + t.count, 0));
  const maxVenue = Math.max(1, ...report.visits.byVenue.map((v) => v.count));

  return (
    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
      <FlowCard index={0} title="Cash revenue" subtitle="Money received when it came, less refunds and payments taken back" icon={<Wallet className="w-4 h-4" />}
        flow={own(report.cash, c)} report={report} money={money} inLabel="Received" outLabel="Paid back" />
      <FlowCard index={1} title="Accrual revenue" subtitle="What was charged, less voids in their own period; balances brought over are not sales" icon={<Receipt className="w-4 h-4" />}
        flow={own(report.accrual, c)} report={report} money={money} inLabel="Charged" outLabel="Voided" />

      <Card index={2} title="Revenue by category" icon={<Coins className="w-4 h-4" />}>
        {kinds.length === 0 ? <Empty /> : (
          <ul className="space-y-2">
            {kinds.map((k) => (
              <li key={k.kind}>
                <div className="flex items-center justify-between text-sm mb-1">
                  <span className="text-slate-700">{k.kind}</span>
                  <span className="font-semibold text-slate-900">{money(k.net, c)}</span>
                </div>
                <Bar share={kindTotal > 0 ? Math.max(0, k.net) / kindTotal : 0} tone="bg-emerald-500" />
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card index={3} title="Receivables by age" icon={<HandCoins className="w-4 h-4" />}
        note={`What members owed ${report.asOfUtc < report.toUtc ? 'now' : 'at the end of the period'}, by days past the due date.`}>
        {!ageing ? <p className="text-sm text-slate-500">Nobody owes anything.</p> : (
          <ul className="space-y-2">
            {([
              ['Not yet due', ageing.current], ['1–30 days', ageing.days1To30], ['31–60 days', ageing.days31To60],
              ['61–90 days', ageing.days61To90], ['Over 90 days', ageing.over90],
            ] as const).map(([label, amount]) => (
              <li key={label}>
                <div className="flex items-center justify-between text-sm mb-1">
                  <span className="text-slate-700">{label}</span>
                  <span className="font-semibold text-slate-900">{money(amount, c)}</span>
                </div>
                <Bar share={ageing.total > 0 ? amount / ageing.total : 0} tone={label === 'Not yet due' ? 'bg-slate-400' : 'bg-rose-500'} />
              </li>
            ))}
            <li className="pt-1 text-xs text-slate-500">{ageing.openCharges} open charge{ageing.openCharges === 1 ? '' : 's'}, {money(ageing.total, c)} in all</li>
          </ul>
        )}
      </Card>

      <Card index={4} title="What the club owes and will bill" icon={<Gift className="w-4 h-4" />}>
        <dl className="grid grid-cols-1 sm:grid-cols-3 gap-4 text-sm">
          <div>
            <dt className="text-slate-500">Left on gift cards</dt>
            <dd className="text-lg font-semibold text-slate-900">{money(giftCards?.outstanding ?? 0, c)}</dd>
            <dd className="text-xs text-slate-500">{giftCards?.cards ?? 0} card{giftCards?.cards === 1 ? '' : 's'} with value left</dd>
          </div>
          <div>
            <dt className="text-slate-500">Loyalty points held</dt>
            <dd className="text-lg font-semibold text-slate-900">{money(report.points.value, report.points.currency)}</dd>
            <dd className="text-xs text-slate-500">{report.points.points.toLocaleString(currentNumberLocale())} points at {report.points.pointsPerUnit} a {report.points.currency}</dd>
          </div>
          <div>
            <dt className="text-slate-500">Subscriptions each month</dt>
            <dd className="text-lg font-semibold text-slate-900">{money(own(report.mrr, c)?.monthly ?? 0, c)}</dd>
            <dd className="text-xs text-slate-500">{own(report.mrr, c)?.subscriptions ?? 0} running, {money(own(report.mrr, c)?.yearly ?? 0, c)} a year</dd>
          </div>
        </dl>
      </Card>

      <Card index={5} title="Events" icon={<CalendarHeart className="w-4 h-4" />}>
        <dl className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <dt className="text-slate-500">Seats filled</dt>
            <dd className="text-3xl font-bold text-pink-600">{percent(events.fillRate)}</dd>
            <dd className="text-xs text-slate-500">{events.seatsTaken} of {events.capacity} seats, {events.limitedEvents} event{events.limitedEvents === 1 ? '' : 's'} with a limit</dd>
          </div>
          <div>
            <dt className="text-slate-500">Did not come</dt>
            <dd className="text-3xl font-bold text-rose-600">{percent(events.noShowRate)}</dd>
            <dd className="text-xs text-slate-500">
              {events.noShows} not come, {events.checkedIn} checked in{events.notMarked ? `, ${events.notMarked} not marked` : ''}
            </dd>
          </div>
        </dl>
        <p className="mt-3 text-xs text-slate-500">{events.events} event{events.events === 1 ? '' : 's'} in the period, {events.ended} over.</p>
      </Card>

      <Card index={6} title="Top spenders" icon={<TrendingUp className="w-4 h-4" />} note="Charges in the period, voids taken off; a household's rolled-up bill counts for who ran it up.">
        {report.topSpenders.length === 0 ? <Empty /> : (
          <ol className="space-y-2">
            {report.topSpenders.map((s, i) => (
              <li key={s.memberId} className="flex items-center justify-between gap-2 text-sm">
                <span className="flex items-center gap-3 min-w-0">
                  <span className="text-xs font-bold text-slate-500 w-5">{i + 1}</span>
                  <span className="font-medium text-slate-900 truncate">{s.memberName}</span>
                </span>
                <span className="font-semibold text-emerald-700">{money(s.amount, s.currency)}</span>
              </li>
            ))}
          </ol>
        )}
      </Card>

      <Card index={7} title="Members" icon={<Crown className="w-4 h-4" />}>
        <p className="text-sm text-slate-600 mb-3">
          {report.members.active} active, {report.members.lapsed} lapsed; {report.members.joined} joined in the period.
        </p>
        <ul className="space-y-2 mb-4">
          {report.members.activeByTier.map((t) => (
            <li key={t.name}>
              <div className="flex items-center justify-between text-sm mb-1">
                <span className="text-slate-700">{t.name}</span>
                <span className="font-semibold text-slate-900">{t.count}</span>
              </div>
              <Bar share={t.count / activeTotal} tone="bg-indigo-500" />
            </li>
          ))}
        </ul>
        <div className="flex items-end gap-1 h-20" aria-label="Members who joined, by period">
          {report.members.joinedByPeriod.map((n, i) => (
            <div key={report.periods[i]?.start ?? i} className="flex h-full flex-1 flex-col items-center justify-end" title={`${periodLabel(report.periods[i]!, report.grain)}: ${n}`}>
              <div className="w-full rounded-t bg-gradient-to-t from-emerald-500 to-teal-400" style={{ height: `${(n / maxJoined) * 100}%`, minHeight: n > 0 ? 3 : 0 }} />
            </div>
          ))}
        </div>
      </Card>

      <Card index={8} title="Check-ins by venue" icon={<ScanLine className="w-4 h-4" />}>
        {report.visits.byVenue.length === 0 ? <Empty /> : (
          <ul className="space-y-2">
            {report.visits.byVenue.map((v) => (
              <li key={v.name}>
                <div className="flex items-center justify-between text-sm mb-1">
                  <span className="text-slate-700">{v.name}</span>
                  <span className="font-semibold text-slate-900">{v.count}</span>
                </div>
                <Bar share={v.count / maxVenue} tone="bg-sky-500" />
              </li>
            ))}
          </ul>
        )}
        <p className="mt-3 text-xs text-slate-500">{report.visits.checkIns} check-in{report.visits.checkIns === 1 ? '' : 's'} in the period.</p>
      </Card>

      {otherCurrencies.length > 0 && (
        <Card index={9} title="Other currencies" icon={<Sparkles className="w-4 h-4" />} note="Listed apart: no amount is ever converted or added to another currency's.">
          <ul className="space-y-1 text-sm">
            {otherCurrencies.map((cur) => (
              <li key={cur} className="flex flex-wrap gap-x-4 text-slate-700">
                <span className="font-semibold">{cur}</span>
                <span>Cash {money(own(report.cash, cur)?.net ?? 0, cur)}</span>
                <span>Accrual {money(own(report.accrual, cur)?.net ?? 0, cur)}</span>
                <span>Owed {money(own(report.receivables, cur)?.total ?? 0, cur)}</span>
                <span>Gift cards {money(own(report.giftCards, cur)?.outstanding ?? 0, cur)}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
    </div>
  );
}

function FlowCard({ index, title, subtitle, icon, flow, report, money, inLabel, outLabel }: {
  index: number;
  title: string;
  subtitle: string;
  icon: React.ReactNode;
  flow: ClubMoneyFlow | undefined;
  report: ClubReport;
  money: Money;
  inLabel: string;
  outLabel: string;
}) {
  const c = report.currency;
  const max = Math.max(1, ...(flow?.byPeriod ?? []).map((p) => Math.max(p.in, p.out)));
  return (
    <Card index={index} title={title} icon={icon} note={subtitle}>
      {!flow ? <Empty /> : (
        <>
          <p className="text-sm text-slate-600 mb-3">
            {inLabel} {money(flow.in, c)}, {outLabel.toLowerCase()} {money(flow.out, c)}: <span className="font-semibold text-slate-900">{money(flow.net, c)}</span>
          </p>
          <div className="overflow-x-auto">
            <table className="w-full text-xs">
              <caption className="sr-only">{title} by {report.grain}</caption>
              <thead>
                <tr className="text-left text-slate-500">
                  <th scope="col" className="py-1 pr-2 font-medium">{report.grain === 'month' ? 'Month' : report.grain === 'week' ? 'Week' : 'Day'}</th>
                  <th scope="col" className="py-1 pr-2 font-medium text-right">{inLabel}</th>
                  <th scope="col" className="py-1 pr-2 font-medium text-right">{outLabel}</th>
                  <th scope="col" className="py-1 font-medium text-right">Net</th>
                </tr>
              </thead>
              <tbody>
                {flow.byPeriod.map((p, i) => (
                  <tr key={report.periods[i]?.start ?? i} className="border-t border-slate-100">
                    <th scope="row" className="py-1 pr-2 font-normal text-slate-700 whitespace-nowrap">{report.periods[i] ? periodLabel(report.periods[i]!, report.grain) : ''}</th>
                    <td className="py-1 pr-2 text-right">
                      <span className="inline-block h-1.5 mr-1 align-middle rounded bg-emerald-400" style={{ width: `${(p.in / max) * 40}px` }} aria-hidden="true" />
                      {money(p.in, c)}
                    </td>
                    <td className="py-1 pr-2 text-right text-rose-700">{p.out > 0 ? money(p.out, c) : '—'}</td>
                    <td className="py-1 text-right font-semibold text-slate-900">{money(p.net, c)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </Card>
  );
}

function Card({ title, icon, index, note, children }: { title: string; icon: React.ReactNode; index: number; note?: string; children: React.ReactNode }) {
  return (
    <StaggerItem index={index} className={`bg-white rounded-2xl border border-slate-200 p-5 shadow-sm hover:border-emerald-200 ${liftOnHover}`}>
      <h2 className="font-semibold text-slate-900 inline-flex items-center gap-2 mb-1">{icon} {title}</h2>
      {note && <p className="text-xs text-slate-500 mb-3">{note}</p>}
      {!note && <div className="mb-3" />}
      {children}
    </StaggerItem>
  );
}

function Bar({ share, tone }: { share: number; tone: string }) {
  return (
    <div className="h-2 bg-slate-100 rounded-full overflow-hidden">
      <div className={`h-full ${tone}`} style={{ width: `${Math.min(100, Math.max(0, share * 100))}%` }} />
    </div>
  );
}

function Empty() {
  return <p className="text-sm text-slate-500">Nothing in this period.</p>;
}

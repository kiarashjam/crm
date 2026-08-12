// Sales Tracker section on the Reports page — a faithful port of the
// P46 Sales Tracker Excel DASHBOARD.
//
// Every KPI / rate / chart below traces to a specific cell formula in the
// source workbook (annotated at the call sites in `computed.ts`). The
// underlying data is the per-lead Sales Extras store combined with the
// live Leads list, so every field the user edits on the Lead detail page
// is picked up here immediately.

import { useMemo } from 'react';
import {
  Users, Target, PhoneCall, Handshake, FileSignature, CircleDollarSign,
  XCircle, UserCheck, Clock, AlertOctagon, TrendingUp, TrendingDown,
  Calendar, Sparkles, Percent,
} from 'lucide-react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Cell,
  PieChart, Pie, Legend, Area, AreaChart,
} from 'recharts';
import type { Lead } from '@/app/api/types';
import { cn } from '@/app/components/ui/utils';
import { buildTrackedRowsFromLeads, leadHasTrackerData } from '../leads/leadTrackerMap';
import {
  parsePipeline, dropoutReasonBreakdown,
  DROPOUT_REASONS, DROPOUT_REASON_LABELS,
} from '../leads/leadPipeline';
import {
  computeKpis,
  computeRates,
  computeContractTiming,
  computeDropOff,
  computeWeeklyMeetings,
  computeMeetingPipeline,
  computeFunnel,
  computeMonthlyCumulative,
  computeContractStatusBreakdown,
  computeDropOffBreakdown,
  computeMembershipStatus,
  computeOutcomeBreakdown,
} from '../salesTracker/computed';

const PALETTE = ['#6366f1', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4', '#ec4899', '#84cc16'];
const FUNNEL_COLORS = ['#3b82f6', '#8b5cf6', '#a855f7', '#d946ef', '#f59e0b', '#10b981', '#14b8a6'];

const fmtPct = (n: number) => `${(n * 100).toFixed(1)}%`;
const fmtNum = (n: number | null | undefined) => (n == null || Number.isNaN(n) ? '—' : Math.round(n).toString());
const fmtDate = (d: Date) =>
  d.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit', year: 'numeric' });

interface Props {
  leads: Lead[];
}

/** Small reusable KPI tile shared across the section. */
function KpiTile({
  label,
  value,
  sub,
  icon: Icon,
  tone,
}: {
  label: string;
  value: string | number;
  sub?: string;
  icon: React.ElementType;
  tone: string;
}) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
      <div className="flex items-center justify-between">
        <span className="text-xs font-medium text-slate-500 uppercase tracking-wider">{label}</span>
        <span className={cn('flex h-7 w-7 items-center justify-center rounded-lg', tone)}>
          <Icon className="h-3.5 w-3.5" />
        </span>
      </div>
      <p className="mt-1.5 text-xl font-bold text-slate-900 tabular-nums">{value}</p>
      {sub && <p className="mt-0.5 text-[11px] text-slate-400">{sub}</p>}
    </div>
  );
}

function ChartCard({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm">
      <h3 className="text-sm font-semibold text-slate-700">{title}</h3>
      {subtitle && <p className="text-[11px] text-slate-400 mb-2">{subtitle}</p>}
      <div className="mt-2">{children}</div>
    </div>
  );
}

export function SalesTrackerReport({ leads }: Props) {
  const rows = useMemo(() => buildTrackedRowsFromLeads(leads), [leads]);
  // Reasons live on the lead's own pipelineState, not on the tracked-row
  // projection, so they are read straight from the leads.
  const reasonRows = useMemo(
    () => dropoutReasonBreakdown(leads.map((l) => parsePipeline(l.pipelineState))),
    [leads],
  );
  const reasonTotals = useMemo(() => ({
    total: reasonRows.reduce((n, r) => n + r.total, 0),
    missing: reasonRows.reduce((n, r) => n + r.missing, 0),
  }), [reasonRows]);
  const waitlistTotal = leads.length;
  const trackedCount = useMemo(() => leads.filter(leadHasTrackerData).length, [leads]);

  const kpis = useMemo(() => computeKpis(rows), [rows]);
  const rates = useMemo(() => computeRates(kpis, waitlistTotal), [kpis, waitlistTotal]);
  const timing = useMemo(() => computeContractTiming(rows), [rows]);
  const dropOff = useMemo(() => computeDropOff(rows), [rows]);
  const weekly = useMemo(() => computeWeeklyMeetings(rows, 10), [rows]);
  const pipeline = useMemo(() => computeMeetingPipeline(rows), [rows]);
  const funnel = useMemo(() => computeFunnel(rows), [rows]);
  const monthly = useMemo(() => computeMonthlyCumulative(rows), [rows]);
  const contractStatus = useMemo(() => computeContractStatusBreakdown(rows), [rows]);
  const dropOffBreakdown = useMemo(() => computeDropOffBreakdown(rows), [rows]);
  const membership = useMemo(() => computeMembershipStatus(rows), [rows]);
  const outcome = useMemo(() => computeOutcomeBreakdown(rows), [rows]);

  const notYetContacted = Math.max(waitlistTotal - kpis.outreachAttempts, 0);

  return (
    <div className="pt-4">
      {/* Header */}
      <div className="relative overflow-hidden rounded-2xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-6 mb-6">
        <div className="absolute -top-16 -right-16 h-56 w-56 rounded-full bg-orange-500/20 blur-3xl" />
        <div className="absolute -bottom-20 -left-16 h-48 w-48 rounded-full bg-violet-500/20 blur-3xl" />
        <div className="relative flex flex-col lg:flex-row lg:items-center lg:justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/30">
              <Sparkles className="w-7 h-7 text-white" />
            </div>
            <div>
              <h2 className="text-xl font-bold text-white tracking-tight">Lead funnel</h2>
              <p className="text-slate-400 mt-0.5 text-sm">
                Built from lead pipeline data (same stages as the P46 tracker workbook)
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2">
            <span className="rounded-full bg-white/10 border border-white/20 text-white text-xs px-3 py-1.5">
              {trackedCount} leads in tracker · {leads.length} total
            </span>
          </div>
        </div>
      </div>

      {/* Waitlist = all leads */}
      <div className="rounded-2xl border border-slate-200 bg-gradient-to-br from-white via-amber-50/30 to-orange-50/40 p-5 shadow-sm mb-6">
        <div className="mb-4">
          <p className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Leads</p>
          <h3 className="mt-0.5 text-base font-bold text-slate-900">Waitlist = all leads in this org</h3>
          <p className="text-xs text-slate-500 mt-1">
            Funnel numbers come from each lead&apos;s pipeline fields — not a separate sales spreadsheet.
          </p>
        </div>
        <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
          <KpiTile label="Waitlist" value={waitlistTotal} sub="Total leads" icon={Users} tone="bg-slate-100 text-slate-600" />
          <KpiTile label="Contacted" value={kpis.outreachAttempts} sub="Successful + attempted" icon={PhoneCall} tone="bg-blue-50 text-blue-600" />
          <KpiTile label="Not yet contacted" value={notYetContacted} sub="Waitlist − outreach" icon={Users} tone="bg-slate-100 text-slate-600" />
          <KpiTile label="Meetings held" value={kpis.meetingsHeld} sub="Scheduled = Yes" icon={Calendar} tone="bg-violet-50 text-violet-600" />
        </div>
      </div>

      {/* KPIs — key performance indicators */}
      <h3 className="flex items-center gap-2 text-lg font-bold text-slate-900 mt-2 mb-3">
        <Target className="h-5 w-5 text-orange-500" />
        Key Performance Indicators
      </h3>
      <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6 mb-6">
        <KpiTile label="Outreach attempts" value={kpis.outreachAttempts} icon={PhoneCall} tone="bg-blue-50 text-blue-600" />
        <KpiTile label="Contacted successfully" value={kpis.contactedSuccessfully} icon={UserCheck} tone="bg-emerald-50 text-emerald-600" />
        <KpiTile label="Meetings held" value={kpis.meetingsHeld} icon={Calendar} tone="bg-violet-50 text-violet-600" />
        <KpiTile label="Interested" value={kpis.interested} icon={TrendingUp} tone="bg-amber-50 text-amber-600" />
        <KpiTile label="Contracts sent" value={kpis.contractsSent} icon={Handshake} tone="bg-indigo-50 text-indigo-600" />
        <KpiTile label="Signed" value={kpis.signed} icon={FileSignature} tone="bg-teal-50 text-teal-600" />
        <KpiTile label="Deposit paid" value={kpis.depositPaid} icon={CircleDollarSign} tone="bg-emerald-50 text-emerald-600" />
        <KpiTile label="Pending signature" value={kpis.pendingSignature} icon={Clock} tone="bg-amber-50 text-amber-600" />
        <KpiTile label="Not interested" value={kpis.notInterested} icon={XCircle} tone="bg-rose-50 text-rose-600" />
        <KpiTile label="Showed up" value={kpis.showedUp} icon={UserCheck} tone="bg-emerald-50 text-emerald-600" />
        <KpiTile label="No shows" value={kpis.noShows} icon={AlertOctagon} tone="bg-rose-50 text-rose-600" />
        <KpiTile label="Overall close rate" value={fmtPct(rates.closeRate)} icon={Percent} tone="bg-emerald-50 text-emerald-600" />
      </div>

      {/* Conversion rates + Charts */}
      <div className="grid gap-6 lg:grid-cols-2 mb-6">
        <ChartCard title="Conversion rates" subtitle="Signed / stage denominators — matches Excel §Conversion Rates">
          <div className="space-y-3 mt-1">
            {[
              { label: 'Contacted / Waitlist', value: rates.contactedOverWaitlist, sub: '% of waitlist contacted', tone: 'bg-blue-500' },
              { label: 'Meeting rate', value: rates.meetingRate, sub: 'Meetings held / contacted', tone: 'bg-violet-500' },
              { label: 'Interest rate', value: rates.interestRate, sub: 'Interested / meetings held', tone: 'bg-amber-500' },
              { label: 'Contract rate', value: rates.contractRate, sub: 'Contracts sent / interested', tone: 'bg-indigo-500' },
              { label: 'Signature rate', value: rates.signatureRate, sub: 'Signed / contracts sent', tone: 'bg-teal-500' },
              { label: 'Overall close rate', value: rates.closeRate, sub: 'Signed / contacted', tone: 'bg-emerald-500' },
            ].map((r) => (
              <div key={r.label} className="space-y-1">
                <div className="flex items-center justify-between text-sm">
                  <div>
                    <p className="font-medium text-slate-700">{r.label}</p>
                    <p className="text-[11px] text-slate-400">{r.sub}</p>
                  </div>
                  <span className="font-bold text-slate-900 tabular-nums">{fmtPct(r.value)}</span>
                </div>
                <div className="h-2 rounded-full bg-slate-100 overflow-hidden">
                  <div
                    className={cn('h-full rounded-full', r.tone)}
                    style={{ width: `${Math.min(100, r.value * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </ChartCard>

        <ChartCard title="Sales funnel" subtitle="Contacted → Signed">
          <ResponsiveContainer width="100%" height={280}>
            <BarChart data={funnel} layout="vertical" margin={{ top: 4, right: 28, left: 20, bottom: 0 }}>
              <XAxis type="number" allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
              <YAxis type="category" dataKey="name" width={110} tick={{ fontSize: 11, fill: '#64748b' }} />
              <Tooltip cursor={{ fill: '#f8fafc' }} />
              <Bar dataKey="value" radius={[0, 6, 6, 0]} maxBarSize={28}>
                {funnel.map((_, i) => <Cell key={i} fill={FUNNEL_COLORS[i % FUNNEL_COLORS.length]} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </ChartCard>
      </div>

      {/* Contract timing */}
      <ChartCard title="Contract timing" subtitle="Signed = signature − sent; Outstanding = today − sent">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-5 mt-1">
          <KpiTile label="Avg days to sign" value={fmtNum(timing.avgDaysToSign)} sub="Signed contracts" icon={TrendingUp} tone="bg-emerald-50 text-emerald-600" />
          <KpiTile label="Avg days outstanding" value={fmtNum(timing.avgDaysOutstanding)} sub="Pending contracts" icon={Clock} tone="bg-amber-50 text-amber-600" />
          <KpiTile label="Max days outstanding" value={fmtNum(timing.maxDaysOutstanding)} sub="Oldest pending" icon={AlertOctagon} tone="bg-rose-50 text-rose-600" />
          <KpiTile label="Outstanding > 30 days" value={timing.outstandingOver30} sub="Flagged overdue" icon={AlertOctagon} tone="bg-rose-50 text-rose-600" />
          <KpiTile label="Outstanding > 14 days" value={timing.outstandingOver14} sub="Approaching overdue" icon={Clock} tone="bg-amber-50 text-amber-600" />
        </div>
        <p className="mt-3 text-[11px] text-slate-400 leading-relaxed">
          ℹ️ Timing metrics require Contract Sent Date and Signature Date on the Lead detail. Leads with neither are ignored.
        </p>
      </ChartCard>

      {/* Drop-off + Charts */}
      <div className="grid gap-6 lg:grid-cols-2 mt-6">
        <ChartCard title="Drop-off analysis" subtitle="Where leads leave the funnel">
          <div className="grid gap-3 sm:grid-cols-2 mt-1">
            <KpiTile label="Declined after meeting" value={dropOff.declinedAfterMeeting} sub={`${fmtPct(dropOff.declinedAfterMeetingPct)} of meetings held`} icon={TrendingDown} tone="bg-rose-50 text-rose-600" />
            <KpiTile label="Declined after contract" value={dropOff.declinedAfterContract} sub={`${fmtPct(dropOff.declinedAfterContractPct)} of contracts sent`} icon={TrendingDown} tone="bg-rose-50 text-rose-600" />
            <KpiTile label="Not interested in meeting" value={dropOff.notInterestedInMeeting} sub="Never met" icon={XCircle} tone="bg-amber-50 text-amber-600" />
            <KpiTile label="No-show at meeting" value={dropOff.noShowAtMeeting} sub="Missed scheduled meeting" icon={AlertOctagon} tone="bg-amber-50 text-amber-600" />
            <KpiTile label="Rejected / no longer interested" value={dropOff.rejectedOrNoLongerInterested} sub="Late-stage decline" icon={XCircle} tone="bg-rose-50 text-rose-600" />
          </div>
        </ChartCard>

        <ChartCard title="Drop-off breakdown" subtitle="Share of each drop-off reason">
          {dropOffBreakdown.every((r) => r.value === 0) ? (
            <div className="flex h-[260px] items-center justify-center text-sm text-slate-400">No drop-offs yet</div>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <PieChart>
                <Pie data={dropOffBreakdown} dataKey="value" nameKey="name" outerRadius={100} label={(e) => (e.value ? `${e.name}: ${e.value}` : '')}>
                  {dropOffBreakdown.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                </Pie>
                <Tooltip />
              </PieChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* Weekly meetings */}
      <div className="mt-6">
        <ChartCard title="Weekly meetings" subtitle="Last 10 weeks — meetings held, contracts sent, and signed by meeting week">
          <ResponsiveContainer width="100%" height={300}>
            <BarChart data={weekly.map((w) => ({
              name: `W${w.weekNumber}`,
              starting: fmtDate(w.weekStarting),
              meetingsHeld: w.meetingsHeld,
              contractsSent: w.contractsSent,
              signed: w.signed,
            }))} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
              <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
              <Tooltip
                cursor={{ fill: '#f8fafc' }}
                labelFormatter={(_l, payload) => {
                  const first = payload?.[0]?.payload as { starting?: string } | undefined;
                  return first?.starting ? `Week starting ${first.starting}` : String(_l);
                }}
              />
              <Legend iconType="circle" wrapperStyle={{ fontSize: 12 }} />
              <Bar dataKey="meetingsHeld" name="Meetings held" fill="#6366f1" radius={[4, 4, 0, 0]} maxBarSize={22} />
              <Bar dataKey="contractsSent" name="Contracts sent" fill="#f59e0b" radius={[4, 4, 0, 0]} maxBarSize={22} />
              <Bar dataKey="signed" name="Signed" fill="#10b981" radius={[4, 4, 0, 0]} maxBarSize={22} />
            </BarChart>
          </ResponsiveContainer>
          <p className="mt-2 text-[11px] text-slate-400 leading-relaxed">
            ℹ️ Weekly contracts sent &amp; signed use the meeting-date week as a proxy — mirrors the Excel note about column J.
          </p>
        </ChartCard>
      </div>

      {/* Meeting pipeline status */}
      <div className="mt-6">
        <ChartCard title="Meeting pipeline status" subtitle="In-progress items still to act on">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4 mt-1">
            <KpiTile label="Not interested in meeting" value={pipeline.notInterestedInMeeting} icon={XCircle} tone="bg-rose-50 text-rose-600" />
            <KpiTile label="Meeting to be scheduled" value={pipeline.meetingToBeScheduled} icon={Calendar} tone="bg-blue-50 text-blue-600" />
            <KpiTile label="Contracts to send" value={pipeline.contractsToSend} icon={Handshake} tone="bg-amber-50 text-amber-600" />
            <KpiTile label="Meeting refusal rate" value={fmtPct(pipeline.meetingRefusalRate)} sub="Not interested / contacted" icon={Percent} tone="bg-slate-100 text-slate-600" />
          </div>
        </ChartCard>
      </div>

      {/* Visual analytics — cumulative + status breakdowns */}
      <div className="grid gap-6 lg:grid-cols-2 mt-6">
        <ChartCard title="Cumulative contacted vs signed" subtitle="By month (calendar year)">
          <ResponsiveContainer width="100%" height={280}>
            <AreaChart data={monthly} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
              <defs>
                <linearGradient id="contactedGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#6366f1" stopOpacity={0.35} />
                  <stop offset="100%" stopColor="#6366f1" stopOpacity={0} />
                </linearGradient>
                <linearGradient id="signedGrad" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#10b981" stopOpacity={0.35} />
                  <stop offset="100%" stopColor="#10b981" stopOpacity={0} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
              <XAxis dataKey="month" tick={{ fontSize: 11, fill: '#94a3b8' }} />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
              <Tooltip />
              <Legend iconType="circle" wrapperStyle={{ fontSize: 12 }} />
              <Area type="monotone" dataKey="cumulativeContacted" name="Contacted" stroke="#6366f1" strokeWidth={2.5} fill="url(#contactedGrad)" />
              <Area type="monotone" dataKey="cumulativeSigned" name="Signed" stroke="#10b981" strokeWidth={2.5} fill="url(#signedGrad)" />
            </AreaChart>
          </ResponsiveContainer>
        </ChartCard>

        <ChartCard title="Contract status" subtitle="Signed vs pending vs to-be-sent vs declined">
          {contractStatus.every((r) => r.value === 0) ? (
            <div className="flex h-[280px] items-center justify-center text-sm text-slate-400">No contracts yet</div>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <BarChart data={contractStatus} margin={{ top: 8, right: 16, left: 0, bottom: 0 }}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f1f5f9" />
                <XAxis dataKey="name" tick={{ fontSize: 11, fill: '#94a3b8' }} />
                <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: '#94a3b8' }} />
                <Tooltip cursor={{ fill: '#f8fafc' }} />
                <Bar dataKey="value" radius={[6, 6, 0, 0]} maxBarSize={56}>
                  {contractStatus.map((_, i) => <Cell key={i} fill={PALETTE[i % PALETTE.length]} />)}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Membership status" subtitle="Where paid vs signed vs pending">
          {membership.every((r) => r.value === 0) ? (
            <div className="flex h-[280px] items-center justify-center text-sm text-slate-400">No members yet</div>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <PieChart>
                <Pie data={membership} dataKey="value" nameKey="name" innerRadius={60} outerRadius={100} paddingAngle={3}>
                  {membership.map((_, i) => <Cell key={i} fill={PALETTE[(i + 2) % PALETTE.length]} />)}
                </Pie>
                <Tooltip />
                <Legend iconType="circle" wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </ChartCard>

        <ChartCard title="Post-meeting outcome" subtitle="Interested / Not / Pending — Excel §Outcome">
          {outcome.every((r) => r.value === 0) ? (
            <div className="flex h-[280px] items-center justify-center text-sm text-slate-400">No meetings yet</div>
          ) : (
            <ResponsiveContainer width="100%" height={280}>
              <PieChart>
                <Pie data={outcome} dataKey="value" nameKey="name" innerRadius={60} outerRadius={100} paddingAngle={3}>
                  <Cell fill="#10b981" />
                  <Cell fill="#ef4444" />
                  <Cell fill="#f59e0b" />
                </Pie>
                <Tooltip />
                <Legend iconType="circle" wrapperStyle={{ fontSize: 12 }} />
              </PieChart>
            </ResponsiveContainer>
          )}
        </ChartCard>
      </div>

      {/* Weekly table (numeric detail alongside the chart) */}
      <div className="mt-6">
        <ChartCard title="Weekly meetings table" subtitle="Same data as the chart above, in tabular form">
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-slate-200 text-left text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                  <th className="py-2 pr-3">Week #</th>
                  <th className="py-2 pr-3">Week starting</th>
                  <th className="py-2 pr-3 text-right">Meetings held</th>
                  <th className="py-2 pr-3 text-right">Contracts sent</th>
                  <th className="py-2 pr-3 text-right">Signed</th>
                </tr>
              </thead>
              <tbody>
                {weekly.map((w) => (
                  <tr key={`${w.weekNumber}-${w.weekStarting.toISOString()}`} className="border-b border-slate-100 last:border-b-0">
                    <td className="py-2 pr-3 font-semibold text-slate-800">W{w.weekNumber}</td>
                    <td className="py-2 pr-3 text-slate-600">{fmtDate(w.weekStarting)}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{w.meetingsHeld}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{w.contractsSent}</td>
                    <td className="py-2 pr-3 text-right tabular-nums">{w.signed}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </ChartCard>

        {/* Drop-off reasons, broken down by the stage they were recorded at. */}
        <ChartCard
          title="Why they did not proceed"
          subtitle="Reason by the phase it was logged at — Meeting vs Contract"
        >
          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead>
                <tr className="border-b border-slate-200 text-[10px] uppercase tracking-wider text-slate-400">
                  <th className="py-2 pr-3 font-semibold">Phase</th>
                  {DROPOUT_REASONS.map((r) => (
                    <th key={r} className="py-2 pr-3 text-right font-semibold">{DROPOUT_REASON_LABELS[r]}</th>
                  ))}
                  <th className="py-2 pr-3 text-right font-semibold">No reason yet</th>
                  <th className="py-2 text-right font-semibold">Total</th>
                </tr>
              </thead>
              <tbody>
                {reasonRows.map((row) => (
                  <tr key={row.phase} className="border-b border-slate-100 last:border-b-0">
                    <td className="py-2 pr-3 font-semibold text-slate-800">
                      {row.phaseTitle}
                      <span className="ml-1 font-normal text-slate-400">· phase {row.phase}</span>
                    </td>
                    {DROPOUT_REASONS.map((r) => (
                      <td key={r} className="py-2 pr-3 text-right tabular-nums text-slate-600">
                        {row.counts[r] || '—'}
                      </td>
                    ))}
                    <td className={cn(
                      'py-2 pr-3 text-right font-semibold tabular-nums',
                      row.missing > 0 ? 'text-rose-600' : 'text-slate-300',
                    )}>
                      {row.missing || '—'}
                    </td>
                    <td className="py-2 text-right font-bold tabular-nums text-slate-800">{row.total}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {reasonTotals.missing > 0 && (
            <p className="mt-2 text-[11px] text-rose-600">
              {reasonTotals.missing} of {reasonTotals.total} drop-outs have no reason recorded yet.
            </p>
          )}
        </ChartCard>
      </div>
    </div>
  );
}

export default SalesTrackerReport;

// Collapsible Sales Tracker dashboard shown at the top of the Leads page.
//
// Compact-first: shows a strip of tiles, a mini-funnel, and conversion
// rates by default. Expandable to reveal the weekly meetings chart, the
// monthly cumulative area chart, and drop-off analytics. Everything here
// is a strict subset of the Reports page's SalesTrackerReport with the
// same underlying computation, so the two views can never disagree.

import { useEffect, useMemo, useState } from 'react';
import {
  ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Cell,
  AreaChart, Area, Legend, PieChart, Pie,
} from 'recharts';
import {
  Sparkles, ChevronDown, ChevronUp, Target, Percent, Clock,
  Users, PhoneCall, Calendar, Handshake, FileSignature, CircleDollarSign,
  AlertOctagon, TrendingUp, TrendingDown, ExternalLink, XCircle,
  Save, RotateCcw, ChevronsUpDown,
} from 'lucide-react';
import type { Lead } from '@/app/api/types';
import { Link } from 'react-router-dom';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { cn } from '@/app/components/ui/utils';
import {
  loadAllSalesExtras,
  loadWaitlistTotal,
  saveWaitlistTotal,
  onSalesExtrasChange,
  EMPTY_SALES_EXTRAS,
  type SalesExtras,
} from '../salesExtrasStore';
import {
  computeKpis, computeRates, computeContractTiming, computeDropOff,
  computeWeeklyMeetings, computeMeetingPipeline, computeFunnel,
  computeMonthlyCumulative, computeContractStatusBreakdown,
  computeDropOffBreakdown, computeMembershipStatus,
  type TrackedRow,
} from '../../salesTracker/computed';

const FUNNEL_COLORS = ['#3b82f6', '#8b5cf6', '#a855f7', '#d946ef', '#f59e0b', '#10b981', '#14b8a6'];
const PIE_COLORS = ['#6366f1', '#14b8a6', '#f59e0b', '#ef4444', '#8b5cf6', '#06b6d4'];

const fmtPct = (n: number) => `${(n * 100).toFixed(1)}%`;
const fmtNum = (n: number | null | undefined) => (n == null || Number.isNaN(n) ? '—' : Math.round(n).toString());
const fmtDate = (d: Date) =>
  d.toLocaleDateString(undefined, { day: '2-digit', month: '2-digit', year: 'numeric' });

interface Props {
  leads: Lead[];
}

/** Merges Lead[] with per-lead extras into TrackedRow[]. */
function buildRows(leads: Lead[], extrasMap: Record<string, SalesExtras>): TrackedRow[] {
  return leads.map((l) => ({ ...EMPTY_SALES_EXTRAS, ...(extrasMap[l.id] ?? {}) }));
}

/** Tone-styled compact KPI tile. Tighter than the Reports tile so the strip fits 6-8 on desktop. */
function CompactTile({
  label, value, sub, icon: Icon, tone,
}: {
  label: string;
  value: string | number;
  sub?: string;
  icon: React.ElementType;
  tone: string;
}) {
  return (
    <div className="group relative overflow-hidden rounded-xl border border-white/10 bg-white/[0.06] backdrop-blur-md p-3 hover:bg-white/[0.10] transition-colors">
      <div className={cn('absolute top-0 right-0 h-12 w-12 rounded-bl-3xl opacity-60 group-hover:opacity-100 transition-opacity', tone)} />
      <div className="relative">
        <span className="flex h-6 w-6 items-center justify-center rounded-lg bg-white/10">
          <Icon className="h-3.5 w-3.5 text-white/80" />
        </span>
        <p className="mt-1.5 text-[10px] uppercase tracking-wider text-white/60 font-semibold truncate">{label}</p>
        <p className="mt-0.5 text-lg font-bold text-white tabular-nums">{value}</p>
        {sub && <p className="text-[10px] text-white/40 truncate">{sub}</p>}
      </div>
    </div>
  );
}

function RateBar({
  label, value, sub, colorClass,
}: { label: string; value: number; sub?: string; colorClass: string }) {
  return (
    <div>
      <div className="flex items-baseline justify-between gap-2">
        <div className="min-w-0">
          <p className="text-xs font-medium text-white/90 truncate">{label}</p>
          {sub && <p className="text-[10px] text-white/50 truncate">{sub}</p>}
        </div>
        <span className="text-sm font-bold text-white tabular-nums">{fmtPct(value)}</span>
      </div>
      <div className="mt-1 h-1.5 rounded-full bg-white/10 overflow-hidden">
        <div
          className={cn('h-full rounded-full transition-all duration-500', colorClass)}
          style={{ width: `${Math.min(100, value * 100)}%` }}
        />
      </div>
    </div>
  );
}

export function SalesTrackerPanel({ leads }: Props) {
  const [extrasMap, setExtrasMap] = useState<Record<string, SalesExtras>>(() => loadAllSalesExtras());
  const [waitlistTotal, setWaitlistTotal] = useState<number>(() => loadWaitlistTotal());
  const [waitlistDraft, setWaitlistDraft] = useState<string>(() => String(loadWaitlistTotal() || ''));
  const [collapsed, setCollapsed] = useState<boolean>(() => {
    try {
      return localStorage.getItem('crm.salesTracker.panel.collapsed') === '1';
    } catch { return false; }
  });
  const [expanded, setExpanded] = useState<boolean>(() => {
    try {
      return localStorage.getItem('crm.salesTracker.panel.expanded') === '1';
    } catch { return false; }
  });

  // Keep dashboard live when the Lead detail editor saves.
  useEffect(() => onSalesExtrasChange(() => {
    setExtrasMap(loadAllSalesExtras());
    const total = loadWaitlistTotal();
    setWaitlistTotal(total);
    setWaitlistDraft(String(total || ''));
  }), []);

  useEffect(() => {
    try { localStorage.setItem('crm.salesTracker.panel.collapsed', collapsed ? '1' : '0'); } catch { /* ignore */ }
  }, [collapsed]);
  useEffect(() => {
    try { localStorage.setItem('crm.salesTracker.panel.expanded', expanded ? '1' : '0'); } catch { /* ignore */ }
  }, [expanded]);

  const rows = useMemo(() => buildRows(leads, extrasMap), [leads, extrasMap]);
  const trackedCount = useMemo(
    () => rows.filter((r) => Object.values(r).some((v) => v && String(v).trim() !== '')).length,
    [rows],
  );

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

  const commitWaitlist = () => {
    const n = Number((waitlistDraft || '').replace(/[^0-9]/g, ''));
    saveWaitlistTotal(Number.isFinite(n) ? n : 0);
    setWaitlistTotal(Number.isFinite(n) ? n : 0);
  };

  const notYetContacted = Math.max((waitlistTotal || 0) - kpis.outreachAttempts, 0);

  // Collapsed pill view (opt-in tiny footprint).
  if (collapsed) {
    return (
      <div className="mb-6 rounded-2xl border border-slate-200/70 bg-white/70 backdrop-blur-md p-3 shadow-sm flex items-center gap-3">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-gradient-to-br from-orange-500 to-amber-500 text-white shadow-sm">
          <Sparkles className="w-4 h-4" />
        </div>
        <div className="flex-1 min-w-0">
          <p className="text-sm font-semibold text-slate-800 truncate">Sales Tracker</p>
          <p className="text-[11px] text-slate-500 truncate">
            {trackedCount} tracked · {kpis.contactedSuccessfully} contacted · {kpis.signed} signed · {fmtPct(rates.closeRate)} close rate
          </p>
        </div>
        <Button
          size="sm"
          variant="outline"
          onClick={() => setCollapsed(false)}
          className="h-8 rounded-lg gap-1 text-xs border-slate-200"
        >
          <ChevronDown className="w-3.5 h-3.5" />
          Expand
        </Button>
      </div>
    );
  }

  return (
    <div className="mb-6 relative overflow-hidden rounded-2xl bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 shadow-xl">
      {/* Decorative blur elements */}
      <div className="absolute -top-16 -right-16 h-56 w-56 rounded-full bg-orange-500/20 blur-3xl" />
      <div className="absolute -bottom-20 -left-16 h-48 w-48 rounded-full bg-violet-500/20 blur-3xl" />
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 h-72 w-72 rounded-full bg-blue-500/10 blur-3xl" />

      {/* Header */}
      <div className="relative px-5 py-4 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 border-b border-white/10">
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 shadow-lg shadow-orange-500/30">
            <Sparkles className="w-5 h-5 text-white" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <h2 className="text-base font-bold text-white truncate">Sales Tracker</h2>
              <span className="rounded-full bg-white/10 border border-white/20 text-white text-[10px] px-2 py-0.5 font-semibold">
                Excel parity
              </span>
            </div>
            <p className="text-[11px] text-white/60 mt-0.5 truncate">
              {trackedCount} of {leads.length} leads · {kpis.outreachAttempts} outreach · {kpis.signed} signed · Same formulas as the P46 workbook
            </p>
          </div>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <Button
            size="sm"
            variant="outline"
            onClick={() => setExpanded((x) => !x)}
            className="h-8 rounded-lg gap-1 text-xs bg-white/5 border-white/20 text-white hover:bg-white/10"
          >
            <ChevronsUpDown className="w-3.5 h-3.5" />
            {expanded ? 'Less' : 'More'}
          </Button>
          <Link
            to="/reports"
            className="inline-flex items-center gap-1 h-8 px-3 rounded-lg text-xs bg-white/5 border border-white/20 text-white hover:bg-white/10 font-medium"
          >
            <ExternalLink className="w-3.5 h-3.5" />
            Full report
          </Link>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setCollapsed(true)}
            className="h-8 w-8 p-0 rounded-lg text-white/70 hover:text-white hover:bg-white/10"
            title="Collapse"
          >
            <ChevronUp className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Waitlist input */}
      <div className="relative px-5 py-3 border-b border-white/10 flex flex-col sm:flex-row sm:items-center gap-3">
        <div className="flex-1 min-w-0">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-white/50">Inputs · Cell F7 of the Excel</p>
          <p className="text-sm font-medium text-white">Total waitlist size</p>
        </div>
        <div className="flex items-center gap-2 shrink-0">
          <Input
            type="number"
            min={0}
            value={waitlistDraft}
            onChange={(e) => setWaitlistDraft(e.target.value)}
            placeholder="e.g. 377"
            className="h-9 w-32 rounded-lg bg-white/10 border-white/20 text-white text-sm placeholder:text-white/40"
          />
          <Button
            size="sm"
            onClick={commitWaitlist}
            disabled={String(waitlistTotal) === waitlistDraft || (!waitlistDraft && waitlistTotal === 0)}
            className="h-9 px-3 rounded-lg bg-amber-500 hover:bg-amber-400 text-white font-medium shadow-md shadow-orange-900/30"
          >
            <Save className="w-3.5 h-3.5 mr-1" /> Save
          </Button>
          <Button
            size="sm"
            variant="ghost"
            onClick={() => setWaitlistDraft(String(waitlistTotal || ''))}
            className="h-9 w-9 p-0 rounded-lg text-white/60 hover:text-white hover:bg-white/10"
          >
            <RotateCcw className="w-3.5 h-3.5" />
          </Button>
        </div>
      </div>

      {/* KPI grid */}
      <div className="relative px-5 py-4">
        <div className="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-2">
          <CompactTile label="Waitlist" value={waitlistTotal || 0} sub={`${notYetContacted} not yet contacted`} icon={Users} tone="bg-slate-500/30" />
          <CompactTile label="Contacted" value={kpis.contactedSuccessfully} sub={`+${kpis.outreachAttempts - kpis.contactedSuccessfully} attempted`} icon={PhoneCall} tone="bg-blue-500/30" />
          <CompactTile label="Meetings held" value={kpis.meetingsHeld} sub={`${kpis.showedUp} showed up`} icon={Calendar} tone="bg-violet-500/30" />
          <CompactTile label="Interested" value={kpis.interested} sub={`${kpis.noShows} no-shows`} icon={TrendingUp} tone="bg-amber-500/30" />
          <CompactTile label="Contracts sent" value={kpis.contractsSent} sub={`${kpis.pendingSignature} pending sig.`} icon={Handshake} tone="bg-indigo-500/30" />
          <CompactTile label="Signed" value={kpis.signed} sub={`${kpis.depositPaid} deposit paid`} icon={FileSignature} tone="bg-emerald-500/30" />
        </div>

        {/* Conversion rates + funnel side-by-side */}
        <div className="mt-4 grid grid-cols-1 lg:grid-cols-2 gap-4">
          {/* Rates */}
          <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
            <div className="flex items-center gap-2 mb-3">
              <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                <Percent className="w-3.5 h-3.5 text-white/80" />
              </div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Conversion rates</h3>
            </div>
            <div className="space-y-2.5">
              <RateBar label="Contacted / Waitlist" value={rates.contactedOverWaitlist} sub="% of waitlist contacted" colorClass="bg-gradient-to-r from-blue-500 to-cyan-400" />
              <RateBar label="Meeting rate" value={rates.meetingRate} sub="Meetings held / contacted" colorClass="bg-gradient-to-r from-violet-500 to-purple-400" />
              <RateBar label="Interest rate" value={rates.interestRate} sub="Interested / showed up" colorClass="bg-gradient-to-r from-amber-500 to-orange-400" />
              <RateBar label="Contract rate" value={rates.contractRate} sub="Contracts sent / interested" colorClass="bg-gradient-to-r from-indigo-500 to-violet-400" />
              <RateBar label="Signature rate" value={rates.signatureRate} sub="Signed / contracts sent" colorClass="bg-gradient-to-r from-teal-500 to-emerald-400" />
              <RateBar label="Overall close rate" value={rates.closeRate} sub="Signed / contacted" colorClass="bg-gradient-to-r from-emerald-500 to-green-400" />
            </div>
          </div>

          {/* Funnel */}
          <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
            <div className="flex items-center gap-2 mb-3">
              <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                <Target className="w-3.5 h-3.5 text-white/80" />
              </div>
              <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Sales funnel</h3>
            </div>
            <ResponsiveContainer width="100%" height={230}>
              <BarChart data={funnel} layout="vertical" margin={{ top: 4, right: 24, left: 8, bottom: 0 }}>
                <XAxis type="number" allowDecimals={false} tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                <YAxis type="category" dataKey="name" width={98} tick={{ fontSize: 10, fill: '#e2e8f0' }} axisLine={false} tickLine={false} />
                <Tooltip
                  cursor={{ fill: 'rgba(255,255,255,0.05)' }}
                  contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }}
                />
                <Bar dataKey="value" radius={[0, 6, 6, 0]} maxBarSize={22} label={{ position: 'right', fill: '#e2e8f0', fontSize: 11 }}>
                  {funnel.map((_, i) => <Cell key={i} fill={FUNNEL_COLORS[i % FUNNEL_COLORS.length]} />)}
                </Bar>
              </BarChart>
            </ResponsiveContainer>
          </div>
        </div>

        {/* Expanded — more analytics */}
        {expanded && (
          <div className="mt-4 space-y-4 animate-in fade-in-50 slide-in-from-top-2 duration-200">
            {/* Timing + drop-off + pipeline row */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <Clock className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Contract timing</h3>
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <MiniStat label="Avg to sign" value={fmtNum(timing.avgDaysToSign)} suffix="days" />
                  <MiniStat label="Avg outstanding" value={fmtNum(timing.avgDaysOutstanding)} suffix="days" />
                  <MiniStat label="Max outstanding" value={fmtNum(timing.maxDaysOutstanding)} suffix="days" tone="text-amber-300" />
                  <MiniStat label="Over 30 days" value={String(timing.outstandingOver30)} tone={timing.outstandingOver30 > 0 ? 'text-rose-300' : 'text-white'} />
                  <MiniStat label="Over 14 days" value={String(timing.outstandingOver14)} tone={timing.outstandingOver14 > 0 ? 'text-amber-300' : 'text-white'} />
                </div>
              </div>

              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <TrendingDown className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Drop-off</h3>
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <MiniStat label="After mtg" value={String(dropOff.declinedAfterMeeting)} sub={fmtPct(dropOff.declinedAfterMeetingPct)} />
                  <MiniStat label="After contract" value={String(dropOff.declinedAfterContract)} sub={fmtPct(dropOff.declinedAfterContractPct)} />
                  <MiniStat label="Not int. in mtg" value={String(dropOff.notInterestedInMeeting)} />
                  <MiniStat label="No-shows" value={String(dropOff.noShowAtMeeting)} />
                  <MiniStat label="Rejected" value={String(dropOff.rejectedOrNoLongerInterested)} />
                </div>
              </div>

              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <AlertOctagon className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Meeting pipeline</h3>
                </div>
                <div className="grid grid-cols-2 gap-2">
                  <MiniStat label="Not int. in mtg" value={String(pipeline.notInterestedInMeeting)} />
                  <MiniStat label="To schedule" value={String(pipeline.meetingToBeScheduled)} />
                  <MiniStat label="Contracts to send" value={String(pipeline.contractsToSend)} tone={pipeline.contractsToSend > 0 ? 'text-amber-300' : 'text-white'} />
                  <MiniStat label="Refusal rate" value={fmtPct(pipeline.meetingRefusalRate)} />
                </div>
              </div>
            </div>

            {/* Weekly meetings chart */}
            <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
              <div className="flex items-center gap-2 mb-3">
                <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                  <Calendar className="w-3.5 h-3.5 text-white/80" />
                </div>
                <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Weekly meetings — last 10 weeks</h3>
              </div>
              <ResponsiveContainer width="100%" height={230}>
                <BarChart
                  data={weekly.map((w) => ({
                    name: `W${w.weekNumber}`,
                    starting: fmtDate(w.weekStarting),
                    meetings: w.meetingsHeld,
                    contracts: w.contractsSent,
                    signed: w.signed,
                  }))}
                  margin={{ top: 4, right: 12, left: 0, bottom: 0 }}
                >
                  <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.08)" />
                  <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                  <YAxis allowDecimals={false} tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                  <Tooltip
                    cursor={{ fill: 'rgba(255,255,255,0.05)' }}
                    contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }}
                    labelFormatter={(_l, payload) => {
                      const first = payload?.[0]?.payload as { starting?: string } | undefined;
                      return first?.starting ? `Week starting ${first.starting}` : String(_l);
                    }}
                  />
                  <Legend iconType="circle" wrapperStyle={{ fontSize: 11, color: '#e2e8f0' }} />
                  <Bar dataKey="meetings" name="Meetings" fill="#8b5cf6" radius={[3, 3, 0, 0]} maxBarSize={18} />
                  <Bar dataKey="contracts" name="Contracts sent" fill="#f59e0b" radius={[3, 3, 0, 0]} maxBarSize={18} />
                  <Bar dataKey="signed" name="Signed" fill="#10b981" radius={[3, 3, 0, 0]} maxBarSize={18} />
                </BarChart>
              </ResponsiveContainer>
            </div>

            {/* Monthly cumulative + Contract status + Membership + Drop-off pie */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <TrendingUp className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Cumulative — contacted vs signed</h3>
                </div>
                <ResponsiveContainer width="100%" height={200}>
                  <AreaChart data={monthly} margin={{ top: 4, right: 12, left: 0, bottom: 0 }}>
                    <defs>
                      <linearGradient id="stpanel-contactedGrad" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#6366f1" stopOpacity={0.5} />
                        <stop offset="100%" stopColor="#6366f1" stopOpacity={0} />
                      </linearGradient>
                      <linearGradient id="stpanel-signedGrad" x1="0" y1="0" x2="0" y2="1">
                        <stop offset="0%" stopColor="#10b981" stopOpacity={0.5} />
                        <stop offset="100%" stopColor="#10b981" stopOpacity={0} />
                      </linearGradient>
                    </defs>
                    <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.08)" />
                    <XAxis dataKey="month" tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                    <YAxis allowDecimals={false} tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                    <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }} />
                    <Legend iconType="circle" wrapperStyle={{ fontSize: 11, color: '#e2e8f0' }} />
                    <Area type="monotone" dataKey="cumulativeContacted" name="Contacted" stroke="#6366f1" strokeWidth={2} fill="url(#stpanel-contactedGrad)" />
                    <Area type="monotone" dataKey="cumulativeSigned" name="Signed" stroke="#10b981" strokeWidth={2} fill="url(#stpanel-signedGrad)" />
                  </AreaChart>
                </ResponsiveContainer>
              </div>

              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <FileSignature className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Contract status</h3>
                </div>
                {contractStatus.every((r) => r.value === 0) ? (
                  <div className="flex h-[200px] items-center justify-center text-xs text-white/40">No contracts yet</div>
                ) : (
                  <ResponsiveContainer width="100%" height={200}>
                    <BarChart data={contractStatus} margin={{ top: 4, right: 12, left: 0, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="rgba(255,255,255,0.08)" />
                      <XAxis dataKey="name" tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                      <YAxis allowDecimals={false} tick={{ fontSize: 10, fill: '#cbd5e1' }} axisLine={false} tickLine={false} />
                      <Tooltip cursor={{ fill: 'rgba(255,255,255,0.05)' }} contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }} />
                      <Bar dataKey="value" radius={[4, 4, 0, 0]} maxBarSize={48}>
                        {contractStatus.map((_, i) => <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />)}
                      </Bar>
                    </BarChart>
                  </ResponsiveContainer>
                )}
              </div>

              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <CircleDollarSign className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Membership status</h3>
                </div>
                {membership.every((r) => r.value === 0) ? (
                  <div className="flex h-[200px] items-center justify-center text-xs text-white/40">No members yet</div>
                ) : (
                  <ResponsiveContainer width="100%" height={200}>
                    <PieChart>
                      <Pie data={membership} dataKey="value" nameKey="name" innerRadius={48} outerRadius={80} paddingAngle={3}>
                        {membership.map((_, i) => <Cell key={i} fill={PIE_COLORS[(i + 2) % PIE_COLORS.length]} />)}
                      </Pie>
                      <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }} />
                      <Legend iconType="circle" wrapperStyle={{ fontSize: 11, color: '#e2e8f0' }} />
                    </PieChart>
                  </ResponsiveContainer>
                )}
              </div>

              <div className="rounded-xl border border-white/10 bg-white/[0.04] backdrop-blur-md p-4">
                <div className="flex items-center gap-2 mb-3">
                  <div className="flex h-6 w-6 items-center justify-center rounded-md bg-white/10">
                    <XCircle className="w-3.5 h-3.5 text-white/80" />
                  </div>
                  <h3 className="text-xs font-bold uppercase tracking-wider text-white/80">Drop-off breakdown</h3>
                </div>
                {dropOffBreakdown.every((r) => r.value === 0) ? (
                  <div className="flex h-[200px] items-center justify-center text-xs text-white/40">No drop-offs yet</div>
                ) : (
                  <ResponsiveContainer width="100%" height={200}>
                    <PieChart>
                      <Pie data={dropOffBreakdown} dataKey="value" nameKey="name" outerRadius={80} label={(e) => (e.value ? `${e.value}` : '')}>
                        {dropOffBreakdown.map((_, i) => <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />)}
                      </Pie>
                      <Tooltip contentStyle={{ background: '#0f172a', border: '1px solid rgba(255,255,255,0.15)', borderRadius: 8, color: '#e2e8f0' }} />
                      <Legend iconType="circle" wrapperStyle={{ fontSize: 11, color: '#e2e8f0' }} />
                    </PieChart>
                  </ResponsiveContainer>
                )}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

function MiniStat({ label, value, sub, suffix, tone }: { label: string; value: string; sub?: string; suffix?: string; tone?: string }) {
  return (
    <div className="rounded-lg border border-white/10 bg-white/5 px-2.5 py-2">
      <p className="text-[9px] font-semibold uppercase tracking-wider text-white/50 truncate">{label}</p>
      <p className={cn('text-base font-bold tabular-nums', tone ?? 'text-white')}>
        {value}
        {suffix && <span className="text-[10px] text-white/50 ml-1 font-medium">{suffix}</span>}
      </p>
      {sub && <p className="text-[9px] text-white/40 truncate">{sub}</p>}
    </div>
  );
}

export default SalesTrackerPanel;

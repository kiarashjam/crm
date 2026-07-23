// Sales Tracker filter chips + Select dropdowns rendered inside the
// existing Leads filter panel.
//
// Encapsulates the filter state as a single object so the parent Leads
// page can serialise / deserialise it to and from the URL alongside its
// other filters.

import { Sparkles, PhoneCall, Calendar, PenSquare, FileCheck2, CircleDollarSign, AlertOctagon, Clock, X } from 'lucide-react';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import {
  OUTREACH_STATUS_OPTIONS,
  MEETING_SCHEDULED_OPTIONS,
  CONTRACT_SENT_OPTIONS,
  CONTRACT_SIGNED_OPTIONS,
} from '../../salesTracker/types';

/** All the sales-tracker filter axes a user can apply. `'all'` means the
 *  axis is inactive; specific enum literals or `'overdue14'`/`'overdue30'`
 *  are applied by the parent filter routine. */
export interface SalesTrackerFilterState {
  outreachStatus: string;
  meetingScheduled: string;
  contractSent: string;
  contractSigned: string;
  depositPaid: string;
  declineStage: string; // '', 'any', 'After Meeting', 'After Contract'
  overdue: string;      // 'all' | 'any' | 'over14' | 'over30'
}

export const EMPTY_SALES_TRACKER_FILTERS: SalesTrackerFilterState = {
  outreachStatus: 'all',
  meetingScheduled: 'all',
  contractSent: 'all',
  contractSigned: 'all',
  depositPaid: 'all',
  declineStage: 'all',
  overdue: 'all',
};

export function activeSalesTrackerFilterCount(f: SalesTrackerFilterState): number {
  return (Object.values(f).filter((v) => v !== 'all')).length;
}

interface Props {
  value: SalesTrackerFilterState;
  onChange: (patch: Partial<SalesTrackerFilterState>) => void;
}

function Label({ icon: Icon, children }: { icon: React.ElementType; children: React.ReactNode }) {
  return (
    <label className="flex items-center gap-2 text-[11px] font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
      <Icon className="w-3.5 h-3.5 text-orange-300/80" />
      {children}
    </label>
  );
}

const SENTINEL_CLEARED = '__cleared__';

/** Renders a select whose options come from an enum array, with an
 *  extra "All" first item that maps to the filter axis's `all` value. */
function EnumSelect({
  value,
  options,
  allLabel,
  onChange,
}: {
  value: string;
  options: readonly string[];
  allLabel: string;
  onChange: (v: string) => void;
}) {
  return (
    <Select
      value={value === '' ? SENTINEL_CLEARED : value}
      onValueChange={(v) => onChange(v === SENTINEL_CLEARED ? '' : v)}
    >
      <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
        <SelectValue />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value="all">{allLabel}</SelectItem>
        {options
          .filter((o) => o !== '')
          .map((opt) => (
            <SelectItem key={opt} value={opt}>{opt}</SelectItem>
          ))}
      </SelectContent>
    </Select>
  );
}

export function SalesTrackerFilters({ value, onChange }: Props) {
  return (
    <>
      <div className="min-w-0">
        <Label icon={PhoneCall}>Outreach status</Label>
        <EnumSelect
          value={value.outreachStatus}
          options={OUTREACH_STATUS_OPTIONS}
          allLabel="All outreach"
          onChange={(v) => onChange({ outreachStatus: v || 'all' })}
        />
      </div>

      <div className="min-w-0">
        <Label icon={Calendar}>Meeting scheduled</Label>
        <EnumSelect
          value={value.meetingScheduled}
          options={MEETING_SCHEDULED_OPTIONS}
          allLabel="Any meeting state"
          onChange={(v) => onChange({ meetingScheduled: v || 'all' })}
        />
      </div>

      <div className="min-w-0">
        <Label icon={PenSquare}>Contract sent</Label>
        <EnumSelect
          value={value.contractSent}
          options={CONTRACT_SENT_OPTIONS}
          allLabel="Any contract state"
          onChange={(v) => onChange({ contractSent: v || 'all' })}
        />
      </div>

      <div className="min-w-0">
        <Label icon={FileCheck2}>Contract signed</Label>
        <EnumSelect
          value={value.contractSigned}
          options={CONTRACT_SIGNED_OPTIONS}
          allLabel="Any signature state"
          onChange={(v) => onChange({ contractSigned: v || 'all' })}
        />
      </div>

      <div className="min-w-0">
        <Label icon={CircleDollarSign}>Deposit paid</Label>
        <Select value={value.depositPaid} onValueChange={(v) => onChange({ depositPaid: v })}>
          <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Any deposit state</SelectItem>
            <SelectItem value="Yes">Deposit paid</SelectItem>
            <SelectItem value="No">Not paid</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="min-w-0">
        <Label icon={AlertOctagon}>Decline stage</Label>
        <Select value={value.declineStage} onValueChange={(v) => onChange({ declineStage: v })}>
          <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Not declined / any</SelectItem>
            <SelectItem value="any">Declined (any stage)</SelectItem>
            <SelectItem value="After Meeting">Declined after meeting</SelectItem>
            <SelectItem value="After Contract">Declined after contract</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <div className="min-w-0">
        <Label icon={Clock}>Overdue contracts</Label>
        <Select value={value.overdue} onValueChange={(v) => onChange({ overdue: v })}>
          <SelectTrigger className="w-full h-10 rounded-lg bg-white/10 border-white/10 text-white hover:bg-white/15 transition-colors">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="all">Any age</SelectItem>
            <SelectItem value="any">Any outstanding</SelectItem>
            <SelectItem value="over14">Outstanding &gt; 14 days</SelectItem>
            <SelectItem value="over30">Outstanding &gt; 30 days</SelectItem>
          </SelectContent>
        </Select>
      </div>
    </>
  );
}

/** Pretty chip row shown in the "showing" strip when any sales-tracker
 *  filter is active. Rendered separately so it can slot into the parent's
 *  existing chip row without a wrapping <div>. */
export function SalesTrackerFilterChips({
  filters,
  onClear,
}: {
  filters: SalesTrackerFilterState;
  onClear: (key: keyof SalesTrackerFilterState) => void;
}) {
  const chips: React.ReactNode[] = [];

  const pill = (
    key: keyof SalesTrackerFilterState,
    label: string,
    icon: React.ElementType,
    tone: string,
  ) => {
    chips.push(
      <span
        key={key}
        className={cn('inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-xs font-medium border', tone)}
      >
        <SmallIcon icon={icon} />
        {label}
        <button
          onClick={() => onClear(key)}
          className="ml-0.5 opacity-70 hover:opacity-100 transition-opacity"
          aria-label={`Clear ${label} filter`}
        >
          <X className="w-3 h-3" />
        </button>
      </span>,
    );
  };

  if (filters.outreachStatus !== 'all') pill('outreachStatus', `Outreach: ${filters.outreachStatus}`, PhoneCall, 'bg-blue-500/20 text-blue-300 border-blue-400/30');
  if (filters.meetingScheduled !== 'all') pill('meetingScheduled', `Meeting: ${filters.meetingScheduled}`, Calendar, 'bg-violet-500/20 text-violet-300 border-violet-400/30');
  if (filters.contractSent !== 'all') pill('contractSent', `Contract sent: ${filters.contractSent}`, PenSquare, 'bg-indigo-500/20 text-indigo-300 border-indigo-400/30');
  if (filters.contractSigned !== 'all') pill('contractSigned', `Signed: ${filters.contractSigned}`, FileCheck2, 'bg-emerald-500/20 text-emerald-300 border-emerald-400/30');
  if (filters.depositPaid !== 'all') pill('depositPaid', `Deposit: ${filters.depositPaid}`, CircleDollarSign, 'bg-emerald-500/20 text-emerald-300 border-emerald-400/30');
  if (filters.declineStage !== 'all') pill('declineStage', filters.declineStage === 'any' ? 'Declined (any)' : `Declined ${filters.declineStage.toLowerCase()}`, AlertOctagon, 'bg-rose-500/20 text-rose-300 border-rose-400/30');
  if (filters.overdue !== 'all') {
    const label =
      filters.overdue === 'over30' ? 'Outstanding > 30d'
      : filters.overdue === 'over14' ? 'Outstanding > 14d'
      : 'Contracts outstanding';
    pill('overdue', label, Clock, 'bg-amber-500/20 text-amber-300 border-amber-400/30');
  }

  return <>{chips}</>;
}

function SmallIcon({ icon: Icon }: { icon: React.ElementType }) {
  return <Icon className="w-3 h-3" />;
}

/** Header pill shown above the sales-tracker filter block so users can
 *  see at a glance what section of filters they're in. */
export function SalesTrackerFiltersHeader({ activeCount }: { activeCount: number }) {
  return (
    <div className="col-span-full flex items-center gap-2 -mb-1">
      <div className="flex h-6 w-6 items-center justify-center rounded-md bg-gradient-to-br from-orange-500/40 to-amber-500/40 ring-1 ring-white/10">
        <Sparkles className="w-3.5 h-3.5 text-orange-200" />
      </div>
      <span className="text-[11px] font-bold uppercase tracking-wider text-white/70">Sales Tracker filters</span>
      {activeCount > 0 && (
        <span className="ml-0.5 rounded-md bg-orange-500/30 border border-orange-400/40 px-1.5 py-0.5 text-[10px] font-bold text-orange-100">
          {activeCount}
        </span>
      )}
    </div>
  );
}

// Compact Sales Tracker editor rendered from a Lead card's "Track" button.
//
// This is the mid-way UX between the full editor on the Lead detail page
// and just showing badges: users can toggle the sales-tracker state
// without leaving the list. Saves on every field change (no explicit Save
// button) because the state is client-only and the round-trip is a few
// bytes to localStorage.

import { useEffect, useMemo, useState } from 'react';
import { Sparkles, Save } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Label } from '@/app/components/ui/label';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Button } from '@/app/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import {
  getSalesExtras, setSalesExtras, EMPTY_SALES_EXTRAS,
  type SalesExtras,
} from '../salesExtrasStore';
import {
  OUTREACH_STATUS_OPTIONS,
  MEETING_SCHEDULED_OPTIONS,
  YES_NO_OPTIONS,
  YES_NO_PENDING_OPTIONS,
  CONTRACT_SENT_OPTIONS,
  CONTRACT_SIGNED_OPTIONS,
} from '../../salesTracker/types';
import {
  declineStage, daysContractOutstanding, daysToSign,
} from '../../salesTracker/computed';
import { ddMmYyyyToIso, isoToDdMmYyyy } from '../../salesTracker/dateUtils';

const SENTINEL = '__cleared__';

function EnumSelect<T extends string>({
  value, options, onChange, placeholder,
}: {
  value: T;
  options: readonly T[];
  onChange: (v: T) => void;
  placeholder?: string;
}) {
  return (
    <Select
      value={value === '' ? SENTINEL : value}
      onValueChange={(v) => onChange((v === SENTINEL ? '' : v) as T)}
    >
      <SelectTrigger className="h-8 rounded-lg bg-white border-slate-200 text-xs">
        <SelectValue placeholder={placeholder ?? '—'} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={SENTINEL}><span className="text-slate-400">—</span></SelectItem>
        {options
          .filter((o) => o !== '')
          .map((opt) => <SelectItem key={opt} value={opt}>{opt}</SelectItem>)}
      </SelectContent>
    </Select>
  );
}

function DateField({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  return (
    <Input
      type="date"
      value={ddMmYyyyToIso(value)}
      onChange={(e) => onChange(isoToDdMmYyyy(e.target.value))}
      className="h-8 rounded-lg bg-white border-slate-200 text-xs"
    />
  );
}

interface Props {
  leadId: string;
  leadName?: string;
  trigger: React.ReactNode;
  /** Called after any successful save so parent can refetch/re-render. */
  onSaved?: (extras: SalesExtras) => void;
}

export function InlineSalesEditorPopover({ leadId, leadName, trigger, onSaved }: Props) {
  const [open, setOpen] = useState(false);
  const [extras, setExtras] = useState<SalesExtras>(() => getSalesExtras(leadId));

  // Reload every time the popover opens so it's fresh (another tab may
  // have saved something since the last render).
  useEffect(() => {
    if (open) setExtras(getSalesExtras(leadId));
  }, [open, leadId]);

  const commit = (patch: Partial<SalesExtras>) => {
    const next = { ...extras, ...patch };
    setExtras(next);
    setSalesExtras(leadId, next);
    onSaved?.(next);
  };

  const computed = useMemo(() => ({
    stage: declineStage(extras),
    outstanding: daysContractOutstanding(extras),
    toSign: daysToSign(extras),
  }), [extras]);

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild onClick={(e) => e.stopPropagation()}>
        {trigger}
      </PopoverTrigger>
      <PopoverContent
        align="end"
        className="w-[380px] p-0 overflow-hidden"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Gradient header */}
        <div className="relative overflow-hidden bg-gradient-to-br from-indigo-500 to-violet-600 px-4 py-3">
          <div className="absolute -top-6 -right-6 h-20 w-20 rounded-full bg-white/10 blur-xl" />
          <div className="relative flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 min-w-0">
              <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-white/20 backdrop-blur-sm ring-1 ring-white/30">
                <Sparkles className="w-3.5 h-3.5 text-white" />
              </div>
              <div className="min-w-0">
                <p className="text-[10px] uppercase tracking-wider text-white/70 font-semibold">Sales Tracker</p>
                <p className="text-xs font-bold text-white truncate">{leadName ?? 'Edit tracker'}</p>
              </div>
            </div>
            <span className="text-[10px] text-white/60 whitespace-nowrap">Auto-saves</span>
          </div>
        </div>

        {/* Fields */}
        <div className="p-4 max-h-[62vh] overflow-y-auto space-y-2.5">
          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Outreach</Label>
              <div className="mt-1">
                <EnumSelect
                  value={extras.outreachStatus}
                  options={OUTREACH_STATUS_OPTIONS}
                  onChange={(v) => commit({ outreachStatus: v })}
                  placeholder="Not started"
                />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Outreach date</Label>
              <div className="mt-1"><DateField value={extras.outreachDate} onChange={(v) => commit({ outreachDate: v })} /></div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Meeting sched.</Label>
              <div className="mt-1">
                <EnumSelect value={extras.meetingScheduled} options={MEETING_SCHEDULED_OPTIONS} onChange={(v) => commit({ meetingScheduled: v })} />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Meeting date</Label>
              <div className="mt-1"><DateField value={extras.meetingDate} onChange={(v) => commit({ meetingDate: v })} /></div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Met (showed)</Label>
              <div className="mt-1">
                <EnumSelect value={extras.met} options={YES_NO_OPTIONS} onChange={(v) => commit({ met: v })} />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Interested</Label>
              <div className="mt-1">
                <EnumSelect value={extras.interestedAfterMtg} options={YES_NO_PENDING_OPTIONS} onChange={(v) => commit({ interestedAfterMtg: v })} />
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Contract sent</Label>
              <div className="mt-1">
                <EnumSelect value={extras.contractSent} options={CONTRACT_SENT_OPTIONS} onChange={(v) => commit({ contractSent: v })} />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Sent date</Label>
              <div className="mt-1"><DateField value={extras.contractSentDate} onChange={(v) => commit({ contractSentDate: v })} /></div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Signed</Label>
              <div className="mt-1">
                <EnumSelect value={extras.contractSigned} options={CONTRACT_SIGNED_OPTIONS} onChange={(v) => commit({ contractSigned: v })} />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Signature date</Label>
              <div className="mt-1"><DateField value={extras.signatureDate} onChange={(v) => commit({ signatureDate: v })} /></div>
            </div>
          </div>

          <div className="grid grid-cols-2 gap-2">
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Deposit paid</Label>
              <div className="mt-1">
                <EnumSelect value={extras.depositPaid} options={YES_NO_OPTIONS} onChange={(v) => commit({ depositPaid: v })} />
              </div>
            </div>
            <div>
              <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Last contact</Label>
              <div className="mt-1"><DateField value={extras.lastContactDate} onChange={(v) => commit({ lastContactDate: v })} /></div>
            </div>
          </div>

          <div>
            <Label className="text-[10px] font-semibold text-slate-500 uppercase tracking-wider">Sales notes</Label>
            <Textarea
              value={extras.salesNotes}
              onChange={(e) => commit({ salesNotes: e.target.value })}
              placeholder="Anything worth remembering from calls, meetings, or follow-ups…"
              className="mt-1 min-h-[60px] rounded-lg border-slate-200 bg-white text-xs"
            />
          </div>

          {/* Computed row */}
          <div className="grid grid-cols-3 gap-1.5 mt-2 pt-2 border-t border-slate-100">
            <div className="rounded-lg bg-slate-50 px-2 py-1.5">
              <p className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold">Outstanding</p>
              <p className={cn(
                'text-sm font-bold tabular-nums',
                computed.outstanding != null && computed.outstanding >= 30 ? 'text-rose-600'
                : computed.outstanding != null && computed.outstanding >= 14 ? 'text-amber-600'
                : 'text-slate-800',
              )}>
                {computed.outstanding ?? '—'}{computed.outstanding != null && <span className="text-[9px] text-slate-400 ml-0.5">d</span>}
              </p>
            </div>
            <div className="rounded-lg bg-slate-50 px-2 py-1.5">
              <p className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold">Days to sign</p>
              <p className="text-sm font-bold text-emerald-600 tabular-nums">
                {computed.toSign ?? '—'}{computed.toSign != null && <span className="text-[9px] text-slate-400 ml-0.5">d</span>}
              </p>
            </div>
            <div className="rounded-lg bg-slate-50 px-2 py-1.5">
              <p className="text-[9px] uppercase tracking-wider text-slate-400 font-semibold">Decline</p>
              <p className={cn(
                'text-[11px] font-semibold truncate',
                computed.stage === 'After Meeting' ? 'text-amber-600'
                : computed.stage === 'After Contract' ? 'text-rose-600'
                : 'text-slate-400',
              )}>
                {computed.stage || '—'}
              </p>
            </div>
          </div>

          <div className="flex items-center justify-between gap-2 pt-1">
            <p className="text-[10px] text-slate-400">Changes save immediately</p>
            <Button
              size="sm"
              variant="ghost"
              onClick={() => {
                setExtras({ ...EMPTY_SALES_EXTRAS });
                setSalesExtras(leadId, { ...EMPTY_SALES_EXTRAS });
                onSaved?.({ ...EMPTY_SALES_EXTRAS });
              }}
              className="h-7 rounded-md text-[11px] text-slate-500 hover:bg-slate-50"
            >
              Clear all
            </Button>
          </div>
        </div>

        <div className="border-t border-slate-100 bg-slate-50 px-4 py-2 flex items-center justify-between">
          <span className="text-[10px] text-slate-500 inline-flex items-center gap-1">
            <Save className="w-3 h-3 text-emerald-500" /> Auto-saved to browser
          </span>
          <button
            type="button"
            onClick={() => setOpen(false)}
            className="text-[11px] font-medium text-indigo-600 hover:text-indigo-800"
          >
            Done
          </button>
        </div>
      </PopoverContent>
    </Popover>
  );
}

export default InlineSalesEditorPopover;

// Compact lead-pipeline editor from a Lead card's "Track" button.
// Saves to lead.pipelineState so the funnel dashboard stays in sync.

import { useEffect, useMemo, useState } from 'react';
import { Sparkles } from 'lucide-react';
import { Popover, PopoverContent, PopoverTrigger } from '@/app/components/ui/popover';
import { Label } from '@/app/components/ui/label';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import type { Lead } from '@/app/api/types';
import {
  EMPTY_SALES_EXTRAS,
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
import {
  leadToTrackedRow,
  salesExtrasToTrackedRow,
  trackedRowToPipeline,
  trackedRowToSalesExtras,
} from '../leadTrackerMap';
import { parsePipeline } from '../leadPipeline';
import type { useLeadStatusSync } from '../useLeadStatusSync';

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
  lead: Lead;
  leadName?: string;
  trigger: React.ReactNode;
  onSaved?: (updated: Lead) => void;
  /** Shared status-sync instance from the Leads page, so behaviour and copy
   *  match the detail page exactly. */
  statusSync: ReturnType<typeof useLeadStatusSync>;
}

export function InlineSalesEditorPopover({ lead, leadName, trigger, onSaved, statusSync }: Props) {
  const [open, setOpen] = useState(false);
  const [extras, setExtras] = useState<SalesExtras>(() => trackedRowToSalesExtras(leadToTrackedRow(lead)));
  const [saving, setSaving] = useState(false);

  // Reseed only when the popover OPENS. Re-seeding on every `lead` change would
  // snap the user's in-progress edits back each time a save round-tripped.
  useEffect(() => {
    if (open) setExtras(trackedRowToSalesExtras(leadToTrackedRow(lead)));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- intentionally open-only
  }, [open]);

  const commit = async (patch: Partial<SalesExtras>) => {
    const next = { ...extras, ...patch };
    setExtras(next);
    setSaving(true);
    try {
      const row = salesExtrasToTrackedRow(next);
      // One PUT carries the pipeline AND any status the pipeline now implies;
      // the hook's sequence guard drops responses from superseded edits.
      const pipeline = trackedRowToPipeline(row, parsePipeline(lead.pipelineState));
      const outcome = await statusSync.save(lead, pipeline, {
        onApplied: (updated) => onSaved?.(updated),
      });
      if (!outcome.ok && !outcome.stale) setExtras(extras);
    } finally {
      setSaving(false);
    }
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
        <div className="relative overflow-hidden bg-gradient-to-br from-indigo-500 to-violet-600 px-4 py-3">
          <div className="absolute -top-6 -right-6 h-20 w-20 rounded-full bg-white/10 blur-xl" />
          <div className="relative flex items-center justify-between gap-2">
            <div className="flex items-center gap-2 min-w-0">
              <Sparkles className="w-4 h-4 text-white shrink-0" />
              <div className="min-w-0">
                <p className="text-xs font-bold text-white truncate">Lead pipeline</p>
                <p className="text-[10px] text-white/70 truncate">{leadName || lead.name}</p>
              </div>
            </div>
            {saving && <span className="text-[10px] text-white/80">Saving…</span>}
          </div>
        </div>

        <div className="p-3 space-y-2.5 max-h-[70vh] overflow-y-auto">
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Outreach</Label>
              <EnumSelect value={extras.outreachStatus} options={OUTREACH_STATUS_OPTIONS} onChange={(v) => void commit({ outreachStatus: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Outreach date</Label>
              <DateField value={extras.outreachDate} onChange={(v) => void commit({ outreachDate: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Meeting</Label>
              <EnumSelect value={extras.meetingScheduled} options={MEETING_SCHEDULED_OPTIONS} onChange={(v) => void commit({ meetingScheduled: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Meeting date</Label>
              <DateField value={extras.meetingDate} onChange={(v) => void commit({ meetingDate: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Met</Label>
              <EnumSelect value={extras.met} options={YES_NO_OPTIONS} onChange={(v) => void commit({ met: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Interested</Label>
              <EnumSelect value={extras.interestedAfterMtg} options={YES_NO_PENDING_OPTIONS} onChange={(v) => void commit({ interestedAfterMtg: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Contract sent</Label>
              <EnumSelect value={extras.contractSent} options={CONTRACT_SENT_OPTIONS} onChange={(v) => void commit({ contractSent: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Sent date</Label>
              <DateField value={extras.contractSentDate} onChange={(v) => void commit({ contractSentDate: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Signed</Label>
              <EnumSelect value={extras.contractSigned} options={CONTRACT_SIGNED_OPTIONS} onChange={(v) => void commit({ contractSigned: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Signature date</Label>
              <DateField value={extras.signatureDate} onChange={(v) => void commit({ signatureDate: v })} />
            </div>
            <div className="space-y-1">
              <Label className="text-[10px] text-slate-500">Deposit</Label>
              <EnumSelect value={extras.depositPaid} options={YES_NO_OPTIONS} onChange={(v) => void commit({ depositPaid: v })} />
            </div>
          </div>
          <div className="space-y-1">
            <Label className="text-[10px] text-slate-500">Notes</Label>
            <Textarea
              value={extras.salesNotes}
              onChange={(e) => setExtras((p) => ({ ...p, salesNotes: e.target.value }))}
              onBlur={() => void commit({ salesNotes: extras.salesNotes })}
              rows={2}
              className="rounded-lg bg-white border-slate-200 text-xs"
            />
          </div>
          <div className="flex flex-wrap gap-2 text-[10px] text-slate-500 pt-1 border-t border-slate-100">
            <span>Decline: {computed.stage || '—'}</span>
            <span>Outstanding: {computed.outstanding ?? '—'}</span>
            <span>To sign: {computed.toSign ?? '—'}</span>
            <button
              type="button"
              className="ml-auto text-rose-600 hover:underline"
              onClick={() => void commit({ ...EMPTY_SALES_EXTRAS })}
            >
              Clear
            </button>
          </div>
        </div>
      </PopoverContent>
    </Popover>
  );
}

export default InlineSalesEditorPopover;

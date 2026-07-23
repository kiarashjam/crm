// Editable lead-pipeline section on the Lead detail page.
// Fields match the P46 tracker workbook and persist on lead.pipelineState
// so the org-wide lead funnel reads the same data.

import { useCallback, useEffect, useMemo, useState } from 'react';
import { Save, Check, Clock, AlertTriangle, Sparkles, TrendingUp } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import { updateLead } from '@/app/api/leads';
import type { Lead } from '@/app/api/types';
import { EMPTY_SALES_EXTRAS, type SalesExtras } from '../salesExtrasStore';
import {
  OUTREACH_STATUS_OPTIONS,
  MEETING_SCHEDULED_OPTIONS,
  YES_NO_OPTIONS,
  YES_NO_PENDING_OPTIONS,
  CONTRACT_SENT_OPTIONS,
  CONTRACT_SIGNED_OPTIONS,
} from '../../salesTracker/types';
import {
  declineStage,
  daysContractOutstanding,
  daysToSign,
  meetingWeek,
} from '../../salesTracker/computed';
import {
  ddMmYyyyToIso,
  isoToDdMmYyyy,
} from '../../salesTracker/dateUtils';
import {
  leadToTrackedRow,
  salesExtrasToTrackedRow,
  serializeTrackedRowAsPipeline,
  trackedRowToSalesExtras,
} from '../leadTrackerMap';

interface SalesTrackerCardProps {
  lead: Lead;
  className?: string;
  onSaved?: (updated: Lead) => void;
}

function EnumSelect<T extends string>({
  value,
  options,
  onChange,
  placeholder,
}: {
  value: T;
  options: readonly T[];
  onChange: (v: T) => void;
  placeholder?: string;
}) {
  const SENTINEL = '__cleared__';
  return (
    <Select
      value={value === '' ? SENTINEL : value}
      onValueChange={(v) => onChange((v === SENTINEL ? '' : v) as T)}
    >
      <SelectTrigger className="h-9 rounded-lg bg-white border-slate-200 text-sm">
        <SelectValue placeholder={placeholder ?? '—'} />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={SENTINEL}>
          <span className="text-slate-400">—</span>
        </SelectItem>
        {options
          .filter((o) => o !== '')
          .map((opt) => (
            <SelectItem key={opt} value={opt}>{opt}</SelectItem>
          ))}
      </SelectContent>
    </Select>
  );
}

function DateField({
  value,
  onChange,
}: { value: string; onChange: (v: string) => void }) {
  const iso = ddMmYyyyToIso(value);
  return (
    <Input
      type="date"
      value={iso}
      onChange={(e) => onChange(isoToDdMmYyyy(e.target.value))}
      className="h-9 rounded-lg bg-white border-slate-200 text-sm"
    />
  );
}

function extrasFromLead(lead: Lead): SalesExtras {
  return trackedRowToSalesExtras(leadToTrackedRow(lead));
}

export function SalesTrackerCard({ lead, className, onSaved }: SalesTrackerCardProps) {
  const [extras, setExtras] = useState<SalesExtras>(() => extrasFromLead(lead));
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setExtras(extrasFromLead(lead));
    setDirty(false);
  }, [lead.id, lead.pipelineState]);

  const update = useCallback((patch: Partial<SalesExtras>) => {
    setExtras((prev) => ({ ...prev, ...patch }));
    setDirty(true);
  }, []);

  const handleSave = async () => {
    setSaving(true);
    try {
      const row = salesExtrasToTrackedRow(extras);
      const pipelineState = serializeTrackedRowAsPipeline(row, lead.pipelineState);
      const updated = await updateLead(lead.id, { pipelineState });
      if (!updated) throw new Error('update failed');
      setDirty(false);
      onSaved?.(updated);
      toast.success('Lead pipeline saved');
    } catch {
      toast.error('Failed to save lead pipeline');
    } finally {
      setSaving(false);
    }
  };

  const handleReset = () => {
    setExtras({ ...EMPTY_SALES_EXTRAS });
    setDirty(true);
  };

  const computed = useMemo(() => ({
    stage: declineStage(extras),
    outstanding: daysContractOutstanding(extras),
    toSign: daysToSign(extras),
    week: meetingWeek(extras),
  }), [extras]);

  return (
    <div
      className={cn(
        'rounded-2xl border border-indigo-200/60 bg-gradient-to-br from-white via-indigo-50/30 to-violet-50/40 p-5 shadow-sm',
        className,
      )}
    >
      <header className="flex items-center justify-between gap-2 mb-4">
        <div className="flex items-center gap-2.5">
          <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-indigo-500 to-violet-600 flex items-center justify-center shadow-sm shadow-indigo-500/30">
            <Sparkles className="w-4 h-4 text-white" />
          </div>
          <div>
            <h3 className="text-sm font-semibold text-slate-900">Lead pipeline</h3>
            <p className="text-[11px] text-slate-500">Outreach → meeting → contract → signature</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {dirty && (
            <span className="inline-flex items-center gap-1 text-[10px] font-semibold text-amber-700 bg-amber-50 border border-amber-200 rounded-full px-2 py-0.5">
              <AlertTriangle className="w-3 h-3" />
              Unsaved
            </span>
          )}
          <Button
            size="sm"
            variant="outline"
            onClick={handleReset}
            className="h-8 px-3 rounded-lg text-xs border-slate-200 text-slate-600 hover:bg-slate-50"
          >
            Reset
          </Button>
          <Button
            size="sm"
            onClick={() => void handleSave()}
            disabled={!dirty || saving}
            className="h-8 px-3 rounded-lg text-xs bg-indigo-600 hover:bg-indigo-700 text-white shadow-sm"
          >
            {saving ? <Check className="w-3.5 h-3.5" /> : <Save className="w-3.5 h-3.5 mr-1" />}
            {saving ? 'Saved' : 'Save'}
          </Button>
        </div>
      </header>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Outreach status</Label>
          <EnumSelect
            value={extras.outreachStatus}
            options={OUTREACH_STATUS_OPTIONS}
            onChange={(v) => update({ outreachStatus: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Outreach date</Label>
          <DateField value={extras.outreachDate} onChange={(v) => update({ outreachDate: v })} />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Meeting scheduled</Label>
          <EnumSelect
            value={extras.meetingScheduled}
            options={MEETING_SCHEDULED_OPTIONS}
            onChange={(v) => update({ meetingScheduled: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Meeting date</Label>
          <DateField value={extras.meetingDate} onChange={(v) => update({ meetingDate: v })} />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Met (showed up)</Label>
          <EnumSelect value={extras.met} options={YES_NO_OPTIONS} onChange={(v) => update({ met: v })} />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Interested after meeting</Label>
          <EnumSelect
            value={extras.interestedAfterMtg}
            options={YES_NO_PENDING_OPTIONS}
            onChange={(v) => update({ interestedAfterMtg: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Contract sent</Label>
          <EnumSelect
            value={extras.contractSent}
            options={CONTRACT_SENT_OPTIONS}
            onChange={(v) => update({ contractSent: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Contract sent date</Label>
          <DateField value={extras.contractSentDate} onChange={(v) => update({ contractSentDate: v })} />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Contract signed</Label>
          <EnumSelect
            value={extras.contractSigned}
            options={CONTRACT_SIGNED_OPTIONS}
            onChange={(v) => update({ contractSigned: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Signature date</Label>
          <DateField value={extras.signatureDate} onChange={(v) => update({ signatureDate: v })} />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Deposit paid</Label>
          <EnumSelect
            value={extras.depositPaid}
            options={YES_NO_OPTIONS}
            onChange={(v) => update({ depositPaid: v })}
          />
        </div>
        <div className="space-y-1.5">
          <Label className="text-[11px] text-slate-500">Last contact date</Label>
          <DateField value={extras.lastContactDate} onChange={(v) => update({ lastContactDate: v })} />
        </div>
      </div>

      <div className="mt-3 space-y-1.5">
        <Label className="text-[11px] text-slate-500">Notes</Label>
        <Textarea
          value={extras.salesNotes}
          onChange={(e) => update({ salesNotes: e.target.value })}
          rows={3}
          className="rounded-lg bg-white border-slate-200 text-sm"
        />
      </div>

      <div className="mt-4 grid grid-cols-2 sm:grid-cols-4 gap-2">
        <div className="rounded-lg bg-white/70 border border-slate-200 px-3 py-2">
          <p className="text-[10px] uppercase tracking-wider text-slate-400 font-semibold">Decline stage</p>
          <p className="text-sm font-semibold text-slate-800 mt-0.5">{computed.stage || '—'}</p>
        </div>
        <div className="rounded-lg bg-white/70 border border-slate-200 px-3 py-2">
          <p className="text-[10px] uppercase tracking-wider text-slate-400 font-semibold flex items-center gap-1">
            <Clock className="w-3 h-3" /> Days outstanding
          </p>
          <p className="text-sm font-semibold text-slate-800 mt-0.5">
            {computed.outstanding == null ? '—' : computed.outstanding}
          </p>
        </div>
        <div className="rounded-lg bg-white/70 border border-slate-200 px-3 py-2">
          <p className="text-[10px] uppercase tracking-wider text-slate-400 font-semibold flex items-center gap-1">
            <TrendingUp className="w-3 h-3" /> Days to sign
          </p>
          <p className="text-sm font-semibold text-slate-800 mt-0.5">
            {computed.toSign == null ? '—' : computed.toSign}
          </p>
        </div>
        <div className="rounded-lg bg-white/70 border border-slate-200 px-3 py-2">
          <p className="text-[10px] uppercase tracking-wider text-slate-400 font-semibold">Meeting week</p>
          <p className="text-sm font-semibold text-slate-800 mt-0.5">
            {computed.week == null ? '—' : computed.week}
          </p>
        </div>
      </div>
    </div>
  );
}

export default SalesTrackerCard;

// Editable "Sales Tracker" section on the Lead detail page.
//
// Surfaces every field from the P46 Sales Tracker Excel CONTACTS sheet
// (outreach status/date, meeting scheduled/date, met, interested after mtg,
// contract sent/date, contract signed/date, deposit paid, last contact,
// sales notes) alongside the three computed columns from the sheet
// (decline stage, days contract outstanding, days to sign).

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
import {
  EMPTY_SALES_EXTRAS,
  getSalesExtras,
  setSalesExtras,
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
  declineStage,
  daysContractOutstanding,
  daysToSign,
  meetingWeek,
} from '../../salesTracker/computed';
import {
  ddMmYyyyToIso,
  isoToDdMmYyyy,
} from '../../salesTracker/dateUtils';

interface SalesTrackerCardProps {
  leadId: string;
  className?: string;
}

/** Dropdown wrapper for a set of Excel enum literals. Empty string = cleared. */
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

/** dd.mm.yyyy field backed by a native date input (converts on the fly). */
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

export function SalesTrackerCard({ leadId, className }: SalesTrackerCardProps) {
  const [extras, setExtras] = useState<SalesExtras>(() => getSalesExtras(leadId));
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);

  // Reload when the lead id changes (SPA navigation between leads).
  useEffect(() => {
    setExtras(getSalesExtras(leadId));
    setDirty(false);
  }, [leadId]);

  const update = useCallback((patch: Partial<SalesExtras>) => {
    setExtras((prev) => ({ ...prev, ...patch }));
    setDirty(true);
  }, []);

  const handleSave = () => {
    setSaving(true);
    setSalesExtras(leadId, extras);
    setDirty(false);
    setTimeout(() => setSaving(false), 250);
    toast.success('Sales tracker saved');
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
            <h3 className="text-sm font-semibold text-slate-900">Sales Tracker</h3>
            <p className="text-[11px] text-slate-500">Founding membership pre-sales pipeline</p>
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
            onClick={handleSave}
            disabled={!dirty || saving}
            className="h-8 px-3 rounded-lg text-xs bg-indigo-600 hover:bg-indigo-700 text-white shadow-sm"
          >
            {saving ? <Check className="w-3.5 h-3.5" /> : <Save className="w-3.5 h-3.5 mr-1" />}
            {saving ? 'Saved' : 'Save'}
          </Button>
        </div>
      </header>

      {/* Outreach row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Outreach status</Label>
          <div className="mt-1">
            <EnumSelect
              value={extras.outreachStatus}
              options={OUTREACH_STATUS_OPTIONS}
              onChange={(v) => update({ outreachStatus: v })}
              placeholder="Not yet contacted"
            />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Outreach date</Label>
          <div className="mt-1"><DateField value={extras.outreachDate} onChange={(v) => update({ outreachDate: v })} /></div>
        </div>
      </div>

      {/* Meeting row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Meeting scheduled</Label>
          <div className="mt-1">
            <EnumSelect
              value={extras.meetingScheduled}
              options={MEETING_SCHEDULED_OPTIONS}
              onChange={(v) => update({ meetingScheduled: v })}
            />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Meeting date</Label>
          <div className="mt-1"><DateField value={extras.meetingDate} onChange={(v) => update({ meetingDate: v })} /></div>
        </div>
      </div>

      {/* Met + Interested row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Met (showed up)</Label>
          <div className="mt-1">
            <EnumSelect value={extras.met} options={YES_NO_OPTIONS} onChange={(v) => update({ met: v })} />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Interested after mtg</Label>
          <div className="mt-1">
            <EnumSelect
              value={extras.interestedAfterMtg}
              options={YES_NO_PENDING_OPTIONS}
              onChange={(v) => update({ interestedAfterMtg: v })}
            />
          </div>
        </div>
      </div>

      {/* Contract sent row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Contract sent</Label>
          <div className="mt-1">
            <EnumSelect
              value={extras.contractSent}
              options={CONTRACT_SENT_OPTIONS}
              onChange={(v) => update({ contractSent: v })}
            />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Contract sent date</Label>
          <div className="mt-1"><DateField value={extras.contractSentDate} onChange={(v) => update({ contractSentDate: v })} /></div>
        </div>
      </div>

      {/* Contract signed row */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Contract signed</Label>
          <div className="mt-1">
            <EnumSelect
              value={extras.contractSigned}
              options={CONTRACT_SIGNED_OPTIONS}
              onChange={(v) => update({ contractSigned: v })}
            />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Signature date</Label>
          <div className="mt-1"><DateField value={extras.signatureDate} onChange={(v) => update({ signatureDate: v })} /></div>
        </div>
      </div>

      {/* Deposit + last contact */}
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 mb-3">
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Deposit paid</Label>
          <div className="mt-1">
            <EnumSelect value={extras.depositPaid} options={YES_NO_OPTIONS} onChange={(v) => update({ depositPaid: v })} />
          </div>
        </div>
        <div>
          <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Last contact date</Label>
          <div className="mt-1"><DateField value={extras.lastContactDate} onChange={(v) => update({ lastContactDate: v })} /></div>
        </div>
      </div>

      {/* Sales notes */}
      <div className="mb-3">
        <Label className="text-[11px] font-semibold text-slate-500 uppercase tracking-wider">Sales notes</Label>
        <Textarea
          value={extras.salesNotes}
          onChange={(e) => update({ salesNotes: e.target.value })}
          placeholder="Anything worth remembering from calls, meetings, or follow-ups…"
          className="mt-1 min-h-[70px] rounded-lg border-slate-200 bg-white text-sm"
        />
      </div>

      {/* Computed row */}
      <div className="grid grid-cols-2 sm:grid-cols-4 gap-2 mt-4 pt-4 border-t border-slate-200/70">
        <div className="rounded-xl bg-white border border-slate-200 px-3 py-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Meeting week</p>
          <p className="text-lg font-bold text-slate-900 tabular-nums mt-0.5">
            {computed.week ?? '—'}
          </p>
        </div>
        <div className="rounded-xl bg-white border border-slate-200 px-3 py-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Days outstanding</p>
          <p
            className={cn(
              'text-lg font-bold tabular-nums mt-0.5 flex items-center gap-1',
              computed.outstanding != null && computed.outstanding >= 30
                ? 'text-rose-600'
                : computed.outstanding != null && computed.outstanding >= 14
                  ? 'text-amber-600'
                  : 'text-slate-900',
            )}
          >
            {computed.outstanding != null && <Clock className="w-3.5 h-3.5" />}
            {computed.outstanding ?? '—'}
          </p>
        </div>
        <div className="rounded-xl bg-white border border-slate-200 px-3 py-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Days to sign</p>
          <p className="text-lg font-bold text-slate-900 tabular-nums mt-0.5 flex items-center gap-1">
            {computed.toSign != null && <TrendingUp className="w-3.5 h-3.5 text-emerald-500" />}
            {computed.toSign ?? '—'}
          </p>
        </div>
        <div className="rounded-xl bg-white border border-slate-200 px-3 py-2">
          <p className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Decline stage</p>
          <p
            className={cn(
              'text-sm font-semibold mt-1',
              computed.stage === 'After Meeting'
                ? 'text-amber-600'
                : computed.stage === 'After Contract'
                  ? 'text-rose-600'
                  : 'text-slate-400',
            )}
          >
            {computed.stage || '—'}
          </p>
        </div>
      </div>
    </div>
  );
}

export default SalesTrackerCard;

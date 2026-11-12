import { useState } from 'react';
import { Loader2, ShieldCheck } from 'lucide-react';
import { toast } from 'sonner';
import { useQueryClient } from '@tanstack/react-query';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import {
  CONSENT_CHANNEL_LABEL, CONSENT_CHANNELS, CONSENT_PURPOSE_LABEL, CONSENT_PURPOSES, CONSENT_SOURCE_LABEL, RECORDABLE_SOURCES,
  recordMemberConsent,
  type ConsentChannel, type ConsentPurpose, type ConsentStateRow, type MemberConsent, type RecordableConsentSource,
} from '@/app/api/consent';
import { clubKeys, useMemberConsent } from '@/app/hooks/queries/club';
import { useWorkspaceScope } from '@/app/hooks/queries/useWorkspaceScope';

const STATE_LABEL: Record<ConsentStateRow['state'], string> = { granted: 'Agreed', withdrawn: 'Said no', unknown: 'Not asked' };
const STATE_BADGE: Record<ConsentStateRow['state'], string> = {
  granted: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  withdrawn: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  unknown: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
};

function formatDate(iso?: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
}

/**
 * A member's consent to be contacted, per channel and purpose, with every answer kept:
 * where each stands, who recorded it, how it was given, and the history behind it.
 *
 * Marketing goes only where the member said yes; a "not asked" is a no. The service
 * column records what the member asked for, but statements and booking confirmations
 * go whatever it says, so it is labelled that way rather than offered as a switch that
 * would stop them.
 */
export default function MemberConsentPanel({ memberId }: { memberId: string }) {
  const query = useMemberConsent(memberId);
  const [editing, setEditing] = useState<{ channel: ConsentChannel; purpose: ConsentPurpose } | null>(null);

  if (query.isPending) {
    return (
      <div className="flex items-center justify-center py-12 text-slate-500" aria-busy="true">
        <Loader2 className="h-5 w-5 animate-spin" aria-hidden />
        <span className="sr-only">Loading consent</span>
      </div>
    );
  }
  if (query.isError && !query.data) {
    return <LoadFailed what="This member's consent" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />;
  }
  const consent = query.data;
  const stateOf = (channel: ConsentChannel, purpose: ConsentPurpose) =>
    consent.current.find((s) => s.channel === channel && s.purpose === purpose)
    ?? { channel, purpose, state: 'unknown' as const };

  return (
    <div className="space-y-4">
      {query.isError && (
        <LoadFailed stale what="This member's consent" error={query.error} onRetry={() => void query.refetch()} retrying={query.isFetching} />
      )}
      <div role="note" className="flex items-start gap-3 rounded-xl border border-indigo-200 bg-indigo-50 px-4 py-3 text-sm text-indigo-900">
        <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
        <p>
          Marketing goes only to channels marked <strong>Agreed</strong>; &ldquo;Not asked&rdquo; counts as no. Service
          messages (statements, bookings, receipts) are sent whatever is recorded here, since the membership needs them.
          CadenceE sends email only: SMS and post are recorded for the club&apos;s own mailings.
        </p>
      </div>

      <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm" aria-label="Consent by channel and purpose">
          <thead className="border-b border-slate-200 bg-slate-50">
            <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
              <th className="px-4 py-3">Channel</th>
              {CONSENT_PURPOSES.map((purpose) => (
                <th key={purpose} className="px-4 py-3">{CONSENT_PURPOSE_LABEL[purpose]}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {CONSENT_CHANNELS.map((channel) => (
              <tr key={channel} className="border-b border-slate-100 align-top">
                <th scope="row" className="px-4 py-3 text-left font-medium text-slate-900">{CONSENT_CHANNEL_LABEL[channel]}</th>
                {CONSENT_PURPOSES.map((purpose) => {
                  const s = stateOf(channel, purpose);
                  return (
                    <td key={purpose} className="px-4 py-3" data-testid={`consent-${channel}-${purpose}`}>
                      <div className="flex flex-wrap items-center gap-2">
                        <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${STATE_BADGE[s.state]}`}>{STATE_LABEL[s.state]}</span>
                        <WriteOnly permission={PERMISSION.ClubMembers}>
                          <button
                            type="button"
                            onClick={() => setEditing({ channel, purpose })}
                            className="text-xs font-medium text-indigo-700 hover:underline"
                            aria-label={`Change ${CONSENT_PURPOSE_LABEL[purpose].toLowerCase()} by ${CONSENT_CHANNEL_LABEL[channel]}`}
                          >
                            Change
                          </button>
                        </WriteOnly>
                      </div>
                      {s.state !== 'unknown' && (
                        <p className="mt-1 text-xs text-slate-500">
                          {s.source ? CONSENT_SOURCE_LABEL[s.source] : ''}
                          {s.recordedAtUtc ? ` · ${formatDate(s.recordedAtUtc)}` : ''}
                          {s.recordedByName ? ` · ${s.recordedByName}` : ''}
                        </p>
                      )}
                      {purpose === 'service' && (
                        <p className="mt-1 text-[11px] text-slate-500">Sent regardless</p>
                      )}
                    </td>
                  );
                })}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <section aria-labelledby="consent-history" className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
        <h3 id="consent-history" className="mb-2 font-semibold text-slate-900">History</h3>
        {consent.history.length === 0 ? (
          <p className="text-sm text-slate-500">Nothing recorded yet.</p>
        ) : (
          <ul className="divide-y divide-slate-100 text-sm">
            {consent.history.map((h) => (
              <li key={h.id} className="py-2">
                <p className="text-slate-900">
                  {h.granted ? 'Agreed to' : 'Said no to'} {CONSENT_PURPOSE_LABEL[h.purpose].toLowerCase()} by {CONSENT_CHANNEL_LABEL[h.channel]}
                  {!h.isCurrent && <span className="ml-2 text-xs text-slate-500">(replaced since)</span>}
                </p>
                <p className="text-xs text-slate-500">
                  {CONSENT_SOURCE_LABEL[h.source]} · {formatDate(h.recordedAtUtc)}
                  {h.recordedByName ? ` · ${h.recordedByName}` : ''}
                  {h.address ? ` · ${h.address}` : ''}
                </p>
                {h.detail && <p className="mt-0.5 text-xs text-slate-600">{h.detail}</p>}
              </li>
            ))}
          </ul>
        )}
      </section>

      {editing && (
        <ConsentDialog
          memberId={memberId}
          channel={editing.channel}
          purpose={editing.purpose}
          current={stateOf(editing.channel, editing.purpose)}
          onClose={() => setEditing(null)}
        />
      )}
    </div>
  );
}

function ConsentDialog({
  memberId, channel, purpose, current, onClose,
}: {
  memberId: string;
  channel: ConsentChannel;
  purpose: ConsentPurpose;
  current: ConsentStateRow;
  onClose: () => void;
}) {
  const client = useQueryClient();
  const ws = useWorkspaceScope();
  const [granted, setGranted] = useState<boolean | null>(current.state === 'unknown' ? null : current.state === 'granted');
  const [source, setSource] = useState<RecordableConsentSource>('member');
  const [detail, setDetail] = useState('');
  const [saving, setSaving] = useState(false);

  const save = async () => {
    if (granted === null) {
      toast.error('Say whether the member agreed.');
      return;
    }
    setSaving(true);
    try {
      const saved = await recordMemberConsent(memberId, { channel, purpose, granted, source, detail: detail.trim() || undefined });
      client.setQueryData<MemberConsent>(clubKeys.memberConsent(ws, memberId), saved);
      toast.success('Consent recorded');
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The consent could not be recorded.');
    } finally {
      setSaving(false);
    }
  };

  const what = `${CONSENT_PURPOSE_LABEL[purpose].toLowerCase()} by ${CONSENT_CHANNEL_LABEL[channel]}`;
  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Consent to {what}</DialogTitle>
          <DialogDescription>
            Record what the member said. The answer it replaces stays in the history.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <fieldset>
            <legend className="mb-1 text-sm font-medium text-slate-700">Did the member agree?</legend>
            <div className="flex gap-2">
              <Button type="button" variant={granted === true ? 'default' : 'outline'} aria-pressed={granted === true} onClick={() => setGranted(true)}>
                Agreed
              </Button>
              <Button type="button" variant={granted === false ? 'default' : 'outline'} aria-pressed={granted === false} onClick={() => setGranted(false)}>
                Said no
              </Button>
            </div>
          </fieldset>
          <div>
            <Label htmlFor="consent-source">How was it given?</Label>
            <Select value={source} onValueChange={(v) => setSource(v as RecordableConsentSource)}>
              <SelectTrigger id="consent-source" aria-label="How was it given?">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {RECORDABLE_SOURCES.map((s) => (
                  <SelectItem key={s} value={s}>{CONSENT_SOURCE_LABEL[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="consent-detail">Note</Label>
            <Textarea
              id="consent-detail"
              aria-label="Note"
              value={detail}
              onChange={(e) => setDetail(e.target.value)}
              rows={3}
              maxLength={500}
              placeholder="What was agreed and where it is kept, e.g. the signed form in the member's file"
            />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button onClick={save} disabled={saving}>
            {saving ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden /> : 'Record'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

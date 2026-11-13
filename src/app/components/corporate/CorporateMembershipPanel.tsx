// One corporate membership on its company's page: its seats and the nominees in them, whose
// account their charges go on, its dues on the company's account, and its renewal. Whoever runs
// the members (club.members) seats, swaps and removes nominees and renews or ends it; dues are
// money.manage's. The server holds the rules (one seat per member, no more nominees than seats);
// this shows its refusals as they are.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { toast } from 'sonner';
import { AlertTriangle, ArrowLeftRight, CalendarClock, Loader2, RefreshCw, UserMinus, UserPlus, XCircle } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Badge } from '@/app/components/ui/badge';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import {
  addNominee, endCorporateMembership, markCorporateNotRenewing, NOMINEE_CHARGES_LABELS, removeNominee, renewCorporateMembership,
  RENEWAL_STATE_LABELS, setCorporateDues, swapNominee, type CorporateMembership, type CorporateNominee,
} from '@/app/api/corporate';
import type { CatalogCadence } from '@/app/api/catalog';
import { CADENCE_LABEL } from '@/app/api/catalog';
import { useClubMembers } from '@/app/hooks/queries';
import { useFormat } from '@/app/i18n';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { errorText, fieldClass, SectionTitle, useClubAmount } from './parts';

const CADENCES: CatalogCadence[] = ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual'];

type Ask =
  | { kind: 'remove'; nominee: CorporateNominee }
  | { kind: 'swap'; nominee: CorporateNominee }
  | { kind: 'not-renewing' }
  | { kind: 'end' }
  | { kind: 'dues' };

export function CorporateMembershipPanel({ membership, readOnly = false, onChanged }: {
  membership: CorporateMembership;
  readOnly?: boolean;
  /** After a change, with the membership as the server now has it. */
  onChanged?: (saved: CorporateMembership) => void;
}) {
  const f = useFormat();
  const amount = useClubAmount();
  const members = useClubMembers();
  const [ask, setAsk] = useState<Ask | null>(null);
  const [reason, setReason] = useState('');
  const [memberId, setMemberId] = useState('');
  const [seatFor, setSeatFor] = useState('');
  const [duesAmount, setDuesAmount] = useState('');
  const [cadence, setCadence] = useState<CatalogCadence>('Annual');
  const [busy, setBusy] = useState(false);

  const m = membership;
  const ended = m.status === 'Ended';
  const editable = !readOnly && !ended;
  const seated = new Set(m.nominees.map((n) => n.memberId));
  // Who could take a seat: members in good standing not in one of these seats (the server
  // refuses one seated on another company's, and says so).
  const candidates = (members.data ?? [])
    .filter((x) => !seated.has(x.id) && (x.status === 'Active' || x.status === 'Pending'))
    .sort((a, b) => `${a.lastName} ${a.firstName}`.localeCompare(`${b.lastName} ${b.firstName}`));
  const full = m.seatsUsed >= m.seats;

  const run = async (what: string, action: () => Promise<CorporateMembership>, done: (saved: CorporateMembership) => string) => {
    setBusy(true);
    try {
      const saved = await action();
      toast.success(done(saved));
      setAsk(null);
      setReason('');
      setMemberId('');
      onChanged?.(saved);
    } catch (err) {
      if (isEditConflict(err)) {
        showEditConflict(err.message, () => onChanged?.(m));
        return;
      }
      toast.error(errorText(err, `${what} did not go through.`));
    } finally {
      setBusy(false);
    }
  };

  const seat = () => {
    if (!seatFor) return;
    void run('The seat', () => addNominee(m.id, seatFor), (saved) => {
      setSeatFor('');
      return `${saved.nominees.find((n) => n.memberId === seatFor)?.memberName ?? 'The member'} holds a seat on ${saved.name} now.`;
    });
  };

  const confirm = () => {
    if (!ask) return;
    const why = reason.trim();
    switch (ask.kind) {
      case 'remove':
        void run('Removing the nominee', () => removeNominee(m.id, ask.nominee.id, why || undefined),
          () => `${ask.nominee.memberName} no longer holds a seat.`);
        break;
      case 'swap':
        if (!memberId) return;
        void run('The swap', () => swapNominee(m.id, ask.nominee.id, memberId, why || undefined),
          (saved) => `${saved.nominees.find((n) => n.memberId === memberId)?.memberName ?? 'The member'} has ${ask.nominee.memberName}’s seat.`);
        break;
      case 'not-renewing':
        if (!why) return;
        void run('Marking it not renewing', () => markCorporateNotRenewing(m.id, why, m.version), () => `${m.name} is marked not renewing.`);
        break;
      case 'end':
        if (!why) return;
        void run('Ending the membership', () => endCorporateMembership(m.id, why), () => `${m.name} has ended; its nominees left their seats.`);
        break;
      case 'dues': {
        const value = Number(duesAmount.replace(',', '.'));
        if (!Number.isFinite(value) || value <= 0) return;
        void run('The dues', () => setCorporateDues(m.id, { amount: value, cadence }), () => `${m.name}’s dues are set.`);
        break;
      }
    }
  };

  const renew = () => void run('The renewal', () => renewCorporateMembership(m.id, m.version),
    (saved) => `${saved.name} is renewed until ${f.date(saved.renewsAtUtc)}.`);

  return (
    <article aria-label={m.name} className="space-y-3 rounded-xl border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <p className="font-semibold text-slate-900 dark:text-slate-100">{m.name}</p>
          <p className="text-xs text-slate-500">{NOMINEE_CHARGES_LABELS[m.nomineeCharges] ?? m.nomineeCharges}</p>
        </div>
        <div className="flex flex-wrap items-center gap-1.5">
          <Badge variant="outline">{m.status}</Badge>
          <Badge variant="outline" className={m.renewalState === 'overdue' || m.renewalState === 'not_renewing' ? 'border-rose-300 text-rose-700' : undefined}>
            {RENEWAL_STATE_LABELS[m.renewalState] ?? m.renewalState}
          </Badge>
        </div>
      </div>

      <dl className="grid grid-cols-2 gap-2 text-xs">
        <div>
          <dt className="text-slate-500">Seats</dt>
          <dd className="font-medium text-slate-900 dark:text-slate-100">{m.seatsUsed} of {m.seats} taken</dd>
        </div>
        <div>
          <dt className="text-slate-500">{ended ? 'Ended' : 'Renews'}</dt>
          <dd className="font-medium text-slate-900 dark:text-slate-100">
            {ended && m.endedAtUtc ? f.date(m.endedAtUtc) : f.date(m.renewsAtUtc)}
          </dd>
        </div>
        <div className="col-span-2">
          <dt className="text-slate-500">Dues</dt>
          <dd className="font-medium text-slate-900 dark:text-slate-100">
            {m.dues
              ? `${amount(m.dues.amount, m.dues.currency)} a ${CADENCE_LABEL[m.dues.cadence as CatalogCadence] ?? m.dues.cadence} · ${m.dues.status}${m.dues.nextChargeAtUtc ? ` · next ${f.date(m.dues.nextChargeAtUtc)}` : ''}`
              : 'None on the company’s account'}
          </dd>
        </div>
      </dl>
      {m.notRenewing && m.notRenewingReason && (
        <p className="flex items-start gap-1.5 text-xs text-rose-700"><XCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" aria-hidden="true" /> Not renewing: {m.notRenewingReason}</p>
      )}

      <div>
        <SectionTitle>Nominees</SectionTitle>
        {m.nominees.length === 0 ? (
          <p className="text-slate-600">Nobody holds a seat yet.</p>
        ) : (
          <ul className="space-y-1.5">
            {m.nominees.map((n) => (
              <li key={n.id} className="flex flex-wrap items-center justify-between gap-2">
                <span>
                  <Link to={`/members/${n.memberId}`} className="font-medium text-slate-900 hover:text-amber-800 dark:text-slate-100">{n.memberName}</Link>
                  <span className="ml-1.5 text-xs text-slate-500">since {f.date(n.addedAtUtc)}</span>
                  {n.ownDuesRunning && (
                    <span className="ml-1.5 inline-flex items-center gap-1 text-xs text-amber-700">
                      <AlertTriangle className="h-3 w-3" aria-hidden="true" /> their own dues still run
                    </span>
                  )}
                </span>
                {editable && (
                  <WriteOnly permission={PERMISSION.ClubMembers}>
                    <span className="flex gap-1">
                      <Button type="button" size="sm" variant="ghost" className="h-7 gap-1 px-2" aria-label={`Swap ${n.memberName}`}
                        onClick={() => { setAsk({ kind: 'swap', nominee: n }); setMemberId(''); setReason(''); }}>
                        <ArrowLeftRight className="h-3.5 w-3.5" aria-hidden="true" /> Swap
                      </Button>
                      <Button type="button" size="sm" variant="ghost" className="h-7 gap-1 px-2 text-rose-700" aria-label={`Remove ${n.memberName}`}
                        onClick={() => { setAsk({ kind: 'remove', nominee: n }); setReason(''); }}>
                        <UserMinus className="h-3.5 w-3.5" aria-hidden="true" /> Remove
                      </Button>
                    </span>
                  </WriteOnly>
                )}
              </li>
            ))}
          </ul>
        )}
        {m.formerNominees.length > 0 && (
          <details className="mt-2 text-xs text-slate-500">
            <summary className="cursor-pointer">Former nominees ({m.formerNominees.length})</summary>
            <ul className="mt-1 space-y-0.5">
              {m.formerNominees.map((n) => (
                <li key={n.id}>{n.memberName}: {f.date(n.addedAtUtc)} to {n.removedAtUtc ? f.date(n.removedAtUtc) : '—'}{n.removedReason ? ` (${n.removedReason})` : ''}</li>
              ))}
            </ul>
          </details>
        )}
      </div>

      {editable && (
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <div className="flex flex-wrap items-end gap-2">
            <div className="min-w-[12rem] flex-1">
              <Label htmlFor={`seat-${m.id}`} className="text-xs text-slate-600">Give a seat to</Label>
              <select id={`seat-${m.id}`} className={fieldClass} value={seatFor} onChange={(e) => setSeatFor(e.target.value)} disabled={full || busy}>
                <option value="">{full ? 'Every seat is taken' : 'Choose a member…'}</option>
                {candidates.map((x) => <option key={x.id} value={x.id}>{x.firstName} {x.lastName}</option>)}
              </select>
            </div>
            <Button type="button" size="sm" onClick={seat} disabled={!seatFor || full || busy} className="gap-1.5">
              {busy ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" /> : <UserPlus className="h-4 w-4" aria-hidden="true" />} Seat
            </Button>
          </div>
          <div className="flex flex-wrap gap-2 border-t border-slate-100 pt-3 dark:border-slate-800">
            <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={renew} disabled={busy}>
              <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" /> Renew
            </Button>
            {!m.notRenewing && (
              <Button type="button" size="sm" variant="outline" className="gap-1.5" onClick={() => { setAsk({ kind: 'not-renewing' }); setReason(''); }} disabled={busy}>
                <CalendarClock className="h-3.5 w-3.5" aria-hidden="true" /> Not renewing
              </Button>
            )}
            <WriteOnly permission={PERMISSION.ManageMoney}>
              <Button type="button" size="sm" variant="outline" onClick={() => { setAsk({ kind: 'dues' }); setDuesAmount(m.dues ? String(m.dues.amount) : ''); }} disabled={busy}>
                {m.dues ? 'Change dues' : 'Set dues'}
              </Button>
            </WriteOnly>
            <Button type="button" size="sm" variant="outline" className="gap-1.5 text-rose-700" onClick={() => { setAsk({ kind: 'end' }); setReason(''); }} disabled={busy}>
              <XCircle className="h-3.5 w-3.5" aria-hidden="true" /> End
            </Button>
          </div>
        </WriteOnly>
      )}

      <Dialog open={ask !== null} onOpenChange={(open) => { if (!open) setAsk(null); }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>
              {ask?.kind === 'remove' && `Remove ${ask.nominee.memberName} from their seat`}
              {ask?.kind === 'swap' && `Give ${ask.nominee.memberName}’s seat to someone else`}
              {ask?.kind === 'not-renewing' && `${m.name} is not renewing`}
              {ask?.kind === 'end' && `End ${m.name}`}
              {ask?.kind === 'dues' && `Dues for ${m.name}`}
            </DialogTitle>
          </DialogHeader>
          <div className="space-y-3">
            {ask?.kind === 'swap' && (
              <div>
                <Label htmlFor={`swap-${m.id}`}>The member who takes the seat</Label>
                <select id={`swap-${m.id}`} className={fieldClass} value={memberId} onChange={(e) => setMemberId(e.target.value)}>
                  <option value="">Choose a member…</option>
                  {candidates.map((x) => <option key={x.id} value={x.id}>{x.firstName} {x.lastName}</option>)}
                </select>
              </div>
            )}
            {ask?.kind === 'dues' ? (
              <div className="grid grid-cols-2 gap-2">
                <div>
                  <Label htmlFor={`dues-amount-${m.id}`}>Amount</Label>
                  <Input id={`dues-amount-${m.id}`} inputMode="decimal" value={duesAmount} onChange={(e) => setDuesAmount(e.target.value)} />
                </div>
                <div>
                  <Label htmlFor={`dues-cadence-${m.id}`}>Every</Label>
                  <select id={`dues-cadence-${m.id}`} className={fieldClass} value={cadence} onChange={(e) => setCadence(e.target.value as CatalogCadence)}>
                    {CADENCES.map((c) => <option key={c} value={c}>{CADENCE_LABEL[c]}</option>)}
                  </select>
                </div>
                <p className="col-span-2 text-xs text-slate-500">Billed to the company’s own house account, as a subscription.</p>
              </div>
            ) : ask && (
              <div>
                <Label htmlFor={`reason-${m.id}`}>{ask.kind === 'remove' || ask.kind === 'swap' ? 'Why (optional)' : 'Why'}</Label>
                <Input id={`reason-${m.id}`} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={500} />
                {ask.kind === 'end' && <p className="mt-1 text-xs text-slate-500">Its nominees leave their seats and its dues stop.</p>}
              </div>
            )}
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setAsk(null)} disabled={busy}>Cancel</Button>
            <Button onClick={confirm} disabled={busy
              || (ask?.kind === 'swap' && !memberId)
              || ((ask?.kind === 'not-renewing' || ask?.kind === 'end') && !reason.trim())
              || (ask?.kind === 'dues' && !(Number(duesAmount.replace(',', '.')) > 0))}>
              {busy ? 'Saving…' : 'Confirm'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </article>
  );
}

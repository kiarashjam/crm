// A member's online payments on their page: the card they saved (with a way to remove it),
// whether their dues are charged to it, a link to pay what they owe, a charge to the card
// now, and what Stripe said of each attempt, failures included.
//
// Nothing here works without a Stripe key, and the card says so rather than offering
// buttons the server would refuse. A card saved before the key was removed still shows, and
// can still be removed.

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { CreditCard, Loader2, Mail, Trash2, Zap } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Switch } from '@/app/components/ui/switch';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { WriteOnly } from '@/app/components/WriteOnly';
import { LoadFailed } from '@/app/components/LoadFailed';
import { ApiError } from '@/app/api/apiClient';
import { PERMISSION } from '@/app/api/organizations';
import type { Charge } from '@/app/api/charges';
import { chargeSavedCard, removeSavedCard, setAutoPay, type MemberPaymentProfile } from '@/app/api/payments';
import { useMemberPaymentProfile, usePaymentStatus } from '@/app/hooks/queries';
import { useRegional } from '@/app/hooks/useMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { PayOnlineDialog } from './PayOnlineDialog';
import { ONLINE_STATUS_BADGE, ONLINE_STATUS_LABEL, PURPOSE_LABEL, describeCard } from './paymentLabels';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

export interface MemberPaymentsCardProps {
  memberId: string;
  memberName: string;
  memberEmail?: string;
  /** Their charges still owing: a pay link can be for some of them. */
  openCharges: Charge[];
  /** What they owe now, to say what charging the card would take. */
  owed: number;
}

export function MemberPaymentsCard({ memberId, memberName, memberEmail, openCharges, owed }: MemberPaymentsCardProps) {
  const statusQuery = usePaymentStatus();
  const profileQuery = useMemberPaymentProfile(memberId);
  const status = statusQuery.data;
  const profile = profileQuery.data;
  const online = status?.onlinePayments === true;

  return (
    <section className="mb-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" aria-label="Online payments">
      <h3 className="mb-3 inline-flex items-center gap-2 font-semibold text-slate-900">
        <CreditCard className="h-4 w-4 text-indigo-600" /> Online payments
      </h3>
      {profileQuery.isError && !profile ? (
        <LoadFailed what="This member's online payments" error={profileQuery.error} onRetry={() => void profileQuery.refetch()} retrying={profileQuery.isFetching} />
      ) : !profile || !status ? (
        <Loader2 className="h-5 w-5 animate-spin text-slate-400" aria-label="Loading online payments" />
      ) : (
        <ProfileBody
          profile={profile}
          online={online}
          memberId={memberId}
          memberName={memberName}
          memberEmail={memberEmail}
          openCharges={openCharges}
          owed={owed}
          reload={async () => { await profileQuery.refetch(); }}
        />
      )}
    </section>
  );
}

function ProfileBody({
  profile, online, memberId, memberName, memberEmail, openCharges, owed, reload,
}: MemberPaymentsCardProps & { profile: MemberPaymentProfile; online: boolean; reload: () => Promise<void> }) {
  const { locale } = useRegional();
  const [dialog, setDialog] = useState<null | 'pay' | 'setup'>(null);
  const [agreeing, setAgreeing] = useState(false);
  const [busy, setBusy] = useState<null | 'remove' | 'autopay' | 'charge'>(null);
  const card = profile.card;

  const act = async (what: 'remove' | 'autopay' | 'charge', run: () => Promise<unknown>, done: string, failed: string) => {
    setBusy(what);
    try {
      await run();
      toast.success(done);
    } catch (err) {
      toast.error(message(err, failed));
      // Changed by someone else meanwhile: read what they did.
      if (err instanceof ApiError && err.status === 409) await reload();
    } finally {
      setBusy(null);
    }
  };

  const remove = () => {
    if (!card || !window.confirm(`Remove ${describeCard(card)}? Stripe forgets it, and dues are no longer charged to it.`)) return;
    void act('remove', () => removeSavedCard(memberId), 'Card removed', 'Could not remove the card');
  };

  const autopayOff = () =>
    void act('autopay', () => setAutoPay(memberId, false, false, profile.version), 'Dues are no longer charged to the card', 'Could not change how dues are paid');

  const autopayOn = () => {
    setAgreeing(false);
    void act('autopay', () => setAutoPay(memberId, true, true, profile.version), 'Dues are charged to the card from now on', 'Could not change how dues are paid');
  };

  const chargeNow = () => {
    if (!card || !window.confirm(`Charge ${describeCard(card)} for what ${memberName} owes now?`)) return;
    setBusy('charge');
    chargeSavedCard(memberId)
      .then((attempt) => {
        if (attempt.status === 'succeeded') toast.success(`Paid: ${formatClubMoney(attempt.amount, attempt.currency, locale)}`);
        else if (attempt.status === 'failed') toast.error(attempt.failureMessage ?? 'The card was declined.');
        else toast.success('Stripe is processing the payment; it reaches the account when Stripe confirms it.');
      })
      .catch((err: unknown) => toast.error(message(err, 'Could not charge the card')))
      .finally(() => setBusy(null));
  };

  return (
    <div className="space-y-4 text-sm">
      {!online && (
        <p className="rounded-lg bg-amber-50 px-3 py-2 text-amber-900">
          Online payments are not set up in this workspace.{' '}
          <Link className="font-medium underline" to="/hospitality-settings?tab=payments">Set up Stripe in Club settings, Payments</Link>.
        </p>
      )}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="font-medium text-slate-800">{card ? describeCard(card) : 'No saved card'}</p>
          <p className="text-xs text-slate-500">
            {card
              ? `Saved ${card.savedAtUtc ? formatDateTime(card.savedAtUtc) : ''} on Stripe's page; the club keeps only Stripe's ids.`
              : 'A card is saved by the member, on Stripe\'s page, from a link the club sends.'}
          </p>
        </div>
        <WriteOnly permission={PERMISSION.ManageMoney}>
          <div className="flex flex-wrap gap-2">
            {card ? (
              <Button type="button" size="sm" variant="outline" onClick={remove} disabled={busy !== null}>
                {busy === 'remove' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Trash2 className="mr-1 h-3.5 w-3.5" />} Remove card
              </Button>
            ) : online ? (
              <Button type="button" size="sm" variant="outline" onClick={() => setDialog('setup')}>
                <Mail className="mr-1 h-3.5 w-3.5" /> Send a link to save a card
              </Button>
            ) : null}
          </div>
        </WriteOnly>
      </div>

      {card && (
        <div className="flex items-start gap-3">
          <WriteOnly permission={PERMISSION.ManageMoney}>
            <Switch
              aria-label="Charge dues to the saved card"
              checked={profile.autoPayDues}
              disabled={busy !== null || (!profile.autoPayDues && !online)}
              onCheckedChange={(on) => (on ? setAgreeing(true) : autopayOff())}
            />
          </WriteOnly>
          <div>
            <p className="font-medium text-slate-800">
              {profile.autoPayDues ? 'Dues are charged to this card' : 'Dues are not charged to this card'}
            </p>
            <p className="text-xs text-slate-500">
              {profile.autoPayDues
                ? `Agreed ${profile.autoPayAgreedAtUtc ? formatDateTime(profile.autoPayAgreedAtUtc) : ''}${profile.autoPayAgreedBy ? ` (${profile.autoPayAgreedBy})` : ''}. Each new dues charge is charged once; a declined card is listed below and on Payments & bank, and the charge stays owing.`
                : 'With the member\'s agreement, each new dues charge is charged to the card once.'}
            </p>
          </div>
        </div>
      )}

      {online && (
        <WriteOnly permission={PERMISSION.ManageMoney}>
          <div className="flex flex-wrap gap-2">
            <Button type="button" size="sm" onClick={() => setDialog('pay')}>
              <CreditCard className="mr-1 h-3.5 w-3.5" /> Pay online…
            </Button>
            {card && owed > 0 && (
              <Button type="button" size="sm" variant="outline" onClick={chargeNow} disabled={busy !== null}>
                {busy === 'charge' ? <Loader2 className="mr-1 h-3.5 w-3.5 animate-spin" /> : <Zap className="mr-1 h-3.5 w-3.5" />} Charge the card now
              </Button>
            )}
          </div>
        </WriteOnly>
      )}

      {profile.payments.length > 0 && (
        <div>
          <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Attempts</p>
          <ul className="divide-y divide-slate-100 rounded-lg border border-slate-200">
            {profile.payments.slice(0, 8).map((p) => (
              <li key={p.id} className="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
                <span className="min-w-0">
                  <span className="block truncate text-slate-800">{PURPOSE_LABEL[p.purpose] ?? p.purpose} · {formatDateTime(p.createdAtUtc)}</span>
                  {p.status === 'failed' && p.failureMessage && <span className="block text-xs text-rose-700">{p.failureMessage}</span>}
                </span>
                <span className="flex items-center gap-2">
                  {p.purpose !== 'setup' && <span className="font-medium">{formatClubMoney(p.amount, p.currency, locale)}</span>}
                  <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${ONLINE_STATUS_BADGE[p.status] ?? ''}`}>{ONLINE_STATUS_LABEL[p.status] ?? p.status}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}

      <PayOnlineDialog
        open={dialog !== null}
        onOpenChange={(open) => { if (!open) setDialog(null); }}
        memberId={memberId}
        memberName={memberName}
        canEmail={!!memberEmail}
        openCharges={openCharges}
        purpose={dialog === 'setup' ? 'setup' : 'pay'}
      />

      <Dialog open={agreeing} onOpenChange={setAgreeing}>
        <DialogContent className="max-w-md">
          <DialogHeader>
            <DialogTitle>Charge {memberName}&apos;s dues to the card?</DialogTitle>
            <DialogDescription>
              Only with their agreement. Confirming records that {memberName} agreed, with your name and the time; each new
              dues charge is then charged to {card ? describeCard(card) : 'the card'} once.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter className="gap-2 sm:gap-2">
            <Button type="button" variant="outline" onClick={() => setAgreeing(false)}>Cancel</Button>
            <Button type="button" onClick={autopayOn}>{memberName} agreed: charge dues to the card</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

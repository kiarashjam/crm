// A link for a member to pay online: what they owe now, or charges chosen here, on Stripe's
// own page. Emailed to them through the outbox, or made here to copy into a message.
//
// The link carries no amount: it is worked out when the member opens it, so a payment made
// meanwhile is not asked for again. Stripe's page offers what the club set up (a card,
// TWINT in CHF, SEPA debit in EUR); the payment reaches the ledger when Stripe confirms it.

import { useState } from 'react';
import { Copy, Link2, Loader2, Mail } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Checkbox } from '@/app/components/ui/checkbox';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/app/components/ui/dialog';
import { createPayLink, emailPayLink, type PayLink, type PayLinkInput } from '@/app/api/payments';
import type { Charge } from '@/app/api/charges';
import { useRegional } from '@/app/hooks/useMoney';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { formatClubMoney } from '@/app/lib/clubMoney';
import { currentDateLocale } from '@/app/i18n/current';

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export interface PayOnlineDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  memberId: string;
  memberName: string;
  /** Whether the member has an email address the link can go to. */
  canEmail: boolean;
  /** The member's charges still owing, to choose from; none: the link is for what they owe. */
  openCharges?: Charge[];
  /** What the link is for: paying, or saving a card (with dues charged to it, if asked). */
  purpose?: 'pay' | 'setup';
}

function formatDay(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(currentDateLocale(), { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
}

export function PayOnlineDialog({ open, onOpenChange, memberId, memberName, canEmail, openCharges = [], purpose = 'pay' }: PayOnlineDialogProps) {
  const { locale } = useRegional();
  const { format } = useClubMoney();
  const [chosen, setChosen] = useState<string[]>([]);
  const [autoPay, setAutoPay] = useState(false);
  const [busy, setBusy] = useState<null | 'email' | 'link'>(null);
  const [link, setLink] = useState<PayLink | null>(null);

  const input = (): PayLinkInput => purpose === 'setup'
    ? { purpose: 'setup', autoPay }
    : chosen.length > 0 ? { purpose: 'charges', chargeIds: chosen } : { purpose: 'account' };

  const close = (next: boolean) => {
    if (!next) {
      setChosen([]);
      setAutoPay(false);
      setLink(null);
    }
    onOpenChange(next);
  };

  const send = async () => {
    setBusy('email');
    try {
      const sent = await emailPayLink(memberId, input());
      if (sent.emailStatus === 'suppressed') {
        // Not sent (the address unsubscribed from everything, say): the link is still made, to pass on another way.
        toast.error(sent.emailReason ?? 'The email was not sent.');
        setLink(sent);
      } else {
        toast.success(`Link emailed to ${memberName}`);
        close(false);
      }
    } catch (err) {
      toast.error(message(err, 'Could not send the link'));
    } finally {
      setBusy(null);
    }
  };

  const make = async () => {
    setBusy('link');
    try {
      setLink(await createPayLink(memberId, input()));
    } catch (err) {
      toast.error(message(err, 'Could not make the link'));
    } finally {
      setBusy(null);
    }
  };

  const copy = async () => {
    if (!link) return;
    try {
      await navigator.clipboard.writeText(link.url);
      toast.success('Link copied');
    } catch {
      toast.error('Could not copy: select the link and copy it.');
    }
  };

  const toggle = (id: string, on: boolean) => setChosen((ids) => (on ? [...ids, id] : ids.filter((x) => x !== id)));
  const setup = purpose === 'setup';

  return (
    <Dialog open={open} onOpenChange={close}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{setup ? `A link for ${memberName} to save a card` : `${memberName} pays online`}</DialogTitle>
          <DialogDescription>
            {setup
              ? "They enter the card on Stripe's own page; the club keeps only Stripe's ids and the last four digits."
              : "They pay on Stripe's own page. The amount is worked out when they open the link, so a payment made meanwhile is not asked for again."}
          </DialogDescription>
        </DialogHeader>

        {link ? (
          <div className="space-y-3">
            <Label htmlFor="pay-link-url" className="text-xs">The link</Label>
            <div className="flex gap-2">
              <Input id="pay-link-url" readOnly value={link.url} onFocus={(e) => e.currentTarget.select()} />
              <Button type="button" variant="outline" onClick={copy}>
                <Copy className="mr-1 h-4 w-4" /> Copy
              </Button>
            </div>
            <p className="text-sm text-slate-600">
              {link.purpose === 'setup'
                ? `Valid until ${formatDay(link.expiresAtUtc)}.`
                : `${formatClubMoney(link.amount, link.currency, locale)} owed now. Valid until ${formatDay(link.expiresAtUtc)}.`}
            </p>
          </div>
        ) : setup ? (
          <label className="flex items-start gap-3 text-sm">
            <Checkbox checked={autoPay} onCheckedChange={(v) => setAutoPay(v === true)} aria-label="Ask them to agree to dues charged to the card" />
            <span>
              <span className="font-medium text-slate-800">Ask them to agree to their dues being charged to it</span>
              <span className="block text-xs text-slate-500">
                Stripe&apos;s page says the card will be charged for future dues; once saved, each new dues charge is charged
                to it once.
              </span>
            </span>
          </label>
        ) : openCharges.length > 0 ? (
          <fieldset className="space-y-2">
            <legend className="mb-1 text-sm text-slate-600">
              For everything owed, or tick the charges the link is for:
            </legend>
            <ul className="max-h-56 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
              {openCharges.map((c) => (
                <li key={c.id}>
                  <label className="flex items-center gap-3 rounded px-2 py-1 text-sm hover:bg-slate-50">
                    <Checkbox
                      checked={chosen.includes(c.id)}
                      onCheckedChange={(v) => toggle(c.id, v === true)}
                      aria-label={c.incurredByName ? `Pay ${c.description}, incurred by ${c.incurredByName}` : `Pay ${c.description}`}
                    />
                    {/* A household member's bill rolled up to this account says whose it is, as the
                        account's charges list does, so a link can be made for the right ones. */}
                    <span className="min-w-0 flex-1">
                      <span className="block truncate">{c.description}</span>
                      {c.incurredByName && <span className="block truncate text-xs text-teal-700">Incurred by {c.incurredByName}</span>}
                    </span>
                    <span className="shrink-0 font-medium">{format(c.amount - c.paidAmount)}</span>
                  </label>
                </li>
              ))}
            </ul>
          </fieldset>
        ) : null}

        <DialogFooter className="gap-2 sm:gap-2">
          {link ? (
            <Button type="button" onClick={() => close(false)}>Done</Button>
          ) : (
            <>
              <Button type="button" variant="outline" onClick={make} disabled={busy !== null}>
                {busy === 'link' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Link2 className="mr-1 h-4 w-4" />} Make a link to copy
              </Button>
              {canEmail && (
                <Button type="button" onClick={send} disabled={busy !== null}>
                  {busy === 'email' ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Mail className="mr-1 h-4 w-4" />} Email it to them
                </Button>
              )}
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

import { useEffect, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/app/components/ui/dialog';
import type { ClubEvent } from '@/app/api/events';
import { addEventAttendee, addWalkIn, type EventAttendee, type TicketTier } from '@/app/api/eventAttendees';
import type { Member } from '@/app/api/members';

/** A refusal from the server (the event filled up, the member is already on the list) is said, not swallowed. */
export function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/** What the server did with a registration, as the person at the desk needs to hear it. */
export function registeredMessage(row: EventAttendee, money: (n: number) => string): string {
  if (row.status === 'Waitlisted') return `${row.memberName} is on the waitlist (#${row.waitlistPosition ?? '?'}): no ticket until a seat comes free`;
  const who = row.status === 'CheckedIn' ? `${row.memberName} is checked in` : `${row.memberName} has a seat`;
  if (row.chargeState === 'charged' && row.ticketPrice) {
    return `${who} · ticket ${money(row.ticketPrice)} on ${row.ticketTier === 'Guest' ? `${row.hostName ?? 'the host'}'s` : 'their'} house account`;
  }
  if (row.chargeState === 'to-collect' && row.ticketPrice) return `${who} · ${money(row.ticketPrice)} to collect: no member account to put it on`;
  return who;
}

/**
 * Puts someone on an event's list: a member by id, or a guest by name with the member who
 * brings them (whose house account the guest ticket goes on). A walk-in is checked in at once.
 * The server decides a seat or the waitlist, and posts the ticket.
 */
export default function RegisterDialog({
  event,
  members,
  open,
  walkIn = false,
  onOpenChange,
  onDone,
  money,
}: {
  event: ClubEvent;
  members: Member[];
  open: boolean;
  walkIn?: boolean;
  onOpenChange: (open: boolean) => void;
  onDone: (row: EventAttendee) => void | Promise<void>;
  money: (n: number) => string;
}) {
  const [tier, setTier] = useState<TicketTier>('Member');
  const [memberId, setMemberId] = useState('');
  const [guestName, setGuestName] = useState('');
  const [guestEmail, setGuestEmail] = useState('');
  const [hostId, setHostId] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (!open) return;
    setTier('Member');
    setMemberId(members[0]?.id ?? '');
    setGuestName('');
    setGuestEmail('');
    setHostId('');
  }, [open, members]);

  const full = event.capacity > 0 && (event.seatsLeft ?? event.capacity - event.registeredCount) <= 0;
  const price = (t: TicketTier) => (t === 'Member' ? event.memberPrice : event.guestPrice);
  const priceText = (t: TicketTier) => (price(t) === 0 ? 'Free' : money(price(t)));

  const submit = async () => {
    if (tier === 'Member' && !memberId) {
      toast.error('Pick a member');
      return;
    }
    if (tier === 'Guest' && !guestName.trim()) {
      toast.error('A guest needs a name');
      return;
    }
    const member = members.find((m) => m.id === memberId);
    const input = tier === 'Member'
      ? { memberId, memberName: member ? `${member.firstName} ${member.lastName}` : '', ticketTier: tier }
      : { memberName: guestName.trim(), email: guestEmail.trim() || undefined, ticketTier: tier, hostMemberId: hostId || undefined };
    setBusy(true);
    try {
      const row = walkIn ? await addWalkIn(event, input) : await addEventAttendee(event, input);
      toast.success(registeredMessage(row, money));
      onOpenChange(false);
      await onDone(row);
    } catch (err) {
      toast.error(errorText(err, walkIn ? 'Could not check them in' : 'Could not add them'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{walkIn ? 'Walk-in at the door' : `Register for ${event.name}`}</DialogTitle>
          <DialogDescription>
            {walkIn
              ? 'Checked in at once on a free seat; their ticket goes on a house account.'
              : full
                ? 'The event is full: they go on the waitlist and are charged when a seat comes free.'
                : 'A member, or a guest by name. A ticket with a price goes on a house account.'}
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div className="flex gap-2" role="radiogroup" aria-label="Ticket">
            {(['Member', 'Guest'] as const).map((t) => (
              <button
                key={t}
                type="button"
                role="radio"
                aria-checked={tier === t}
                onClick={() => setTier(t)}
                className={`flex-1 px-3 py-2 rounded-lg text-sm font-medium transition ${
                  tier === t ? 'bg-pink-100 text-pink-800 ring-2 ring-pink-400' : 'bg-slate-50 text-slate-600 hover:bg-slate-100'
                }`}
              >
                {t} · {priceText(t)}
              </button>
            ))}
          </div>
          {tier === 'Member' ? (
            <div>
              <Label htmlFor="register-member">Member</Label>
              <select
                id="register-member"
                value={memberId}
                onChange={(e) => setMemberId(e.target.value)}
                className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
              >
                {members.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.firstName} {m.lastName} — {m.tier}
                  </option>
                ))}
              </select>
            </div>
          ) : (
            <>
              <div>
                <Label htmlFor="register-guest-name">Guest name</Label>
                <Input id="register-guest-name" value={guestName} onChange={(e) => setGuestName(e.target.value)} />
              </div>
              <div>
                <Label htmlFor="register-guest-email">Guest email</Label>
                <Input id="register-guest-email" type="email" value={guestEmail} onChange={(e) => setGuestEmail(e.target.value)} />
              </div>
              <div>
                <Label htmlFor="register-host">Brought by</Label>
                <select
                  id="register-host"
                  value={hostId}
                  onChange={(e) => setHostId(e.target.value)}
                  className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm"
                >
                  <option value="">Nobody: collect the ticket at the door</option>
                  {members.map((m) => (
                    <option key={m.id} value={m.id}>
                      {m.firstName} {m.lastName}
                    </option>
                  ))}
                </select>
                <p className="mt-1 text-xs text-slate-500">The guest ticket goes on this member's house account.</p>
              </div>
            </>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={busy}>
            Cancel
          </Button>
          <Button onClick={submit} disabled={busy}>
            {busy ? <Loader2 className="w-4 h-4 mr-2 animate-spin" /> : null}
            {walkIn ? 'Check in' : full ? 'Add to waitlist' : 'Register'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

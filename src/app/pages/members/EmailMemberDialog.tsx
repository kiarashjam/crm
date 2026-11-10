import { useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import {
  CATEGORIES, defaultPurposeFor, sendMemberEmail,
  type CommCategory, type CommPurpose, type Communication,
} from '@/app/api/communications';
import type { Member } from '@/app/api/members';
import { useMemberConsent } from '@/app/hooks/queries/club';

/** Whether an email went: anything else was recorded with the reason it did not. */
export function wasSent(message: Communication): boolean {
  return message.status === 'queued' || message.status === 'sent' || message.status === 'delivered' || message.status === 'opened';
}

/**
 * Where the member stands on marketing by email, said before anything is sent: a
 * marketing message to a member who has not agreed is not sent, and the sender should
 * know that now rather than find a "Not sent" in the log.
 */
function MarketingConsentHint({ memberId, firstName }: { memberId: string; firstName: string }) {
  const consent = useMemberConsent(memberId);
  if (!memberId) return null;
  if (consent.isPending) return <p className="text-xs text-slate-500">Checking whether {firstName} agreed to marketing…</p>;
  if (consent.isError || !consent.data) return <p className="text-xs text-slate-500">Whether {firstName} agreed to marketing could not be read; the server checks as it sends.</p>;
  const state = consent.data.current.find((s) => s.channel === 'email' && s.purpose === 'marketing')?.state ?? 'unknown';
  if (state === 'granted') return <p className="text-xs text-emerald-700">{firstName} agreed to marketing by email.</p>;
  return (
    <p className="text-xs text-amber-800" role="status">
      {firstName} {state === 'withdrawn' ? 'said no to' : 'has not agreed to'} marketing by email, so a marketing email
      will not be sent. Record their consent on their page first, or send it as service if their membership needs it.
    </p>
  );
}

/**
 * Email one member through the outbox: from the communications log, with a member to
 * pick, or from a member's page, for them. The message says what it is for, marketing or
 * service, and the server holds it to that.
 */
export default function EmailMemberDialog({
  members, member, onClose, onSent,
}: {
  /** Who can be picked, when no `member` is given. */
  members?: Member[];
  /** The member it is for, fixed. */
  member?: Member;
  onClose: () => void;
  onSent?: (message: Communication) => void;
}) {
  const [memberId, setMemberId] = useState(member?.id ?? '');
  const [category, setCategory] = useState<CommCategory>('Announcement');
  const [purpose, setPurpose] = useState<CommPurpose>(defaultPurposeFor('Announcement'));
  const [subject, setSubject] = useState('');
  const [body, setBody] = useState('');
  const [sending, setSending] = useState(false);

  const to = member ?? members?.find((m) => m.id === memberId);

  const changeCategory = (next: CommCategory) => {
    setCategory(next);
    setPurpose(defaultPurposeFor(next));
  };

  const send = async () => {
    if (!to) {
      toast.error('Pick the member it is for.');
      return;
    }
    if (!subject.trim() || !body.trim()) {
      toast.error('Write a subject and a message.');
      return;
    }
    setSending(true);
    try {
      const message = await sendMemberEmail(
        { memberId: to.id, category, purpose, subject: subject.trim(), body: body.trim() },
        `${to.firstName} ${to.lastName}`,
        to.email,
      );
      onSent?.(message);
      if (wasSent(message)) {
        toast.success(message.status === 'queued' ? `Queued for ${to.firstName}: it goes out in a moment.` : `Sent to ${to.firstName}.`);
      } else {
        toast.warning(`Not sent to ${to.firstName}. ${message.reason ?? 'The outbox did not send it.'}`);
      }
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The email could not be sent.');
    } finally {
      setSending(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => { if (!open && !sending) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{member ? `Email ${member.firstName} ${member.lastName}` : 'Email a member'}</DialogTitle>
          <DialogDescription>
            Sent from the club&apos;s address. The log shows whether it was delivered, opened or bounced.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          {!member && (
            <div>
              <Label htmlFor="email-member">To</Label>
              <Select value={memberId} onValueChange={setMemberId}>
                <SelectTrigger id="email-member" aria-label="To">
                  <SelectValue placeholder={members ? 'Pick a member' : 'Loading members…'} />
                </SelectTrigger>
                <SelectContent>
                  {(members ?? []).map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName}{m.email ? ` · ${m.email}` : ' · no email address'}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <div>
              <Label htmlFor="email-category">Category</Label>
              <Select value={category} onValueChange={(v) => changeCategory(v as CommCategory)}>
                <SelectTrigger id="email-category" aria-label="Category"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="email-purpose">What is it?</Label>
              <Select value={purpose} onValueChange={(v) => setPurpose(v as CommPurpose)}>
                <SelectTrigger id="email-purpose" aria-label="What is it?"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="service">Service: about their membership</SelectItem>
                  <SelectItem value="marketing">Marketing: news, offers, invitations</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          {purpose === 'marketing' && to && <MarketingConsentHint memberId={to.id} firstName={to.firstName} />}
          {purpose === 'service' && (
            <p className="text-xs text-slate-500">
              A service email goes whatever the member said about marketing, so keep it to what their membership needs:
              dues, a booking, their application.
            </p>
          )}
          <div>
            <Label htmlFor="email-subject">Subject</Label>
            <Input id="email-subject" aria-label="Subject" value={subject} onChange={(e) => setSubject(e.target.value)} maxLength={300} />
          </div>
          <div>
            <Label htmlFor="email-body">Message</Label>
            <Textarea id="email-body" aria-label="Message" value={body} onChange={(e) => setBody(e.target.value)} rows={6} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={sending}>Cancel</Button>
          <Button onClick={() => void send()} disabled={sending}>
            {sending ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> Sending</> : 'Send email'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

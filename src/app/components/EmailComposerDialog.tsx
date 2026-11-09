import { useEffect, useState } from 'react';
import { Mail, Send, Loader2, Sparkles } from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Label } from '@/app/components/ui/label';
import { sendEmail, cancelEmail, createActivity, generateCopy, servesDemoData, mailtoHref } from '@/app/api';
import { ApiError } from '@/app/api/apiClient';

/**
 * Refusals that are the answer, not a fault to work around in the person's own email
 * app. A placeholder with nothing to fill it is one too: the email app gets the words as
 * typed, and would send "Hi {{firstName}}," to the person.
 */
const REFUSALS = new Set(['Contact.DoNotContact', 'Email.Suppressed', 'Email.MergeField']);

export interface EmailComposerContext {
  leadId?: string;
  contactId?: string;
  dealId?: string;
}

interface EmailComposerDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Default recipient (editable). */
  to: string;
  defaultSubject?: string;
  defaultBody?: string;
  /** Associates the sent message + logged activity with a record. */
  context?: EmailComposerContext;
  /** Extra context for the AI draft (e.g. "Acme Corp · enterprise lead"). */
  aiContext?: string;
  /** Called after a successful send (e.g. to refresh a timeline). */
  onSent?: () => void;
}

/**
 * Reusable composer that sends through the `sendEmail` API — the server's outbox
 * sends it under the workspace's name and writes it on the record's timeline once
 * it has gone; demo mode simulates, and logs the `email` activity here.
 *
 * It says "Email sent" only when the server sent it (it used to say so with
 * nothing sent at all). When the mail server did not take it yet, it says the
 * email is queued and offers to cancel it. When a send fails, the toast gives the
 * server's reason and offers to open the message in the person's own email app
 * instead — except when the recipient asked not to be emailed or their address bounced.
 */
export default function EmailComposerDialog({
  open, onOpenChange, to, defaultSubject = '', defaultBody = '', context, aiContext, onSent,
}: EmailComposerDialogProps) {
  const [toAddr, setToAddr] = useState(to);
  const [cc, setCc] = useState('');
  const [showCc, setShowCc] = useState(false);
  const [subject, setSubject] = useState(defaultSubject);
  const [body, setBody] = useState(defaultBody);
  const [sending, setSending] = useState(false);
  const [drafting, setDrafting] = useState(false);

  const aiDraft = async () => {
    setDrafting(true);
    try {
      const goal = subject.trim()
        ? `Write a concise, friendly outreach email about: ${subject.trim()}`
        : 'Write a concise, friendly sales outreach email to start a conversation';
      const copy = await generateCopy({
        copyTypeId: 'sales-email',
        goal,
        context: aiContext || `Recipient: ${toAddr}`,
        length: 'medium',
      });
      if (copy) { setBody(copy); toast.success('Draft generated'); }
      else toast.error('Could not generate a draft');
    } catch {
      toast.error('Could not generate a draft');
    } finally {
      setDrafting(false);
    }
  };

  // Re-seed the fields each time the dialog is opened for a new context.
  useEffect(() => {
    if (open) {
      setToAddr(to);
      setSubject(defaultSubject);
      setBody(defaultBody);
      setCc('');
      setShowCc(false);
    }
  }, [open, to, defaultSubject, defaultBody]);

  const handleSend = async () => {
    if (!toAddr.trim()) { toast.error('A recipient is required'); return; }
    if (!subject.trim()) { toast.error('A subject is required'); return; }
    setSending(true);
    try {
      const sent = await sendEmail({
        to: toAddr.trim(),
        cc: cc.trim() || undefined,
        subject: subject.trim(),
        body,
        leadId: context?.leadId,
        contactId: context?.contactId,
        dealId: context?.dealId,
      });
      if (!sent) { toast.error('Failed to send email'); return; }
      if (servesDemoData()) {
        // Demo mode has no server to keep the timeline, so it is mirrored here.
        createActivity({
          type: 'email',
          subject: `Email: ${subject.trim()}`,
          body,
          leadId: context?.leadId,
          contactId: context?.contactId,
          dealId: context?.dealId,
        }).catch(() => { /* non-fatal */ });
        toast.success('Email sent (simulated in demo mode)');
      } else if (sent.status === 'queued') {
        // Not sent yet, and not lost: the server tries again, and puts it on the
        // timeline once it has gone. No email-app way out here, which would send it twice.
        const queuedId = sent.id;
        toast.info('Not sent yet: the mail server did not take it. Cadence keeps trying, and it shows on the timeline once it has gone.', {
          duration: 15_000,
          action: {
            label: 'Cancel sending',
            onClick: () => {
              cancelEmail(queuedId)
                .then(() => toast.success('Cancelled. It will not be sent.'))
                .catch((error: unknown) => toast.error(error instanceof Error && error.message ? error.message : 'It could not be cancelled.'));
            },
          },
        });
      } else {
        // The server writes it on the timeline; writing it here as well showed it twice.
        toast.success('Email sent');
      }
      onOpenChange(false);
      onSent?.();
    } catch (err) {
      const reason = err instanceof Error && err.message ? err.message : 'Failed to send email';
      // A "do not contact" or bounced-address refusal is the answer, not a fault to
      // work around: the email-app button sent the same message from the rep's own
      // mailbox and noted it on the timeline.
      if (err instanceof ApiError && err.code && REFUSALS.has(err.code)) {
        toast.error(reason, { duration: 12_000 });
        return;
      }
      toast.error(reason, {
        duration: 12_000,
        action: { label: 'Open in email app', onClick: handleOpenInMailApp },
      });
    } finally {
      setSending(false);
    }
  };

  /** Hand the message to the person's own email app, and note it on the timeline. */
  const handleOpenInMailApp = () => {
    if (!toAddr.trim()) { toast.error('A recipient is required'); return; }
    if (!subject.trim()) { toast.error('A subject is required'); return; }
    window.location.href = mailtoHref({ to: toAddr.trim(), cc: cc.trim() || undefined, subject: subject.trim(), body });
    createActivity({
      type: 'email',
      subject: `Email: ${subject.trim()}`,
      body,
      leadId: context?.leadId,
      contactId: context?.contactId,
      dealId: context?.dealId,
    }).catch(() => { /* non-fatal */ });
    toast.success('Opened in your email app. Send it from there — it is noted on the timeline.');
    onOpenChange(false);
    onSent?.();
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Mail className="h-5 w-5 text-indigo-600" /> Compose email
          </DialogTitle>
          <DialogDescription>
            {!servesDemoData()
              ? 'Sends from your workspace\'s email address and logs it on the timeline.'
              : 'Demo mode — sending is simulated, but it still logs to the timeline.'}
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div>
            <div className="flex items-center justify-between">
              <Label htmlFor="email-to">To</Label>
              {!showCc && (
                <button type="button" onClick={() => setShowCc(true)} className="text-xs font-medium text-indigo-600 hover:text-indigo-800">
                  Add Cc
                </button>
              )}
            </div>
            <Input id="email-to" type="email" value={toAddr} onChange={(e) => setToAddr(e.target.value)} placeholder="recipient@example.com" className="mt-1" />
          </div>
          {showCc && (
            <div>
              <Label htmlFor="email-cc">Cc</Label>
              <Input id="email-cc" type="email" value={cc} onChange={(e) => setCc(e.target.value)} placeholder="cc@example.com" className="mt-1" />
            </div>
          )}
          <div>
            <Label htmlFor="email-subject">Subject</Label>
            <Input id="email-subject" value={subject} onChange={(e) => setSubject(e.target.value)} placeholder="Subject" className="mt-1" />
          </div>
          <div>
            <div className="flex items-center justify-between">
              <Label htmlFor="email-body">Message</Label>
              <button
                type="button"
                onClick={aiDraft}
                disabled={drafting}
                className="inline-flex items-center gap-1 text-xs font-medium text-indigo-600 hover:text-indigo-800 disabled:opacity-60"
              >
                {drafting ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Sparkles className="h-3.5 w-3.5" />}
                {drafting ? 'Drafting…' : 'AI draft'}
              </button>
            </div>
            <Textarea id="email-body" value={body} onChange={(e) => setBody(e.target.value)} rows={9} placeholder="Write your message…" className="mt-1 resize-none" />
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={sending}>Cancel</Button>
          <Button onClick={handleSend} disabled={sending} className="gap-1.5">
            {sending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
            {sending ? 'Sending…' : 'Send'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

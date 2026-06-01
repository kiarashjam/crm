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
import { sendEmail, createActivity, generateCopy, isUsingRealApi } from '@/app/api';

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
 * Reusable composer that actually sends through the `sendEmail` API (the real
 * backend delivers via its provider; demo mode simulates) and logs an `email`
 * activity against the related record so it lands on the timeline.
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
      // Mirror onto the activity timeline.
      createActivity({
        type: 'email',
        subject: `Email: ${subject.trim()}`,
        body,
        leadId: context?.leadId,
        contactId: context?.contactId,
        dealId: context?.dealId,
      }).catch(() => { /* non-fatal */ });
      toast.success(isUsingRealApi() ? 'Email sent' : 'Email sent (simulated in demo mode)');
      onOpenChange(false);
      onSent?.();
    } catch {
      toast.error('Failed to send email');
    } finally {
      setSending(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[560px]">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Mail className="h-5 w-5 text-indigo-600" /> Compose email
          </DialogTitle>
          <DialogDescription>
            {isUsingRealApi()
              ? 'Sends through your connected email provider and logs to the timeline.'
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

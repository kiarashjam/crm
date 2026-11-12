import { useEffect, useMemo, useState } from 'react';
import { toast } from 'sonner';
import { MessageSquareHeart, RotateCcw, Send } from 'lucide-react';
import { RecordCard } from '@/app/components/RecordCard';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import { getOnboardingSettings, resendOnboarding, startOnboarding, type OnboardingSettings } from '@/app/api/onboarding';
import { OnboardingTimeline, useDealOnboarding } from './OnboardingTimeline';
import { WelcomeConfirmDialog } from './WelcomeConfirmDialog';
import { WelcomeFields } from './WelcomeFields';
import { draftFrom, startLabel, toPreviewRequest, toStartRequest, type WelcomeDraft } from './welcome';

const STATUS: Record<string, { label: string; className: string }> = {
  queued: { label: 'Handing over', className: 'bg-orange-100 text-orange-700' },
  delivered: { label: 'With the portal', className: 'bg-sky-100 text-sky-700' },
  account_ready: { label: 'Account open', className: 'bg-indigo-100 text-indigo-700' },
  welcomed: { label: 'Welcomed', className: 'bg-emerald-100 text-emerald-700' },
  active: { label: 'In the portal', className: 'bg-emerald-500 text-white' },
  failed: { label: 'Needs attention', className: 'bg-rose-100 text-rose-700' },
};

/**
 * The deal's welcome: how far the portal has got with it, or a way to send one when
 * the lead was converted without, or before the portal was connected.
 */
export function DealWelcomeCard({ dealId, contactName, dealName, index }: { dealId: string; contactName?: string; dealName?: string; index?: number }) {
  const { items, latest, refresh, setItems } = useDealOnboarding(dealId);
  const [settings, setSettings] = useState<OnboardingSettings | null>(null);
  const [composing, setComposing] = useState(false);
  const [draft, setDraft] = useState<WelcomeDraft>(() => draftFrom(null));
  const [confirming, setConfirming] = useState(false);
  const [sending, setSending] = useState(false);

  useEffect(() => {
    if (servesDemoData()) return;
    getOnboardingSettings().then(setSettings).catch(() => setSettings(null));
  }, []);

  const previewRequest = useMemo(() => (confirming ? toPreviewRequest(draft, { dealId }) : null), [confirming, draft, dealId]);

  if (servesDemoData()) return null;
  // Nothing to show and nowhere to send: the card waits until the portal is connected.
  if (items !== null && items.length === 0 && settings && !settings.configured) return null;

  const send = async () => {
    setSending(true);
    try {
      const made = await startOnboarding(dealId, toStartRequest(draft));
      setItems([made]);
      setConfirming(false);
      setComposing(false);
      toast.success('Handed to the portal: it opens their account and sends the welcome.');
      void refresh();
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'The welcome could not be sent.');
    } finally {
      setSending(false);
    }
  };

  const resend = async () => {
    if (!latest) return;
    setSending(true);
    try {
      const again = await resendOnboarding(latest.id);
      setItems([again, ...(items ?? []).slice(1)]);
      toast.success('Sent to the portal again.');
      void refresh();
    } catch (e) {
      toast.error(e instanceof Error && e.message ? e.message : 'It could not be sent again.');
    } finally {
      setSending(false);
    }
  };

  const status = latest ? STATUS[latest.status] ?? STATUS.queued! : null;

  return (
    <RecordCard
      title="Welcome & portal"
      icon={<MessageSquareHeart className="h-3.5 w-3.5" />}
      tone="pink"
      index={index}
      action={status && <span className={`rounded-full px-2 py-0.5 text-[11px] font-semibold ${status.className}`}>{status.label}</span>}
    >
      {items === null ? (
        <div className="py-5 text-center"><Spinner size={20} className="mx-auto" /></div>
      ) : latest ? (
        <div className="space-y-3">
          <div className="flex flex-wrap gap-1.5 text-xs">
            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-slate-600">{latest.email}</span>
            <span className="rounded-full bg-amber-50 px-2 py-0.5 text-amber-700">Starts: {startLabel(latest.startsOnUtc, latest.startUndetermined)}</span>
          </div>
          <OnboardingTimeline onboarding={latest} compact />
          {latest.welcomeMessage && (
            <blockquote className="whitespace-pre-line rounded-lg border-l-2 border-orange-300 bg-orange-50/60 px-3 py-2 text-xs text-slate-600">
              {latest.welcomeMessage}
            </blockquote>
          )}
          {latest.canResend && (
            <WriteOnly permission={PERMISSION.EditRecords}>
              <Button type="button" size="sm" variant="outline" onClick={() => void resend()} disabled={sending}>
                {sending ? <Spinner size={14} /> : <RotateCcw className="h-3.5 w-3.5" />}
                {latest.status === 'failed' ? 'Try again' : 'Send the welcome again'}
              </Button>
            </WriteOnly>
          )}
        </div>
      ) : (
        <div className="space-y-3">
          <p className="text-sm text-slate-600">
            Hand {contactName || 'the contact'} to your portal: it opens their account and sends your welcome email with a link to set their password.
          </p>
          <WriteOnly permission={PERMISSION.EditRecords}>
            <Button
              type="button"
              size="sm"
              onClick={() => { setDraft({ ...draftFrom(settings), send: true }); setComposing(true); }}
              className="bg-gradient-to-r from-orange-500 to-pink-500 text-white hover:from-orange-600 hover:to-pink-600"
            >
              <Send className="h-3.5 w-3.5" /> Welcome {contactName?.split(' ')[0] || 'them'}
            </Button>
          </WriteOnly>
        </div>
      )}

      <Dialog open={composing} onOpenChange={setComposing}>
        <DialogContent className="sm:max-w-[600px]">
          <DialogTitle>Welcome {contactName || 'them'}</DialogTitle>
          <DialogDescription>{dealName ? `For the deal "${dealName}". ` : ''}Check it on the next step before it goes.</DialogDescription>
          <div className="max-h-[60vh] overflow-y-auto pr-1">
            <WelcomeFields draft={draft} onChange={setDraft} settings={settings} idPrefix="deal-welcome" showToggle={false} />
          </div>
          <div className="flex justify-end gap-2 pt-2">
            <Button type="button" variant="outline" onClick={() => setComposing(false)}>Cancel</Button>
            <Button type="button" onClick={() => setConfirming(true)} className="bg-gradient-to-r from-orange-500 to-pink-500 text-white">
              Preview &amp; confirm
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      <WelcomeConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        request={previewRequest}
        recipient={{ name: contactName }}
        confirmLabel="Send welcome"
        busy={sending}
        onConfirm={() => void send()}
      />
    </RecordCard>
  );
}

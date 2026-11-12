import { useEffect, useState } from 'react';
import { motion } from 'motion/react';
import { AlertTriangle, ArrowLeft, CalendarDays, Languages, Send, Sparkles, UserRound } from 'lucide-react';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { previewOnboarding, type OnboardingPreview, type OnboardingPreviewRequest } from '@/app/api/onboarding';
import { EmailPreviewFrame } from './EmailPreviewFrame';
import { startLabel } from './welcome';

/**
 * "This is what they'll get": the welcome email filled for this person, before anything
 * is sent. The person confirms it, or goes back and changes it.
 */
export function WelcomeConfirmDialog({
  open,
  onOpenChange,
  request,
  recipient,
  confirmLabel,
  busy,
  onConfirm,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  request: OnboardingPreviewRequest | null;
  recipient: { name?: string; email?: string };
  confirmLabel: string;
  busy?: boolean;
  onConfirm: () => void;
}) {
  const [preview, setPreview] = useState<OnboardingPreview | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!open || !request) return;
    let alive = true;
    setPreview(null);
    setError(null);
    previewOnboarding(request)
      .then((p) => { if (alive) setPreview(p); })
      .catch((e: unknown) => { if (alive) setError(e instanceof Error && e.message ? e.message : 'The preview could not be made.'); });
    return () => { alive = false; };
  }, [open, request]);

  const blocked = !preview || preview.unknown.length > 0;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="gap-0 overflow-hidden p-0 sm:max-w-[720px]">
        <div className="relative overflow-hidden bg-gradient-to-br from-orange-500 via-pink-500 to-fuchsia-500 px-6 py-5 text-white">
          <div className="absolute -right-10 -top-12 h-40 w-40 rounded-full bg-white/15 blur-2xl" />
          <div className="relative flex items-center gap-3">
            <motion.div
              className="flex h-11 w-11 items-center justify-center rounded-xl border border-white/30 bg-white/20 shadow-lg backdrop-blur-sm"
              initial={{ rotate: -12, scale: 0.8 }}
              animate={{ rotate: 0, scale: 1 }}
              transition={{ type: 'spring', stiffness: 260, damping: 16 }}
            >
              <Sparkles className="h-5 w-5" />
            </motion.div>
            <div>
              <DialogTitle className="text-lg font-bold tracking-tight text-white">This is what they'll get</DialogTitle>
              <DialogDescription className="text-sm text-white/85">
                Check the welcome email, then confirm. The portal opens their account and sends it.
              </DialogDescription>
            </div>
          </div>
          <div className="relative mt-4 flex flex-wrap gap-2 text-xs">
            <span className="inline-flex items-center gap-1.5 rounded-full bg-white/20 px-2.5 py-1 backdrop-blur-sm">
              <UserRound className="h-3.5 w-3.5" /> {recipient.name || 'The contact'}{recipient.email ? ` · ${recipient.email}` : ''}
            </span>
            {request && (
              <>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-white/20 px-2.5 py-1 backdrop-blur-sm">
                  <CalendarDays className="h-3.5 w-3.5" /> Starts: {startLabel(request.startsOnUtc, request.startUndetermined)}
                </span>
                <span className="inline-flex items-center gap-1.5 rounded-full bg-white/20 px-2.5 py-1 backdrop-blur-sm">
                  <Languages className="h-3.5 w-3.5" /> {request.language === 'en' ? 'English' : 'Français'}
                </span>
              </>
            )}
          </div>
        </div>

        <div className="max-h-[60vh] space-y-3 overflow-y-auto bg-slate-50/60 p-5 dark:bg-slate-900/40">
          {error ? (
            <div className="flex items-start gap-2 rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">
              <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
            </div>
          ) : !preview ? (
            <div className="py-16 text-center">
              <Spinner size={26} className="mx-auto mb-3" />
              <p className="text-sm text-slate-500">Filling in the email…</p>
            </div>
          ) : (
            <motion.div initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ duration: 0.3 }} className="space-y-3">
              {preview.unknown.length > 0 && (
                <div className="flex items-start gap-2 rounded-xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-800">
                  <AlertTriangle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>
                    Nothing fills {preview.unknown.join(', ')}. Go back and use one of the listed placeholders, or give it a fallback like {'{{name|there}}'}.
                  </span>
                </div>
              )}
              {preview.usesReceiverDesign && (
                <p className="rounded-lg bg-violet-50 px-3 py-2 text-xs text-violet-700">
                  No email of your own yet, so the portal sends its own welcome design with your message in it. This is close to what it looks like.
                </p>
              )}
              <EmailPreviewFrame subject={preview.subject} html={preview.html} to={recipient.email} />
              <p className="text-xs text-slate-500">
                The button and links point at the portal once it sends: it puts in the link to set a password, valid for a few days.
              </p>
            </motion.div>
          )}
        </div>

        <div className="flex items-center gap-3 border-t border-slate-100 px-6 py-4">
          <Button type="button" variant="outline" onClick={() => onOpenChange(false)} className="h-11 flex-1" disabled={busy}>
            <ArrowLeft className="h-4 w-4" /> Back to edit
          </Button>
          <Button
            type="button"
            onClick={onConfirm}
            disabled={busy || blocked}
            className="h-11 flex-[2] bg-gradient-to-r from-orange-500 to-pink-500 font-medium text-white shadow-lg shadow-orange-200/50 hover:from-orange-600 hover:to-pink-600"
          >
            {busy ? <Spinner size={16} /> : <Send className="h-4 w-4" />}
            {confirmLabel}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

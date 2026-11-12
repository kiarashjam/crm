import { useEffect, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { ArrowRight, CheckCircle2, Handshake, PartyPopper, RotateCcw, UserRound } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Button } from '@/app/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from '@/app/components/ui/dialog';
import { Confetti } from '@/app/components/Confetti';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { startOnboarding, type StartOnboardingRequest } from '@/app/api/onboarding';
import { OnboardingTimeline, useDealOnboarding } from './OnboardingTimeline';
import { startLabel } from './welcome';

/**
 * Right after a lead becomes a deal with a welcome: the handoff starts here, and each
 * step lights up as the portal opens the account and sends the email.
 */
export function WelcomeProgressDialog({
  dealId,
  personName,
  request,
  onClose,
}: {
  dealId: string | null;
  personName: string;
  request: StartOnboardingRequest | null;
  onClose: () => void;
}) {
  const navigate = useNavigate();
  const [startError, setStartError] = useState<string | null>(null);
  const [starting, setStarting] = useState(false);
  const startedFor = useRef<string | null>(null);
  const { latest, refresh, setItems } = useDealOnboarding(dealId);

  const start = async () => {
    if (!dealId || !request) return;
    setStarting(true);
    setStartError(null);
    try {
      const made = await startOnboarding(dealId, request);
      setItems([made]);
      void refresh();
    } catch (e) {
      setStartError(e instanceof Error && e.message ? e.message : 'The welcome could not be sent.');
    } finally {
      setStarting(false);
    }
  };

  // Once per deal, even when React runs the effect twice.
  useEffect(() => {
    if (!dealId || !request || startedFor.current === dealId) return;
    startedFor.current = dealId;
    void start();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dealId, request]);

  const finished = latest?.status === 'welcomed' || latest?.status === 'active';

  return (
    <Dialog open={Boolean(dealId)} onOpenChange={(open) => !open && onClose()}>
      <DialogContent className="gap-0 overflow-hidden p-0 sm:max-w-[520px]">
        <div className="relative overflow-hidden bg-gradient-to-br from-emerald-600 via-emerald-500 to-teal-400 px-6 py-5 text-white">
          <Confetti fire={finished ? 2 : dealId ? 1 : 0} count={22} />
          <div className="absolute -right-10 -top-10 h-40 w-40 rounded-full bg-white/10 blur-2xl" />
          <div className="relative flex items-center gap-3">
            <motion.div
              className="flex h-12 w-12 items-center justify-center rounded-xl border border-white/30 bg-white/20 shadow-lg backdrop-blur-sm"
              initial={{ scale: 0.6, rotate: -20 }}
              animate={{ scale: 1, rotate: 0 }}
              transition={{ type: 'spring', stiffness: 240, damping: 14 }}
            >
              <PartyPopper className="h-6 w-6" />
            </motion.div>
            <div>
              <DialogTitle className="text-xl font-bold tracking-tight text-white">Welcome aboard, {personName.split(' ')[0] || 'friend'}</DialogTitle>
              <DialogDescription className="text-sm text-white/85">The lead is now a deal, and their welcome is on its way.</DialogDescription>
            </div>
          </div>
          <div className="relative mt-4 flex flex-wrap items-center gap-2 text-xs">
            {[
              { icon: UserRound, label: 'Lead' },
              { icon: Handshake, label: 'Deal' },
              { icon: CheckCircle2, label: 'Portal' },
            ].map((step, i) => (
              <motion.span
                key={step.label}
                className="inline-flex items-center gap-1.5 rounded-full bg-white/20 px-2.5 py-1 backdrop-blur-sm"
                initial={{ opacity: 0, x: -8 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.15 + i * 0.12 }}
              >
                <step.icon className="h-3.5 w-3.5" /> {step.label}
                {i < 2 && <ArrowRight className="ml-1 h-3 w-3 opacity-70" />}
              </motion.span>
            ))}
          </div>
        </div>

        <div className="space-y-4 p-6">
          {startError ? (
            <div className="space-y-3">
              <p className="rounded-xl border border-rose-200 bg-rose-50 p-3 text-sm text-rose-700">{startError}</p>
              <p className="text-xs text-slate-500">The deal is saved. You can send the welcome again here, or later from the deal.</p>
              <Button type="button" variant="outline" onClick={() => void start()} disabled={starting}>
                {starting ? <Spinner size={14} /> : <RotateCcw className="h-4 w-4" />} Try again
              </Button>
            </div>
          ) : !latest ? (
            <div className="py-8 text-center">
              <Spinner size={26} className="mx-auto mb-3" />
              <p className="text-sm text-slate-500">Handing {personName || 'them'} to the portal…</p>
            </div>
          ) : (
            <motion.div initial={{ opacity: 0, y: 8 }} animate={{ opacity: 1, y: 0 }} className="space-y-4">
              <div className="flex flex-wrap gap-2 text-xs">
                <span className="rounded-full bg-slate-100 px-2.5 py-1 text-slate-600">{latest.email}</span>
                <span className="rounded-full bg-amber-50 px-2.5 py-1 text-amber-700">Starts: {startLabel(latest.startsOnUtc, latest.startUndetermined)}</span>
                {latest.clubMemberId && <span className="rounded-full bg-indigo-50 px-2.5 py-1 text-indigo-700">Added to club members</span>}
              </div>
              <OnboardingTimeline onboarding={latest} />
              <p className="text-xs text-slate-500">You can close this: the deal keeps the progress, and it carries on without you.</p>
            </motion.div>
          )}
        </div>

        <div className="flex items-center gap-3 border-t border-slate-100 px-6 py-4">
          <Button type="button" variant="outline" className="h-10 flex-1" onClick={onClose}>Done</Button>
          <Button
            type="button"
            className="h-10 flex-1 bg-gradient-to-r from-emerald-600 to-teal-500 text-white hover:from-emerald-700 hover:to-teal-600"
            onClick={() => { onClose(); if (dealId) navigate(`/deals/${dealId}`); }}
          >
            Open the deal <ArrowRight className="h-4 w-4" />
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

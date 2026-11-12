import { useCallback, useEffect, useRef, useState } from 'react';
import { motion } from 'motion/react';
import { AlertTriangle, Check, KeyRound, MailCheck, Send, UserRoundCheck } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { getDealOnboarding, onboardingSettled, type Onboarding } from '@/app/api/onboarding';

/** The four things that happen once someone is handed over, in order. */
const STAGES = [
  { key: 'delivered', label: 'Handed to the portal', icon: Send, waiting: 'Handing over…' },
  { key: 'account', label: 'Account opened', icon: UserRoundCheck, waiting: 'Opening their account…' },
  { key: 'email', label: 'Welcome email sent', icon: MailCheck, waiting: 'Sending the welcome email…' },
  { key: 'password', label: 'Password set', icon: KeyRound, waiting: 'Waiting for them to set a password' },
] as const;

/** Which stage each step of the portal's reports completes. */
const REACHES: Record<string, number> = {
  delivered: 1,
  account_created: 2,
  account_existing: 2,
  email_sent: 3,
  password_set: 4,
};

type StageState = 'done' | 'active' | 'error' | 'pending';

/** Where this round of handing over has got to (since the latest send). */
export function stagesOf(o: Onboarding): { state: StageState; at?: string; detail?: string | null }[] {
  const round = Math.max(...o.steps.map((s, i) => (s.key === 'queued' || s.key === 'resent' ? i : -1)));
  const steps = o.steps.slice(round + 1);
  let reached = 0;
  const at: (string | undefined)[] = [];
  const detail: (string | null | undefined)[] = [];
  for (const s of steps) {
    const r = REACHES[s.key];
    if (r) {
      // A report can skip one (the account and the email in one go): what it implies is done.
      for (let i = reached; i < r; i++) at[i] ??= s.atUtc;
      reached = Math.max(reached, r);
      if (s.key === 'account_existing') detail[1] = 'They already had one';
      // The portal says who it went out from: "Sent from Pavillon 46 <hello@…>".
      if (s.key === 'email_sent' && s.detail) detail[2] = s.detail;
    }
  }
  const failedStep = o.status === 'failed' ? [...steps].reverse().find((s) => ['undeliverable', 'email_failed', 'failed'].includes(s.key)) : undefined;
  const failedAt = failedStep ? (failedStep.key === 'undeliverable' ? 0 : failedStep.key === 'email_failed' ? 2 : Math.min(reached, 3)) : -1;
  return STAGES.map((_, i) => {
    if (i < reached) return { state: 'done', at: at[i], detail: detail[i] };
    if (i === failedAt) {
      const why = failedStep?.detail || o.delivery?.lastError;
      return { state: 'error', at: failedStep?.atUtc, detail: why };
    }
    if (i === reached && o.status !== 'failed') {
      // Only once an attempt has failed: the outbox counts an attempt as it starts it.
      const retrying = i === 0 && o.delivery?.status === 'queued' && Boolean(o.delivery.lastStatusCode || o.delivery.lastError);
      return {
        state: 'active',
        detail: retrying
          ? `The portal did not take it yet${o.delivery?.lastStatusCode ? ` (it answered ${o.delivery.lastStatusCode})` : ''}; trying again${o.delivery?.nextAttemptAtUtc ? ` at ${new Date(o.delivery.nextAttemptAtUtc).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : ''}.`
          : undefined,
      };
    }
    return { state: 'pending' };
  });
}

function time(iso?: string): string {
  if (!iso) return '';
  const d = new Date(iso);
  const today = new Date().toDateString() === d.toDateString();
  return today
    ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
    : d.toLocaleDateString(undefined, { day: 'numeric', month: 'short' }) + ', ' + d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
}

/** The handoff's progress: each stage lights up as the portal reports it. */
export function OnboardingTimeline({ onboarding, compact }: { onboarding: Onboarding; compact?: boolean }) {
  const reduce = useMotionPreference();
  const stages = stagesOf(onboarding);
  const done = stages.filter((s) => s.state === 'done').length;

  return (
    <div className="relative">
      <ol className={cn('relative space-y-0', compact ? 'ml-1' : 'ml-2')}>
        {STAGES.map((stage, i) => {
          const s = stages[i]!;
          const Icon = s.state === 'error' ? AlertTriangle : s.state === 'done' ? Check : stage.icon;
          const last = i === STAGES.length - 1;
          return (
            <li key={stage.key} className="relative flex gap-3 pb-4 last:pb-0">
              {!last && (
                <span className="absolute left-[15px] top-8 h-[calc(100%-24px)] w-0.5 overflow-hidden rounded-full bg-slate-200 dark:bg-slate-700" aria-hidden>
                  <motion.span
                    className="block w-full bg-gradient-to-b from-emerald-400 to-teal-400"
                    initial={false}
                    animate={{ height: i < done ? '100%' : '0%' }}
                    transition={{ duration: reduce ? 0 : 0.5, ease: 'easeOut', delay: reduce ? 0 : 0.15 }}
                  />
                </span>
              )}
              <motion.span
                className={cn(
                  'relative z-10 flex h-8 w-8 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
                  s.state === 'done' && 'border-emerald-500 bg-emerald-500 text-white shadow-md shadow-emerald-500/30',
                  s.state === 'active' && 'border-orange-400 bg-white text-orange-500',
                  s.state === 'error' && 'border-rose-500 bg-rose-500 text-white shadow-md shadow-rose-500/30',
                  s.state === 'pending' && 'border-slate-200 bg-white text-slate-300 dark:border-slate-700 dark:bg-slate-800',
                )}
                initial={false}
                animate={s.state === 'done' && !reduce ? { scale: [0.8, 1.15, 1] } : { scale: 1 }}
                transition={{ duration: 0.4 }}
              >
                {s.state === 'active' && !reduce && (
                  <motion.span
                    className="absolute inset-0 rounded-full border-2 border-orange-400"
                    animate={{ scale: [1, 1.5], opacity: [0.7, 0] }}
                    transition={{ duration: 1.4, repeat: Infinity, ease: 'easeOut' }}
                    aria-hidden
                  />
                )}
                <Icon className="h-4 w-4" />
              </motion.span>
              <div className="min-w-0 flex-1 pt-1">
                <p
                  className={cn(
                    'text-sm font-medium',
                    s.state === 'pending' ? 'text-slate-400' : s.state === 'error' ? 'text-rose-700' : 'text-slate-800 dark:text-slate-100',
                  )}
                >
                  {s.state === 'active' ? stage.waiting : stage.label}
                  {s.at && s.state === 'done' && <span className="ml-2 text-xs font-normal text-slate-400">{time(s.at)}</span>}
                </p>
                {s.detail && (
                  <p className={cn('mt-0.5 text-xs', s.state === 'error' ? 'text-rose-600' : 'text-slate-500')}>{s.detail}</p>
                )}
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

/**
 * A deal's handoffs, read again every few seconds while the latest is still moving, so
 * the timeline fills in as the portal reports. Stops once it settles, or after ten minutes.
 */
export function useDealOnboarding(dealId: string | null | undefined) {
  const [items, setItems] = useState<Onboarding[] | null>(null);
  const [error, setError] = useState<string | null>(null);
  const started = useRef(Date.now());

  const refresh = useCallback(async () => {
    if (!dealId) return;
    try {
      setItems(await getDealOnboarding(dealId));
      setError(null);
    } catch (e) {
      setError(e instanceof Error && e.message ? e.message : 'The handoff could not be read.');
    }
  }, [dealId]);

  useEffect(() => {
    started.current = Date.now();
    setItems(null);
    void refresh();
  }, [refresh]);

  const latest = items?.[0];
  const moving = Boolean(latest && !onboardingSettled(latest));
  useEffect(() => {
    if (!moving) return;
    const elapsed = Date.now() - started.current;
    if (elapsed > 10 * 60_000) return;
    const t = window.setTimeout(() => { void refresh(); }, elapsed < 60_000 ? 2000 : 6000);
    return () => window.clearTimeout(t);
  }, [moving, items, refresh]);

  return { items, latest, error, refresh, setItems };
}

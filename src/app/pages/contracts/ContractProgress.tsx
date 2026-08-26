// Where a contract is, at a glance.
//
// A status pill reading "sent" answers none of the questions somebody actually
// has: sent to whom, how long ago, did they open it, and am I waiting on them or
// are they waiting on me. This renders all four, plus which of the four steps is
// current — so the panel can be understood without reading any prose.
//
// Every word and every step number comes from `describeState`, which is tested.
// Nothing here decides anything; it only draws what that function returned. That
// split matters because the awkward cases are all decisions — an executed
// contract whose copies never went out must not read as finished — and a
// component is the wrong place to test a decision.

import { motion } from 'motion/react';
import { Check, Clock, ArrowRight, Ban, PartyPopper } from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import {
  CONTRACT_STEPS, describeState, type DescribeInput, type StateTone,
} from './contractLifecycle';

/** One palette per tone, so colour carries the same meaning everywhere. */
const TONE: Record<StateTone, {
  ring: string; fill: string; text: string; band: string; icon: typeof Clock;
}> = {
  neutral: {
    ring: 'ring-slate-300', fill: 'bg-slate-400', text: 'text-slate-700',
    band: 'border-slate-200 bg-slate-50', icon: Clock,
  },
  // Your move. Deliberately the loudest of the five: it is the only tone that
  // means somebody has to do something.
  action: {
    ring: 'ring-indigo-400', fill: 'bg-indigo-500', text: 'text-indigo-900',
    band: 'border-indigo-200 bg-indigo-50', icon: ArrowRight,
  },
  waiting: {
    ring: 'ring-amber-400', fill: 'bg-amber-500', text: 'text-amber-900',
    band: 'border-amber-200 bg-amber-50', icon: Clock,
  },
  done: {
    ring: 'ring-emerald-400', fill: 'bg-emerald-500', text: 'text-emerald-900',
    band: 'border-emerald-200 bg-emerald-50', icon: PartyPopper,
  },
  stopped: {
    ring: 'ring-slate-300', fill: 'bg-slate-400', text: 'text-slate-600',
    band: 'border-slate-200 bg-slate-100', icon: Ban,
  },
};

const TURN_LABEL = {
  you: 'Your turn',
  them: 'Their turn',
  nobody: null,
} as const;

export function ContractProgress({ contract }: { contract: DescribeInput }) {
  const reduceMotion = useMotionPreference();
  const state = describeState(contract);
  const tone = TONE[state.tone];
  const stopped = state.tone === 'stopped';
  const turnLabel = TURN_LABEL[state.turn];
  // A finished step is finished regardless of what is happening now, so it gets
  // one stable colour and the tone is reserved for the CURRENT step. Painting
  // completed steps in the tone made step 1 amber while "waiting" — reading as a
  // warning about something that had already gone fine. Muted when the process
  // stopped, because those steps did not lead anywhere.
  const doneFill = stopped ? 'bg-slate-300' : 'bg-emerald-500';
  const doneText = stopped ? 'text-slate-500' : 'text-white';

  return (
    <div>
      {/* ── The four steps ─────────────────────────────────────────────────── */}
      <ol
        className="flex items-start gap-1.5"
        // The visible row is decorative duplication of the band below, which
        // already states everything in words. Announcing both would make a screen
        // reader read the state twice.
        aria-hidden
      >
        {CONTRACT_STEPS.map((label, i) => {
          const n = i + 1;
          const complete = n <= state.completed;
          const current = !stopped && n === state.step && !complete;
          return (
            <li key={label} className="min-w-0 flex-1">
              <div className="flex items-center gap-1.5">
                <span
                  className={cn(
                    'flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10px] font-bold',
                    complete && cn(doneFill, doneText),
                    current && cn('bg-white ring-2', tone.ring, tone.text),
                    !complete && !current && 'bg-slate-100 text-slate-400',
                  )}
                >
                  {complete ? <Check className="h-3 w-3" strokeWidth={3} /> : n}
                </span>
                {/* Connector. The last step has none, so the row does not end in
                    a line pointing at nothing. */}
                {i < CONTRACT_STEPS.length - 1 && (
                  <span className="h-0.5 min-w-0 flex-1 overflow-hidden rounded-full bg-slate-100">
                    <motion.span
                      className={cn('block h-full', doneFill)}
                      initial={reduceMotion ? false : { scaleX: 0 }}
                      animate={{ scaleX: n <= state.completed ? 1 : 0 }}
                      style={{ originX: 0 }}
                      transition={{ duration: 0.45, ease: [0.16, 1, 0.3, 1] }}
                    />
                  </span>
                )}
              </div>
              <p
                className={cn(
                  'mt-1.5 truncate text-[11px] leading-tight',
                  complete || current ? 'font-semibold text-slate-700' : 'text-slate-400',
                )}
                title={label}
              >
                {label}
              </p>
            </li>
          );
        })}
      </ol>

      {/* ── What is actually happening, in words ───────────────────────────── */}
      <div
        className={cn('mt-3 flex items-start gap-2.5 rounded-xl border px-3.5 py-3', tone.band)}
        // The one live region: this is the sentence that changes when the
        // contract moves, and the only one worth announcing.
        role="status"
      >
        <tone.icon className={cn('mt-0.5 h-4 w-4 shrink-0', tone.text)} aria-hidden />
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
            <p className={cn('text-sm font-semibold', tone.text)}>{state.headline}</p>
            {turnLabel && (
              <span className={cn(
                'rounded-full px-1.5 py-0.5 text-[10px] font-bold uppercase tracking-wide',
                state.turn === 'you' ? 'bg-indigo-600 text-white' : 'bg-white/70 text-slate-600',
              )}>
                {turnLabel}
              </span>
            )}
          </div>
          <p className={cn('mt-0.5 text-xs leading-relaxed', tone.text, 'opacity-80')}>
            {state.detail}
          </p>
        </div>
      </div>
    </div>
  );
}

export default ContractProgress;

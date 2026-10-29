import { useEffect, useState, type ReactNode } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import {
  ArrowUpRight, Briefcase, Check, CheckCircle2, Copy, GitMerge, ListTodo, Loader2, MessageSquare,
  PenLine, Pencil, Repeat2, Send, Sparkles, StickyNote, UserCheck, UserPlus, Workflow,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import type { AgentAction } from './brain/answer';

export const EASE = [0.16, 1, 0.3, 1] as const;

/**
 * The agent's face: a glowing orb with a slowly turning highlight. It breathes while the
 * agent is thinking. With less motion asked for, it is a still gradient.
 */
export function AiOrb({ size = 32, thinking = false, reduce = false, className }: { size?: number; thinking?: boolean; reduce?: boolean; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn('relative inline-flex shrink-0 items-center justify-center', className)}
      style={{ width: size, height: size }}
    >
      {!reduce && (
        <motion.span
          className="absolute inset-0 rounded-full bg-gradient-to-br from-orange-400 via-fuchsia-500 to-violet-500 blur-md"
          animate={{ opacity: thinking ? [0.45, 0.9, 0.45] : [0.35, 0.55, 0.35], scale: thinking ? [1, 1.25, 1] : [1, 1.08, 1] }}
          transition={{ duration: thinking ? 1.2 : 3.2, repeat: Infinity, ease: 'easeInOut' }}
        />
      )}
      <span className="relative flex h-full w-full items-center justify-center overflow-hidden rounded-full bg-gradient-to-br from-orange-500 via-fuchsia-500 to-violet-600 shadow-lg shadow-fuchsia-500/30 ring-2 ring-white dark:ring-slate-900">
        {!reduce && (
          <motion.span
            className="absolute inset-[-40%] bg-[conic-gradient(from_0deg,transparent_0deg,rgba(255,255,255,0.55)_60deg,transparent_120deg)]"
            animate={{ rotate: 360 }}
            transition={{ duration: thinking ? 1.4 : 5, repeat: Infinity, ease: 'linear' }}
          />
        )}
        <Sparkles className="relative text-white" style={{ width: size * 0.48, height: size * 0.48 }} />
      </span>
    </span>
  );
}

const THINKING = ['Reading your records', 'Weighing the queue', 'Checking dates and amounts', 'Putting it together'];

/** Three bouncing dots and a line that says what the agent is looking at. */
export function ThinkingBubble({ reduce }: { reduce: boolean }) {
  const [step, setStep] = useState(0);
  useEffect(() => {
    const handle = window.setInterval(() => setStep((current) => (current + 1) % THINKING.length), 900);
    return () => window.clearInterval(handle);
  }, []);
  return (
    <motion.div
      className="flex items-end gap-2.5"
      initial={reduce ? false : { opacity: 0, y: 10, scale: 0.96 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, scale: 0.96, transition: { duration: 0.15 } }}
      transition={{ duration: 0.3, ease: EASE }}
      role="status"
      aria-label="The agent is thinking"
    >
      <AiOrb size={28} thinking reduce={reduce} />
      <div className="rounded-2xl rounded-bl-md border border-fuchsia-100 bg-white/90 px-4 py-3 shadow-sm dark:border-fuchsia-900/50 dark:bg-slate-900">
        <div className="flex items-center gap-1.5">
          {[0, 1, 2].map((dot) => (
            <motion.span
              key={dot}
              className="h-2 w-2 rounded-full bg-gradient-to-br from-orange-400 to-fuchsia-500"
              animate={reduce ? undefined : { y: [0, -5, 0], opacity: [0.5, 1, 0.5] }}
              transition={{ duration: 0.9, repeat: Infinity, delay: dot * 0.15, ease: 'easeInOut' }}
            />
          ))}
        </div>
        <AnimatePresence mode="wait" initial={false}>
          <motion.p
            key={step}
            className="mt-1.5 bg-gradient-to-r from-slate-500 via-fuchsia-500 to-slate-500 bg-[length:200%_100%] bg-clip-text text-xs font-medium text-transparent motion-safe:animate-[shimmer_2.2s_linear_infinite]"
            initial={reduce ? false : { opacity: 0, y: 4 }}
            animate={{ opacity: 1, y: 0 }}
            exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: -4, transition: { duration: 0.12 } }}
            transition={{ duration: 0.2 }}
          >
            {THINKING[step]}…
          </motion.p>
        </AnimatePresence>
      </div>
    </motion.div>
  );
}

const ACTION_ICON: Record<AgentAction['kind'], typeof Send> = {
  open: ArrowUpRight,
  draft: PenLine,
  task: ListTodo,
  note: StickyNote,
  comment: MessageSquare,
  send: Send,
  assign: UserCheck,
  convert: Repeat2,
  enroll: Workflow,
  merge: GitMerge,
  'create-contact': UserPlus,
  'create-deal': Briefcase,
  'update-deal': Pencil,
};

/** A button under an answer. Writes look like a confirmation; opening and drafting look like links. */
export function ActionChip({ action, busy, pending, index, reduce, onRun }: {
  action: AgentAction;
  busy: boolean;
  pending: boolean;
  index: number;
  reduce: boolean;
  onRun: (action: AgentAction) => void;
}) {
  const Icon = ACTION_ICON[action.kind] ?? Sparkles;
  const write = action.kind !== 'open' && action.kind !== 'draft';
  return (
    <motion.button
      type="button"
      disabled={busy}
      onClick={() => onRun(action)}
      initial={reduce ? false : { opacity: 0, y: 6, scale: 0.94 }}
      animate={{ opacity: 1, y: 0, scale: 1 }}
      transition={{ type: 'spring', stiffness: 420, damping: 26, delay: reduce ? 0 : index * 0.05 }}
      whileHover={reduce || busy ? undefined : { y: -2 }}
      whileTap={reduce || busy ? undefined : { scale: 0.96 }}
      className={cn(
        'group inline-flex min-h-10 items-center gap-1.5 rounded-full border px-3.5 py-1.5 text-sm font-semibold shadow-sm transition-colors disabled:opacity-50',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 focus-visible:ring-offset-2',
        write
          ? 'border-transparent bg-gradient-to-r from-orange-500 to-fuchsia-500 text-white shadow-fuchsia-500/20 hover:shadow-md'
          : 'border-slate-200 bg-white text-slate-700 hover:border-fuchsia-300 hover:text-fuchsia-700 dark:border-slate-600 dark:bg-slate-950 dark:text-slate-100',
      )}
    >
      {pending
        ? <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
        : <Icon className="h-4 w-4 transition-transform motion-safe:group-hover:scale-110" aria-hidden="true" />}
      {action.label}
    </motion.button>
  );
}

/** A question the person can ask with one press. */
export function SuggestionChips({ suggestions, disabled, reduce, onPick, label }: {
  suggestions: string[];
  disabled: boolean;
  reduce: boolean;
  onPick: (text: string) => void;
  label: string;
}) {
  if (suggestions.length === 0) return null;
  return (
    <div className="flex flex-wrap gap-2" role="group" aria-label={label}>
      {suggestions.map((text, index) => (
        <motion.button
          key={text}
          type="button"
          disabled={disabled}
          onClick={() => onPick(text)}
          initial={reduce ? false : { opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3, ease: EASE, delay: reduce ? 0 : 0.08 + index * 0.05 }}
          whileHover={reduce || disabled ? undefined : { y: -2, scale: 1.02 }}
          whileTap={reduce || disabled ? undefined : { scale: 0.97 }}
          className="inline-flex items-center gap-1.5 rounded-full border border-fuchsia-200/80 bg-gradient-to-r from-white to-fuchsia-50/60 px-3 py-1.5 text-xs font-medium text-slate-700 shadow-sm transition-colors hover:border-fuchsia-300 hover:text-fuchsia-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-fuchsia-400 disabled:opacity-50 dark:border-fuchsia-900/60 dark:from-slate-900 dark:to-fuchsia-950/40 dark:text-slate-200"
        >
          <Sparkles className="h-3 w-3 text-fuchsia-500" aria-hidden="true" />
          {text}
        </motion.button>
      ))}
    </div>
  );
}

/** Labels the answers write. Only these are bolded, so a record named "Overdue: …" is not. */
const LABELS = /^(Email|Phone|Job title|Last contact date|Lead score|Subject|Criteria|Not loaded|Leads|Overdue tasks|Close date already passed|Deals quiet for \d+ days|Recorded amount|Proposed amount|Recorded deal|Recorded close date|Proposed close date|Open total(?: in [A-Z]{3})?|Say|Avoid|Next in line|Suggestion, from the saved step)$/;

/**
 * One line of an answer, made easier to scan: "Email: ada@…" gets a bold label, a line
 * ending in a colon reads as a small heading, and a numbered line keeps its number apart.
 */
function Line({ text }: { text: string }) {
  const heading = /^[^:]{2,48}:$/.exec(text);
  if (heading) return <span className="mt-1 block text-[11px] font-semibold uppercase tracking-wider text-fuchsia-700 dark:text-fuchsia-300">{text.slice(0, -1)}</span>;
  const numbered = /^(\d+)\.\s+(.*)$/.exec(text);
  if (numbered) {
    return (
      <span className="flex gap-2">
        <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-fuchsia-100 text-[11px] font-bold text-fuchsia-700 dark:bg-fuchsia-900/50 dark:text-fuchsia-200">{numbered[1]}</span>
        <span>{numbered[2]}</span>
      </span>
    );
  }
  const labelled = /^([^:]+):\s+(.+)$/.exec(text);
  if (labelled?.[1] && LABELS.test(labelled[1])) {
    return (
      <>
        <span className="font-semibold text-slate-900 dark:text-white">{labelled[1]}:</span> {labelled[2]}
      </>
    );
  }
  return <>{text}</>;
}

/** What the person typed: on the right, in the brand gradient. */
export function UserBubble({ text, fresh, reduce }: { text: string; fresh: boolean; reduce: boolean }) {
  return (
    <motion.div
      className="flex justify-end"
      initial={!fresh || reduce ? false : { opacity: 0, x: 24, scale: 0.96 }}
      animate={{ opacity: 1, x: 0, scale: 1 }}
      transition={{ type: 'spring', stiffness: 380, damping: 30 }}
    >
      <p className="sr-only">You:</p>
      <p className="max-w-[85%] whitespace-pre-wrap break-words rounded-2xl rounded-br-md bg-gradient-to-br from-orange-500 to-amber-500 px-4 py-2.5 text-sm text-white shadow-md shadow-orange-500/20">
        {text}
      </p>
    </motion.div>
  );
}

/**
 * An answer: on the left, next to the orb. A new answer arrives a line at a time, each
 * line sharpening into place, so it reads as being worked out. The whole text is in the
 * page from the start, so a screen reader hears it at once.
 */
export function AgentBubble({ text, saved, fresh, reduce, children }: {
  text: string;
  saved?: boolean;
  fresh: boolean;
  reduce: boolean;
  children?: ReactNode;
}) {
  const [copied, setCopied] = useState(false);
  const lines = text.split('\n');
  const animate = fresh && !reduce;
  const copy = () => {
    void navigator.clipboard?.writeText(text).then(() => {
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1400);
    }).catch(() => undefined);
  };
  return (
    <motion.div
      className="group/bubble flex items-start gap-2.5"
      initial={animate ? { opacity: 0, y: 10 } : false}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3, ease: EASE }}
    >
      {saved ? (
        <motion.span
          aria-hidden="true"
          className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-500 text-white shadow-md shadow-emerald-500/30"
          initial={animate ? { scale: 0, rotate: -90 } : false}
          animate={{ scale: 1, rotate: 0 }}
          transition={{ type: 'spring', stiffness: 420, damping: 18 }}
        >
          <CheckCircle2 className="h-4 w-4" />
        </motion.span>
      ) : (
        <AiOrb size={28} reduce={reduce} className="mt-0.5" />
      )}
      <div className="min-w-0 max-w-[92%] flex-1">
        <p className="sr-only">Agent:</p>
        <div
          className={cn(
            'relative rounded-2xl rounded-tl-md border px-4 py-3 text-sm shadow-sm',
            saved
              ? 'border-emerald-200 bg-emerald-50/80 text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100'
              : 'border-slate-200/80 bg-white text-slate-800 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100',
          )}
        >
          {lines.map((line, index) => (
            <motion.p
              // Lines can repeat, so the index is part of the key.
              key={`${index}-${line}`}
              className={cn('whitespace-pre-wrap break-words leading-relaxed', !line.trim() && 'h-2', index === 0 && !saved && 'font-medium')}
              initial={animate ? { opacity: 0, y: 6, filter: 'blur(4px)' } : false}
              animate={{ opacity: 1, y: 0, filter: 'blur(0px)' }}
              transition={{ duration: 0.35, ease: EASE, delay: animate ? Math.min(index, 14) * 0.07 : 0 }}
            >
              {saved ? line : <Line text={line} />}
            </motion.p>
          ))}
          {!saved && (
            <button
              type="button"
              onClick={copy}
              aria-label={copied ? 'Answer copied' : 'Copy this answer'}
              className="absolute -right-2 -top-2 flex h-7 w-7 items-center justify-center rounded-full border border-slate-200 bg-white text-slate-500 opacity-0 shadow-sm transition-opacity hover:text-fuchsia-600 focus-visible:opacity-100 group-hover/bubble:opacity-100 dark:border-slate-600 dark:bg-slate-900"
            >
              {copied ? <Check className="h-3.5 w-3.5 text-emerald-600" /> : <Copy className="h-3.5 w-3.5" />}
            </button>
          )}
        </div>
        {children && (
          <motion.div
            className="mt-2 flex flex-wrap gap-2"
            initial={animate ? { opacity: 0 } : false}
            animate={{ opacity: 1 }}
            transition={{ duration: 0.25, delay: animate ? Math.min(lines.length, 14) * 0.07 + 0.05 : 0 }}
          >
            {children}
          </motion.div>
        )}
      </div>
    </motion.div>
  );
}

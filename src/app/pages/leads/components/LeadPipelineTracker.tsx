import { useCallback, useEffect, useId, useMemo, useRef, useState } from 'react';
import { AnimatePresence, motion } from 'motion/react';
import {
  Phone, Users, FileSignature, PenLine, Wallet,
  Check, X, ChevronDown, ChevronsDownUp, ChevronsUpDown,
  XCircle, AlertCircle, ArrowRightCircle, Sparkles,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { Button } from '@/app/components/ui/button';
import { useMotionPreference } from '@/app/hooks';
import {
  type LeadPipeline, type OutreachStatus, type ContactOutcome,
  type ContractStatus, type ContractSigned, type PhaseStep, type DropoutReason,
  PHASE_TITLES, OUTREACH_LABELS, OUTCOME_LABELS, CONTRACT_LABELS, SIGNED_LABELS,
  DROPOUT_REASONS, DROPOUT_REASON_LABELS,
  phaseCompletion, currentPhase, lostReason, lostPhase, phaseCaptions, phaseSteps,
  isPipelineComplete, isReasonComplete, isReasonRequired, dropoutPhaseFor,
} from '../leadPipeline';
import { usePhaseTransitions } from '../usePhaseTransitions';
import { usePhaseDisclosure, defaultFocus } from '../usePhaseDisclosure';
import { usePhaseMotion, type PhaseMotionTokens } from './phaseMotion';
import { useTrackerAnnouncement } from './useTrackerAnnouncement';

const PHASE_ICONS: LucideIcon[] = [Phone, Users, FileSignature, PenLine, Wallet];

type LogHint = { subject: string; body?: string };

/**
 * One discrete state per card, replacing three overlapping booleans.
 *
 * `done` and `active` were both true on phase 5 of a finished pipeline and both
 * wrote classes: twMerge resolved the border to emerald, but `ring-1
 * ring-indigo-100 shadow-sm` are separate properties and survived — so a
 * completed pipeline rendered an emerald card still wearing an indigo ring.
 */
type PhaseState = 'idle' | 'active' | 'done' | 'lost';

// Idle cards sit BACK into the section's own tint rather than being white like
// the rest. It costs nothing in contrast — their text is already muted — and it
// buys the one thing colour alone was not buying: the phase you are on is a card
// lifted off the page, and the phases you have not reached are part of the page.
const SURFACE: Record<PhaseState, string> = {
  idle: 'border-slate-200/80 bg-slate-50/70',
  active: 'border-indigo-200 bg-white shadow-sm ring-1 ring-indigo-100',
  done: 'border-emerald-200 bg-white',
  lost: 'border-rose-200 bg-white',
};
const BADGE: Record<PhaseState, string> = {
  idle: 'bg-slate-100 text-slate-400',
  active: 'bg-indigo-600 text-white',
  done: 'bg-emerald-500 text-white',
  lost: 'bg-rose-500 text-white',
};
const PILL: Record<PhaseState, string | null> = {
  idle: null,
  active: 'bg-indigo-50 text-indigo-700',
  done: 'bg-emerald-50 text-emerald-700',
  lost: 'bg-rose-50 text-rose-700',
};
const PILL_TEXT: Record<PhaseState, string | null> = {
  idle: null, active: 'In progress', done: 'Complete', lost: 'Stopped',
};
const CAPTION: Record<PhaseState, string> = {
  idle: 'text-slate-400',
  active: 'text-slate-600',
  done: 'text-emerald-700',
  lost: 'text-rose-700',
};
/** Hover affordance on the fold-open header, keyed to the card's own hue. */
const HEADER_HOVER: Record<PhaseState, string> = {
  idle: 'hover:bg-slate-50',
  active: 'hover:bg-indigo-50/60',
  done: 'hover:bg-emerald-50/60',
  lost: 'hover:bg-rose-50/60',
};

/** A row of mutually-exclusive choice chips. */
function Choices<T extends string>({
  options, value, disabled, onPick,
}: {
  options: { value: T; label: string; tone?: 'default' | 'danger' | 'success'; hint?: string | null }[];
  value?: T;
  disabled?: boolean;
  onPick: (v: T) => void;
}) {
  return (
    <div className="flex flex-wrap gap-2">
      {options.map((o) => {
        const active = value === o.value;
        return (
          <button
            key={o.value}
            type="button"
            disabled={disabled}
            aria-pressed={active}
            onClick={() => onPick(o.value)}
            className={cn(
              'rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors duration-150 disabled:opacity-50',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400 focus-visible:ring-offset-1',
              active && o.tone === 'danger' && 'border-rose-300 bg-rose-50 text-rose-700',
              active && o.tone === 'success' && 'border-emerald-300 bg-emerald-50 text-emerald-700',
              active && (!o.tone || o.tone === 'default') && 'border-indigo-300 bg-indigo-50 text-indigo-700',
              !active && 'border-slate-200 bg-white text-slate-600 hover:border-slate-300 hover:bg-slate-50',
            )}
          >
            {o.label}
            {!active && o.hint && (
              <span className="ml-1.5 text-[10px] font-semibold text-slate-400">
                → {o.hint}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
}

/**
 * Why the lead walked away. Rendered at whichever phase recorded the drop-out,
 * from ONE options list, so Meeting and Contract can never drift apart and the
 * report can compare them.
 *
 * "Mandatory" has to mean something particular here: the tracker autosaves on
 * every click, so there is no submit to block. Instead the requirement is made
 * impossible to miss and impossible to lose — a rose border and a Required
 * badge while it is outstanding, the phase caption saying a reason is owed, and
 * a banner at the top of the tracker. The negative answer itself still saves,
 * because refusing to record "they said no" until a dropdown is filled would
 * lose the more important fact.
 */
function DropoutReasonField({
  phase, value, disabled, onChange,
}: {
  /** 1-based phase this field belongs to — stamped onto the reason when set. */
  phase: number;
  value: LeadPipeline;
  disabled?: boolean;
  onChange: (patch: Partial<LeadPipeline>, log?: LogHint) => void;
}) {
  const complete = isReasonComplete(value);
  const needsText = value.dropoutReason === 'other';

  return (
    <div
      className={cn(
        'space-y-2 rounded-xl border p-3',
        complete ? 'border-slate-200 bg-slate-50/70' : 'border-rose-300 bg-rose-50/60',
      )}
    >
      <div className="flex flex-wrap items-center gap-2">
        <span className="text-xs font-semibold text-slate-700">Reason they are not proceeding</span>
        {complete
          ? <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-700">Recorded</span>
          : <span className="rounded-full bg-rose-100 px-2 py-0.5 text-[10px] font-bold text-rose-700">Required</span>}
      </div>

      <Choices<DropoutReason>
        disabled={disabled}
        value={value.dropoutReason}
        options={DROPOUT_REASONS.map((r) => ({
          value: r,
          label: DROPOUT_REASON_LABELS[r],
          tone: 'default' as const,
        }))}
        onPick={(r) => onChange(
          {
            dropoutReason: r,
            // Stamp the phase only on first capture; a reason already explained
            // at an earlier phase keeps that attribution.
            dropoutReasonPhase: value.dropoutReasonPhase ?? phase,
            // Switching away from "Other" drops the now-meaningless free text.
            ...(r === 'other' ? {} : { dropoutReasonOther: undefined }),
          },
          { subject: `Drop-out reason: ${DROPOUT_REASON_LABELS[r]}`, body: `Recorded at phase ${value.dropoutReasonPhase ?? phase}` },
        )}
      />

      {needsText && (
        <label className="block space-y-1">
          <span className="text-xs font-medium text-slate-500">
            Please say more <span className="text-rose-600">(required)</span>
          </span>
          <textarea
            rows={2}
            disabled={disabled}
            value={value.dropoutReasonOther ?? ''}
            onChange={(e) => onChange({ dropoutReasonOther: e.target.value })}
            onBlur={(e) => {
              const t = e.target.value.trim();
              if (t) onChange({ dropoutReasonOther: t }, { subject: 'Drop-out reason (other)', body: t });
            }}
            placeholder="In their words, if possible"
            className={cn(
              'w-full rounded-lg border bg-white px-3 py-1.5 text-sm text-slate-700 disabled:opacity-50',
              'focus:outline-none focus:ring-2 focus:ring-indigo-100',
              value.dropoutReasonOther?.trim() ? 'border-slate-200 focus:border-indigo-400' : 'border-rose-300',
            )}
          />
        </label>
      )}
    </div>
  );
}

/** A labelled date field. */
function DateField({
  label, value, disabled, onChange,
}: {
  label: string;
  value?: string;
  disabled?: boolean;
  onChange: (v: string) => void;
}) {
  return (
    <label className="flex flex-col gap-1 text-sm">
      <span className="text-xs font-medium text-slate-500">{label}</span>
      <input
        type="date"
        value={value ?? ''}
        disabled={disabled}
        onChange={(e) => onChange(e.target.value)}
        className="w-full max-w-[200px] rounded-lg border border-slate-200 bg-white px-3 py-1.5 text-sm text-slate-700 disabled:opacity-50 focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-100"
      />
    </label>
  );
}

/**
 * Progress WITHIN a phase, one dot per field.
 *
 * Three states, because two would lie: a field answered "Profile rejected" is
 * neither blank nor satisfied, and the dot that represents it should not look
 * like either. Filling in as you go also gives the folded header something to
 * do — you can watch a phase fill without expanding it.
 */
function StepDots({ steps, lost }: { steps: PhaseStep[]; lost: boolean }) {
  return (
    <span aria-hidden className="flex items-center gap-[3px]">
      {steps.map((s) => (
        <span
          key={s.label}
          className={cn(
            'h-1.5 w-1.5 rounded-full transition-colors duration-200',
            s.done ? 'bg-emerald-500' : s.value ? (lost ? 'bg-rose-400' : 'bg-amber-400') : 'bg-slate-200',
          )}
        />
      ))}
    </span>
  );
}

/**
 * The answers already on a folded card, for phases that are still open.
 *
 * Deliberately NOT shown on a finished or stopped phase: there the caption
 * already reports the outcome ("Attended · still interested"), and repeating it
 * as chips two inches to the right is noise dressed up as information. On a
 * phase still in progress the caption says what is MISSING, so what is present
 * is genuinely unsaid. Hidden below `md`, where there is no room for it.
 */
function ValueChips({ steps }: { steps: PhaseStep[] }) {
  const values = steps.map((s) => s.value).filter((v): v is string => !!v);
  if (values.length === 0) return null;
  return (
    <span aria-hidden className="hidden items-center gap-1 md:flex">
      {values.slice(0, 2).map((v, i) => (
        <span
          key={`${i}-${v}`}
          className="max-w-[10rem] truncate rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-medium text-slate-500"
        >
          {v}
        </span>
      ))}
      {values.length > 2 && (
        <span className="text-[10px] font-medium text-slate-400">+{values.length - 2}</span>
      )}
    </span>
  );
}

function PhaseCard({
  n, title, caption, state, celebrate, steps, open, onToggle, t, cardRef, children,
}: {
  n: number;
  title: string;
  caption: string;
  state: PhaseState;
  /** This card is the one that just completed — play the one-shot beats. */
  celebrate: boolean;
  steps: PhaseStep[];
  open: boolean;
  onToggle: () => void;
  t: PhaseMotionTokens;
  cardRef: (el: HTMLLIElement | null) => void;
  children: React.ReactNode;
}) {
  const Icon = PHASE_ICONS[n - 1] ?? Phone;
  const isDone = state === 'done';
  const uid = useId();
  const bodyId = `${uid}-phase-${n}`;
  // The short tick marks "you are here" and "it stopped here". It is HELD at
  // full height while done so it never retracts upward against the downward seal.
  const tickOn = state === 'active' || state === 'lost';

  // The fold is the one animation that moves layout, so the box is clipped only
  // while it is actually moving. Left clipped permanently, it would shear the
  // focus ring off the leftmost chip inside.
  const [clipping, setClipping] = useState(false);

  // Keyframe arrays are re-created each render; memoising keeps the target
  // identity stable. Framer already shallow-compares keyframes, so this is
  // cheap insurance rather than a bug fix.
  const badgeAnimate = useMemo(
    () => (celebrate ? { scale: [1, 0.9, 1] } : { scale: 1 }),
    [celebrate],
  );

  return (
    <li
      ref={cardRef}
      aria-current={state === 'active' ? 'step' : undefined}
      className={cn(
        // No overflow-hidden: the accent bars are inset from the corners instead,
        // so a focused chip's ring is never clipped at the card edge.
        'relative rounded-2xl border p-2 sm:p-2.5',
        'transition-[border-color,box-shadow,background-color] duration-[240ms] ease-out',
        SURFACE[state],
      )}
    >
      {/* THE SEAL — pours down the left edge when this phase completes. */}
      <motion.span
        aria-hidden
        initial={false}
        animate={{ scaleY: isDone ? 1 : 0 }}
        transition={celebrate ? t.seal : { duration: 0 }}
        className="pointer-events-none absolute bottom-3 left-0 top-3 w-[3px] origin-top rounded-full bg-emerald-500"
      />
      {/* THE LANDING — the short tick for the active (or stopped) card. */}
      <motion.span
        aria-hidden
        initial={false}
        animate={{ scaleY: tickOn || isDone ? 1 : 0, opacity: tickOn ? 1 : 0 }}
        transition={{ scaleY: t.accent, opacity: t.tickOut }}
        className={cn(
          'pointer-events-none absolute left-0 top-3 h-7 w-[3px] origin-top rounded-full',
          'transition-colors duration-[240ms]',
          state === 'lost' ? 'bg-rose-500' : 'bg-indigo-500',
        )}
      />

      {/* The header IS the toggle: the whole row, not a 24px chevron.
          Heading-wraps-button is the APG accordion pattern — a heading cannot
          live INSIDE a button (phrasing content only), and dropping it would
          take all five phases out of the document outline. */}
      <h3>
      <button
        type="button"
        onClick={onToggle}
        aria-expanded={open}
        aria-controls={bodyId}
        data-phase={title}
        className={cn(
          'flex w-full items-start gap-3 rounded-xl p-2 text-left transition-colors duration-150 sm:p-2.5',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
          HEADER_HOVER[state],
        )}
      >
        <motion.span
          initial={false}
          animate={badgeAnimate}
          transition={celebrate ? t.badge : { duration: 0 }}
          className={cn(
            'relative flex h-10 w-10 shrink-0 items-center justify-center rounded-xl',
            'transition-colors duration-[240ms]',
            BADGE[state],
          )}
        >
          {/* Both glyphs stay mounted and absolutely positioned, so the badge is
              never empty mid-swap and nothing is measured or pinned out of flow. */}
          <motion.span
            aria-hidden
            initial={false}
            animate={{ opacity: isDone ? 0 : 1, scale: isDone ? 0.82 : 1 }}
            transition={isDone ? t.glyphOut : t.glyphIn}
            className="absolute inset-0 grid place-items-center"
          >
            {state === 'lost' ? <X className="h-5 w-5" strokeWidth={2.75} /> : <Icon className="h-5 w-5" />}
          </motion.span>
          <motion.span
            aria-hidden
            initial={false}
            animate={{ opacity: isDone ? 1 : 0, scale: isDone ? 1 : 0.82 }}
            transition={isDone ? t.glyphIn : t.glyphOut}
            className="absolute inset-0 grid place-items-center"
          >
            <Check className="h-5 w-5" strokeWidth={2.75} />
          </motion.span>
        </motion.span>

        <span className="min-w-0 flex-1">
          <span className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
            <span className="text-[15px] font-semibold tracking-[-0.01em] text-slate-900">{title}</span>
            <span className="shrink-0 text-[11px] font-medium tabular-nums text-slate-500">Phase {n}</span>
            {PILL[state] && (
              <span className={cn('rounded-full px-2 py-0.5 text-[10px] font-bold', PILL[state])}>
                {PILL_TEXT[state]}
              </span>
            )}
          </span>
          <span className={cn('mt-1 block text-xs transition-colors duration-[240ms]', CAPTION[state])}>
            {caption}
          </span>
        </span>

        <span className="flex shrink-0 items-center gap-2 pt-1.5">
          {!open && state !== 'done' && state !== 'lost' && <ValueChips steps={steps} />}
          <StepDots steps={steps} lost={state === 'lost'} />
          <motion.span
            aria-hidden
            initial={false}
            animate={{ rotate: open ? 180 : 0 }}
            transition={t.chevron}
            className={cn(
              'grid h-6 w-6 place-items-center rounded-lg text-slate-400',
              open && 'bg-slate-100 text-slate-500',
            )}
          >
            <ChevronDown className="h-4 w-4" />
          </motion.span>
        </span>
      </button>
      </h3>

      <AnimatePresence initial={false}>
        {open && (
          <motion.div
            key="body"
            id={bodyId}
            initial={{ height: 0, opacity: 0 }}
            animate={{ height: 'auto', opacity: 1 }}
            exit={{ height: 0, opacity: 0 }}
            transition={t.disclosure}
            onAnimationStart={() => setClipping(true)}
            onAnimationComplete={() => setClipping(false)}
            className={cn(clipping && 'overflow-hidden')}
          >
            {/* Padding rather than margin: a margin outside the animated box does
                not collapse with the height and leaves a 16px ghost at zero. */}
            <div className="space-y-4 px-2 pb-2 pt-3 sm:pl-[62px] sm:pr-3">{children}</div>
          </motion.div>
        )}
      </AnimatePresence>
    </li>
  );
}

interface Props {
  value: LeadPipeline;
  disabled?: boolean;
  /** Persist a new pipeline state; `log` (if given) is written to the activity timeline. */
  onChange: (next: LeadPipeline, log?: LogHint) => void;
  /**
   * The lead status the given edit would produce, or null when it would not move
   * the status. Rendered inline on the chip so the consequence is visible BEFORE
   * the click — a hover-only hint is no use on touch or to a fast clicker.
   */
  previewStatus?: (patch: Partial<LeadPipeline>) => string | null;
  /** Fired when the user chooses to turn the completed lead into a deal. */
  onConvert?: () => void;
}

export function LeadPipelineTracker({ value, disabled = false, onChange, previewStatus, onConvert }: Props) {
  const reduce = useMotionPreference();
  const t = usePhaseMotion(reduce);

  const done = useMemo(() => phaseCompletion(value), [value]);
  const phase = useMemo(() => currentPhase(value), [value]);
  const lost = useMemo(() => lostReason(value), [value]);
  const lostAt = useMemo(() => lostPhase(value), [value]);
  const captions = useMemo(() => phaseCaptions(value), [value]);
  const steps = useMemo(() => phaseSteps(value), [value]);
  const complete = useMemo(() => isPipelineComplete(value), [value]);
  const doneCount = done.filter(Boolean).length;

  // Gated on `!disabled` only, deliberately NOT `!disabled && !lost`: a lost lead
  // must still ANNOUNCE its state change to a screen reader. `lost` suppresses
  // the visual celebration per-element instead.
  const { justCompleted, nonce } = usePhaseTransitions(done, { enabled: !disabled, holdMs: 900 });
  const announcement = useTrackerAnnouncement({ justCompleted, nonce, phase, lost, complete });

  const reasonPhase = useMemo(() => dropoutPhaseFor(value), [value]);
  const reasonOutstanding = useMemo(
    () => isReasonRequired(value) && !isReasonComplete(value),
    [value],
  );

  // An owed reason outranks the ordinary focus rule: the card that needs
  // something from you should be the one already open.
  const focus = useMemo(
    () => (reasonOutstanding && reasonPhase !== null
      ? reasonPhase - 1
      : defaultFocus({ currentPhase: phase, lostPhase: lostAt, complete })),
    [reasonOutstanding, reasonPhase, phase, lostAt, complete],
  );
  const disclosure = usePhaseDisclosure(focus, PHASE_TITLES.length);

  const cards = useRef<(HTMLLIElement | null)[]>([]);
  const setCardRef = useCallback(
    (i: number) => (el: HTMLLIElement | null) => { cards.current[i] = el; },
    [],
  );

  // After a phase completes, bring the card you have been handed to into view —
  // but only if it is not already there. `block: 'nearest'` is doing that work:
  // it is a no-op when the element is on screen, so this never yanks the page
  // around for someone who can already see it. Delayed past the fold so the
  // measurement happens against the card's final height, not its opening one.
  useEffect(() => {
    if (nonce === 0 || justCompleted === null || reduce) return;
    const target = cards.current[Math.min(justCompleted + 1, PHASE_TITLES.length - 1)];
    if (!target?.scrollIntoView) return;
    const id = setTimeout(() => target.scrollIntoView({ block: 'nearest', behavior: 'smooth' }), 280);
    return () => clearTimeout(id);
    // Keyed on the nonce alone — the same reasoning as the announcement effect.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nonce]);

  const patch = (partial: Partial<LeadPipeline>, log?: LogHint) => onChange({ ...value, ...partial }, log);
  /** Status this choice would produce, for the inline consequence label. */
  const hint = (partial: Partial<LeadPipeline>) => previewStatus?.(partial) ?? null;

  const contacted = value.outreachStatus === 'contacted';
  const wantsMeeting = contacted && (value.contactOutcome === 'meeting_scheduled' || value.contactOutcome === 'follow_up');

  const stateFor = (i: number): PhaseState => {
    if (done[i]) return 'done';
    if (lostAt === i + 1) return 'lost';
    if (phase === i + 1 && !lost) return 'active';
    return 'idle';
  };
  /** Only the card that just completed plays the one-shot beats — never a lost one. */
  const celebrates = (i: number) => justCompleted === i && !lost && !reduce;

  /** Jump to a phase from the rail: open it and scroll it into view. */
  const jumpTo = (i: number) => {
    disclosure.reveal(i);
    const el = cards.current[i];
    el?.scrollIntoView?.({ block: 'nearest', behavior: reduce ? 'auto' : 'smooth' });
  };

  const card = (i: number, children: React.ReactNode) => (
    <PhaseCard
      n={i + 1}
      title={PHASE_TITLES[i]!}
      caption={captions[i] ?? ''}
      state={stateFor(i)}
      celebrate={celebrates(i)}
      steps={steps[i] ?? []}
      open={disclosure.isOpen(i)}
      onToggle={() => disclosure.toggle(i)}
      cardRef={setCardRef(i)}
      t={t}
    >
      {children}
    </PhaseCard>
  );

  return (
    <section className="rounded-3xl border border-slate-200 bg-slate-50/60 p-5">
      {/* The transition said in words. Polite, so it never interrupts. */}
      <p aria-live="polite" aria-atomic="true" className="sr-only">{announcement}</p>

      <div className="mb-5">
        <h2 className="flex items-center gap-2 text-base font-bold text-slate-900">
          <Sparkles className="h-4 w-4 text-indigo-600" /> Lead lifecycle
        </h2>
        <p className={cn(
          'mt-0.5 text-[13px]',
          lost ? 'font-medium text-rose-700' : complete ? 'font-medium text-emerald-700' : 'text-slate-500',
        )}>
          {lost
            ? `Stopped — ${lost}`
            : complete
              ? 'All phases complete — ready to become a deal'
              : `Currently in Phase ${phase}: ${PHASE_TITLES[phase - 1]}`}
        </p>

        {/* The meter. Full width so the connectors read as distance travelled
            rather than as hyphens between dots. Every pip is a jump target. */}
        <div className="mt-4 flex items-start gap-2 rounded-2xl border border-slate-200 bg-white/80 px-3 py-2.5 shadow-[0_1px_2px_rgba(15,23,42,0.04)] sm:gap-3">
          <span className={cn(
            'mt-1.5 shrink-0 text-[11px] font-bold tabular-nums',
            doneCount === PHASE_TITLES.length ? 'text-emerald-700' : 'text-slate-500',
          )}>
            {doneCount}/{PHASE_TITLES.length}
          </span>

          <ol className="flex flex-1 items-start" aria-label="Pipeline progress">
            {PHASE_TITLES.map((title, i) => {
              const st = stateFor(i);
              const isDone = st === 'done';
              const isCurrent = st === 'active';
              const isLostHere = st === 'lost';
              const word = isDone ? 'complete' : isCurrent ? 'in progress' : isLostHere ? 'stopped here' : 'not started';
              return (
                <li key={title} className={cn('flex items-start', i < PHASE_TITLES.length - 1 && 'flex-1')}>
                  <button
                    type="button"
                    onClick={() => jumpTo(i)}
                    aria-current={isCurrent ? 'step' : undefined}
                    className={cn(
                      'group flex w-8 shrink-0 flex-col items-center gap-1.5 rounded-lg py-0.5 sm:w-[4.5rem]',
                      'transition-colors duration-150 hover:bg-slate-50',
                      'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
                    )}
                  >
                    <span
                      className={cn(
                        'flex h-6 w-6 items-center justify-center rounded-full border text-[11px] font-bold transition-all duration-200',
                        'group-hover:scale-110',
                        // Filled = banked, OUTLINED = current, flat = future. A
                        // shape difference, so it survives greyscale and
                        // red-green deficiency where hue alone would not.
                        isDone && 'border-emerald-500 bg-emerald-500 text-white',
                        isCurrent && 'border-indigo-500 bg-white text-indigo-700 shadow-[0_0_0_4px_#e0e7ff]',
                        isLostHere && 'border-rose-500 bg-rose-500 text-white',
                        !isDone && !isCurrent && !isLostHere && 'border-slate-200 bg-slate-100 text-slate-400',
                      )}
                    >
                      <span className="sr-only">{`Phase ${i + 1}, ${title}: ${word}. Show this phase.`}</span>
                      <span aria-hidden>
                        {isDone ? <Check className="h-3.5 w-3.5" strokeWidth={3} />
                          : isLostHere ? <X className="h-3.5 w-3.5" strokeWidth={3} />
                            : i + 1}
                      </span>
                    </span>
                    <span aria-hidden className={cn(
                      'hidden text-center text-[10px] font-medium leading-tight sm:block',
                      isDone ? 'text-emerald-700' : isCurrent ? 'text-indigo-700' : isLostHere ? 'text-rose-700' : 'text-slate-400',
                    )}>
                      {title}
                    </span>
                  </button>
                  {i < PHASE_TITLES.length - 1 && (
                    <span aria-hidden className="mt-[13px] h-[3px] flex-1 overflow-hidden rounded-full bg-slate-200">
                      <motion.span
                        initial={false}
                        animate={{ scaleX: done[i] ? 1 : 0 }}
                        transition={celebrates(i) ? t.connector : { duration: 0 }}
                        className="block h-full w-full origin-left rounded-full bg-emerald-400"
                      />
                    </span>
                  )}
                </li>
              );
            })}
          </ol>

          {/* The visible word is the first word of the label, so the accessible
              name still contains it once the text drops away below `sm`. */}
          <button
            type="button"
            onClick={() => disclosure.setAll(!disclosure.allOpen)}
            aria-label={disclosure.allOpen ? 'Collapse all phases' : 'Expand all phases'}
            className={cn(
              'mt-0.5 ml-1 flex shrink-0 items-center gap-1 rounded-lg border-l border-slate-200 py-1 pl-2 pr-1.5 text-[11px] font-semibold text-slate-500',
              'transition-colors duration-150 hover:bg-slate-100 hover:text-slate-700',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400',
            )}
          >
            {disclosure.allOpen
              ? <ChevronsDownUp className="h-3.5 w-3.5" />
              : <ChevronsUpDown className="h-3.5 w-3.5" />}
            <span aria-hidden className="hidden sm:inline">
              {disclosure.allOpen ? 'Collapse' : 'Expand all'}
            </span>
          </button>
        </div>
      </div>

      {lost && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
          <XCircle className="h-4 w-4 shrink-0" /> This lead dropped out: <strong>{lost}</strong>. You can still update any phase to reopen it.
        </div>
      )}

      {/* An outstanding reason is announced here as well as on the card, because
          the card it belongs to may be folded away. */}
      {reasonOutstanding && (
        <div className="mb-4 flex items-start gap-2 rounded-xl border border-rose-300 bg-rose-100/70 px-3 py-2 text-sm text-rose-800">
          <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
          <span>
            <strong>A reason is required.</strong> Open Phase {reasonPhase} and record why they are
            not proceeding, so this shows up in the drop-off report.
          </span>
        </div>
      )}

      <ol className="space-y-2.5">
        {/* Phase 1 — Outreach */}
        {card(0, (
          <>
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Outreach status</span>
              <Choices<OutreachStatus>
                disabled={disabled}
                value={value.outreachStatus}
                options={[
                  { value: 'attempted_no_answer', label: OUTREACH_LABELS.attempted_no_answer, hint: hint({ outreachStatus: 'attempted_no_answer' }) },
                  { value: 'contacted', label: OUTREACH_LABELS.contacted, tone: 'success', hint: hint({ outreachStatus: 'contacted' }) },
                ]}
                onPick={(v) => patch({ outreachStatus: v }, { subject: `Outreach: ${OUTREACH_LABELS[v]}`, body: value.outreachDate ? `Interaction date: ${value.outreachDate}` : undefined })}
              />
            </div>
            <DateField label="Date of interaction" value={value.outreachDate} disabled={disabled}
              onChange={(v) => patch({ outreachDate: v })} />

            {contacted && (
              <div className="space-y-1.5 border-t border-slate-100 pt-3">
                <span className="text-xs font-medium text-slate-500">Result of contact</span>
                <Choices<ContactOutcome>
                  disabled={disabled}
                  value={value.contactOutcome}
                  options={[
                    { value: 'meeting_scheduled', label: OUTCOME_LABELS.meeting_scheduled, tone: 'success', hint: hint({ contactOutcome: 'meeting_scheduled' }) },
                    { value: 'follow_up', label: OUTCOME_LABELS.follow_up, hint: hint({ contactOutcome: 'follow_up' }) },
                    { value: 'not_interested', label: OUTCOME_LABELS.not_interested, tone: 'danger', hint: hint({ contactOutcome: 'not_interested' }) },
                  ]}
                  onPick={(v) => patch({ contactOutcome: v }, { subject: `Contact result: ${OUTCOME_LABELS[v]}` })}
                />
                {wantsMeeting && (
                  <div className="pt-2">
                    <DateField
                      label={value.contactOutcome === 'follow_up' ? 'Follow-up date' : 'Meeting date'}
                      value={value.meetingDate}
                      disabled={disabled}
                      onChange={(v) => patch({ meetingDate: v }, { subject: 'Meeting date set', body: v })}
                    />
                  </div>
                )}
              </div>
            )}
          </>
        ))}

        {/* Phase 2 — After the meeting */}
        {card(1, (
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Showed up to the meeting?</span>
              <Choices<'yes' | 'no'>
                disabled={disabled}
                value={value.meetingAttended === undefined ? undefined : value.meetingAttended ? 'yes' : 'no'}
                options={[
                  { value: 'yes', label: 'Yes', tone: 'success', hint: hint({ meetingAttended: true }) },
                  { value: 'no', label: 'No', tone: 'danger', hint: hint({ meetingAttended: false }) },
                ]}
                onPick={(v) => patch({ meetingAttended: v === 'yes' }, { subject: `Meeting attended: ${v === 'yes' ? 'Yes' : 'No'}` })}
              />
            </div>
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Still interested?</span>
              <Choices<'yes' | 'no'>
                disabled={disabled}
                value={value.stillInterested === undefined ? undefined : value.stillInterested ? 'yes' : 'no'}
                options={[
                  { value: 'yes', label: 'Yes', tone: 'success', hint: hint({ stillInterested: true }) },
                  { value: 'no', label: 'No', tone: 'danger', hint: hint({ stillInterested: false }) },
                ]}
                onPick={(v) => patch(
                  v === 'yes'
                    // Reopening a lead clears the explanation for a departure
                    // that no longer happened, so it cannot linger in the report.
                    ? { stillInterested: true, dropoutReason: undefined, dropoutReasonOther: undefined, dropoutReasonPhase: undefined }
                    : { stillInterested: false },
                  { subject: `Still interested: ${v === 'yes' ? 'Yes' : 'No'}` },
                )}
              />
            </div>
            {value.stillInterested === false && (
              <div className="sm:col-span-2">
                <DropoutReasonField phase={2} value={value} disabled={disabled} onChange={patch} />
              </div>
            )}
          </div>
        ))}

        {/* Phase 3 — Contract */}
        {card(2, (
          <>
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Contract status</span>
              <Choices<ContractStatus>
                disabled={disabled}
                value={value.contractStatus}
                options={[
                  { value: 'yes', label: CONTRACT_LABELS.yes, tone: 'success', hint: hint({ contractStatus: 'yes' }) },
                  { value: 'to_be_sent', label: CONTRACT_LABELS.to_be_sent, hint: hint({ contractStatus: 'to_be_sent' }) },
                  { value: 'profile_rejected', label: CONTRACT_LABELS.profile_rejected, tone: 'danger', hint: hint({ contractStatus: 'profile_rejected' }) },
                  { value: 'no_longer_interested', label: CONTRACT_LABELS.no_longer_interested, tone: 'danger', hint: hint({ contractStatus: 'no_longer_interested' }) },
                ]}
                onPick={(v) => patch(
                  v === 'no_longer_interested'
                    ? { contractStatus: v }
                    : { contractStatus: v, ...(dropoutPhaseFor(value) === 3 ? { dropoutReason: undefined, dropoutReasonOther: undefined, dropoutReasonPhase: undefined } : {}) },
                  { subject: `Contract: ${CONTRACT_LABELS[v]}` },
                )}
              />
            </div>
            {value.contractStatus === 'no_longer_interested' && (
              <DropoutReasonField phase={3} value={value} disabled={disabled} onChange={patch} />
            )}
            <DateField label="Contract sent date" value={value.contractSentDate} disabled={disabled}
              onChange={(v) => patch({ contractSentDate: v }, { subject: 'Contract sent', body: v })} />
          </>
        ))}

        {/* Phase 4 — Signature */}
        {card(3, (
          <>
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Signature status</span>
              <Choices<ContractSigned>
                disabled={disabled}
                value={value.contractSigned}
                options={[
                  { value: 'yes', label: SIGNED_LABELS.yes, tone: 'success', hint: hint({ contractSigned: 'yes' }) },
                  { value: 'pending', label: SIGNED_LABELS.pending, hint: hint({ contractSigned: 'pending' }) },
                  { value: 'no', label: SIGNED_LABELS.no, tone: 'danger', hint: hint({ contractSigned: 'no' }) },
                ]}
                onPick={(v) => patch({ contractSigned: v }, { subject: `Contract signature: ${SIGNED_LABELS[v]}` })}
              />
            </div>
            <DateField label="Signature date" value={value.signatureDate} disabled={disabled}
              onChange={(v) => patch({ signatureDate: v }, { subject: 'Contract signed', body: v })} />
          </>
        ))}

        {/* Phase 5 — Deposit */}
        {card(4, (
          <>
            <div className="space-y-1.5">
              <span className="text-xs font-medium text-slate-500">Deposit paid?</span>
              <Choices<'yes' | 'no'>
                disabled={disabled}
                value={value.depositPaid === undefined ? undefined : value.depositPaid ? 'yes' : 'no'}
                options={[
                  { value: 'yes', label: 'Yes', tone: 'success', hint: hint({ depositPaid: true }) },
                  { value: 'no', label: 'No', tone: 'danger', hint: hint({ depositPaid: false }) },
                ]}
                onPick={(v) => patch({ depositPaid: v === 'yes' }, { subject: `Deposit paid: ${v === 'yes' ? 'Yes' : 'No'}` })}
              />
            </div>
            <DateField label="Payment date" value={value.paymentDate} disabled={disabled}
              onChange={(v) => patch({ paymentDate: v }, { subject: 'Deposit paid', body: v })} />
          </>
        ))}
      </ol>

      {/* Completion → become a deal. Arrives after the phase-5 seal, so the
          reading order is "done → therefore this". */}
      {complete && onConvert && (
        <motion.div
          initial={celebrates(PHASE_TITLES.length - 1) ? { opacity: 0, y: 6 } : false}
          animate={{ opacity: 1, y: 0 }}
          transition={t.convert}
          className="mt-4 flex flex-col items-start gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 sm:flex-row sm:items-center sm:justify-between"
        >
          <div className="flex items-center gap-3">
            <span className="flex h-10 w-10 items-center justify-center rounded-xl bg-emerald-500 text-white">
              <Check className="h-5 w-5" />
            </span>
            <div>
              <p className="text-sm font-semibold text-emerald-900">All five phases complete</p>
              <p className="text-xs text-emerald-700">Deposit received — this lead is ready to become a deal.</p>
            </div>
          </div>
          <Button onClick={onConvert} disabled={disabled} className="gap-1.5 bg-emerald-600 hover:bg-emerald-700">
            <ArrowRightCircle className="h-4 w-4" /> Convert to deal
          </Button>
        </motion.div>
      )}
    </section>
  );
}

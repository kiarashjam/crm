import { useMemo } from 'react';
import { motion } from 'motion/react';
import {
  Phone, Users, FileSignature, PenLine, Wallet,
  Check, X, Circle, XCircle, ArrowRightCircle, Sparkles,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { Button } from '@/app/components/ui/button';
import { useMotionPreference } from '@/app/hooks';
import {
  type LeadPipeline, type OutreachStatus, type ContactOutcome,
  type ContractStatus, type ContractSigned,
  PHASE_TITLES, OUTREACH_LABELS, OUTCOME_LABELS, CONTRACT_LABELS, SIGNED_LABELS,
  phaseCompletion, currentPhase, lostReason, lostPhase, phaseCaptions, isPipelineComplete,
} from '../leadPipeline';
import { usePhaseTransitions } from '../usePhaseTransitions';
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

const SURFACE: Record<PhaseState, string> = {
  idle: 'border-slate-200',
  active: 'border-indigo-200 shadow-sm ring-1 ring-indigo-100',
  done: 'border-emerald-200',
  lost: 'border-rose-200',
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

function PhaseShell({
  n, title, caption, state, celebrate, t, children,
}: {
  n: number;
  title: string;
  caption: string;
  state: PhaseState;
  /** This card is the one that just completed — play the one-shot beats. */
  celebrate: boolean;
  t: PhaseMotionTokens;
  children: React.ReactNode;
}) {
  const Icon = PHASE_ICONS[n - 1] ?? Phone;
  const isDone = state === 'done';
  // The short tick marks "you are here" and "it stopped here". It is HELD at
  // full height while done so it never retracts upward against the downward seal.
  const tickOn = state === 'active' || state === 'lost';

  // Keyframe arrays are re-created each render; memoising keeps the target
  // identity stable. Framer already shallow-compares keyframes, so this is
  // cheap insurance rather than a bug fix.
  const badgeAnimate = useMemo(
    () => (celebrate ? { scale: [1, 0.9, 1] } : { scale: 1 }),
    [celebrate],
  );

  return (
    <li
      aria-current={state === 'active' ? 'step' : undefined}
      className={cn(
        // No overflow-hidden: the accent bars are inset from the corners instead,
        // so a focused chip's ring is never clipped at the card edge.
        'relative rounded-2xl border bg-white p-4 sm:p-5',
        'transition-[border-color,box-shadow] duration-[240ms] ease-out',
        SURFACE[state],
      )}
    >
      {/* THE SEAL — pours down the left edge when this phase completes. */}
      <motion.span
        aria-hidden
        initial={false}
        animate={{ scaleY: isDone ? 1 : 0 }}
        transition={celebrate ? t.seal : { duration: 0 }}
        className="pointer-events-none absolute bottom-4 left-0 top-4 w-[3px] origin-top rounded-full bg-emerald-500"
      />
      {/* THE LANDING — the short tick for the active (or stopped) card. */}
      <motion.span
        aria-hidden
        initial={false}
        animate={{ scaleY: tickOn || isDone ? 1 : 0, opacity: tickOn ? 1 : 0 }}
        transition={{ scaleY: t.accent, opacity: t.tickOut }}
        className={cn(
          'pointer-events-none absolute left-0 top-4 h-7 w-[3px] origin-top rounded-full',
          'transition-colors duration-[240ms]',
          state === 'lost' ? 'bg-rose-500' : 'bg-indigo-500',
        )}
      />

      <div className="flex items-start gap-3">
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

        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
            <h3 className="text-[15px] font-semibold tracking-[-0.01em] text-slate-900">{title}</h3>
            <span className="shrink-0 text-[11px] font-medium tabular-nums text-slate-500">Phase {n}</span>
            {PILL[state] && (
              <span className={cn('rounded-full px-2 py-0.5 text-[10px] font-bold', PILL[state])}>
                {PILL_TEXT[state]}
              </span>
            )}
          </div>
          <p className={cn('mt-1 text-xs transition-colors duration-[240ms]', CAPTION[state])}>{caption}</p>
        </div>
      </div>

      <div className="mt-4 space-y-4 pl-0 sm:pl-[52px]">{children}</div>
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
  const complete = useMemo(() => isPipelineComplete(value), [value]);
  const doneCount = done.filter(Boolean).length;

  // Gated on `!disabled` only, deliberately NOT `!disabled && !lost`: a lost lead
  // must still ANNOUNCE its state change to a screen reader. `lost` suppresses
  // the visual celebration per-element instead.
  const { justCompleted, nonce } = usePhaseTransitions(done, { enabled: !disabled, holdMs: 900 });
  const announcement = useTrackerAnnouncement({ justCompleted, nonce, phase, lost, complete });

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

  const shell = (i: number, children: React.ReactNode) => (
    <PhaseShell
      n={i + 1}
      title={PHASE_TITLES[i]!}
      caption={captions[i] ?? ''}
      state={stateFor(i)}
      celebrate={celebrates(i)}
      t={t}
    >
      {children}
    </PhaseShell>
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
            rather than as hyphens between dots. */}
        <div className="mt-4 flex items-start gap-3 rounded-2xl border border-slate-200 bg-white/80 px-3 py-2.5 shadow-[0_1px_2px_rgba(15,23,42,0.04)]">
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
                  <div className="flex w-8 shrink-0 flex-col items-center gap-1.5 sm:w-[4.5rem]">
                    <span
                      aria-current={isCurrent ? 'step' : undefined}
                      className={cn(
                        'flex h-6 w-6 items-center justify-center rounded-full border text-[11px] font-bold transition-colors duration-200',
                        // Filled = banked, OUTLINED = current, flat = future. A
                        // shape difference, so it survives greyscale and
                        // red-green deficiency where hue alone would not.
                        isDone && 'border-emerald-500 bg-emerald-500 text-white',
                        isCurrent && 'border-indigo-500 bg-white text-indigo-700 shadow-[0_0_0_4px_#e0e7ff]',
                        isLostHere && 'border-rose-500 bg-rose-500 text-white',
                        !isDone && !isCurrent && !isLostHere && 'border-slate-200 bg-slate-100 text-slate-400',
                      )}
                    >
                      <span className="sr-only">{`Phase ${i + 1}, ${title}: ${word}`}</span>
                      <span aria-hidden>
                        {isDone ? <Check className="h-3.5 w-3.5" strokeWidth={3} />
                          : isLostHere ? <X className="h-3.5 w-3.5" strokeWidth={3} />
                            : i + 1}
                      </span>
                    </span>
                    <span className={cn(
                      'hidden text-center text-[10px] font-medium leading-tight sm:block',
                      isDone ? 'text-emerald-700' : isCurrent ? 'text-indigo-700' : isLostHere ? 'text-rose-700' : 'text-slate-400',
                    )}>
                      {title}
                    </span>
                  </div>
                  {i < PHASE_TITLES.length - 1 && (
                    <span aria-hidden className="mt-[11px] h-[3px] flex-1 overflow-hidden rounded-full bg-slate-200">
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
        </div>
      </div>

      {lost && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
          <XCircle className="h-4 w-4 shrink-0" /> This lead dropped out: <strong>{lost}</strong>. You can still update any phase to reopen it.
        </div>
      )}

      <ol className="space-y-3">
        {/* Phase 1 — Outreach */}
        {shell(0, (
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
        {shell(1, (
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
                onPick={(v) => patch({ stillInterested: v === 'yes' }, { subject: `Still interested: ${v === 'yes' ? 'Yes' : 'No'}` })}
              />
            </div>
          </div>
        ))}

        {/* Phase 3 — Contract */}
        {shell(2, (
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
                onPick={(v) => patch({ contractStatus: v }, { subject: `Contract: ${CONTRACT_LABELS[v]}` })}
              />
            </div>
            <DateField label="Contract sent date" value={value.contractSentDate} disabled={disabled}
              onChange={(v) => patch({ contractSentDate: v }, { subject: 'Contract sent', body: v })} />
          </>
        ))}

        {/* Phase 4 — Signature */}
        {shell(3, (
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
        {shell(4, (
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

      {/* subtle legend for empty state */}
      {phase === 1 && !done[0] && !lost && (
        <p className="mt-3 flex items-center gap-1.5 text-xs text-slate-400">
          <Circle className="h-3 w-3" /> Start by logging your first outreach above.
        </p>
      )}
    </section>
  );
}

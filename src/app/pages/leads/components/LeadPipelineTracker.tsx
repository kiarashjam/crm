import { useMemo } from 'react';
import {
  Phone, Users, FileSignature, PenLine, Wallet,
  Check, Circle, XCircle, ArrowRightCircle, Sparkles,
  type LucideIcon,
} from 'lucide-react';
import { cn } from '@/app/components/ui/utils';
import { Button } from '@/app/components/ui/button';
import {
  type LeadPipeline, type OutreachStatus, type ContactOutcome,
  type ContractStatus, type ContractSigned,
  PHASE_TITLES, OUTREACH_LABELS, OUTCOME_LABELS, CONTRACT_LABELS, SIGNED_LABELS,
  phaseCompletion, currentPhase, lostReason, isPipelineComplete,
} from '../leadPipeline';

const PHASE_ICONS: LucideIcon[] = [Phone, Users, FileSignature, PenLine, Wallet];

type LogHint = { subject: string; body?: string };

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
            onClick={() => onPick(o.value)}
            className={cn(
              'rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors disabled:opacity-50',
              active && o.tone === 'danger' && 'border-rose-300 bg-rose-50 text-rose-700',
              active && o.tone === 'success' && 'border-emerald-300 bg-emerald-50 text-emerald-700',
              active && (!o.tone || o.tone === 'default') && 'border-indigo-300 bg-indigo-50 text-indigo-700',
              !active && 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50',
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
  n, title, done, active, lost, children,
}: {
  n: number; title: string; done?: boolean; active: boolean; lost: boolean; children: React.ReactNode;
}) {
  const Icon = PHASE_ICONS[n - 1] ?? Phone;
  return (
    <div
      className={cn(
        'rounded-2xl border bg-white p-4 transition-shadow',
        active && !lost && 'border-indigo-200 ring-1 ring-indigo-100 shadow-sm',
        done && 'border-emerald-200',
        !active && !done && 'border-slate-200',
      )}
    >
      <div className="mb-3 flex items-center gap-3">
        <span
          className={cn(
            'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl',
            done ? 'bg-emerald-500 text-white' : active && !lost ? 'bg-indigo-600 text-white' : 'bg-slate-100 text-slate-400',
          )}
        >
          {done ? <Check className="h-5 w-5" /> : <Icon className="h-5 w-5" />}
        </span>
        <div className="min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-400">Phase {n}</span>
            {done && <span className="rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-bold text-emerald-600">Complete</span>}
            {active && !done && !lost && <span className="rounded-full bg-indigo-50 px-2 py-0.5 text-[10px] font-bold text-indigo-600">In progress</span>}
          </div>
          <h3 className="text-sm font-semibold text-slate-800">{title}</h3>
        </div>
      </div>
      <div className="space-y-4 pl-12">{children}</div>
    </div>
  );
}

export function LeadPipelineTracker({ value, disabled = false, onChange, previewStatus, onConvert }: Props) {
  const done = useMemo(() => phaseCompletion(value), [value]);
  const phase = useMemo(() => currentPhase(value), [value]);
  const lost = useMemo(() => lostReason(value), [value]);
  const complete = useMemo(() => isPipelineComplete(value), [value]);

  const patch = (partial: Partial<LeadPipeline>, log?: LogHint) => onChange({ ...value, ...partial }, log);
  /** Status this choice would produce, for the inline consequence label. */
  const hint = (partial: Partial<LeadPipeline>) => previewStatus?.(partial) ?? null;

  const contacted = value.outreachStatus === 'contacted';
  const wantsMeeting = contacted && (value.contactOutcome === 'meeting_scheduled' || value.contactOutcome === 'follow_up');

  return (
    <section className="rounded-3xl border border-slate-200 bg-slate-50/60 p-5">
      {/* Header + step rail */}
      <div className="mb-5 flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="flex items-center gap-2 text-base font-bold text-slate-900">
            <Sparkles className="h-4 w-4 text-indigo-600" /> Lead lifecycle
          </h2>
          <p className="mt-0.5 text-xs text-slate-500">
            {lost
              ? `Stopped — ${lost}`
              : complete
                ? 'All phases complete — ready to become a deal'
                : `Currently in Phase ${phase}: ${PHASE_TITLES[phase - 1]}`}
          </p>
        </div>
        <ol className="flex items-center gap-1">
          {PHASE_TITLES.map((t, i) => {
            const isDone = done[i];
            const isCurrent = i + 1 === phase && !isDone && !lost;
            return (
              <li key={t} className="flex items-center gap-1">
                <span
                  className={cn(
                    'flex h-6 w-6 items-center justify-center rounded-full text-[11px] font-bold',
                    isDone ? 'bg-emerald-500 text-white' : isCurrent ? 'bg-indigo-600 text-white' : 'bg-slate-200 text-slate-500',
                  )}
                  title={t}
                >
                  {isDone ? <Check className="h-3.5 w-3.5" /> : i + 1}
                </span>
                {i < PHASE_TITLES.length - 1 && <span className={cn('h-0.5 w-4 rounded', done[i] ? 'bg-emerald-400' : 'bg-slate-200')} />}
              </li>
            );
          })}
        </ol>
      </div>

      {lost && (
        <div className="mb-4 flex items-center gap-2 rounded-xl border border-rose-200 bg-rose-50 px-3 py-2 text-sm text-rose-700">
          <XCircle className="h-4 w-4 shrink-0" /> This lead dropped out: <strong>{lost}</strong>. You can still update any phase to reopen it.
        </div>
      )}

      <div className="space-y-3">
        {/* Phase 1 — Outreach */}
        <PhaseShell n={1} title="Outreach" done={done[0]} active={phase === 1} lost={!!lost}>
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
        </PhaseShell>

        {/* Phase 2 — After the meeting */}
        <PhaseShell n={2} title="After the meeting" done={done[1]} active={phase === 2} lost={!!lost}>
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
        </PhaseShell>

        {/* Phase 3 — Contract */}
        <PhaseShell n={3} title="Contract" done={done[2]} active={phase === 3} lost={!!lost}>
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
        </PhaseShell>

        {/* Phase 4 — Signature */}
        <PhaseShell n={4} title="Contract signed" done={done[3]} active={phase === 4} lost={!!lost}>
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
        </PhaseShell>

        {/* Phase 5 — Deposit */}
        <PhaseShell n={5} title="Deposit" done={done[4]} active={phase === 5} lost={!!lost}>
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
        </PhaseShell>
      </div>

      {/* Completion → become a deal */}
      {complete && onConvert && (
        <div className="mt-4 flex flex-col items-start gap-3 rounded-2xl border border-emerald-200 bg-emerald-50 p-4 sm:flex-row sm:items-center sm:justify-between">
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
        </div>
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

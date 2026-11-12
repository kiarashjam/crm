import { ArrowDown, ArrowUp, Clock, GitBranch, Plus, Trash2, Zap } from 'lucide-react';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Button } from '@/app/components/ui/button';
import {
  STEP_LABELS,
  type ActionType, type AutomationStep, type AutomationTriggerDef, type StepType,
} from '@/app/api/automations';
import { ConditionsEditor, SELECT_CLASS, newCondition } from './ConditionsEditor';
import type { OptionLists } from './options';

/** How deep branches may go inside each other, as the server allows: a branch in a branch in a branch. */
export const MAX_BRANCH_DEPTH = 3;
/** How many steps a rule may have in all, branches' sides counted. */
export const MAX_STEPS = 30;

const UNITS = [
  { value: 'minutes', label: 'minutes' },
  { value: 'hours', label: 'hours' },
  { value: 'days', label: 'days' },
  { value: 'weeks', label: 'weeks' },
];

export interface StepContext {
  trigger: AutomationTriggerDef | undefined;
  ops: Record<string, string[]>;
  lists: OptionLists;
  /** Every step the rule has, branches' sides counted, for the limit. */
  total: number;
}

/** The records whose emails go to a club member, who may have agreed to marketing or not. */
const MEMBER_RECORDS = new Set(['member', 'subscription', 'reservation', 'registration', 'application', 'payment']);

/** A new step of a type, with what it needs to say filled in where there is a sensible default. */
export function newStep(type: StepType, ctx: StepContext): AutomationStep {
  switch (type) {
    case 'wait':
      return { type, config: { amount: '1', unit: 'days' } };
    case 'branch':
      return {
        type,
        config: {},
        condition: { match: 'all', conditions: [newCondition(ctx.trigger?.fields[0], ctx.ops)] },
        then: [],
        else: [],
      };
    case 'create_task':
      return { type, config: { title: '', dueInDays: '1' } };
    default:
      return { type, config: {} };
  }
}

/** Every step in the list, branches' sides counted. */
export function countSteps(steps: AutomationStep[]): number {
  return steps.reduce((n, s) => n + 1 + countSteps(s.then ?? []) + countSteps(s.else ?? []), 0);
}

/** Every step in the list and inside its branches. */
export function everyStep(steps: AutomationStep[]): AutomationStep[] {
  return steps.flatMap((s) => [s, ...everyStep(s.then ?? []), ...everyStep(s.else ?? [])]);
}

function StepBody({
  step, ctx, label, level, onChange,
}: {
  step: AutomationStep;
  ctx: StepContext;
  label: string;
  level: number;
  onChange: (s: AutomationStep) => void;
}) {
  const set = (key: string, value: string) => onChange({ ...step, config: { ...step.config, [key]: value } });
  const setMany = (values: Record<string, string>) => onChange({ ...step, config: { ...step.config, ...values } });
  const people = ctx.lists.user ?? [];
  const sequences = ctx.lists.sequence ?? [];
  const toMember = MEMBER_RECORDS.has(ctx.trigger?.record ?? '');

  switch (step.type) {
    case 'create_task':
      return (
        <div className="space-y-2">
          <Input aria-label={`${label}: task title`} value={step.config.title ?? ''} onChange={(e) => set('title', e.target.value)} placeholder="Task title (Follow up when left empty)" className="h-8 bg-white" />
          <div className="flex flex-wrap items-center gap-2 text-sm text-slate-500">
            Due in
            <Input aria-label={`${label}: due in days`} type="number" min={0} max={365} value={step.config.dueInDays ?? ''} onChange={(e) => set('dueInDays', e.target.value)} className="h-8 w-20 bg-white" />
            days, for
            <select aria-label={`${label}: for whom`} className={SELECT_CLASS} value={step.config.assigneeId ?? ''} onChange={(e) => set('assigneeId', e.target.value)}>
              <option value="">the record&apos;s owner, else whoever set it off</option>
              {people.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
            </select>
            <select aria-label={`${label}: priority`} className={SELECT_CLASS} value={step.config.priority ?? ''} onChange={(e) => set('priority', e.target.value)}>
              <option value="">No priority</option>
              <option value="low">Low</option>
              <option value="medium">Medium</option>
              <option value="high">High</option>
            </select>
          </div>
        </div>
      );
    case 'send_email':
      return (
        <div className="space-y-2">
          <Input aria-label={`${label}: subject`} value={step.config.subject ?? ''} onChange={(e) => set('subject', e.target.value)} placeholder="Subject" className="h-8 bg-white" />
          <Textarea aria-label={`${label}: body`} value={step.config.body ?? ''} onChange={(e) => set('body', e.target.value)} placeholder="Hi {{firstName}}, …" rows={3} className="resize-none bg-white" />
          {toMember && (
            <select aria-label={`${label}: kind of email`} className={SELECT_CLASS} value={step.config.purpose ?? 'marketing'} onChange={(e) => set('purpose', e.target.value)}>
              <option value="marketing">Marketing: only to members who agreed to it</option>
              <option value="service">About their membership: not marketing</option>
            </select>
          )}
          <p className="text-xs text-slate-500">
            Goes through the outbox, like every email Cadence sends: someone who unsubscribed or bounced is not emailed, and the run log says so.
          </p>
        </div>
      );
    case 'notify':
      return (
        <div className="space-y-2">
          <Input aria-label={`${label}: message`} value={step.config.message ?? ''} onChange={(e) => set('message', e.target.value)} placeholder="Notification message" className="h-8 bg-white" />
          <div className="flex flex-wrap items-center gap-2 text-sm text-slate-500">
            To
            <select aria-label={`${label}: to whom`} className={SELECT_CLASS} value={step.config.userId ?? ''} onChange={(e) => set('userId', e.target.value)}>
              <option value="">the record&apos;s owner, else whoever set it off</option>
              {people.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
            </select>
          </div>
        </div>
      );
    case 'assign':
      return (
        <select
          aria-label={`${label}: assign to`}
          className={SELECT_CLASS}
          value={step.config.userId ?? ''}
          onChange={(e) => setMany({ userId: e.target.value, assigneeName: people.find((p) => p.value === e.target.value)?.label ?? '' })}
        >
          <option value="">Assign to…</option>
          {people.map((p) => <option key={p.value} value={p.value}>{p.label}</option>)}
        </select>
      );
    case 'add_to_sequence':
      return (
        <select
          aria-label={`${label}: sequence`}
          className={SELECT_CLASS}
          value={step.config.sequenceId ?? ''}
          onChange={(e) => setMany({ sequenceId: e.target.value, sequence: sequences.find((q) => q.value === e.target.value)?.label ?? '' })}
        >
          <option value="">{sequences.length ? 'Pick a sequence…' : 'No sequences yet'}</option>
          {sequences.map((q) => <option key={q.value} value={q.value}>{q.label}</option>)}
        </select>
      );
    case 'wait':
      return <WaitBody step={step} ctx={ctx} label={label} onChange={onChange} />;
    case 'branch':
      return (
        <div className="space-y-3">
          <div>
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">If</p>
            <ConditionsEditor
              value={step.condition ?? { match: 'all', conditions: [] }}
              onChange={(condition) => onChange({ ...step, condition })}
              trigger={ctx.trigger}
              ops={ctx.ops}
              lists={ctx.lists}
              label={`${label} condition`}
            />
          </div>
          <div className="rounded-lg border-l-2 border-emerald-300 pl-3">
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-emerald-700">Then</p>
            <StepsEditor steps={step.then ?? []} onChange={(then) => onChange({ ...step, then })} ctx={ctx} level={level + 1} label={`${label} then step`} side={`${label}, then`} />
          </div>
          <div className="rounded-lg border-l-2 border-slate-300 pl-3">
            <p className="mb-1 text-xs font-semibold uppercase tracking-wide text-slate-500">Otherwise</p>
            <StepsEditor steps={step.else ?? []} onChange={(otherwise) => onChange({ ...step, else: otherwise })} ctx={ctx} level={level + 1} label={`${label} otherwise step`} side={`${label}, otherwise`} />
          </div>
        </div>
      );
    default:
      return null;
  }
}

/**
 * A wait for a time ("2 days") or until a date on the record ("1 day before the booking's
 * start"). The server reads the date when the wait is reached and again when it looks,
 * so a booking moved to another day moves the wait with it.
 */
function WaitBody({ step, ctx, label, onChange }: { step: AutomationStep; ctx: StepContext; label: string; onChange: (s: AutomationStep) => void }) {
  const dates = ctx.trigger?.dates ?? [];
  const until = step.config.mode === 'until';
  const offset = Number.parseInt(step.config.offsetAmount ?? '', 10);
  const side = !Number.isFinite(offset) || offset === 0 ? 'on' : offset < 0 ? 'before' : 'after';
  const magnitude = Number.isFinite(offset) && offset !== 0 ? String(Math.abs(offset)) : '';
  const setOffset = (amount: string, direction: string) => {
    const n = Number.parseInt(amount, 10);
    const signed = direction === 'on' || !Number.isFinite(n) || n === 0 ? '' : String(direction === 'before' ? -Math.abs(n) : Math.abs(n));
    onChange({ ...step, config: { ...step.config, offsetAmount: signed, offsetUnit: signed ? (step.config.offsetUnit || 'days') : '' } });
  };

  return (
    <div className="flex flex-wrap items-center gap-2 text-sm text-slate-500">
      {dates.length > 0 && (
        <select
          aria-label={`${label}: wait for`}
          className={SELECT_CLASS}
          value={until ? 'until' : 'for'}
          onChange={(e) => onChange({
            ...step,
            config: e.target.value === 'until'
              ? { mode: 'until', date: dates[0]?.key ?? '' }
              : { amount: '1', unit: 'days' },
          })}
        >
          <option value="for">For</option>
          <option value="until">Until</option>
        </select>
      )}
      {!until && (
        <>
          {dates.length === 0 && 'For'}
          <Input aria-label={`${label}: how long`} type="number" min={1} value={step.config.amount ?? ''} onChange={(e) => onChange({ ...step, config: { ...step.config, amount: e.target.value } })} className="h-8 w-20 bg-white" />
          <select aria-label={`${label}: unit`} className={SELECT_CLASS} value={step.config.unit ?? 'days'} onChange={(e) => onChange({ ...step, config: { ...step.config, unit: e.target.value } })}>
            {UNITS.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
          </select>
        </>
      )}
      {until && (
        <>
          {side !== 'on' && (
            <>
              <Input aria-label={`${label}: how far from the date`} type="number" min={1} max={365} value={magnitude} onChange={(e) => setOffset(e.target.value, side)} className="h-8 w-20 bg-white" />
              <select aria-label={`${label}: offset unit`} className={SELECT_CLASS} value={step.config.offsetUnit || 'days'} onChange={(e) => onChange({ ...step, config: { ...step.config, offsetUnit: e.target.value } })}>
                {UNITS.map((u) => <option key={u.value} value={u.value}>{u.label}</option>)}
              </select>
            </>
          )}
          <select aria-label={`${label}: when`} className={SELECT_CLASS} value={side} onChange={(e) => setOffset(magnitude || '1', e.target.value)}>
            <option value="on">the time of</option>
            <option value="before">before</option>
            <option value="after">after</option>
          </select>
          <select aria-label={`${label}: date`} className={SELECT_CLASS} value={step.config.date ?? ''} onChange={(e) => onChange({ ...step, config: { ...step.config, date: e.target.value } })}>
            {!dates.some((d) => d.key === step.config.date) && <option value={step.config.date ?? ''}>Pick a date…</option>}
            {dates.map((d) => <option key={d.key} value={d.key}>{d.label.charAt(0).toLowerCase() + d.label.slice(1)}</option>)}
          </select>
        </>
      )}
    </div>
  );
}

const STEP_ICON: Partial<Record<StepType, React.ElementType>> = { wait: Clock, branch: GitBranch };

/**
 * The steps a rule takes, in order: actions, waits, and if/else branches with steps of
 * their own on each side. The step types offered are the trigger's: a booking can start
 * a task or an email, not a lead's sequence.
 */
export function StepsEditor({
  steps, onChange, ctx, level = 0, label = 'Step', side,
}: {
  steps: AutomationStep[];
  onChange: (steps: AutomationStep[]) => void;
  ctx: StepContext;
  /** How many branches this list is inside. */
  level?: number;
  label?: string;
  /** For a branch's side, which one, so its add buttons say where they add: "Step 3, then". */
  side?: string;
}) {
  const actions: ActionType[] = ctx.trigger?.actions ?? ['create_task', 'send_email', 'notify'];
  const offered: StepType[] = [...actions, 'wait', ...(level < MAX_BRANCH_DEPTH ? ['branch' as const] : [])];
  const full = ctx.total >= MAX_STEPS;
  const setStep = (i: number, s: AutomationStep) => onChange(steps.map((x, idx) => (idx === i ? s : x)));
  const move = (i: number, by: number) => {
    const j = i + by;
    if (j < 0 || j >= steps.length) return;
    const next = [...steps];
    const [moved] = next.splice(i, 1);
    if (moved) next.splice(j, 0, moved);
    onChange(next);
  };

  return (
    <div className="space-y-2">
      {steps.length === 0 && level > 0 && <p className="text-xs text-slate-400">Nothing on this side.</p>}
      {steps.map((step, i) => {
        const stepLabel = `${label} ${i + 1}`;
        const Icon = STEP_ICON[step.type] ?? Zap;
        const allowed = offered.includes(step.type);
        return (
          <div key={step.id ?? `new-${i}`} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3 dark:border-slate-700 dark:bg-slate-900/40">
            <div className="flex flex-wrap items-center gap-2">
              <Icon className="h-4 w-4 text-slate-400" aria-hidden />
              <select
                aria-label={stepLabel}
                className={SELECT_CLASS}
                value={step.type}
                onChange={(e) => setStep(i, { ...newStep(e.target.value as StepType, ctx), id: step.id })}
              >
                {offered.map((t) => <option key={t} value={t}>{STEP_LABELS[t]}</option>)}
                {!allowed && <option value={step.type}>{STEP_LABELS[step.type] ?? step.type} (not for this trigger)</option>}
              </select>
              <div className="ml-auto flex items-center gap-1">
                <button type="button" onClick={() => move(i, -1)} disabled={i === 0} aria-label={`Move ${stepLabel.toLowerCase()} up`} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-30">
                  <ArrowUp className="h-3.5 w-3.5" />
                </button>
                <button type="button" onClick={() => move(i, 1)} disabled={i === steps.length - 1} aria-label={`Move ${stepLabel.toLowerCase()} down`} className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700 disabled:opacity-30">
                  <ArrowDown className="h-3.5 w-3.5" />
                </button>
                <button type="button" onClick={() => onChange(steps.filter((_, idx) => idx !== i))} aria-label={`Remove ${stepLabel.toLowerCase()}`} className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600">
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              </div>
            </div>
            {!allowed && (
              <p className="mt-1 text-xs text-amber-700">This trigger cannot run this step. Change it or remove it before saving.</p>
            )}
            <div className="mt-2">
              <StepBody step={step} ctx={ctx} label={stepLabel} level={level} onChange={(s) => setStep(i, s)} />
            </div>
          </div>
        );
      })}
      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" size="sm" className="h-7 gap-1 text-xs" disabled={full}
          aria-label={side ? `Add an action to ${side}` : undefined}
          onClick={() => onChange([...steps, newStep(actions[0] ?? 'create_task', ctx)])}>
          <Plus className="h-3.5 w-3.5" /> {level > 0 ? 'Action' : 'Add an action'}
        </Button>
        <Button type="button" variant="outline" size="sm" className="h-7 gap-1 text-xs" disabled={full}
          aria-label={side ? `Add a wait to ${side}` : undefined}
          onClick={() => onChange([...steps, newStep('wait', ctx)])}>
          <Clock className="h-3.5 w-3.5" /> {level > 0 ? 'Wait' : 'Add a wait'}
        </Button>
        {level < MAX_BRANCH_DEPTH && (
          <Button type="button" variant="outline" size="sm" className="h-7 gap-1 text-xs" disabled={full || (ctx.trigger?.fields.length ?? 0) === 0}
            aria-label={side ? `Add an if / else to ${side}` : undefined}
            onClick={() => onChange([...steps, newStep('branch', ctx)])}>
            <GitBranch className="h-3.5 w-3.5" /> {level > 0 ? 'If / else' : 'Add an if / else'}
          </Button>
        )}
      </div>
      {full && level === 0 && <p className="text-xs text-slate-500">A rule can have at most {MAX_STEPS} steps.</p>}
    </div>
  );
}

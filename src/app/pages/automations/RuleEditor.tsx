import { useMemo, useState } from 'react';
import { Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import {
  Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import {
  STEP_LABELS, createAutomation, updateAutomation,
  type AutomationCatalog, type AutomationInput, type AutomationRule, type AutomationStep,
  type AutomationTriggerDef, type ConditionGroup, type SendWindow, type TriggerType,
} from '@/app/api/automations';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import { ConditionsEditor, SELECT_CLASS, cleanGroup, conditionComplete, groupEmpty } from './ConditionsEditor';
import { StepsEditor, countSteps, everyStep, newStep, type StepContext } from './StepsEditor';
import { SendWindowEditor, windowProblem } from './SendWindowEditor';
import { optionsNeeded, useConditionOptions } from './options';

/** How many days ahead a renewal rule fires, as the server allows. */
const DEFAULT_DAYS_BEFORE = '14';
const MAX_DAYS_BEFORE = 90;

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

function conditionsOf(group: ConditionGroup | null | undefined): ConditionGroup[] {
  if (!group) return [];
  return [group, ...(group.groups ?? [])];
}

/** The conditions that still mean something on another trigger: its fields, of the same kind. */
function keepFitting(group: ConditionGroup, from: AutomationTriggerDef | undefined, to: AutomationTriggerDef | undefined): ConditionGroup {
  const fits = (field: string) => {
    const before = from?.fields.find((f) => f.key === field);
    const after = to?.fields.find((f) => f.key === field);
    return !!after && (!before || before.type === after.type);
  };
  return {
    match: group.match,
    conditions: group.conditions.filter((c) => fits(c.field)),
    groups: (group.groups ?? []).map((g) => keepFitting(g, from, to)).filter((g) => !groupEmpty(g)),
  };
}

/** Steps as they are saved: empty settings left out, branches' conditions and sides the same way. */
function cleanSteps(steps: AutomationStep[]): AutomationStep[] {
  return steps.map((s) => {
    const config = Object.fromEntries(Object.entries(s.config).filter(([, v]) => v != null && String(v).trim() !== ''));
    if (s.type !== 'branch') return { id: s.id, type: s.type, config };
    return { id: s.id, type: s.type, config, condition: cleanGroup(s.condition), then: cleanSteps(s.then ?? []), else: cleanSteps(s.else ?? []) };
  });
}

/** What would stop the rule from saving, as a person reads it; null when nothing does. */
export function ruleProblem(input: {
  name: string;
  trigger: AutomationTriggerDef | undefined;
  daysBefore: string;
  conditions: ConditionGroup;
  steps: AutomationStep[];
  sendWindow: SendWindow | null;
}): string | null {
  const { trigger, steps } = input;
  if (!input.name.trim()) return 'Give the rule a name.';
  if (!trigger) return 'Pick what sets the rule off.';
  if (trigger.takesDaysBefore) {
    const days = Number(input.daysBefore);
    if (!Number.isInteger(days) || days < 1 || days > MAX_DAYS_BEFORE) return `A renewal rule fires between 1 and ${MAX_DAYS_BEFORE} days ahead.`;
  }
  if (conditionsOf(input.conditions).some((g) => g.conditions.some((c) => !conditionComplete(c)))) {
    return 'Finish each condition, or remove it.';
  }
  if (steps.length === 0) return 'Add at least one step.';
  for (const step of everyStep(steps)) {
    const label = STEP_LABELS[step.type] ?? step.type;
    if (step.type === 'branch') {
      if (groupEmpty(step.condition)) return 'An if / else needs a condition to decide on.';
      if (conditionsOf(step.condition).some((g) => g.conditions.some((c) => !conditionComplete(c)))) return "Finish each of an if / else's conditions, or remove it.";
      if ((step.then ?? []).length === 0 && (step.else ?? []).length === 0) return 'An if / else needs at least one step on one of its sides.';
      continue;
    }
    if (step.type === 'wait') {
      if (step.config.mode === 'until') {
        if (!trigger.dates.some((d) => d.key === step.config.date)) return 'Pick the date a wait waits for.';
      } else {
        const amount = Number(step.config.amount);
        if (!Number.isInteger(amount) || amount < 1) return 'Say how long to wait, as a whole number.';
      }
      continue;
    }
    if (!trigger.actions.includes(step.type)) return `"${trigger.label}" cannot run "${label}". Change that step or remove it.`;
    if (step.type === 'assign' && !step.config.userId) return 'Pick who to assign to.';
    if (step.type === 'add_to_sequence' && !step.config.sequenceId) return 'Pick a sequence.';
    if (step.type === 'send_email' && (!step.config.subject?.trim() || !step.config.body?.trim())) return 'An email needs a subject and a body.';
  }
  return windowProblem(input.sendWindow);
}

/**
 * A rule: when something happens, and only if its record meets the conditions, take the
 * steps. The trigger decides what the conditions can test, what a wait can wait for and
 * which actions are offered.
 */
export function RuleEditor({
  open, onOpenChange, rule, catalog, timeZone, modulesOn, onSaved, onConflict,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  rule: AutomationRule | null;
  catalog: AutomationCatalog;
  timeZone?: string;
  /** Which halves of Cadence the workspace has on: triggers of one turned off are not offered for a new rule. */
  modulesOn: { sales: boolean; club: boolean };
  onSaved: (rule: AutomationRule) => void;
  /** Someone saved the rule after it was opened: the latest copy should be read. */
  onConflict: () => void;
}) {
  const [name, setName] = useState(rule?.name ?? '');
  const [trigger, setTrigger] = useState<TriggerType>(rule?.trigger ?? 'lead.created');
  const [enabled, setEnabled] = useState(rule?.enabled ?? true);
  const [daysBefore, setDaysBefore] = useState(rule?.triggerConfig?.daysBefore ?? DEFAULT_DAYS_BEFORE);
  const [conditions, setConditions] = useState<ConditionGroup>(rule?.conditions ?? { match: 'all', conditions: [] });
  const [sendWindow, setSendWindow] = useState<SendWindow | null>(rule?.sendWindow ?? null);
  const def = catalog.triggers.find((t) => t.key === trigger);
  const [steps, setSteps] = useState<AutomationStep[]>(() => rule?.actions ?? [newStep('send_email', { trigger: def, ops: catalog.ops, lists: {}, total: 0 })]);
  const [saving, setSaving] = useState(false);
  const [dropped, setDropped] = useState(false);

  const needed = useMemo(() => optionsNeeded(def), [def]);
  const lists = useConditionOptions(needed);
  const ctx: StepContext = { trigger: def, ops: catalog.ops, lists, total: countSteps(steps) };

  // Triggers by group, those of a module turned off left out, except the one the rule has.
  const groups = useMemo(() => {
    const byGroup = new Map<string, AutomationTriggerDef[]>();
    for (const t of catalog.triggers) {
      const off = (t.module === 'sales' && !modulesOn.sales) || (t.module === 'club' && !modulesOn.club);
      if (off && t.key !== rule?.trigger) continue;
      byGroup.set(t.group, [...(byGroup.get(t.group) ?? []), t]);
    }
    return [...byGroup.entries()];
  }, [catalog.triggers, modulesOn.sales, modulesOn.club, rule?.trigger]);

  const changeTrigger = (next: TriggerType) => {
    const to = catalog.triggers.find((t) => t.key === next);
    const kept = keepFitting(conditions, def, to);
    const count = (g: ConditionGroup): number => g.conditions.length + (g.groups ?? []).reduce((n, x) => n + count(x), 0);
    setDropped(count(kept) < count(conditions));
    setConditions(kept);
    setTrigger(next);
  };

  const hasEmail = everyStep(steps).some((s) => s.type === 'send_email');

  const save = async () => {
    const problem = ruleProblem({ name, trigger: def, daysBefore, conditions, steps, sendWindow });
    if (problem) { toast.error(problem); return; }
    const payload: AutomationInput = {
      name: name.trim(),
      enabled,
      trigger,
      // Null takes them off; the server keeps what is left out as it was.
      conditions: cleanGroup(conditions),
      sendWindow,
      actions: cleanSteps(steps),
      ...(def?.takesDaysBefore ? { triggerConfig: { daysBefore: daysBefore.trim() } } : {}),
      ...(rule?.version ? { version: rule.version } : {}),
    };
    setSaving(true);
    try {
      const saved = rule ? await updateAutomation(rule.id, payload) : await createAutomation(payload);
      if (!saved) { toast.error('The automation could not be saved.'); return; }
      toast.success(rule ? 'Automation updated' : 'Automation created');
      onSaved(saved);
    } catch (err) {
      if (isEditConflict(err)) {
        showEditConflict(err.message, onConflict);
        return;
      }
      // The server says why: a manager-only change, an action pointing outside the workspace…
      toast.error(reason(err, 'The automation could not be saved.'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-[760px]">
        <DialogHeader>
          <DialogTitle>{rule ? 'Edit automation' : 'New automation'}</DialogTitle>
          <DialogDescription>When something happens, and only for the records you say, take these steps.</DialogDescription>
        </DialogHeader>

        <div className="space-y-5">
          <div>
            <Label htmlFor="auto-name">Name</Label>
            <Input id="auto-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Welcome new leads" className="mt-1" />
          </div>

          <section aria-labelledby="auto-when">
            <h3 id="auto-when" className="text-sm font-semibold text-slate-800 dark:text-slate-100">When</h3>
            <select id="auto-trigger" aria-label="Trigger" className={`${SELECT_CLASS} mt-1 h-9 w-full`} value={trigger} onChange={(e) => changeTrigger(e.target.value as TriggerType)}>
              {groups.map(([group, triggers]) => (
                <optgroup key={group} label={group}>
                  {triggers.map((t) => <option key={t.key} value={t.key}>{t.label}</option>)}
                </optgroup>
              ))}
            </select>
            {def?.description && <p className="mt-1 text-xs text-slate-500">{def.description}</p>}
            {def?.takesDaysBefore && (
              <div className="mt-2 flex items-center gap-2 text-sm text-slate-600">
                <Input aria-label="Days before the renewal" type="number" min={1} max={MAX_DAYS_BEFORE} value={daysBefore} onChange={(e) => setDaysBefore(e.target.value)} className="h-8 w-20" />
                days before the subscription renews, once for each renewal.
              </div>
            )}
          </section>

          <section aria-labelledby="auto-if">
            <h3 id="auto-if" className="text-sm font-semibold text-slate-800 dark:text-slate-100">Only if</h3>
            {(def?.fields.length ?? 0) > 0 && (
              <p className="mb-2 text-xs text-slate-500">
                {groupEmpty(conditions) ? 'No conditions: the rule runs for every record.' : 'The record is checked as it is when the rule is set off.'}
              </p>
            )}
            {dropped && <p className="mb-2 text-xs text-amber-700">Conditions this trigger cannot test were taken off.</p>}
            <ConditionsEditor value={conditions} onChange={setConditions} trigger={def} ops={catalog.ops} lists={lists} />
          </section>

          <section aria-labelledby="auto-then">
            <h3 id="auto-then" className="text-sm font-semibold text-slate-800 dark:text-slate-100">Then</h3>
            <p className="mb-2 text-xs text-slate-500">Steps run in order. A wait holds the steps after it; an if / else checks the record when it is reached.</p>
            <StepsEditor steps={steps} onChange={setSteps} ctx={ctx} />
          </section>

          {(hasEmail || sendWindow) && (
            <section aria-labelledby="auto-window">
              <h3 id="auto-window" className="text-sm font-semibold text-slate-800 dark:text-slate-100">Send window</h3>
              <SendWindowEditor value={sendWindow} onChange={setSendWindow} timeZone={timeZone} />
            </section>
          )}

          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <Switch checked={enabled} onCheckedChange={setEnabled} aria-label="Enabled" /> Enabled
          </label>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving && <Loader2 className="h-4 w-4 animate-spin" />} {rule ? 'Save changes' : 'Create automation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

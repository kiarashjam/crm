import { Plus, Trash2 } from 'lucide-react';
import { Input } from '@/app/components/ui/input';
import { Button } from '@/app/components/ui/button';
import {
  OP_LABELS,
  type AutomationCondition, type AutomationFieldDef, type AutomationTriggerDef, type ConditionGroup,
} from '@/app/api/automations';
import { choicesOf, type OptionLists } from './options';

export const SELECT_CLASS =
  'h-8 rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900';

const NO_VALUE = new Set(['is_empty', 'is_not_empty', 'is_true', 'is_false']);
const MANY = new Set(['is_any_of', 'is_none_of']);
const NUMERIC = new Set(['number', 'money']);

/** A condition on a field, with a sensible first comparison for its kind. */
export function newCondition(field: AutomationFieldDef | undefined, ops: Record<string, string[]>): AutomationCondition {
  const op = field ? (ops[field.type]?.[0] ?? 'is') : 'is';
  return { field: field?.key ?? '', op };
}

/** Whether a condition says enough to be saved: a value where its comparison needs one. */
export function conditionComplete(c: AutomationCondition): boolean {
  if (!c.field || !c.op) return false;
  if (NO_VALUE.has(c.op)) return true;
  if (MANY.has(c.op)) return (c.values ?? []).some((v) => v.trim());
  if (c.op === 'between') return !!c.value?.trim() && !!c.to?.trim();
  return !!c.value?.trim();
}

/** A group with nothing in it, which every record meets. */
export function groupEmpty(group: ConditionGroup | null | undefined): boolean {
  return !group || (group.conditions.length === 0 && (group.groups ?? []).every(groupEmpty));
}

/** The group as it is saved: empty groups dropped, values trimmed. */
export function cleanGroup(group: ConditionGroup | null | undefined): ConditionGroup | null {
  if (!group || groupEmpty(group)) return null;
  return {
    match: group.match,
    conditions: group.conditions.map((c) => ({
      ...c,
      value: c.value?.trim() || undefined,
      to: c.to?.trim() || undefined,
      values: c.values?.map((v) => v.trim()).filter(Boolean),
    })),
    groups: (group.groups ?? []).map(cleanGroup).filter((g): g is ConditionGroup => g != null),
  };
}

function ConditionRow({
  condition, fields, ops, lists, label, onChange, onRemove,
}: {
  condition: AutomationCondition;
  fields: AutomationFieldDef[];
  ops: Record<string, string[]>;
  lists: OptionLists;
  label: string;
  onChange: (c: AutomationCondition) => void;
  onRemove: () => void;
}) {
  const field = fields.find((f) => f.key === condition.field);
  const fieldOps = field ? (ops[field.type] ?? []) : [];
  const choices = choicesOf(field, lists);
  const op = condition.op;
  const numeric = field ? NUMERIC.has(field.type) : false;

  const valueInput = (() => {
    if (!field || NO_VALUE.has(op)) return null;
    if (MANY.has(op)) {
      if (choices) {
        const chosen = new Set(condition.values ?? []);
        return (
          <div className="flex flex-wrap gap-1.5" role="group" aria-label={`${label}: values`}>
            {choices.map((choice) => (
              <label key={choice.value} className="inline-flex items-center gap-1 rounded-md border border-slate-200 bg-white px-1.5 py-0.5 text-xs">
                <input
                  type="checkbox"
                  checked={chosen.has(choice.value)}
                  onChange={(e) => {
                    const next = new Set(chosen);
                    if (e.target.checked) next.add(choice.value); else next.delete(choice.value);
                    onChange({ ...condition, values: [...next] });
                  }}
                />
                {choice.label}
              </label>
            ))}
          </div>
        );
      }
      return (
        <Input
          aria-label={`${label}: values`}
          className="h-8 w-56 bg-white"
          placeholder="Values, separated by commas"
          value={(condition.values ?? []).join(', ')}
          onChange={(e) => onChange({ ...condition, values: e.target.value.split(',').map((v) => v.trimStart()) })}
        />
      );
    }
    if (op === 'between') {
      return (
        <span className="inline-flex items-center gap-1.5 text-sm text-slate-500">
          <Input aria-label={`${label}: from`} type="number" className="h-8 w-24 bg-white" value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value })} />
          and
          <Input aria-label={`${label}: to`} type="number" className="h-8 w-24 bg-white" value={condition.to ?? ''} onChange={(e) => onChange({ ...condition, to: e.target.value })} />
        </span>
      );
    }
    if (choices && !numeric) {
      return (
        <select aria-label={`${label}: value`} className={SELECT_CLASS} value={condition.value ?? ''} onChange={(e) => onChange({ ...condition, value: e.target.value })}>
          <option value="">Choose…</option>
          {choices.map((choice) => <option key={choice.value} value={choice.value}>{choice.label}</option>)}
          {/* A value saved before that the list no longer has stays readable and selected. */}
          {condition.value && !choices.some((c) => c.value === condition.value) && <option value={condition.value}>{condition.value}</option>}
        </select>
      );
    }
    return (
      <Input
        aria-label={`${label}: value`}
        type={numeric ? 'number' : 'text'}
        className="h-8 w-44 bg-white"
        value={condition.value ?? ''}
        onChange={(e) => onChange({ ...condition, value: e.target.value })}
      />
    );
  })();

  return (
    <div className="flex flex-wrap items-center gap-2">
      <select
        aria-label={`${label}: field`}
        className={SELECT_CLASS}
        value={condition.field}
        onChange={(e) => {
          const next = fields.find((f) => f.key === e.target.value);
          onChange(newCondition(next, ops));
        }}
      >
        {!field && <option value="">Choose a field…</option>}
        {fields.map((f) => <option key={f.key} value={f.key}>{f.label}</option>)}
      </select>
      {field && (
        <select
          aria-label={`${label}: comparison`}
          className={SELECT_CLASS}
          value={op}
          onChange={(e) => onChange({ field: condition.field, op: e.target.value })}
        >
          {fieldOps.map((o) => <option key={o} value={o}>{OP_LABELS[o] ?? o}</option>)}
        </select>
      )}
      {valueInput}
      <button type="button" onClick={onRemove} aria-label={`Remove ${label.toLowerCase()}`} className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600">
        <Trash2 className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}

/**
 * Conditions joined by all or any, with groups one level down: "status is Qualified and
 * (value is over 10'000 or source is Referral)". The fields are the trigger's.
 */
export function ConditionsEditor({
  value, onChange, trigger, ops, lists, nested = false, label = 'Condition',
}: {
  value: ConditionGroup;
  onChange: (group: ConditionGroup) => void;
  trigger: AutomationTriggerDef | undefined;
  ops: Record<string, string[]>;
  lists: OptionLists;
  /** Inside another group: no groups of its own. */
  nested?: boolean;
  label?: string;
}) {
  const fields = trigger?.fields ?? [];
  if (fields.length === 0) {
    return <p className="text-sm text-slate-500">This trigger has nothing to test: the rule runs every time.</p>;
  }
  const setCondition = (i: number, c: AutomationCondition) =>
    onChange({ ...value, conditions: value.conditions.map((x, idx) => (idx === i ? c : x)) });
  const groups = value.groups ?? [];
  const many = value.conditions.length + groups.length > 1;
  return (
    <div className="space-y-2">
      {many && (
        <div className="flex items-center gap-2 text-sm text-slate-600">
          <select aria-label={`${label}s: join`} className={SELECT_CLASS} value={value.match} onChange={(e) => onChange({ ...value, match: e.target.value as 'all' | 'any' })}>
            <option value="all">All</option>
            <option value="any">Any</option>
          </select>
          of these
        </div>
      )}
      {value.conditions.map((c, i) => (
        <ConditionRow
          key={i}
          condition={c}
          fields={fields}
          ops={ops}
          lists={lists}
          label={`${label} ${i + 1}`}
          onChange={(next) => setCondition(i, next)}
          onRemove={() => onChange({ ...value, conditions: value.conditions.filter((_, idx) => idx !== i) })}
        />
      ))}
      {groups.map((group, g) => (
        <div key={g} className="rounded-lg border border-dashed border-slate-300 p-2">
          <ConditionsEditor
            value={group}
            onChange={(next) => onChange({ ...value, groups: groups.map((x, idx) => (idx === g ? next : x)) })}
            trigger={trigger}
            ops={ops}
            lists={lists}
            nested
            label={`Group ${g + 1} condition`}
          />
          <button type="button" className="mt-1 text-xs text-slate-500 hover:text-red-600" onClick={() => onChange({ ...value, groups: groups.filter((_, idx) => idx !== g) })}>
            Remove this group
          </button>
        </div>
      ))}
      <div className="flex flex-wrap gap-2">
        <Button type="button" variant="outline" size="sm" className="h-7 gap-1 text-xs"
          onClick={() => onChange({ ...value, conditions: [...value.conditions, newCondition(fields[0], ops)] })}>
          <Plus className="h-3.5 w-3.5" /> {nested ? 'Add to this group' : 'Add a condition'}
        </Button>
        {!nested && (
          <Button type="button" variant="outline" size="sm" className="h-7 gap-1 text-xs"
            onClick={() => onChange({ ...value, groups: [...groups, { match: 'any', conditions: [newCondition(fields[0], ops)] }] })}>
            <Plus className="h-3.5 w-3.5" /> Add a group
          </Button>
        )}
      </div>
    </div>
  );
}

import { useEffect, useState } from 'react';
import {
  Zap, Plus, Trash2, Pencil, Loader2, ArrowRight, Mail, CheckSquare, Bell, UserPlus, Workflow,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from '@/app/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import {
  getAutomations, createAutomation, updateAutomation, deleteAutomation,
  TRIGGER_LABELS, ACTION_LABELS,
  type AutomationRule, type TriggerType, type ActionType, type AutomationAction,
} from '@/app/api';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { servesDemoData } from '@/app/api/apiClient';
import { getOrgMembers, isOrgAdmin, type OrgMemberDto } from '@/app/api/organizations';
import { getSequences, type Sequence } from '@/app/api/sequences';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

const ACTION_ICON: Record<ActionType, React.ElementType> = {
  create_task: CheckSquare, send_email: Mail, notify: Bell, assign: UserPlus, add_to_sequence: Workflow,
};

type DraftAction = Omit<AutomationAction, 'id'>;

const newAction = (type: ActionType = 'create_task'): DraftAction => ({ type, config: {} });

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export default function Automations() {
  const [rules, setRules] = useState<AutomationRule[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<AutomationRule | null>(null);
  const org = useOrgOptional()?.currentOrg ?? null;
  // A rule acts for the whole workspace, so the server lets the owner or a manager
  // change one. Everyone else sees the rules but isn't offered what would be refused.
  const canChange = servesDemoData() || isOrgAdmin(org);

  const load = () => {
    setLoading(true);
    getAutomations().then(setRules).catch(() => toast.error('Failed to load automations')).finally(() => setLoading(false));
  };
  useEffect(() => { load(); }, []);

  const toggle = async (r: AutomationRule) => {
    setRules((prev) => prev.map((x) => (x.id === r.id ? { ...x, enabled: !x.enabled } : x)));
    const ok = await updateAutomation(r.id, { enabled: !r.enabled }).catch((err: unknown) => {
      toast.error(reason(err, 'Failed to update'));
      return 'shown' as const;
    });
    if (ok === 'shown') load();
    else if (!ok) { toast.error('Failed to update'); load(); }
  };

  const remove = async (r: AutomationRule) => {
    if (!confirm(`Delete automation "${r.name}"?`)) return;
    setRules((prev) => prev.filter((x) => x.id !== r.id));
    try {
      if (await deleteAutomation(r.id)) toast.success('Automation deleted');
      else { toast.error('Failed to delete'); load(); }
    } catch (err) {
      toast.error(reason(err, 'Failed to delete'));
      load();
    }
  };

  // How this person likes rules shown: stacked, as cards or a table, saved for them.
  const view = useViewPreference('automations', { layouts: ['list', 'grid', 'table'] });
  const openRule = canChange ? (r: AutomationRule) => { setEditing(r); setEditorOpen(true); } : undefined;
  const shortDate = (iso?: string) => {
    if (!iso) return '—';
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  };

  /** The rule's trigger, then each action it runs. */
  const ruleFlow = (r: AutomationRule) => (
    <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
      <span className="inline-flex items-center gap-1 rounded-lg bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700">{TRIGGER_LABELS[r.trigger]}</span>
      {r.actions.map((a) => {
        const Icon = ACTION_ICON[a.type];
        return (
          <span key={a.id} className="inline-flex items-center gap-1">
            <ArrowRight className="h-3.5 w-3.5 text-slate-300" />
            <span className="inline-flex items-center gap-1 rounded-lg bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600">
              <Icon className="h-3 w-3" /> {ACTION_LABELS[a.type]}
            </span>
          </span>
        );
      })}
    </div>
  );

  /** On, edit and delete: the same on a card, a row and a table line. */
  const ruleControls = (r: AutomationRule) => canChange && (
    <WriteOnly>
      <div className="flex shrink-0 items-center gap-2">
        <Switch checked={r.enabled} onCheckedChange={() => toggle(r)} aria-label="Enabled" />
        <button type="button" onClick={() => { setEditing(r); setEditorOpen(true); }} aria-label={`Edit ${r.name}`} className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"><Pencil className="h-4 w-4" /></button>
        <button type="button" onClick={() => remove(r)} aria-label={`Delete ${r.name}`} className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
      </div>
    </WriteOnly>
  );

  const ruleColumns: RecordColumn<AutomationRule>[] = [
    { id: 'name', label: 'Name', required: true, cell: (r) => r.name },
    { id: 'trigger', label: 'When', cell: (r) => TRIGGER_LABELS[r.trigger] },
    {
      id: 'actions',
      label: 'Then',
      cell: (r) => (r.actions.length ? r.actions.map((a) => ACTION_LABELS[a.type]).join(' → ') : '—'),
    },
    {
      id: 'status',
      label: 'Status',
      cell: (r) => (
        <span className={cn('inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-xs font-medium', r.enabled ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500')}>
          <span className={cn('h-1.5 w-1.5 rounded-full', r.enabled ? 'bg-emerald-500' : 'bg-slate-400')} />
          {r.enabled ? 'On' : 'Off'}
        </span>
      ),
    },
    { id: 'runs', label: 'Runs', numeric: true, cell: (r) => r.runCount ?? '—' },
    { id: 'created', label: 'Created', byDefault: false, cell: (r) => shortDate(r.createdAtUtc) },
  ];

  const enabledCount = rules.filter((r) => r.enabled).length;
  const totalRuns = rules.reduce((s, r) => s + (r.runCount ?? 0), 0);
  const triggerCount = new Set(rules.map((r) => r.trigger)).size;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Zap}
            tone="amber"
            title="Automations"
            subtitle="Run actions automatically when something happens in your CRM."
            actions={canChange && (
              <WriteOnly>
                <Button onClick={() => { setEditing(null); setEditorOpen(true); }} className={heroPrimary('amber')}>
                  <Plus className="h-4 w-4" /> New automation
                </Button>
              </WriteOnly>
            )}
            stats={[
              { label: 'Automations', value: rules.length, icon: Zap, tone: 'amber', hint: `${triggerCount} ${triggerCount === 1 ? 'trigger' : 'triggers'}` },
              { label: 'Active', value: enabledCount, icon: CheckSquare, tone: 'emerald', hint: `${rules.length - enabledCount} off` },
              { label: 'Total runs', value: totalRuns, icon: ArrowRight, tone: 'indigo', hint: 'all time' },
            ]}
          />

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : rules.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-200 bg-white py-16 text-center">
              <Zap className="mx-auto h-10 w-10 text-slate-300" />
              <p className="mt-3 text-sm font-medium text-slate-700">No automations yet</p>
              <p className="mt-1 text-sm text-slate-500">Create a rule to automate repetitive work.</p>
            </div>
          ) : (
            <>
            <div className="mb-3 flex flex-wrap items-center justify-between gap-2">
              <p className="text-sm text-slate-500">
                {rules.length} {rules.length === 1 ? 'automation' : 'automations'} · {enabledCount} on
              </p>
              <ViewSwitcher view={view} columns={columnChoices(ruleColumns)} />
            </div>
            <ViewTransition layout={view.layout}>
            {view.layout === 'table' ? (
              <RecordTable
                label="Automations"
                items={rules}
                view={view}
                getKey={(r) => r.id}
                onOpen={openRule}
                columns={ruleColumns}
                actions={ruleControls}
              />
            ) : (
            <div className={view.layout === 'grid'
              ? `grid sm:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3')}`
              : view.density === 'compact' ? 'space-y-2' : 'space-y-3'}
            >
              {rules.map((r, index) => (
                <StaggerItem key={r.id} index={index} className={cn('rounded-2xl border border-slate-200 bg-white shadow-sm hover:border-amber-200', view.density === 'compact' ? 'p-3' : 'p-4', view.layout === 'grid' && 'flex flex-col gap-3', liftOnHover)}>
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        {view.layout === 'grid' && (
                          <span className={cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-sm', r.enabled ? 'from-amber-400 to-orange-500' : 'from-slate-300 to-slate-400')}>
                            <Zap className="h-4 w-4" aria-hidden />
                          </span>
                        )}
                        <h3 className="font-semibold text-slate-900">{r.name}</h3>
                        {typeof r.runCount === 'number' && <span className="shrink-0 whitespace-nowrap rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500">{r.runCount} runs</span>}
                      </div>
                      <div className="mt-2">{ruleFlow(r)}</div>
                    </div>
                    {view.layout !== 'grid' && ruleControls(r)}
                  </div>
                  {view.layout === 'grid' && canChange && (
                    <div className="mt-auto flex justify-end border-t border-slate-100 pt-3">{ruleControls(r)}</div>
                  )}
                </StaggerItem>
              ))}
            </div>
            )}
            </ViewTransition>
            </>
          )}
        </main>
      </PageTransition>

      {editorOpen && (
        <AutomationEditor
          open={editorOpen}
          onOpenChange={setEditorOpen}
          rule={editing}
          organizationId={org?.id ?? null}
          onSaved={() => { setEditorOpen(false); load(); }}
        />
      )}
    </div>
  );
}

function AutomationEditor({ open, onOpenChange, rule, organizationId, onSaved }: {
  open: boolean; onOpenChange: (o: boolean) => void; rule: AutomationRule | null; organizationId: string | null; onSaved: () => void;
}) {
  const [name, setName] = useState(rule?.name ?? '');
  const [trigger, setTrigger] = useState<TriggerType>(rule?.trigger ?? 'lead.created');
  const [enabled, setEnabled] = useState(rule?.enabled ?? true);
  const [actions, setActions] = useState<DraftAction[]>(
    rule?.actions.map(({ id: _id, ...rest }) => rest) ?? [newAction('send_email')],
  );
  const [saving, setSaving] = useState(false);
  // Assign and add-to-sequence save ids, which is what the server runs on. They used
  // to save the name typed into a text box, and the actions did nothing.
  const [people, setPeople] = useState<OrgMemberDto[]>([]);
  const [sequences, setSequences] = useState<Sequence[]>([]);
  useEffect(() => {
    if (organizationId || servesDemoData()) {
      getOrgMembers(organizationId ?? '').then(setPeople).catch(() => setPeople([]));
    }
    getSequences().then(setSequences).catch(() => setSequences([]));
  }, [organizationId]);

  const setActionType = (i: number, type: ActionType) => setActions((p) => p.map((a, idx) => (idx === i ? { type, config: {} } : a)));
  const setConfig = (i: number, key: string, value: string) =>
    setActions((p) => p.map((a, idx) => (idx === i ? { ...a, config: { ...a.config, [key]: value } } : a)));
  const setConfigs = (i: number, values: Record<string, string>) =>
    setActions((p) => p.map((a, idx) => (idx === i ? { ...a, config: { ...a.config, ...values } } : a)));

  const save = async () => {
    if (!name.trim()) { toast.error('Name is required'); return; }
    if (actions.length === 0) { toast.error('Add at least one action'); return; }
    if (actions.some((a) => a.type === 'assign' && !a.config.userId)) { toast.error('Pick who to assign to'); return; }
    if (actions.some((a) => a.type === 'add_to_sequence' && !a.config.sequenceId)) { toast.error('Pick a sequence'); return; }
    setSaving(true);
    try {
      const payload = { name: name.trim(), trigger, enabled, actions };
      const res = rule ? await updateAutomation(rule.id, payload) : await createAutomation(payload);
      if (!res) { toast.error('Failed to save'); return; }
      toast.success(rule ? 'Automation updated' : 'Automation created');
      onSaved();
    } catch (err) {
      // The server says why: a manager-only change, or an action pointing outside the workspace.
      toast.error(reason(err, 'Failed to save'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{rule ? 'Edit automation' : 'New automation'}</DialogTitle>
          <DialogDescription>Pick a trigger, then the actions to run when it fires.</DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <Label htmlFor="auto-name">Name</Label>
            <Input id="auto-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Welcome new leads" className="mt-1" />
          </div>
          <div>
            <Label htmlFor="auto-trigger">Trigger</Label>
            <Select value={trigger} onValueChange={(v) => setTrigger(v as TriggerType)}>
              <SelectTrigger id="auto-trigger" className="mt-1"><SelectValue /></SelectTrigger>
              <SelectContent>
                {(Object.keys(TRIGGER_LABELS) as TriggerType[]).map((t) => (
                  <SelectItem key={t} value={t}>{TRIGGER_LABELS[t]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div>
            <Label>Actions</Label>
            <div className="mt-1.5 space-y-2">
              {actions.map((a, i) => (
                <div key={i} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <div className="flex items-center gap-2">
                    <Select value={a.type} onValueChange={(v) => setActionType(i, v as ActionType)}>
                      <SelectTrigger className="h-8 w-48 bg-white"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        {(Object.keys(ACTION_LABELS) as ActionType[]).map((t) => (
                          <SelectItem key={t} value={t}>{ACTION_LABELS[t]}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                    <button type="button" onClick={() => setActions((p) => p.filter((_, idx) => idx !== i))} className="ml-auto rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600">
                      <Trash2 className="h-3.5 w-3.5" />
                    </button>
                  </div>
                  <div className="mt-2 space-y-2">
                    {a.type === 'create_task' && (
                      <>
                        <Input value={a.config.title ?? ''} onChange={(e) => setConfig(i, 'title', e.target.value)} placeholder="Task title" className="h-8 bg-white" />
                        <div className="flex items-center gap-2 text-sm text-slate-500">
                          Due in <Input type="number" min={0} value={a.config.dueInDays ?? ''} onChange={(e) => setConfig(i, 'dueInDays', e.target.value)} className="h-8 w-20 bg-white" /> days
                        </div>
                      </>
                    )}
                    {a.type === 'send_email' && (
                      <>
                        <Input value={a.config.subject ?? ''} onChange={(e) => setConfig(i, 'subject', e.target.value)} placeholder="Subject" className="h-8 bg-white" />
                        <Textarea value={a.config.body ?? ''} onChange={(e) => setConfig(i, 'body', e.target.value)} placeholder="Email body" rows={3} className="resize-none bg-white" />
                      </>
                    )}
                    {a.type === 'notify' && (
                      <Input value={a.config.message ?? ''} onChange={(e) => setConfig(i, 'message', e.target.value)} placeholder="Notification message" className="h-8 bg-white" />
                    )}
                    {a.type === 'assign' && (
                      <Select
                        value={a.config.userId ?? ''}
                        onValueChange={(v) => setConfigs(i, { userId: v, assigneeName: people.find((p) => p.userId === v)?.name ?? '' })}
                      >
                        <SelectTrigger className="h-8 bg-white" aria-label="Assign to"><SelectValue placeholder="Assign to…" /></SelectTrigger>
                        <SelectContent>
                          {people.map((p) => <SelectItem key={p.userId} value={p.userId}>{p.name || p.email}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    )}
                    {a.type === 'add_to_sequence' && (
                      <Select
                        value={a.config.sequenceId ?? ''}
                        onValueChange={(v) => setConfigs(i, { sequenceId: v, sequence: sequences.find((s) => s.id === v)?.name ?? '' })}
                      >
                        <SelectTrigger className="h-8 bg-white" aria-label="Sequence"><SelectValue placeholder={sequences.length ? 'Pick a sequence…' : 'No sequences yet'} /></SelectTrigger>
                        <SelectContent>
                          {sequences.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
                        </SelectContent>
                      </Select>
                    )}
                  </div>
                </div>
              ))}
            </div>
            <Button type="button" variant="outline" size="sm" onClick={() => setActions((p) => [...p, newAction('create_task')])} className="mt-2 gap-1.5">
              <Plus className="h-4 w-4" /> Add action
            </Button>
          </div>

          <label className="flex items-center gap-2 text-sm text-slate-700">
            <Switch checked={enabled} onCheckedChange={setEnabled} /> Enabled
          </label>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={save} disabled={saving} className={cn('gap-1.5')}>
            {saving && <Loader2 className="h-4 w-4 animate-spin" />} {rule ? 'Save changes' : 'Create automation'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

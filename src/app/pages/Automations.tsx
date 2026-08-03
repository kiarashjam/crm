import { useEffect, useState } from 'react';
import {
  Zap, Plus, Trash2, Pencil, Loader2, ArrowRight, Mail, CheckSquare, Bell, UserPlus, Workflow,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero from '@/app/components/PageHero';
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

const ACTION_ICON: Record<ActionType, React.ElementType> = {
  create_task: CheckSquare, send_email: Mail, notify: Bell, assign: UserPlus, add_to_sequence: Workflow,
};

type DraftAction = Omit<AutomationAction, 'id'>;

const newAction = (type: ActionType = 'create_task'): DraftAction => ({ type, config: {} });

export default function Automations() {
  const [rules, setRules] = useState<AutomationRule[]>([]);
  const [loading, setLoading] = useState(true);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<AutomationRule | null>(null);

  const load = () => {
    setLoading(true);
    getAutomations().then(setRules).catch(() => toast.error('Failed to load automations')).finally(() => setLoading(false));
  };
  useEffect(() => { load(); }, []);

  const toggle = async (r: AutomationRule) => {
    setRules((prev) => prev.map((x) => (x.id === r.id ? { ...x, enabled: !x.enabled } : x)));
    const ok = await updateAutomation(r.id, { enabled: !r.enabled });
    if (!ok) { toast.error('Failed to update'); load(); }
  };

  const remove = async (r: AutomationRule) => {
    if (!confirm(`Delete automation "${r.name}"?`)) return;
    setRules((prev) => prev.filter((x) => x.id !== r.id));
    const ok = await deleteAutomation(r.id);
    if (ok) toast.success('Automation deleted'); else { toast.error('Failed to delete'); load(); }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Zap}
            iconGradient="from-amber-500 to-orange-500"
            title="Automations"
            subtitle="Run actions automatically when something happens in your CRM."
            actions={
              <WriteOnly>
                <Button onClick={() => { setEditing(null); setEditorOpen(true); }} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 hover:from-amber-600 hover:to-orange-600 shadow-lg shadow-orange-500/30 font-semibold text-white">
                  <Plus className="h-4 w-4" /> New automation
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Automations', value: rules.length, icon: Zap, tone: 'amber' },
              { label: 'Active', value: rules.filter((r) => r.enabled).length, icon: CheckSquare, tone: 'emerald' },
              { label: 'Total runs', value: rules.reduce((s, r) => s + (r.runCount ?? 0), 0), icon: ArrowRight, tone: 'indigo' },
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
            <div className="space-y-3">
              {rules.map((r) => (
                <div key={r.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                  <div className="flex items-start justify-between gap-3">
                    <div className="min-w-0">
                      <div className="flex items-center gap-2">
                        <h3 className="font-semibold text-slate-900">{r.name}</h3>
                        {typeof r.runCount === 'number' && <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500">{r.runCount} runs</span>}
                      </div>
                      <div className="mt-2 flex flex-wrap items-center gap-2 text-sm text-slate-600">
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
                    </div>
                    <WriteOnly>
                      <div className="flex shrink-0 items-center gap-2">
                        <Switch checked={r.enabled} onCheckedChange={() => toggle(r)} aria-label="Enabled" />
                        <button type="button" onClick={() => { setEditing(r); setEditorOpen(true); }} className="rounded p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-700"><Pencil className="h-4 w-4" /></button>
                        <button type="button" onClick={() => remove(r)} className="rounded p-1.5 text-slate-400 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
                      </div>
                    </WriteOnly>
                  </div>
                </div>
              ))}
            </div>
          )}
        </main>
      </PageTransition>

      {editorOpen && (
        <AutomationEditor
          open={editorOpen}
          onOpenChange={setEditorOpen}
          rule={editing}
          onSaved={() => { setEditorOpen(false); load(); }}
        />
      )}
    </div>
  );
}

function AutomationEditor({ open, onOpenChange, rule, onSaved }: {
  open: boolean; onOpenChange: (o: boolean) => void; rule: AutomationRule | null; onSaved: () => void;
}) {
  const [name, setName] = useState(rule?.name ?? '');
  const [trigger, setTrigger] = useState<TriggerType>(rule?.trigger ?? 'lead.created');
  const [enabled, setEnabled] = useState(rule?.enabled ?? true);
  const [actions, setActions] = useState<DraftAction[]>(
    rule?.actions.map(({ id: _id, ...rest }) => rest) ?? [newAction('send_email')],
  );
  const [saving, setSaving] = useState(false);

  const setActionType = (i: number, type: ActionType) => setActions((p) => p.map((a, idx) => (idx === i ? { type, config: {} } : a)));
  const setConfig = (i: number, key: string, value: string) =>
    setActions((p) => p.map((a, idx) => (idx === i ? { ...a, config: { ...a.config, [key]: value } } : a)));

  const save = async () => {
    if (!name.trim()) { toast.error('Name is required'); return; }
    if (actions.length === 0) { toast.error('Add at least one action'); return; }
    setSaving(true);
    try {
      const payload = { name: name.trim(), trigger, enabled, actions };
      const res = rule ? await updateAutomation(rule.id, payload) : await createAutomation(payload);
      if (!res) { toast.error('Failed to save'); return; }
      toast.success(rule ? 'Automation updated' : 'Automation created');
      onSaved();
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
                      <Input value={a.config.assigneeName ?? ''} onChange={(e) => setConfig(i, 'assigneeName', e.target.value)} placeholder="Assignee name" className="h-8 bg-white" />
                    )}
                    {a.type === 'add_to_sequence' && (
                      <Input value={a.config.sequence ?? ''} onChange={(e) => setConfig(i, 'sequence', e.target.value)} placeholder="Sequence name" className="h-8 bg-white" />
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

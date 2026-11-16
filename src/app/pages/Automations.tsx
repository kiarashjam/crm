import { useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import {
  Zap, Plus, Trash2, Pencil, Loader2, ArrowRight, Mail, CheckSquare, Bell, UserPlus, Workflow, Clock, GitBranch, Filter, CalendarClock,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { Button } from '@/app/components/ui/button';
import { Switch } from '@/app/components/ui/switch';
import {
  Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle,
} from '@/app/components/ui/dialog';
import { cn } from '@/app/components/ui/utils';
import {
  deleteAutomation, updateAutomation, STEP_LABELS, TRIGGER_LABELS,
  type AutomationCatalog, type AutomationRule, type AutomationStep, type StepType,
} from '@/app/api/automations';
import { WriteOnly } from '@/app/components/WriteOnly';
import { LoadFailed } from '@/app/components/LoadFailed';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { servesDemoData } from '@/app/api/apiClient';
import { moduleOn, orgCan, PERMISSION } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { queryKeys, useAutomationCatalog, useAutomations, useWorkspaceScope } from '@/app/hooks/queries';
import { isEditConflict, showEditConflict } from '@/app/lib/editConflict';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { RuleEditor } from './automations/RuleEditor';
import { RunsPanel } from './automations/RunsPanel';
import { describeWindow } from './automations/SendWindowEditor';
import { groupEmpty } from './automations/ConditionsEditor';
import { currentDateLocale } from '@/app/i18n/current';

const STEP_ICON: Record<StepType, React.ElementType> = {
  create_task: CheckSquare, send_email: Mail, notify: Bell, assign: UserPlus, add_to_sequence: Workflow, wait: Clock, branch: GitBranch,
};

const reason = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

/** A step in a few words, for the rule's summary: "Wait 2 days", "If / else". */
function stepWords(step: AutomationStep): string {
  if (step.type === 'wait') {
    if (step.config.mode === 'until') return 'Wait for a date';
    const amount = step.config.amount ?? '';
    const unit = step.config.unit ?? 'days';
    return amount ? `Wait ${amount} ${amount === '1' ? unit.replace(/s$/, '') : unit}` : 'Wait';
  }
  return STEP_LABELS[step.type] ?? step.type;
}

function conditionCount(rule: AutomationRule): number {
  const group = rule.conditions;
  if (!group || groupEmpty(group)) return 0;
  return group.conditions.length + (group.groups ?? []).reduce((n, g) => n + g.conditions.length, 0);
}

export default function Automations() {
  const rulesQuery = useAutomations();
  const catalogQuery = useAutomationCatalog();
  const rules = rulesQuery.data ?? [];
  usePageLoading(rulesQuery.isPending);
  const queryClient = useQueryClient();
  const ws = useWorkspaceScope();
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<AutomationRule | null>(null);
  const [runsRule, setRunsRule] = useState('');
  const org = useOrgOptional()?.currentOrg ?? null;
  // A rule acts for the whole workspace, so the server lets whoever manages its settings
  // (the owner and managers by default) change one, and see what the rules did. Everyone
  // else sees the rules but isn't offered what would be refused.
  const canChange = servesDemoData() || (Boolean(org) && orgCan(org, PERMISSION.ManageSettings));
  const modulesOn = { sales: moduleOn(org, 'sales'), club: moduleOn(org, 'club') };

  const refresh = () => { void rulesQuery.refetch(); };
  const open = (rule: AutomationRule | null) => { setEditing(rule); setEditorOpen(true); };

  const toggle = async (r: AutomationRule) => {
    // Shown at once; the server's answer replaces it.
    queryClient.setQueryData<AutomationRule[]>(queryKeys.automations.all(ws), (prev) =>
      prev?.map((x) => (x.id === r.id ? { ...x, enabled: !x.enabled } : x)));
    try {
      const saved = await updateAutomation(r.id, { enabled: !r.enabled, ...(r.version ? { version: r.version } : {}) });
      if (!saved) toast.error('The automation could not be changed.');
    } catch (err) {
      if (isEditConflict(err)) showEditConflict(err.message, refresh);
      else toast.error(reason(err, 'The automation could not be changed.'));
    } finally {
      refresh();
    }
  };

  const remove = async (r: AutomationRule) => {
    const going = r.activeRuns ? ` Its ${r.activeRuns} ${r.activeRuns === 1 ? 'run' : 'runs'} still going will stop.` : '';
    if (!confirm(`Delete automation "${r.name}"?${going}`)) return;
    try {
      if (await deleteAutomation(r.id)) toast.success('Automation deleted');
      else toast.error('The automation could not be deleted.');
    } catch (err) {
      toast.error(reason(err, 'The automation could not be deleted.'));
    } finally {
      refresh();
    }
  };

  // How this person likes rules shown: stacked, as cards or a table, saved for them.
  const view = useViewPreference('automations', { layouts: ['list', 'grid', 'table'] });
  const openRule = canChange ? (r: AutomationRule) => open(r) : undefined;
  const shortDate = (iso?: string) => {
    if (!iso) return '—';
    const d = new Date(iso);
    return Number.isNaN(d.getTime()) ? '—' : d.toLocaleDateString(currentDateLocale(), { month: 'short', day: 'numeric', year: 'numeric' });
  };

  /** What the rule is still doing and what failed, which open its runs for those who may see them. */
  const runBadges = (r: AutomationRule) => {
    const badges: { label: string; tone: string; status: string }[] = [];
    if (r.activeRuns) badges.push({ label: `${r.activeRuns} still going`, tone: 'bg-indigo-50 text-indigo-700', status: 'active' });
    if (r.failedRuns) badges.push({ label: `${r.failedRuns} failed`, tone: 'bg-red-50 text-red-700', status: 'failed' });
    return badges.map((b) => (canChange ? (
      <button
        key={b.status}
        type="button"
        onClick={() => { setRunsRule(r.id); document.getElementById('automation-runs')?.scrollIntoView?.({ behavior: 'smooth' }); }}
        className={cn('shrink-0 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-medium hover:underline', b.tone)}
      >
        {b.label}
      </button>
    ) : (
      <span key={b.status} className={cn('shrink-0 whitespace-nowrap rounded-full px-2 py-0.5 text-[11px] font-medium', b.tone)}>{b.label}</span>
    )));
  };

  /** The rule's trigger, its conditions, then each step it takes. */
  const ruleFlow = (r: AutomationRule) => {
    const conditions = conditionCount(r);
    return (
      <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
        <span className="inline-flex items-center gap-1 rounded-lg bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700">{TRIGGER_LABELS[r.trigger] ?? r.trigger}</span>
        {r.trigger === 'membership.renewal_due' && r.triggerConfig?.daysBefore && (
          <span className="inline-flex items-center gap-1 rounded-lg bg-indigo-50 px-2 py-1 text-xs font-medium text-indigo-700">
            <CalendarClock className="h-3 w-3" /> {r.triggerConfig.daysBefore} days ahead
          </span>
        )}
        {conditions > 0 && (
          <span className="inline-flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 text-xs font-medium text-amber-800">
            <Filter className="h-3 w-3" /> Only if {conditions} {conditions === 1 ? 'condition' : 'conditions'}
          </span>
        )}
        {r.actions.map((a, i) => {
          const Icon = STEP_ICON[a.type] ?? Zap;
          return (
            <span key={a.id ?? i} className="inline-flex items-center gap-1">
              <ArrowRight className="h-3.5 w-3.5 text-slate-300" />
              <span className="inline-flex items-center gap-1 rounded-lg bg-slate-100 px-2 py-1 text-xs font-medium text-slate-600">
                <Icon className="h-3 w-3" /> {stepWords(a)}
              </span>
            </span>
          );
        })}
        {r.sendWindow && (
          <span className="inline-flex items-center gap-1 rounded-lg bg-slate-50 px-2 py-1 text-xs text-slate-500">
            <Mail className="h-3 w-3" /> Emails {describeWindow(r.sendWindow)}
          </span>
        )}
      </div>
    );
  };

  /** On, edit and delete: the same on a card, a row and a table line. */
  const ruleControls = (r: AutomationRule) => canChange && (
    <WriteOnly permission={PERMISSION.ManageSettings}>
      <div className="flex shrink-0 items-center gap-2">
        <Switch checked={r.enabled} onCheckedChange={() => toggle(r)} aria-label={`${r.name} enabled`} />
        <button type="button" onClick={() => open(r)} aria-label={`Edit ${r.name}`} className="rounded p-1.5 text-slate-500 hover:bg-slate-100 hover:text-slate-700"><Pencil className="h-4 w-4" /></button>
        <button type="button" onClick={() => remove(r)} aria-label={`Delete ${r.name}`} className="rounded p-1.5 text-slate-500 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-4 w-4" /></button>
      </div>
    </WriteOnly>
  );

  const ruleColumns: RecordColumn<AutomationRule>[] = [
    { id: 'name', label: 'Name', required: true, cell: (r) => r.name },
    { id: 'trigger', label: 'When', cell: (r) => TRIGGER_LABELS[r.trigger] ?? r.trigger },
    {
      id: 'conditions',
      label: 'Only if',
      cell: (r) => { const n = conditionCount(r); return n ? `${n} ${n === 1 ? 'condition' : 'conditions'}` : '—'; },
    },
    {
      id: 'actions',
      label: 'Then',
      cell: (r) => (r.actions.length ? r.actions.map(stepWords).join(' → ') : '—'),
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
    { id: 'failed', label: 'Failed', numeric: true, cell: (r) => r.failedRuns ?? 0 },
    { id: 'created', label: 'Created', byDefault: false, cell: (r) => shortDate(r.createdAtUtc) },
  ];

  const enabledCount = rules.filter((r) => r.enabled).length;
  const totalRuns = rules.reduce((s, r) => s + (r.runCount ?? 0), 0);
  const failedRuns = rules.reduce((s, r) => s + (r.failedRuns ?? 0), 0);
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
            subtitle="When something happens, check the record, then take the steps you set: tasks, emails, waits and branches."
            actions={canChange && (
              <WriteOnly permission={PERMISSION.ManageSettings}>
                <Button onClick={() => open(null)} className={heroPrimary('amber')}>
                  <Plus className="h-4 w-4" /> New automation
                </Button>
              </WriteOnly>
            )}
            stats={[
              { label: 'Automations', value: rules.length, icon: Zap, tone: 'amber', hint: `${triggerCount} ${triggerCount === 1 ? 'trigger' : 'triggers'}` },
              { label: 'Active', value: enabledCount, icon: CheckSquare, tone: 'emerald', hint: `${rules.length - enabledCount} off` },
              { label: 'Total runs', value: totalRuns, icon: ArrowRight, tone: 'indigo', hint: failedRuns ? `${failedRuns} failed` : 'all time' },
            ]}
          />

          {rulesQuery.isError && rulesQuery.data && (
            <LoadFailed stale what="Automations" error={rulesQuery.error} onRetry={refresh} retrying={rulesQuery.isFetching} className="mb-4" />
          )}

          {rulesQuery.isPending ? (
            <div className="flex items-center justify-center py-20 text-slate-500"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : rulesQuery.isError && !rulesQuery.data ? (
            <LoadFailed what="Automations" error={rulesQuery.error} onRetry={refresh} retrying={rulesQuery.isFetching} />
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
                      <div className="flex flex-wrap items-center gap-2">
                        {view.layout === 'grid' && (
                          <span className={cn('flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-sm', r.enabled ? 'from-amber-400 to-orange-500' : 'from-slate-300 to-slate-400')}>
                            <Zap className="h-4 w-4" aria-hidden />
                          </span>
                        )}
                        <h3 className="font-semibold text-slate-900">{r.name}</h3>
                        {typeof r.runCount === 'number' && <span className="shrink-0 whitespace-nowrap rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-500">{r.runCount} runs</span>}
                        {runBadges(r)}
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

          {/* What the rules did is for those who manage them: a run names its record and the emails it queued. */}
          {canChange && !rulesQuery.isPending && !(rulesQuery.isError && !rulesQuery.data) && (
            <RunsPanel rules={rules} ruleId={runsRule} onRuleChange={setRunsRule} canAct={canChange} />
          )}
        </main>
      </PageTransition>

      {editorOpen && (
        catalogQuery.data ? (
          <RuleEditor
            open={editorOpen}
            onOpenChange={setEditorOpen}
            rule={editing}
            catalog={catalogQuery.data}
            timeZone={org?.timeZone}
            modulesOn={modulesOn}
            onSaved={() => { setEditorOpen(false); refresh(); }}
            onConflict={() => { setEditorOpen(false); refresh(); }}
          />
        ) : (
          <BuilderUnavailable open={editorOpen} onOpenChange={setEditorOpen} catalog={catalogQuery} />
        )
      )}
    </div>
  );
}

/** The builder before what it is drawn from has been read, or when it could not be. */
function BuilderUnavailable({ open, onOpenChange, catalog }: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catalog: { isError: boolean; error: unknown; isFetching: boolean; refetch: () => Promise<{ data?: AutomationCatalog }> };
}) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Automation builder</DialogTitle>
          <DialogDescription>The triggers, conditions and steps a rule can have.</DialogDescription>
        </DialogHeader>
        {catalog.isError ? (
          <LoadFailed what="The automation builder" error={catalog.error} onRetry={() => void catalog.refetch()} retrying={catalog.isFetching} className="p-4 shadow-none" />
        ) : (
          <div className="flex items-center justify-center py-10 text-slate-500"><Loader2 className="h-5 w-5 animate-spin" /></div>
        )}
      </DialogContent>
    </Dialog>
  );
}

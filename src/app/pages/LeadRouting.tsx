import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { Copy, Loader2, Lock, RotateCw, Trash2 } from 'lucide-react';
import AppHeader from '@/app/components/AppHeader';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { isUsingRealApi } from '@/app/api/apiClient';
import { getOrgMembers, isOrgAdmin, type OrgMemberDto } from '@/app/api/organizations';
import {
  type AssignmentRule,
  type CaptureForm,
  type ConversionReport,
  type LeadQueue,
  type ScoringRule,
  type Territory,
  createCaptureForm,
  deleteAssignmentRule,
  deleteQueue,
  deleteTerritory,
  getAssignmentRules,
  getCaptureForms,
  getConversionReport,
  getQueues,
  getScoringRules,
  getSla,
  getTerritories,
  saveAssignmentRule,
  saveQueue,
  saveScoringRules,
  saveSla,
  saveTerritory,
} from '@/app/api/leadOps';

/**
 * Lead routing: response deadline, scoring, assignment, territories, queues, capture
 * forms, and how leads convert.
 *
 * Each section loads and saves on its own. They used to load in one Promise.all:
 * if any of the seven requests failed, the score rules stayed empty and the
 * deadline stayed at its default 4 — and "Save" then wrote those over the real
 * settings. Saving one section also reloaded every other one, throwing away edits
 * not yet saved there. Now a section that did not load cannot be saved, and a save
 * touches only its own section.
 */

type Section<T> = { status: 'loading' | 'ready' | 'failed'; data: T };

const SCORE_FIELDS: { value: string; label: string; needsMatch: boolean; hint?: string }[] = [
  { value: 'source', label: 'Source contains', needsMatch: true },
  { value: 'stageAtLeast', label: 'Reached stage', needsMatch: true, hint: 'e.g. Contract Pending' },
  { value: 'status', label: 'Status contains', needsMatch: true },
  { value: 'country', label: 'Country is', needsMatch: true },
  { value: 'industry', label: 'Industry contains', needsMatch: true },
  { value: 'hasPhone', label: 'Has a phone number', needsMatch: false },
  { value: 'hasJobTitle', label: 'Has a job title', needsMatch: false },
  { value: 'hasCompany', label: 'Has a company', needsMatch: false },
  { value: 'contactedWithinDays', label: 'Contacted within (days)', needsMatch: true },
  { value: 'emailOpens', label: 'Email opens (cap)', needsMatch: true },
  { value: 'staleDays', label: 'Untouched for (days)', needsMatch: true },
];

const KIND_LABEL: Record<string, string> = { RoundRobin: 'Round robin', Source: 'By source', Territory: 'By territory' };

function useSection<T>(load: () => Promise<T>, empty: T, enabled: boolean) {
  const [section, setSection] = useState<Section<T>>({ status: 'loading', data: empty });
  const reload = useCallback(() => {
    if (!enabled) return;
    setSection((was) => ({ ...was, status: 'loading' }));
    load()
      .then((data) => setSection({ status: 'ready', data }))
      .catch(() => setSection((was) => ({ ...was, status: 'failed' })));
    // eslint-disable-next-line react-hooks/exhaustive-deps -- load is a stable module function
  }, [enabled]);
  useEffect(() => { reload(); }, [reload]);
  const set = (data: T) => setSection({ status: 'ready', data });
  return { ...section, reload, set };
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

export default function LeadRouting() {
  const navigate = useNavigate();
  const org = useOrgOptional();
  const live = isUsingRealApi();
  // Writes need an owner or a manager; the server refuses anyone else.
  const canManage = isOrgAdmin(org?.currentOrg);
  const orgId = org?.currentOrgId ?? null;

  const sla = useSection(() => getSla().then((s) => s.hours), 4, live);
  const scoring = useSection(getScoringRules, [] as ScoringRule[], live);
  const assignment = useSection(getAssignmentRules, [] as AssignmentRule[], live);
  const territories = useSection(getTerritories, [] as Territory[], live);
  const queues = useSection(getQueues, [] as LeadQueue[], live);
  const forms = useSection(getCaptureForms, [] as CaptureForm[], live);
  const report = useSection(getConversionReport, null as ConversionReport | null, live);

  const [members, setMembers] = useState<OrgMemberDto[] | null>(null);
  useEffect(() => {
    if (!orgId || !live) return;
    getOrgMembers(orgId).then(setMembers).catch(() => setMembers([]));
  }, [orgId, live]);
  // People who can work a lead: Viewers cannot log activity, so routing skips them.
  const workers = useMemo(() => (members ?? []).filter((m) => m.role !== 3), [members]);
  const nameOf = useCallback(
    (userId: string) => {
      if (members === null) return '…';
      const m = members.find((x) => x.userId === userId);
      return m ? m.name || m.email : 'Former member';
    },
    [members],
  );

  if (!live) {
    return (
      <div className="min-h-screen bg-slate-50 dark:bg-slate-950">
        <AppHeader />
        <main className="mx-auto max-w-5xl px-4 py-8">
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Lead routing</h1>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">Lead routing runs on the live workspace and is not available in demo mode.</p>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-slate-50 dark:bg-slate-950">
      <AppHeader />
      <main className="mx-auto max-w-5xl space-y-6 px-4 py-8">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Lead routing</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">Who gets each new lead, how fast they should reply, and how leads are scored.</p>
          </div>
          <Button variant="outline" onClick={() => navigate('/leads')}>Back to leads</Button>
        </div>

        {!canManage && (
          <p className="flex items-center gap-2 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-200" role="note">
            <Lock className="h-4 w-4 shrink-0" aria-hidden="true" />
            Only owners and managers can change lead routing. You can see how it is set up.
          </p>
        )}

        <Panel title="First response" status={sla.status} onRetry={sla.reload}>
          <SlaEditor hours={sla.data} disabled={!canManage || sla.status !== 'ready'} onSaved={sla.set} />
        </Panel>

        <Panel title="Score rules" status={scoring.status} onRetry={scoring.reload}>
          <ScoreRulesEditor rules={scoring.data} disabled={!canManage || scoring.status !== 'ready'} onSaved={scoring.set} />
        </Panel>

        <Panel title="Assignment rules" status={assignment.status} onRetry={assignment.reload}>
          <AssignmentEditor
            rules={assignment.data}
            territories={territories.data}
            queues={queues.data}
            disabled={!canManage || assignment.status !== 'ready'}
            onChange={assignment.set}
          />
        </Panel>

        <Panel title="Territories" status={territories.status} onRetry={territories.reload}>
          <TerritoryEditor items={territories.data} disabled={!canManage || territories.status !== 'ready'} onChange={territories.set} />
        </Panel>

        <Panel title="Queues" status={queues.status} onRetry={queues.reload}>
          <QueueEditor items={queues.data} workers={workers} nameOf={nameOf} disabled={!canManage || queues.status !== 'ready'} onChange={queues.set} />
        </Panel>

        <Panel title="Public capture forms" status={forms.status} onRetry={forms.reload}>
          <FormsEditor items={forms.data} disabled={!canManage || forms.status !== 'ready'} onChange={forms.set} />
        </Panel>

        <Panel title="How leads convert" status={report.status} onRetry={report.reload}>
          <ReportTable caption="By source" rows={report.data?.bySource ?? []} keyLabel={(k) => k} />
          <ReportTable
            caption="By owner"
            rows={report.data?.byOwner ?? []}
            // The report groups by user id; it used to print the raw GUIDs.
            keyLabel={(k) => (k === 'Unassigned' ? k : nameOf(k))}
          />
        </Panel>
      </main>
    </div>
  );
}

// ── sections ────────────────────────────────────────────────────────────────

function SlaEditor({ hours, disabled, onSaved }: { hours: number; disabled: boolean; onSaved: (h: number) => void }) {
  const [value, setValue] = useState(hours);
  const [busy, setBusy] = useState(false);
  useEffect(() => setValue(hours), [hours]);
  const save = async () => {
    setBusy(true);
    try {
      const saved = await saveSla(value);
      onSaved(saved.hours);
      toast.success('Response deadline saved');
    } catch (err) {
      toast.error(errorText(err, 'Could not save the deadline'));
    } finally {
      setBusy(false);
    }
  };
  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="text-sm text-slate-700 dark:text-slate-300">
        Hours before a new lead counts as late
        <Input className="mt-1 w-28" type="number" min={1} max={168} value={value} disabled={disabled} onChange={(e) => setValue(Number(e.target.value))} />
      </label>
      <Button onClick={save} disabled={disabled || busy || value < 1 || value > 168}>
        {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />}Save
      </Button>
    </div>
  );
}

function ScoreRulesEditor({ rules, disabled, onSaved }: { rules: ScoringRule[]; disabled: boolean; onSaved: (r: ScoringRule[]) => void }) {
  const [draft, setDraft] = useState(rules);
  const [busy, setBusy] = useState(false);
  useEffect(() => setDraft(rules), [rules]);
  const update = (index: number, patch: Partial<ScoringRule>) => setDraft(draft.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  const save = async () => {
    setBusy(true);
    try {
      onSaved(await saveScoringRules(draft));
      toast.success('Score rules saved');
    } catch (err) {
      toast.error(errorText(err, 'Could not save the score rules'));
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <p className="mb-3 text-xs text-slate-500 dark:text-slate-400">Points add up to a score between 0 and 100. With no active rules, the built-in set is used.</p>
      <ul className="space-y-2">
        {draft.map((rule, index) => {
          const field = SCORE_FIELDS.find((f) => f.value === rule.field);
          const n = index + 1;
          return (
            <li key={rule.id} className="grid grid-cols-1 items-center gap-2 sm:grid-cols-[1.4fr_1.4fr_0.6fr_auto_auto]">
              <select
                aria-label={`Rule ${n}: what it checks`}
                className="h-10 rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900"
                value={rule.field}
                disabled={disabled}
                onChange={(e) => update(index, { field: e.target.value })}
              >
                {!field && <option value={rule.field}>{rule.field}</option>}
                {SCORE_FIELDS.map((f) => <option key={f.value} value={f.value}>{f.label}</option>)}
              </select>
              <Input
                aria-label={`Rule ${n}: value to match`}
                value={rule.matchValue ?? ''}
                placeholder={field?.needsMatch === false ? '—' : field?.hint ?? 'Value'}
                disabled={disabled || field?.needsMatch === false}
                onChange={(e) => update(index, { matchValue: e.target.value })}
              />
              <Input aria-label={`Rule ${n}: points`} type="number" value={rule.points} disabled={disabled} onChange={(e) => update(index, { points: Number(e.target.value) })} />
              <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
                <input type="checkbox" checked={rule.isActive} disabled={disabled} onChange={(e) => update(index, { isActive: e.target.checked })} />
                Active
              </label>
              <Button variant="ghost" size="sm" disabled={disabled} aria-label={`Remove rule ${n}`} onClick={() => setDraft(draft.filter((_, i) => i !== index))}>
                <Trash2 className="h-4 w-4" aria-hidden="true" />
              </Button>
            </li>
          );
        })}
      </ul>
      <div className="mt-3 flex flex-wrap gap-2">
        <Button variant="outline" disabled={disabled} onClick={() => setDraft([...draft, { id: crypto.randomUUID(), field: 'source', matchValue: '', points: 5, isActive: true }])}>Add rule</Button>
        <Button onClick={save} disabled={disabled || busy}>
          {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />}Save score rules
        </Button>
      </div>
    </>
  );
}

function AssignmentEditor({ rules, territories, queues, disabled, onChange }: {
  rules: AssignmentRule[]; territories: Territory[]; queues: LeadQueue[]; disabled: boolean; onChange: (r: AssignmentRule[]) => void;
}) {
  const [name, setName] = useState('');
  const [kind, setKind] = useState('RoundRobin');
  const [match, setMatch] = useState('');
  const [territoryId, setTerritoryId] = useState('');
  const [queueId, setQueueId] = useState('');
  const [busy, setBusy] = useState(false);
  const incomplete = !name.trim() || (kind === 'Source' && !match.trim()) || (kind === 'Territory' && !territoryId);

  const add = async () => {
    setBusy(true);
    try {
      const saved = await saveAssignmentRule({
        name: name.trim(),
        kind,
        sourceMatch: kind === 'Source' ? match.trim() : undefined,
        // The chosen territory and queue — these used to be territories[0] and queues[0].
        territoryId: kind === 'Territory' ? territoryId : undefined,
        queueId: queueId || undefined,
        isActive: true,
        priority: rules.length,
      });
      onChange([...rules, saved]);
      setName('');
      setMatch('');
      toast.success('Rule added');
    } catch (err) {
      toast.error(errorText(err, 'Could not add the rule'));
    } finally {
      setBusy(false);
    }
  };

  const toggle = async (rule: AssignmentRule) => {
    try {
      const saved = await saveAssignmentRule({ ...rule, isActive: !rule.isActive });
      onChange(rules.map((r) => (r.id === rule.id ? saved : r)));
    } catch (err) {
      toast.error(errorText(err, 'Could not change the rule'));
    }
  };

  const remove = async (rule: AssignmentRule) => {
    try {
      await deleteAssignmentRule(rule.id);
      onChange(rules.filter((r) => r.id !== rule.id));
    } catch (err) {
      toast.error(errorText(err, 'Could not delete the rule'));
    }
  };

  return (
    <>
      <p className="mb-3 text-xs text-slate-500 dark:text-slate-400">Rules are tried in order; the first that matches assigns the lead, in turn, to the people in its queue (or everyone who can work leads).</p>
      <ul className="mb-4 space-y-2">
        {rules.length === 0 && <li className="text-sm text-slate-500">No rules yet. New leads stay unassigned.</li>}
        {rules.map((rule) => (
          <li key={rule.id} className="flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 px-3 py-2 text-sm dark:border-slate-700">
            <span className={rule.isActive ? 'font-medium text-slate-900 dark:text-white' : 'text-slate-400 line-through'}>{rule.name}</span>
            <span className="rounded-full bg-slate-100 px-2 py-0.5 text-xs text-slate-600 dark:bg-slate-800 dark:text-slate-300">{KIND_LABEL[rule.kind] ?? rule.kind}</span>
            {rule.sourceMatch && <span className="text-xs text-slate-500">source contains “{rule.sourceMatch}”</span>}
            {rule.territoryId && <span className="text-xs text-slate-500">territory {territories.find((t) => t.id === rule.territoryId)?.name ?? '(deleted)'}</span>}
            {rule.queueId && <span className="text-xs text-slate-500">queue {queues.find((q) => q.id === rule.queueId)?.name ?? '(deleted)'}</span>}
            <span className="ml-auto flex gap-1">
              <Button variant="outline" size="sm" disabled={disabled} onClick={() => toggle(rule)}>{rule.isActive ? 'Turn off' : 'Turn on'}</Button>
              <Button variant="ghost" size="sm" disabled={disabled} aria-label={`Delete rule ${rule.name}`} onClick={() => remove(rule)}>
                <Trash2 className="h-4 w-4" aria-hidden="true" />
              </Button>
            </span>
          </li>
        ))}
      </ul>
      <fieldset disabled={disabled} className="grid gap-2 sm:grid-cols-2">
        <legend className="mb-1 text-sm font-medium text-slate-700 dark:text-slate-300">Add a rule</legend>
        <label className="text-sm text-slate-700 dark:text-slate-300">Name<Input className="mt-1" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Website leads" /></label>
        <label className="text-sm text-slate-700 dark:text-slate-300">
          Matches
          <select aria-label="Matches" className="mt-1 h-10 w-full rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900" value={kind} onChange={(e) => setKind(e.target.value)}>
            <option value="RoundRobin">Every lead</option>
            <option value="Source">Leads from a source</option>
            <option value="Territory" disabled={territories.length === 0}>Leads in a territory{territories.length === 0 ? ' (add one first)' : ''}</option>
          </select>
        </label>
        {kind === 'Source' && (
          <label className="text-sm text-slate-700 dark:text-slate-300">Source contains<Input className="mt-1" value={match} onChange={(e) => setMatch(e.target.value)} placeholder="e.g. Website" /></label>
        )}
        {kind === 'Territory' && (
          <label className="text-sm text-slate-700 dark:text-slate-300">
            Territory
            <select aria-label="Territory" className="mt-1 h-10 w-full rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900" value={territoryId} onChange={(e) => setTerritoryId(e.target.value)}>
              <option value="">Choose…</option>
              {territories.map((t) => <option key={t.id} value={t.id}>{t.name}</option>)}
            </select>
          </label>
        )}
        <label className="text-sm text-slate-700 dark:text-slate-300">
          Assign within
          <select aria-label="Assign within" className="mt-1 h-10 w-full rounded-md border border-slate-200 bg-white px-2 text-sm dark:border-slate-700 dark:bg-slate-900" value={queueId} onChange={(e) => setQueueId(e.target.value)}>
            <option value="">Everyone who can work leads</option>
            {queues.map((q) => <option key={q.id} value={q.id}>Queue: {q.name}</option>)}
          </select>
        </label>
        <div className="sm:col-span-2">
          <Button onClick={add} disabled={busy || incomplete}>
            {busy && <Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden="true" />}Add rule
          </Button>
        </div>
      </fieldset>
    </>
  );
}

function TerritoryEditor({ items, disabled, onChange }: { items: Territory[]; disabled: boolean; onChange: (t: Territory[]) => void }) {
  const [name, setName] = useState('');
  const [countries, setCountries] = useState('');
  const [industries, setIndustries] = useState('');
  const [busy, setBusy] = useState(false);
  const add = async () => {
    setBusy(true);
    try {
      onChange([...items, await saveTerritory({ name: name.trim(), countries: countries.trim(), industries: industries.trim() })]);
      setName('');
      setCountries('');
      setIndustries('');
      toast.success('Territory added');
    } catch (err) {
      toast.error(errorText(err, 'Could not add the territory'));
    } finally {
      setBusy(false);
    }
  };
  const remove = async (t: Territory) => {
    try {
      await deleteTerritory(t.id);
      onChange(items.filter((x) => x.id !== t.id));
    } catch (err) {
      toast.error(errorText(err, 'Could not delete the territory'));
    }
  };
  return (
    <>
      <ul className="mb-3 space-y-1 text-sm">
        {items.length === 0 && <li className="text-slate-500">No territories yet.</li>}
        {items.map((t) => (
          <li key={t.id} className="flex items-center gap-2">
            <span className="font-medium text-slate-900 dark:text-white">{t.name}</span>
            <span className="text-slate-500">{[t.countries, t.industries].filter(Boolean).join(' · ') || 'matches nothing yet'}</span>
            <Button variant="ghost" size="sm" className="ml-auto" disabled={disabled} aria-label={`Delete territory ${t.name}`} onClick={() => remove(t)}>
              <Trash2 className="h-4 w-4" aria-hidden="true" />
            </Button>
          </li>
        ))}
      </ul>
      <p className="mb-2 text-xs text-slate-500 dark:text-slate-400">A lead is in a territory when its country or its industry is on the list.</p>
      <fieldset disabled={disabled} className="grid gap-2 sm:grid-cols-[1fr_1fr_1fr_auto] sm:items-end">
        <label className="text-sm text-slate-700 dark:text-slate-300">Territory name<Input className="mt-1" value={name} onChange={(e) => setName(e.target.value)} /></label>
        <label className="text-sm text-slate-700 dark:text-slate-300">Countries, comma separated<Input className="mt-1" value={countries} onChange={(e) => setCountries(e.target.value)} placeholder="e.g. Switzerland, France" /></label>
        <label className="text-sm text-slate-700 dark:text-slate-300">Industries, comma separated<Input className="mt-1" value={industries} onChange={(e) => setIndustries(e.target.value)} placeholder="optional" /></label>
        <Button variant="outline" onClick={add} disabled={busy || !name.trim() || (!countries.trim() && !industries.trim())}>Add territory</Button>
      </fieldset>
    </>
  );
}

function QueueEditor({ items, workers, nameOf, disabled, onChange }: {
  items: LeadQueue[]; workers: OrgMemberDto[]; nameOf: (id: string) => string; disabled: boolean; onChange: (q: LeadQueue[]) => void;
}) {
  const [name, setName] = useState('');
  const [picked, setPicked] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const add = async () => {
    setBusy(true);
    try {
      onChange([...items, await saveQueue({ name: name.trim(), memberUserIds: picked })]);
      setName('');
      setPicked([]);
      toast.success('Queue added');
    } catch (err) {
      toast.error(errorText(err, 'Could not add the queue'));
    } finally {
      setBusy(false);
    }
  };
  const remove = async (q: LeadQueue) => {
    try {
      await deleteQueue(q.id);
      onChange(items.filter((x) => x.id !== q.id));
    } catch (err) {
      toast.error(errorText(err, 'Could not delete the queue'));
    }
  };
  return (
    <>
      <ul className="mb-3 space-y-1 text-sm">
        {items.length === 0 && <li className="text-slate-500">No queues yet.</li>}
        {items.map((q) => (
          <li key={q.id} className="flex items-center gap-2">
            <span className="font-medium text-slate-900 dark:text-white">{q.name}</span>
            <span className="text-slate-500">{q.memberUserIds.length ? q.memberUserIds.map(nameOf).join(', ') : 'nobody'}</span>
            <Button variant="ghost" size="sm" className="ml-auto" disabled={disabled} aria-label={`Delete queue ${q.name}`} onClick={() => remove(q)}>
              <Trash2 className="h-4 w-4" aria-hidden="true" />
            </Button>
          </li>
        ))}
      </ul>
      <fieldset disabled={disabled} className="space-y-2">
        <label className="block text-sm text-slate-700 dark:text-slate-300">Queue name<Input className="mt-1 max-w-sm" value={name} onChange={(e) => setName(e.target.value)} /></label>
        {/* People, by name. This was a text box asking for "member user ids, comma
            separated" — ids no screen shows, so queues could not be filled in. */}
        <div role="group" aria-label="People in this queue" className="flex flex-wrap gap-2">
          {workers.map((m) => (
            <label key={m.userId} className="flex items-center gap-2 rounded-lg border border-slate-200 px-2 py-1 text-sm dark:border-slate-700">
              <input
                type="checkbox"
                checked={picked.includes(m.userId)}
                onChange={(e) => setPicked(e.target.checked ? [...picked, m.userId] : picked.filter((id) => id !== m.userId))}
              />
              {m.name || m.email}
            </label>
          ))}
        </div>
        <Button variant="outline" onClick={add} disabled={busy || !name.trim() || picked.length === 0}>Add queue</Button>
      </fieldset>
    </>
  );
}

function FormsEditor({ items, disabled, onChange }: { items: CaptureForm[]; disabled: boolean; onChange: (f: CaptureForm[]) => void }) {
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const add = async () => {
    setBusy(true);
    try {
      onChange([...items, await createCaptureForm(name.trim())]);
      setName('');
      toast.success('Form created');
    } catch (err) {
      toast.error(errorText(err, 'Could not create the form'));
    } finally {
      setBusy(false);
    }
  };
  const copy = async (url: string) => {
    try {
      await navigator.clipboard.writeText(url);
      toast.success('Link copied');
    } catch {
      toast.error('Could not copy — select the link and copy it');
    }
  };
  return (
    <>
      <p className="mb-3 text-xs text-slate-500 dark:text-slate-400">Anyone with a form's link can submit it, so share it where you mean it to be public — your website, for one.</p>
      <ul className="mb-3 space-y-2 text-sm">
        {items.length === 0 && <li className="text-slate-500">No forms yet.</li>}
        {items.map((form) => {
          const url = `${window.location.origin}${form.publicPath}`;
          return (
            <li key={form.id} className="flex flex-wrap items-center gap-2">
              <span className="font-medium text-slate-900 dark:text-white">{form.name}</span>
              <code className="truncate rounded bg-slate-100 px-2 py-0.5 text-xs text-slate-600 dark:bg-slate-800 dark:text-slate-300">{url}</code>
              <Button variant="ghost" size="sm" aria-label={`Copy the link to ${form.name}`} onClick={() => copy(url)}>
                <Copy className="h-4 w-4" aria-hidden="true" />
              </Button>
            </li>
          );
        })}
      </ul>
      <fieldset disabled={disabled} className="flex flex-wrap items-end gap-2">
        <label className="text-sm text-slate-700 dark:text-slate-300">Form name<Input className="mt-1" maxLength={64} value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Website" /></label>
        <Button onClick={add} disabled={busy || !name.trim()}>Create form</Button>
      </fieldset>
    </>
  );
}

// ── shared ──────────────────────────────────────────────────────────────────

function Panel({ title, status, onRetry, children }: { title: string; status: Section<unknown>['status']; onRetry: () => void; children: ReactNode }) {
  return (
    <section className="rounded-2xl border border-slate-200 bg-white p-5 dark:border-slate-700 dark:bg-slate-900" aria-busy={status === 'loading'}>
      <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-slate-900 dark:text-white">
        {title}
        {status === 'loading' && <Loader2 className="h-4 w-4 animate-spin text-slate-400" aria-label="Loading" />}
      </h2>
      {status === 'failed' ? (
        <div className="flex items-center gap-3 text-sm text-slate-600 dark:text-slate-300" role="status">
          This section could not be loaded, so it cannot be changed right now.
          <Button variant="outline" size="sm" onClick={onRetry}>
            <RotateCw className="mr-1 h-4 w-4" aria-hidden="true" />Try again
          </Button>
        </div>
      ) : children}
    </section>
  );
}

function ReportTable({ caption, rows, keyLabel }: { caption: string; rows: ConversionReport['bySource']; keyLabel: (key: string) => string }) {
  return (
    <div className="mb-4 overflow-x-auto">
      <table className="w-full text-left text-sm">
        <caption className="mb-2 text-left text-sm font-medium text-slate-700 dark:text-slate-300">{caption}</caption>
        <thead className="text-xs text-slate-500">
          <tr>
            <th scope="col" className="py-1 pr-3">Group</th><th scope="col" className="pr-3">Created</th><th scope="col" className="pr-3">Qualified</th>
            <th scope="col" className="pr-3">Converted</th><th scope="col" className="pr-3">Rate</th><th scope="col" className="pr-3">Hours to first reply</th>
            <th scope="col">Hours to convert</th>
          </tr>
        </thead>
        <tbody>
          {rows.length === 0 && <tr><td colSpan={7} className="py-2 text-slate-500">No leads yet.</td></tr>}
          {rows.map((row) => (
            <tr key={row.key} className="border-t border-slate-100 dark:border-slate-800">
              <td className="py-1 pr-3">{keyLabel(row.key)}</td>
              <td className="pr-3">{row.created}</td>
              <td className="pr-3">{row.qualified}</td>
              <td className="pr-3">{row.converted}</td>
              <td className="pr-3">{Math.round(row.conversionRate * 100)}%</td>
              <td className="pr-3">{row.avgHoursToFirstResponse ?? '—'}</td>
              <td>{row.avgHoursToConvert ?? '—'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

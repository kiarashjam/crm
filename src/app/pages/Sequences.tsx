import { useEffect, useMemo, useState } from 'react';
import {
  Workflow, Plus, Mail, Phone, CheckSquare, Clock, Trash2, Pencil, Play, Pause,
  Users, ArrowUp, ArrowDown, Loader2, X,
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
import { Badge } from '@/app/components/ui/badge';
import {
  Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter, DialogDescription,
} from '@/app/components/ui/dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import {
  getSequences, createSequence, updateSequence, deleteSequence,
  getEnrollments, enrollInSequence, setEnrollmentStatus, unenroll,
  getLeads,
  type Sequence, type SequenceStep, type SequenceStepType, type SequenceStatus,
  type SequenceEnrollment,
} from '@/app/api';
import type { Lead } from '@/app/api/types';
import { WriteOnly } from '@/app/components/WriteOnly';

const STEP_META: Record<SequenceStepType, { icon: React.ElementType; label: string; tone: string }> = {
  email: { icon: Mail, label: 'Email', tone: 'bg-blue-100 text-blue-700' },
  call: { icon: Phone, label: 'Call', tone: 'bg-teal-100 text-teal-700' },
  task: { icon: CheckSquare, label: 'Task', tone: 'bg-orange-100 text-orange-700' },
  wait: { icon: Clock, label: 'Wait', tone: 'bg-slate-100 text-slate-600' },
};

const STATUS_TONE: Record<SequenceStatus, string> = {
  active: 'bg-emerald-100 text-emerald-700 border-emerald-200',
  paused: 'bg-amber-100 text-amber-700 border-amber-200',
  draft: 'bg-slate-100 text-slate-600 border-slate-200',
};

type DraftStep = Omit<SequenceStep, 'id'>;

const emptyStep = (type: SequenceStepType = 'email'): DraftStep => ({
  order: 0, type, dayOffset: type === 'email' ? 0 : 2, subject: '', body: '', taskTitle: '', note: '',
});

export default function Sequences() {
  const [sequences, setSequences] = useState<Sequence[]>([]);
  const [enrollmentsBySeq, setEnrollmentsBySeq] = useState<Record<string, SequenceEnrollment[]>>({});
  const [loading, setLoading] = useState(true);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<Sequence | null>(null);
  const [enrollFor, setEnrollFor] = useState<Sequence | null>(null);
  const [expanded, setExpanded] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    try {
      const [seqs, enrolls] = await Promise.all([getSequences(), getEnrollments()]);
      setSequences(seqs);
      const grouped: Record<string, SequenceEnrollment[]> = {};
      for (const e of enrolls) (grouped[e.sequenceId] ??= []).push(e);
      setEnrollmentsBySeq(grouped);
    } catch {
      toast.error('Failed to load sequences');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { void load(); }, []);

  const openNew = () => { setEditing(null); setEditorOpen(true); };
  const openEdit = (s: Sequence) => { setEditing(s); setEditorOpen(true); };

  const toggleStatus = async (s: Sequence) => {
    const next: SequenceStatus = s.status === 'active' ? 'paused' : 'active';
    setSequences((prev) => prev.map((x) => (x.id === s.id ? { ...x, status: next } : x)));
    const ok = await updateSequence(s.id, { status: next });
    if (!ok) { toast.error('Failed to update'); void load(); }
  };

  const handleDelete = async (s: Sequence) => {
    if (!confirm(`Delete sequence "${s.name}"? Enrollments will be removed.`)) return;
    setSequences((prev) => prev.filter((x) => x.id !== s.id));
    const ok = await deleteSequence(s.id);
    if (ok) toast.success('Sequence deleted'); else { toast.error('Failed to delete'); void load(); }
  };

  const activeCount = (id: string) => (enrollmentsBySeq[id] ?? []).filter((e) => e.status === 'active').length;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Workflow}
            iconGradient="from-indigo-500 to-violet-500"
            title="Sequences"
            subtitle="Automated, multi-step outreach cadences for leads and contacts."
            actions={
              <WriteOnly>
                <Button onClick={openNew} className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-indigo-500 to-violet-500 hover:from-indigo-600 hover:to-violet-600 shadow-lg shadow-indigo-500/30 font-semibold text-white">
                  <Plus className="h-4 w-4" /> New sequence
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Sequences', value: sequences.length, icon: Workflow, tone: 'indigo' },
              { label: 'Active', value: sequences.filter((s) => s.status === 'active').length, icon: Play, tone: 'emerald' },
              { label: 'Active enrollments', value: Object.values(enrollmentsBySeq).flat().filter((e) => e.status === 'active').length, icon: Users, tone: 'blue' },
            ]}
          />

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400"><Loader2 className="h-6 w-6 animate-spin" /></div>
          ) : sequences.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-slate-200 bg-white py-16 text-center">
              <Workflow className="mx-auto h-10 w-10 text-slate-300" />
              <p className="mt-3 text-sm font-medium text-slate-700">No sequences yet</p>
              <p className="mt-1 text-sm text-slate-500">Create a cadence to start automating follow-ups.</p>
              <WriteOnly>
                <Button onClick={openNew} className="mt-4 gap-1.5"><Plus className="h-4 w-4" /> New sequence</Button>
              </WriteOnly>
            </div>
          ) : (
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              {sequences.map((s) => {
                const enrollments = enrollmentsBySeq[s.id] ?? [];
                const isOpen = expanded === s.id;
                return (
                  <div key={s.id} className="flex flex-col rounded-2xl border border-slate-200 bg-white shadow-sm">
                    <div className="border-b border-slate-100 p-4">
                      <div className="flex items-start justify-between gap-2">
                        <h3 className="font-semibold text-slate-900">{s.name}</h3>
                        <Badge variant="outline" className={cn('shrink-0 capitalize', STATUS_TONE[s.status])}>{s.status}</Badge>
                      </div>
                      {s.description && <p className="mt-1 line-clamp-2 text-sm text-slate-500">{s.description}</p>}
                    </div>
                    <div className="flex items-center gap-4 px-4 py-3 text-xs text-slate-500">
                      <span className="inline-flex items-center gap-1"><Workflow className="h-3.5 w-3.5" /> {s.steps.length} steps</span>
                      <button type="button" onClick={() => setExpanded(isOpen ? null : s.id)} className="inline-flex items-center gap-1 hover:text-indigo-600">
                        <Users className="h-3.5 w-3.5" /> {activeCount(s.id)} active
                      </button>
                    </div>
                    {/* Step preview */}
                    <div className="flex flex-wrap gap-1.5 px-4 pb-3">
                      {s.steps.map((st) => {
                        const M = STEP_META[st.type];
                        return (
                          <span key={st.id} className={cn('inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[11px] font-medium', M.tone)}>
                            <M.icon className="h-3 w-3" /> {st.dayOffset > 0 ? `+${st.dayOffset}d` : 'Day 0'}
                          </span>
                        );
                      })}
                    </div>
                    {isOpen && (
                      <div className="border-t border-slate-100 px-4 py-3">
                        <div className="mb-2 flex items-center justify-between">
                          <span className="text-xs font-semibold uppercase tracking-wider text-slate-400">Enrollments</span>
                          <Button size="sm" variant="outline" className="h-7 gap-1 text-xs" onClick={() => setEnrollFor(s)}>
                            <Plus className="h-3 w-3" /> Enroll
                          </Button>
                        </div>
                        {enrollments.length === 0 ? (
                          <p className="py-2 text-xs text-slate-400">No one enrolled yet.</p>
                        ) : (
                          <ul className="space-y-1.5">
                            {enrollments.map((e) => (
                              <li key={e.id} className="flex items-center justify-between gap-2 text-sm">
                                <span className="min-w-0 truncate text-slate-700">{e.targetName}</span>
                                <span className="flex items-center gap-1">
                                  <Badge variant="outline" className="capitalize">{e.status}</Badge>
                                  <button
                                    type="button"
                                    title={e.status === 'paused' ? 'Resume' : 'Pause'}
                                    onClick={async () => { await setEnrollmentStatus(e.id, e.status === 'paused' ? 'active' : 'paused'); void load(); }}
                                    className="rounded p-1 text-slate-400 hover:bg-slate-100 hover:text-slate-700"
                                  >
                                    {e.status === 'paused' ? <Play className="h-3.5 w-3.5" /> : <Pause className="h-3.5 w-3.5" />}
                                  </button>
                                  <button
                                    type="button" title="Remove"
                                    onClick={async () => { await unenroll(e.id); void load(); }}
                                    className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600"
                                  >
                                    <X className="h-3.5 w-3.5" />
                                  </button>
                                </span>
                              </li>
                            ))}
                          </ul>
                        )}
                      </div>
                    )}
                    {/* Card actions */}
                    <WriteOnly>
                      <div className="mt-auto flex items-center gap-1 border-t border-slate-100 p-2">
                        <Button size="sm" variant="ghost" className="flex-1 gap-1 text-xs" onClick={() => toggleStatus(s)}>
                          {s.status === 'active' ? <><Pause className="h-3.5 w-3.5" /> Pause</> : <><Play className="h-3.5 w-3.5" /> Activate</>}
                        </Button>
                        <Button size="sm" variant="ghost" className="flex-1 gap-1 text-xs" onClick={() => openEdit(s)}>
                          <Pencil className="h-3.5 w-3.5" /> Edit
                        </Button>
                        <Button size="sm" variant="ghost" className="gap-1 text-xs text-red-600 hover:bg-red-50 hover:text-red-700" onClick={() => handleDelete(s)}>
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </WriteOnly>
                  </div>
                );
              })}
            </div>
          )}
        </main>
      </PageTransition>

      {editorOpen && (
        <SequenceEditorDialog
          open={editorOpen}
          onOpenChange={setEditorOpen}
          sequence={editing}
          onSaved={() => { setEditorOpen(false); void load(); }}
        />
      )}
      {enrollFor && (
        <EnrollDialog
          sequence={enrollFor}
          onOpenChange={(o) => { if (!o) setEnrollFor(null); }}
          onEnrolled={() => { setEnrollFor(null); void load(); }}
        />
      )}
    </div>
  );
}

// ---------- Editor ----------

function SequenceEditorDialog({ open, onOpenChange, sequence, onSaved }: {
  open: boolean; onOpenChange: (o: boolean) => void; sequence: Sequence | null; onSaved: () => void;
}) {
  const [name, setName] = useState(sequence?.name ?? '');
  const [description, setDescription] = useState(sequence?.description ?? '');
  const [status, setStatus] = useState<SequenceStatus>(sequence?.status ?? 'draft');
  const [steps, setSteps] = useState<DraftStep[]>(
    sequence?.steps.map(({ id: _id, ...rest }) => rest) ?? [emptyStep('email')],
  );
  const [saving, setSaving] = useState(false);

  const updateStep = (i: number, patch: Partial<DraftStep>) =>
    setSteps((prev) => prev.map((s, idx) => (idx === i ? { ...s, ...patch } : s)));
  const addStep = () => setSteps((prev) => [...prev, emptyStep('email')]);
  const removeStep = (i: number) => setSteps((prev) => prev.filter((_, idx) => idx !== i));
  const move = (i: number, dir: -1 | 1) => setSteps((prev) => {
    const j = i + dir;
    if (j < 0 || j >= prev.length) return prev;
    const next = [...prev];
    const tmp = next[i]!;
    next[i] = next[j]!;
    next[j] = tmp;
    return next;
  });

  const save = async () => {
    if (!name.trim()) { toast.error('Name is required'); return; }
    if (steps.length === 0) { toast.error('Add at least one step'); return; }
    setSaving(true);
    try {
      const payload = { name: name.trim(), description: description.trim() || undefined, status, steps: steps.map((s, i) => ({ ...s, order: i })) };
      const res = sequence ? await updateSequence(sequence.id, payload) : await createSequence(payload);
      if (!res) { toast.error('Failed to save'); return; }
      toast.success(sequence ? 'Sequence updated' : 'Sequence created');
      onSaved();
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[640px] max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>{sequence ? 'Edit sequence' : 'New sequence'}</DialogTitle>
          <DialogDescription>Define the steps that run after a lead or contact is enrolled.</DialogDescription>
        </DialogHeader>

        <div className="space-y-3">
          <div className="grid grid-cols-[1fr_auto] gap-3">
            <div>
              <Label htmlFor="seq-name">Name</Label>
              <Input id="seq-name" value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. New lead outreach" className="mt-1" />
            </div>
            <div>
              <Label htmlFor="seq-status">Status</Label>
              <Select value={status} onValueChange={(v) => setStatus(v as SequenceStatus)}>
                <SelectTrigger id="seq-status" className="mt-1 w-32"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="draft">Draft</SelectItem>
                  <SelectItem value="active">Active</SelectItem>
                  <SelectItem value="paused">Paused</SelectItem>
                </SelectContent>
              </Select>
            </div>
          </div>
          <div>
            <Label htmlFor="seq-desc">Description</Label>
            <Input id="seq-desc" value={description} onChange={(e) => setDescription(e.target.value)} placeholder="Optional" className="mt-1" />
          </div>

          <div className="pt-1">
            <Label>Steps</Label>
            <div className="mt-1.5 space-y-2">
              {steps.map((st, i) => (
                <div key={i} className="rounded-xl border border-slate-200 bg-slate-50/60 p-3">
                  <div className="flex items-center gap-2">
                    <span className="text-xs font-semibold text-slate-400">#{i + 1}</span>
                    <Select value={st.type} onValueChange={(v) => updateStep(i, { type: v as SequenceStepType })}>
                      <SelectTrigger className="h-8 w-28 bg-white"><SelectValue /></SelectTrigger>
                      <SelectContent>
                        <SelectItem value="email">Email</SelectItem>
                        <SelectItem value="call">Call</SelectItem>
                        <SelectItem value="task">Task</SelectItem>
                        <SelectItem value="wait">Wait</SelectItem>
                      </SelectContent>
                    </Select>
                    <div className="flex items-center gap-1 text-sm text-slate-500">
                      <span>after</span>
                      <Input
                        type="number" min={0} value={st.dayOffset}
                        onChange={(e) => updateStep(i, { dayOffset: Math.max(0, parseInt(e.target.value, 10) || 0) })}
                        className="h-8 w-16 bg-white"
                      />
                      <span>days</span>
                    </div>
                    <div className="ml-auto flex items-center gap-0.5">
                      <button type="button" onClick={() => move(i, -1)} disabled={i === 0} className="rounded p-1 text-slate-400 hover:bg-white disabled:opacity-30"><ArrowUp className="h-3.5 w-3.5" /></button>
                      <button type="button" onClick={() => move(i, 1)} disabled={i === steps.length - 1} className="rounded p-1 text-slate-400 hover:bg-white disabled:opacity-30"><ArrowDown className="h-3.5 w-3.5" /></button>
                      <button type="button" onClick={() => removeStep(i)} className="rounded p-1 text-slate-400 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>
                    </div>
                  </div>
                  {st.type === 'email' && (
                    <div className="mt-2 space-y-2">
                      <Input value={st.subject ?? ''} onChange={(e) => updateStep(i, { subject: e.target.value })} placeholder="Subject" className="h-8 bg-white" />
                      <Textarea value={st.body ?? ''} onChange={(e) => updateStep(i, { body: e.target.value })} placeholder="Email body — use {{firstName}}, {{company}}" rows={3} className="resize-none bg-white" />
                    </div>
                  )}
                  {(st.type === 'task' || st.type === 'call') && (
                    <Input value={st.taskTitle ?? ''} onChange={(e) => updateStep(i, { taskTitle: e.target.value })} placeholder={st.type === 'call' ? 'Call objective' : 'Task title'} className="mt-2 h-8 bg-white" />
                  )}
                  {st.type === 'wait' && (
                    <p className="mt-2 text-xs text-slate-400">Pauses the cadence for the days set above.</p>
                  )}
                </div>
              ))}
            </div>
            <Button type="button" variant="outline" size="sm" onClick={addStep} className="mt-2 gap-1.5"><Plus className="h-4 w-4" /> Add step</Button>
          </div>
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={saving}>Cancel</Button>
          <Button onClick={save} disabled={saving} className="gap-1.5">
            {saving && <Loader2 className="h-4 w-4 animate-spin" />} {sequence ? 'Save changes' : 'Create sequence'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------- Enroll ----------

function EnrollDialog({ sequence, onOpenChange, onEnrolled }: {
  sequence: Sequence; onOpenChange: (o: boolean) => void; onEnrolled: () => void;
}) {
  const [leads, setLeads] = useState<Lead[]>([]);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState('');
  const [enrolling, setEnrolling] = useState<string | null>(null);

  useEffect(() => {
    getLeads().then((l) => setLeads(Array.isArray(l) ? l : [])).catch(() => setLeads([])).finally(() => setLoading(false));
  }, []);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    const base = q ? leads.filter((l) => l.name.toLowerCase().includes(q) || l.email.toLowerCase().includes(q)) : leads;
    return base.slice(0, 50);
  }, [leads, query]);

  const enroll = async (lead: Lead) => {
    setEnrolling(lead.id);
    try {
      const res = await enrollInSequence({
        sequenceId: sequence.id, targetType: 'lead', targetId: lead.id, targetName: lead.name, targetEmail: lead.email,
      });
      if (res) { toast.success(`${lead.name} enrolled in "${sequence.name}"`); onEnrolled(); }
      else toast.error('Failed to enroll');
    } finally {
      setEnrolling(null);
    }
  };

  return (
    <Dialog open onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[480px]">
        <DialogHeader>
          <DialogTitle>Enroll a lead</DialogTitle>
          <DialogDescription>Add a lead to “{sequence.name}”.</DialogDescription>
        </DialogHeader>
        <Input value={query} onChange={(e) => setQuery(e.target.value)} placeholder="Search leads…" autoFocus />
        <div className="max-h-[320px] overflow-y-auto">
          {loading ? (
            <div className="py-8 text-center text-slate-400"><Loader2 className="mx-auto h-5 w-5 animate-spin" /></div>
          ) : filtered.length === 0 ? (
            <p className="py-8 text-center text-sm text-slate-400">No leads found.</p>
          ) : (
            <ul className="divide-y divide-slate-50">
              {filtered.map((l) => (
                <li key={l.id} className="flex items-center justify-between gap-2 py-2">
                  <span className="min-w-0">
                    <span className="block truncate text-sm font-medium text-slate-800">{l.name}</span>
                    <span className="block truncate text-xs text-slate-500">{l.email}</span>
                  </span>
                  <Button size="sm" variant="outline" disabled={enrolling === l.id} onClick={() => enroll(l)} className="gap-1">
                    {enrolling === l.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Plus className="h-3.5 w-3.5" />} Enroll
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}

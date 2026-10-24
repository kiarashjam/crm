import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useLocation, Link } from 'react-router-dom';
import { AnimatePresence, motion } from 'motion/react';
import {
  ArrowLeft, Mail, Phone, Building2, Sparkles, Trash2,
  User as UserIcon, CheckCircle2, Loader2, Pencil, Plus, Calendar,
  MessageSquarePlus, FileText, Clock, UserPlus, Copy, Check, AlertCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import { ApiError } from '@/app/api/apiClient';
import { NextActionsSlot } from '@/app/salesAgent/NextActionsSlot';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getLeadById, updateLead, assignLead, deleteLead,
  getCompanies, getContacts, getLeadStatuses, getLeadSources,
  getActivitiesByLead, createActivity,
  getTasksByLead, createTask, updateTask, deleteTask,
  setLeadReferral,
  messages,
} from '@/app/api';
import type { Lead, Company, Contact, LeadStatus, LeadSource, Activity, TaskItem } from '@/app/api/types';
import { getOrgMembers, type OrgMemberDto } from '@/app/api/organizations';
import { LeadStatusTimeline } from './leads/components/LeadStatusTimeline';
import { useOrg } from '@/app/contexts/OrgContext';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import { useMotionPreference } from '@/app/hooks/useMotionPreference';
import { Button } from '@/app/components/ui/button';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import { DerivedStatusBadge } from './leads/components/DerivedStatusBadge';
import { LeadDetailToolbar } from './leads/components/LeadDetailToolbar';
import { QuickLogPopover } from './leads/components/QuickLogPopover';
import { LeadPipelineTracker } from './leads/components/LeadPipelineTracker';
import { ContractPanel } from './contracts/ContractPanel';
import { parsePipeline, serializePipeline, type LeadPipeline } from './leads/leadPipeline';
import { useLeadStatusSync } from './leads/useLeadStatusSync';
import type { StatusDrift, SuggestReason } from './leads/leadStatusSync';
import { StatusSyncStrip } from './leads/components/StatusSyncStrip';
import { AddLeadDialog } from './leads/AddLeadDialog';
import { FALLBACK_STATUSES, FALLBACK_SOURCES, LIFECYCLE_STAGES, EMPTY_LEAD_FORM } from './leads/config';
import { isValidGuid } from './leads/utils';
import { loadLeadAssignments, setLeadAssignment } from './leads/leadAssignmentStore';
import type { LeadForm } from './leads/types';
import { InlineField } from './leads/detail/InlineField';
import { ActivityTimeline } from './leads/detail/ActivityTimeline';
import { ScoreGauge } from './leads/detail/ScoreGauge';
import { SalesTrackerCard } from './leads/detail/SalesTrackerCard';
import { ReferralCard } from './leads/detail/ReferralCard';
import AiNextActionCard from '@/app/components/AiNextActionCard';
import SaveLeadAsContactDialog from '@/app/components/SaveLeadAsContactDialog';
import CustomFieldsCard from '@/app/components/CustomFieldsCard';
import AttachmentsCard from '@/app/components/AttachmentsCard';
import { Confetti } from '@/app/components/Confetti';
import EmailComposerDialog from '@/app/components/EmailComposerDialog';
import { usePageLoading } from '@/app/components/LoadingOverlay';

type Tab = 'activity' | 'tasks' | 'notes';

/** One easing for the page: a quick start that settles, so motion reads as arrival, not travel. */
const EASE = [0.16, 1, 0.3, 1] as const;

const STATUS_GRADIENTS: Record<string, string> = {
  New: 'from-blue-500 via-indigo-500 to-cyan-400',
  Contacted: 'from-amber-500 via-orange-500 to-rose-400',
  Qualified: 'from-emerald-500 via-teal-500 to-cyan-400',
  Lost: 'from-slate-500 via-slate-400 to-slate-300',
};

function initialsOf(name: string): string {
  return name.split(' ').map((n) => n[0]).slice(0, 2).join('').toUpperCase();
}

function formatRelative(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  const now = Date.now();
  const diff = now - d.getTime();
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 7) return `${days}d ago`;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

function formatFull(iso?: string): string | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return d.toLocaleString(undefined, { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' });
}

/** A due date is overdue when it's in the past (used to flag open tasks). */
function isOverdue(iso?: string): boolean {
  if (!iso) return false;
  const t = Date.parse(iso);
  return !Number.isNaN(t) && t < Date.now();
}

const OUTREACH_ACTIVITY_TYPES = new Set(['call', 'email', 'meeting']);

/** The referrer fields of a saved lead, so a merge can take them even when they were cleared. */
function pickReferral(lead: Lead): Pick<Lead, 'referredByContactId' | 'referredByContactName'> {
  return { referredByContactId: lead.referredByContactId, referredByContactName: lead.referredByContactName };
}

export default function LeadDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { currentOrgId, isReadOnly } = useOrg();
  // Less motion when the OS or Settings asks for it: things appear instead of moving.
  const reduce = useMotionPreference();
  const rise = (delay = 0) => (reduce
    ? { initial: false as const }
    : { initial: { opacity: 0, y: 12 }, animate: { opacity: 1, y: 0 }, transition: { duration: 0.45, ease: EASE, delay } });
  // Sidebar cards arrive one after another, from the right.
  const card = (index: number) => (reduce
    ? { initial: false as const }
    : { initial: { opacity: 0, x: 18 }, animate: { opacity: 1, x: 0 }, transition: { duration: 0.4, ease: EASE, delay: 0.16 + index * 0.06 } });

  // When the user arrives from the leads list, Leads.tsx stashes the source
  // URL (including filters/sort) here so Back / breadcrumb can return there
  // intact. Direct deep-links won't have it; fall back to the bare list.
  const backUrl = (location.state as { from?: string } | null)?.from ?? '/leads';

  // Core data
  const [lead, setLead] = useState<Lead | null>(null);
  useDocumentTitle(lead?.name, 'Leads');
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [notFound, setNotFound] = useState(false);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [statuses, setStatuses] = useState<LeadStatus[]>([]);
  // Auto status sync must not run against FALLBACK_STATUSES while the org's real
  // list is still in flight — it could write a status this org does not have.
  const [statusesLoaded, setStatusesLoaded] = useState(false);
  // "Not loaded" has two causes and only one of them ever resolves. An org with
  // no statuses configured (or a failing request) leaves the gate shut forever,
  // and every pipeline edit then skipped the status write in total silence —
  // which reads as the whole feature being broken. Tracked separately so the UI
  // can say which of the two it is.
  const [statusesUnavailable, setStatusesUnavailable] = useState(false);
  const [sources, setSources] = useState<LeadSource[]>([]);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [orgMembers, setOrgMembers] = useState<OrgMemberDto[]>([]);

  // UI
  const [tab, setTab] = useState<Tab>('activity');
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  // Full "Edit lead" dialog — reuses the same multi-step editor as the leads
  // list so every field (incl. Referred by) can be edited from this page.
  const [editOpen, setEditOpen] = useState(false);
  const [emailOpen, setEmailOpen] = useState(false);
  const [saveContactOpen, setSaveContactOpen] = useState(false);
  const [editForm, setEditForm] = useState<LeadForm>(EMPTY_LEAD_FORM);
  const [savingEdit, setSavingEdit] = useState(false);

  // Transient "copied!" feedback for the contact quick-copy buttons.
  const [copiedField, setCopiedField] = useState<string | null>(null);

  // Quick-log + email composer + task input
  const [noteDraft, setNoteDraft] = useState('');
  const [savingNote, setSavingNote] = useState(false);
  const [emailSubject, setEmailSubject] = useState('');
  const [emailBody, setEmailBody] = useState('');
  const [sendingEmail, setSendingEmail] = useState(false);
  const [newTaskTitle, setNewTaskTitle] = useState('');
  const [newTaskDue, setNewTaskDue] = useState('');
  const [creatingTask, setCreatingTask] = useState(false);
  // The task just ticked off, so its box can throw a little confetti.
  const [taskBurst, setTaskBurst] = useState<{ id: string; at: number } | null>(null);

  // ----- Initial load -----
  // Each id-keyed effect uses a `cancelled` flag so a quick A → B navigation
  // can't let A's response stomp B's data. State for the previous lead is
  // also reset eagerly so the page never briefly mixes A's lead with B's tasks.
  useEffect(() => {
    if (!id) return;
    setLoading(true);
    setNotFound(false);
    setLead(null);
    setActivities([]);
    setTasks([]);
    let cancelled = false;
    (async () => {
      try {
        const found = await getLeadById(id);
        if (cancelled) return;
        if (!found) {
          setNotFound(true);
        } else {
          // An owner saved only in this browser, from before owners were shared.
          const ownerId = found.assignedToId || loadLeadAssignments()[found.id];
          setLead({ ...found, assignedToId: ownerId || undefined });
        }
      } catch {
        if (!cancelled) setNotFound(true);
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => { cancelled = true; };
  }, [id]);

  // Static lookup data (don't block first paint of lead detail).
  useEffect(() => {
    let cancelled = false;
    getCompanies().then((v) => { if (!cancelled) setCompanies(v); }).catch(() => { if (!cancelled) setCompanies([]); });
    getContacts().then((v) => { if (!cancelled) setContacts(v); }).catch(() => { if (!cancelled) setContacts([]); });
    getLeadStatuses()
      .then((v) => {
        if (cancelled) return;
        setStatuses(v);
        // Only open the auto-sync gate once we hold the org's OWN list. On a
        // failure or an empty result `statusOptions` falls back to
        // FALLBACK_STATUSES, which is precisely the case this gate exists to
        // keep auto-sync away from — so it stays shut.
        const ready = Array.isArray(v) && v.length > 0;
        setStatusesLoaded(ready);
        setStatusesUnavailable(!ready);
      })
      .catch(() => {
        if (!cancelled) { setStatuses([]); setStatusesLoaded(false); setStatusesUnavailable(true); }
      });
    getLeadSources().then((v) => { if (!cancelled) setSources(v); }).catch(() => { if (!cancelled) setSources([]); });
    return () => { cancelled = true; };
  }, []);
  useEffect(() => {
    if (!currentOrgId) { setOrgMembers([]); return; }
    let cancelled = false;
    getOrgMembers(currentOrgId)
      .then((v) => { if (!cancelled) setOrgMembers(v); })
      .catch(() => { if (!cancelled) setOrgMembers([]); });
    return () => { cancelled = true; };
  }, [currentOrgId]);

  // Activities + tasks for the current lead, with the same cancellation pattern.
  useEffect(() => {
    if (!id) return;
    let cancelled = false;
    getActivitiesByLead(id)
      .then((v) => { if (!cancelled) setActivities(v); })
      .catch(() => { if (!cancelled) setActivities([]); });
    getTasksByLead(id)
      .then((v) => { if (!cancelled) setTasks(v); })
      .catch(() => { if (!cancelled) setTasks([]); });
    return () => { cancelled = true; };
  }, [id]);

  // ----- Derived -----
  const statusOptions = useMemo(
    () => (statuses.length > 0 ? statuses.map((s) => ({ id: s.id, name: s.name })) : FALLBACK_STATUSES.map((n) => ({ id: n, name: n }))),
    [statuses],
  );
  const sourceOptions = useMemo(
    () => (sources.length > 0 ? sources : FALLBACK_SOURCES.map((n) => ({ id: n, name: n, organizationId: '', displayOrder: 0 }))),
    [sources],
  );
  const company = useMemo(() => companies.find((c) => c.id === lead?.companyId) ?? null, [companies, lead?.companyId]);
  const assignee = useMemo(
    () => (lead?.assignedToId ? orgMembers.find((m) => m.userId === lead.assignedToId) ?? null : null),
    [orgMembers, lead?.assignedToId],
  );
  const referredContact = useMemo(
    () => (lead?.referredByContactId ? contacts.find((c) => c.id === lead.referredByContactId) ?? null : null),
    [contacts, lead?.referredByContactId],
  );
  const referredByName = referredContact?.name ?? lead?.referredByContactName ?? null;

  // Tasks ordered for action: open tasks first (soonest due first, undated
  // last), completed tasks sink to the bottom.
  const sortedTasks = useMemo(() => {
    const doneRank = (t: TaskItem) => (t.status === 'completed' ? 1 : 0);
    return [...tasks].sort((a, b) => {
      if (doneRank(a) !== doneRank(b)) return doneRank(a) - doneRank(b);
      const ad = a.dueDateUtc ? Date.parse(a.dueDateUtc) : Number.POSITIVE_INFINITY;
      const bd = b.dueDateUtc ? Date.parse(b.dueDateUtc) : Number.POSITIVE_INFINITY;
      return ad - bd;
    });
  }, [tasks]);
  // The single most urgent open task — surfaced as the "Next step".
  const nextTask = useMemo(
    () => sortedTasks.find((t) => t.status !== 'completed') ?? null,
    [sortedTasks],
  );

  const gradient = lead && (lead.isConverted ? 'from-emerald-600 via-teal-500 to-cyan-400' : STATUS_GRADIENTS[lead.status] || 'from-slate-500 via-slate-400 to-slate-300');

  // ----- Write actions -----

  /**
   * Save one or more fields. Wraps updateLead, handles the GUID-only
   * leadStatusId/leadSourceId trap, and emits an optional system activity.
   */
  const patchLead = useCallback(async (patch: Record<string, unknown>, log?: { subject: string; body?: string }): Promise<boolean> => {
    if (!lead) return false;
    try {
      const updated = await updateLead(lead.id, patch);
      if (!updated) {
        toast.error('Failed to save');
        return false;
      }
      setLead((prev) => (prev ? {
        ...prev,
        ...updated,
        assignedToId: updated.assignedToId ?? prev.assignedToId,
      } : prev));
      if (log) {
        createActivity({ type: 'system', subject: log.subject, body: log.body, leadId: lead.id })
          .then((a) => a && setActivities((prev) => [a, ...prev]))
          .catch(() => { /* non-fatal */ });
      }
      return true;
    } catch (err) {
      console.error('updateLead failed', err);
      toast.error(err instanceof ApiError && err.message ? err.message : 'Failed to save');
      return false;
    }
  }, [lead]);

  // Structured 5-phase lifecycle (outreach → meeting → contract → signature →
  // deposit), persisted as JSON on the lead so the whole org shares one view.
  const pipeline = useMemo<LeadPipeline>(() => parsePipeline(lead?.pipelineState), [lead?.pipelineState]);

  // Keeps lead.status honest as the pipeline advances.
  const statusSync = useLeadStatusSync({
    statusOptions: useMemo(
      () => statusOptions.map((s) => ({
        id: s.id,
        name: s.name,
        displayOrder: statuses.find((x) => x.id === s.id)?.displayOrder,
      })),
      [statusOptions, statuses],
    ),
    statusesLoaded,
    statusesUnavailable,
  });

  // Memoised so the strip does not re-read localStorage on every render.
  const statusSyncView = useMemo<{
    drift: StatusDrift | null;
    reasons: SuggestReason[] | undefined;
  }>(() => {
    if (!lead) return { drift: null, reasons: undefined };
    const p = statusSync.plan(lead, pipeline);
    return {
      drift: statusSync.drift(lead, pipeline),
      reasons: p.kind === 'suggest' ? p.reasons : undefined,
    };
  }, [lead, pipeline, statusSync]);

  const savePipeline = useCallback(
    (next: LeadPipeline, log?: { subject: string; body?: string }) => {
      if (!lead) return;
      const json = serializePipeline(next);
      // Snapshot for rollback: without this a rejected PUT leaves the tracker
      // rendering a phase the server refused, and the next click builds its
      // patch from that phantom base.
      const prevPipelineState = lead.pipelineState;
      const prevStatus = lead.status;
      const prevStatusId = lead.leadStatusId;

      // Optimistic so the tracker reflects the choice instantly.
      setLead((prev) => (prev ? { ...prev, pipelineState: json } : prev));

      void statusSync
        .save(lead, next, {
          log,
          onApplied: (updated) => {
            setLead((prev) => (prev ? {
              ...prev,
              ...updated,
              assignedToId: updated.assignedToId ?? prev.assignedToId,
            } : prev));
          },
        })
        .then((outcome) => {
          if (outcome.stale) return;
          if (!outcome.ok) {
            setLead((prev) => (prev ? {
              ...prev,
              pipelineState: prevPipelineState,
              status: prevStatus,
              leadStatusId: prevStatusId,
            } : prev));
            return;
          }
          // Refresh the timeline whenever an activity was written — that is any
          // edit carrying a `log`, not just one that moved the status. Gating on
          // 'apply' alone hid the pipeline-edit entry until a reload.
          if (log || outcome.plan.kind === 'apply') {
            getActivitiesByLead(lead.id).then(setActivities).catch(() => { /* non-fatal */ });
          }
        });
    },
    // `statusSync` is stable via useMemo; listing it keeps the closure honest.
    [lead, statusSync],
  );

  /**
   * Writes a status the PIPELINE derived — never one a person typed.
   *
   * The only caller is the drift strip's "bring it in line" action, which accepts
   * what the tracker already says rather than choosing something. There is no
   * longer any path by which a person picks a status directly: every route goes
   * through a pipeline step, so the badge and the tracker cannot disagree.
   */
  const applyDerivedStatus = async (status: string) => {
    if (!lead) return;
    const opt = statusOptions.find((s) => s.name === status);
    const patch: Record<string, unknown> = { status };
    if (opt && isValidGuid(opt.id)) patch.leadStatusId = opt.id;
    await patchLead(patch, {
      subject: `Status brought in line with the pipeline: ${status}`,
      body: `From "${lead.status}" to "${status}"`,
    });
  };

  const setSource = async (source: string) => {
    if (!lead) return;
    const opt = sourceOptions.find((s) => s.name === source);
    const patch: Record<string, unknown> = { source };
    if (opt && isValidGuid(opt.id)) patch.leadSourceId = opt.id;
    await patchLead(patch, { subject: 'Source updated', body: `Now ${source}` });
  };

  const setLifecycle = (stage: string) =>
    patchLead({ lifecycleStage: stage }, { subject: 'Lifecycle stage updated', body: stage });

  const setAssignment = (userId: string) => {
    if (!lead) return;
    const next = userId || undefined;
    const m = orgMembers.find((x) => x.userId === userId);
    // Persist server-side so the whole org sees the same owner. The local store
    // is kept as a demo-mode / offline fallback and mirrors the optimistic state.
    setLeadAssignment(lead.id, next);
    setLead((prev) => (prev ? { ...prev, assignedToId: next } : prev));
    assignLead(lead.id, next ?? null)
      .then((updated) => {
        if (!updated) toast.error('Failed to save assignment');
      })
      .catch(() => toast.error('Failed to save assignment'));
    createActivity({ type: 'system', subject: m ? `Assigned to ${m.name}` : 'Unassigned', leadId: lead.id })
      .then((a) => a && setActivities((prev) => [a, ...prev]))
      .catch(() => { /* non-fatal */ });
    toast.success(m ? `Assigned to ${m.name}` : 'Unassigned');
  };

  const setCompanyId = (companyId: string | '') => {
    const next = companyId ? companyId : undefined;
    const c = companies.find((x) => x.id === companyId);
    return patchLead({ companyId: next }, { subject: c ? `Linked to ${c.name}` : 'Company cleared' });
  };

  // Inline-edit savers for text fields — return boolean for InlineField.
  const saveName = (v: string) => patchLead({ name: v }, { subject: 'Name updated' });
  const saveEmail = (v: string) => patchLead({ email: v }, { subject: 'Email updated' });
  const savePhone = (v: string) => patchLead({ phone: v }, { subject: 'Phone updated' });
  const saveDescription = (v: string) => patchLead({ description: v }, { subject: 'Description updated' });
  const saveScore = async (v: string): Promise<boolean> => {
    const n = parseInt(v, 10);
    if (Number.isNaN(n) || n < 0 || n > 100) {
      toast.error('Score must be 0–100');
      return false;
    }
    return patchLead({ leadScore: n }, { subject: `Score set to ${n}` });
  };

  // Logging an outreach touch (call/email/meeting) advances "Last contacted"
  // so the hero metric reflects reality. Optimistic locally; the PUT is
  // best-effort (kept for the session even if it fails).
  const bumpLastContacted = async () => {
    if (!lead) return;
    const when = new Date().toISOString();
    setLead((prev) => (prev ? { ...prev, lastContactedAt: when } : prev));
    try {
      await updateLead(lead.id, { lastContactedAt: when });
    } catch {
      /* non-fatal */
    }
  };

  const copyToClipboard = async (text: string, label: string) => {
    try {
      await navigator.clipboard.writeText(text);
      setCopiedField(label);
      toast.success(`${label} copied`);
      window.setTimeout(() => setCopiedField((c) => (c === label ? null : c)), 1500);
    } catch {
      toast.error('Copy failed');
    }
  };

  // Quick-log via popover (from card) — also used by the inline note + email flows.
  const logActivity = async (params: { type: string; subject?: string; body: string }) => {
    if (!lead) return;
    const a = await createActivity({ ...params, leadId: lead.id });
    if (a) setActivities((prev) => [a, ...prev]);
    if (OUTREACH_ACTIVITY_TYPES.has(params.type)) void bumpLastContacted();
  };

  const handleAddNote = async () => {
    if (!lead || !noteDraft.trim() || savingNote) return;
    setSavingNote(true);
    try {
      const a = await createActivity({ type: 'note', body: noteDraft.trim(), leadId: lead.id });
      if (a) {
        setActivities((prev) => [a, ...prev]);
        setNoteDraft('');
        toast.success('Note added');
      } else {
        toast.error('Failed to add note');
      }
    } finally {
      setSavingNote(false);
    }
  };

  const handleSendEmail = async () => {
    if (!lead?.email) { toast.error('Lead has no email address'); return; }
    if (!emailBody.trim() || sendingEmail) return;
    setSendingEmail(true);
    try {
      const a = await createActivity({
        type: 'email',
        subject: emailSubject.trim() || `Email to ${lead.name}`,
        body: emailBody.trim(),
        leadId: lead.id,
      });
      if (a) setActivities((prev) => [a, ...prev]);
      void bumpLastContacted();
      const mailto = `mailto:${encodeURIComponent(lead.email)}?subject=${encodeURIComponent(emailSubject)}&body=${encodeURIComponent(emailBody)}`;
      window.location.href = mailto;
      setEmailSubject('');
      setEmailBody('');
      toast.success('Email logged and composer opened');
    } finally {
      setSendingEmail(false);
    }
  };

  const handleAddTask = async () => {
    if (!lead || !newTaskTitle.trim() || creatingTask) return;
    setCreatingTask(true);
    try {
      const t = await createTask({
        title: newTaskTitle.trim(),
        leadId: lead.id,
        dueDateUtc: newTaskDue ? new Date(newTaskDue).toISOString() : undefined,
        status: 'todo',
        priority: 'medium',
      });
      if (t) {
        setTasks((prev) => [t, ...prev]);
        setNewTaskTitle('');
        setNewTaskDue('');
        createActivity({ type: 'system', subject: 'Task created', body: t.title, leadId: lead.id })
          .then((a) => a && setActivities((prev) => [a, ...prev])).catch(() => {});
        toast.success('Task created');
      } else {
        toast.error('Failed to create task');
      }
    } finally {
      setCreatingTask(false);
    }
  };

  const toggleTask = async (task: TaskItem) => {
    const nextStatus = task.status === 'completed' ? 'todo' : 'completed';
    if (nextStatus === 'completed') setTaskBurst({ id: task.id, at: Date.now() });
    const updated = await updateTask(task.id, { status: nextStatus, completed: nextStatus === 'completed' });
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? updated : t)));
      createActivity({
        type: 'system',
        subject: nextStatus === 'completed' ? 'Task completed' : 'Task reopened',
        body: task.title,
        leadId: lead?.id,
      }).catch(() => {});
    }
  };

  const handleDeleteTask = async (task: TaskItem) => {
    const snapshot = tasks;
    setTasks((prev) => prev.filter((t) => t.id !== task.id)); // optimistic
    const ok = await deleteTask(task.id);
    if (ok) {
      toast.success('Task deleted');
      createActivity({ type: 'system', subject: 'Task deleted', body: task.title, leadId: lead?.id }).catch(() => {});
    } else {
      setTasks(snapshot); // revert
      toast.error('Failed to delete task');
    }
  };

  const handleDelete = async () => {
    if (!lead || deleting) return;
    setDeleting(true);
    try {
      const ok = await deleteLead(lead.id);
      if (ok) {
        toast.success('Lead deleted');
        navigate(backUrl);
      } else {
        toast.error('Failed to delete lead');
      }
    } finally {
      setDeleting(false);
      setDeleteOpen(false);
    }
  };

  /** Records who referred this lead for the whole team, and notes the change on its timeline. */
  const saveReferral = useCallback(async (contactId: string | null): Promise<boolean> => {
    if (!lead) return false;
    const before = lead.referredByContactName ?? contacts.find((c) => c.id === lead.referredByContactId)?.name;
    try {
      const updated = await setLeadReferral(lead.id, contactId);
      if (!updated) {
        toast.error('The referrer was not saved.');
        return false;
      }
      setLead((prev) => (prev ? { ...prev, ...pickReferral(updated) } : prev));
      const name = updated.referredByContactName ?? contacts.find((c) => c.id === contactId)?.name ?? 'the contact';
      const subject = contactId ? `Referred by ${name}` : 'Referrer removed';
      toast.success(contactId ? `${name} is saved as the referrer.` : 'Referrer removed.');
      createActivity({ type: 'system', subject, body: before ? `Was ${before}` : undefined, leadId: lead.id })
        .then((a) => a && setActivities((prev) => [a, ...prev]))
        .catch(() => { /* non-fatal */ });
      return true;
    } catch (err) {
      toast.error(err instanceof ApiError && err.message ? err.message : 'The referrer was not saved.');
      return false;
    }
  }, [lead, contacts]);

  // Open the full multi-step editor, prefilled from the current lead. Mirrors
  // the leads list's openEdit so the two stay behaviourally identical.
  const openEdit = useCallback(() => {
    if (!lead) return;
    const sourceOpt = sourceOptions.find((s) => s.id === lead.leadSourceId || s.name === lead.source);
    const statusOpt = statusOptions.find((s) => s.id === lead.leadStatusId || s.name === lead.status);
    setEditForm({
      name: lead.name,
      email: lead.email,
      phone: lead.phone ?? '',
      referredByContactId: lead.referredByContactId ?? '',
      companyId: lead.companyId ?? '',
      source: lead.source ?? (sourceOpt?.name ?? 'Manual'),
      status: lead.status,
      leadSourceId: lead.leadSourceId ?? (sourceOpt?.id ?? ''),
      leadStatusId: lead.leadStatusId ?? (statusOpt?.id ?? ''),
      leadScore: lead.leadScore?.toString() ?? '',
      description: lead.description ?? '',
      lifecycleStage: lead.lifecycleStage ?? '',
    });
    setEditOpen(true);
  }, [lead, sourceOptions, statusOptions]);

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!lead) return;
    if (!editForm.name.trim() || !editForm.email.trim()) {
      toast.error(messages.validation.nameAndEmailRequired);
      return;
    }
    setSavingEdit(true);
    try {
      // Only send leadSourceId/leadStatusId when they're real GUIDs (fallback
      // option ids are plain strings the backend would reject).
      const validLeadSourceId = isValidGuid(editForm.leadSourceId) ? editForm.leadSourceId : undefined;
      const validLeadStatusId = isValidGuid(editForm.leadStatusId) ? editForm.leadStatusId : undefined;
      const parsedScore = editForm.leadScore ? parseInt(editForm.leadScore, 10) : undefined;
      const validLeadScore = parsedScore !== undefined && !Number.isNaN(parsedScore) && parsedScore >= 0 && parsedScore <= 100 ? parsedScore : undefined;

      const updated = await updateLead(lead.id, {
        name: editForm.name.trim(),
        email: editForm.email.trim(),
        phone: editForm.phone.trim() || undefined,
        companyId: editForm.companyId || undefined,
        source: editForm.source || undefined,
        status: editForm.status,
        leadSourceId: validLeadSourceId,
        leadStatusId: validLeadStatusId,
        leadScore: validLeadScore,
        description: editForm.description.trim() || undefined,
        lifecycleStage: editForm.lifecycleStage || undefined,
      });
      if (!updated) {
        toast.error(messages.errors.generic);
        return;
      }
      // The referrer has its own endpoint: it can change on a converted lead too.
      const referredByContactId = editForm.referredByContactId || null;
      const withReferral = referredByContactId !== (lead.referredByContactId ?? null)
        ? await setLeadReferral(lead.id, referredByContactId) ?? updated
        : updated;
      setLead((prev) => (prev ? { ...prev, ...updated, ...pickReferral(withReferral), assignedToId: prev.assignedToId } : prev));
      createActivity({ type: 'system', subject: 'Lead details updated', leadId: lead.id })
        .then((a) => a && setActivities((prev) => [a, ...prev]))
        .catch(() => { /* non-fatal */ });
      toast.success(messages.success.leadUpdated);
      setEditOpen(false);
    } catch (err) {
      // The server's reason when it gave one — e.g. another open lead already has this email.
      toast.error(err instanceof ApiError && err.message ? err.message : messages.errors.generic);
    } finally {
      setSavingEdit(false);
    }
  };

  // Keyboard shortcut: `e` opens the full editor (ignored while typing in a
  // field or when a dialog is already open), matching the leads list's `n`/`/`.
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.metaKey || e.ctrlKey || e.altKey) return;
      const t = e.target as HTMLElement | null;
      const typing = !!t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.isContentEditable);
      if (typing) return;
      if (e.key.toLowerCase() === 'e' && !editOpen && !deleteOpen) {
        e.preventDefault();
        openEdit();
      }
    };
    window.addEventListener('keydown', onKeyDown);
    return () => window.removeEventListener('keydown', onKeyDown);
  }, [openEdit, editOpen, deleteOpen]);

  // ----- Render -----

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <PageTransition>
          <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
            <ContentSkeleton rows={6} />
          </main>
        </PageTransition>
      </div>
    );
  }

  if (notFound || !lead) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <PageTransition>
          <main id={MAIN_CONTENT_ID} className="flex-1 w-full flex items-center justify-center px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
            <div className="mx-auto max-w-md text-center">
              <h1 className="text-2xl font-semibold text-slate-900">Lead not found</h1>
              <p className="mt-2 text-slate-500">It may have been deleted, or you don&apos;t have access to it.</p>
              <Button asChild className="mt-6">
                <Link to={backUrl}><ArrowLeft className="w-4 h-4 mr-2" /> Back to leads</Link>
              </Button>
            </div>
          </main>
        </PageTransition>
      </div>
    );
  }

  const noteActivities = activities.filter((a) => a.type === 'note');
  const openTaskCount = tasks.filter((t) => t.status !== 'completed').length;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full" tabIndex={-1}>
          <LeadDetailToolbar
            name={lead.name}
            backUrl={backUrl}
            readOnly={isReadOnly}
            hasEmail={Boolean(lead.email)}
            converted={Boolean(lead.isConverted)}
            onBack={() => navigate(backUrl)}
            onEmail={() => setEmailOpen(true)}
            onEdit={openEdit}
            onSaveContact={() => setSaveContactOpen(true)}
            onConvert={() => navigate(`/leads?convertLeadId=${lead.id}`)}
            onDelete={() => setDeleteOpen(true)}
          />

          {/* Hero: the one full-width band. Who the person is, how to reach them, and the
              numbers that matter, in one compact strip. Everything below sits in two columns. */}
          <section className="relative isolate overflow-hidden border-b border-slate-200/70 bg-gradient-to-br from-white via-slate-50/70 to-white">
            <motion.div
              aria-hidden
              className={cn('pointer-events-none absolute -top-28 right-[8%] h-72 w-72 rounded-full bg-gradient-to-br opacity-30 blur-3xl', gradient)}
              animate={reduce ? undefined : { x: [0, -40, 0], y: [0, 18, 0], scale: [1, 1.08, 1] }}
              transition={{ duration: 16, repeat: Infinity, ease: 'easeInOut' }}
            />
            <motion.div
              aria-hidden
              className="pointer-events-none absolute -bottom-32 left-[12%] h-72 w-72 rounded-full bg-gradient-to-br from-fuchsia-300/30 via-violet-300/25 to-sky-300/20 blur-3xl"
              animate={reduce ? undefined : { x: [0, 36, 0], y: [0, -14, 0] }}
              transition={{ duration: 19, repeat: Infinity, ease: 'easeInOut' }}
            />
            <div aria-hidden className="pointer-events-none absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-fuchsia-300/60 to-transparent" />

            <div className="relative w-full px-[var(--page-padding)] py-5">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-center xl:justify-between xl:gap-8">
                <div className="flex min-w-0 items-center gap-4">
                  <motion.div
                    className="relative shrink-0"
                    initial={reduce ? false : { opacity: 0, scale: 0.8, rotate: -6 }}
                    animate={{ opacity: 1, scale: 1, rotate: 0 }}
                    transition={{ type: 'spring', stiffness: 320, damping: 20 }}
                  >
                    {/* A turning ring of colour around the initials. */}
                    <span aria-hidden className="absolute -inset-1 overflow-hidden rounded-[20px]">
                      <motion.span
                        className="absolute left-1/2 top-1/2 aspect-square w-[220%] -translate-x-1/2 -translate-y-1/2 bg-[conic-gradient(from_0deg,#f97316,#ec4899,#8b5cf6,#0ea5e9,#10b981,#f97316)]"
                        animate={reduce ? undefined : { rotate: 360 }}
                        transition={{ duration: 10, repeat: Infinity, ease: 'linear' }}
                      />
                    </span>
                    <div
                      className={cn(
                        'relative flex h-16 w-16 items-center justify-center rounded-2xl bg-gradient-to-br text-xl font-bold text-white shadow-lg ring-2 ring-white',
                        gradient,
                      )}
                    >
                      {initialsOf(lead.name) || <UserIcon className="w-7 h-7" />}
                    </div>
                    {lead.isConverted && (
                      <span
                        aria-hidden
                        className="absolute -right-1.5 -bottom-1.5 flex h-6 w-6 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-500 text-white shadow ring-2 ring-white"
                      >
                        <CheckCircle2 className="w-3.5 h-3.5" />
                      </span>
                    )}
                  </motion.div>
                  <motion.div className="min-w-0 flex-1" {...rise(0.06)}>
                    <div className="flex flex-wrap items-center gap-2">
                      <h1 className="truncate text-2xl font-bold leading-tight tracking-tight text-slate-900 sm:text-[28px]">
                        {lead.name}
                      </h1>
                      {/* Read-only: the status is derived from the pipeline below,
                          and the badge carries its own cause so "why does this say
                          Qualified?" is answerable without a picker. */}
                      <DerivedStatusBadge
                        status={lead.status}
                        pipeline={pipeline}
                        statusesUnavailable={statusesUnavailable}
                      />
                      {lead.isConverted && (
                        <span className="inline-flex items-center gap-1.5 rounded-lg bg-gradient-to-r from-emerald-500 to-teal-500 px-2.5 py-1 text-xs font-bold text-white shadow-sm">
                          <CheckCircle2 className="w-3.5 h-3.5" />
                          Converted
                        </span>
                      )}
                      {lead.lifecycleStage && (
                        <span className="inline-flex items-center gap-1 rounded-full bg-indigo-50 px-2 py-0.5 text-[11px] font-semibold uppercase tracking-wider text-indigo-700 ring-1 ring-indigo-100">
                          {lead.lifecycleStage}
                        </span>
                      )}
                    </div>
                    {/* How to reach them, and who they belong to, as coloured chips. */}
                    <motion.div
                      className="mt-2 flex flex-wrap items-center gap-1.5"
                      initial={reduce ? false : 'hidden'}
                      animate="shown"
                      variants={{ hidden: {}, shown: { transition: { staggerChildren: 0.05, delayChildren: 0.12 } } }}
                    >
                      {company && (
                        <InfoChip tone="indigo" icon={<Building2 className="w-3 h-3" />} to={`/companies/${company.id}`}>{company.name}</InfoChip>
                      )}
                      {lead.email && (
                        <InfoChip
                          tone="sky"
                          icon={<Mail className="w-3 h-3" />}
                          href={`mailto:${lead.email}`}
                          trailing={<CopyButton label="Copy email" copied={copiedField === 'Email'} onClick={() => copyToClipboard(lead.email!, 'Email')} />}
                        >
                          {lead.email}
                        </InfoChip>
                      )}
                      {lead.phone && (
                        <InfoChip
                          tone="teal"
                          icon={<Phone className="w-3 h-3" />}
                          href={`tel:${lead.phone}`}
                          trailing={<CopyButton label="Copy phone" copied={copiedField === 'Phone'} onClick={() => copyToClipboard(lead.phone!, 'Phone')} />}
                        >
                          {lead.phone}
                        </InfoChip>
                      )}
                      {assignee && (
                        <InfoChip tone="purple" icon={<UserIcon className="w-3 h-3" />}>{assignee.name}</InfoChip>
                      )}
                      {referredByName && (
                        <InfoChip
                          tone="violet"
                          icon={<UserPlus className="w-3 h-3" />}
                          to={lead.referredByContactId ? `/contacts/${lead.referredByContactId}` : undefined}
                        >
                          Referred by {referredByName}
                        </InfoChip>
                      )}
                    </motion.div>
                  </motion.div>
                </div>

                {/* The numbers, then the quick actions. A tile that names a tab opens it. */}
                <motion.div
                  className="flex flex-col gap-3 sm:flex-row sm:flex-wrap sm:items-center xl:flex-nowrap xl:justify-end"
                  initial={reduce ? false : { opacity: 0, x: 16 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ duration: 0.45, ease: EASE, delay: 0.12 }}
                >
                  {/* One swipeable row on a phone, wrapping from small screens up. */}
                  <div className="-mx-1 flex gap-2 overflow-x-auto px-1 pb-1 [scrollbar-width:none] sm:mx-0 sm:flex-wrap sm:items-stretch sm:overflow-visible sm:px-0 sm:pb-0">
                    <div className="flex shrink-0 items-center gap-2.5 rounded-xl border border-slate-200/80 bg-white/80 px-2.5 py-1.5 shadow-sm backdrop-blur">
                      <ScoreEditor score={lead.leadScore ?? 0} onSave={saveScore} readOnly={isReadOnly} size={44} />
                      <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Score</span>
                    </div>
                    <StatTile tone="amber" icon={<Clock className="w-3.5 h-3.5" />} label="Last contact" title={formatFull(lead.lastContactedAt) ?? undefined}>
                      {formatRelative(lead.lastContactedAt) ?? 'Never'}
                    </StatTile>
                    <StatTile tone="orange" icon={<CheckCircle2 className="w-3.5 h-3.5" />} label="Open tasks" onClick={() => setTab('tasks')}>
                      {openTaskCount}
                    </StatTile>
                    <StatTile tone="indigo" icon={<MessageSquarePlus className="w-3.5 h-3.5" />} label="Activity" onClick={() => setTab('activity')}>
                      {activities.length}
                    </StatTile>
                    <StatTile tone="slate" icon={<Calendar className="w-3.5 h-3.5" />} label="Added" title={formatFull(lead.createdAtUtc) ?? undefined}>
                      {formatRelative(lead.createdAtUtc) ?? '—'}
                    </StatTile>
                  </div>
                  <div className="flex flex-wrap items-center gap-2">
                    {lead.email && (
                      <Button asChild size="sm" variant="outline" className="gap-1.5 rounded-xl">
                        <a href={`mailto:${lead.email}`}><Mail className="w-4 h-4" /> Email</a>
                      </Button>
                    )}
                    {lead.phone && (
                      <Button asChild size="sm" variant="outline" className="gap-1.5 rounded-xl">
                        <a href={`tel:${lead.phone}`}><Phone className="w-4 h-4" /> Call</a>
                      </Button>
                    )}
                    {!isReadOnly && (
                      <QuickLogPopover
                        onSubmit={(p) => logActivity(p)}
                        trigger={
                          <Button size="sm" className="gap-1.5 rounded-xl bg-gradient-to-r from-orange-500 via-pink-500 to-fuchsia-500 text-white shadow shadow-fuchsia-500/25 hover:opacity-95">
                            <MessageSquarePlus className="w-4 h-4" />
                            Log activity
                          </Button>
                        }
                      />
                    )}
                  </div>
                </motion.div>
              </div>
            </div>
          </section>

          {/* Body: everything else in two columns, so the page uses its width. */}
          <div className="grid w-full gap-5 px-[var(--page-padding)] py-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
            {/* Main column */}
            <div className="min-w-0 space-y-4">
              <motion.div {...rise(0.14)}>
                <NextActionsSlot className="mb-4" />
                {/* Status ↔ pipeline relationship: how the automation is behaving,
                    or a one-click resolution when the two disagree. */}
                <StatusSyncStrip
                  className="mb-3"
                  drift={statusSyncView.drift}
                  reasons={statusSyncView.reasons}
                  enabled={statusSync.enabled}
                  onToggleEnabled={statusSync.setEnabled}
                  disabled={lead.isConverted || isReadOnly}
                  onApply={(name) => applyDerivedStatus(name)}
                />
                {/* `previewStatus` is withheld from a viewer: the chips are disabled,
                    so a "→ Qualified" hint would advertise something they cannot do. */}
                <LeadPipelineTracker
                  value={pipeline}
                  disabled={lead.isConverted || isReadOnly}
                  onChange={savePipeline}
                  previewStatus={isReadOnly ? undefined : (patch) => statusSync.preview(lead, pipeline, patch)}
                  onConvert={isReadOnly ? undefined : () => navigate(`/leads?convertLeadId=${lead.id}`)}
                />
              </motion.div>

              {/* Sits directly under the tracker because it drives it: sending
                  satisfies the Contract phase and full execution satisfies the
                  Signature phase, which the status auto-sync then carries through. */}
              <motion.div {...rise(0.2)}>
                <ContractPanel leadId={lead.id} leadName={lead.name} readOnly={isReadOnly} />
              </motion.div>

              {/* Tab bar: a pill that slides to the tab you pick. */}
              <div className="sticky top-[52px] z-20 flex items-center gap-1 rounded-2xl border border-slate-200/80 bg-white/85 p-1 shadow-sm backdrop-blur-md">
                <TabButton active={tab === 'activity'} tone="indigo" onClick={() => setTab('activity')} icon={<MessageSquarePlus className="w-4 h-4" />} count={activities.length}>
                  Activity
                </TabButton>
                <TabButton active={tab === 'tasks'} tone="orange" onClick={() => setTab('tasks')} icon={<CheckCircle2 className="w-4 h-4" />} count={openTaskCount}>
                  Tasks
                </TabButton>
                <TabButton active={tab === 'notes'} tone="emerald" onClick={() => setTab('notes')} icon={<FileText className="w-4 h-4" />} count={noteActivities.length}>
                  Notes
                </TabButton>
              </div>

              <AnimatePresence mode="wait" initial={false}>
              <motion.div
                key={tab}
                initial={reduce ? false : { opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, y: -6, transition: { duration: 0.14 } }}
                transition={{ duration: 0.24, ease: EASE }}
              >
              {tab === 'activity' && (
                <div className="space-y-5">
                  {/* Quick note composer — writers only */}
                  {!isReadOnly && (
                  <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm transition-[border-color,box-shadow] duration-300 focus-within:border-amber-300 focus-within:shadow-lg focus-within:shadow-amber-500/15 focus-within:ring-4 focus-within:ring-amber-200/40">
                    {/* Warms up while writing: an amber border and a soft glow. */}
                    <label className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                      <span className="flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br from-amber-400 to-orange-500 text-white shadow-sm" aria-hidden>
                        <FileText className="h-3.5 w-3.5" />
                      </span>
                      Add a note
                    </label>
                    <textarea
                      value={noteDraft}
                      onChange={(e) => setNoteDraft(e.target.value)}
                      onKeyDown={(e) => {
                        if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') { e.preventDefault(); handleAddNote(); }
                      }}
                      rows={2}
                      placeholder="Capture a quick thought — Cmd/Ctrl+Enter to save."
                      className="mt-2 w-full resize-none rounded-lg border border-slate-200 bg-amber-50/30 px-3 py-2 text-sm placeholder:text-slate-400 focus:border-amber-300 focus:bg-white focus:outline-none focus:ring-2 focus:ring-amber-300/40"
                    />
                    <div className="mt-2 flex items-center justify-end">
                      <Button size="sm" onClick={handleAddNote} disabled={!noteDraft.trim() || savingNote} className="gap-1.5 rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 text-white shadow shadow-orange-500/25 hover:opacity-95">
                        {savingNote ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        Save note
                      </Button>
                    </div>
                  </div>
                  )}

                  {/* Email composer — sending also logs an activity, so writers only */}
                  {lead.email && !isReadOnly && (
                    <details className="rounded-2xl border border-slate-200 bg-white shadow-sm group">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50">
                        <span className="inline-flex items-center gap-2">
                          <span className="flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br from-purple-500 to-fuchsia-600 text-white shadow-sm" aria-hidden>
                            <Mail className="h-3.5 w-3.5" />
                          </span>
                          Compose email to {lead.name}
                        </span>
                        <Pencil className="w-3.5 h-3.5 text-slate-300 group-open:rotate-90 transition-transform" />
                      </summary>
                      <div className="space-y-2 border-t border-slate-100 p-4">
                        <input
                          value={emailSubject}
                          onChange={(e) => setEmailSubject(e.target.value)}
                          placeholder="Subject"
                          className="w-full rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
                        />
                        <textarea
                          value={emailBody}
                          onChange={(e) => setEmailBody(e.target.value)}
                          rows={4}
                          placeholder="Write your message…"
                          className="w-full resize-none rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
                        />
                        <div className="flex items-center justify-end">
                          <Button size="sm" onClick={handleSendEmail} disabled={!emailBody.trim() || sendingEmail} className="gap-1.5 rounded-xl bg-gradient-to-r from-purple-600 to-fuchsia-600 text-white shadow shadow-fuchsia-500/25 hover:opacity-95">
                            {sendingEmail ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Mail className="w-3.5 h-3.5" />}
                            Log & open in mail client
                          </Button>
                        </div>
                      </div>
                    </details>
                  )}

                  <ActivityTimeline activities={activities} />
                </div>
              )}

              {tab === 'tasks' && (
                <div className="space-y-4">
                  {!isReadOnly && (
                  <div className="rounded-2xl border border-slate-200 bg-gradient-to-br from-white to-orange-50/40 p-4 shadow-sm transition-shadow focus-within:shadow-md focus-within:shadow-orange-500/10">
                    <label className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
                      <span className="flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-sm" aria-hidden>
                        <Plus className="h-3.5 w-3.5" />
                      </span>
                      New task
                    </label>
                    <div className="mt-1.5 flex flex-col gap-2 sm:flex-row">
                      <input
                        value={newTaskTitle}
                        onChange={(e) => setNewTaskTitle(e.target.value)}
                        onKeyDown={(e) => { if (e.key === 'Enter') { e.preventDefault(); handleAddTask(); } }}
                        placeholder="What needs doing?"
                        className="flex-1 rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
                      />
                      <input
                        type="datetime-local"
                        value={newTaskDue}
                        onChange={(e) => setNewTaskDue(e.target.value)}
                        className="rounded-lg border border-slate-200 px-3 py-2 text-sm focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
                      />
                      <Button size="sm" onClick={handleAddTask} disabled={!newTaskTitle.trim() || creatingTask} className="gap-1.5 rounded-xl bg-gradient-to-r from-orange-500 to-pink-500 text-white shadow shadow-pink-500/25 hover:opacity-95">
                        {creatingTask ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        Add
                      </Button>
                    </div>
                  </div>
                  )}

                  {tasks.length === 0 ? (
                    <div className="rounded-2xl border border-dashed border-orange-200 bg-gradient-to-br from-white via-orange-50/40 to-pink-50/40 px-6 py-10 text-center">
                      <motion.span
                        aria-hidden
                        className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-gradient-to-br from-orange-500 to-pink-500 text-white shadow-lg shadow-pink-500/25"
                        animate={reduce ? undefined : { y: [0, -6, 0], rotate: [0, -6, 6, 0] }}
                        transition={{ duration: 2.8, repeat: Infinity, ease: 'easeInOut' }}
                      >
                        <CheckCircle2 className="h-6 w-6" />
                      </motion.span>
                      <p className="mt-3 text-sm font-medium text-slate-600">No tasks yet.</p>
                      <p className="mt-0.5 text-xs text-slate-400">Add one above, and tick it off here when it is done.</p>
                    </div>
                  ) : (
                    <ul className="space-y-2">
                      <AnimatePresence mode="popLayout" initial={false}>
                      {sortedTasks.map((t) => {
                        const overdue = t.status !== 'completed' && isOverdue(t.dueDateUtc);
                        return (
                          <motion.li
                            key={t.id}
                            layout={reduce ? false : 'position'}
                            initial={reduce ? false : { opacity: 0, y: -8, scale: 0.98 }}
                            animate={{ opacity: t.status === 'completed' ? 0.6 : 1, y: 0, scale: 1 }}
                            exit={reduce ? { opacity: 0, transition: { duration: 0 } } : { opacity: 0, x: 32, transition: { duration: 0.2 } }}
                            transition={{ duration: 0.3, ease: EASE }}
                            className={cn(
                            'group flex items-start gap-3 rounded-2xl border border-l-4 px-4 py-3 transition-[border-color,box-shadow,background-color] hover:shadow-md',
                            t.status === 'completed'
                              ? 'border-emerald-200 border-l-emerald-400 bg-emerald-50/40'
                              : overdue
                                ? 'border-red-200 border-l-red-500 bg-gradient-to-r from-red-50/60 to-white'
                                : t.priority === 'high'
                                  ? 'border-slate-200 border-l-pink-500 bg-white'
                                  : t.priority === 'medium'
                                    ? 'border-slate-200 border-l-amber-400 bg-white'
                                    : 'border-slate-200 border-l-sky-300 bg-white',
                          )}>
                            <span className="relative mt-0.5 flex shrink-0">
                            <Confetti fire={taskBurst?.id === t.id ? taskBurst.at : 0} count={14} spread={42} />
                            <motion.button
                              type="button"
                              whileTap={reduce ? undefined : { scale: 0.85 }}
                              onClick={() => toggleTask(t)}
                              aria-label={t.status === 'completed' ? 'Mark task as not done' : 'Mark task as done'}
                              className={cn(
                                'flex h-5 w-5 shrink-0 items-center justify-center rounded-md border-2 transition-colors',
                                t.status === 'completed'
                                  ? 'border-emerald-500 bg-gradient-to-br from-emerald-400 to-teal-500 text-white'
                                  : 'border-slate-300 bg-white hover:border-emerald-400 hover:bg-emerald-50',
                              )}
                            >
                              <AnimatePresence initial={false}>
                                {t.status === 'completed' && (
                                  <motion.span
                                    initial={reduce ? false : { scale: 0, rotate: -45 }}
                                    animate={{ scale: 1, rotate: 0 }}
                                    exit={reduce ? { opacity: 0 } : { scale: 0 }}
                                    transition={{ type: 'spring', stiffness: 500, damping: 22 }}
                                    className="inline-flex"
                                  >
                                    <CheckCircle2 className="w-3 h-3" />
                                  </motion.span>
                                )}
                              </AnimatePresence>
                            </motion.button>
                            </span>
                            <div className="min-w-0 flex-1">
                              <p className={cn('text-sm font-medium text-slate-800', t.status === 'completed' && 'line-through text-slate-500')}>
                                {t.title}
                              </p>
                              {t.dueDateUtc && (
                                <p className={cn(
                                  'mt-0.5 inline-flex items-center gap-1 text-xs',
                                  overdue ? 'font-semibold text-red-600' : 'text-slate-500',
                                )}>
                                  {overdue && <AlertCircle className="w-3 h-3" />}
                                  {overdue ? 'Overdue · ' : 'Due '}{formatFull(t.dueDateUtc)}
                                </p>
                              )}
                            </div>
                            <div className="flex shrink-0 items-center gap-1">
                              <span className={cn(
                                'text-[10px] font-bold uppercase tracking-wider rounded-full px-2 py-0.5',
                                t.priority === 'high'
                                  ? 'bg-gradient-to-r from-pink-500 to-rose-500 text-white shadow-sm shadow-pink-500/30'
                                  : t.priority === 'medium'
                                    ? 'bg-amber-100 text-amber-700'
                                    : 'bg-sky-50 text-sky-600',
                              )}>{t.priority}</span>
                              <button
                                type="button"
                                onClick={() => handleDeleteTask(t)}
                                aria-label="Delete task"
                                className="flex h-7 w-7 items-center justify-center rounded-md text-slate-300 opacity-0 transition-all hover:bg-red-50 hover:text-red-600 focus-visible:opacity-100 group-hover:opacity-100"
                              >
                                <Trash2 className="w-3.5 h-3.5" />
                              </button>
                            </div>
                          </motion.li>
                        );
                      })}
                      </AnimatePresence>
                    </ul>
                  )}
                </div>
              )}

              {tab === 'notes' && (
                <div className="space-y-4">
                  <InlineField
                    label="Description"
                    value={lead.description ?? ''}
                    onSave={saveDescription}
                    variant="textarea"
                    readOnly={isReadOnly}
                    placeholder="Add a longer description, context, or qualification notes…"
                    emptyHint="Click to add a description"
                  />
                  <ActivityTimeline
                    activities={noteActivities}
                    emptyHint="No notes yet. Click ‘Add a note’ on the Activity tab."
                    emptyDetail="Notes you save show up here, newest first."
                  />
                </div>
              )}
              </motion.div>
              </AnimatePresence>
            </div>

            {/* Sidebar: compact, colour-coded cards beside the pipeline from the top, so the
                whole page scrolls as one. They arrive one after another. */}
            <aside className="min-w-0 space-y-3">
              {/* AI assist — derived engagement score + recommended next action. */}
              <motion.div {...card(0)}>
                <AiNextActionCard
                  lead={lead}
                  activities={activities}
                  tasks={tasks}
                  className="overflow-hidden rounded-2xl border border-indigo-200/70 bg-gradient-to-br from-indigo-50/80 via-white to-fuchsia-50/40 shadow-sm"
                  onAct={(kind) => {
                    if (kind === 'convert') navigate(`/leads?convertLeadId=${lead.id}`);
                    else if (kind === 'task') setTab('tasks');
                    else setTab('activity');
                  }}
                />
              </motion.div>

              {/* Next step — the most urgent open task, one glance and one click away. */}
              <motion.div {...card(1)}>
                <SideCard
                  title="Next step"
                  tone={nextTask && isOverdue(nextTask.dueDateUtc) ? 'rose' : 'orange'}
                  icon={nextTask && isOverdue(nextTask.dueDateUtc) ? <AlertCircle className="w-3.5 h-3.5" /> : <Clock className="w-3.5 h-3.5" />}
                  action={(
                    <button
                      type="button"
                      onClick={() => setTab('tasks')}
                      className="text-[11px] font-semibold text-indigo-600 hover:text-indigo-800"
                    >
                      {nextTask ? 'All tasks' : 'Add task'}
                    </button>
                  )}
                >
                  {nextTask ? (
                    <div className="flex items-start gap-3">
                      <button
                        type="button"
                        onClick={() => toggleTask(nextTask)}
                        aria-label="Mark next step done"
                        className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border-2 border-slate-300 bg-white transition-colors hover:border-emerald-400 hover:bg-emerald-50"
                      />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-slate-800">{nextTask.title}</p>
                        {nextTask.dueDateUtc ? (
                          <p className={cn(
                            'mt-0.5 inline-flex items-center gap-1 text-xs',
                            isOverdue(nextTask.dueDateUtc) ? 'font-semibold text-red-600' : 'text-slate-500',
                          )}>
                            {isOverdue(nextTask.dueDateUtc) ? 'Overdue · ' : 'Due '}{formatFull(nextTask.dueDateUtc)}
                          </p>
                        ) : (
                          <p className="mt-0.5 text-xs text-slate-400">No due date</p>
                        )}
                      </div>
                    </div>
                  ) : (
                    <button
                      type="button"
                      onClick={() => setTab('tasks')}
                      className="flex w-full items-center gap-2 rounded-lg border border-dashed border-orange-200 px-3 py-2 text-left text-sm text-slate-500 transition-colors hover:border-orange-300 hover:bg-orange-50/50 hover:text-orange-700"
                    >
                      <Plus className="w-4 h-4" />
                      Add a follow-up task
                    </button>
                  )}
                </SideCard>
              </motion.div>

              {/* Who referred this lead: visible to the whole team, and editable here. */}
              <motion.div {...card(2)}>
                <ReferralCard lead={lead} contacts={contacts} readOnly={isReadOnly} onChange={saveReferral} />
              </motion.div>

              {/* Contact: the fields edit in place. */}
              <motion.div {...card(3)}>
                <SideCard title="Contact" tone="sky" icon={<UserIcon className="w-3.5 h-3.5" />}>
                  <div className="space-y-2.5">
                    <InlineField label="Name" value={lead.name} onSave={saveName} readOnly={isReadOnly} icon={<UserIcon className="w-3.5 h-3.5" />} />
                    <InlineField label="Email" type="email" value={lead.email ?? ''} onSave={saveEmail} readOnly={isReadOnly} icon={<Mail className="w-3.5 h-3.5" />} />
                    <InlineField label="Phone" type="tel" value={lead.phone ?? ''} onSave={savePhone} readOnly={isReadOnly} icon={<Phone className="w-3.5 h-3.5" />} />
                    {lead.firstResponseDueAtUtc && (
                      <p className="text-xs text-slate-500">
                        First response due {new Date(lead.firstResponseDueAtUtc).toLocaleString()}
                        {lead.slaBreached ? ' · late' : lead.firstResponseAtUtc ? ' · answered' : ' · waiting'}
                        {lead.enrichedDomain ? ` · company from ${lead.enrichedDomain}` : ''}
                      </p>
                    )}
                    <LeadStatusTimeline leadId={lead.id} status={lead.status} />
                  </div>
                </SideCard>
              </motion.div>

              {/* Details: company, source, stage and owner side by side, two to a row. */}
              <motion.div {...card(4)}>
                <SideCard title="Details" tone="violet" icon={<Sparkles className="w-3.5 h-3.5" />}>
                  <div className="grid grid-cols-2 gap-x-3 gap-y-2.5">
                    <SidebarSelectField label="Company" icon={<Building2 className="w-3 h-3 text-indigo-400" />}>
                      <Select value={lead.companyId ?? ''} disabled={isReadOnly} onValueChange={(v) => setCompanyId(v === '__none__' ? '' : v)}>
                        <SelectTrigger className="h-9 text-sm">
                          <SelectValue placeholder="No company" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">No company</SelectItem>
                          {companies.map((c) => (
                            <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </SidebarSelectField>
                    <SidebarSelectField label="Owner" icon={<UserIcon className="w-3 h-3 text-purple-400" />}>
                      <Select value={lead.assignedToId ?? '__none__'} disabled={isReadOnly} onValueChange={(v) => setAssignment(v === '__none__' ? '' : v)}>
                        <SelectTrigger className="h-9 text-sm">
                          <SelectValue placeholder="Unassigned" />
                        </SelectTrigger>
                        <SelectContent>
                          <SelectItem value="__none__">Unassigned</SelectItem>
                          {orgMembers.map((m) => (
                            <SelectItem key={m.userId} value={m.userId}>{m.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </SidebarSelectField>
                    <SidebarSelectField label="Source" icon={<Sparkles className="w-3 h-3 text-orange-400" />}>
                      <Select value={lead.source ?? ''} disabled={isReadOnly} onValueChange={setSource}>
                        <SelectTrigger className="h-9 text-sm">
                          <SelectValue placeholder="Not set" />
                        </SelectTrigger>
                        <SelectContent>
                          {sourceOptions.map((s) => (
                            <SelectItem key={s.id} value={s.name}>{s.name}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </SidebarSelectField>
                    <SidebarSelectField label="Stage" icon={<CheckCircle2 className="w-3 h-3 text-emerald-400" />}>
                      <Select value={lead.lifecycleStage ?? ''} disabled={isReadOnly} onValueChange={setLifecycle}>
                        <SelectTrigger className="h-9 text-sm">
                          <SelectValue placeholder="Not set" />
                        </SelectTrigger>
                        <SelectContent>
                          {LIFECYCLE_STAGES.map((s) => (
                            <SelectItem key={s} value={s}>{s}</SelectItem>
                          ))}
                        </SelectContent>
                      </Select>
                    </SidebarSelectField>
                  </div>
                </SideCard>
              </motion.div>

              {/* Tags intentionally not surfaced here yet: the backend's
                  UpdateLeadRequest has no Tags field, so any add/remove would
                  silently fail to persist. Re-enable once the backend gains
                  Lead.Tags support. */}

              {/* Sales Tracker (Excel-parity fields for pre-sales pipeline) */}
              <motion.div {...card(5)}>
                <SalesTrackerCard
                  lead={lead}
                  statusSync={statusSync}
                  readOnly={isReadOnly}
                  onSaved={(updated) => setLead((prev) => (prev ? { ...prev, ...updated } : prev))}
                />
              </motion.div>

              {/* Custom fields (renders only when defined for leads) */}
              <motion.div {...card(6)}>
                <CustomFieldsCard entityType="lead" recordId={lead.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm" />
              </motion.div>

              {/* Attachments */}
              <motion.div {...card(7)}>
                <AttachmentsCard entityType="lead" recordId={lead.id} className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm" />
              </motion.div>
            </aside>
          </div>
        </main>
      </PageTransition>

      {/* Delete confirm */}
      <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete this lead?</AlertDialogTitle>
            <AlertDialogDescription>
              {lead.name} will be permanently removed along with all related activities. This cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={deleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete} disabled={deleting} className="bg-red-600 hover:bg-red-700">
              {deleting ? <Loader2 className="w-4 h-4 animate-spin" /> : 'Delete'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Full lead editor — same multi-step dialog used on the leads list, so
          every field (incl. Referred by) is editable from the detail page. */}
      <AddLeadDialog
        open={editOpen}
        onOpenChange={setEditOpen}
        editingLead={lead}
        form={editForm}
        setForm={setEditForm}
        companies={companies}
        contacts={contacts}
        sourceOptions={sourceOptions}
        statusOptions={statusOptions}
        onSubmit={handleEditSubmit}
        saving={savingEdit}
      />

      {lead.email && (
        <EmailComposerDialog
          open={emailOpen}
          onOpenChange={setEmailOpen}
          to={lead.email}
          context={{ leadId: lead.id }}
          aiContext={`${lead.name}${lead.companyName ? ` at ${lead.companyName}` : ''} · ${lead.status} lead`}
          onSent={() => {
            getActivitiesByLead(lead.id).then(setActivities).catch(() => {});
            void bumpLastContacted();
          }}
        />
      )}

      <SaveLeadAsContactDialog
        open={saveContactOpen}
        onOpenChange={setSaveContactOpen}
        lead={lead}
        companyName={company?.name ?? lead.companyName}
        existingContacts={contacts}
        onCreated={() => { getActivitiesByLead(lead.id).then(setActivities).catch(() => {}); }}
      />
    </div>
  );
}

// ---- Small inline components ----

type Tone = 'indigo' | 'orange' | 'emerald' | 'sky' | 'teal' | 'purple' | 'violet' | 'amber' | 'rose' | 'slate';

const TAB_PILL: Partial<Record<Tone, string>> = {
  indigo: 'from-indigo-500 to-violet-500 shadow-indigo-500/25',
  orange: 'from-orange-500 to-pink-500 shadow-orange-500/25',
  emerald: 'from-emerald-500 to-teal-500 shadow-emerald-500/25',
};

/** One tab of the segmented bar. A coloured pill slides to the tab picked. */
function TabButton({
  active, onClick, icon, children, tone, count,
}: { active: boolean; onClick: () => void; icon: React.ReactNode; children: React.ReactNode; tone: Tone; count: number }) {
  const reduce = useMotionPreference();
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'relative flex flex-1 items-center justify-center gap-1.5 rounded-xl px-3 py-2 text-sm font-semibold transition-colors sm:flex-none',
        active ? 'text-white' : 'text-slate-500 hover:bg-slate-100/70 hover:text-slate-800',
      )}
      aria-current={active ? 'page' : undefined}
    >
      {active && (
        <motion.span
          aria-hidden
          layoutId="lead-detail-tab-pill"
          transition={reduce ? { duration: 0 } : { type: 'spring', stiffness: 480, damping: 36 }}
          className={cn('absolute inset-0 rounded-xl bg-gradient-to-r shadow-md', TAB_PILL[tone])}
        />
      )}
      <span className="relative z-10 inline-flex items-center gap-1.5">
        {icon}
        {children}
        <span
          className={cn(
            'inline-flex min-w-5 items-center justify-center rounded-full px-1.5 text-[10px] font-bold tabular-nums',
            active ? 'bg-white/25 text-white' : 'bg-slate-100 text-slate-500',
          )}
        >
          {count}
        </span>
      </span>
    </button>
  );
}

const CHIP_TONE: Record<Tone, string> = {
  indigo: 'bg-indigo-50 text-indigo-700 ring-indigo-100 hover:bg-indigo-100',
  sky: 'bg-sky-50 text-sky-700 ring-sky-100 hover:bg-sky-100',
  teal: 'bg-teal-50 text-teal-700 ring-teal-100 hover:bg-teal-100',
  purple: 'bg-purple-50 text-purple-700 ring-purple-100 hover:bg-purple-100',
  violet: 'bg-violet-50 text-violet-700 ring-violet-100 hover:bg-violet-100',
  orange: 'bg-orange-50 text-orange-700 ring-orange-100 hover:bg-orange-100',
  emerald: 'bg-emerald-50 text-emerald-700 ring-emerald-100 hover:bg-emerald-100',
  amber: 'bg-amber-50 text-amber-700 ring-amber-100 hover:bg-amber-100',
  rose: 'bg-rose-50 text-rose-700 ring-rose-100 hover:bg-rose-100',
  slate: 'bg-slate-50 text-slate-600 ring-slate-200 hover:bg-slate-100',
};

/** A coloured chip in the header: a way to reach the person, or something about them. */
function InfoChip({ tone, icon, to, href, trailing, children }: {
  tone: Tone;
  icon: React.ReactNode;
  to?: string;
  href?: string;
  trailing?: React.ReactNode;
  children: React.ReactNode;
}) {
  const className = cn(
    'inline-flex max-w-[16rem] items-center gap-1.5 rounded-full py-1 pl-1 pr-2.5 text-xs font-medium ring-1 transition-all hover:-translate-y-px hover:shadow-sm',
    CHIP_TONE[tone],
  );
  const inner = (
    <>
      <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-white/90 shadow-sm">{icon}</span>
      <span className="truncate">{children}</span>
    </>
  );
  const chip = to
    ? <Link to={to} className={className}>{inner}</Link>
    : href
      ? <a href={href} className={className}>{inner}</a>
      : <span className={className}>{inner}</span>;
  return (
    <motion.span
      className="group/copy inline-flex items-center gap-0.5"
      variants={{ hidden: { opacity: 0, y: 6, scale: 0.95 }, shown: { opacity: 1, y: 0, scale: 1 } }}
      transition={{ type: 'spring', stiffness: 420, damping: 26 }}
    >
      {chip}
      {trailing}
    </motion.span>
  );
}

const TILE_ICON: Record<Tone, string> = {
  amber: 'from-amber-400 to-orange-500 shadow-amber-500/30',
  orange: 'from-orange-500 to-pink-500 shadow-orange-500/30',
  indigo: 'from-indigo-500 to-violet-500 shadow-indigo-500/30',
  slate: 'from-slate-400 to-slate-500 shadow-slate-500/20',
  emerald: 'from-emerald-500 to-teal-500 shadow-emerald-500/30',
  sky: 'from-sky-400 to-blue-500 shadow-sky-500/30',
  teal: 'from-teal-400 to-cyan-500 shadow-teal-500/30',
  purple: 'from-purple-500 to-fuchsia-500 shadow-purple-500/30',
  violet: 'from-violet-500 to-fuchsia-500 shadow-violet-500/30',
  rose: 'from-rose-500 to-pink-500 shadow-rose-500/30',
};

/** One number in the header strip. With a tab to open, it is a button. */
function StatTile({ tone, icon, label, title, onClick, children }: {
  tone: Tone;
  icon: React.ReactNode;
  label: string;
  title?: string;
  onClick?: () => void;
  children: React.ReactNode;
}) {
  const reduce = useMotionPreference();
  const body = (
    <>
      <span className={cn('flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-md', TILE_ICON[tone])}>{icon}</span>
      <span className="min-w-0 text-left">
        <span className="block truncate text-sm font-bold tabular-nums leading-tight text-slate-900">{children}</span>
        <span className="block text-[10px] font-semibold uppercase tracking-wider text-slate-400">{label}</span>
      </span>
    </>
  );
  const className = 'flex shrink-0 items-center gap-2 rounded-xl border border-slate-200/80 bg-white/80 px-2.5 py-1.5 shadow-sm backdrop-blur';
  if (!onClick) return <div className={className} title={title}>{body}</div>;
  return (
    <motion.button
      type="button"
      onClick={onClick}
      title={title}
      whileHover={reduce ? undefined : { y: -2 }}
      whileTap={reduce ? undefined : { scale: 0.97 }}
      className={cn(className, 'transition-shadow hover:border-slate-300 hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400')}
    >
      {body}
    </motion.button>
  );
}

const CARD_ICON: Partial<Record<Tone, string>> = {
  orange: 'from-orange-500 to-amber-500',
  rose: 'from-rose-500 to-red-500',
  sky: 'from-sky-500 to-blue-500',
  violet: 'from-violet-500 to-fuchsia-500',
};

/** A sidebar card: a coloured icon and a small caps title, then its content. */
function SideCard({ title, tone, icon, action, children }: {
  title: string;
  tone: Tone;
  icon: React.ReactNode;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
  return (
    <section className={cn(
      'overflow-hidden rounded-2xl border bg-white shadow-sm transition-shadow hover:shadow-md',
      tone === 'rose' ? 'border-rose-200' : 'border-slate-200/80',
    )}>
      <div className="flex items-center justify-between gap-2 px-4 pb-1 pt-3">
        <h3 className="flex items-center gap-2 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
          <span className={cn('flex h-6 w-6 items-center justify-center rounded-lg bg-gradient-to-br text-white shadow-sm', CARD_ICON[tone])}>{icon}</span>
          {title}
        </h3>
        {action}
      </div>
      <div className="px-4 pb-4 pt-2">{children}</div>
    </section>
  );
}

/** Labelled wrapper for sidebar <Select> rows so the label has the same
 *  weight & icon style as the click-to-edit InlineField rows above it. */
function SidebarSelectField({
  label, icon, children,
}: { label: string; icon?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div>
      <div className="mb-1 flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500">
        {icon}
        {label}
      </div>
      {children}
    </div>
  );
}

/** Subtle copy-to-clipboard affordance shown beside email/phone in the hero.
 *  Reveals on hover of its `group/copy` parent; flips to a check on success. */
function CopyButton({ onClick, copied, label }: { onClick: () => void; copied: boolean; label: string }) {
  return (
    <button
      type="button"
      onClick={onClick}
      aria-label={label}
      title={label}
      className="inline-flex h-5 w-5 items-center justify-center rounded text-slate-300 opacity-0 transition-all hover:bg-slate-100 hover:text-slate-600 focus-visible:opacity-100 group-hover/copy:opacity-100"
    >
      {copied ? <Check className="w-3 h-3 text-emerald-600" /> : <Copy className="w-3 h-3" />}
    </button>
  );
}

/**
 * Lead score editor — a circular gauge that flips into an inline number input
 * when clicked. The gauge IS the affordance; no separate "Edit" link.
 */
function ScoreEditor({ score, onSave, readOnly = false, size }: { score: number; onSave: (v: string) => Promise<boolean>; readOnly?: boolean; size?: number }) {
  const [editing, setEditing] = useState(false);
  const [draft, setDraft] = useState(String(score));
  const [saving, setSaving] = useState(false);
  const inputRef = useRef<HTMLInputElement | null>(null);

  useEffect(() => {
    if (!editing) setDraft(String(score));
  }, [score, editing]);
  useEffect(() => {
    if (editing) inputRef.current?.focus();
  }, [editing]);

  const commit = async () => {
    if (saving) return;
    if (draft.trim() === String(score)) { setEditing(false); return; }
    setSaving(true);
    const ok = await onSave(draft.trim());
    setSaving(false);
    if (ok) setEditing(false);
  };

  if (editing) {
    return (
      <div className="flex items-center gap-2 rounded-xl border border-indigo-300 bg-white px-2 py-1.5 shadow-sm">
        <span className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Score</span>
        <input
          ref={inputRef}
          type="number"
          min={0}
          max={100}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Escape') { e.preventDefault(); setDraft(String(score)); setEditing(false); }
            if (e.key === 'Enter') { e.preventDefault(); commit(); }
          }}
          onBlur={commit}
          className="w-14 rounded-md border border-slate-200 px-1.5 py-1 text-center text-sm font-semibold tabular-nums focus:outline-none focus:ring-2 focus:ring-indigo-400/40"
        />
        {saving && <Loader2 className="w-3.5 h-3.5 animate-spin text-slate-400" />}
      </div>
    );
  }

  return (
    <button
      type="button"
      onClick={() => { if (!readOnly) setEditing(true); }}
      aria-label="Edit lead score"
      className="group relative transition-transform hover:scale-[1.03] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400/40 rounded-full"
    >
      <ScoreGauge score={score} size={size} strokeWidth={size && size < 72 ? 5 : undefined} hideCaption={!!size && size < 72} />
      <span className="absolute -bottom-1 -right-1 flex h-6 w-6 items-center justify-center rounded-full bg-white text-slate-400 opacity-0 shadow ring-1 ring-slate-200 transition-opacity group-hover:opacity-100">
        <Pencil className="w-3 h-3" />
      </span>
    </button>
  );
}

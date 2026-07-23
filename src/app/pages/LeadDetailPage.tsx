import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate, useParams, useLocation, Link } from 'react-router-dom';
import {
  ArrowLeft, Mail, Phone, Building2, Sparkles, Trash2, ArrowRightCircle,
  User as UserIcon, CheckCircle2, Loader2, Pencil, Plus, Calendar,
  MessageSquarePlus, FileText, Clock, UserPlus, Copy, Check, AlertCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { ContentSkeleton } from '@/app/components/PageSkeleton';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getLeadById, updateLead, assignLead, deleteLead,
  getCompanies, getContacts, getLeadStatuses, getLeadSources,
  getActivitiesByLead, createActivity,
  getTasksByLead, createTask, updateTask, deleteTask,
  messages,
} from '@/app/api';
import type { Lead, Company, Contact, LeadStatus, LeadSource, Activity, TaskItem } from '@/app/api/types';
import { getOrgMembers, type OrgMemberDto } from '@/app/api/organizations';
import { useOrg } from '@/app/contexts/OrgContext';
import { Button } from '@/app/components/ui/button';
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import { cn } from '@/app/components/ui/utils';
import { StatusChangePopover } from './leads/components/StatusChangePopover';
import { QuickLogPopover } from './leads/components/QuickLogPopover';
import { LeadPipelineTracker } from './leads/components/LeadPipelineTracker';
import { parsePipeline, serializePipeline, type LeadPipeline } from './leads/leadPipeline';
import { AddLeadDialog } from './leads/AddLeadDialog';
import { FALLBACK_STATUSES, FALLBACK_SOURCES, LIFECYCLE_STAGES, EMPTY_LEAD_FORM } from './leads/config';
import { isValidGuid } from './leads/utils';
import { loadLeadReferrals, setLeadReferral } from './leads/leadReferralStore';
import { loadLeadAssignments, setLeadAssignment } from './leads/leadAssignmentStore';
import type { LeadForm } from './leads/types';
import { InlineField } from './leads/detail/InlineField';
import { ActivityTimeline } from './leads/detail/ActivityTimeline';
import { ScoreGauge } from './leads/detail/ScoreGauge';
import AiNextActionCard from '@/app/components/AiNextActionCard';
import SaveLeadAsContactDialog from '@/app/components/SaveLeadAsContactDialog';
import CustomFieldsCard from '@/app/components/CustomFieldsCard';
import AttachmentsCard from '@/app/components/AttachmentsCard';
import EmailComposerDialog from '@/app/components/EmailComposerDialog';

type Tab = 'activity' | 'tasks' | 'notes';

const STATUS_GRADIENTS: Record<string, string> = {
  New: 'from-blue-500 via-indigo-500 to-cyan-400',
  Contacted: 'from-amber-500 via-orange-500 to-rose-400',
  Qualified: 'from-emerald-500 via-teal-500 to-cyan-400',
  Lost: 'from-slate-500 via-slate-400 to-slate-300',
};
const STATUS_BADGE_TONE: Record<string, string> = {
  New: 'bg-blue-50 text-blue-700 border-blue-200',
  Contacted: 'bg-amber-50 text-amber-700 border-amber-200',
  Qualified: 'bg-emerald-50 text-emerald-700 border-emerald-200',
  Lost: 'bg-slate-100 text-slate-600 border-slate-200',
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

export default function LeadDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { currentOrgId } = useOrg();

  // When the user arrives from the leads list, Leads.tsx stashes the source
  // URL (including filters/sort) here so Back / breadcrumb can return there
  // intact. Direct deep-links won't have it; fall back to the bare list.
  const backUrl = (location.state as { from?: string } | null)?.from ?? '/leads';

  // Core data
  const [lead, setLead] = useState<Lead | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [companies, setCompanies] = useState<Company[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [statuses, setStatuses] = useState<LeadStatus[]>([]);
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
          // referredByContactId and assignedToId aren't persisted by the backend —
          // recover them from the local stores (kept in sync with the leads list).
          const referredId = found.referredByContactId || loadLeadReferrals()[found.id];
          const ownerId = found.assignedToId || loadLeadAssignments()[found.id];
          setLead({ ...found, referredByContactId: referredId || undefined, assignedToId: ownerId || undefined });
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
    getLeadStatuses().then((v) => { if (!cancelled) setStatuses(v); }).catch(() => { if (!cancelled) setStatuses([]); });
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
  const statusBadgeTone = lead ? STATUS_BADGE_TONE[lead.status] ?? 'bg-slate-100 text-slate-600 border-slate-200' : '';

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
      // The backend response omits referredByContactId (not part of its update
      // contract), so keep the locally-tracked referral rather than dropping it.
      setLead((prev) => (prev ? {
        ...prev,
        ...updated,
        referredByContactId: updated.referredByContactId ?? prev.referredByContactId,
        referredByContactName: updated.referredByContactName ?? prev.referredByContactName,
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
      toast.error('Failed to save');
      return false;
    }
  }, [lead]);

  // Structured 5-phase lifecycle (outreach → meeting → contract → signature →
  // deposit), persisted as JSON on the lead so the whole org shares one view.
  const pipeline = useMemo<LeadPipeline>(() => parsePipeline(lead?.pipelineState), [lead?.pipelineState]);

  const savePipeline = useCallback(
    (next: LeadPipeline, log?: { subject: string; body?: string }) => {
      if (!lead) return;
      const json = serializePipeline(next);
      // Optimistic so the tracker reflects the choice instantly; patchLead then
      // persists it and (with `log`) records the change on the activity timeline.
      setLead((prev) => (prev ? { ...prev, pipelineState: json } : prev));
      void patchLead({ pipelineState: json }, log);
    },
    [lead, patchLead],
  );

  const setStatus = async (status: string) => {
    if (!lead) return;
    const opt = statusOptions.find((s) => s.name === status);
    const patch: Record<string, unknown> = { status };
    if (opt && isValidGuid(opt.id)) patch.leadStatusId = opt.id;
    await patchLead(patch, { subject: `Status set to ${status}`, body: `From "${lead.status}" to "${status}"` });
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
      // referredByContactId isn't part of the backend update contract — mirror
      // it to the shared local store (and local state) like the leads list does.
      const referredByContactId = editForm.referredByContactId || undefined;
      setLeadReferral(lead.id, referredByContactId);
      const referredByContactName = referredByContactId
        ? contacts.find((c) => c.id === referredByContactId)?.name
        : undefined;
      setLead((prev) => (prev ? { ...prev, ...updated, referredByContactId, referredByContactName, assignedToId: prev.assignedToId } : prev));
      createActivity({ type: 'system', subject: 'Lead details updated', leadId: lead.id })
        .then((a) => a && setActivities((prev) => [a, ...prev]))
        .catch(() => { /* non-fatal */ });
      toast.success(messages.success.leadUpdated);
      setEditOpen(false);
    } catch {
      toast.error(messages.errors.generic);
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
          {/* Sticky toolbar */}
          <div className="sticky top-0 z-30 border-b border-slate-200/80 bg-white/80 backdrop-blur-md">
            <div className="flex w-full items-center justify-between gap-3 px-[var(--page-padding)] py-2.5">
              <div className="flex min-w-0 items-center gap-3">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => navigate(backUrl)}
                  className="gap-1.5 text-slate-600 hover:text-slate-900"
                  aria-label="Back to leads"
                >
                  <ArrowLeft className="w-4 h-4" />
                  <span className="hidden sm:inline">Back</span>
                </Button>
                <span className="hidden h-5 w-px bg-slate-200 sm:block" />
                <Link to={backUrl} className="hidden text-sm text-slate-500 hover:text-slate-700 sm:inline">Leads</Link>
                <span className="hidden text-slate-300 sm:inline">/</span>
                <span className="truncate text-sm font-medium text-slate-700">{lead.name}</span>
              </div>
              <div className="flex items-center gap-1.5">
                {lead.email && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setEmailOpen(true)}
                    className="gap-1.5 border-slate-200 bg-white text-slate-700 hover:bg-slate-50"
                  >
                    <Mail className="w-4 h-4" />
                    <span className="hidden sm:inline">Email</span>
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={openEdit}
                  className="gap-1.5 border-indigo-200 bg-white text-indigo-700 hover:bg-indigo-50 hover:text-indigo-800"
                >
                  <Pencil className="w-4 h-4" />
                  <span className="hidden sm:inline">Edit</span>
                </Button>
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setSaveContactOpen(true)}
                  className="gap-1.5 border-violet-200 bg-white text-violet-700 hover:bg-violet-50 hover:text-violet-800"
                >
                  <UserPlus className="w-4 h-4" />
                  <span className="hidden sm:inline">Save as contact</span>
                </Button>
                {!lead.isConverted && (
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => navigate(`/leads?convertLeadId=${lead.id}`)}
                    className="gap-1.5 border-emerald-200 bg-white text-emerald-700 hover:bg-emerald-50 hover:text-emerald-800"
                  >
                    <ArrowRightCircle className="w-4 h-4" />
                    <span className="hidden sm:inline">Convert</span>
                  </Button>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setDeleteOpen(true)}
                  className="gap-1.5 border-red-200 bg-white text-red-600 hover:bg-red-50 hover:text-red-700"
                >
                  <Trash2 className="w-4 h-4" />
                  <span className="hidden sm:inline">Delete</span>
                </Button>
              </div>
            </div>
          </div>

          {/* Hero — status-tinted gradient backdrop with decorative blurs gives the page depth */}
          <section className="relative isolate overflow-hidden border-b border-slate-200/70 bg-gradient-to-br from-white via-slate-50/60 to-white">
            {/* status-tinted radial accent */}
            <div
              aria-hidden
              className={cn(
                'pointer-events-none absolute -top-24 -right-12 h-72 w-72 rounded-full blur-3xl opacity-25',
                'bg-gradient-to-br', gradient,
              )}
            />
            <div aria-hidden className="pointer-events-none absolute -bottom-24 -left-12 h-64 w-64 rounded-full bg-indigo-200/30 blur-3xl" />
            <div aria-hidden className="pointer-events-none absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-slate-200/80 to-transparent" />

            <div className="relative w-full px-[var(--page-padding)] py-7 sm:py-8">
              <div className="flex flex-col gap-6 lg:flex-row lg:items-start lg:justify-between">
                <div className="flex min-w-0 items-start gap-4 sm:gap-5">
                  <div className="relative shrink-0">
                    <div
                      className={cn(
                        'flex h-16 w-16 sm:h-20 sm:w-20 items-center justify-center rounded-2xl bg-gradient-to-br text-white text-xl sm:text-2xl font-bold shadow-lg ring-4 ring-white',
                        gradient,
                      )}
                    >
                      {initialsOf(lead.name) || <UserIcon className="w-7 h-7" />}
                    </div>
                    {lead.isConverted && (
                      <span
                        aria-hidden
                        className="absolute -right-1 -bottom-1 flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-emerald-500 to-teal-500 text-white shadow ring-4 ring-white"
                      >
                        <CheckCircle2 className="w-4 h-4" />
                      </span>
                    )}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-slate-900 leading-tight">
                        {lead.name}
                      </h1>
                      <StatusChangePopover
                        currentStatus={lead.status}
                        statuses={statusOptions.map((s) => s.name)}
                        disabled={lead.isConverted}
                        onChange={setStatus}
                        className={cn(
                          'rounded-lg border px-2.5 py-1 text-xs font-bold shadow-sm ring-1 ring-black/5',
                          statusBadgeTone,
                        )}
                        prefix={<span className={cn('w-2 h-2 rounded-full', {
                          New: 'bg-blue-500', Contacted: 'bg-amber-500', Qualified: 'bg-emerald-500', Lost: 'bg-slate-400',
                        }[lead.status] ?? 'bg-slate-400')} />}
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
                    <div className="mt-2 flex flex-wrap items-center gap-x-4 gap-y-1.5 text-sm text-slate-500">
                      {company && (
                        <Link to={`/companies/${company.id}`} className="inline-flex items-center gap-1.5 hover:text-slate-800">
                          <Building2 className="w-3.5 h-3.5" />
                          {company.name}
                        </Link>
                      )}
                      {lead.email && (
                        <span className="group/copy inline-flex items-center gap-1">
                          <a href={`mailto:${lead.email}`} className="inline-flex items-center gap-1.5 hover:text-blue-700">
                            <Mail className="w-3.5 h-3.5" />
                            {lead.email}
                          </a>
                          <CopyButton label="Copy email" copied={copiedField === 'Email'} onClick={() => copyToClipboard(lead.email!, 'Email')} />
                        </span>
                      )}
                      {lead.phone && (
                        <span className="group/copy inline-flex items-center gap-1">
                          <a href={`tel:${lead.phone}`} className="inline-flex items-center gap-1.5 hover:text-teal-700">
                            <Phone className="w-3.5 h-3.5" />
                            {lead.phone}
                          </a>
                          <CopyButton label="Copy phone" copied={copiedField === 'Phone'} onClick={() => copyToClipboard(lead.phone!, 'Phone')} />
                        </span>
                      )}
                      {assignee && (
                        <span className="inline-flex items-center gap-1.5">
                          <UserIcon className="w-3.5 h-3.5" />
                          {assignee.name}
                        </span>
                      )}
                      {referredByName && (
                        referredContact ? (
                          <Link to={`/contacts/${referredContact.id}`} className="inline-flex items-center gap-1.5 hover:text-violet-700">
                            <UserPlus className="w-3.5 h-3.5" />
                            Referred by {referredByName}
                          </Link>
                        ) : (
                          <span className="inline-flex items-center gap-1.5">
                            <UserPlus className="w-3.5 h-3.5" />
                            Referred by {referredByName}
                          </span>
                        )
                      )}
                    </div>
                    {/* Compact meta strip: timestamps live inline rather than as separate cards */}
                    <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-400">
                      <span className="inline-flex items-center gap-1.5" title={formatFull(lead.lastContactedAt) ?? ''}>
                        <Clock className="w-3 h-3" />
                        Last contacted {formatRelative(lead.lastContactedAt) ?? '—'}
                      </span>
                      <span className="text-slate-200">·</span>
                      <span className="inline-flex items-center gap-1.5" title={formatFull(lead.createdAtUtc) ?? ''}>
                        <Calendar className="w-3 h-3" />
                        Created {formatRelative(lead.createdAtUtc) ?? '—'}
                      </span>
                    </div>
                  </div>
                </div>

                {/* Right side: score gauge + action buttons (stack on mobile, side-by-side on lg) */}
                <div className="flex shrink-0 flex-row items-center gap-5 lg:flex-col lg:items-end lg:gap-4">
                  <ScoreEditor score={lead.leadScore ?? 0} onSave={saveScore} />
                  <div className="flex flex-wrap items-center justify-end gap-2">
                    {lead.email && (
                      <Button asChild size="sm" variant="outline" className="gap-1.5">
                        <a href={`mailto:${lead.email}`}><Mail className="w-4 h-4" /> Email</a>
                      </Button>
                    )}
                    {lead.phone && (
                      <Button asChild size="sm" variant="outline" className="gap-1.5">
                        <a href={`tel:${lead.phone}`}><Phone className="w-4 h-4" /> Call</a>
                      </Button>
                    )}
                    <QuickLogPopover
                      onSubmit={(p) => logActivity(p)}
                      trigger={
                        <Button size="sm" className="gap-1.5 bg-gradient-to-r from-orange-500 to-amber-500 text-white hover:from-orange-600 hover:to-amber-600 shadow shadow-orange-500/20">
                          <MessageSquarePlus className="w-4 h-4" />
                          Log activity
                        </Button>
                      }
                    />
                  </div>
                </div>
              </div>
            </div>
          </section>

          {/* Lead lifecycle pipeline (full-width, under the hero) */}
          <div className="w-full px-[var(--page-padding)] pt-6">
            <LeadPipelineTracker
              value={pipeline}
              disabled={lead.isConverted}
              onChange={savePipeline}
              onConvert={() => navigate(`/leads?convertLeadId=${lead.id}`)}
            />
          </div>

          {/* Body: tabs + sidebar */}
          <div className="grid w-full gap-6 px-[var(--page-padding)] py-6 lg:grid-cols-[1fr_320px]">
            {/* Main column */}
            <div className="min-w-0">
              {/* Tab bar */}
              <div className="sticky top-[52px] z-20 -mx-1 mb-4 flex items-center gap-1 border-b border-slate-200 bg-slate-50/40 px-1 backdrop-blur-sm">
                <TabButton active={tab === 'activity'} onClick={() => setTab('activity')} icon={<MessageSquarePlus className="w-4 h-4" />}>
                  Activity <span className="ml-1.5 text-xs text-slate-400">({activities.length})</span>
                </TabButton>
                <TabButton active={tab === 'tasks'} onClick={() => setTab('tasks')} icon={<CheckCircle2 className="w-4 h-4" />}>
                  Tasks {openTaskCount > 0 && (
                    <span className="ml-1.5 inline-flex items-center justify-center rounded-full bg-orange-100 px-1.5 text-[10px] font-bold text-orange-700">{openTaskCount}</span>
                  )}
                </TabButton>
                <TabButton active={tab === 'notes'} onClick={() => setTab('notes')} icon={<FileText className="w-4 h-4" />}>
                  Notes <span className="ml-1.5 text-xs text-slate-400">({noteActivities.length})</span>
                </TabButton>
              </div>

              {tab === 'activity' && (
                <div className="space-y-5">
                  {/* Quick note composer */}
                  <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                    <label className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Add a note</label>
                    <textarea
                      value={noteDraft}
                      onChange={(e) => setNoteDraft(e.target.value)}
                      onKeyDown={(e) => {
                        if ((e.metaKey || e.ctrlKey) && e.key === 'Enter') { e.preventDefault(); handleAddNote(); }
                      }}
                      rows={2}
                      placeholder="Capture a quick thought — Cmd/Ctrl+Enter to save."
                      className="mt-1.5 w-full resize-none rounded-lg border border-slate-200 bg-white px-3 py-2 text-sm placeholder:text-slate-400 focus:border-indigo-400 focus:outline-none focus:ring-2 focus:ring-indigo-400/30"
                    />
                    <div className="mt-2 flex items-center justify-end">
                      <Button size="sm" onClick={handleAddNote} disabled={!noteDraft.trim() || savingNote} className="gap-1.5">
                        {savingNote ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        Save note
                      </Button>
                    </div>
                  </div>

                  {/* Email composer */}
                  {lead.email && (
                    <details className="rounded-2xl border border-slate-200 bg-white shadow-sm group">
                      <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-4 py-3 text-sm font-semibold text-slate-700 hover:bg-slate-50">
                        <span className="inline-flex items-center gap-2">
                          <Mail className="w-4 h-4 text-purple-600" />
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
                          <Button size="sm" onClick={handleSendEmail} disabled={!emailBody.trim() || sendingEmail} className="gap-1.5 bg-purple-600 hover:bg-purple-700">
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
                  <div className="rounded-2xl border border-slate-200 bg-white p-4 shadow-sm">
                    <label className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">New task</label>
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
                      <Button size="sm" onClick={handleAddTask} disabled={!newTaskTitle.trim() || creatingTask} className="gap-1.5">
                        {creatingTask ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Plus className="w-3.5 h-3.5" />}
                        Add
                      </Button>
                    </div>
                  </div>

                  {tasks.length === 0 ? (
                    <div className="rounded-2xl border border-dashed border-slate-200 bg-slate-50/40 px-6 py-10 text-center text-sm text-slate-500">
                      No tasks yet. Add one above to get started.
                    </div>
                  ) : (
                    <ul className="space-y-2">
                      {sortedTasks.map((t) => {
                        const overdue = t.status !== 'completed' && isOverdue(t.dueDateUtc);
                        return (
                          <li key={t.id} className={cn(
                            'group flex items-start gap-3 rounded-2xl border bg-white px-4 py-3 transition-colors',
                            overdue ? 'border-red-200' : 'border-slate-200',
                            t.status === 'completed' && 'opacity-60',
                          )}>
                            <button
                              type="button"
                              onClick={() => toggleTask(t)}
                              aria-label={t.status === 'completed' ? 'Mark task as not done' : 'Mark task as done'}
                              className={cn(
                                'mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border-2 transition-colors',
                                t.status === 'completed'
                                  ? 'border-emerald-500 bg-emerald-500 text-white'
                                  : 'border-slate-300 bg-white hover:border-emerald-400',
                              )}
                            >
                              {t.status === 'completed' && <CheckCircle2 className="w-3 h-3" />}
                            </button>
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
                                'text-[10px] font-semibold uppercase tracking-wider rounded-md px-1.5 py-0.5',
                                t.priority === 'high' ? 'bg-red-50 text-red-600' : t.priority === 'medium' ? 'bg-amber-50 text-amber-700' : 'bg-slate-100 text-slate-500',
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
                          </li>
                        );
                      })}
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
                    placeholder="Add a longer description, context, or qualification notes…"
                    emptyHint="Click to add a description"
                  />
                  <ActivityTimeline
                    activities={noteActivities}
                    emptyHint="No notes yet. Click ‘Add a note’ on the Activity tab."
                  />
                </div>
              )}
            </div>

            {/* Sidebar — single card with section dividers, sticky on lg+ so it
                stays in view as the user scrolls the activity timeline. */}
            <aside className="lg:sticky lg:top-[64px] lg:self-start lg:max-h-[calc(100vh-80px)] lg:overflow-y-auto">
              {/* AI assist — derived engagement score + recommended next action. */}
              <AiNextActionCard
                lead={lead}
                activities={activities}
                tasks={tasks}
                className="mb-4 overflow-hidden rounded-2xl border border-indigo-200/70 bg-gradient-to-br from-indigo-50/80 to-white shadow-sm"
                onAct={(kind) => {
                  if (kind === 'convert') navigate(`/leads?convertLeadId=${lead.id}`);
                  else if (kind === 'task') setTab('tasks');
                  else setTab('activity');
                }}
              />

              {/* Next step — the most urgent open task, surfaced so the next
                  follow-up is always one glance (and one click) away. */}
              <div className={cn(
                'mb-4 overflow-hidden rounded-2xl border bg-white shadow-sm',
                nextTask && isOverdue(nextTask.dueDateUtc) ? 'border-red-200' : 'border-slate-200',
              )}>
                <div className="flex items-center justify-between border-b border-slate-100 px-4 py-2.5">
                  <h3 className="text-[11px] font-semibold uppercase tracking-wider text-slate-500">Next step</h3>
                  <button
                    type="button"
                    onClick={() => setTab('tasks')}
                    className="text-[11px] font-medium text-indigo-600 hover:text-indigo-800"
                  >
                    {nextTask ? 'All tasks' : 'Add task'}
                  </button>
                </div>
                <div className="p-4">
                  {nextTask ? (
                    <div className="flex items-start gap-3">
                      <button
                        type="button"
                        onClick={() => toggleTask(nextTask)}
                        aria-label="Mark next step done"
                        className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-md border-2 border-slate-300 bg-white transition-colors hover:border-emerald-400"
                      />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm font-medium text-slate-800">{nextTask.title}</p>
                        {nextTask.dueDateUtc ? (
                          <p className={cn(
                            'mt-0.5 inline-flex items-center gap-1 text-xs',
                            isOverdue(nextTask.dueDateUtc) ? 'font-semibold text-red-600' : 'text-slate-500',
                          )}>
                            {isOverdue(nextTask.dueDateUtc) ? <AlertCircle className="w-3 h-3" /> : <Clock className="w-3 h-3" />}
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
                      className="flex w-full items-center gap-2 rounded-lg border border-dashed border-slate-200 px-3 py-2.5 text-left text-sm text-slate-500 transition-colors hover:border-indigo-300 hover:bg-indigo-50/40 hover:text-indigo-700"
                    >
                      <Plus className="w-4 h-4" />
                      Add a follow-up task
                    </button>
                  )}
                </div>
              </div>

              <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                {/* Contact section */}
                <SidebarSection title="Contact">
                  <div className="space-y-3">
                    <InlineField label="Name" value={lead.name} onSave={saveName} icon={<UserIcon className="w-3.5 h-3.5" />} />
                    <InlineField label="Email" type="email" value={lead.email ?? ''} onSave={saveEmail} icon={<Mail className="w-3.5 h-3.5" />} />
                    <InlineField label="Phone" type="tel" value={lead.phone ?? ''} onSave={savePhone} icon={<Phone className="w-3.5 h-3.5" />} />
                    <SidebarSelectField label="Company" icon={<Building2 className="w-3.5 h-3.5 text-slate-400" />}>
                      <Select value={lead.companyId ?? ''} onValueChange={(v) => setCompanyId(v === '__none__' ? '' : v)}>
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
                  </div>
                </SidebarSection>

                {/* Details section */}
                <SidebarSection title="Details">
                  <div className="space-y-3">
                    <SidebarSelectField label="Source" icon={<Sparkles className="w-3.5 h-3.5 text-orange-400" />}>
                      <Select value={lead.source ?? ''} onValueChange={setSource}>
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
                    <SidebarSelectField label="Lifecycle stage">
                      <Select value={lead.lifecycleStage ?? ''} onValueChange={setLifecycle}>
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
                </SidebarSection>

                {/* Owner section */}
                <SidebarSection title="Owner" last>
                  <Select value={lead.assignedToId ?? '__none__'} onValueChange={(v) => setAssignment(v === '__none__' ? '' : v)}>
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
                  {assignee && (
                    <div className="mt-2 flex items-center gap-2">
                      <div className="flex h-7 w-7 items-center justify-center rounded-full bg-gradient-to-br from-indigo-500 to-purple-500 text-white text-[11px] font-semibold ring-2 ring-white shadow-sm">
                        {(assignee.name?.[0] ?? '?').toUpperCase()}
                      </div>
                      <div className="min-w-0">
                        <p className="truncate text-xs font-medium text-slate-700">{assignee.name}</p>
                        <p className="truncate text-[11px] text-slate-400">{assignee.email}</p>
                      </div>
                    </div>
                  )}
                </SidebarSection>
              </div>

              {/* Tags intentionally not surfaced here yet: the backend's
                  UpdateLeadRequest has no Tags field, so any add/remove would
                  silently fail to persist. Re-enable once the backend gains
                  Lead.Tags support. */}

              {/* Custom fields (renders only when defined for leads) */}
              <CustomFieldsCard entityType="lead" recordId={lead.id} className="mt-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" />

              {/* Attachments */}
              <AttachmentsCard entityType="lead" recordId={lead.id} className="mt-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm" />
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

function TabButton({
  active, onClick, icon, children,
}: { active: boolean; onClick: () => void; icon: React.ReactNode; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        'relative flex items-center gap-1.5 rounded-t-lg px-3 py-2 text-sm font-medium transition-colors',
        active
          ? 'text-indigo-700'
          : 'text-slate-500 hover:text-slate-800 hover:bg-slate-100/60',
      )}
      aria-current={active ? 'page' : undefined}
    >
      {icon}
      {children}
      <span
        aria-hidden
        className={cn(
          'absolute inset-x-2 -bottom-px h-0.5 rounded-full bg-gradient-to-r from-indigo-500 to-purple-500 transition-opacity duration-200',
          active ? 'opacity-100' : 'opacity-0',
        )}
      />
    </button>
  );
}

/**
 * Sidebar section with a small uppercase header, separated from the next
 * section by a thin divider rather than living in its own white card. Keeps
 * the right rail visually calm while still grouping fields.
 */
function SidebarSection({
  title, children, last,
}: { title: string; children: React.ReactNode; last?: boolean }) {
  return (
    <div className={cn('p-4', !last && 'border-b border-slate-100')}>
      <h3 className="mb-3 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
        {title}
      </h3>
      {children}
    </div>
  );
}

/** Labelled wrapper for sidebar <Select> rows so the label has the same
 *  weight & icon style as the click-to-edit InlineField rows above it. */
function SidebarSelectField({
  label, icon, children,
}: { label: string; icon?: React.ReactNode; children: React.ReactNode }) {
  return (
    <div>
      <div className="mb-1 flex items-center gap-1.5 text-[11px] font-semibold uppercase tracking-wider text-slate-500">
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
function ScoreEditor({ score, onSave }: { score: number; onSave: (v: string) => Promise<boolean> }) {
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
      onClick={() => setEditing(true)}
      aria-label="Edit lead score"
      className="group relative transition-transform hover:scale-[1.03] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-indigo-400/40 rounded-full"
    >
      <ScoreGauge score={score} />
      <span className="absolute -bottom-1 -right-1 flex h-6 w-6 items-center justify-center rounded-full bg-white text-slate-400 opacity-0 shadow ring-1 ring-slate-200 transition-opacity group-hover:opacity-100">
        <Pencil className="w-3 h-3" />
      </span>
    </button>
  );
}

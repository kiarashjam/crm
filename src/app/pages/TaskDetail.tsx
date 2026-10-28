import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeft,
  CheckCircle2,
  Circle,
  Clock,
  Calendar,
  User,
  Briefcase,
  Target,
  MessageSquare,
  Send,
  Trash2,
  Pencil,
  Flag,
  AlertCircle,
  Loader2,
} from 'lucide-react';
import { toast } from 'sonner';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import AppHeader from '@/app/components/AppHeader';
import { RecordSkeleton } from '@/app/components/PageSkeleton';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getTaskById,
  updateTask,
  deleteTask,
  getTaskComments,
  addTaskComment,
  deleteTaskComment,
} from '@/app/api/tasks';
import type { TaskCommentDto } from '@/app/api/tasks';
import type { TaskItem, TaskStatusType, TaskPriorityType } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Textarea } from '@/app/components/ui/textarea';
import { Label } from '@/app/components/ui/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import { cn } from '@/app/components/ui/utils';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { RecordCard } from '@/app/components/RecordCard';
import { StaggerItem } from '@/app/components/motion/Stagger';

const STATUS_OPTIONS: { value: TaskStatusType; label: string; icon: typeof Circle; color: string }[] = [
  { value: 'todo', label: 'To Do', icon: Circle, color: 'text-slate-500' },
  { value: 'in_progress', label: 'In Progress', icon: Clock, color: 'text-blue-500' },
  { value: 'completed', label: 'Completed', icon: CheckCircle2, color: 'text-emerald-500' },
  { value: 'cancelled', label: 'Cancelled', icon: AlertCircle, color: 'text-red-400' },
];

const PRIORITY_OPTIONS: { value: TaskPriorityType; label: string; color: string }[] = [
  { value: 'none', label: 'None', color: 'text-slate-500' },
  { value: 'low', label: 'Low', color: 'text-blue-500' },
  { value: 'medium', label: 'Medium', color: 'text-amber-500' },
  { value: 'high', label: 'High', color: 'text-red-700' },
];

function timeAgo(iso: string): string {
  const d = new Date(iso);
  const now = new Date();
  const diff = (now.getTime() - d.getTime()) / 1000;
  if (diff < 60) return 'just now';
  if (diff < 3600) return `${Math.floor(diff / 60)}m ago`;
  if (diff < 86400) return `${Math.floor(diff / 3600)}h ago`;
  if (diff < 604800) return `${Math.floor(diff / 86400)}d ago`;
  return d.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
}

export default function TaskDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [task, setTask] = useState<TaskItem | null>(null);
  useDocumentTitle(task?.title, 'Tasks');
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);

  // Edit mode
  const [editing, setEditing] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editNotes, setEditNotes] = useState('');
  const [editDueDate, setEditDueDate] = useState('');
  const [saving, setSaving] = useState(false);

  // Comments
  const [comments, setComments] = useState<TaskCommentDto[]>([]);
  const [commentsLoading, setCommentsLoading] = useState(false);
  const [commentText, setCommentText] = useState('');
  const [addingComment, setAddingComment] = useState(false);

  // Delete confirmation
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  const loadTask = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      const t = await getTaskById(id);
      setTask(t);
      if (t) {
        setEditTitle(t.title);
        setEditDescription(t.description || '');
        setEditNotes(t.notes || '');
        setEditDueDate(t.dueDateUtc ? t.dueDateUtc.slice(0, 16) : ''); // Preserve time for datetime-local input
      }
    } catch {
      toast.error('Failed to load task');
    } finally {
      setLoading(false);
    }
  }, [id]);

  const loadComments = useCallback(async () => {
    if (!id) return;
    setCommentsLoading(true);
    try {
      const c = await getTaskComments(id);
      setComments(c);
    } catch {
      // ignore
    } finally {
      setCommentsLoading(false);
    }
  }, [id]);

  useEffect(() => { loadTask(); }, [loadTask]);
  useEffect(() => { loadComments(); }, [loadComments]);

  async function handleStatusChange(status: TaskStatusType) {
    if (!task || !id) return;
    try {
      const updated = await updateTask(id, { status });
      if (updated) { setTask(updated); toast.success('Status updated'); }
    } catch {
      toast.error('Failed to update status');
    }
  }

  async function handlePriorityChange(priority: TaskPriorityType) {
    if (!task || !id) return;
    try {
      const updated = await updateTask(id, { priority });
      if (updated) { setTask(updated); toast.success('Priority updated'); }
    } catch {
      toast.error('Failed to update priority');
    }
  }

  async function handleSaveEdit() {
    if (!task || !id) return;
    setSaving(true);
    try {
      const updated = await updateTask(id, {
        title: editTitle.trim() || task.title,
        description: editDescription.trim() || undefined,
        notes: editNotes.trim() || undefined,
        dueDateUtc: editDueDate || undefined,
      });
      if (updated) { setTask(updated); setEditing(false); toast.success('Task updated'); }
    } catch {
      toast.error('Failed to update task');
    } finally {
      setSaving(false);
    }
  }

  async function handleDelete() {
    if (!id) return;
    setDeleting(true);
    try {
      await deleteTask(id);
      toast.success('Task deleted');
      navigate('/tasks');
    } catch {
      toast.error('Failed to delete task');
    } finally {
      setDeleting(false);
    }
  }

  async function handleAddComment() {
    if (!id || !commentText.trim()) return;
    setAddingComment(true);
    try {
      const c = await addTaskComment(id, commentText.trim());
      if (c) { setComments(prev => [...prev, c]); setCommentText(''); }
    } catch {
      toast.error('Failed to add comment');
    } finally {
      setAddingComment(false);
    }
  }

  async function handleDeleteComment(commentId: string) {
    if (!id) return;
    try {
      await deleteTaskComment(id, commentId);
      setComments(prev => prev.filter(c => c.id !== commentId));
      toast.success('Comment deleted');
    } catch {
      toast.error('Failed to delete comment');
    }
  }

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <RecordSkeleton />
        </main>
      </div>
    );
  }


  if (!task) {
    return (
      <div className="min-h-screen bg-gradient-subtle">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="mx-auto max-w-4xl px-6 py-10">
          <div className="text-center py-20">
            <h2 className="text-xl font-semibold text-slate-700">Task not found</h2>
            <Button variant="outline" className="mt-4" onClick={() => navigate('/tasks')}>
              <ArrowLeft className="w-4 h-4 mr-2" /> Back to Tasks
            </Button>
          </div>
        </main>
      </div>
    );
  }

  const statusInfo = STATUS_OPTIONS.find(s => s.value === task.status) ?? STATUS_OPTIONS[0]!;
  const priorityInfo = PRIORITY_OPTIONS.find(p => p.value === task.priority) ?? PRIORITY_OPTIONS[0]!;
  const StatusIcon = statusInfo.icon;
  const due = task.dueDateUtc ? new Date(task.dueDateUtc) : null;
  const overdue = !!due && due < new Date() && task.status !== 'completed';
  const dueLabel = due ? due.toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' }) : null;
  const daysLeft = due ? Math.ceil((due.getTime() - Date.now()) / 86400000) : null;
  const dueHint = daysLeft === null || task.status === 'completed'
    ? undefined
    : daysLeft < 0 ? `${-daysLeft}d overdue` : daysLeft === 0 ? 'today' : `in ${daysLeft}d`;

  return (
    <div className="min-h-screen bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]">
          <PageHero
            icon={StatusIcon}
            tone="amber"
            back={{ to: '/tasks', label: 'Back to Tasks' }}
            title={task.title}
            badges={
              <>
                <span className="inline-flex items-center gap-1 rounded-full bg-white/80 px-2 py-0.5 text-[11px] font-semibold text-slate-700 ring-1 ring-slate-200">
                  <StatusIcon className={cn('h-3 w-3', statusInfo.color)} aria-hidden />
                  {statusInfo.label}
                </span>
                {overdue && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-gradient-to-r from-red-500 to-rose-500 px-2 py-0.5 text-[11px] font-bold text-white">
                    <AlertCircle className="h-3 w-3" aria-hidden /> Overdue
                  </span>
                )}
              </>
            }
            subtitle={task.description || undefined}
            actions={!editing && (
              <WriteOnly>
                <Button variant="outline" onClick={() => setEditing(true)} className={heroSecondary}>
                  <Pencil className="h-4 w-4" /> Edit
                </Button>
                <Button
                  variant="outline"
                  onClick={() => setDeleteOpen(true)}
                  className={`${heroSecondary} text-red-600 hover:border-red-200 hover:bg-red-50 hover:text-red-700`}
                >
                  <Trash2 className="h-4 w-4" /> Delete
                </Button>
              </WriteOnly>
            )}
            stats={[
              { label: 'Due', value: dueLabel ?? '—', icon: Calendar, tone: overdue ? 'rose' : 'amber', hint: dueHint },
              { label: 'Priority', value: priorityInfo.label, icon: Flag, tone: task.priority === 'high' ? 'rose' : task.priority === 'medium' ? 'amber' : 'sky' },
              { label: 'Assignee', value: task.assigneeName || 'Nobody', icon: User, tone: 'violet' },
              { label: 'Comments', value: comments.length, icon: MessageSquare, tone: 'emerald', hint: comments.length > 0 ? `last ${timeAgo(comments[comments.length - 1]!.createdAtUtc)}` : 'none yet' },
            ]}
          >
            {/* Status and priority, changed in place. */}
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <Label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">Status</Label>
                <Select value={task.status} onValueChange={(v) => handleStatusChange(v as TaskStatusType)}>
                  <SelectTrigger aria-label="Status" className="h-9 w-[160px] rounded-xl border-slate-200/80 bg-white/80 shadow-sm backdrop-blur">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {STATUS_OPTIONS.map(s => (
                      <SelectItem key={s.value} value={s.value}>
                        <span className="flex items-center gap-2">
                          <s.icon className={cn('w-4 h-4', s.color)} />
                          {s.label}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label className="mb-1 block text-[11px] font-semibold uppercase tracking-wider text-slate-500">Priority</Label>
                <Select value={task.priority} onValueChange={(v) => handlePriorityChange(v as TaskPriorityType)}>
                  <SelectTrigger aria-label="Priority" className="h-9 w-[140px] rounded-xl border-slate-200/80 bg-white/80 shadow-sm backdrop-blur">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {PRIORITY_OPTIONS.map(p => (
                      <SelectItem key={p.value} value={p.value}>
                        <span className="flex items-center gap-2">
                          <Flag className={cn('w-4 h-4', p.color)} />
                          {p.label}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
          </PageHero>

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="min-w-0 space-y-5">
              {editing && (
                <RecordCard title="Edit task" icon={<Pencil className="h-3.5 w-3.5" />} tone="amber" index={0} className="border-amber-200">
                  <div className="space-y-4">
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Title</Label>
                      <Input aria-label="Title" value={editTitle} onChange={e => setEditTitle(e.target.value)} className="text-lg font-semibold" />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Description</Label>
                      <Textarea aria-label="Description" value={editDescription} onChange={e => setEditDescription(e.target.value)} rows={3} placeholder="Task description..." />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Notes</Label>
                      <Textarea aria-label="Notes" value={editNotes} onChange={e => setEditNotes(e.target.value)} rows={3} placeholder="Internal notes..." />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Due Date</Label>
                      <Input aria-label="Due Date" type="date" value={editDueDate} onChange={e => setEditDueDate(e.target.value)} className="w-48" />
                    </div>
                    <div className="flex gap-2">
                      <Button onClick={handleSaveEdit} disabled={saving} size="sm" className="rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 text-white hover:opacity-95">
                        {saving ? <Loader2 className="w-4 h-4 animate-spin mr-1" /> : null} Save
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setEditing(false)}>Cancel</Button>
                    </div>
                  </div>
                </RecordCard>
              )}

              {!editing && task.notes && (
                <RecordCard title="Notes" icon={<Pencil className="h-3.5 w-3.5" />} tone="amber" index={0}>
                  <p className="whitespace-pre-wrap rounded-xl border-l-4 border-amber-300 bg-amber-50/70 px-3 py-2 text-sm text-amber-900">{task.notes}</p>
                </RecordCard>
              )}

              <RecordCard title="Comments" icon={<MessageSquare className="h-3.5 w-3.5" />} tone="emerald" count={comments.length} index={1}>
                <ul className="mb-4 space-y-2">
                  {commentsLoading && (
                    <li className="flex items-center justify-center gap-2 py-4 text-sm text-slate-500">
                      <Loader2 className="w-4 h-4 animate-spin" /> Loading comments...
                    </li>
                  )}
                  {!commentsLoading && comments.length === 0 && (
                    <li className="py-4 text-center text-sm text-slate-500">No comments yet. Start a discussion!</li>
                  )}
                  {comments.map((c, index) => (
                    <StaggerItem as="li" key={c.id} index={index} className="group flex gap-3 rounded-xl p-3 transition-colors hover:bg-slate-50">
                      <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-gradient-to-br from-orange-400 to-pink-500 text-xs font-semibold text-white shadow-sm">
                        {c.authorName.charAt(0).toUpperCase()}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="mb-1 flex items-center gap-2">
                          <span className="text-sm font-medium text-slate-900">{c.authorName}</span>
                          <span className="text-xs text-slate-500">{timeAgo(c.createdAtUtc)}</span>
                          <button
                            onClick={() => handleDeleteComment(c.id)}
                            className="ml-auto text-slate-500 opacity-0 transition-all hover:text-red-500 group-hover:opacity-100"
                            title="Delete comment"
                          >
                            <Trash2 className="w-3.5 h-3.5" />
                          </button>
                        </div>
                        <p className="whitespace-pre-wrap text-sm text-slate-700">{c.body}</p>
                      </div>
                    </StaggerItem>
                  ))}
                </ul>

                <div className="flex gap-3 border-t border-slate-100 pt-4">
                  <Textarea
                    value={commentText}
                    onChange={e => setCommentText(e.target.value)}
                    placeholder="Write a comment..."
                    rows={2}
                    className="flex-1 resize-none focus-visible:border-emerald-300"
                    onKeyDown={(e) => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) handleAddComment(); }}
                  />
                  <WriteOnly>
                    <Button
                      onClick={handleAddComment}
                      disabled={addingComment || !commentText.trim()}
                      size="sm"
                      aria-label="Send comment"
                      className="self-end rounded-xl bg-gradient-to-r from-emerald-500 to-teal-500 text-white hover:opacity-95"
                    >
                      {addingComment ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
                    </Button>
                  </WriteOnly>
                </div>
                <p className="mt-2 text-xs text-slate-500">Press Ctrl+Enter to send</p>
              </RecordCard>
            </div>

            <aside className="min-w-0 space-y-5">
              <RecordCard title="Dates" icon={<Calendar className="h-3.5 w-3.5" />} tone="amber" index={2}>
                <dl className="space-y-3 text-sm">
                  <div className="flex justify-between">
                    <dt className="text-slate-500">Created</dt>
                    <dd className="text-slate-900">{task.createdAtUtc ? new Date(task.createdAtUtc).toLocaleDateString() : '—'}</dd>
                  </div>
                  {task.dueDateUtc && (
                    <div className="flex justify-between">
                      <dt className="text-slate-500">Due Date</dt>
                      <dd className={cn('font-medium', overdue ? 'text-red-600' : 'text-slate-900')}>
                        {new Date(task.dueDateUtc).toLocaleDateString()}
                      </dd>
                    </div>
                  )}
                  {task.completedAtUtc && (
                    <div className="flex justify-between">
                      <dt className="text-slate-500">Completed</dt>
                      <dd className="text-emerald-700">{new Date(task.completedAtUtc).toLocaleDateString()}</dd>
                    </div>
                  )}
                  {task.updatedAtUtc && (
                    <div className="flex justify-between">
                      <dt className="text-slate-500">Last Updated</dt>
                      <dd className="text-slate-700">{timeAgo(task.updatedAtUtc)}</dd>
                    </div>
                  )}
                </dl>
              </RecordCard>

              <RecordCard title="Linked to" icon={<Target className="h-3.5 w-3.5" />} tone="violet" index={3}>
                <dl className="space-y-3 text-sm">
                  {task.assigneeName && (
                    <div className="flex items-center gap-2">
                      <User className="w-4 h-4 text-blue-500" />
                      <span className="text-slate-500">Assignee:</span>
                      <span className="font-medium text-slate-900">{task.assigneeName}</span>
                    </div>
                  )}
                  {task.dealId && (
                    <div className="flex items-center gap-2">
                      <Briefcase className="w-4 h-4 text-violet-500" />
                      <span className="text-slate-500">Deal:</span>
                      <Link to={`/deals/${task.dealId}`} className="font-medium text-violet-700 hover:underline">
                        {task.dealName || 'View Deal'}
                      </Link>
                    </div>
                  )}
                  {task.leadId && (
                    <div className="flex items-center gap-2">
                      <Target className="w-4 h-4 text-orange-500" />
                      <span className="text-slate-500">Lead:</span>
                      <Link to={`/leads/${task.leadId}`} className="font-medium text-orange-700 hover:underline">
                        {task.leadName || 'View Lead'}
                      </Link>
                    </div>
                  )}
                  {task.contactId && (
                    <div className="flex items-center gap-2">
                      <User className="w-4 h-4 text-emerald-500" />
                      <span className="text-slate-500">Contact:</span>
                      <Link to={`/contacts/${task.contactId}`} className="font-medium text-emerald-700 hover:underline">
                        {task.contactName || 'View Contact'}
                      </Link>
                    </div>
                  )}
                  {!task.assigneeName && !task.dealId && !task.leadId && !task.contactId && (
                    <p className="text-sm text-slate-500">No linked entities</p>
                  )}
                </dl>
              </RecordCard>
            </aside>
          </div>

          {/* Delete confirmation */}
          <AlertDialog open={deleteOpen} onOpenChange={setDeleteOpen}>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete Task</AlertDialogTitle>
                <AlertDialogDescription>
                  Are you sure you want to delete "{task.title}"? This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={handleDelete} className="bg-red-600 hover:bg-red-700" disabled={deleting}>
                  {deleting ? 'Deleting...' : 'Delete'}
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        </main>
      </PageTransition>
    </div>
  );
}

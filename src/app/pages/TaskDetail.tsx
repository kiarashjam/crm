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
import AppHeader from '@/app/components/AppHeader';
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

const STATUS_OPTIONS: { value: TaskStatusType; label: string; icon: typeof Circle; color: string }[] = [
  { value: 'todo', label: 'To Do', icon: Circle, color: 'text-slate-400' },
  { value: 'in_progress', label: 'In Progress', icon: Clock, color: 'text-blue-500' },
  { value: 'completed', label: 'Completed', icon: CheckCircle2, color: 'text-emerald-500' },
  { value: 'cancelled', label: 'Cancelled', icon: AlertCircle, color: 'text-red-400' },
];

const PRIORITY_OPTIONS: { value: TaskPriorityType; label: string; color: string }[] = [
  { value: 'none', label: 'None', color: 'text-slate-400' },
  { value: 'low', label: 'Low', color: 'text-blue-500' },
  { value: 'medium', label: 'Medium', color: 'text-amber-500' },
  { value: 'high', label: 'High', color: 'text-red-500' },
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
  const [loading, setLoading] = useState(true);

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
      <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-orange-50">
        <AppHeader />
        <main id={MAIN_CONTENT_ID} className="mx-auto max-w-4xl px-6 py-10">
          <div className="flex items-center justify-center py-20">
            <Loader2 className="w-8 h-8 animate-spin text-orange-500" />
          </div>
        </main>
      </div>
    );
  }

  if (!task) {
    return (
      <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-orange-50">
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
  const _priorityInfo = PRIORITY_OPTIONS.find(p => p.value === task.priority) ?? PRIORITY_OPTIONS[0];
  const StatusIcon = statusInfo!.icon;

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-orange-50">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="mx-auto max-w-4xl px-6 py-8">
          {/* Back nav */}
          <button
            onClick={() => navigate('/tasks')}
            className="inline-flex items-center gap-2 text-sm text-slate-500 hover:text-orange-600 mb-6 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" /> Back to Tasks
          </button>

          {/* Header */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6 mb-6">
            <div className="flex items-start justify-between gap-4">
              <div className="flex-1 min-w-0">
                {editing ? (
                  <div className="space-y-4">
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Title</Label>
                      <Input value={editTitle} onChange={e => setEditTitle(e.target.value)} className="text-lg font-semibold" />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Description</Label>
                      <Textarea value={editDescription} onChange={e => setEditDescription(e.target.value)} rows={3} placeholder="Task description..." />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Notes</Label>
                      <Textarea value={editNotes} onChange={e => setEditNotes(e.target.value)} rows={3} placeholder="Internal notes..." />
                    </div>
                    <div>
                      <Label className="text-xs text-slate-500 mb-1">Due Date</Label>
                      <Input type="date" value={editDueDate} onChange={e => setEditDueDate(e.target.value)} className="w-48" />
                    </div>
                    <div className="flex gap-2">
                      <Button onClick={handleSaveEdit} disabled={saving} size="sm">
                        {saving ? <Loader2 className="w-4 h-4 animate-spin mr-1" /> : null} Save
                      </Button>
                      <Button variant="outline" size="sm" onClick={() => setEditing(false)}>Cancel</Button>
                    </div>
                  </div>
                ) : (
                  <>
                    <div className="flex items-center gap-3 mb-2">
                      <StatusIcon className={cn('w-6 h-6', statusInfo!.color)} />
                      <h1 className="text-2xl font-bold text-slate-900 truncate">{task.title}</h1>
                    </div>
                    {task.description && <p className="text-slate-600 mt-1">{task.description}</p>}
                    {task.notes && (
                      <div className="mt-3 p-3 bg-amber-50 rounded-lg border border-amber-100">
                        <p className="text-sm text-amber-800"><strong>Notes:</strong> {task.notes}</p>
                      </div>
                    )}
                  </>
                )}
              </div>
              {!editing && (
                <div className="flex gap-2 shrink-0">
                  <Button variant="outline" size="sm" onClick={() => setEditing(true)}>
                    <Pencil className="w-4 h-4 mr-1" /> Edit
                  </Button>
                  <Button variant="outline" size="sm" className="text-red-600 hover:bg-red-50" onClick={() => setDeleteOpen(true)}>
                    <Trash2 className="w-4 h-4 mr-1" /> Delete
                  </Button>
                </div>
              )}
            </div>

            {/* Status & Priority selectors */}
            <div className="flex flex-wrap gap-4 mt-6 pt-4 border-t border-slate-100">
              <div>
                <Label className="text-xs text-slate-500 mb-1.5 block">Status</Label>
                <Select value={task.status} onValueChange={(v) => handleStatusChange(v as TaskStatusType)}>
                  <SelectTrigger className="w-[160px] h-9">
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
                <Label className="text-xs text-slate-500 mb-1.5 block">Priority</Label>
                <Select value={task.priority} onValueChange={(v) => handlePriorityChange(v as TaskPriorityType)}>
                  <SelectTrigger className="w-[140px] h-9">
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
          </div>

          {/* Details cards */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
            {/* Dates */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
              <h3 className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-4">
                <Calendar className="w-4 h-4 text-orange-500" /> Dates
              </h3>
              <dl className="space-y-3 text-sm">
                <div className="flex justify-between">
                  <dt className="text-slate-500">Created</dt>
                  <dd className="text-slate-900">{task.createdAtUtc ? new Date(task.createdAtUtc).toLocaleDateString() : '—'}</dd>
                </div>
                {task.dueDateUtc && (
                  <div className="flex justify-between">
                    <dt className="text-slate-500">Due Date</dt>
                    <dd className={cn('font-medium', new Date(task.dueDateUtc) < new Date() && task.status !== 'completed' ? 'text-red-600' : 'text-slate-900')}>
                      {new Date(task.dueDateUtc).toLocaleDateString()}
                    </dd>
                  </div>
                )}
                {task.completedAtUtc && (
                  <div className="flex justify-between">
                    <dt className="text-slate-500">Completed</dt>
                    <dd className="text-emerald-600">{new Date(task.completedAtUtc).toLocaleDateString()}</dd>
                  </div>
                )}
                {task.updatedAtUtc && (
                  <div className="flex justify-between">
                    <dt className="text-slate-500">Last Updated</dt>
                    <dd className="text-slate-700">{timeAgo(task.updatedAtUtc)}</dd>
                  </div>
                )}
              </dl>
            </div>

            {/* Linked Entities */}
            <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5">
              <h3 className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-4">
                <Target className="w-4 h-4 text-orange-500" /> Linked To
              </h3>
              <dl className="space-y-3 text-sm">
                {task.assigneeName && (
                  <div className="flex items-center gap-2">
                    <User className="w-4 h-4 text-blue-500" />
                    <span className="text-slate-500">Assignee:</span>
                    <span className="text-slate-900 font-medium">{task.assigneeName}</span>
                  </div>
                )}
                {task.dealId && (
                  <div className="flex items-center gap-2">
                    <Briefcase className="w-4 h-4 text-emerald-500" />
                    <span className="text-slate-500">Deal:</span>
                    <Link to={`/deals/${task.dealId}`} className="text-orange-600 hover:underline font-medium">
                      {task.dealName || 'View Deal'}
                    </Link>
                  </div>
                )}
                {task.leadId && (
                  <div className="flex items-center gap-2">
                    <Target className="w-4 h-4 text-violet-500" />
                    <span className="text-slate-500">Lead:</span>
                    <span className="text-slate-900 font-medium">{task.leadName || task.leadId}</span>
                  </div>
                )}
                {task.contactId && (
                  <div className="flex items-center gap-2">
                    <User className="w-4 h-4 text-cyan-500" />
                    <span className="text-slate-500">Contact:</span>
                    <Link to={`/contacts/${task.contactId}`} className="text-orange-600 hover:underline font-medium">
                      {task.contactName || 'View Contact'}
                    </Link>
                  </div>
                )}
                {!task.assigneeName && !task.dealId && !task.leadId && !task.contactId && (
                  <p className="text-slate-400 text-sm">No linked entities</p>
                )}
              </dl>
            </div>
          </div>

          {/* Comments Section — HP-11 */}
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm p-6">
            <h3 className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-4">
              <MessageSquare className="w-4 h-4 text-orange-500" />
              Comments
              {comments.length > 0 && (
                <span className="ml-auto text-xs font-normal text-slate-400">{comments.length}</span>
              )}
            </h3>

            {/* Comment list */}
            <div className="space-y-4 mb-6">
              {commentsLoading && (
                <div className="flex items-center gap-2 py-4 justify-center text-slate-400 text-sm">
                  <Loader2 className="w-4 h-4 animate-spin" /> Loading comments...
                </div>
              )}
              {!commentsLoading && comments.length === 0 && (
                <p className="text-sm text-slate-400 text-center py-4">No comments yet. Start a discussion!</p>
              )}
              {comments.map((c) => (
                <div key={c.id} className="group flex gap-3 p-3 rounded-xl hover:bg-slate-50 transition-colors">
                  <div className="w-8 h-8 rounded-full bg-orange-100 flex items-center justify-center shrink-0">
                    <span className="text-xs font-semibold text-orange-600">
                      {c.authorName.charAt(0).toUpperCase()}
                    </span>
                  </div>
                  <div className="flex-1 min-w-0">
                    <div className="flex items-center gap-2 mb-1">
                      <span className="text-sm font-medium text-slate-900">{c.authorName}</span>
                      <span className="text-xs text-slate-400">{timeAgo(c.createdAtUtc)}</span>
                      <button
                        onClick={() => handleDeleteComment(c.id)}
                        className="ml-auto opacity-0 group-hover:opacity-100 text-slate-400 hover:text-red-500 transition-all"
                        title="Delete comment"
                      >
                        <Trash2 className="w-3.5 h-3.5" />
                      </button>
                    </div>
                    <p className="text-sm text-slate-700 whitespace-pre-wrap">{c.body}</p>
                  </div>
                </div>
              ))}
            </div>

            {/* Add comment form */}
            <div className="flex gap-3 pt-4 border-t border-slate-100">
              <Textarea
                value={commentText}
                onChange={e => setCommentText(e.target.value)}
                placeholder="Write a comment..."
                rows={2}
                className="flex-1 resize-none"
                onKeyDown={(e) => { if (e.key === 'Enter' && (e.metaKey || e.ctrlKey)) handleAddComment(); }}
              />
              <Button
                onClick={handleAddComment}
                disabled={addingComment || !commentText.trim()}
                size="sm"
                className="self-end"
              >
                {addingComment ? <Loader2 className="w-4 h-4 animate-spin" /> : <Send className="w-4 h-4" />}
              </Button>
            </div>
            <p className="text-xs text-slate-400 mt-2">Press Ctrl+Enter to send</p>
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

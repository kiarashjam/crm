import { useState, useEffect, useMemo } from 'react';
import { CheckSquare, Square, Plus, Pencil, Calendar, AlertCircle, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getTasks, createTask, updateTask, deleteTask, getLeads, getDeals } from '@/app/api';
import type { TaskItem, Lead, Deal } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { Textarea } from '@/app/components/ui/textarea';

type Filter = 'all' | 'pending' | 'overdue';

export default function Tasks() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<Filter>('pending');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<TaskItem | null>(null);
  const [form, setForm] = useState({
    title: '',
    description: '',
    dueDate: '',
    leadId: '',
    dealId: '',
  });
  const [leads, setLeads] = useState<Lead[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [saving, setSaving] = useState(false);
  const [deleteConfirmTask, setDeleteConfirmTask] = useState<TaskItem | null>(null);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    getTasks(filter === 'overdue')
      .then((data) => { if (!cancelled) setTasks(data); })
      .catch(() => { if (!cancelled) toast.error('Failed to load tasks'); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, [filter]);

  const now = useMemo(() => new Date(), []);
  const filteredTasks = useMemo(() => {
    if (filter === 'overdue') return tasks;
    return tasks.filter((t) => {
      if (filter === 'pending') return !t.completed;
      return true;
    });
  }, [tasks, filter]);

  const pendingCount = useMemo(() => tasks.filter((t) => !t.completed).length, [tasks]);
  const overdueCount = useMemo(
    () => tasks.filter((t) => !t.completed && t.dueDateUtc && new Date(t.dueDateUtc) < now).length,
    [tasks, now]
  );

  const isOverdue = (t: TaskItem) => {
    if (t.completed || !t.dueDateUtc) return false;
    return new Date(t.dueDateUtc) < now;
  };

  useEffect(() => {
    if (dialogOpen) {
      Promise.all([getLeads(), getDeals()]).then(([l, d]) => {
        setLeads(l);
        setDeals(d);
      });
    }
  }, [dialogOpen]);

  const openCreate = () => {
    setEditingTask(null);
    setForm({ title: '', description: '', dueDate: '', leadId: '', dealId: '' });
    setDialogOpen(true);
  };

  const openEdit = (task: TaskItem) => {
    setEditingTask(task);
    setForm({
      title: task.title,
      description: task.description ?? '',
      dueDate: task.dueDateUtc ? task.dueDateUtc.slice(0, 16) : '',
      leadId: task.leadId ?? '',
      dealId: task.dealId ?? '',
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      toast.error('Title is required');
      return;
    }
    setSaving(true);
    try {
      const dueDateUtc = form.dueDate ? new Date(form.dueDate).toISOString() : undefined;
      if (editingTask) {
        const updated = await updateTask(editingTask.id, {
          title: form.title.trim(),
          description: form.description.trim() || undefined,
          dueDateUtc: dueDateUtc ?? undefined,
          leadId: form.leadId || undefined,
          dealId: form.dealId || undefined,
        });
        if (updated) {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
          toast.success('Task updated');
          setDialogOpen(false);
        } else {
          toast.error('Failed to update task');
        }
      } else {
        const created = await createTask({
          title: form.title.trim(),
          description: form.description.trim() || undefined,
          dueDateUtc,
          leadId: form.leadId || undefined,
          dealId: form.dealId || undefined,
        });
        if (created) {
          setTasks((prev) => [created, ...prev]);
          toast.success('Task created');
          setDialogOpen(false);
        } else {
          toast.error('Failed to create task');
        }
      }
    } catch {
      toast.error('Something went wrong');
    } finally {
      setSaving(false);
    }
  };

  const handleToggleComplete = async (task: TaskItem) => {
    const updated = await updateTask(task.id, { completed: !task.completed });
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      toast.success(updated.completed ? 'Task completed' : 'Task reopened');
    } else {
      toast.error('Failed to update task');
    }
  };

  const formatDue = (iso: string | undefined) => {
    if (!iso) return '—';
    const d = new Date(iso);
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    const due = new Date(d);
    due.setHours(0, 0, 0, 0);
    if (due.getTime() === today.getTime()) return 'Today';
    if (due.getTime() < today.getTime()) return d.toLocaleDateString(undefined, { dateStyle: 'short' }) + ' (overdue)';
    return d.toLocaleDateString(undefined, { dateStyle: 'short' });
  };

  return (
    <div className="min-h-screen bg-slate-50">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="w-full max-w-4xl mx-auto px-[var(--page-padding)] py-8" tabIndex={-1}>
        <div className="mb-6 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
          <div>
            <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 tracking-tight">Tasks</h1>
            <p className="text-slate-600 mt-1">Follow-ups and to-dos linked to leads or deals.</p>
          </div>
          <Button onClick={openCreate} className="gap-2 bg-orange-600 hover:bg-orange-500">
            <Plus className="w-4 h-4" />
            Add task
          </Button>
        </div>

        <div className="flex gap-2 mb-4">
          {(['pending', 'overdue', 'all'] as const).map((f) => (
            <Button
              key={f}
              variant={filter === f ? 'default' : 'outline'}
              size="sm"
              onClick={() => setFilter(f)}
              className={filter === f ? 'bg-orange-600 hover:bg-orange-500' : ''}
            >
              {f === 'pending' ? `Pending (${pendingCount})` : f === 'overdue' ? `Overdue (${overdueCount})` : `All (${tasks.length})`}
            </Button>
          ))}
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : filteredTasks.length === 0 ? (
          <EmptyState
            icon={CheckSquare}
            title={tasks.length === 0 ? 'No tasks yet' : 'No tasks match this filter'}
            description={
              tasks.length === 0
                ? 'Add a task to stay on top of follow-ups.'
                : 'Try another filter or add a new task.'
            }
            actionLabel="Add task"
            onAction={openCreate}
          />
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
            <ul className="divide-y divide-slate-100">
              {filteredTasks.map((task) => (
                <li
                  key={task.id}
                  className="flex items-start gap-3 px-4 py-4 hover:bg-slate-50/50 transition-colors"
                >
                  <button
                    type="button"
                    onClick={() => handleToggleComplete(task)}
                    className="mt-0.5 shrink-0 rounded focus-visible:ring-2 focus-visible:ring-orange-500 focus-visible:ring-offset-2"
                    aria-label={task.completed ? 'Mark incomplete' : 'Mark complete'}
                  >
                    {task.completed ? (
                      <CheckSquare className="w-5 h-5 text-emerald-600" />
                    ) : (
                      <Square className="w-5 h-5 text-slate-400" />
                    )}
                  </button>
                  <div className="flex-1 min-w-0">
                    <p
                      className={
                        task.completed
                          ? 'text-slate-500 line-through'
                          : 'font-medium text-slate-900'
                      }
                    >
                      {task.title}
                    </p>
                    {task.description && (
                      <p className="text-sm text-slate-500 mt-0.5 line-clamp-2">{task.description}</p>
                    )}
                    <div className="flex items-center gap-2 mt-1 text-xs text-slate-500 flex-wrap">
                      <Calendar className="w-3.5 h-3.5" />
                      {formatDue(task.dueDateUtc)}
                      {task.leadId && (
                        <span className="text-slate-500">
                          Lead: {leads.find((l) => l.id === task.leadId)?.name ?? '—'}
                        </span>
                      )}
                      {task.dealId && (
                        <span className="text-slate-500">
                          Deal: {deals.find((d) => d.id === task.dealId)?.name ?? '—'}
                        </span>
                      )}
                      {isOverdue(task) && (
                        <span className="inline-flex items-center gap-1 text-amber-600">
                          <AlertCircle className="w-3.5 h-3.5" />
                          Overdue
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1 shrink-0">
                    <Button variant="ghost" size="sm" onClick={() => openEdit(task)} className="gap-1.5" aria-label={`Edit task ${task.title}`}>
                      <Pencil className="w-4 h-4" />
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setDeleteConfirmTask(task)}
                      className="gap-1.5 text-red-600 hover:text-red-700 hover:bg-red-50"
                      aria-label={`Delete task ${task.title}`}
                    >
                      <Trash2 className="w-4 h-4" />
                      Delete
                    </Button>
                  </div>
                </li>
              ))}
            </ul>
          </div>
        )}
      </main>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{editingTask ? 'Edit task' : 'Add task'}</DialogTitle>
          </DialogHeader>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div>
              <Label htmlFor="task-title">Title *</Label>
              <Input
                id="task-title"
                value={form.title}
                onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                placeholder="e.g. Follow up with Acme"
                className="mt-1"
                required
              />
            </div>
            <div>
              <Label htmlFor="task-desc">Description</Label>
              <Textarea
                id="task-desc"
                value={form.description}
                onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                placeholder="Optional details"
                rows={3}
                className="mt-1"
              />
            </div>
            <div>
              <Label htmlFor="task-due">Due date</Label>
              <Input
                id="task-due"
                type="datetime-local"
                value={form.dueDate}
                onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))}
                className="mt-1"
              />
            </div>
            <div>
              <Label>Link to lead</Label>
              <Select
                value={form.leadId || 'none'}
                onValueChange={(v) => setForm((f) => ({ ...f, leadId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {leads.map((l) => (
                    <SelectItem key={l.id} value={l.id}>
                      {l.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Link to deal</Label>
              <Select
                value={form.dealId || 'none'}
                onValueChange={(v) => setForm((f) => ({ ...f, dealId: v === 'none' ? '' : v }))}
              >
                <SelectTrigger className="mt-1">
                  <SelectValue placeholder="Optional" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">— None —</SelectItem>
                  {deals.map((d) => (
                    <SelectItem key={d.id} value={d.id}>
                      {d.name} · {d.value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setDialogOpen(false)}>
                Cancel
              </Button>
              <Button type="submit" disabled={saving} className="bg-orange-600 hover:bg-orange-500">
                {saving ? 'Saving...' : editingTask ? 'Update' : 'Create'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </div>
  );
}

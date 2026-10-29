import { useState, useEffect, useMemo, useCallback } from 'react';
import { DndProvider } from 'react-dnd';
import { HTML5Backend } from 'react-dnd-html5-backend';
import {
  CheckCircle2,
  Circle,
  Plus,
  Pencil,
  Calendar,
  AlertTriangle,
  Trash2,
  Search,
  Filter,
  Target,
  ListTodo,
  Zap,
  User,
  Link2,
  Flag,
  CheckCheck,
  Play,
  RotateCcw,
  FileText,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { KanbanSkeleton } from '@/app/components/PageSkeleton';
import EmptyState from '@/app/components/EmptyState';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import {
  getTasks,
  createTask,
  updateTask,
  updateTaskStatus,
  assignTask,
  linkTaskToLead,
  linkTaskToDeal,
  deleteTask,
  getTaskStats,
  getLeads,
  getDeals,
  getContacts,
  getOrgMembers,
  messages,
  createActivity,
} from '@/app/api';
import { bulkUpdateTasks } from '@/app/api/tasks';
import type { TaskItem, Lead, Deal, Contact, TaskStatusType, TaskPriorityType, TaskRecurrence, TaskStats } from '@/app/api/types';
import { getCurrentOrganizationId } from '@/app/lib/auth';
import { 
  statusConfig, 
  priorityConfig, 
  kanbanColumns, 
  TASK_CARD_TYPE, 
  priorityOrder 
} from './tasks/config';
import type { ViewMode, TaskGroup, KanbanColumn } from './tasks/types';
import { getTaskGroup, formatDue, getInitials } from './tasks/utils';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogTitle,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { Textarea } from '@/app/components/ui/textarea';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
} from '@/app/components/ui/alert-dialog';
import { KanbanTaskCard } from './tasks/components/KanbanTaskCard';
import { KanbanColumnComponent } from './tasks/components/KanbanColumn';
import { TaskGroupSection } from './tasks/components/TaskGroupSection';
import { TaskDetailModal } from './tasks/components/TaskDetailModal';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary, type PageStat } from '@/app/components/PageHero';
import { StaggerItem } from '@/app/components/motion/Stagger';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, useViewPreference, type RecordColumn,
} from '@/app/components/views';
import { Checkbox } from '@/app/components/ui/checkbox';
import { cn } from '@/app/components/ui/utils';
import { fromLocalInputValue, toLocalInputValue } from '@/app/lib/dateInputs';

export default function Tasks() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [stats, setStats] = useState<TaskStats | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  // How this person likes tasks shown: the board, grouped list or a table, saved for them.
  const view = useViewPreference('tasks', { layouts: ['board', 'list', 'table'] });
  const viewMode: ViewMode = view.layout === 'board' ? 'kanban' : 'list';
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingTask, setEditingTask] = useState<TaskItem | null>(null);
  const [form, setForm] = useState({
    title: '',
    description: '',
    dueDate: '',
    reminderDate: '',
    status: 'todo' as TaskStatusType,
    priority: 'none' as TaskPriorityType,
    leadId: '',
    dealId: '',
    contactId: '',
    assigneeId: '',
    notes: '',
    recurrence: '' as TaskRecurrence | '',
  });
  const [leads, setLeads] = useState<Lead[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [orgMembers, setOrgMembers] = useState<{ userId: string; name: string; email: string }[]>([]);
  const [saving, setSaving] = useState(false);
  const [deleteConfirmTask, setDeleteConfirmTask] = useState<TaskItem | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [detailTask, setDetailTask] = useState<TaskItem | null>(null);
  const [detailModalOpen, setDetailModalOpen] = useState(false);

  // HP-9: Bulk selection
  const [bulkMode, setBulkMode] = useState(false);
  const [selectedTaskIds, setSelectedTaskIds] = useState<Set<string>>(new Set());
  const [bulkProcessing, setBulkProcessing] = useState(false);

  // Filters
  const [searchQuery, setSearchQuery] = useState('');
  const [priorityFilter, setPriorityFilter] = useState<TaskPriorityType | 'all'>('all');
  const [assigneeFilter, setAssigneeFilter] = useState<string>('all');
  const [leadFilter, setLeadFilter] = useState<string>('all');
  const [dealFilter, setDealFilter] = useState<string>('all');

  // Quick add
  const [quickAddTitle, setQuickAddTitle] = useState('');
  const [quickAddColumn, setQuickAddColumn] = useState<KanbanColumn | null>(null);
  const [isQuickAdding, setIsQuickAdding] = useState(false);

  // Collapsed sections (for list view)
  const [collapsedGroups, setCollapsedGroups] = useState<Set<TaskGroup>>(new Set(['completed']));

  // Load tasks and related data
  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const orgId = getCurrentOrganizationId();
      const [taskList, taskStats, leadList, dealList, contactList, memberList] = await Promise.all([
        getTasks(),
        getTaskStats(),
        getLeads(),
        getDeals(),
        getContacts(),
        orgId ? getOrgMembers(orgId) : Promise.resolve([]),
      ]);
      setTasks(taskList);
      setStats(taskStats);
      setLeads(leadList);
      setDeals(dealList);
      setContacts(contactList);
      setOrgMembers(Array.isArray(memberList) ? memberList : []);
    } catch {
      toast.error(messages.errors.loadFailed);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Date helpers — recompute whenever tasks change (ensures fresh grouping after data loads)
  // eslint-disable-next-line react-hooks/exhaustive-deps -- `tasks` is the refresh trigger, not an input
  const now = useMemo(() => new Date(), [tasks]);
  const today = useMemo(() => {
    const d = new Date(now);
    d.setHours(0, 0, 0, 0);
    return d;
  }, [now]);
  const tomorrow = useMemo(() => {
    const d = new Date(today);
    d.setDate(d.getDate() + 1);
    return d;
  }, [today]);
  const endOfWeek = useMemo(() => {
    const d = new Date(today);
    d.setDate(d.getDate() + (7 - d.getDay()));
    return d;
  }, [today]);

  const getTaskGroupMemoized = useCallback((task: TaskItem): TaskGroup => {
    return getTaskGroup(task, today, tomorrow, endOfWeek);
  }, [today, tomorrow, endOfWeek]);

  // Filter tasks (shared between views)
  const filteredTasks = useMemo(() => {
    return tasks.filter((task) => {
      if (searchQuery) {
        const q = searchQuery.toLowerCase();
        // HP-5: Search includes title, description, AND notes
        if (!task.title.toLowerCase().includes(q) && !task.description?.toLowerCase().includes(q) && !task.notes?.toLowerCase().includes(q)) {
          return false;
        }
      }
      if (priorityFilter !== 'all' && task.priority !== priorityFilter) return false;
      if (assigneeFilter !== 'all' && task.assigneeId !== assigneeFilter) return false;
      if (leadFilter !== 'all' && task.leadId !== leadFilter) return false;
      if (dealFilter !== 'all' && task.dealId !== dealFilter) return false;
      return true;
    });
  }, [tasks, searchQuery, priorityFilter, assigneeFilter, leadFilter, dealFilter]);

  // Kanban columns data
  const kanbanTasks = useMemo(() => {
    const columns: Record<KanbanColumn, TaskItem[]> = {
      todo: [],
      in_progress: [],
      completed: [],
    };

    filteredTasks.forEach((task) => {
      if (task.status === 'cancelled') {
        columns.completed.push(task);
      } else if (task.status === 'completed') {
        columns.completed.push(task);
      } else if (task.status === 'in_progress') {
        columns.in_progress.push(task);
      } else {
        columns.todo.push(task);
      }
    });

    // Sort by priority then due date
    Object.values(columns).forEach((col) => {
      col.sort((a, b) => {
        const pA = priorityOrder[a.priority || 'none'];
        const pB = priorityOrder[b.priority || 'none'];
        if (pA !== pB) return pA - pB;
        if (a.dueDateUtc && b.dueDateUtc) {
          return new Date(a.dueDateUtc).getTime() - new Date(b.dueDateUtc).getTime();
        }
        if (a.dueDateUtc) return -1;
        if (b.dueDateUtc) return 1;
        return 0;
      });
    });

    return columns;
  }, [filteredTasks]);

  // List view grouped tasks
  const groupedTasks = useMemo(() => {
    const groups: Record<TaskGroup, TaskItem[]> = {
      overdue: [],
      today: [],
      tomorrow: [],
      thisWeek: [],
      later: [],
      noDue: [],
      completed: [],
    };

    filteredTasks.forEach((task) => {
      const group = getTaskGroupMemoized(task);
      groups[group].push(task);
    });

    Object.values(groups).forEach((group) => {
      group.sort((a, b) => {
        const pA = priorityOrder[a.priority || 'none'];
        const pB = priorityOrder[b.priority || 'none'];
        if (pA !== pB) return pA - pB;
        if (a.dueDateUtc && b.dueDateUtc) {
          return new Date(a.dueDateUtc).getTime() - new Date(b.dueDateUtc).getTime();
        }
        return 0;
      });
    });

    return groups;
  }, [filteredTasks, getTaskGroupMemoized]);

  // Activity logging helper
  const logTaskActivity = useCallback(async (task: TaskItem, action: string, details?: string) => {
    try {
      await createActivity({
        type: 'task',
        subject: `Task ${action}: ${task.title}`,
        body: details || `Task "${task.title}" was ${action}`,
        leadId: task.leadId,
        dealId: task.dealId,
        contactId: task.contactId,
      });
    } catch (error) {
      console.error('Failed to log activity:', error);
    }
  }, []);

  // Open task detail modal
  const openTaskDetail = useCallback((task: TaskItem) => {
    setDetailTask(task);
    setDetailModalOpen(true);
  }, []);

  // Handlers
  const openCreate = (initialStatus?: TaskStatusType) => {
    setEditingTask(null);
    setForm({
      title: '',
      description: '',
      dueDate: '',
      reminderDate: '',
      status: initialStatus || 'todo',
      priority: 'none',
      leadId: '',
      dealId: '',
      contactId: '',
      assigneeId: '',
      notes: '',
      recurrence: '',
    });
    setDialogOpen(true);
  };

  const openEdit = (task: TaskItem) => {
    setEditingTask(task);
    setForm({
      title: task.title,
      description: task.description ?? '',
      dueDate: toLocalInputValue(task.dueDateUtc),
      reminderDate: toLocalInputValue(task.reminderDateUtc),
      status: task.status,
      priority: task.priority,
      leadId: task.leadId ?? '',
      dealId: task.dealId ?? '',
      contactId: task.contactId ?? '',
      assigneeId: task.assigneeId ?? '',
      notes: task.notes ?? '',
      recurrence: task.recurrence ?? '',
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      toast.error(messages.validation.titleRequired);
      return;
    }
    // The next one is due a day, week or month after this one, so this one needs a date.
    if (form.recurrence && !form.dueDate) {
      toast.error('A repeating task needs a due date');
      return;
    }
    setSaving(true);
    try {
      const dueDateUtc = fromLocalInputValue(form.dueDate);
      const reminderDateUtc = fromLocalInputValue(form.reminderDate);
      
      if (editingTask) {
        const updated = await updateTask(editingTask.id, {
          title: form.title.trim(),
          description: form.description.trim() || undefined,
          dueDateUtc,
          reminderDateUtc,
          status: form.status,
          priority: form.priority,
          leadId: form.leadId || undefined,
          dealId: form.dealId || undefined,
          contactId: form.contactId || undefined,
          assigneeId: form.assigneeId || undefined,
          notes: form.notes.trim() || undefined,
          recurrence: form.recurrence || (editingTask.recurrence ? 'none' : undefined),
          clearDueDate: !form.dueDate && !!editingTask.dueDateUtc,
          clearReminderDate: !form.reminderDate && !!editingTask.reminderDateUtc,
          clearAssignee: !form.assigneeId && !!editingTask.assigneeId,
          clearLead: !form.leadId && !!editingTask.leadId,
          clearDeal: !form.dealId && !!editingTask.dealId,
          clearContact: !form.contactId && !!editingTask.contactId,
        });
        if (updated) {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
          // Update detail modal if it's the same task
          if (detailTask?.id === updated.id) {
            setDetailTask(updated);
          }
          // Log activity
          const changes: string[] = [];
          if (editingTask.title !== form.title.trim()) changes.push('title');
          if (editingTask.description !== form.description.trim()) changes.push('description');
          if (editingTask.status !== form.status) changes.push(`status to ${statusConfig[form.status].label}`);
          if (editingTask.priority !== form.priority) changes.push(`priority to ${priorityConfig[form.priority].label}`);
          if (editingTask.dueDateUtc !== dueDateUtc) changes.push('due date');
          await logTaskActivity(updated, 'updated', changes.length > 0 ? `Changed: ${changes.join(', ')}` : undefined);
          toast.success(messages.success.taskUpdated);
          setDialogOpen(false);
          getTaskStats().then(setStats);
        } else {
          toast.error(messages.errors.generic);
        }
      } else {
        const created = await createTask({
          title: form.title.trim(),
          description: form.description.trim() || undefined,
          dueDateUtc,
          reminderDateUtc,
          status: form.status,
          priority: form.priority,
          leadId: form.leadId || undefined,
          dealId: form.dealId || undefined,
          contactId: form.contactId || undefined,
          assigneeId: form.assigneeId || undefined,
          notes: form.notes.trim() || undefined,
          recurrence: form.recurrence || undefined,
        });
        if (created) {
          setTasks((prev) => [created, ...prev]);
          // Log activity
          await logTaskActivity(created, 'created', `New task "${created.title}" created with status ${statusConfig[created.status].label}`);
          toast.success(messages.success.taskCreated);
          setDialogOpen(false);
          getTaskStats().then(setStats);
        } else {
          toast.error(messages.errors.generic);
        }
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSaving(false);
    }
  };

  const handleDeleteConfirm = async () => {
    if (!deleteConfirmTask) return;
    setDeleting(true);
    try {
      const ok = await deleteTask(deleteConfirmTask.id);
      if (ok) {
        // Log activity before removing from state
        await logTaskActivity(deleteConfirmTask, 'deleted', `Task "${deleteConfirmTask.title}" was permanently deleted`);
        setTasks((prev) => prev.filter((t) => t.id !== deleteConfirmTask.id));
        // Close detail modal if it's the same task
        if (detailTask?.id === deleteConfirmTask.id) {
          setDetailModalOpen(false);
          setDetailTask(null);
        }
        toast.success(messages.success.taskDeleted);
        setDeleteConfirmTask(null);
        getTaskStats().then(setStats);
      } else {
        toast.error(messages.errors.generic);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const handleStatusChange = async (task: TaskItem, newStatus: TaskStatusType) => {
    const oldStatus = task.status;
    setTasks((prev) =>
      prev.map((t) =>
        t.id === task.id ? { ...t, status: newStatus, completed: newStatus === 'completed' } : t
      )
    );

    const updated = await updateTaskStatus(task.id, newStatus);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      // Update detail modal if it's the same task
      if (detailTask?.id === updated.id) {
        setDetailTask(updated);
      }
      // Log activity
      await logTaskActivity(updated, 'status changed', `Status changed from ${statusConfig[oldStatus].label} to ${statusConfig[newStatus].label}`);
      toast.success(`Task moved to ${statusConfig[newStatus].label}`);
      getTaskStats().then(setStats);
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleAssigneeChange = async (task: TaskItem, newAssigneeId: string | null) => {
    const assigneeName = newAssigneeId ? orgMembers.find((m) => m.userId === newAssigneeId)?.name : undefined;
    const oldAssigneeName = task.assigneeName;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, assigneeId: newAssigneeId ?? undefined, assigneeName } : t))
    );

    const updated = await assignTask(task.id, newAssigneeId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      // Update detail modal if it's the same task
      if (detailTask?.id === updated.id) {
        setDetailTask(updated);
      }
      // Log activity
      const details = newAssigneeId 
        ? `Task assigned to ${assigneeName}${oldAssigneeName ? ` (was: ${oldAssigneeName})` : ''}`
        : `Task unassigned${oldAssigneeName ? ` (was: ${oldAssigneeName})` : ''}`;
      await logTaskActivity(updated, newAssigneeId ? 'assigned' : 'unassigned', details);
      toast.success(newAssigneeId ? 'Task assigned' : 'Task unassigned');
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handlePriorityChange = async (task: TaskItem, newPriority: TaskPriorityType) => {
    const oldPriority = task.priority;
    setTasks((prev) => prev.map((t) => (t.id === task.id ? { ...t, priority: newPriority } : t)));

    const updated = await updateTask(task.id, { priority: newPriority });
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      // Update detail modal if it's the same task
      if (detailTask?.id === updated.id) {
        setDetailTask(updated);
      }
      // Log activity
      await logTaskActivity(updated, 'priority changed', `Priority changed from ${priorityConfig[oldPriority].label} to ${priorityConfig[newPriority].label}`);
      toast.success(`Priority set to ${priorityConfig[newPriority].label}`);
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleLeadChange = async (task: TaskItem, newLeadId: string | null) => {
    const leadName = newLeadId ? leads.find((l) => l.id === newLeadId)?.name : undefined;
    const oldLeadName = task.leadName;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, leadId: newLeadId ?? undefined, leadName } : t))
    );

    const updated = await linkTaskToLead(task.id, newLeadId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      // Update detail modal if it's the same task
      if (detailTask?.id === updated.id) {
        setDetailTask(updated);
      }
      // Log activity
      const details = newLeadId 
        ? `Task linked to lead "${leadName}"${oldLeadName ? ` (was: ${oldLeadName})` : ''}`
        : `Task unlinked from lead${oldLeadName ? ` "${oldLeadName}"` : ''}`;
      await logTaskActivity(updated, newLeadId ? 'linked to lead' : 'unlinked from lead', details);
      toast.success(newLeadId ? 'Task linked to lead' : 'Task unlinked from lead');
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleDealChange = async (task: TaskItem, newDealId: string | null) => {
    const dealName = newDealId ? deals.find((d) => d.id === newDealId)?.name : undefined;
    const oldDealName = task.dealName;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, dealId: newDealId ?? undefined, dealName } : t))
    );

    const updated = await linkTaskToDeal(task.id, newDealId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      // Update detail modal if it's the same task
      if (detailTask?.id === updated.id) {
        setDetailTask(updated);
      }
      // Log activity
      const details = newDealId 
        ? `Task linked to deal "${dealName}"${oldDealName ? ` (was: ${oldDealName})` : ''}`
        : `Task unlinked from deal${oldDealName ? ` "${oldDealName}"` : ''}`;
      await logTaskActivity(updated, newDealId ? 'linked to deal' : 'unlinked from deal', details);
      toast.success(newDealId ? 'Task linked to deal' : 'Task unlinked from deal');
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleQuickAdd = async (column: KanbanColumn) => {
    if (!quickAddTitle.trim()) return;
    setIsQuickAdding(true);

    const statusMap: Record<KanbanColumn, TaskStatusType> = {
      todo: 'todo',
      in_progress: 'in_progress',
      completed: 'completed',
    };

    try {
      const created = await createTask({
        title: quickAddTitle.trim(),
        status: statusMap[column],
        priority: 'none',
      });
      if (created) {
        setTasks((prev) => [created, ...prev]);
        setQuickAddTitle('');
        setQuickAddColumn(null);
        toast.success('Task added');
        getTaskStats().then(setStats);
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setIsQuickAdding(false);
    }
  };

  // Drag and drop handler for react-dnd
  const handleDropTask = async (taskId: string, newColumn: KanbanColumn) => {
    const task = tasks.find((t) => t.id === taskId);
    if (!task) return;

    const statusMap: Record<KanbanColumn, TaskStatusType> = {
      todo: 'todo',
      in_progress: 'in_progress',
      completed: 'completed',
    };

    const newStatus = statusMap[newColumn];
    if (task.status !== newStatus) {
      await handleStatusChange(task, newStatus);
    }
  };

  const toggleGroupCollapse = (group: TaskGroup) => {
    setCollapsedGroups((prev) => {
      const next = new Set(prev);
      if (next.has(group)) next.delete(group);
      else next.add(group);
      return next;
    });
  };

  const formatDueMemoized = useCallback((iso: string | undefined) => {
    return formatDue(iso);
  }, []);

  const memberName = (id?: string) => (id ? orgMembers.find((m) => m.userId === id)?.name : undefined);
  const linkedTo = (t: TaskItem) =>
    (t.leadId && leads.find((l) => l.id === t.leadId)?.name)
    || (t.dealId && deals.find((d) => d.id === t.dealId)?.name)
    || (t.contactId && contacts.find((c) => c.id === t.contactId)?.name)
    || '—';
  const taskColumns: RecordColumn<TaskItem>[] = [
    {
      id: 'title',
      label: 'Task',
      required: true,
      cell: (t) => (
        <span className="flex items-center gap-2.5">
          {bulkMode && (
            <span onClick={(e) => e.stopPropagation()} className="flex items-center">
              <Checkbox checked={selectedTaskIds.has(t.id)} onCheckedChange={() => toggleTaskSelection(t.id)} aria-label={`Select ${t.title}`} />
            </span>
          )}
          <span className={cn('truncate', t.status === 'completed' && 'text-slate-400 line-through')}>{t.title}</span>
        </span>
      ),
    },
    {
      id: 'status',
      label: 'Status',
      cell: (t) => {
        const c = statusConfig[t.status as TaskStatusType] ?? statusConfig.todo;
        return <span className={cn('inline-flex rounded-full px-2 py-0.5 text-xs font-medium', c.bgColor, c.color)}>{c.label}</span>;
      },
    },
    {
      id: 'priority',
      label: 'Priority',
      cell: (t) => {
        const c = priorityConfig[(t.priority ?? 'none') as TaskPriorityType] ?? priorityConfig.none;
        return <span className={cn('inline-flex rounded-full px-2 py-0.5 text-xs font-medium', c.bgColor, c.color)}>{c.label}</span>;
      },
    },
    { id: 'due', label: 'Due', cell: (t) => (t.dueDateUtc ? formatDue(t.dueDateUtc) : '—') },
    { id: 'assignee', label: 'Assignee', cell: (t) => memberName(t.assigneeId) ?? 'Unassigned' },
    { id: 'linked', label: 'Linked to', cell: linkedTo },
  ];
  /** Edit and delete, as on the board and in the list. */
  const taskActions = (t: TaskItem) => (
    <WriteOnly>
      <Button variant="ghost" size="icon" className="h-8 w-8 rounded-lg text-slate-400 hover:bg-amber-50 hover:text-amber-700" onClick={() => openEdit(t)} aria-label={`Edit ${t.title}`}>
        <Pencil className="h-4 w-4" />
      </Button>
      <Button variant="ghost" size="icon" className="h-8 w-8 rounded-lg text-slate-400 hover:bg-red-50 hover:text-red-600" onClick={() => setDeleteConfirmTask(t)} aria-label={`Delete ${t.title}`}>
        <Trash2 className="h-4 w-4" />
      </Button>
    </WriteOnly>
  );

  // HP-9: Bulk operations handlers
  const toggleTaskSelection = useCallback((taskId: string) => {
    setSelectedTaskIds((prev) => {
      const next = new Set(prev);
      if (next.has(taskId)) next.delete(taskId);
      else next.add(taskId);
      return next;
    });
  }, []);

  const selectAllVisible = useCallback(() => {
    setSelectedTaskIds(new Set(filteredTasks.map(t => t.id)));
  }, [filteredTasks]);

  const clearSelection = useCallback(() => {
    setSelectedTaskIds(new Set());
  }, []);

  const exitBulkMode = useCallback(() => {
    setBulkMode(false);
    setSelectedTaskIds(new Set());
  }, []);

  const handleBulkAction = async (action: 'status' | 'priority' | 'assignee' | 'delete', value?: string) => {
    if (selectedTaskIds.size === 0) return;
    setBulkProcessing(true);
    try {
      const result = await bulkUpdateTasks({
        taskIds: Array.from(selectedTaskIds),
        action,
        ...(action === 'status' && { status: value as TaskStatusType }),
        ...(action === 'priority' && { priority: value as TaskPriorityType }),
        ...(action === 'assignee' && { assigneeId: value || null }),
        ...(action === 'delete' && {}),
      });
      if (result) {
        toast.success(`Bulk ${action}: ${result.succeeded}/${result.totalRequested} tasks updated`);
        await loadData();
        exitBulkMode();
      }
    } catch {
      toast.error('Bulk operation failed');
    } finally {
      setBulkProcessing(false);
    }
  };

  // HP-6: Handlers for inline ListTaskCard link actions
  const handleInlineAssigneeChange = useCallback(async (task: TaskItem, assigneeId: string | null) => {
    await handleAssigneeChange(task, assigneeId);
  }, [handleAssigneeChange]);

  const handleInlineDealChange = useCallback(async (task: TaskItem, dealId: string | null) => {
    await handleDealChange(task, dealId);
  }, [handleDealChange]);

  const handleInlineLeadChange = useCallback(async (task: TaskItem, leadId: string | null) => {
    await handleLeadChange(task, leadId);
  }, [handleLeadChange]);

  // Wrapper component for KanbanTaskCard that provides all required props
  const KanbanTaskCardWrapper = ({ task }: { task: TaskItem }) => (
    <KanbanTaskCard
      task={task}
      openEdit={openEdit}
      handleDelete={(t) => setDeleteConfirmTask(t)}
      handlePriorityChange={handlePriorityChange}
      handleAssigneeChange={handleAssigneeChange}
      handleLeadChange={handleLeadChange}
      handleDealChange={handleDealChange}
      members={orgMembers}
      leads={leads}
      deals={deals}
      priorityConfig={priorityConfig}
      getInitials={getInitials}
      formatDue={formatDueMemoized}
      taskCardType={TASK_CARD_TYPE}
      onViewDetails={openTaskDetail}
    />
  );

  // Handler for updating task from detail modal
  const handleDetailModalUpdate = async (taskId: string, updates: Partial<TaskItem>): Promise<TaskItem | null> => {
    try {
      const updated = await updateTask(taskId, {
        title: updates.title,
        description: updates.description,
        dueDateUtc: updates.dueDateUtc,
        reminderDateUtc: updates.reminderDateUtc,
        status: updates.status,
        priority: updates.priority,
        notes: updates.notes,
        clearDueDate: updates.dueDateUtc === undefined,
        clearReminderDate: updates.reminderDateUtc === undefined,
      });
      if (updated) {
        setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
        setDetailTask(updated);
        // Log activity
        await logTaskActivity(updated, 'updated', 'Task details were modified');
        getTaskStats().then(setStats);
        return updated;
      }
      return null;
    } catch {
      toast.error('Failed to update task');
      return null;
    }
  };

  // The page's numbers, in its header.
  const completionRate = stats && stats.total > 0 ? Math.round((stats.completed / stats.total) * 100) : 0;
  const taskHeroStats: PageStat[] | undefined = stats ? [
    { label: 'Total Tasks', value: stats.total, icon: ListTodo, tone: 'amber' },
    { label: 'To Do', value: stats.todo, icon: Circle, tone: 'slate' },
    { label: 'In Progress', value: stats.inProgress, icon: Play, tone: 'blue' },
    { label: 'Due Today', value: stats.dueToday, icon: Target, tone: 'orange' },
    { label: 'Overdue', value: stats.overdue, icon: AlertTriangle, tone: 'rose' },
    { label: 'High Priority', value: stats.highPriority, icon: Zap, tone: 'pink' },
    { label: 'Completed', value: stats.completed, icon: CheckCheck, tone: 'emerald' },
    {
      label: 'Completion', value: completionRate, icon: CheckCircle2, tone: 'violet',
      hint: `${stats.completed}/${stats.total}`, format: (n) => `${Math.round(n)}%`,
    },
  ] : undefined;

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={ListTodo}
            tone="amber"
            title="Task Manager"
            subtitle="Organize, prioritize, and track your work efficiently"
            actions={
              <>
                <ViewSwitcher view={view} columns={columnChoices(taskColumns)} />

                {/* HP-9: Bulk mode toggle (list and table) */}
                {viewMode === 'list' && (
                  <Button
                    variant={bulkMode ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => bulkMode ? exitBulkMode() : setBulkMode(true)}
                    className={cn(
                      heroSecondary,
                      bulkMode && 'border-orange-600 bg-orange-600 text-white hover:border-orange-700 hover:bg-orange-700 hover:text-white',
                    )}
                  >
                    <CheckCheck className="h-4 w-4" />
                    {bulkMode ? 'Exit Bulk' : 'Bulk'}
                  </Button>
                )}

                <WriteOnly>
                  <Button onClick={() => openCreate()} className={heroPrimary('amber')}>
                    <Plus className="h-4 w-4" />
                    New Task
                  </Button>
                </WriteOnly>
              </>
            }
            stats={!loading ? taskHeroStats : undefined}
          />

        {loading ? (
          <KanbanSkeleton columns={4} />
        ) : (
          <>
            {/* Search and filters */}
            <div className="relative mb-6 overflow-hidden rounded-2xl border border-slate-200/80 bg-white/85 p-4 shadow-sm backdrop-blur">
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-amber-300/25 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-orange-300/25 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-wrap items-center gap-3">
                {/* Search - Enhanced */}
                <div className="relative flex-1 min-w-[240px] max-w-md group">
                  <div className="absolute inset-0 bg-gradient-to-r from-amber-500/20 via-orange-500/20 to-amber-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-amber-100 to-orange-100 flex items-center justify-center group-focus-within:from-amber-200 group-focus-within:to-orange-200 transition-all duration-300">
                      <Search className="w-4 h-4 text-amber-600 group-focus-within:text-amber-700 transition-colors" />
                    </div>
                    <Input
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Search tasks by title or description..."
                      className="pl-14 pr-10 h-11 rounded-xl border border-slate-200 bg-slate-50/80 text-slate-900 placeholder:text-slate-400 focus:border-orange-300 focus:ring-4 focus:ring-orange-200/50 focus:bg-white hover:bg-white hover:border-slate-300 transition-all duration-300"
                    />
                  </div>
                </div>

                {/* Filters */}
                <div className="flex items-center gap-2 flex-wrap">
                  <div className="flex items-center gap-2 text-slate-500 mr-1">
                    <Filter className="w-4 h-4" />
                    <span className="text-sm font-medium hidden sm:inline">Filters:</span>
                  </div>

                  <Select value={priorityFilter} onValueChange={(v) => setPriorityFilter(v as TaskPriorityType | 'all')}>
                    <SelectTrigger className="w-36 h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-amber-50 hover:border-amber-300 focus:border-amber-300 focus:ring-2 focus:ring-amber-200/60 transition-all duration-300">
                      <SelectValue placeholder="Priority" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="all">All priorities</SelectItem>
                      <SelectItem value="high">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-red-500" />
                          High
                        </span>
                      </SelectItem>
                      <SelectItem value="medium">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-amber-500" />
                          Medium
                        </span>
                      </SelectItem>
                      <SelectItem value="low">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-blue-500" />
                          Low
                        </span>
                      </SelectItem>
                      <SelectItem value="none">No priority</SelectItem>
                    </SelectContent>
                  </Select>

                  {orgMembers.length > 0 && (
                    <Select value={assigneeFilter} onValueChange={setAssigneeFilter}>
                      <SelectTrigger className="w-40 h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-amber-50 hover:border-amber-300 focus:border-amber-300 focus:ring-2 focus:ring-amber-200/60 transition-all duration-300">
                        <SelectValue placeholder="Assignee" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All assignees</SelectItem>
                        {orgMembers.map((m) => (
                          <SelectItem key={m.userId} value={m.userId}>{m.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}

                  {leads.length > 0 && (
                    <Select value={leadFilter} onValueChange={setLeadFilter}>
                      <SelectTrigger className="w-32 h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-amber-50 hover:border-amber-300 focus:border-amber-300 focus:ring-2 focus:ring-amber-200/60 transition-all duration-300">
                        <SelectValue placeholder="Lead" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All leads</SelectItem>
                        {leads.map((l) => (
                          <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}

                  {deals.length > 0 && (
                    <Select value={dealFilter} onValueChange={setDealFilter}>
                      <SelectTrigger className="w-32 h-11 rounded-xl border border-slate-200 bg-white text-slate-700 shadow-sm hover:bg-amber-50 hover:border-amber-300 focus:border-amber-300 focus:ring-2 focus:ring-amber-200/60 transition-all duration-300">
                        <SelectValue placeholder="Deal" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="all">All deals</SelectItem>
                        {deals.map((d) => (
                          <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  )}

                  {/* Clear filters button */}
                  {(searchQuery || priorityFilter !== 'all' || assigneeFilter !== 'all' || leadFilter !== 'all' || dealFilter !== 'all') && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => {
                        setSearchQuery('');
                        setPriorityFilter('all');
                        setAssigneeFilter('all');
                        setLeadFilter('all');
                        setDealFilter('all');
                      }}
                      className="h-11 px-4 rounded-xl text-slate-500 hover:text-red-600 hover:bg-red-50 transition-all duration-300"
                    >
                      <RotateCcw className="w-4 h-4 mr-2" />
                      Clear
                    </Button>
                  )}
                </div>
              </div>
            </div>

            {/* Content */}
            {tasks.length === 0 ? (
              <EmptyState
                icon={ListTodo}
                title="No tasks yet"
                description="Create your first task to start organizing your work."
                actionLabel="Create task"
                onAction={() => openCreate()}
              />
            ) : filteredTasks.length === 0 ? (
              <EmptyState
                icon={Search}
                title="No tasks found"
                description="Try adjusting your search or filters."
                actionLabel="Clear filters"
                onAction={() => {
                  setSearchQuery('');
                  setPriorityFilter('all');
                  setAssigneeFilter('all');
                  setLeadFilter('all');
                  setDealFilter('all');
                }}
              />
            ) : viewMode === 'kanban' ? (
              /* Kanban View with Drag and Drop */
              <DndProvider backend={HTML5Backend}>
                <div className="grid grid-cols-1 gap-6 w-full md:grid-cols-3">
                  {kanbanColumns.map((column) => (
                    <KanbanColumnComponent
                      key={column.id}
                      column={column}
                      tasks={kanbanTasks[column.id]}
                      onDrop={handleDropTask}
                      quickAddColumn={quickAddColumn}
                      quickAddTitle={quickAddTitle}
                      setQuickAddTitle={setQuickAddTitle}
                      setQuickAddColumn={setQuickAddColumn}
                      handleQuickAdd={handleQuickAdd}
                      isQuickAdding={isQuickAdding}
                      onOpenCreate={openCreate}
                      KanbanTaskCard={KanbanTaskCardWrapper}
                    />
                  ))}
                </div>
              </DndProvider>
            ) : (
              /* List and table views */
              <div className="space-y-2">
                {/* HP-9: Bulk operations toolbar */}
                {bulkMode && (
                  <div className="sticky top-0 z-10 flex items-center gap-3 p-3 bg-orange-50 border border-orange-200 rounded-xl shadow-sm">
                    <span className="text-sm font-medium text-orange-800">
                      {selectedTaskIds.size} selected
                    </span>
                    <Button size="sm" variant="outline" onClick={selectAllVisible} className="text-xs h-7">Select all ({filteredTasks.length})</Button>
                    <Button size="sm" variant="outline" onClick={clearSelection} className="text-xs h-7">Clear</Button>
                    <div className="w-px h-5 bg-orange-200" />
                    <Button size="sm" variant="outline" onClick={() => handleBulkAction('status', 'completed')} disabled={bulkProcessing || selectedTaskIds.size === 0} className="text-xs h-7">
                      <CheckCheck className="w-3 h-3 mr-1" /> Complete
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => handleBulkAction('priority', 'high')} disabled={bulkProcessing || selectedTaskIds.size === 0} className="text-xs h-7">
                      <Flag className="w-3 h-3 mr-1" /> High Priority
                    </Button>
                    <Button size="sm" variant="outline" onClick={() => handleBulkAction('delete')} disabled={bulkProcessing || selectedTaskIds.size === 0} className="text-xs h-7 text-red-600 hover:text-red-700 hover:bg-red-50">
                      <Trash2 className="w-3 h-3 mr-1" /> Delete
                    </Button>
                    <div className="ml-auto">
                      <Button size="sm" variant="ghost" onClick={exitBulkMode} className="text-xs h-7">Exit bulk mode</Button>
                    </div>
                  </div>
                )}

                <ViewTransition layout={view.layout}>
                {view.layout === 'table' ? (
                  <RecordTable
                    label="Tasks"
                    items={filteredTasks}
                    view={view}
                    getKey={(t) => t.id}
                    onOpen={openTaskDetail}
                    selected={(t) => selectedTaskIds.has(t.id)}
                    columns={taskColumns}
                    actions={taskActions}
                  />
                ) : (
                <div className="space-y-2">
                {(['overdue', 'today', 'tomorrow', 'thisWeek', 'later', 'noDue', 'completed'] as const).map((group, index) => (
                  // A group with nothing to show renders nothing, so its wrapper hides too.
                  <StaggerItem key={group} index={index} className="empty:hidden">
                    <TaskGroupSection 
                      group={group} 
                      tasks={groupedTasks[group]}
                      isCollapsed={collapsedGroups.has(group)}
                      onToggle={() => toggleGroupCollapse(group)}
                      openEdit={openEdit}
                      handleDelete={setDeleteConfirmTask}
                      handleStatusChange={handleStatusChange}
                      handlePriorityChange={handlePriorityChange}
                      statusConfig={statusConfig}
                      priorityConfig={priorityConfig}
                      contacts={contacts}
                      members={orgMembers}
                      getInitials={getInitials}
                      formatDue={formatDue}
                      onViewDetails={openTaskDetail}
                      onAssigneeChange={handleInlineAssigneeChange}
                      onDealChange={handleInlineDealChange}
                      onLeadChange={handleInlineLeadChange}
                      leads={leads}
                      deals={deals}
                      selectedTaskIds={selectedTaskIds}
                      onSelectionToggle={toggleTaskSelection}
                      bulkMode={bulkMode}
                    />
                  </StaggerItem>
                ))}
                </div>
                )}
                </ViewTransition>
              </div>
            )}
          </>
        )}
        </main>
      </PageTransition>

      {/* Create/Edit Dialog - Modern Design */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="sm:max-w-[560px] p-0 gap-0 overflow-hidden bg-white rounded-2xl border-0 shadow-2xl">
          {/* Header with gradient */}
          <div className="relative bg-gradient-to-r from-slate-900 via-slate-800 to-slate-900 px-6 py-5">
            <div className="absolute inset-0 opacity-30">
              <div className="absolute top-0 right-0 w-32 h-32 bg-orange-500/30 rounded-full blur-3xl" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-violet-500/20 rounded-full blur-2xl" />
            </div>
            <div className="relative flex items-center gap-4">
              <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-lg shadow-orange-500/30">
                {editingTask ? <Pencil className="w-5 h-5 text-white" /> : <Plus className="w-6 h-6 text-white" />}
              </div>
              <div>
                <DialogTitle className="text-xl font-bold text-white mb-0.5">
                  {editingTask ? 'Edit Task' : 'Create New Task'}
                </DialogTitle>
                <p className="text-sm text-slate-400">
                  {editingTask ? 'Update the task details below' : 'Add a new task to your workflow'}
                </p>
              </div>
            </div>
          </div>

          {/* Form Content */}
          <form onSubmit={handleSubmit} className="px-6 py-5 max-h-[60vh] overflow-y-auto">
            <div className="space-y-5">
              {/* Title Field */}
              <div>
                <Label htmlFor="task-title" className="text-sm font-semibold text-slate-700 flex items-center gap-2">
                  <ListTodo className="w-4 h-4 text-slate-400" />
                  Task Title <span className="text-red-500">*</span>
                </Label>
                <Input
                  id="task-title"
                  value={form.title}
                  onChange={(e) => setForm((f) => ({ ...f, title: e.target.value }))}
                  placeholder="e.g. Follow up with Acme Corp about Q2 proposal"
                  className="mt-2 h-11 rounded-xl border-slate-200 focus:border-orange-500 focus:ring-orange-500/20"
                  required
                />
              </div>

              {/* Description Field */}
              <div>
                <Label htmlFor="task-desc" className="text-sm font-semibold text-slate-700 flex items-center gap-2">
                  <FileText className="w-4 h-4 text-slate-400" />
                  Description
                </Label>
                <Textarea
                  id="task-desc"
                  value={form.description}
                  onChange={(e) => setForm((f) => ({ ...f, description: e.target.value }))}
                  placeholder="Add more context about this task..."
                  rows={2}
                  className="mt-2 rounded-xl border-slate-200 focus:border-orange-500 focus:ring-orange-500/20 resize-none"
                />
              </div>

              {/* Status & Priority Row */}
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <Label className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-2">
                    <Circle className="w-4 h-4 text-slate-400" />
                    Status
                  </Label>
                  <Select value={form.status} onValueChange={(v) => setForm((f) => ({ ...f, status: v as TaskStatusType }))}>
                    <SelectTrigger className="h-11 rounded-xl border-slate-200">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="todo">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-slate-400" />
                          To Do
                        </span>
                      </SelectItem>
                      <SelectItem value="in_progress">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-blue-500" />
                          In Progress
                        </span>
                      </SelectItem>
                      <SelectItem value="completed">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-emerald-500" />
                          Completed
                        </span>
                      </SelectItem>
                      <SelectItem value="cancelled">
                        <span className="flex items-center gap-2">
                          <span className="w-2 h-2 rounded-full bg-slate-300" />
                          Cancelled
                        </span>
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
                <div>
                  <Label className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-2">
                    <Flag className="w-4 h-4 text-slate-400" />
                    Priority
                  </Label>
                  <Select value={form.priority} onValueChange={(v) => setForm((f) => ({ ...f, priority: v as TaskPriorityType }))}>
                    <SelectTrigger className="h-11 rounded-xl border-slate-200">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">
                        <span className="flex items-center gap-2 text-slate-500">No priority</span>
                      </SelectItem>
                      <SelectItem value="low">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-blue-500" />
                          Low
                        </span>
                      </SelectItem>
                      <SelectItem value="medium">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-amber-500" />
                          Medium
                        </span>
                      </SelectItem>
                      <SelectItem value="high">
                        <span className="flex items-center gap-2">
                          <Flag className="w-3.5 h-3.5 text-red-500" />
                          High
                        </span>
                      </SelectItem>
                    </SelectContent>
                  </Select>
                </div>
              </div>

              {/* Dates Section */}
              <div className="p-4 bg-slate-50 rounded-xl border border-slate-100">
                <p className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <Calendar className="w-3.5 h-3.5" />
                  Schedule
                </p>
                <div className="grid grid-cols-2 gap-4">
                  <div>
                    <Label htmlFor="task-due" className="text-sm font-medium text-slate-600">Due Date</Label>
                    <Input
                      id="task-due"
                      type="datetime-local"
                      value={form.dueDate}
                      onChange={(e) => setForm((f) => ({ ...f, dueDate: e.target.value }))}
                      className="mt-1.5 h-10 rounded-lg border-slate-200 bg-white"
                    />
                  </div>
                  <div>
                    <Label htmlFor="task-reminder" className="text-sm font-medium text-slate-600">Reminder</Label>
                    <Input
                      id="task-reminder"
                      type="datetime-local"
                      value={form.reminderDate}
                      onChange={(e) => setForm((f) => ({ ...f, reminderDate: e.target.value }))}
                      className="mt-1.5 h-10 rounded-lg border-slate-200 bg-white"
                    />
                  </div>
                </div>
                <div className="mt-4">
                  <Label htmlFor="task-repeat" className="text-sm font-medium text-slate-600">Repeats</Label>
                  <Select
                    value={form.recurrence || 'none'}
                    onValueChange={(v) => setForm((f) => ({ ...f, recurrence: v === 'none' ? '' : (v as TaskRecurrence) }))}
                  >
                    <SelectTrigger id="task-repeat" className="mt-1.5 h-10 rounded-lg border-slate-200 bg-white"><SelectValue /></SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">Does not repeat</SelectItem>
                      <SelectItem value="daily">Every day</SelectItem>
                      <SelectItem value="weekly">Every week</SelectItem>
                      <SelectItem value="monthly">Every month</SelectItem>
                    </SelectContent>
                  </Select>
                  {form.recurrence && (
                    <p className="mt-1.5 text-xs text-slate-500">Completing it opens the next one, due a {form.recurrence === 'daily' ? 'day' : form.recurrence === 'weekly' ? 'week' : 'month'} after this one.</p>
                  )}
                </div>
              </div>

              {/* Assignee */}
              {orgMembers.length > 0 && (
                <div>
                  <Label className="text-sm font-semibold text-slate-700 flex items-center gap-2 mb-2">
                    <User className="w-4 h-4 text-slate-400" />
                    Assign To
                  </Label>
                  <Select value={form.assigneeId || 'none'} onValueChange={(v) => setForm((f) => ({ ...f, assigneeId: v === 'none' ? '' : v }))}>
                    <SelectTrigger className="h-11 rounded-xl border-slate-200">
                      <SelectValue placeholder="Select team member" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">
                        <span className="text-slate-500">Unassigned</span>
                      </SelectItem>
                      {orgMembers.map((m) => (
                        <SelectItem key={m.userId} value={m.userId}>
                          <span className="flex items-center gap-2">
                            <span className="w-6 h-6 rounded-full bg-gradient-to-br from-orange-100 to-orange-200 flex items-center justify-center text-[10px] font-semibold text-orange-700">
                              {m.name.charAt(0).toUpperCase()}
                            </span>
                            {m.name}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              )}

              {/* Links Section */}
              <div className="p-4 bg-gradient-to-br from-violet-50 to-purple-50 rounded-xl border border-violet-100">
                <p className="text-xs font-semibold text-violet-600 uppercase tracking-wider mb-3 flex items-center gap-2">
                  <Link2 className="w-3.5 h-3.5" />
                  Link To
                </p>
                <div className="grid grid-cols-3 gap-3">
                  <div>
                    <Label className="text-xs font-medium text-slate-600 mb-1.5 block">Lead</Label>
                    <Select value={form.leadId || 'none'} onValueChange={(v) => setForm((f) => ({ ...f, leadId: v === 'none' ? '' : v }))}>
                      <SelectTrigger className="h-9 rounded-lg border-slate-200 bg-white text-sm">
                        <SelectValue placeholder="None" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {leads.map((l) => (
                          <SelectItem key={l.id} value={l.id}>{l.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label className="text-xs font-medium text-slate-600 mb-1.5 block">Deal</Label>
                    <Select value={form.dealId || 'none'} onValueChange={(v) => setForm((f) => ({ ...f, dealId: v === 'none' ? '' : v }))}>
                      <SelectTrigger className="h-9 rounded-lg border-slate-200 bg-white text-sm">
                        <SelectValue placeholder="None" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {deals.map((d) => (
                          <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                  <div>
                    <Label className="text-xs font-medium text-slate-600 mb-1.5 block">Contact</Label>
                    <Select value={form.contactId || 'none'} onValueChange={(v) => setForm((f) => ({ ...f, contactId: v === 'none' ? '' : v }))}>
                      <SelectTrigger className="h-9 rounded-lg border-slate-200 bg-white text-sm">
                        <SelectValue placeholder="None" />
                      </SelectTrigger>
                      <SelectContent>
                        <SelectItem value="none">None</SelectItem>
                        {contacts.map((c) => (
                          <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                </div>
              </div>

              {/* Notes */}
              <div>
                <Label htmlFor="task-notes" className="text-sm font-semibold text-slate-700 flex items-center gap-2">
                  <FileText className="w-4 h-4 text-slate-400" />
                  Notes
                </Label>
                <Textarea
                  id="task-notes"
                  value={form.notes}
                  onChange={(e) => setForm((f) => ({ ...f, notes: e.target.value }))}
                  placeholder="Additional notes or context..."
                  rows={2}
                  className="mt-2 rounded-xl border-slate-200 focus:border-orange-500 focus:ring-orange-500/20 resize-none"
                />
              </div>
            </div>
          </form>

          {/* Footer */}
          <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex items-center justify-end gap-3">
            <Button 
              type="button" 
              variant="outline" 
              onClick={() => setDialogOpen(false)}
              className="h-10 px-5 rounded-xl border-slate-200 hover:bg-slate-100"
            >
              Cancel
            </Button>
            <Button 
              type="submit"
              onClick={handleSubmit}
              disabled={saving} 
              className="h-10 px-6 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/25 font-semibold"
            >
              {saving ? (
                <>
                  <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2" />
                  Saving...
                </>
              ) : editingTask ? (
                <>
                  <CheckCircle2 className="w-4 h-4 mr-2" />
                  Update Task
                </>
              ) : (
                <>
                  <Plus className="w-4 h-4 mr-2" />
                  Create Task
                </>
              )}
            </Button>
          </div>
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation - Modern Design */}
      <AlertDialog open={!!deleteConfirmTask} onOpenChange={(open) => !open && setDeleteConfirmTask(null)}>
        <AlertDialogContent className="sm:max-w-[420px] p-0 gap-0 overflow-hidden bg-white rounded-2xl border-0 shadow-2xl">
          {/* Header */}
          <div className="relative bg-gradient-to-r from-red-600 to-rose-600 px-6 py-5">
            <div className="absolute inset-0 opacity-20">
              <div className="absolute top-0 right-0 w-24 h-24 bg-white/20 rounded-full blur-2xl" />
            </div>
            <div className="relative flex items-center gap-4">
              <div className="w-12 h-12 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center">
                <Trash2 className="w-6 h-6 text-white" />
              </div>
              <div>
                <AlertDialogTitle className="text-xl font-bold text-white mb-0.5">
                  Delete Task
                </AlertDialogTitle>
                <p className="text-sm text-red-100">
                  This action cannot be undone
                </p>
              </div>
            </div>
          </div>

          {/* Content */}
          <div className="px-6 py-5">
            <AlertDialogDescription className="text-slate-600 text-base leading-relaxed">
              Are you sure you want to permanently delete{' '}
              <span className="font-semibold text-slate-900">&quot;{deleteConfirmTask?.title}&quot;</span>?
              This will remove all associated data.
            </AlertDialogDescription>
          </div>

          {/* Footer */}
          <div className="px-6 py-4 bg-slate-50 border-t border-slate-100 flex items-center justify-end gap-3">
            <AlertDialogCancel 
              disabled={deleting}
              className="h-10 px-5 rounded-xl border-slate-200 hover:bg-slate-100 font-medium"
            >
              Cancel
            </AlertDialogCancel>
            <AlertDialogAction 
              onClick={handleDeleteConfirm} 
              disabled={deleting} 
              className="h-10 px-5 rounded-xl bg-gradient-to-r from-red-600 to-rose-600 hover:from-red-700 hover:to-rose-700 shadow-lg shadow-red-500/25 font-semibold"
            >
              {deleting ? (
                <>
                  <span className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin mr-2" />
                  Deleting...
                </>
              ) : (
                <>
                  <Trash2 className="w-4 h-4 mr-2" />
                  Delete Task
                </>
              )}
            </AlertDialogAction>
          </div>
        </AlertDialogContent>
      </AlertDialog>

      {/* Task Detail Modal */}
      <TaskDetailModal
        task={detailTask}
        open={detailModalOpen}
        onOpenChange={(open) => {
          setDetailModalOpen(open);
          if (!open) setDetailTask(null);
        }}
        onUpdate={handleDetailModalUpdate}
        onDelete={(t) => {
          setDetailModalOpen(false);
          setDeleteConfirmTask(t);
        }}
        onStatusChange={handleStatusChange}
        onPriorityChange={handlePriorityChange}
        onAssigneeChange={handleAssigneeChange}
        onLeadChange={handleLeadChange}
        onDealChange={handleDealChange}
        members={orgMembers}
        leads={leads}
        deals={deals}
        contacts={contacts}
      />
    </div>
  );
}

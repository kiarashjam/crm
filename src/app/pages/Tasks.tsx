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
  LayoutList,
  Columns3,
  FileText,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import LoadingSpinner from '@/app/components/LoadingSpinner';
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
} from '@/app/api';
import type { TaskItem, Lead, Deal, Contact, TaskStatusType, TaskPriorityType, TaskStats } from '@/app/api/types';
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

export default function Tasks() {
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [stats, setStats] = useState<TaskStats | null>(null);
  const [loading, setLoading] = useState(true);
  const [viewMode, setViewMode] = useState<ViewMode>('kanban');
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
  });
  const [leads, setLeads] = useState<Lead[]>([]);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [contacts, setContacts] = useState<Contact[]>([]);
  const [orgMembers, setOrgMembers] = useState<{ userId: string; name: string; email: string }[]>([]);
  const [saving, setSaving] = useState(false);
  const [deleteConfirmTask, setDeleteConfirmTask] = useState<TaskItem | null>(null);
  const [deleting, setDeleting] = useState(false);

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

  // Date helpers
  const now = useMemo(() => new Date(), []);
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
        if (!task.title.toLowerCase().includes(q) && !task.description?.toLowerCase().includes(q)) {
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
  }, [filteredTasks, priorityOrder]);

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
    });
    setDialogOpen(true);
  };

  const openEdit = (task: TaskItem) => {
    setEditingTask(task);
    setForm({
      title: task.title,
      description: task.description ?? '',
      dueDate: task.dueDateUtc ? task.dueDateUtc.slice(0, 16) : '',
      reminderDate: task.reminderDateUtc ? task.reminderDateUtc.slice(0, 16) : '',
      status: task.status,
      priority: task.priority,
      leadId: task.leadId ?? '',
      dealId: task.dealId ?? '',
      contactId: task.contactId ?? '',
      assigneeId: task.assigneeId ?? '',
      notes: task.notes ?? '',
    });
    setDialogOpen(true);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form.title.trim()) {
      toast.error(messages.validation.titleRequired);
      return;
    }
    setSaving(true);
    try {
      const dueDateUtc = form.dueDate ? new Date(form.dueDate).toISOString() : undefined;
      const reminderDateUtc = form.reminderDate ? new Date(form.reminderDate).toISOString() : undefined;
      
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
          clearDueDate: !form.dueDate && !!editingTask.dueDateUtc,
          clearReminderDate: !form.reminderDate && !!editingTask.reminderDateUtc,
          clearAssignee: !form.assigneeId && !!editingTask.assigneeId,
          clearLead: !form.leadId && !!editingTask.leadId,
          clearDeal: !form.dealId && !!editingTask.dealId,
          clearContact: !form.contactId && !!editingTask.contactId,
        });
        if (updated) {
          setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
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
        });
        if (created) {
          setTasks((prev) => [created, ...prev]);
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
        setTasks((prev) => prev.filter((t) => t.id !== deleteConfirmTask.id));
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
    setTasks((prev) =>
      prev.map((t) =>
        t.id === task.id ? { ...t, status: newStatus, completed: newStatus === 'completed' } : t
      )
    );

    const updated = await updateTaskStatus(task.id, newStatus);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      toast.success(`Task moved to ${statusConfig[newStatus].label}`);
      getTaskStats().then(setStats);
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleAssigneeChange = async (task: TaskItem, newAssigneeId: string | null) => {
    const assigneeName = newAssigneeId ? orgMembers.find((m) => m.userId === newAssigneeId)?.name : undefined;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, assigneeId: newAssigneeId ?? undefined, assigneeName } : t))
    );

    const updated = await assignTask(task.id, newAssigneeId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      toast.success(newAssigneeId ? 'Task assigned' : 'Task unassigned');
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handlePriorityChange = async (task: TaskItem, newPriority: TaskPriorityType) => {
    setTasks((prev) => prev.map((t) => (t.id === task.id ? { ...t, priority: newPriority } : t)));

    const updated = await updateTask(task.id, { priority: newPriority });
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      toast.success(`Priority set to ${priorityConfig[newPriority].label}`);
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleLeadChange = async (task: TaskItem, newLeadId: string | null) => {
    const leadName = newLeadId ? leads.find((l) => l.id === newLeadId)?.name : undefined;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, leadId: newLeadId ?? undefined, leadName } : t))
    );

    const updated = await linkTaskToLead(task.id, newLeadId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
      toast.success(newLeadId ? 'Task linked to lead' : 'Task unlinked from lead');
    } else {
      setTasks((prev) => prev.map((t) => (t.id === task.id ? task : t)));
      toast.error(messages.errors.generic);
    }
  };

  const handleDealChange = async (task: TaskItem, newDealId: string | null) => {
    const dealName = newDealId ? deals.find((d) => d.id === newDealId)?.name : undefined;
    setTasks((prev) =>
      prev.map((t) => (t.id === task.id ? { ...t, dealId: newDealId ?? undefined, dealName } : t))
    );

    const updated = await linkTaskToDeal(task.id, newDealId);
    if (updated) {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? updated : t)));
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
    />
  );


  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
        {/* Modern Hero Header */}
        <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
          {/* Animated background elements */}
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-20 -right-20 w-80 h-80 bg-orange-500/20 rounded-full blur-3xl animate-pulse" />
            <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-violet-500/15 rounded-full blur-3xl" />
            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl" />
            {/* Subtle grid pattern */}
            <div className="absolute inset-0 opacity-5" style={{
              backgroundImage: `url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cpath d='M54.627 0l.83.828-1.415 1.415L51.8 0h2.827zM5.373 0l-.83.828L5.96 2.243 8.2 0H5.374zM48.97 0l3.657 3.657-1.414 1.414L46.143 0h2.828zM11.03 0L7.372 3.657 8.787 5.07 13.857 0H11.03zm32.284 0L49.8 6.485 48.384 7.9l-7.9-7.9h2.83zM16.686 0L10.2 6.485 11.616 7.9l7.9-7.9h-2.83zM22.343 0L13.857 8.485 15.272 9.9l9.9-9.9h-2.83zM32 0l-3.486 3.485-1.414-1.414L30.586 0H32zM0 5.373l.828-.83 1.415 1.415L0 8.2V5.374zm0 5.656l.828-.829 5.657 5.657-1.414 1.414L0 11.03v-.001zm0 5.656l.828-.828 8.485 8.485-1.414 1.414L0 16.686v-.001zm0 5.657l.828-.828 11.314 11.314-1.414 1.414L0 22.343v-.001zM60 5.373l-.828-.83-1.415 1.415L60 8.2V5.374zm0 5.656l-.828-.829-5.657 5.657 1.414 1.414L60 11.03v-.001zm0 5.656l-.828-.828-8.485 8.485 1.414 1.414L60 16.686v-.001zm0 5.657l-.828-.828-11.314 11.314 1.414 1.414L60 22.343v-.001z' fill='%23fff' fill-opacity='1' fill-rule='evenodd'/%3E%3C/svg%3E")`,
            }} />
          </div>

          <div className="relative px-6 lg:px-8 py-8 lg:py-10">
            <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
              <div className="flex items-center gap-5">
                <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-500 flex items-center justify-center shadow-2xl shadow-orange-500/30">
                  <ListTodo className="w-8 h-8 text-white" />
                </div>
                <div>
                  <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Task Manager</h1>
                  <p className="text-slate-400 mt-1">Organize, prioritize, and track your work efficiently</p>
                </div>
              </div>

              <div className="flex items-center gap-3">
                {/* View Toggle */}
                <div className="flex items-center bg-white/10 backdrop-blur-sm rounded-xl p-1 border border-white/10">
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setViewMode('kanban')}
                    className={`gap-2 rounded-lg transition-all ${viewMode === 'kanban' 
                      ? 'bg-white text-slate-900 shadow-lg hover:bg-white' 
                      : 'text-white/80 hover:text-white hover:bg-white/10'
                    }`}
                  >
                    <Columns3 className="w-4 h-4" />
                    Board
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => setViewMode('list')}
                    className={`gap-2 rounded-lg transition-all ${viewMode === 'list' 
                      ? 'bg-white text-slate-900 shadow-lg hover:bg-white' 
                      : 'text-white/80 hover:text-white hover:bg-white/10'
                    }`}
                  >
                    <LayoutList className="w-4 h-4" />
                    List
                  </Button>
                </div>

                <Button 
                  onClick={() => openCreate()} 
                  className="gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 shadow-lg shadow-orange-500/30 font-semibold"
                >
                  <Plus className="w-4 h-4" />
                  New Task
                </Button>
              </div>
            </div>
          </div>
        </div>

        {loading ? (
          <LoadingSpinner />
        ) : (
          <>
            {/* Modern Stats Cards */}
            {stats && (
              <div className="grid grid-cols-2 md:grid-cols-4 lg:grid-cols-8 gap-4 mb-8">
                {/* Total Tasks */}
                <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-100 to-slate-50 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <ListTodo className="w-5 h-5 text-slate-600" />
                    </div>
                    <p className="text-3xl font-bold text-slate-900 tracking-tight">{stats.total}</p>
                    <p className="text-xs font-medium text-slate-500 mt-1">Total Tasks</p>
                  </div>
                </div>

                {/* To Do */}
                <div className="group relative bg-white rounded-2xl border border-slate-200/80 p-5 shadow-sm hover:shadow-xl hover:border-slate-300 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-slate-50 to-slate-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-slate-100 to-slate-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <Circle className="w-5 h-5 text-slate-500" />
                    </div>
                    <p className="text-3xl font-bold text-slate-700 tracking-tight">{stats.todo}</p>
                    <p className="text-xs font-medium text-slate-500 mt-1">To Do</p>
                  </div>
                </div>

                {/* In Progress */}
                <div className="group relative bg-white rounded-2xl border border-blue-100 p-5 shadow-sm hover:shadow-xl hover:shadow-blue-100 hover:border-blue-200 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-blue-50 to-blue-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-blue-100 to-blue-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <Play className="w-5 h-5 text-blue-600" />
                    </div>
                    <p className="text-3xl font-bold text-blue-600 tracking-tight">{stats.inProgress}</p>
                    <p className="text-xs font-medium text-blue-600/70 mt-1">In Progress</p>
                  </div>
                </div>

                {/* Due Today */}
                <div className="group relative bg-white rounded-2xl border border-orange-100 p-5 shadow-sm hover:shadow-xl hover:shadow-orange-100 hover:border-orange-200 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-orange-50 to-orange-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-orange-100 to-orange-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <Target className="w-5 h-5 text-orange-600" />
                    </div>
                    <p className="text-3xl font-bold text-orange-600 tracking-tight">{stats.dueToday}</p>
                    <p className="text-xs font-medium text-orange-600/70 mt-1">Due Today</p>
                  </div>
                </div>

                {/* Overdue */}
                <div className="group relative bg-white rounded-2xl border border-red-100 p-5 shadow-sm hover:shadow-xl hover:shadow-red-100 hover:border-red-200 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-red-50 to-red-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-red-100 to-red-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <AlertTriangle className="w-5 h-5 text-red-600" />
                    </div>
                    <p className="text-3xl font-bold text-red-600 tracking-tight">{stats.overdue}</p>
                    <p className="text-xs font-medium text-red-600/70 mt-1">Overdue</p>
                  </div>
                </div>

                {/* High Priority */}
                <div className="group relative bg-white rounded-2xl border border-rose-100 p-5 shadow-sm hover:shadow-xl hover:shadow-rose-100 hover:border-rose-200 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-rose-50 to-rose-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-rose-100 to-rose-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <Zap className="w-5 h-5 text-rose-600" />
                    </div>
                    <p className="text-3xl font-bold text-rose-600 tracking-tight">{stats.highPriority}</p>
                    <p className="text-xs font-medium text-rose-600/70 mt-1">High Priority</p>
                  </div>
                </div>

                {/* Completed */}
                <div className="group relative bg-white rounded-2xl border border-emerald-100 p-5 shadow-sm hover:shadow-xl hover:shadow-emerald-100 hover:border-emerald-200 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-gradient-to-br from-emerald-50 to-emerald-100 rounded-bl-[60px] -mr-2 -mt-2 group-hover:scale-110 transition-transform" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-emerald-100 to-emerald-200 flex items-center justify-center mb-3 group-hover:scale-110 transition-transform">
                      <CheckCheck className="w-5 h-5 text-emerald-600" />
                    </div>
                    <p className="text-3xl font-bold text-emerald-600 tracking-tight">{stats.completed}</p>
                    <p className="text-xs font-medium text-emerald-600/70 mt-1">Completed</p>
                  </div>
                </div>

                {/* Progress */}
                <div className="group relative bg-gradient-to-br from-violet-500 to-purple-600 rounded-2xl p-5 shadow-lg shadow-violet-500/20 hover:shadow-xl hover:shadow-violet-500/30 transition-all duration-300 overflow-hidden">
                  <div className="absolute top-0 right-0 w-20 h-20 bg-white/10 rounded-bl-[60px] -mr-2 -mt-2" />
                  <div className="relative">
                    <div className="w-10 h-10 rounded-xl bg-white/20 backdrop-blur-sm flex items-center justify-center mb-3">
                      <CheckCircle2 className="w-5 h-5 text-white" />
                    </div>
                    <p className="text-3xl font-bold text-white tracking-tight">
                      {stats.total > 0 ? Math.round((stats.completed / stats.total) * 100) : 0}%
                    </p>
                    <p className="text-xs font-medium text-white/80 mt-1">Completion</p>
                    <div className="mt-3">
                      <div className="h-1.5 bg-white/20 rounded-full overflow-hidden">
                        <div 
                          className="h-full bg-white rounded-full transition-all duration-500"
                          style={{ width: `${stats.total > 0 ? Math.round((stats.completed / stats.total) * 100) : 0}%` }}
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            )}

            {/* Modern Filters Bar - Dark Theme */}
            <div className="relative bg-gradient-to-br from-slate-800 via-slate-800 to-slate-900 rounded-2xl p-4 mb-6 shadow-xl overflow-hidden">
              {/* Decorative elements */}
              <div className="absolute top-0 right-0 w-32 h-32 bg-orange-500/10 rounded-full blur-2xl -mr-10 -mt-10" />
              <div className="absolute bottom-0 left-0 w-24 h-24 bg-amber-500/10 rounded-full blur-2xl -ml-10 -mb-10" />
              
              <div className="relative flex flex-wrap items-center gap-3">
                {/* Search - Enhanced */}
                <div className="relative flex-1 min-w-[240px] max-w-md group">
                  <div className="absolute inset-0 bg-gradient-to-r from-orange-500/20 via-amber-500/20 to-orange-500/20 rounded-xl blur-xl opacity-0 group-hover:opacity-50 group-focus-within:opacity-100 transition-all duration-500" />
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 w-8 h-8 rounded-lg bg-gradient-to-br from-orange-500/20 to-amber-500/20 flex items-center justify-center group-focus-within:from-orange-500/40 group-focus-within:to-amber-500/40 transition-all duration-300">
                      <Search className="w-4 h-4 text-orange-300 group-focus-within:text-orange-200 transition-colors" />
                    </div>
                    <Input
                      value={searchQuery}
                      onChange={(e) => setSearchQuery(e.target.value)}
                      placeholder="Search tasks by title or description..."
                      className="pl-14 pr-10 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white placeholder:text-slate-400 shadow-xl shadow-black/10 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 focus:bg-white/10 hover:bg-white/[0.07] hover:border-white/20 transition-all duration-300"
                    />
                  </div>
                </div>

                {/* Filters */}
                <div className="flex items-center gap-2 flex-wrap">
                  <div className="flex items-center gap-2 text-slate-400 mr-1">
                    <Filter className="w-4 h-4" />
                    <span className="text-sm font-medium hidden sm:inline">Filters:</span>
                  </div>

                  <Select value={priorityFilter} onValueChange={(v) => setPriorityFilter(v as TaskPriorityType | 'all')}>
                    <SelectTrigger className="w-36 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
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
                      <SelectTrigger className="w-40 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
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
                      <SelectTrigger className="w-32 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
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
                      <SelectTrigger className="w-32 h-11 rounded-xl border border-white/10 bg-white/5 backdrop-blur-md text-white shadow-xl shadow-black/10 hover:bg-white/10 hover:border-white/20 focus:border-orange-400/50 focus:ring-2 focus:ring-orange-400/20 transition-all duration-300">
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
                      className="h-11 px-4 rounded-xl text-slate-300 hover:text-white hover:bg-white/10 transition-all duration-300"
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
                <div className="grid grid-cols-3 gap-6 w-full">
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
              /* List View */
              <div className="space-y-2">
                <TaskGroupSection 
                  group="overdue" 
                  tasks={groupedTasks.overdue}
                  isCollapsed={collapsedGroups.has('overdue')}
                  onToggle={() => toggleGroupCollapse('overdue')}
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
                />
                <TaskGroupSection 
                  group="today" 
                  tasks={groupedTasks.today}
                  isCollapsed={collapsedGroups.has('today')}
                  onToggle={() => toggleGroupCollapse('today')}
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
                />
                <TaskGroupSection 
                  group="tomorrow" 
                  tasks={groupedTasks.tomorrow}
                  isCollapsed={collapsedGroups.has('tomorrow')}
                  onToggle={() => toggleGroupCollapse('tomorrow')}
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
                />
                <TaskGroupSection 
                  group="thisWeek" 
                  tasks={groupedTasks.thisWeek}
                  isCollapsed={collapsedGroups.has('thisWeek')}
                  onToggle={() => toggleGroupCollapse('thisWeek')}
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
                />
                <TaskGroupSection 
                  group="later" 
                  tasks={groupedTasks.later}
                  isCollapsed={collapsedGroups.has('later')}
                  onToggle={() => toggleGroupCollapse('later')}
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
                />
                <TaskGroupSection 
                  group="noDue" 
                  tasks={groupedTasks.noDue}
                  isCollapsed={collapsedGroups.has('noDue')}
                  onToggle={() => toggleGroupCollapse('noDue')}
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
                />
                <TaskGroupSection 
                  group="completed" 
                  tasks={groupedTasks.completed}
                  isCollapsed={collapsedGroups.has('completed')}
                  onToggle={() => toggleGroupCollapse('completed')}
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
                />
              </div>
            )}
          </>
        )}
      </main>

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
    </div>
  );
}

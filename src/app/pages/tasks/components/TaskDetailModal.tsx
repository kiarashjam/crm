import { useState, useEffect } from 'react';
import {
  Calendar,
  CheckCircle2,
  Circle,
  Clock,
  FileText,
  Flag,
  Link2,
  Pencil,
  Play,
  Target,
  Trash2,
  User,
  XCircle,
  Save,
  AlertTriangle,
  Bell,
  History,
  X,
  MessageSquare,
  Users,
  Briefcase,
} from 'lucide-react';
import { toast } from 'sonner';
import {
  Dialog,
  DialogContent,
} from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import { Avatar, AvatarFallback } from '@/app/components/ui/avatar';
import { Badge } from '@/app/components/ui/badge';
import type { TaskItem, Lead, Deal, Contact, TaskStatusType, TaskPriorityType, Activity } from '@/app/api/types';
import { statusConfig, priorityConfig } from '../config';
import { getInitials, formatDue } from '../utils';
import { getActivitiesByLead, getActivitiesByDeal } from '@/app/api/activities';

export interface TaskDetailModalProps {
  task: TaskItem | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUpdate: (taskId: string, updates: Partial<TaskItem>) => Promise<TaskItem | null>;
  onDelete: (task: TaskItem) => void;
  onStatusChange: (task: TaskItem, status: TaskStatusType) => Promise<void>;
  onPriorityChange: (task: TaskItem, priority: TaskPriorityType) => Promise<void>;
  onAssigneeChange: (task: TaskItem, assigneeId: string | null) => Promise<void>;
  onLeadChange: (task: TaskItem, leadId: string | null) => Promise<void>;
  onDealChange: (task: TaskItem, dealId: string | null) => Promise<void>;
  members: Array<{ userId: string; name: string; email: string }>;
  leads: Lead[];
  deals: Deal[];
  contacts: Contact[];
  onActivityCreated?: () => void;
}

export function TaskDetailModal({
  task,
  open,
  onOpenChange,
  onUpdate,
  onDelete,
  onStatusChange,
  onPriorityChange,
  onAssigneeChange,
  onLeadChange,
  onDealChange,
  members,
  leads,
  deals,
  contacts,
}: TaskDetailModalProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [loadingActivities, setLoadingActivities] = useState(false);
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

  useEffect(() => {
    if (task) {
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
      setIsEditing(false);
      loadRelatedActivities(task);
    }
  }, [task]);

  const loadRelatedActivities = async (t: TaskItem) => {
    setLoadingActivities(true);
    try {
      const activitiesList: Activity[] = [];
      if (t.leadId) {
        const leadActivities = await getActivitiesByLead(t.leadId);
        activitiesList.push(...leadActivities);
      }
      if (t.dealId) {
        const dealActivities = await getActivitiesByDeal(t.dealId);
        activitiesList.push(...dealActivities);
      }
      const taskActivities = activitiesList
        .filter(a => a.type === 'task')
        .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())
        .slice(0, 10);
      setActivities(taskActivities);
    } catch {
      console.error('Failed to load activities');
    } finally {
      setLoadingActivities(false);
    }
  };

  if (!task) return null;

  const priority = priorityConfig[task.priority || 'none'];
  const status = statusConfig[task.status];
  const now = new Date();
  const isOverdue = task.status !== 'completed' && task.status !== 'cancelled' && task.dueDateUtc && new Date(task.dueDateUtc) < now;

  const handleSave = async () => {
    if (!form.title.trim()) {
      toast.error('Title is required');
      return;
    }
    setSaving(true);
    try {
      const dueDateUtc = form.dueDate ? new Date(form.dueDate).toISOString() : undefined;
      const reminderDateUtc = form.reminderDate ? new Date(form.reminderDate).toISOString() : undefined;

      const updated = await onUpdate(task.id, {
        title: form.title.trim(),
        description: form.description.trim() || undefined,
        dueDateUtc,
        reminderDateUtc,
        status: form.status,
        priority: form.priority,
        notes: form.notes.trim() || undefined,
        leadId: form.leadId || undefined,
        dealId: form.dealId || undefined,
        contactId: form.contactId || undefined,
        assigneeId: form.assigneeId || undefined,
      });

      if (updated) {
        setIsEditing(false);
        toast.success('Task updated');
      }
    } catch {
      toast.error('Failed to update task');
    } finally {
      setSaving(false);
    }
  };

  const handleQuickStatusChange = async (newStatus: TaskStatusType) => {
    try {
      await onStatusChange(task, newStatus);
      setForm(f => ({ ...f, status: newStatus }));
    } catch {
      toast.error('Failed to update status');
    }
  };

  const handleQuickPriorityChange = async (newPriority: TaskPriorityType) => {
    try {
      await onPriorityChange(task, newPriority);
      setForm(f => ({ ...f, priority: newPriority }));
    } catch {
      toast.error('Failed to update priority');
    }
  };

  const handleQuickAssigneeChange = async (newAssigneeId: string) => {
    try {
      await onAssigneeChange(task, newAssigneeId === 'none' ? null : newAssigneeId);
      setForm(f => ({ ...f, assigneeId: newAssigneeId === 'none' ? '' : newAssigneeId }));
    } catch {
      toast.error('Failed to update assignee');
    }
  };

  const handleQuickLeadChange = async (newLeadId: string) => {
    try {
      await onLeadChange(task, newLeadId === 'none' ? null : newLeadId);
      setForm(f => ({ ...f, leadId: newLeadId === 'none' ? '' : newLeadId }));
    } catch {
      toast.error('Failed to update lead');
    }
  };

  const handleQuickDealChange = async (newDealId: string) => {
    try {
      await onDealChange(task, newDealId === 'none' ? null : newDealId);
      setForm(f => ({ ...f, dealId: newDealId === 'none' ? '' : newDealId }));
    } catch {
      toast.error('Failed to update deal');
    }
  };

  const formatDateTime = (isoString?: string) => {
    if (!isoString) return '-';
    return new Date(isoString).toLocaleString('en-US', {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const formatRelativeTime = (isoString: string) => {
    const date = new Date(isoString);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  };

  const StatusIcon = status.icon;

  const statusOptions = [
    { value: 'todo', label: 'To Do', icon: Circle, color: 'text-slate-500', bg: 'bg-slate-100' },
    { value: 'in_progress', label: 'In Progress', icon: Play, color: 'text-blue-600', bg: 'bg-blue-100' },
    { value: 'completed', label: 'Completed', icon: CheckCircle2, color: 'text-emerald-600', bg: 'bg-emerald-100' },
    { value: 'cancelled', label: 'Cancelled', icon: XCircle, color: 'text-slate-400', bg: 'bg-slate-100' },
  ];

  const priorityOptions = [
    { value: 'none', label: 'None', color: 'text-slate-400', bg: 'bg-slate-100', dot: 'bg-slate-300' },
    { value: 'low', label: 'Low', color: 'text-blue-600', bg: 'bg-blue-100', dot: 'bg-blue-500' },
    { value: 'medium', label: 'Medium', color: 'text-amber-600', bg: 'bg-amber-100', dot: 'bg-amber-500' },
    { value: 'high', label: 'High', color: 'text-red-600', bg: 'bg-red-100', dot: 'bg-red-500' },
  ];

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="!max-w-5xl w-[95vw] max-h-[92vh] p-0 gap-0 overflow-hidden bg-white rounded-3xl border-0 shadow-2xl">
        {/* Close Button */}
        <button
          onClick={() => onOpenChange(false)}
          className="absolute right-6 top-6 z-50 w-10 h-10 rounded-xl bg-white/10 backdrop-blur-sm border border-white/20 flex items-center justify-center text-white/70 hover:text-white hover:bg-white/20 transition-all"
        >
          <X className="w-5 h-5" />
        </button>

        {/* Header */}
        <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 px-10 py-8">
          <div className="absolute inset-0 overflow-hidden">
            <div className="absolute -top-24 -right-24 w-64 h-64 bg-orange-500/20 rounded-full blur-3xl" />
            <div className="absolute -bottom-24 -left-24 w-64 h-64 bg-violet-500/15 rounded-full blur-3xl" />
            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-32 bg-blue-500/10 rounded-full blur-3xl" />
          </div>
          
          <div className="relative">
            {/* Status & Priority Badges */}
            <div className="flex items-center gap-3 mb-5">
              <Badge className={`${status.bgColor} ${status.color} border-0 px-4 py-1.5 text-sm font-medium`}>
                <StatusIcon className="w-4 h-4 mr-2" />
                {status.label}
              </Badge>
              
              {task.priority && task.priority !== 'none' && (
                <Badge className={`${priority.bgColor} ${priority.color} border-0 px-4 py-1.5 text-sm font-medium`}>
                  <Flag className="w-4 h-4 mr-2" />
                  {priority.label}
                </Badge>
              )}
              
              {isOverdue && (
                <Badge className="bg-red-500/20 text-red-300 border border-red-500/30 px-4 py-1.5 text-sm font-medium">
                  <AlertTriangle className="w-4 h-4 mr-2" />
                  Overdue
                </Badge>
              )}
            </div>

            {/* Title */}
            <div className="flex items-start justify-between gap-6">
              <div className="flex-1">
                {isEditing ? (
                  <Input
                    value={form.title}
                    onChange={(e) => setForm(f => ({ ...f, title: e.target.value }))}
                    className="text-3xl font-bold bg-white/10 border-white/20 text-white placeholder:text-white/50 h-auto py-3 px-4 rounded-xl"
                    placeholder="Task title"
                    autoFocus
                  />
                ) : (
                  <h1 className={`text-3xl font-bold text-white tracking-tight ${task.status === 'completed' ? 'line-through opacity-70' : ''}`}>
                    {task.title}
                  </h1>
                )}
                
                <div className="flex items-center gap-4 mt-4 text-sm text-slate-400">
                  <span className="flex items-center gap-2">
                    <Clock className="w-4 h-4" />
                    Created {formatDateTime(task.createdAtUtc)}
                  </span>
                  {task.updatedAtUtc && task.updatedAtUtc !== task.createdAtUtc && (
                    <span>• Updated {formatRelativeTime(task.updatedAtUtc)}</span>
                  )}
                </div>
              </div>

              {/* Actions */}
              <div className="flex items-center gap-3">
                {isEditing ? (
                  <>
                    <Button
                      variant="ghost"
                      onClick={() => {
                        setIsEditing(false);
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
                      }}
                      className="h-11 px-5 text-white/80 hover:text-white hover:bg-white/10 rounded-xl"
                    >
                      Cancel
                    </Button>
                    <Button
                      onClick={handleSave}
                      disabled={saving}
                      className="h-11 px-6 bg-gradient-to-r from-orange-500 to-amber-500 hover:from-orange-600 hover:to-amber-600 text-white rounded-xl font-semibold shadow-lg shadow-orange-500/25"
                    >
                      {saving ? (
                        <span className="w-5 h-5 border-2 border-white border-t-transparent rounded-full animate-spin mr-2" />
                      ) : (
                        <Save className="w-5 h-5 mr-2" />
                      )}
                      Save Changes
                    </Button>
                  </>
                ) : (
                  <>
                    <Button
                      variant="ghost"
                      onClick={() => setIsEditing(true)}
                      className="h-11 px-5 text-white/80 hover:text-white hover:bg-white/10 rounded-xl"
                    >
                      <Pencil className="w-5 h-5 mr-2" />
                      Edit
                    </Button>
                    <Button
                      variant="ghost"
                      onClick={() => onDelete(task)}
                      className="h-11 px-4 text-red-400 hover:text-red-300 hover:bg-red-500/20 rounded-xl"
                    >
                      <Trash2 className="w-5 h-5" />
                    </Button>
                  </>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Content */}
        <div className="overflow-y-auto max-h-[calc(92vh-200px)]">
          <div className="p-10">
            {/* Quick Actions Row */}
            <div className="grid grid-cols-4 gap-6 mb-10">
              {/* Status */}
              <div className="space-y-3">
                <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Status</label>
                <Select value={form.status} onValueChange={handleQuickStatusChange}>
                  <SelectTrigger className="h-14 rounded-2xl border-2 border-slate-200 bg-slate-50/50 hover:bg-slate-100 hover:border-slate-300 transition-all text-base font-medium">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {statusOptions.map(opt => (
                      <SelectItem key={opt.value} value={opt.value}>
                        <span className="flex items-center gap-3">
                          <div className={`w-8 h-8 rounded-lg ${opt.bg} flex items-center justify-center`}>
                            <opt.icon className={`w-4 h-4 ${opt.color}`} />
                          </div>
                          {opt.label}
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Priority */}
              <div className="space-y-3">
                <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Priority</label>
                <Select value={form.priority} onValueChange={handleQuickPriorityChange}>
                  <SelectTrigger className="h-14 rounded-2xl border-2 border-slate-200 bg-slate-50/50 hover:bg-slate-100 hover:border-slate-300 transition-all text-base font-medium">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {priorityOptions.map(opt => (
                      <SelectItem key={opt.value} value={opt.value}>
                        <span className="flex items-center gap-3">
                          <div className={`w-3 h-3 rounded-full ${opt.dot}`} />
                          <span className={opt.value === 'none' ? 'text-slate-500' : ''}>{opt.label}</span>
                        </span>
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Due Date */}
              <div className="space-y-3">
                <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Due Date</label>
                {isEditing ? (
                  <Input
                    type="datetime-local"
                    value={form.dueDate}
                    onChange={(e) => setForm(f => ({ ...f, dueDate: e.target.value }))}
                    className="h-14 rounded-2xl border-2 border-slate-200 bg-slate-50/50"
                  />
                ) : (
                  <div className={`h-14 rounded-2xl border-2 flex items-center px-4 gap-3 ${
                    isOverdue 
                      ? 'border-red-200 bg-red-50 text-red-700' 
                      : task.dueDateUtc 
                        ? 'border-slate-200 bg-slate-50/50' 
                        : 'border-dashed border-slate-200 bg-slate-50/30'
                  }`}>
                    <Calendar className={`w-5 h-5 ${isOverdue ? 'text-red-500' : 'text-slate-400'}`} />
                    <span className={`font-medium ${!task.dueDateUtc ? 'text-slate-400' : ''}`}>
                      {formatDue(task.dueDateUtc) || 'Not set'}
                    </span>
                  </div>
                )}
              </div>

              {/* Assignee */}
              <div className="space-y-3">
                <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Assignee</label>
                {members.length > 0 ? (
                  <Select value={form.assigneeId || 'none'} onValueChange={handleQuickAssigneeChange}>
                    <SelectTrigger className="h-14 rounded-2xl border-2 border-slate-200 bg-slate-50/50 hover:bg-slate-100 hover:border-slate-300 transition-all text-base font-medium">
                      <SelectValue placeholder="Unassigned" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">
                        <span className="flex items-center gap-3 text-slate-500">
                          <div className="w-8 h-8 rounded-full bg-slate-100 flex items-center justify-center">
                            <User className="w-4 h-4 text-slate-400" />
                          </div>
                          Unassigned
                        </span>
                      </SelectItem>
                      {members.map((m) => (
                        <SelectItem key={m.userId} value={m.userId}>
                          <span className="flex items-center gap-3">
                            <Avatar className="w-8 h-8">
                              <AvatarFallback className="text-xs bg-gradient-to-br from-orange-100 to-amber-100 text-orange-700">
                                {getInitials(m.name)}
                              </AvatarFallback>
                            </Avatar>
                            {m.name}
                          </span>
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                ) : (
                  <div className="h-14 rounded-2xl border-2 border-dashed border-slate-200 bg-slate-50/30 flex items-center px-4 gap-3">
                    <User className="w-5 h-5 text-slate-400" />
                    <span className="font-medium text-slate-400">No team members</span>
                  </div>
                )}
              </div>
            </div>

            {/* Main Content Grid */}
            <div className="grid grid-cols-3 gap-8">
              {/* Left Column - Description & Notes */}
              <div className="col-span-2 space-y-8">
                {/* Description */}
                <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                  <div className="px-7 py-5 bg-gradient-to-r from-violet-50 to-purple-50/50 border-b border-violet-100/50">
                    <div className="flex items-center gap-4">
                      <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-violet-500 to-purple-600 flex items-center justify-center shadow-lg shadow-violet-500/20">
                        <MessageSquare className="w-5 h-5 text-white" />
                      </div>
                      <div>
                        <h3 className="font-semibold text-slate-900 text-lg">Description</h3>
                        <p className="text-sm text-slate-500">Task details and context</p>
                      </div>
                    </div>
                  </div>
                  <div className="p-7">
                    {isEditing ? (
                      <Textarea
                        value={form.description}
                        onChange={(e) => setForm(f => ({ ...f, description: e.target.value }))}
                        placeholder="Add a detailed description..."
                        rows={5}
                        className="rounded-2xl border-2 border-slate-200 focus:border-violet-400 focus:ring-violet-400/20 resize-none text-base"
                      />
                    ) : task.description ? (
                      <p className="text-slate-700 whitespace-pre-wrap leading-relaxed text-base">{task.description}</p>
                    ) : (
                      <div className="flex flex-col items-center justify-center py-12 text-center">
                        <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
                          <MessageSquare className="w-7 h-7 text-slate-400" />
                        </div>
                        <p className="text-slate-500 font-medium">No description</p>
                        <p className="text-sm text-slate-400 mt-1">Click edit to add details</p>
                      </div>
                    )}
                  </div>
                </div>

                {/* Notes */}
                <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                  <div className="px-7 py-5 bg-gradient-to-r from-emerald-50 to-teal-50/50 border-b border-emerald-100/50">
                    <div className="flex items-center gap-4">
                      <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-emerald-500 to-teal-600 flex items-center justify-center shadow-lg shadow-emerald-500/20">
                        <FileText className="w-5 h-5 text-white" />
                      </div>
                      <div>
                        <h3 className="font-semibold text-slate-900 text-lg">Notes</h3>
                        <p className="text-sm text-slate-500">Additional information</p>
                      </div>
                    </div>
                  </div>
                  <div className="p-7">
                    {isEditing ? (
                      <Textarea
                        value={form.notes}
                        onChange={(e) => setForm(f => ({ ...f, notes: e.target.value }))}
                        placeholder="Add notes, reminders, or context..."
                        rows={4}
                        className="rounded-2xl border-2 border-slate-200 focus:border-emerald-400 focus:ring-emerald-400/20 resize-none text-base"
                      />
                    ) : task.notes ? (
                      <p className="text-slate-700 whitespace-pre-wrap leading-relaxed text-base">{task.notes}</p>
                    ) : (
                      <div className="flex flex-col items-center justify-center py-10 text-center">
                        <div className="w-14 h-14 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
                          <FileText className="w-6 h-6 text-slate-400" />
                        </div>
                        <p className="text-slate-400">No notes yet</p>
                      </div>
                    )}
                  </div>
                </div>
              </div>

              {/* Right Column - Links & Activity */}
              <div className="space-y-8">
                {/* Linked Items */}
                <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                  <div className="px-6 py-5 bg-gradient-to-r from-blue-50 to-indigo-50/50 border-b border-blue-100/50">
                    <div className="flex items-center gap-4">
                      <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-blue-500 to-indigo-600 flex items-center justify-center shadow-lg shadow-blue-500/20">
                        <Link2 className="w-5 h-5 text-white" />
                      </div>
                      <div>
                        <h3 className="font-semibold text-slate-900 text-lg">Linked Items</h3>
                        <p className="text-sm text-slate-500">Connected records</p>
                      </div>
                    </div>
                  </div>
                  <div className="p-6 space-y-5">
                    {/* Lead */}
                    {leads.length > 0 && (
                      <div>
                        <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2 block">Lead</label>
                        <Select value={form.leadId || 'none'} onValueChange={handleQuickLeadChange}>
                          <SelectTrigger className="h-12 rounded-xl border-2 border-slate-200 bg-slate-50/50 hover:bg-slate-100">
                            <SelectValue placeholder="No lead" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">No lead</SelectItem>
                            {leads.map((l) => (
                              <SelectItem key={l.id} value={l.id}>
                                <span className="flex items-center gap-2">
                                  <Target className="w-4 h-4 text-purple-500" />
                                  {l.name}
                                </span>
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {task.leadName && (
                          <div className="flex items-center gap-3 mt-2 p-3 bg-purple-50 rounded-xl border border-purple-100">
                            <Target className="w-4 h-4 text-purple-600" />
                            <span className="text-sm font-medium text-purple-700">{task.leadName}</span>
                          </div>
                        )}
                      </div>
                    )}

                    {/* Deal */}
                    {deals.length > 0 && (
                      <div>
                        <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2 block">Deal</label>
                        <Select value={form.dealId || 'none'} onValueChange={handleQuickDealChange}>
                          <SelectTrigger className="h-12 rounded-xl border-2 border-slate-200 bg-slate-50/50 hover:bg-slate-100">
                            <SelectValue placeholder="No deal" />
                          </SelectTrigger>
                          <SelectContent>
                            <SelectItem value="none">No deal</SelectItem>
                            {deals.map((d) => (
                              <SelectItem key={d.id} value={d.id}>
                                <span className="flex items-center gap-2">
                                  <Briefcase className="w-4 h-4 text-teal-500" />
                                  {d.name}
                                </span>
                              </SelectItem>
                            ))}
                          </SelectContent>
                        </Select>
                        {task.dealName && (
                          <div className="flex items-center gap-3 mt-2 p-3 bg-teal-50 rounded-xl border border-teal-100">
                            <Briefcase className="w-4 h-4 text-teal-600" />
                            <span className="text-sm font-medium text-teal-700">{task.dealName}</span>
                          </div>
                        )}
                      </div>
                    )}

                    {/* Reminder */}
                    <div>
                      <label className="text-xs font-semibold text-slate-500 uppercase tracking-wider mb-2 block">Reminder</label>
                      {isEditing ? (
                        <Input
                          type="datetime-local"
                          value={form.reminderDate}
                          onChange={(e) => setForm(f => ({ ...f, reminderDate: e.target.value }))}
                          className="h-12 rounded-xl border-2 border-slate-200 bg-slate-50/50"
                        />
                      ) : (
                        <div className={`h-12 rounded-xl border-2 flex items-center px-4 gap-3 ${
                          task.reminderDateUtc ? 'border-slate-200 bg-slate-50/50' : 'border-dashed border-slate-200 bg-slate-50/30'
                        }`}>
                          <Bell className="w-5 h-5 text-slate-400" />
                          <span className={`font-medium ${!task.reminderDateUtc ? 'text-slate-400' : 'text-slate-700'}`}>
                            {task.reminderDateUtc ? formatDateTime(task.reminderDateUtc) : 'Not set'}
                          </span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                {/* Activity */}
                <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
                  <div className="px-6 py-5 bg-gradient-to-r from-orange-50 to-amber-50/50 border-b border-orange-100/50">
                    <div className="flex items-center gap-4">
                      <div className="w-11 h-11 rounded-2xl bg-gradient-to-br from-orange-500 to-amber-600 flex items-center justify-center shadow-lg shadow-orange-500/20">
                        <History className="w-5 h-5 text-white" />
                      </div>
                      <div>
                        <h3 className="font-semibold text-slate-900 text-lg">Activity</h3>
                        <p className="text-sm text-slate-500">Recent updates</p>
                      </div>
                    </div>
                  </div>
                  <div className="p-6">
                    {loadingActivities ? (
                      <div className="flex items-center justify-center py-10">
                        <div className="w-8 h-8 border-3 border-orange-500 border-t-transparent rounded-full animate-spin" />
                      </div>
                    ) : activities.length > 0 ? (
                      <div className="space-y-4">
                        {activities.slice(0, 5).map((activity, index) => (
                          <div key={activity.id} className="flex items-start gap-3">
                            <div className={`w-8 h-8 rounded-full flex items-center justify-center flex-shrink-0 ${
                              index === 0 ? 'bg-orange-100' : 'bg-slate-100'
                            }`}>
                              <CheckCircle2 className={`w-4 h-4 ${index === 0 ? 'text-orange-600' : 'text-slate-500'}`} />
                            </div>
                            <div className="flex-1 min-w-0">
                              <p className="text-sm font-medium text-slate-900 line-clamp-1">{activity.subject || 'Activity'}</p>
                              <p className="text-xs text-slate-400 mt-0.5">{formatRelativeTime(activity.createdAt)}</p>
                            </div>
                          </div>
                        ))}
                      </div>
                    ) : (
                      <div className="flex flex-col items-center justify-center py-10 text-center">
                        <div className="w-14 h-14 rounded-2xl bg-slate-100 flex items-center justify-center mb-4">
                          <History className="w-6 h-6 text-slate-400" />
                        </div>
                        <p className="text-slate-400 text-sm">No activity yet</p>
                      </div>
                    )}
                  </div>
                </div>

                {/* Metadata */}
                <div className="bg-slate-50 rounded-2xl p-5 space-y-3">
                  <p className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Task Info</p>
                  <div className="space-y-2 text-sm">
                    <div className="flex justify-between">
                      <span className="text-slate-500">ID</span>
                      <span className="font-mono text-slate-700">{task.id.slice(0, 8)}...</span>
                    </div>
                    {task.completedAtUtc && (
                      <div className="flex justify-between">
                        <span className="text-slate-500">Completed</span>
                        <span className="text-emerald-600 font-medium">{formatDateTime(task.completedAtUtc)}</span>
                      </div>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

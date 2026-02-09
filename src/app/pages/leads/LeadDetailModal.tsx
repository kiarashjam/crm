// LeadDetailModal - Full implementation extracted from Leads.tsx
// Handles viewing and inline editing of lead details

import { useState, useEffect } from 'react';
import {
  User,
  Building2,
  Phone,
  Mail,
  Pencil,
  ArrowRightCircle,
  CheckCircle2,
  CircleDot,
  Sparkles,
  Target,
  Users,
  Tag,
  X,
  Plus,
  Clock,
  Info,
  Activity,
  Trash2,
  Check,
  Briefcase,
} from 'lucide-react';
import { Dialog, DialogContent } from '@/app/components/ui/dialog';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/app/components/ui/select';
import { Checkbox } from '@/app/components/ui/checkbox';
import { toast } from 'sonner';
import LoadingSpinner from '@/app/components/LoadingSpinner';
import type { LeadDetailModalProps, ActivityWithUser, TaskItem } from './types';
import { STATUS_BADGE_COLORS, ACTIVITY_TYPES, LIFECYCLE_STAGES } from './config';

function LeadDetailModal({
  open,
  onOpenChange,
  lead,
  companies,
  statusOptions,
  sourceOptions,
  onEdit,
  onConvert,
  onDelete,
  onUpdate,
  orgMembers,
  currentUser,
}: LeadDetailModalProps) {
  // Editable form state - mirrors lead data
  const [editForm, setEditForm] = useState({
    name: '',
    email: '',
    phone: '',
    companyId: '',
    status: '',
    leadStatusId: '',
    source: '',
    leadSourceId: '',
    leadScore: '',
    lifecycleStage: '',
    description: '',
  });
  const [activities, setActivities] = useState<ActivityWithUser[]>([]);
  const [tasks, setTasks] = useState<TaskItem[]>([]);
  const [loadingActivities, setLoadingActivities] = useState(false);
  const [loadingTasks, setLoadingTasks] = useState(false);
  const [saving, setSaving] = useState(false);
  const [newNote, setNewNote] = useState('');
  const [addingNote, setAddingNote] = useState(false);
  const [newTask, setNewTask] = useState({ title: '', dueDate: '' });
  const [addingTask, setAddingTask] = useState(false);
  const [showAddTask, setShowAddTask] = useState(false);
  const [tags, setTags] = useState<string[]>([]);
  const [newTag, setNewTag] = useState('');
  const [assignedTo, setAssignedTo] = useState<string>('');
  const [editingField, setEditingField] = useState<string | null>(null);
  const [_hasUnsavedChanges, _setHasUnsavedChanges] = useState(false);

  // Status colors for badges - use imported config or fallback
  const statusColors = STATUS_BADGE_COLORS;

  // Initialize form when lead changes
  useEffect(() => {
    if (lead && open) {
      setEditForm({
        name: lead.name || '',
        email: lead.email || '',
        phone: lead.phone || '',
        companyId: lead.companyId || '',
        status: lead.status || 'New',
        leadStatusId: lead.leadStatusId || '',
        source: lead.source || '',
        leadSourceId: lead.leadSourceId || '',
        leadScore: lead.leadScore?.toString() || '',
        lifecycleStage: lead.lifecycleStage || '',
        description: lead.description || '',
      });
      loadActivities();
      loadTasks();
      setTags(lead.tags || []);
      setAssignedTo(lead.assignedToId || '');
      _setHasUnsavedChanges(false);
      setEditingField(null);
    }
  }, [lead?.id, open]);

  const loadActivities = async () => {
    if (!lead) return;
    setLoadingActivities(true);
    try {
      const { getActivitiesByLead } = await import('@/app/api');
      const data = await getActivitiesByLead(lead.id);
      // Add user info to activities (would come from backend in real implementation)
      const activitiesWithUser: ActivityWithUser[] = data.map(a => ({
        ...a,
        userName: currentUser?.name || 'You',
        userEmail: currentUser?.email || '',
      }));
      setActivities(activitiesWithUser);
    } catch (err) {
      console.error('Failed to load activities:', err);
    } finally {
      setLoadingActivities(false);
    }
  };

  const loadTasks = async () => {
    if (!lead) return;
    setLoadingTasks(true);
    try {
      const { getTasksByLead } = await import('@/app/api/tasks');
      const leadTasks = await getTasksByLead(lead.id);
      setTasks(leadTasks);
    } catch (err) {
      console.error('Failed to load tasks:', err);
    } finally {
      setLoadingTasks(false);
    }
  };

  // Log an activity for field changes
  const logFieldChange = async (fieldName: string, oldValue: string, newValue: string) => {
    if (!lead || oldValue === newValue) return;
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type: 'system',
        subject: `Updated ${fieldName}`,
        body: `Changed ${fieldName} from "${oldValue || '(empty)'}" to "${newValue || '(empty)'}"`,
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
      }
    } catch (err) {
      console.error('Failed to log field change:', err);
    }
  };

  // Save field change to backend
  const saveFieldChange = async (fieldName: string, value: string | number | undefined) => {
    if (!lead) return false;
    setSaving(true);
    try {
      const { updateLead } = await import('@/app/api');
      const updateData: Record<string, unknown> = {};
      
      // Map field names to API fields
      if (fieldName === 'status') {
        updateData.status = value;
        const statusOpt = statusOptions.find(s => s.name === value);
        if (statusOpt) updateData.leadStatusId = statusOpt.id;
      } else if (fieldName === 'source') {
        updateData.source = value;
        const sourceOpt = sourceOptions.find(s => s.name === value);
        if (sourceOpt) updateData.leadSourceId = sourceOpt.id;
      } else if (fieldName === 'leadScore') {
        updateData.leadScore = typeof value === 'string' ? parseInt(value, 10) : value;
      } else {
        updateData[fieldName] = value;
      }

      const updated = await updateLead(lead.id, updateData as Parameters<typeof updateLead>[1]);
      if (updated) {
        onUpdate(updated);
        return true;
      }
      return false;
    } catch (err) {
      console.error('Failed to save field:', err);
      toast.error('Failed to save changes');
      return false;
    } finally {
      setSaving(false);
    }
  };

  // Handle inline field edit
  const handleFieldSave = async (fieldName: string, newValue: string) => {
    if (!lead) return;
    const oldValue = ((lead as unknown) as Record<string, unknown>)[fieldName] as string || '';
    
    if (oldValue === newValue) {
      setEditingField(null);
      return;
    }

    const saved = await saveFieldChange(fieldName, newValue);
    if (saved) {
      await logFieldChange(fieldName, oldValue, newValue);
      toast.success(`${fieldName.charAt(0).toUpperCase() + fieldName.slice(1)} updated`);
    }
    setEditingField(null);
  };

  // Handle status change with logging
  const handleStatusChange = async (newStatus: string) => {
    if (!lead || lead.status === newStatus) return;
    
    const oldStatus = lead.status;
    setEditForm(prev => ({ ...prev, status: newStatus }));
    
    const saved = await saveFieldChange('status', newStatus);
    if (saved) {
      await logFieldChange('Status', oldStatus, newStatus);
      toast.success('Status updated');
    }
  };

  // Handle source change with logging
  const handleSourceChange = async (newSource: string) => {
    if (!lead) return;
    const oldSource = lead.source || '';
    if (oldSource === newSource) return;
    
    setEditForm(prev => ({ ...prev, source: newSource }));
    
    const saved = await saveFieldChange('source', newSource);
    if (saved) {
      await logFieldChange('Source', oldSource, newSource);
      toast.success('Source updated');
    }
  };

  // Handle score change with logging
  const handleScoreChange = async (newScore: string) => {
    if (!lead) return;
    const oldScore = lead.leadScore?.toString() || '';
    if (oldScore === newScore) return;
    
    setEditForm(prev => ({ ...prev, leadScore: newScore }));
    
    const scoreNum = parseInt(newScore, 10);
    if (isNaN(scoreNum) || scoreNum < 0 || scoreNum > 100) {
      toast.error('Score must be between 0 and 100');
      return;
    }
    
    const saved = await saveFieldChange('leadScore', scoreNum);
    if (saved) {
      await logFieldChange('Lead Score', oldScore, newScore);
      toast.success('Score updated');
    }
  };

  // Handle lifecycle stage change
  const handleLifecycleChange = async (newStage: string) => {
    if (!lead) return;
    const oldStage = lead.lifecycleStage || '';
    if (oldStage === newStage) return;
    
    setEditForm(prev => ({ ...prev, lifecycleStage: newStage }));
    
    const saved = await saveFieldChange('lifecycleStage', newStage);
    if (saved) {
      await logFieldChange('Lifecycle Stage', oldStage, newStage);
      toast.success('Lifecycle stage updated');
    }
  };

  const handleAddNote = async () => {
    if (!lead || !newNote.trim()) return;
    setAddingNote(true);
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type: 'note',
        subject: 'Note added',
        body: newNote.trim(),
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
        setNewNote('');
        toast.success('Note added');
      }
    } catch {
      toast.error('Failed to add note');
    } finally {
      setAddingNote(false);
    }
  };

  const handleLogActivity = async (type: string, details?: string) => {
    if (!lead) return;
    const subjectMap: Record<string, string> = {
      call: 'Phone Call',
      email: 'Email Sent',
      meeting: 'Meeting',
      note: 'Note',
    };
    const subject = subjectMap[type] || 'Activity';
    
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type,
        subject,
        body: details || '',
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
        toast.success(`${subject} logged`);
      }
    } catch {
      toast.error('Failed to log activity');
    }
  };

  const handleAddTask = async () => {
    if (!lead || !newTask.title.trim()) return;
    setAddingTask(true);
    try {
      const { createTask, createActivity } = await import('@/app/api');
      const task = await createTask({
        title: newTask.title.trim(),
        dueDateUtc: newTask.dueDate || undefined,
        leadId: lead.id,
      });
      if (task) {
        setTasks(prev => [...prev, task]);
        // Log the task creation
        const activity = await createActivity({
          type: 'system',
          subject: 'Task created',
          body: `Added task: "${newTask.title.trim()}"${newTask.dueDate ? ` (due ${new Date(newTask.dueDate).toLocaleDateString()})` : ''}`,
          leadId: lead.id,
        });
        if (activity) {
          const activityWithUser: ActivityWithUser = {
            ...activity,
            userName: currentUser?.name || 'You',
            userEmail: currentUser?.email || '',
          };
          setActivities(prev => [activityWithUser, ...prev]);
        }
        setNewTask({ title: '', dueDate: '' });
        setShowAddTask(false);
        toast.success('Task added');
      }
    } catch {
      toast.error('Failed to add task');
    } finally {
      setAddingTask(false);
    }
  };

  const handleToggleTask = async (task: TaskItem) => {
    try {
      const { updateTask, createActivity } = await import('@/app/api');
      const updated = await updateTask(task.id, { completed: !task.completed });
      if (updated) {
        setTasks(prev => prev.map(t => t.id === task.id ? updated : t));
        // Log task completion/uncomplete
        const activity = await createActivity({
          type: 'system',
          subject: updated.completed ? 'Task completed' : 'Task reopened',
          body: `${updated.completed ? 'Completed' : 'Reopened'}: "${task.title}"`,
          leadId: lead!.id,
        });
        if (activity) {
          const activityWithUser: ActivityWithUser = {
            ...activity,
            userName: currentUser?.name || 'You',
            userEmail: currentUser?.email || '',
          };
          setActivities(prev => [activityWithUser, ...prev]);
        }
      }
    } catch {
      toast.error('Failed to update task');
    }
  };

  const handleAddTag = async () => {
    if (!lead || !newTag.trim() || tags.includes(newTag.trim())) return;
    const tagToAdd = newTag.trim();
    const updatedTags = [...tags, tagToAdd];
    setTags(updatedTags);
    setNewTag('');
    
    // Log tag addition
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type: 'system',
        subject: 'Tag added',
        body: `Added tag: "${tagToAdd}"`,
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
      }
    } catch (err) {
      console.error('Failed to log tag addition:', err);
    }
    toast.success('Tag added');
  };

  const handleRemoveTag = async (tag: string) => {
    if (!lead) return;
    setTags(prev => prev.filter(t => t !== tag));
    
    // Log tag removal
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type: 'system',
        subject: 'Tag removed',
        body: `Removed tag: "${tag}"`,
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
      }
    } catch (err) {
      console.error('Failed to log tag removal:', err);
    }
    toast.success('Tag removed');
  };

  const handleAssignmentChange = async (userId: string) => {
    if (!lead) return;
    const oldAssignee = orgMembers.find(m => m.userId === assignedTo);
    const newAssignee = orgMembers.find(m => m.userId === userId);
    
    setAssignedTo(userId);
    
    // Log assignment change
    try {
      const { createActivity } = await import('@/app/api');
      const activity = await createActivity({
        type: 'system',
        subject: userId ? 'Lead assigned' : 'Assignment removed',
        body: userId 
          ? `Assigned to ${newAssignee?.name || 'team member'}`
          : `Removed assignment from ${oldAssignee?.name || 'previous assignee'}`,
        leadId: lead.id,
      });
      if (activity) {
        const activityWithUser: ActivityWithUser = {
          ...activity,
          userName: currentUser?.name || 'You',
          userEmail: currentUser?.email || '',
        };
        setActivities(prev => [activityWithUser, ...prev]);
      }
    } catch (err) {
      console.error('Failed to log assignment change:', err);
    }
    toast.success(userId ? 'Lead assigned' : 'Assignment removed');
  };

  if (!lead) return null;

  const companyName = companies.find(c => c.id === lead.companyId)?.name;
  const statusStyle = statusColors[lead.status] || statusColors.New || { bg: 'bg-slate-50', text: 'text-slate-600', border: 'border-slate-200', dot: 'bg-slate-400' };
  const assignee = orgMembers.find(m => m.userId === assignedTo);

  // Get initials for avatar
  const initials = lead.name
    .split(' ')
    .map(n => n[0])
    .slice(0, 2)
    .join('')
    .toUpperCase();

  // Format date helper
  const formatDate = (dateStr: string) => {
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now.getTime() - date.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMins / 60);
    const diffDays = Math.floor(diffHours / 24);

    if (diffMins < 1) return 'Just now';
    if (diffMins < 60) return `${diffMins}m ago`;
    if (diffHours < 24) return `${diffHours}h ago`;
    if (diffDays < 7) return `${diffDays}d ago`;
    return date.toLocaleDateString();
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-[900px] p-0 gap-0 overflow-hidden max-h-[90vh] flex flex-col">
        {/* Gradient Header */}
        <div className="relative overflow-hidden shrink-0">
          <div className="absolute inset-0 bg-gradient-to-br from-orange-500 via-amber-500 to-orange-400" />
          <div className="absolute inset-0 bg-[radial-gradient(circle_at_30%_20%,rgba(255,255,255,0.2),transparent_50%)]" />
          <div className="absolute -top-10 -right-10 w-40 h-40 bg-white/10 rounded-full blur-2xl" />
          
          <div className="relative px-6 py-5">
            <div className="flex items-start gap-4">
              {/* Avatar */}
              <div className="w-16 h-16 rounded-2xl bg-white/20 backdrop-blur-sm border border-white/30 flex items-center justify-center text-white font-bold text-xl shadow-lg">
                {initials || <User className="w-8 h-8" />}
              </div>
              
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-3 mb-1">
                  <h2 className="text-2xl font-bold text-white truncate">{lead.name}</h2>
                  <span className={`inline-flex items-center gap-1.5 text-xs font-medium px-2.5 py-1 rounded-lg border ${statusStyle.bg} ${statusStyle.text} ${statusStyle.border}`}>
                    <span className={`w-1.5 h-1.5 rounded-full ${statusStyle.dot}`} />
                    {lead.status}
                  </span>
                  {lead.isConverted && (
                    <span className="inline-flex items-center gap-1 text-xs font-medium px-2.5 py-1 rounded-lg bg-emerald-100 text-emerald-700 border border-emerald-200">
                      <CheckCircle2 className="w-3.5 h-3.5" />
                      Converted
                    </span>
                  )}
                </div>
                
                <div className="flex items-center gap-4 text-white/80 text-sm">
                  {lead.email && (
                    <a href={`mailto:${lead.email}`} className="flex items-center gap-1.5 hover:text-white transition-colors">
                      <Mail className="w-4 h-4" />
                      {lead.email}
                    </a>
                  )}
                  {lead.phone && (
                    <a href={`tel:${lead.phone}`} className="flex items-center gap-1.5 hover:text-white transition-colors">
                      <Phone className="w-4 h-4" />
                      {lead.phone}
                    </a>
                  )}
                  {companyName && (
                    <span className="flex items-center gap-1.5">
                      <Building2 className="w-4 h-4" />
                      {companyName}
                    </span>
                  )}
                </div>
              </div>

              {/* Quick Actions */}
              <div className="flex items-center gap-2 shrink-0">
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={() => { onOpenChange(false); onEdit(lead); }}
                  className="text-white/80 hover:text-white hover:bg-white/20"
                >
                  <Pencil className="w-4 h-4" />
                </Button>
                {!lead.isConverted && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => { onOpenChange(false); onConvert(lead); }}
                    className="text-white/80 hover:text-white hover:bg-white/20"
                  >
                    <ArrowRightCircle className="w-4 h-4" />
                  </Button>
                )}
              </div>
            </div>
          </div>
        </div>

        {/* Main Content - Two Column Layout */}
        <div className="flex-1 overflow-hidden flex">
          {/* Left Column - Lead Details with Inline Editing */}
          <div className="w-[380px] border-r border-slate-200 overflow-y-auto p-5 space-y-5 bg-slate-50/50">
            
            {/* Status Selector - Clickable */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2 mb-3">
                <CircleDot className="w-4 h-4 text-blue-500" />
                Status
                {saving && <span className="text-xs text-slate-400 ml-2">Saving...</span>}
              </h3>
              <div className="grid grid-cols-2 gap-2">
                {statusOptions.slice(0, 8).map((s) => {
                  const isSelected = editForm.status === s.name || lead.status === s.name;
                  const colors = statusColors[s.name] || statusColors.New || { bg: 'bg-slate-50', text: 'text-slate-600', border: 'border-slate-200', dot: 'bg-slate-400' };
                  return (
                    <button
                      key={s.id}
                      type="button"
                      onClick={() => handleStatusChange(s.name)}
                      disabled={saving}
                      className={`flex items-center gap-2 px-3 py-2 rounded-lg border-2 transition-all text-left ${
                        isSelected
                          ? `${colors.bg} ${colors.border} ${colors.text} shadow-sm`
                          : 'border-slate-100 bg-white hover:border-slate-200 text-slate-600'
                      }`}
                    >
                      <span className={`w-2 h-2 rounded-full ${isSelected ? colors.dot || 'bg-slate-300' : 'bg-slate-300'}`} />
                      <span className="text-xs font-medium truncate">{s.name}</span>
                      {isSelected && <Check className="w-3 h-3 ml-auto shrink-0" />}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Lead Score - Editable */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <div className="flex items-center justify-between mb-3">
                <span className="text-sm font-medium text-slate-700 flex items-center gap-2">
                  <Sparkles className="w-4 h-4 text-amber-500" />
                  Lead Score
                </span>
                <div className="flex items-center gap-2">
                  <Input
                    type="number"
                    min="0"
                    max="100"
                    value={editForm.leadScore}
                    onChange={(e) => setEditForm(prev => ({ ...prev, leadScore: e.target.value }))}
                    onBlur={() => handleScoreChange(editForm.leadScore)}
                    className="w-16 h-8 text-center text-lg font-bold"
                  />
                  <span className="text-slate-400 text-sm">/100</span>
                </div>
              </div>
              <div className="w-full bg-slate-100 rounded-full h-2">
                <div 
                  className={`h-2 rounded-full transition-all ${
                    parseInt(editForm.leadScore || '0') >= 70 ? 'bg-emerald-500' : 
                    parseInt(editForm.leadScore || '0') >= 40 ? 'bg-amber-500' : 'bg-slate-400'
                  }`}
                  style={{ width: `${Math.min(100, parseInt(editForm.leadScore || '0'))}%` }}
                />
              </div>
            </div>

            {/* Contact Info - Editable */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2">
                <User className="w-4 h-4 text-blue-500" />
                Contact Information
              </h3>
              
              {/* Name */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Name</label>
                {editingField === 'name' ? (
                  <Input
                    value={editForm.name}
                    onChange={(e) => setEditForm(prev => ({ ...prev, name: e.target.value }))}
                    onBlur={() => handleFieldSave('name', editForm.name)}
                    onKeyDown={(e) => e.key === 'Enter' && handleFieldSave('name', editForm.name)}
                    autoFocus
                    className="h-9"
                  />
                ) : (
                  <button
                    onClick={() => setEditingField('name')}
                    className="w-full text-left px-3 py-2 rounded-lg bg-slate-50 hover:bg-slate-100 text-sm font-medium text-slate-800 transition-colors flex items-center justify-between group"
                  >
                    <span>{lead.name}</span>
                    <Pencil className="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity" />
                  </button>
                )}
              </div>

              {/* Email */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Email</label>
                {editingField === 'email' ? (
                  <Input
                    type="email"
                    value={editForm.email}
                    onChange={(e) => setEditForm(prev => ({ ...prev, email: e.target.value }))}
                    onBlur={() => handleFieldSave('email', editForm.email)}
                    onKeyDown={(e) => e.key === 'Enter' && handleFieldSave('email', editForm.email)}
                    autoFocus
                    className="h-9"
                  />
                ) : (
                  <button
                    onClick={() => setEditingField('email')}
                    className="w-full text-left px-3 py-2 rounded-lg bg-slate-50 hover:bg-slate-100 text-sm text-slate-800 transition-colors flex items-center justify-between group"
                  >
                    <span className="flex items-center gap-2">
                      <Mail className="w-4 h-4 text-slate-400" />
                      {lead.email}
                    </span>
                    <Pencil className="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity" />
                  </button>
                )}
              </div>

              {/* Phone */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Phone</label>
                {editingField === 'phone' ? (
                  <Input
                    type="tel"
                    value={editForm.phone}
                    onChange={(e) => setEditForm(prev => ({ ...prev, phone: e.target.value }))}
                    onBlur={() => handleFieldSave('phone', editForm.phone)}
                    onKeyDown={(e) => e.key === 'Enter' && handleFieldSave('phone', editForm.phone)}
                    autoFocus
                    className="h-9"
                    placeholder="Add phone number"
                  />
                ) : (
                  <button
                    onClick={() => setEditingField('phone')}
                    className="w-full text-left px-3 py-2 rounded-lg bg-slate-50 hover:bg-slate-100 text-sm text-slate-800 transition-colors flex items-center justify-between group"
                  >
                    <span className="flex items-center gap-2">
                      <Phone className="w-4 h-4 text-slate-400" />
                      {lead.phone || <span className="text-slate-400">Add phone</span>}
                    </span>
                    <Pencil className="w-3.5 h-3.5 text-slate-400 opacity-0 group-hover:opacity-100 transition-opacity" />
                  </button>
                )}
              </div>

              {/* Company */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Company</label>
                <Select 
                  value={editForm.companyId || 'none'} 
                  onValueChange={async (v) => {
                    const newCompanyId = v === 'none' ? '' : v;
                    setEditForm(prev => ({ ...prev, companyId: newCompanyId }));
                    const oldCompany = companies.find(c => c.id === lead.companyId)?.name || '';
                    const newCompany = companies.find(c => c.id === newCompanyId)?.name || '';
                    if (oldCompany !== newCompany) {
                      const saved = await saveFieldChange('companyId', newCompanyId || undefined);
                      if (saved) {
                        await logFieldChange('Company', oldCompany, newCompany);
                        toast.success('Company updated');
                      }
                    }
                  }}
                >
                  <SelectTrigger className="h-9 bg-slate-50 hover:bg-slate-100 border-0">
                    <div className="flex items-center gap-2">
                      <Building2 className="w-4 h-4 text-slate-400" />
                      <SelectValue placeholder="Select company" />
                    </div>
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none"><span className="text-slate-400">No company</span></SelectItem>
                    {companies.map(c => (
                      <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Source & Lifecycle - Editable Dropdowns */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 space-y-3">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2">
                <Target className="w-4 h-4 text-orange-500" />
                Lead Details
              </h3>
              
              {/* Source */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Source</label>
                <Select value={editForm.source || lead.source || 'Manual'} onValueChange={handleSourceChange}>
                  <SelectTrigger className="h-9 bg-slate-50 hover:bg-slate-100 border-0">
                    <SelectValue placeholder="Select source" />
                  </SelectTrigger>
                  <SelectContent>
                    {sourceOptions.map(s => (
                      <SelectItem key={s.id} value={s.name}>{s.name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>

              {/* Lifecycle Stage */}
              <div className="space-y-1">
                <label className="text-xs text-slate-500">Lifecycle Stage</label>
                <Select value={editForm.lifecycleStage || 'none'} onValueChange={(v) => handleLifecycleChange(v === 'none' ? '' : v)}>
                  <SelectTrigger className="h-9 bg-slate-50 hover:bg-slate-100 border-0">
                    <SelectValue placeholder="Select stage" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="none"><span className="text-slate-400">None</span></SelectItem>
                    {LIFECYCLE_STAGES.map(stage => (
                      <SelectItem key={stage} value={stage}>{stage}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              
              {/* Read-only dates */}
              {lead.lastContactedAt && (
                <div className="flex items-center justify-between text-sm pt-2 border-t border-slate-100">
                  <span className="text-slate-500">Last Contacted</span>
                  <span className="font-medium text-slate-700">{formatDate(lead.lastContactedAt)}</span>
                </div>
              )}
              {lead.createdAtUtc && (
                <div className="flex items-center justify-between text-sm">
                  <span className="text-slate-500">Created</span>
                  <span className="font-medium text-slate-700">{formatDate(lead.createdAtUtc)}</span>
                </div>
              )}
            </div>

            {/* Assignment */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2 mb-3">
                <Users className="w-4 h-4 text-blue-500" />
                Assignment
              </h3>
              <Select value={assignedTo || 'unassigned'} onValueChange={v => handleAssignmentChange(v === 'unassigned' ? '' : v)}>
                <SelectTrigger className="h-10 bg-slate-50 border-slate-200">
                  <SelectValue placeholder="Assign to team member" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="unassigned">
                    <span className="text-slate-400">Unassigned</span>
                  </SelectItem>
                  {orgMembers.map(member => (
                    <SelectItem key={member.userId} value={member.userId}>
                      <div className="flex items-center gap-2">
                        <div className="w-6 h-6 rounded-full bg-gradient-to-br from-blue-500 to-cyan-400 flex items-center justify-center text-white text-xs font-medium">
                          {member.name.charAt(0).toUpperCase()}
                        </div>
                        <span>{member.name}</span>
                      </div>
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
              {assignee && (
                <div className="mt-3 flex items-center gap-2 text-sm text-slate-600">
                  <div className="w-8 h-8 rounded-full bg-gradient-to-br from-blue-500 to-cyan-400 flex items-center justify-center text-white text-sm font-medium">
                    {assignee.name.charAt(0).toUpperCase()}
                  </div>
                  <div>
                    <p className="font-medium text-slate-800">{assignee.name}</p>
                    <p className="text-xs text-slate-500">{assignee.email}</p>
                  </div>
                </div>
              )}
            </div>

            {/* Tags */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2 mb-3">
                <Tag className="w-4 h-4 text-purple-500" />
                Tags
              </h3>
              <div className="flex flex-wrap gap-2 mb-3">
                {tags.length === 0 ? (
                  <span className="text-sm text-slate-400">No tags yet</span>
                ) : (
                  tags.map(tag => (
                    <span
                      key={tag}
                      className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-purple-50 text-purple-700 text-sm border border-purple-200"
                    >
                      {tag}
                      <button
                        onClick={() => handleRemoveTag(tag)}
                        className="ml-1 hover:text-purple-900"
                      >
                        <X className="w-3 h-3" />
                      </button>
                    </span>
                  ))
                )}
              </div>
              <div className="flex gap-2">
                <Input
                  value={newTag}
                  onChange={e => setNewTag(e.target.value)}
                  placeholder="Add tag..."
                  className="h-9 text-sm"
                  onKeyDown={e => e.key === 'Enter' && handleAddTag()}
                />
                <Button size="sm" variant="outline" onClick={handleAddTag} disabled={!newTag.trim()}>
                  <Plus className="w-4 h-4" />
                </Button>
              </div>
            </div>

            {/* Tasks */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <div className="flex items-center justify-between mb-3">
                <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2">
                  <Clock className="w-4 h-4 text-emerald-500" />
                  Tasks
                </h3>
                <Button size="sm" variant="ghost" onClick={() => setShowAddTask(!showAddTask)}>
                  <Plus className="w-4 h-4" />
                </Button>
              </div>
              
              {showAddTask && (
                <div className="space-y-2 mb-3 p-3 bg-slate-50 rounded-lg">
                  <Input
                    value={newTask.title}
                    onChange={e => setNewTask(prev => ({ ...prev, title: e.target.value }))}
                    placeholder="Task title..."
                    className="h-9 text-sm"
                  />
                  <Input
                    type="datetime-local"
                    value={newTask.dueDate}
                    onChange={e => setNewTask(prev => ({ ...prev, dueDate: e.target.value }))}
                    className="h-9 text-sm"
                  />
                  <div className="flex gap-2">
                    <Button size="sm" onClick={handleAddTask} disabled={addingTask || !newTask.title.trim()}>
                      {addingTask ? 'Adding...' : 'Add'}
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setShowAddTask(false)}>
                      Cancel
                    </Button>
                  </div>
                </div>
              )}

              {loadingTasks ? (
                <div className="flex justify-center py-4">
                  <LoadingSpinner size="sm" />
                </div>
              ) : tasks.length === 0 ? (
                <p className="text-sm text-slate-400 text-center py-2">No tasks</p>
              ) : (
                <div className="space-y-2">
                  {tasks.slice(0, 5).map(task => (
                    <div
                      key={task.id}
                      className={`flex items-start gap-2 p-2 rounded-lg ${task.completed ? 'bg-slate-50' : 'bg-amber-50'}`}
                    >
                      <Checkbox
                        checked={task.completed}
                        onCheckedChange={() => handleToggleTask(task)}
                        className="mt-0.5"
                      />
                      <div className="flex-1 min-w-0">
                        <p className={`text-sm ${task.completed ? 'line-through text-slate-400' : 'text-slate-700'}`}>
                          {task.title}
                        </p>
                        {task.dueDateUtc && (
                          <p className="text-xs text-slate-500">{formatDate(task.dueDateUtc)}</p>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Description - Editable */}
            <div className="bg-white rounded-xl border border-slate-200 p-4">
              <h3 className="text-sm font-semibold text-slate-800 flex items-center gap-2 mb-2">
                <Info className="w-4 h-4 text-slate-500" />
                Description
                {editingField === 'description' && (
                  <span className="text-xs text-slate-400 font-normal ml-auto">Press Ctrl+Enter to save</span>
                )}
              </h3>
              {editingField === 'description' ? (
                <textarea
                  autoFocus
                  rows={4}
                  className="w-full text-sm text-slate-600 border border-slate-200 rounded-lg p-2 focus:border-blue-500 focus:ring-1 focus:ring-blue-500 resize-none"
                  value={editForm.description ?? ''}
                  onChange={(e) => setEditForm(prev => ({ ...prev, description: e.target.value }))}
                  onBlur={() => {
                    if (editForm.description !== (lead.description || '')) {
                      handleFieldSave('description', editForm.description ?? '');
                    } else {
                      setEditingField(null);
                    }
                  }}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' && e.ctrlKey) {
                      e.preventDefault();
                      if (editForm.description !== (lead.description || '')) {
                        handleFieldSave('description', editForm.description ?? '');
                      } else {
                        setEditingField(null);
                      }
                    }
                    if (e.key === 'Escape') {
                      setEditForm(prev => ({ ...prev, description: lead.description || '' }));
                      setEditingField(null);
                    }
                  }}
                  placeholder="Add notes or context about this lead..."
                />
              ) : (
                <button
                  type="button"
                  onClick={() => setEditingField('description')}
                  className="w-full text-left text-sm text-slate-600 hover:bg-slate-50 rounded-lg p-2 -m-2 transition-colors min-h-[60px]"
                >
                  {lead.description ? (
                    <span className="whitespace-pre-wrap">{lead.description}</span>
                  ) : (
                    <span className="text-slate-400 italic">Click to add description...</span>
                  )}
                </button>
              )}
            </div>
          </div>

          {/* Right Column - Activity Timeline */}
          <div className="flex-1 overflow-hidden flex flex-col bg-white">
            {/* Quick Log Buttons */}
            <div className="p-4 border-b border-slate-100 bg-slate-50/50">
              <div className="flex items-center gap-2 mb-3">
                <span className="text-sm font-medium text-slate-700">Quick Log:</span>
                {ACTIVITY_TYPES.map(type => (
                  <Button
                    key={type.id}
                    size="sm"
                    variant="outline"
                    onClick={() => handleLogActivity(type.id)}
                    className="gap-1.5 h-8"
                  >
                    <type.icon className="w-3.5 h-3.5" />
                    {type.label}
                  </Button>
                ))}
              </div>
              
              {/* Add Note Input */}
              <div className="flex gap-2">
                <Input
                  value={newNote}
                  onChange={e => setNewNote(e.target.value)}
                  placeholder="Add a note..."
                  className="flex-1"
                  onKeyDown={e => e.key === 'Enter' && !e.shiftKey && handleAddNote()}
                />
                <Button onClick={handleAddNote} disabled={addingNote || !newNote.trim()}>
                  {addingNote ? 'Adding...' : 'Add Note'}
                </Button>
              </div>
            </div>

            {/* Activity Timeline */}
            <div className="flex-1 overflow-y-auto p-4">
              <h3 className="text-sm font-semibold text-slate-800 mb-4 flex items-center gap-2">
                <Activity className="w-4 h-4 text-indigo-500" />
                Recent Interactions
              </h3>
              
              {loadingActivities ? (
                <div className="flex justify-center py-8">
                  <LoadingSpinner />
                </div>
              ) : activities.length === 0 ? (
                <div className="text-center py-12">
                  <div className="w-16 h-16 rounded-2xl bg-slate-100 flex items-center justify-center mx-auto mb-4">
                    <Activity className="w-8 h-8 text-slate-400" />
                  </div>
                  <p className="text-slate-500 mb-1">No interactions yet</p>
                  <p className="text-sm text-slate-400">Log a call, email, or meeting to get started</p>
                </div>
              ) : (
                <div className="space-y-3">
                  {activities.map((activity, idx) => {
                    const typeConfig = ACTIVITY_TYPES.find(t => t.id === activity.type) || ACTIVITY_TYPES[3] || { id: 'note', label: 'Note', icon: Briefcase, color: 'amber' };
                    const TypeIcon = typeConfig?.icon || Briefcase;
                    const isSystem = activity.type === 'system';
                    const activityWithUser = activity as ActivityWithUser;
                    
                    return (
                      <div key={activity.id} className="relative flex gap-3">
                        {/* Timeline line */}
                        {idx < activities.length - 1 && (
                          <div className="absolute left-4 top-9 bottom-0 w-px bg-slate-200" />
                        )}
                        
                        {/* Icon - smaller for system activities */}
                        <div className={`shrink-0 rounded-xl flex items-center justify-center ${
                          isSystem 
                            ? 'w-8 h-8 bg-slate-100 text-slate-500' 
                            : `w-9 h-9 ${
                              typeConfig?.color === 'blue' ? 'bg-blue-100 text-blue-600' :
                              typeConfig?.color === 'purple' ? 'bg-purple-100 text-purple-600' :
                              typeConfig?.color === 'green' ? 'bg-emerald-100 text-emerald-600' :
                              'bg-amber-100 text-amber-600'
                            }`
                        }`}>
                          <TypeIcon className={isSystem ? 'w-4 h-4' : 'w-5 h-5'} />
                        </div>
                        
                        {/* Content */}
                        <div className={`flex-1 min-w-0 pb-3 ${isSystem ? 'opacity-80' : ''}`}>
                          {/* User and timestamp line */}
                          <div className="flex items-center gap-2 mb-0.5">
                            {activityWithUser.userName && (
                              <span className={`font-medium ${isSystem ? 'text-slate-500 text-xs' : 'text-slate-700 text-sm'}`}>
                                {activityWithUser.userName}
                              </span>
                            )}
                            <span className={`${isSystem ? 'text-slate-400' : 'text-slate-500'} text-xs`}>
                              •
                            </span>
                            <span className="text-xs text-slate-400">{formatDate(activity.createdAt)}</span>
                          </div>
                          
                          {/* Activity subject/action */}
                          <div className={`flex items-center gap-2 ${isSystem ? 'mb-0' : 'mb-1'}`}>
                            <span className={`${isSystem ? 'text-sm text-slate-600' : 'font-medium text-slate-800'}`}>
                              {activity.subject || typeConfig?.label || 'Note'}
                            </span>
                            {isSystem && (
                              <span className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-medium bg-slate-100 text-slate-500 uppercase tracking-wide">
                                System
                              </span>
                            )}
                          </div>
                          
                          {/* Activity body/details */}
                          {activity.body && (
                            <p className={`text-sm whitespace-pre-wrap ${
                              isSystem 
                                ? 'text-slate-500 italic' 
                                : 'text-slate-600 bg-slate-50 rounded-lg p-3 mt-1'
                            }`}>
                              {activity.body}
                            </p>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Footer Actions */}
        <div className="shrink-0 px-6 py-4 border-t border-slate-200 bg-slate-50 flex items-center justify-between">
          <Button
            variant="ghost"
            className="text-red-600 hover:text-red-700 hover:bg-red-50"
            onClick={() => { onOpenChange(false); onDelete(lead); }}
          >
            <Trash2 className="w-4 h-4 mr-2" />
            Delete Lead
          </Button>
          
          <div className="flex items-center gap-3">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              Close
            </Button>
            {!lead.isConverted && (
              <Button
                onClick={() => { onOpenChange(false); onConvert(lead); }}
                className="gap-2 bg-gradient-to-r from-emerald-500 to-teal-500 hover:from-emerald-600 hover:to-teal-600"
              >
                <ArrowRightCircle className="w-4 h-4" />
                Convert Lead
              </Button>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}

export { LeadDetailModal };
export default LeadDetailModal;

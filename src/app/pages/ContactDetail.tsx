import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeft, Pencil, Trash2, Mail, Phone, Building2,
  Briefcase, Target, Calendar, Clock, Archive, ArchiveRestore,
  Plus, User, Activity as ActivityIcon,
  CheckCircle2, Zap
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { authFetchJson } from '@/app/api/apiClient';
import {
  getActivitiesByContact, getTasksByContact,
  getDealsPaged, updateContact, deleteContact, archiveContact,
  unarchiveContact, createActivity, messages
} from '@/app/api';
import type { Contact, Activity, TaskItem, Deal } from '@/app/api/types';
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
import { cn } from '@/app/components/ui/utils';

type ContactRaw = Contact & Record<string, unknown>;

export default function ContactDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [contact, setContact] = useState<Contact | null>(null);
  const [loading, setLoading] = useState(true);
  const [activities, setActivities] = useState<Activity[]>([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [activityForm, setActivityForm] = useState({ type: 'Note', subject: '', body: '' });
  const [savingActivity, setSavingActivity] = useState(false);
  const [contactTasks, setContactTasks] = useState<TaskItem[]>([]);
  const [tasksLoading, setTasksLoading] = useState(false);
  const [deals, setDeals] = useState<Deal[]>([]);
  const [dealsLoading, setDealsLoading] = useState(false);

  // Edit state
  const [editOpen, setEditOpen] = useState(false);
  const [editForm, setEditForm] = useState({ name: '', email: '', phone: '', jobTitle: '', description: '', doNotContact: false, preferredContactMethod: '' });
  const [saving, setSaving] = useState(false);

  // Delete state
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    authFetchJson<ContactRaw>(`/api/contacts/${id}`)
      .then((c) => { if (c) setContact(c as Contact); })
      .catch(() => {
        toast.error('Failed to load contact');
      })
      .finally(() => setLoading(false));
  }, [id]);

  // Load activities
  useEffect(() => {
    if (!id) return;
    setActivitiesLoading(true);
    getActivitiesByContact(id)
      .then(setActivities)
      .catch(() => setActivities([]))
      .finally(() => setActivitiesLoading(false));
  }, [id]);

  // Load tasks
  useEffect(() => {
    if (!id) return;
    setTasksLoading(true);
    getTasksByContact(id)
      .then(setContactTasks)
      .catch(() => setContactTasks([]))
      .finally(() => setTasksLoading(false));
  }, [id]);

  // Load deals filtered by contactId (server-side)
  useEffect(() => {
    if (!id) return;
    setDealsLoading(true);
    getDealsPaged({ page: 1, pageSize: 100, contactId: id })
      .then((result) => {
        setDeals(result.items);
      })
      .catch(() => setDeals([]))
      .finally(() => setDealsLoading(false));
  }, [id]);

  const openEdit = () => {
    if (!contact) return;
    setEditForm({
      name: contact.name,
      email: contact.email,
      phone: contact.phone ?? '',
      jobTitle: contact.jobTitle ?? '',
      description: contact.description ?? '',
      doNotContact: contact.doNotContact ?? false,
      preferredContactMethod: contact.preferredContactMethod ?? '',
    });
    setEditOpen(true);
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!contact || !editForm.name.trim() || !editForm.email.trim()) {
      toast.error('Name and email are required');
      return;
    }
    setSaving(true);
    try {
      const { contact: updated, error } = await updateContact(contact.id, {
        name: editForm.name.trim(),
        email: editForm.email.trim(),
        phone: editForm.phone.trim() || undefined,
        jobTitle: editForm.jobTitle.trim() || undefined,
        description: editForm.description.trim() || undefined,
        doNotContact: editForm.doNotContact,
        preferredContactMethod: editForm.preferredContactMethod.trim() || undefined,
      });
      if (updated) {
        setContact(updated);
        setEditOpen(false);
        toast.success('Contact updated');
      } else if (error) {
        toast.error(error);
      }
    } catch {
      toast.error('Failed to update contact');
    } finally {
      setSaving(false);
    }
  };

  const handleLogActivity = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!id || !activityForm.subject.trim()) {
      toast.error('Subject is required');
      return;
    }
    setSavingActivity(true);
    try {
      const activity = await createActivity({
        type: activityForm.type,
        subject: activityForm.subject.trim(),
        body: activityForm.body.trim() || undefined,
        contactId: id,
      });
      if (activity) {
        setActivities((prev) => [activity, ...prev]);
        setActivityForm({ type: 'Note', subject: '', body: '' });
        toast.success('Activity logged');
      }
    } catch {
      toast.error('Failed to log activity');
    } finally {
      setSavingActivity(false);
    }
  };

  const handleDelete = async () => {
    if (!contact) return;
    setDeleting(true);
    try {
      const ok = await deleteContact(contact.id);
      if (ok) {
        toast.success('Contact deleted');
        navigate('/contacts');
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const handleArchiveToggle = async () => {
    if (!contact) return;
    try {
      const ok = contact.isArchived
        ? await unarchiveContact(contact.id)
        : await archiveContact(contact.id);
      if (ok) {
        toast.success(contact.isArchived ? 'Contact unarchived' : 'Contact archived');
        // Reload contact data via direct API
        const refreshed = await authFetchJson<ContactRaw>(`/api/contacts/${contact.id}`);
        if (refreshed) setContact(refreshed as Contact);
      }
    } catch {
      toast.error('Failed to update archive status');
    }
  };

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
        <AppHeader />
        <main className="flex-1 flex items-center justify-center">
          <div className="text-center">
            <Clock className="w-8 h-8 text-slate-400 animate-spin mx-auto mb-3" />
            <p className="text-slate-500">Loading contact...</p>
          </div>
        </main>
      </div>
    );
  }

  if (!contact) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30">
        <AppHeader />
        <main className="flex-1 flex items-center justify-center">
          <div className="text-center">
            <User className="w-12 h-12 text-slate-300 mx-auto mb-3" />
            <h2 className="text-lg font-semibold text-slate-700">Contact not found</h2>
            <Link to="/contacts" className="text-emerald-600 text-sm hover:underline mt-2 block">Back to Contacts</Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full max-w-5xl mx-auto px-6 py-8" tabIndex={-1}>
          {/* Back + Actions */}
          <div className="flex items-center justify-between mb-6">
            <Button variant="ghost" onClick={() => navigate('/contacts')} className="gap-2 text-slate-600">
              <ArrowLeft className="w-4 h-4" /> Back to Contacts
            </Button>
            <div className="flex gap-2">
              <Button variant="outline" size="sm" onClick={openEdit} className="gap-1">
                <Pencil className="w-3.5 h-3.5" /> Edit
              </Button>
              <Button variant="outline" size="sm" onClick={handleArchiveToggle} className="gap-1">
                {contact.isArchived ? <ArchiveRestore className="w-3.5 h-3.5" /> : <Archive className="w-3.5 h-3.5" />}
                {contact.isArchived ? 'Unarchive' : 'Archive'}
              </Button>
              <Button variant="outline" size="sm" className="text-red-600 hover:bg-red-50" onClick={() => setDeleteOpen(true)}>
                <Trash2 className="w-3.5 h-3.5" />
              </Button>
            </div>
          </div>

          {/* Contact Header */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <div className="flex items-start justify-between">
              <div>
                <div className="flex items-center gap-3 mb-3">
                  <h1 className="text-2xl font-bold text-slate-900 dark:text-white">{contact.name}</h1>
                  {contact.doNotContact && (
                    <span className="inline-flex items-center rounded-lg px-3 py-1.5 text-sm font-bold bg-red-100 text-red-700">
                      Do Not Contact
                    </span>
                  )}
                  {contact.isArchived && (
                    <span className="inline-flex items-center rounded-lg px-3 py-1.5 text-sm font-bold bg-yellow-100 text-yellow-700">
                      Archived
                    </span>
                  )}
                </div>
                <div className="flex items-center gap-4">
                  {contact.email && (
                    <div className="flex items-center gap-2">
                      <Mail className="w-4 h-4 text-slate-400" />
                      <a href={`mailto:${contact.email}`} className="text-sm text-slate-600 dark:text-slate-300 hover:text-emerald-600">
                        {contact.email}
                      </a>
                    </div>
                  )}
                  {contact.phone && (
                    <div className="flex items-center gap-2">
                      <Phone className="w-4 h-4 text-slate-400" />
                      <a href={`tel:${contact.phone}`} className="text-sm text-slate-600 dark:text-slate-300 hover:text-emerald-600">
                        {contact.phone}
                      </a>
                    </div>
                  )}
                </div>
              </div>
            </div>

            {/* Contact Meta Grid */}
            <div className="grid grid-cols-2 md:grid-cols-4 gap-6 mt-8 pt-6 border-t border-slate-100 dark:border-slate-700">
              {contact.jobTitle && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1 flex items-center gap-1">
                    <Briefcase className="w-3 h-3" /> Job Title
                  </p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{contact.jobTitle}</p>
                </div>
              )}
              {contact.companyName && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1 flex items-center gap-1">
                    <Building2 className="w-3 h-3" /> Company
                  </p>
                  <Link
                    to={contact.companyId ? `/companies/${contact.companyId}` : '/companies'}
                    className="text-sm font-medium text-emerald-600 hover:underline"
                  >
                    {contact.companyName}
                  </Link>
                </div>
              )}
              {contact.preferredContactMethod && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1 flex items-center gap-1">
                    <Phone className="w-3 h-3" /> Preferred Method
                  </p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200 capitalize">
                    {contact.preferredContactMethod}
                  </p>
                </div>
              )}
              {contact.createdAtUtc && (
                <div>
                  <p className="text-xs text-slate-500 uppercase tracking-wider mb-1 flex items-center gap-1">
                    <Calendar className="w-3 h-3" /> Created
                  </p>
                  <p className="text-sm font-medium text-slate-800 dark:text-slate-200">
                    {new Date(contact.createdAtUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                  </p>
                </div>
              )}
            </div>

            {/* Description */}
            {contact.description && (
              <div className="mt-6 pt-4 border-t border-slate-100 dark:border-slate-700">
                <p className="text-xs text-slate-500 uppercase tracking-wider mb-2">Description</p>
                <p className="text-sm text-slate-700 dark:text-slate-300 whitespace-pre-wrap">{contact.description}</p>
              </div>
            )}

            {/* Timestamps */}
            <div className="mt-4 flex gap-4 text-xs text-slate-400">
              {contact.createdAtUtc && <span>Created {new Date(contact.createdAtUtc).toLocaleDateString()}</span>}
              {contact.updatedAtUtc && <span>Updated {new Date(contact.updatedAtUtc).toLocaleDateString()}</span>}
            </div>
          </div>

          {/* Tasks Section */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <div className="flex items-center gap-2 mb-4">
              <CheckCircle2 className="w-5 h-5 text-emerald-500" />
              <h2 className="text-lg font-semibold text-slate-800 dark:text-white">Tasks</h2>
              <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full">{contactTasks.length}</span>
            </div>

            {tasksLoading ? (
              <div className="py-6 text-center">
                <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-sm text-slate-500">Loading tasks...</p>
              </div>
            ) : contactTasks.length === 0 ? (
              <div className="py-6 text-center">
                <CheckCircle2 className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-sm text-slate-500">No tasks linked to this contact</p>
              </div>
            ) : (
              <div className="space-y-2">
                {contactTasks.map((t) => (
                  <div key={t.id} className={cn(
                    'flex items-center gap-3 p-3 bg-slate-50 dark:bg-slate-700/50 rounded-xl border border-slate-100 dark:border-slate-600 transition-all',
                    t.status === 'completed' && 'opacity-60'
                  )}>
                    <div className={cn(
                      'w-5 h-5 rounded-full border-2 flex items-center justify-center shrink-0',
                      t.status === 'completed' ? 'bg-emerald-500 border-emerald-500 text-white' : 'border-slate-300'
                    )}>
                      {t.status === 'completed' && <svg className="w-3 h-3" viewBox="0 0 12 12" fill="none"><path d="M2 6l3 3 5-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className={cn('text-sm font-medium', t.status === 'completed' ? 'text-slate-500 line-through' : 'text-slate-800 dark:text-slate-200')}>
                        {t.title}
                      </p>
                      <div className="flex items-center gap-2 mt-0.5">
                        {t.dueDateUtc && (
                          <span className={cn('text-xs', new Date(t.dueDateUtc) < new Date() && t.status !== 'completed' ? 'text-red-500' : 'text-slate-400')}>
                            Due {new Date(t.dueDateUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                          </span>
                        )}
                        {t.priority && t.priority !== 'none' && (
                          <span className={cn('text-xs px-1.5 py-0.5 rounded',
                            t.priority === 'high' && 'bg-red-100 text-red-600',
                            t.priority === 'medium' && 'bg-amber-100 text-amber-600',
                            t.priority === 'low' && 'bg-blue-100 text-blue-600'
                          )}>
                            {t.priority}
                          </span>
                        )}
                        {t.assigneeName && <span className="text-xs text-slate-400">{t.assigneeName}</span>}
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>

          {/* Deals Section */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8 mb-6">
            <div className="flex items-center gap-2 mb-4">
              <Target className="w-5 h-5 text-emerald-500" />
              <h2 className="text-lg font-semibold text-slate-800 dark:text-white">Deals</h2>
              <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full">{deals.length}</span>
            </div>

            {dealsLoading ? (
              <div className="py-6 text-center">
                <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                <p className="text-sm text-slate-500">Loading deals...</p>
              </div>
            ) : deals.length === 0 ? (
              <div className="py-6 text-center">
                <Target className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                <p className="text-sm text-slate-500">No deals linked to this contact</p>
              </div>
            ) : (
              <div className="space-y-2">
                {deals.map((deal) => (
                  <Link
                    key={deal.id}
                    to={`/deals/${deal.id}`}
                    className="flex items-center justify-between p-3 bg-slate-50 dark:bg-slate-700/50 rounded-xl border border-slate-100 dark:border-slate-600 hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors group"
                  >
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-slate-800 dark:text-slate-200 group-hover:text-emerald-600 truncate">
                        {deal.name}
                      </p>
                      <p className="text-xs text-slate-500">{deal.dealStageName || deal.stage || 'No stage'}</p>
                    </div>
                    <p className="text-sm font-bold text-emerald-600">
                      {deal.currency || '$'}{deal.value}
                    </p>
                  </Link>
                ))}
              </div>
            )}
          </div>

          {/* Activities Section */}
          <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg p-8">
            <div className="flex items-center gap-2 mb-4">
              <ActivityIcon className="w-5 h-5 text-slate-500" />
              <h2 className="text-lg font-semibold text-slate-800 dark:text-white">Activities</h2>
              <span className="text-xs bg-slate-100 text-slate-600 px-2 py-0.5 rounded-full">{activities.length}</span>
            </div>

            <form onSubmit={handleLogActivity} className="mb-6 p-4 bg-slate-50 dark:bg-slate-700/50 rounded-xl border border-slate-200 dark:border-slate-600">
              <div className="flex gap-2 mb-2">
                <Select value={activityForm.type} onValueChange={(v) => setActivityForm((f) => ({ ...f, type: v }))}>
                  <SelectTrigger className="w-28 h-9 text-sm bg-white dark:bg-slate-800"><SelectValue /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="Note">Note</SelectItem>
                    <SelectItem value="Call">Call</SelectItem>
                    <SelectItem value="Email">Email</SelectItem>
                    <SelectItem value="Meeting">Meeting</SelectItem>
                    <SelectItem value="Task">Task</SelectItem>
                  </SelectContent>
                </Select>
                <Input
                  placeholder="Subject..."
                  value={activityForm.subject}
                  onChange={(e) => setActivityForm((f) => ({ ...f, subject: e.target.value }))}
                  className="flex-1 h-9 text-sm bg-white dark:bg-slate-800"
                />
              </div>
              <textarea
                placeholder="Details (optional)..."
                value={activityForm.body}
                onChange={(e) => setActivityForm((f) => ({ ...f, body: e.target.value }))}
                className="w-full px-3 py-2 text-sm border border-slate-200 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20"
                rows={2}
              />
              <div className="flex justify-end mt-2">
                <Button type="submit" size="sm" disabled={savingActivity || !activityForm.subject.trim()} className="bg-emerald-600 hover:bg-emerald-700 text-white">
                  <Plus className="w-3 h-3 mr-1" /> Log Activity
                </Button>
              </div>
            </form>

            <div className="space-y-3">
              {activitiesLoading ? (
                <div className="py-8 text-center">
                  <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                  <p className="text-sm text-slate-500">Loading activities...</p>
                </div>
              ) : activities.length === 0 ? (
                <div className="py-8 text-center">
                  <ActivityIcon className="w-10 h-10 text-slate-300 mx-auto mb-2" />
                  <p className="text-sm text-slate-500">No activities yet</p>
                </div>
              ) : (
                activities.map((a) => (
                  <div key={a.id} className="flex gap-3 p-4 bg-white dark:bg-slate-700/50 rounded-xl border border-slate-100 dark:border-slate-600">
                    <div className={cn(
                      'w-9 h-9 rounded-lg flex items-center justify-center shrink-0',
                      a.type === 'Call' && 'bg-blue-100 text-blue-600',
                      a.type === 'Email' && 'bg-purple-100 text-purple-600',
                      a.type === 'Meeting' && 'bg-amber-100 text-amber-600',
                      a.type === 'Task' && 'bg-emerald-100 text-emerald-600',
                      a.type === 'Note' && 'bg-slate-100 text-slate-600',
                      !['Call', 'Email', 'Meeting', 'Task', 'Note'].includes(a.type) && 'bg-slate-100 text-slate-600'
                    )}>
                      {a.type === 'Call' && <Zap className="w-4 h-4" />}
                      {a.type === 'Email' && <Mail className="w-4 h-4" />}
                      {a.type === 'Meeting' && <User className="w-4 h-4" />}
                      {a.type === 'Task' && <CheckCircle2 className="w-4 h-4" />}
                      {a.type === 'Note' && <Pencil className="w-4 h-4" />}
                      {!['Call', 'Email', 'Meeting', 'Task', 'Note'].includes(a.type) && <ActivityIcon className="w-4 h-4" />}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{a.subject || a.type}</p>
                      <p className="text-xs text-slate-400 mt-0.5">{a.type} &middot; {new Date(a.createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}</p>
                      {a.body && <p className="text-xs text-slate-600 dark:text-slate-400 mt-1">{a.body}</p>}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </main>
      </PageTransition>

      {/* Edit Dialog */}
      <Dialog open={editOpen} onOpenChange={setEditOpen}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader><DialogTitle>Edit contact</DialogTitle></DialogHeader>
          <form onSubmit={handleEditSubmit} className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label htmlFor="edit-name">Name *</Label>
                <Input id="edit-name" value={editForm.name} onChange={(e) => setEditForm(f => ({ ...f, name: e.target.value }))} required />
              </div>
              <div>
                <Label htmlFor="edit-email">Email *</Label>
                <Input id="edit-email" type="email" value={editForm.email} onChange={(e) => setEditForm(f => ({ ...f, email: e.target.value }))} required />
              </div>
              <div>
                <Label htmlFor="edit-phone">Phone</Label>
                <Input id="edit-phone" value={editForm.phone} onChange={(e) => setEditForm(f => ({ ...f, phone: e.target.value }))} />
              </div>
              <div>
                <Label htmlFor="edit-jobTitle">Job Title</Label>
                <Input id="edit-jobTitle" value={editForm.jobTitle} onChange={(e) => setEditForm(f => ({ ...f, jobTitle: e.target.value }))} />
              </div>
              <div>
                <Label htmlFor="edit-preferredMethod">Preferred Contact Method</Label>
                <Select value={editForm.preferredContactMethod || '_none_'} onValueChange={(v) => setEditForm(f => ({ ...f, preferredContactMethod: v === '_none_' ? '' : v }))}>
                  <SelectTrigger id="edit-preferredMethod"><SelectValue placeholder="Select..." /></SelectTrigger>
                  <SelectContent>
                    <SelectItem value="_none_">None</SelectItem>
                    <SelectItem value="email">Email</SelectItem>
                    <SelectItem value="phone">Phone</SelectItem>
                    <SelectItem value="meeting">Meeting</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <div className="flex items-center gap-2 pt-6">
                <input
                  id="edit-doNotContact"
                  type="checkbox"
                  checked={editForm.doNotContact}
                  onChange={(e) => setEditForm(f => ({ ...f, doNotContact: e.target.checked }))}
                  className="rounded border-slate-300"
                />
                <Label htmlFor="edit-doNotContact" className="text-sm text-red-600">Do Not Contact</Label>
              </div>
            </div>
            <div>
              <Label htmlFor="edit-description">Description</Label>
              <textarea
                id="edit-description"
                value={editForm.description}
                onChange={(e) => setEditForm(f => ({ ...f, description: e.target.value }))}
                className="w-full px-3 py-2 text-sm border border-slate-200 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-800 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20"
                rows={3}
              />
            </div>
            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditOpen(false)} disabled={saving}>Cancel</Button>
              <Button type="submit" disabled={saving} className="bg-emerald-600 hover:bg-emerald-700 text-white">
                {saving ? 'Saving...' : 'Save Changes'}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Delete Dialog */}
      <Dialog open={deleteOpen} onOpenChange={setDeleteOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader><DialogTitle>Delete contact</DialogTitle></DialogHeader>
          <p className="text-slate-600">Are you sure you want to delete <strong>{contact?.name}</strong>?</p>
          {(deals.length > 0 || activities.length > 0) && (
            <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
              <p className="font-medium">Warning: linked records will be unlinked</p>
              <ul className="mt-1 list-disc list-inside text-xs">
                {deals.length > 0 && <li>{deals.length} deal{deals.length > 1 ? 's' : ''} will lose their contact link</li>}
                {activities.length > 0 && <li>{activities.length} activit{activities.length > 1 ? 'ies' : 'y'} will lose their contact link</li>}
              </ul>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deleting}>Cancel</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>{deleting ? 'Deleting...' : 'Delete'}</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

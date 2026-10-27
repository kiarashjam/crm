import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  Pencil, Trash2, Mail, Phone, Building2,
  Briefcase, Target, Calendar, Archive, ArchiveRestore,
  User, Activity as ActivityIcon,
  CheckCircle2,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { Spinner } from '@/app/components/motion/CadenceLoader';
import { RecordSkeleton } from '@/app/components/PageSkeleton';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmailComposerDialog from '@/app/components/EmailComposerDialog';
import CustomFieldsCard from '@/app/components/CustomFieldsCard';
import AttachmentsCard from '@/app/components/AttachmentsCard';
import ReferredLeadsCard from '@/app/components/ReferredLeadsCard';
import RelationshipsCard from '@/app/components/RelationshipsCard';
import { useOrg } from '@/app/contexts/OrgContext';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import {
  getActivitiesByContact, getTasksByContact,
  getDealsPaged, updateContact, deleteContact, archiveContact,
  unarchiveContact, createActivity, messages,
  getContactById, getContactLookalikes, mergePersonContacts, type ContactLookalike,
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
import { currencyOf, formatDealSum, formatMoney, parseAmount } from '@/app/lib/money';
import { WriteOnly } from '@/app/components/WriteOnly';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroSecondary } from '@/app/components/PageHero';
import { RecordAvatar, RecordCard } from '@/app/components/RecordCard';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { ActivityLogForm, ActivityRow } from '@/app/components/RecordActivity';

/** A deal's amount in its own currency, however the value was typed. */
function dealAmount(deal: Deal): string {
  return formatMoney(parseAmount(deal.value), currencyOf(deal));
}

export default function ContactDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [contact, setContact] = useState<Contact | null>(null);
  useDocumentTitle(contact?.name, 'Contacts');
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
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
  const [emailOpen, setEmailOpen] = useState(false);
  const [lookalike, setLookalike] = useState<ContactLookalike | null>(null);
  const [merging, setMerging] = useState(false);
  const [dismissedLookalike, setDismissedLookalike] = useState(false);
  const [recordRevision, setRecordRevision] = useState(0);
  const { isReadOnly } = useOrg();

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    getContactById(id)
      .then((c) => { if (c) setContact(c); })
      .catch(() => {
        toast.error('Failed to load contact');
      })
      .finally(() => setLoading(false));
  }, [id, recordRevision]);

  useEffect(() => {
    if (!id) {
      setLookalike(null);
      return;
    }
    let cancelled = false;
    setDismissedLookalike(false);
    getContactLookalikes(id)
      .then((list) => { if (!cancelled) setLookalike(list[0] ?? null); })
      .catch(() => { if (!cancelled) setLookalike(null); });
    return () => { cancelled = true; };
  }, [id, recordRevision]);

  // Load activities
  useEffect(() => {
    if (!id) return;
    setActivitiesLoading(true);
    getActivitiesByContact(id)
      .then(setActivities)
      .catch(() => setActivities([]))
      .finally(() => setActivitiesLoading(false));
  }, [id, recordRevision]);

  // Load tasks
  useEffect(() => {
    if (!id) return;
    setTasksLoading(true);
    getTasksByContact(id)
      .then(setContactTasks)
      .catch(() => setContactTasks([]))
      .finally(() => setTasksLoading(false));
  }, [id, recordRevision]);

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
  }, [id, recordRevision]);

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
        const refreshed = await getContactById(contact.id);
        if (refreshed) setContact(refreshed);
      }
    } catch {
      toast.error('Failed to update archive status');
    }
  };

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


  if (!contact) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
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

  const openTaskCount = contactTasks.filter((t) => t.status !== 'completed').length;
  const createdLabel = contact.createdAtUtc
    ? new Date(contact.createdAtUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
    : null;
  const dealsHint = deals.length > 0 ? `${formatDealSum(deals)} in all` : 'none yet';
  const lastActivityHint = activities[0]
    ? `last ${new Date(activities[0].createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}`
    : 'none yet';

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={User}
            avatar={<RecordAvatar name={contact.name} tone="emerald" />}
            tone="emerald"
            back={{ to: '/contacts', label: 'Back to Contacts' }}
            title={contact.name}
            badges={
              <>
                {contact.doNotContact && (
                  <span className="inline-flex items-center rounded-full bg-red-100 px-2 py-0.5 text-[11px] font-bold text-red-700">Do Not Contact</span>
                )}
                {contact.isArchived && (
                  <span className="inline-flex items-center rounded-full bg-yellow-100 px-2 py-0.5 text-[11px] font-bold text-yellow-700">Archived</span>
                )}
              </>
            }
            subtitle={
              <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
                {contact.email && (
                  <a href={`mailto:${contact.email}`} className="inline-flex items-center gap-1.5 hover:text-emerald-700">
                    <Mail className="h-3.5 w-3.5" aria-hidden /> {contact.email}
                  </a>
                )}
                {contact.phone && (
                  <a href={`tel:${contact.phone}`} className="inline-flex items-center gap-1.5 hover:text-emerald-700">
                    <Phone className="h-3.5 w-3.5" aria-hidden /> {contact.phone}
                  </a>
                )}
                {(contact.jobTitle || contact.companyName) && (
                  <span className="inline-flex items-center gap-1.5">
                    <Briefcase className="h-3.5 w-3.5" aria-hidden />
                    {[contact.jobTitle, contact.companyName].filter(Boolean).join(' · ')}
                  </span>
                )}
              </span>
            }
            actions={
              <>
                {contact.email && !contact.doNotContact && (
                  <WriteOnly>
                    <Button variant="outline" onClick={() => setEmailOpen(true)} className={heroSecondary}>
                      <Mail className="h-4 w-4" /> Email
                    </Button>
                  </WriteOnly>
                )}
                <WriteOnly>
                  <Button variant="outline" onClick={openEdit} className={heroSecondary}>
                    <Pencil className="h-4 w-4" /> Edit
                  </Button>
                </WriteOnly>
                <WriteOnly>
                  <Button variant="outline" onClick={handleArchiveToggle} className={heroSecondary}>
                    {contact.isArchived ? <ArchiveRestore className="h-4 w-4" /> : <Archive className="h-4 w-4" />}
                    {contact.isArchived ? 'Unarchive' : 'Archive'}
                  </Button>
                </WriteOnly>
                <WriteOnly>
                  <Button
                    variant="outline"
                    onClick={() => setDeleteOpen(true)}
                    aria-label="Delete contact"
                    className={`${heroSecondary} text-red-600 hover:border-red-200 hover:bg-red-50 hover:text-red-700`}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </WriteOnly>
              </>
            }
            stats={[
              { label: 'Open tasks', value: openTaskCount, icon: CheckCircle2, tone: 'emerald', hint: `${contactTasks.length} in all` },
              { label: 'Deals', value: deals.length, icon: Target, tone: 'violet', hint: dealsHint },
              { label: 'Activities', value: activities.length, icon: ActivityIcon, tone: 'sky', hint: lastActivityHint },
              { label: 'Contact since', value: createdLabel ?? '—', icon: Calendar, tone: 'amber', hint: contact.preferredContactMethod ? `prefers ${contact.preferredContactMethod}` : undefined },
            ]}
          />

          {lookalike && !dismissedLookalike && (
            <section className="mb-6 rounded-2xl border border-amber-200 bg-amber-50 p-5" aria-labelledby="same-person-heading">
              <h2 id="same-person-heading" className="text-base font-semibold text-amber-950">
                Is this the same person as {lookalike.name}?
              </h2>
              <p className="mt-1 text-sm text-amber-900">
                These two contacts share a name, email, or phone. Compare them, then keep one person or leave them separate.
              </p>
              <div className="mt-4 overflow-x-auto">
                <table className="w-full text-sm">
                  <thead>
                    <tr className="text-left text-amber-800">
                      <th className="py-1 pr-3 font-medium">Field</th>
                      <th className="py-1 pr-3 font-medium">This contact</th>
                      <th className="py-1 pr-3 font-medium">Other contact</th>
                      <th className="py-1 font-medium">Match</th>
                    </tr>
                  </thead>
                  <tbody>
                    {(lookalike.fields ?? []).map((field) => (
                      <tr key={field.field} className="border-t border-amber-200/80">
                        <td className="py-2 pr-3 font-medium text-slate-800">{field.field}</td>
                        <td className="py-2 pr-3 text-slate-700">{field.yours || '—'}</td>
                        <td className="py-2 pr-3 text-slate-700">{field.theirs || '—'}</td>
                        <td className="py-2 text-slate-700">{field.same ? 'Same' : 'Different'}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="mt-4 flex flex-wrap gap-2">
                <WriteOnly>
                  <Button
                    type="button"
                    className="h-11 bg-orange-600 hover:bg-orange-500"
                    disabled={merging}
                    onClick={async () => {
                      if (!contact) return;
                      setMerging(true);
                      try {
                        const ok = await mergePersonContacts(contact.id, lookalike.id);
                        if (!ok) { toast.error('Could not merge these contacts'); return; }
                        toast.success('Merged into one person');
                        setLookalike(null);
                        setRecordRevision((current) => current + 1);
                      } finally {
                        setMerging(false);
                      }
                    }}
                  >
                    {merging ? 'Merging…' : 'Yes, merge into this contact'}
                  </Button>
                </WriteOnly>
                <Button type="button" variant="outline" className="h-11" onClick={() => setDismissedLookalike(true)}>
                  No, keep them separate
                </Button>
              </div>
            </section>
          )}

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="min-w-0 space-y-5">
              <RecordCard title="Activities" icon={<ActivityIcon className="h-3.5 w-3.5" />} tone="sky" count={activities.length} index={0}>
                <ActivityLogForm draft={activityForm} onChange={setActivityForm} saving={savingActivity} onSubmit={handleLogActivity} />

                {activitiesLoading ? (
                  <div className="py-8 text-center">
                    <Spinner size={22} className="mx-auto mb-2" />
                    <p className="text-sm text-slate-500">Loading activities...</p>
                  </div>
                ) : activities.length === 0 ? (
                  <div className="py-8 text-center">
                    <ActivityIcon className="mx-auto mb-2 h-10 w-10 text-slate-300" />
                    <p className="text-sm text-slate-500">No activities yet</p>
                  </div>
                ) : (
                  <ul className="space-y-2">
                    {activities.map((a, index) => <ActivityRow key={a.id} activity={a} index={index} />)}
                  </ul>
                )}
              </RecordCard>

              <RecordCard title="Tasks" icon={<CheckCircle2 className="h-3.5 w-3.5" />} tone="emerald" count={contactTasks.length} index={1}>
                {tasksLoading ? (
                  <div className="py-6 text-center">
                    <Spinner size={22} className="mx-auto mb-2" />
                    <p className="text-sm text-slate-500">Loading tasks...</p>
                  </div>
                ) : contactTasks.length === 0 ? (
                  <div className="py-6 text-center">
                    <CheckCircle2 className="mx-auto mb-2 h-10 w-10 text-slate-300" />
                    <p className="text-sm text-slate-500">No tasks linked to this contact</p>
                  </div>
                ) : (
                  <ul className="space-y-2">
                    {contactTasks.map((t, index) => (
                      <StaggerItem as="li" key={t.id} index={index} className={cn(
                        'flex items-center gap-3 rounded-xl border border-slate-100 border-l-4 bg-slate-50 p-3 transition-all dark:border-slate-600 dark:bg-slate-700/50',
                        t.status === 'completed' ? 'border-l-emerald-400 opacity-60'
                          : t.priority === 'high' ? 'border-l-pink-500'
                            : t.priority === 'medium' ? 'border-l-amber-400' : 'border-l-sky-300',
                      )}>
                        <div className={cn(
                          'flex h-5 w-5 shrink-0 items-center justify-center rounded-full border-2',
                          t.status === 'completed' ? 'border-emerald-500 bg-emerald-500 text-white' : 'border-slate-300'
                        )}>
                          {t.status === 'completed' && <svg className="h-3 w-3" viewBox="0 0 12 12" fill="none"><path d="M2 6l3 3 5-5" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round" /></svg>}
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className={cn('text-sm font-medium', t.status === 'completed' ? 'text-slate-500 line-through' : 'text-slate-800 dark:text-slate-200')}>
                            {t.title}
                          </p>
                          <div className="mt-0.5 flex items-center gap-2">
                            {t.dueDateUtc && (
                              <span className={cn('text-xs', new Date(t.dueDateUtc) < new Date() && t.status !== 'completed' ? 'text-red-500' : 'text-slate-400')}>
                                Due {new Date(t.dueDateUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}
                              </span>
                            )}
                            {t.priority && t.priority !== 'none' && (
                              <span className={cn('rounded px-1.5 py-0.5 text-xs',
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
                      </StaggerItem>
                    ))}
                  </ul>
                )}
              </RecordCard>
            </div>

            <aside className="min-w-0 space-y-5">
              <RecordCard title="Details" icon={<User className="h-3.5 w-3.5" />} tone="emerald" index={2}>
                {!contact.jobTitle && !contact.companyName && !contact.preferredContactMethod && !contact.createdAtUtc && !contact.description && (
                  <p className="text-sm text-slate-500">No job title, company or notes yet. Add them with Edit.</p>
                )}
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm empty:hidden">
                  {contact.jobTitle && (
                    <div className="min-w-0">
                      <dt className="flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500"><Briefcase className="h-3 w-3" /> Job Title</dt>
                      <dd className="mt-0.5 truncate font-medium text-slate-800 dark:text-slate-200">{contact.jobTitle}</dd>
                    </div>
                  )}
                  {contact.companyName && (
                    <div className="min-w-0">
                      <dt className="flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500"><Building2 className="h-3 w-3" /> Company</dt>
                      <dd className="mt-0.5 truncate">
                        <Link to={contact.companyId ? `/companies/${contact.companyId}` : '/companies'} className="font-medium text-emerald-600 hover:underline">
                          {contact.companyName}
                        </Link>
                      </dd>
                    </div>
                  )}
                  {contact.preferredContactMethod && (
                    <div className="min-w-0">
                      <dt className="flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500"><Phone className="h-3 w-3" /> Preferred Method</dt>
                      <dd className="mt-0.5 font-medium capitalize text-slate-800 dark:text-slate-200">{contact.preferredContactMethod}</dd>
                    </div>
                  )}
                  {contact.createdAtUtc && (
                    <div className="min-w-0">
                      <dt className="flex items-center gap-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500"><Calendar className="h-3 w-3" /> Created</dt>
                      <dd className="mt-0.5 font-medium text-slate-800 dark:text-slate-200">{createdLabel}</dd>
                    </div>
                  )}
                </dl>
                {contact.description && (
                  <div className="mt-4 border-t border-slate-100 pt-3 dark:border-slate-700">
                    <p className="mb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500">Description</p>
                    <p className="whitespace-pre-wrap text-sm text-slate-700 dark:text-slate-300">{contact.description}</p>
                  </div>
                )}
                <div className="mt-3 flex gap-4 text-xs text-slate-400">
                  {contact.createdAtUtc && <span>Created {new Date(contact.createdAtUtc).toLocaleDateString()}</span>}
                  {contact.updatedAtUtc && <span>Updated {new Date(contact.updatedAtUtc).toLocaleDateString()}</span>}
                </div>
              </RecordCard>

              <RecordCard title="Deals" icon={<Target className="h-3.5 w-3.5" />} tone="violet" count={deals.length} index={3}>
                {dealsLoading ? (
                  <div className="py-6 text-center">
                    <Spinner size={22} className="mx-auto mb-2" />
                    <p className="text-sm text-slate-500">Loading deals...</p>
                  </div>
                ) : deals.length === 0 ? (
                  <div className="py-6 text-center">
                    <Target className="mx-auto mb-2 h-10 w-10 text-slate-300" />
                    <p className="text-sm text-slate-500">No deals linked to this contact</p>
                  </div>
                ) : (
                  <ul className="space-y-2">
                    {deals.map((deal, index) => (
                      <StaggerItem as="li" key={deal.id} index={index}>
                        <Link
                          to={`/deals/${deal.id}`}
                          className={cn('group flex items-center justify-between rounded-xl border border-slate-100 bg-slate-50 p-3 hover:border-violet-200 hover:bg-white dark:border-slate-600 dark:bg-slate-700/50 dark:hover:bg-slate-700', liftOnHover)}
                        >
                          <div className="min-w-0 flex-1">
                            <p className="truncate text-sm font-medium text-slate-800 group-hover:text-violet-700 dark:text-slate-200">
                              {deal.name}
                            </p>
                            <p className="text-xs text-slate-500">{deal.dealStageName || deal.stage || 'No stage'}</p>
                          </div>
                          <p className="text-sm font-bold text-emerald-600">{dealAmount(deal)}</p>
                        </Link>
                      </StaggerItem>
                    ))}
                  </ul>
                )}
              </RecordCard>

              {/* Spouse or partner (the couples package) and family, from either side */}
              <RelationshipsCard
                owner={{ contactId: contact.id }}
                personName={contact.name}
                readOnly={isReadOnly || !!contact.isArchived}
                revision={recordRevision}
              />

              {/* Leads this person referred, each a link to that lead */}
              <ReferredLeadsCard contactId={contact.id} contactName={contact.name} revision={recordRevision} />

              {/* Custom fields (renders only when fields are defined for contacts) */}
              <CustomFieldsCard
                entityType="contact"
                recordId={contact.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />

              <AttachmentsCard
                entityType="contact"
                recordId={contact.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </aside>
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

      {contact.email && (
        <EmailComposerDialog
          open={emailOpen}
          onOpenChange={setEmailOpen}
          to={contact.email}
          context={{ contactId: contact.id }}
          onSent={() => { if (id) getActivitiesByContact(id).then(setActivities).catch(() => {}); }}
        />
      )}
    </div>
  );
}

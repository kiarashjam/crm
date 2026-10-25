import { useState, useEffect } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
  ArrowLeft,
  Pencil,
  Trash2,
  Trophy,
  X,
  User,
  Building2,
  Calendar,
  Clock,
  Target,
  Activity as ActivityIcon,
  CheckCircle2,
  Briefcase,
  BarChart3,
  Lock,
  Unlock,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import DealLineItemsCard from '@/app/components/DealLineItemsCard';
import CustomFieldsCard from '@/app/components/CustomFieldsCard';
import AttachmentsCard from '@/app/components/AttachmentsCard';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getDealById, updateDeal, deleteDeal, messages } from '@/app/api';
import { getActivitiesByDeal, createActivity } from '@/app/api/activities';
import { getTasksByDeal } from '@/app/api/tasks';
import type { Deal, Activity, TaskItem } from '@/app/api/types';
import { Button } from '@/app/components/ui/button';
import { Label } from '@/app/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/app/components/ui/dialog';
import { cn } from '@/app/components/ui/utils';
import { UrgencyBadge, getDaysUntilClose, STAGE_COLORS } from './pipeline/DealCard';
import { WriteOnly } from '@/app/components/WriteOnly';
import { useDocumentTitle } from '@/app/hooks/useDocumentTitle';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { RecordCard } from '@/app/components/RecordCard';
import { ActivityLogForm, ActivityRow } from '@/app/components/RecordActivity';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import { currencyOf, formatMoney, parseAmount } from '@/app/lib/money';

export default function DealDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [deal, setDeal] = useState<Deal | null>(null);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);

  // Activities
  const [activities, setActivities] = useState<Activity[]>([]);
  const [activitiesLoading, setActivitiesLoading] = useState(false);
  const [activityForm, setActivityForm] = useState({ type: 'Note', subject: '', body: '' });
  const [savingActivity, setSavingActivity] = useState(false);

  // Linked Tasks
  const [dealTasks, setDealTasks] = useState<TaskItem[]>([]);
  const [tasksLoading, setTasksLoading] = useState(false);

  // Close deal dialog
  const [closeOpen, setCloseOpen] = useState(false);
  const [closeAsWon, setCloseAsWon] = useState(true);
  const [closeReason, setCloseReason] = useState('');
  const [savingClose, setSavingClose] = useState(false);
  const [savingPrivacy, setSavingPrivacy] = useState(false);

  // Delete dialog
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

  useDocumentTitle(deal?.name, 'Deals');

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    getDealById(id)
      .then((d) => setDeal(d))
      .catch(() => toast.error('Failed to load deal'))
      .finally(() => setLoading(false));
  }, [id]);

  useEffect(() => {
    if (!id) return;
    setActivitiesLoading(true);
    getActivitiesByDeal(id)
      .then(setActivities)
      .catch(() => setActivities([]))
      .finally(() => setActivitiesLoading(false));
  }, [id]);

  // Load linked tasks
  useEffect(() => {
    if (!id) return;
    setTasksLoading(true);
    getTasksByDeal(id)
      .then(setDealTasks)
      .catch(() => setDealTasks([]))
      .finally(() => setTasksLoading(false));
  }, [id]);

  const handleLogActivity = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!deal || !activityForm.subject.trim()) return;
    setSavingActivity(true);
    try {
      const activity = await createActivity({
        type: activityForm.type,
        subject: activityForm.subject.trim(),
        body: activityForm.body.trim() || undefined,
        dealId: deal.id,
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

  const handleCloseDeal = async () => {
    if (!deal) return;
    setSavingClose(true);
    try {
      const updated = await updateDeal(deal.id, {
        isWon: closeAsWon,
        closedReason: closeReason.trim() || undefined,
      });
      if (updated) {
        setDeal(updated);
        toast.success(closeAsWon ? 'Deal marked as Won!' : 'Deal marked as Lost');
        setCloseOpen(false);
        setCloseReason('');
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setSavingClose(false);
    }
  };

  // The server decides who may: the deal's creator, its owner, or a manager. Anyone
  // else gets its reason as the error.
  const handleTogglePrivate = async () => {
    if (!deal) return;
    setSavingPrivacy(true);
    try {
      const updated = await updateDeal(deal.id, { isPrivate: !deal.isPrivate });
      if (updated) {
        setDeal(updated);
        toast.success(updated.isPrivate
          ? 'Private: only its creator, its owner and managers can see this deal'
          : 'Shared with the whole team again');
      }
    } catch (err) {
      toast.error(err instanceof Error && err.message ? err.message : messages.errors.generic);
    } finally {
      setSavingPrivacy(false);
    }
  };

  const handleDelete = async () => {
    if (!deal) return;
    setDeleting(true);
    try {
      const ok = await deleteDeal(deal.id);
      if (ok) {
        toast.success(messages.success.dealDeleted);
        navigate('/deals');
      }
    } catch {
      toast.error(messages.errors.generic);
    } finally {
      setDeleting(false);
    }
  };

  const stageName = deal?.dealStageName || deal?.stage || 'Qualification';
  const daysToClose = getDaysUntilClose(deal?.expectedCloseDateUtc);
  const stageColors = STAGE_COLORS[stageName];
  const closeLabel = deal?.expectedCloseDateUtc
    ? new Date(deal.expectedCloseDateUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })
    : null;

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <div className="flex-1 flex items-center justify-center">
          <div className="animate-pulse text-slate-400">Loading deal...</div>
        </div>
      </div>
    );
  }

  if (!deal) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-subtle">
        <AppHeader />
        <div className="flex-1 flex flex-col items-center justify-center gap-4">
          <p className="text-slate-500">Deal not found</p>
          <Button variant="outline" onClick={() => navigate('/deals')}>
            <ArrowLeft className="w-4 h-4 mr-2" /> Back to Pipeline
          </Button>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Target}
            tone="violet"
            back={{ to: '/deals', label: 'Back to Pipeline' }}
            title={deal.name}
            badges={
              <>
                <span className={cn('inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-bold', stageColors?.bg, stageColors?.accent)}>
                  <span className="h-2 w-2 rounded-full" style={{ backgroundColor: stageColors?.bar ?? '#94a3b8' }} />
                  {stageName}
                </span>
                {daysToClose !== null && deal.isWon == null && <UrgencyBadge days={daysToClose} />}
                {deal.isWon != null && (
                  <span className={cn('inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[11px] font-bold', deal.isWon ? 'bg-gradient-to-r from-emerald-500 to-teal-500 text-white' : 'bg-slate-200 text-slate-600')}>
                    {deal.isWon ? <Trophy className="h-3 w-3" aria-hidden /> : <X className="h-3 w-3" aria-hidden />}
                    {deal.isWon ? 'Won' : 'Lost'}
                  </span>
                )}
                {deal.isPrivate && (
                  <span className="inline-flex items-center gap-1 rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-700">
                    <Lock className="h-3 w-3" aria-hidden /> Private
                  </span>
                )}
              </>
            }
            subtitle={(deal.contactName || deal.companyName || deal.assigneeName) && (
              <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
                {deal.contactName && <span className="inline-flex items-center gap-1.5"><User className="h-3.5 w-3.5" aria-hidden /> {deal.contactName}</span>}
                {deal.companyName && (
                  <span className="inline-flex items-center gap-1.5">
                    <Building2 className="h-3.5 w-3.5" aria-hidden />
                    {deal.companyId
                      ? <Link to={`/companies/${deal.companyId}`} className="font-medium text-violet-700 hover:underline">{deal.companyName}</Link>
                      : deal.companyName}
                  </span>
                )}
                {deal.assigneeName && <span className="inline-flex items-center gap-1.5"><Briefcase className="h-3.5 w-3.5" aria-hidden /> Owner {deal.assigneeName}</span>}
              </span>
            )}
            actions={
              <>
                <WriteOnly>
                  <Button variant="outline" onClick={() => navigate(`/deals`)} className={heroSecondary}>
                    <Pencil className="h-4 w-4" /> Edit
                  </Button>
                  <Button
                    variant="outline"
                    onClick={handleTogglePrivate}
                    disabled={savingPrivacy}
                    aria-pressed={!!deal.isPrivate}
                    className={heroSecondary}
                  >
                    {deal.isPrivate
                      ? <><Unlock className="h-4 w-4" /> Share with team</>
                      : <><Lock className="h-4 w-4" /> Make private</>}
                  </Button>
                </WriteOnly>
                {deal.isWon == null && (
                  <>
                    <WriteOnly>
                      <Button
                        variant="outline"
                        className={heroSecondary}
                        onClick={() => { setCloseOpen(true); setCloseAsWon(false); }}
                      >
                        <X className="h-4 w-4" /> Lost
                      </Button>
                    </WriteOnly>
                    <WriteOnly>
                      <Button
                        className={heroPrimary('emerald')}
                        onClick={() => { setCloseOpen(true); setCloseAsWon(true); }}
                      >
                        <Trophy className="h-4 w-4" /> Won
                      </Button>
                    </WriteOnly>
                  </>
                )}
                <WriteOnly>
                  <Button
                    variant="outline"
                    aria-label="Delete deal"
                    className={`${heroSecondary} text-red-600 hover:border-red-200 hover:bg-red-50 hover:text-red-700`}
                    onClick={() => setDeleteOpen(true)}
                  >
                    <Trash2 className="h-4 w-4" />
                  </Button>
                </WriteOnly>
              </>
            }
            stats={[
              { label: 'Value', value: parseAmount(deal.value), icon: Target, tone: 'emerald', format: (n) => formatMoney(n, currencyOf(deal)), hint: deal.pipelineName ?? undefined },
              ...(deal.probability != null
                ? [{ label: 'Win probability', value: deal.probability, icon: BarChart3, tone: 'blue' as const, format: (n: number) => `${Math.round(n)}%` }]
                : []),
              { label: 'Expected close', value: closeLabel ?? '—', icon: Calendar, tone: 'amber', hint: daysToClose === null ? undefined : daysToClose < 0 ? `${-daysToClose}d overdue` : `in ${daysToClose}d` },
              { label: 'Activities', value: activities.length, icon: ActivityIcon, tone: 'sky', hint: activities[0] ? `last ${new Date(activities[0].createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}` : 'none yet' },
              { label: 'Tasks', value: dealTasks.filter((t) => t.status !== 'completed').length, icon: CheckCircle2, tone: 'violet', hint: `${dealTasks.length} linked` },
            ]}
          />

          <div className="grid gap-5 lg:grid-cols-[minmax(0,1fr)_340px] xl:grid-cols-[minmax(0,1fr)_380px]">
            <div className="min-w-0 space-y-5">
              <RecordCard title="Activity log" icon={<ActivityIcon className="h-3.5 w-3.5" />} tone="sky" count={activities.length} index={0}>
                <ActivityLogForm draft={activityForm} onChange={setActivityForm} saving={savingActivity} onSubmit={handleLogActivity} />
                {activitiesLoading ? (
                  <div className="py-8 text-center">
                    <Clock className="mx-auto mb-2 h-5 w-5 animate-spin text-slate-400" />
                    <p className="text-xs text-slate-500">Loading activities...</p>
                  </div>
                ) : activities.length === 0 ? (
                  <div className="py-8 text-center">
                    <ActivityIcon className="mx-auto mb-3 h-10 w-10 text-slate-300" />
                    <p className="text-sm font-medium text-slate-500">No activities yet</p>
                    <p className="mt-1 text-xs text-slate-400">Log your first activity above to start tracking</p>
                  </div>
                ) : (
                  <ul className="max-h-[560px] space-y-2 overflow-y-auto pr-1">
                    {activities.map((a, index) => <ActivityRow key={a.id} activity={a} index={index} />)}
                  </ul>
                )}
              </RecordCard>

              {/* Products & line items */}
              <DealLineItemsCard
                dealId={deal.id}
                currency={deal.currency}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
                onValueSynced={(t) => setDeal((prev) => (prev ? { ...prev, value: String(t) } : prev))}
              />

              <RecordCard title="Linked tasks" icon={<CheckCircle2 className="h-3.5 w-3.5" />} tone="violet" count={dealTasks.length} index={1}>
                {tasksLoading ? (
                  <div className="py-6 text-center">
                    <Clock className="mx-auto mb-2 h-5 w-5 animate-spin text-slate-400" />
                    <p className="text-xs text-slate-500">Loading tasks...</p>
                  </div>
                ) : dealTasks.length === 0 ? (
                  <div className="py-6 text-center">
                    <CheckCircle2 className="mx-auto mb-2 h-8 w-8 text-slate-300" />
                    <p className="text-sm text-slate-500">No tasks linked to this deal</p>
                  </div>
                ) : (
                  <ul className="space-y-2">
                    {dealTasks.map((task, index) => (
                      <StaggerItem as="li" key={task.id} index={index}>
                        <Link
                          to={`/tasks/${task.id}`}
                          className={cn('group flex items-center gap-3 rounded-xl border border-slate-100 p-3 hover:border-violet-200 hover:bg-violet-50/30 dark:border-slate-700 dark:hover:border-violet-800 dark:hover:bg-violet-900/10', liftOnHover)}
                        >
                          <span className={cn(
                            'h-2.5 w-2.5 shrink-0 rounded-full',
                            task.status === 'completed' ? 'bg-emerald-400' :
                            task.status === 'in_progress' ? 'bg-blue-400' :
                            task.status === 'cancelled' ? 'bg-slate-300' :
                            'bg-amber-400'
                          )} />
                          <div className="min-w-0 flex-1">
                            <p className={cn(
                              'truncate text-sm font-medium',
                              task.status === 'completed' ? 'text-slate-400 line-through' : 'text-slate-700 group-hover:text-violet-700 dark:text-slate-200'
                            )}>
                              {task.title}
                            </p>
                            <div className="mt-0.5 flex items-center gap-2 text-xs text-slate-400">
                              <span className="capitalize">{task.status?.replace('_', ' ')}</span>
                              {task.dueDateUtc && (
                                <>
                                  <span>&middot;</span>
                                  <span>Due {new Date(task.dueDateUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric' })}</span>
                                </>
                              )}
                              {task.priority && task.priority !== 'none' && (
                                <>
                                  <span>&middot;</span>
                                  <span className={cn(
                                    'capitalize',
                                    task.priority === 'high' && 'text-red-500',
                                    task.priority === 'medium' && 'text-amber-500'
                                  )}>{task.priority}</span>
                                </>
                              )}
                            </div>
                          </div>
                        </Link>
                      </StaggerItem>
                    ))}
                  </ul>
                )}
              </RecordCard>
            </div>

            <aside className="min-w-0 space-y-5">
              {deal.isWon != null && (
                <RecordCard title={deal.isWon ? 'Deal won' : 'Deal lost'} icon={deal.isWon ? <Trophy className="h-3.5 w-3.5" /> : <X className="h-3.5 w-3.5" />} tone={deal.isWon ? 'emerald' : 'slate'} index={2}
                  className={deal.isWon ? 'border-emerald-200 bg-emerald-50/60' : undefined}
                >
                  {deal.closedAtUtc && (
                    <p className="text-xs text-slate-500">
                      {new Date(deal.closedAtUtc).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' })}
                    </p>
                  )}
                  {deal.closedReason ? <p className="mt-1 text-sm text-slate-600">{deal.closedReason}</p> : !deal.closedAtUtc && <p className="text-sm text-slate-500">Closed.</p>}
                </RecordCard>
              )}

              <RecordCard title="Deal details" icon={<Target className="h-3.5 w-3.5" />} tone="violet" index={3}>
                {!deal.contactName && !deal.companyName && !deal.assigneeName && !deal.pipelineName && !deal.expectedCloseDateUtc && !deal.createdAtUtc && !deal.description && (
                  <p className="text-sm text-slate-500">No contact, company, owner or close date yet.</p>
                )}
                <dl className="grid grid-cols-2 gap-x-4 gap-y-3 text-sm empty:hidden">
                  {deal.contactName && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Contact</dt>
                      <dd className="mt-0.5 truncate font-medium text-slate-800 dark:text-slate-200">{deal.contactName}</dd>
                    </div>
                  )}
                  {deal.companyName && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Company</dt>
                      <dd className="mt-0.5 truncate">
                        {deal.companyId ? (
                          <Link to={`/companies/${deal.companyId}`} className="font-medium text-violet-700 hover:underline">{deal.companyName}</Link>
                        ) : (
                          <span className="font-medium text-slate-800 dark:text-slate-200">{deal.companyName}</span>
                        )}
                      </dd>
                    </div>
                  )}
                  {deal.assigneeName && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Assignee</dt>
                      <dd className="mt-0.5 truncate font-medium text-slate-800 dark:text-slate-200">{deal.assigneeName}</dd>
                    </div>
                  )}
                  {deal.pipelineName && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Pipeline</dt>
                      <dd className="mt-0.5 truncate font-medium text-slate-800 dark:text-slate-200">{deal.pipelineName}</dd>
                    </div>
                  )}
                  {deal.expectedCloseDateUtc && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Expected close</dt>
                      <dd className="mt-0.5 font-medium text-slate-800 dark:text-slate-200">
                        {new Date(deal.expectedCloseDateUtc).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' })}
                      </dd>
                    </div>
                  )}
                  {deal.createdAtUtc && (
                    <div className="min-w-0">
                      <dt className="text-[10px] font-semibold uppercase tracking-wider text-slate-500">Created</dt>
                      <dd className="mt-0.5 text-slate-600 dark:text-slate-400">
                        {new Date(deal.createdAtUtc).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                      </dd>
                    </div>
                  )}
                </dl>
                {deal.description && (
                  <div className="mt-4 border-t border-slate-100 pt-3 dark:border-slate-700">
                    <p className="mb-1 text-[10px] font-semibold uppercase tracking-wider text-slate-500">Description</p>
                    <p className="whitespace-pre-wrap text-sm text-slate-600 dark:text-slate-400">{deal.description}</p>
                  </div>
                )}
              </RecordCard>

              {/* Custom fields (renders only when defined for deals) */}
              <CustomFieldsCard
                entityType="deal"
                recordId={deal.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />

              <AttachmentsCard
                entityType="deal"
                recordId={deal.id}
                className="rounded-2xl border border-slate-200/80 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </aside>
          </div>
        </main>
      </PageTransition>

      {/* Close Deal Dialog */}
      <Dialog open={closeOpen} onOpenChange={(open) => { if (!open) { setCloseOpen(false); setCloseReason(''); } }}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>{closeAsWon ? 'Mark Deal as Won' : 'Mark Deal as Lost'}</DialogTitle>
          </DialogHeader>
          <div className="space-y-4">
            <div className="flex gap-2">
              <Button
                type="button"
                variant={closeAsWon ? 'default' : 'outline'}
                className={closeAsWon ? 'bg-emerald-600 hover:bg-emerald-700 text-white' : ''}
                onClick={() => setCloseAsWon(true)}
              >
                <Trophy className="w-4 h-4 mr-1" /> Won
              </Button>
              <Button
                type="button"
                variant={!closeAsWon ? 'default' : 'outline'}
                className={!closeAsWon ? 'bg-slate-600 hover:bg-slate-700 text-white' : ''}
                onClick={() => setCloseAsWon(false)}
              >
                <X className="w-4 h-4 mr-1" /> Lost
              </Button>
            </div>
            <div>
              <Label htmlFor="deal-close-reason">Reason (optional)</Label>
              <textarea
                id="deal-close-reason"
                value={closeReason}
                onChange={(e) => setCloseReason(e.target.value)}
                placeholder={closeAsWon ? 'Why did we win this deal?' : 'Why was this deal lost?'}
                className="mt-1 w-full px-3 py-2 text-sm border border-slate-200 rounded-lg bg-white resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => { setCloseOpen(false); setCloseReason(''); }} disabled={savingClose}>Cancel</Button>
            <Button
              onClick={handleCloseDeal}
              disabled={savingClose}
              className={closeAsWon ? 'bg-emerald-600 hover:bg-emerald-700' : 'bg-slate-600 hover:bg-slate-700'}
            >
              {savingClose ? 'Saving...' : closeAsWon ? 'Mark as Won' : 'Mark as Lost'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Delete dialog */}
      <Dialog open={deleteOpen} onOpenChange={(open) => !open && setDeleteOpen(false)}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Delete deal</DialogTitle>
          </DialogHeader>
          <p className="text-slate-600">
            Are you sure you want to delete <strong>{deal.name}</strong>? This cannot be undone.
          </p>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDeleteOpen(false)} disabled={deleting}>Cancel</Button>
            <Button variant="destructive" onClick={handleDelete} disabled={deleting}>
              {deleting ? 'Deleting...' : 'Delete'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

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
  Plus,
  Zap,
  CheckCircle2,
  Briefcase,
  BarChart3,
} from 'lucide-react';
import { toast } from 'sonner';
import { motion } from 'motion/react';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { getDealById, updateDeal, deleteDeal, messages } from '@/app/api';
import { getActivitiesByDeal, createActivity } from '@/app/api/activities';
import { getTasksByDeal } from '@/app/api/tasks';
import type { Deal, Activity, TaskItem } from '@/app/api/types';
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
import { getCurrencySymbol, UrgencyBadge, getDaysUntilClose, STAGE_COLORS } from './pipeline/DealCard';

export default function DealDetail() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [deal, setDeal] = useState<Deal | null>(null);
  const [loading, setLoading] = useState(true);

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

  // Delete dialog
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleting, setDeleting] = useState(false);

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

  if (loading) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
        <AppHeader />
        <div className="flex-1 flex items-center justify-center">
          <div className="animate-pulse text-slate-400">Loading deal...</div>
        </div>
      </div>
    );
  }

  if (!deal) {
    return (
      <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30">
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
    <div className="min-h-screen flex flex-col bg-gradient-to-br from-slate-50 via-white to-emerald-50/30 dark:from-slate-900 dark:via-slate-900 dark:to-emerald-950/30">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full max-w-5xl mx-auto px-6 py-8" tabIndex={-1}>
          {/* Back button */}
          <button
            type="button"
            onClick={() => navigate('/deals')}
            className="flex items-center gap-2 text-sm text-slate-500 hover:text-slate-700 mb-6 transition-colors"
          >
            <ArrowLeft className="w-4 h-4" /> Back to Pipeline
          </button>

          {/* Deal Header */}
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-lg overflow-hidden mb-6"
          >
            {/* Top color bar */}
            <div
              className="h-2"
              style={{ backgroundColor: stageColors?.bar ?? '#94a3b8' }}
            />
            <div className="p-6 lg:p-8">
              <div className="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-4">
                <div className="flex-1 min-w-0">
                  <h1 className="text-2xl lg:text-3xl font-bold text-slate-900 dark:text-white">{deal.name}</h1>
                  <div className="flex flex-wrap items-center gap-3 mt-3">
                    <span className="inline-flex items-center gap-1.5 rounded-xl bg-gradient-to-r from-emerald-500/10 to-teal-500/10 border border-emerald-200 px-4 py-2 text-lg font-bold text-emerald-700">
                      <span className="text-sm">{getCurrencySymbol(deal.currency)}</span>
                      {deal.value}
                    </span>
                    <span
                      className={cn(
                        'inline-flex items-center gap-2 rounded-xl px-3 py-1.5 text-sm font-bold',
                        stageColors?.bg,
                        stageColors?.accent
                      )}
                    >
                      <span className="w-2.5 h-2.5 rounded-full" style={{ backgroundColor: stageColors?.bar }} />
                      {stageName}
                    </span>
                    {deal.probability != null && (
                      <span className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-xl bg-blue-50 text-blue-700 text-sm font-semibold border border-blue-200">
                        <BarChart3 className="w-3.5 h-3.5" />
                        {deal.probability}% win probability
                      </span>
                    )}
                    {daysToClose !== null && <UrgencyBadge days={daysToClose} />}
                  </div>
                </div>

                {/* Actions */}
                <div className="flex items-center gap-2 shrink-0">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => navigate(`/deals`)}
                    className="gap-1.5"
                  >
                    <Pencil className="w-3.5 h-3.5" /> Edit
                  </Button>
                  {deal.isWon == null && (
                    <>
                      <Button
                        size="sm"
                        className="gap-1.5 bg-emerald-600 hover:bg-emerald-700 text-white"
                        onClick={() => { setCloseOpen(true); setCloseAsWon(true); }}
                      >
                        <Trophy className="w-3.5 h-3.5" /> Won
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        className="gap-1.5 text-slate-600"
                        onClick={() => { setCloseOpen(true); setCloseAsWon(false); }}
                      >
                        <X className="w-3.5 h-3.5" /> Lost
                      </Button>
                    </>
                  )}
                  <Button
                    variant="outline"
                    size="sm"
                    className="text-red-600 hover:bg-red-50"
                    onClick={() => setDeleteOpen(true)}
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                  </Button>
                </div>
              </div>
            </div>
          </motion.div>

          {/* Deal Info Grid + Activities */}
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
            {/* Left column — Deal info */}
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.1 }}
              className="lg:col-span-1 space-y-4"
            >
              {/* Key details card */}
              <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-5 shadow-sm">
                <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4 flex items-center gap-2">
                  <Target className="w-4 h-4" /> Deal Details
                </h3>
                <div className="space-y-4">
                  {deal.contactName && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center shrink-0">
                        <User className="w-4 h-4 text-blue-600" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Contact</p>
                        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{deal.contactName}</p>
                      </div>
                    </div>
                  )}
                  {deal.companyName && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-purple-100 dark:bg-purple-900/30 flex items-center justify-center shrink-0">
                        <Building2 className="w-4 h-4 text-purple-600" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Company</p>
                        {deal.companyId ? (
                          <Link to={`/companies/${deal.companyId}`} className="text-sm font-medium text-emerald-600 hover:underline">
                            {deal.companyName}
                          </Link>
                        ) : (
                          <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{deal.companyName}</p>
                        )}
                      </div>
                    </div>
                  )}
                  {deal.assigneeName && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-emerald-100 dark:bg-emerald-900/30 flex items-center justify-center shrink-0">
                        <User className="w-4 h-4 text-emerald-600" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Assignee</p>
                        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{deal.assigneeName}</p>
                      </div>
                    </div>
                  )}
                  {deal.pipelineName && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center shrink-0">
                        <Briefcase className="w-4 h-4 text-amber-600" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Pipeline</p>
                        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{deal.pipelineName}</p>
                      </div>
                    </div>
                  )}
                  {deal.expectedCloseDateUtc && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-slate-100 dark:bg-slate-700 flex items-center justify-center shrink-0">
                        <Calendar className="w-4 h-4 text-slate-600" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Expected Close</p>
                        <p className="text-sm font-medium text-slate-800 dark:text-slate-200">
                          {new Date(deal.expectedCloseDateUtc).toLocaleDateString(undefined, {
                            month: 'long', day: 'numeric', year: 'numeric',
                          })}
                        </p>
                      </div>
                    </div>
                  )}
                  {deal.createdAtUtc && (
                    <div className="flex items-start gap-3">
                      <div className="w-8 h-8 rounded-lg bg-slate-100 dark:bg-slate-700 flex items-center justify-center shrink-0">
                        <Clock className="w-4 h-4 text-slate-500" />
                      </div>
                      <div>
                        <p className="text-xs text-slate-500 uppercase tracking-wider">Created</p>
                        <p className="text-sm text-slate-600 dark:text-slate-400">
                          {new Date(deal.createdAtUtc).toLocaleDateString(undefined, {
                            month: 'short', day: 'numeric', year: 'numeric',
                          })}
                        </p>
                      </div>
                    </div>
                  )}
                </div>
              </div>

              {/* Description card */}
              {deal.description && (
                <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 p-5 shadow-sm">
                  <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">Description</h3>
                  <p className="text-sm text-slate-600 dark:text-slate-400 whitespace-pre-wrap">{deal.description}</p>
                </div>
              )}

              {/* Closed info */}
              {deal.isWon != null && (
                <div className={cn(
                  'rounded-2xl border p-5 shadow-sm',
                  deal.isWon ? 'bg-emerald-50 border-emerald-200' : 'bg-slate-50 border-slate-200'
                )}>
                  <p className={cn('text-base font-bold', deal.isWon ? 'text-emerald-700' : 'text-slate-600')}>
                    {deal.isWon ? 'Deal Won' : 'Deal Lost'}
                  </p>
                  {deal.closedAtUtc && (
                    <p className="text-xs text-slate-500 mt-1">
                      {new Date(deal.closedAtUtc).toLocaleDateString(undefined, { month: 'long', day: 'numeric', year: 'numeric' })}
                    </p>
                  )}
                  {deal.closedReason && (
                    <p className="text-sm text-slate-600 mt-2">{deal.closedReason}</p>
                  )}
                </div>
              )}
              {/* Linked Tasks Section */}
              <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm mt-6">
                <div className="p-5 border-b border-slate-200 dark:border-slate-700">
                  <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-2">
                    <CheckCircle2 className="w-4 h-4" /> Linked Tasks
                    <span className="text-xs bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400 px-2 py-0.5 rounded-full">
                      {dealTasks.length}
                    </span>
                  </h3>
                </div>
                <div className="p-4 space-y-2 max-h-[350px] overflow-y-auto">
                  {tasksLoading ? (
                    <div className="py-6 text-center">
                      <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                      <p className="text-xs text-slate-500">Loading tasks...</p>
                    </div>
                  ) : dealTasks.length === 0 ? (
                    <div className="py-6 text-center">
                      <CheckCircle2 className="w-8 h-8 text-slate-300 mx-auto mb-2" />
                      <p className="text-sm text-slate-500">No tasks linked to this deal</p>
                    </div>
                  ) : (
                    dealTasks.map((task) => (
                      <Link
                        key={task.id}
                        to={`/tasks/${task.id}`}
                        className="flex items-center gap-3 p-3 rounded-xl border border-slate-100 dark:border-slate-700 hover:border-emerald-200 dark:hover:border-emerald-800 hover:bg-emerald-50/30 dark:hover:bg-emerald-900/10 transition-colors group"
                      >
                        <span className={cn(
                          'w-2.5 h-2.5 rounded-full shrink-0',
                          task.status === 'completed' ? 'bg-emerald-400' :
                          task.status === 'in_progress' ? 'bg-blue-400' :
                          task.status === 'cancelled' ? 'bg-slate-300' :
                          'bg-amber-400'
                        )} />
                        <div className="flex-1 min-w-0">
                          <p className={cn(
                            'text-sm font-medium truncate',
                            task.status === 'completed' ? 'text-slate-400 line-through' : 'text-slate-700 dark:text-slate-200'
                          )}>
                            {task.title}
                          </p>
                          <div className="flex items-center gap-2 text-xs text-slate-400 mt-0.5">
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
                    ))
                  )}
                </div>
              </div>
            </motion.div>

            {/* Right column — Activities */}
            <motion.div
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: 0.2 }}
              className="lg:col-span-2"
            >
              <div className="bg-white dark:bg-slate-800 rounded-2xl border border-slate-200 dark:border-slate-700 shadow-sm">
                <div className="p-5 border-b border-slate-200 dark:border-slate-700">
                  <div className="flex items-center justify-between">
                    <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 flex items-center gap-2">
                      <ActivityIcon className="w-4 h-4" /> Activity Log
                      <span className="text-xs bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400 px-2 py-0.5 rounded-full">
                        {activities.length}
                      </span>
                    </h3>
                  </div>
                </div>

                {/* Log Activity Form */}
                <form onSubmit={handleLogActivity} className="p-4 border-b border-slate-100 dark:border-slate-700 bg-slate-50/50 dark:bg-slate-800/50">
                  <div className="flex gap-2 mb-2">
                    <Select value={activityForm.type} onValueChange={(v) => setActivityForm((f) => ({ ...f, type: v }))}>
                      <SelectTrigger className="w-28 h-9 text-xs bg-white dark:bg-slate-700">
                        <SelectValue />
                      </SelectTrigger>
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
                      className="flex-1 h-9 text-sm bg-white dark:bg-slate-700"
                    />
                  </div>
                  <textarea
                    placeholder="Details (optional)..."
                    value={activityForm.body}
                    onChange={(e) => setActivityForm((f) => ({ ...f, body: e.target.value }))}
                    className="w-full px-3 py-2 text-sm border border-slate-200 dark:border-slate-600 rounded-lg bg-white dark:bg-slate-700 resize-none focus:outline-none focus:ring-2 focus:ring-emerald-500/20 focus:border-emerald-400"
                    rows={2}
                  />
                  <div className="flex justify-end mt-2">
                    <Button
                      type="submit"
                      size="sm"
                      disabled={savingActivity || !activityForm.subject.trim()}
                      className="h-8 text-xs bg-emerald-600 hover:bg-emerald-700 text-white"
                    >
                      <Plus className="w-3 h-3 mr-1" />
                      {savingActivity ? 'Saving...' : 'Log Activity'}
                    </Button>
                  </div>
                </form>

                {/* Activity Timeline */}
                <div className="p-4 space-y-3 max-h-[500px] overflow-y-auto">
                  {activitiesLoading ? (
                    <div className="py-8 text-center">
                      <Clock className="w-5 h-5 text-slate-400 animate-spin mx-auto mb-2" />
                      <p className="text-xs text-slate-500">Loading activities...</p>
                    </div>
                  ) : activities.length === 0 ? (
                    <div className="py-8 text-center">
                      <ActivityIcon className="w-10 h-10 text-slate-300 mx-auto mb-3" />
                      <p className="text-sm text-slate-500 font-medium">No activities yet</p>
                      <p className="text-xs text-slate-400 mt-1">Log your first activity above to start tracking</p>
                    </div>
                  ) : (
                    activities.map((a) => (
                      <div
                        key={a.id}
                        className="flex gap-3 p-3 bg-white dark:bg-slate-800 rounded-xl border border-slate-100 dark:border-slate-700 hover:border-slate-200 dark:hover:border-slate-600 transition-colors"
                      >
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
                          {a.type === 'Email' && <Building2 className="w-4 h-4" />}
                          {a.type === 'Meeting' && <User className="w-4 h-4" />}
                          {a.type === 'Task' && <CheckCircle2 className="w-4 h-4" />}
                          {a.type === 'Note' && <Pencil className="w-4 h-4" />}
                          {!['Call', 'Email', 'Meeting', 'Task', 'Note'].includes(a.type) && <ActivityIcon className="w-4 h-4" />}
                        </div>
                        <div className="flex-1 min-w-0">
                          <p className="text-sm font-medium text-slate-800 dark:text-slate-200">{a.subject || a.type}</p>
                          <p className="text-xs text-slate-400">
                            {a.type} &middot; {new Date(a.createdAt).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' })}
                          </p>
                          {a.body && <p className="text-xs text-slate-600 dark:text-slate-400 mt-1 line-clamp-3">{a.body}</p>}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </motion.div>
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

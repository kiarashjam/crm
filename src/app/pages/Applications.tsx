import { useEffect, useMemo, useState } from 'react';
import {
  ClipboardList,
  Plus,
  Search,
  Check,
  X,
  Clock,
  ChevronRight,
  Loader2,
  Mail,
  Phone,
  Briefcase,
  UserCheck,
  AlertTriangle,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
  DialogDescription,
} from '@/app/components/ui/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/app/components/ui/select';
import {
  getApplications,
  createApplication,
  updateApplication,
  deleteApplication,
  approveApplication,
  computeApplicationStats,
  APPLICATION_STATUSES,
  type Application,
  type ApplicationStatus,
} from '@/app/api/applications';
import { MEMBER_TIERS, TIER_DUES, type MemberTier } from '@/app/api/members';

const STATUS_BADGE: Record<ApplicationStatus, string> = {
  Submitted: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  UnderReview: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Rejected: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Waitlisted: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
};

const STATUS_LABEL: Record<ApplicationStatus, string> = {
  Submitted: 'Submitted',
  UnderReview: 'Under review',
  Approved: 'Approved',
  Rejected: 'Rejected',
  Waitlisted: 'Waitlisted',
};

interface FormState {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  requestedTier: MemberTier;
  referredByName: string;
  occupation: string;
  reasonForJoining: string;
}

function newForm(): FormState {
  return {
    firstName: '',
    lastName: '',
    email: '',
    phone: '',
    requestedTier: 'Silver',
    referredByName: '',
    occupation: '',
    reasonForJoining: '',
  };
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, {
      month: 'short',
      day: 'numeric',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

export default function Applications() {
  const [applications, setApplications] = useState<Application[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | ApplicationStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<FormState>(newForm());
  const [saving, setSaving] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [reviewDialog, setReviewDialog] = useState<{ app: Application; action: 'approve' | 'reject' | 'waitlist' } | null>(null);
  const [reviewerNotes, setReviewerNotes] = useState('');

  const load = async () => {
    try {
      const list = await getApplications();
      setApplications(list);
    } catch {
      toast.error('Failed to load applications');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeApplicationStats(applications), [applications]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return applications.filter((a) => {
      if (statusFilter !== 'All' && a.status !== statusFilter) return false;
      if (!q) return true;
      return `${a.firstName} ${a.lastName} ${a.email} ${a.occupation ?? ''} ${a.referredByName ?? ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [applications, search, statusFilter]);

  const openCreate = () => {
    setForm(newForm());
    setDialogOpen(true);
  };

  const submit = async () => {
    if (!form.firstName.trim() || !form.lastName.trim() || !form.email.trim()) {
      toast.error('First name, last name, and email are required');
      return;
    }
    setSaving(true);
    try {
      await createApplication({
        firstName: form.firstName.trim(),
        lastName: form.lastName.trim(),
        email: form.email.trim(),
        phone: form.phone.trim() || undefined,
        requestedTier: form.requestedTier,
        referredByName: form.referredByName.trim() || undefined,
        occupation: form.occupation.trim() || undefined,
        reasonForJoining: form.reasonForJoining.trim() || undefined,
      });
      toast.success('Application submitted');
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to submit application');
    } finally {
      setSaving(false);
    }
  };

  const startReview = (app: Application, action: 'approve' | 'reject' | 'waitlist') => {
    setReviewerNotes(app.reviewerNotes ?? '');
    setReviewDialog({ app, action });
  };

  const completeReview = async () => {
    if (!reviewDialog) return;
    const { app, action } = reviewDialog;
    setBusyId(app.id);
    try {
      if (action === 'approve') {
        const updated = await approveApplication(app.id, reviewerNotes || undefined);
        if (updated) toast.success(`${app.firstName} ${app.lastName} approved — added as pending member`);
      } else {
        const status: ApplicationStatus = action === 'reject' ? 'Rejected' : 'Waitlisted';
        await updateApplication(app.id, {
          status,
          reviewedAtUtc: new Date().toISOString(),
          reviewerNotes: reviewerNotes || undefined,
        });
        toast.success(`Application ${status.toLowerCase()}`);
      }
      setReviewDialog(null);
      await load();
    } catch {
      toast.error('Action failed');
    } finally {
      setBusyId(null);
    }
  };

  const setStatus = async (app: Application, status: ApplicationStatus) => {
    setApplications((prev) => prev.map((x) => (x.id === app.id ? { ...x, status } : x)));
    const ok = await updateApplication(app.id, { status });
    if (!ok) {
      toast.error('Failed to update');
      void load();
    }
  };

  const remove = async (a: Application) => {
    if (!confirm(`Delete application from ${a.firstName} ${a.lastName}?`)) return;
    setBusyId(a.id);
    try {
      const ok = await deleteApplication(a.id);
      if (ok) {
        toast.success('Application deleted');
        await load();
      }
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          {/* Hero */}
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0 overflow-hidden">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-blue-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-cyan-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-blue-500 to-cyan-500 flex items-center justify-center shadow-2xl shadow-blue-500/30">
                    <ClipboardList className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Applications</h1>
                    <p className="text-slate-400 mt-1">
                      Review prospective members, approve to onboard, or waitlist.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-blue-500 to-cyan-500 hover:from-blue-600 hover:to-cyan-600 shadow-lg shadow-blue-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  New application
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<Clock className="w-5 h-5 text-amber-600" />}
              label="Awaiting decision"
              value={String(stats.submitted + stats.underReview)}
              hint={`${stats.submitted} submitted · ${stats.underReview} reviewing`}
            />
            <StatCard
              icon={<UserCheck className="w-5 h-5 text-emerald-600" />}
              label="Approved · 30d"
              value={String(stats.approvedThisMonth)}
              hint="New members onboarded"
            />
            <StatCard
              icon={<AlertTriangle className="w-5 h-5 text-violet-600" />}
              label="Waitlisted"
              value={String(stats.waitlisted)}
              hint="Active waitlist"
            />
            <StatCard
              icon={<X className="w-5 h-5 text-rose-600" />}
              label="Rejected · 30d"
              value={String(stats.rejectedThisMonth)}
              hint="Closed in last 30 days"
            />
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search applicants..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={statusFilter}
              onValueChange={(v) => setStatusFilter(v as 'All' | ApplicationStatus)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-48">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {APPLICATION_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>
                    {STATUS_LABEL[s]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* List */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={ClipboardList}
              title={applications.length === 0 ? 'No applications yet' : 'No applications match these filters'}
              description={
                applications.length === 0
                  ? 'Once people apply to join, their applications land here for committee review.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={applications.length === 0 ? 'New application' : undefined}
              onAction={applications.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <div className="space-y-3">
              {filtered.map((a) => (
                <div
                  key={a.id}
                  className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm hover:shadow-md transition-shadow"
                >
                  <div className="flex flex-col lg:flex-row gap-4">
                    <div className="flex items-start gap-4 min-w-0 flex-1">
                      <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-100 to-cyan-100 flex items-center justify-center text-blue-700 font-semibold shrink-0">
                        {(a.firstName[0] ?? '?') + (a.lastName[0] ?? '')}
                      </div>
                      <div className="min-w-0 flex-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <h3 className="font-semibold text-slate-900 truncate">
                            {a.firstName} {a.lastName}
                          </h3>
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[a.status]}`}>
                            {STATUS_LABEL[a.status]}
                          </span>
                          <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">
                            Requested: {a.requestedTier}
                          </span>
                        </div>
                        <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500">
                          <span className="inline-flex items-center gap-1 truncate">
                            <Mail className="w-3.5 h-3.5" /> {a.email}
                          </span>
                          {a.phone && (
                            <span className="inline-flex items-center gap-1">
                              <Phone className="w-3.5 h-3.5" /> {a.phone}
                            </span>
                          )}
                          {a.occupation && (
                            <span className="inline-flex items-center gap-1 truncate">
                              <Briefcase className="w-3.5 h-3.5" /> {a.occupation}
                            </span>
                          )}
                          <span className="inline-flex items-center gap-1">
                            <Clock className="w-3.5 h-3.5" /> Submitted {formatDate(a.submittedAtUtc)}
                          </span>
                        </div>
                        {a.referredByName && (
                          <p className="mt-2 text-xs text-slate-500">
                            Referred by <span className="font-medium text-slate-700">{a.referredByName}</span>
                          </p>
                        )}
                        {a.reasonForJoining && (
                          <p className="mt-2 text-sm text-slate-600 line-clamp-2 italic">
                            "{a.reasonForJoining}"
                          </p>
                        )}
                        {a.reviewerNotes && (
                          <p className="mt-2 text-xs text-slate-500 bg-slate-50 rounded p-2">
                            <span className="font-medium">Committee:</span> {a.reviewerNotes}
                          </p>
                        )}
                      </div>
                    </div>
                    <div className="flex flex-row lg:flex-col gap-2 lg:items-end shrink-0">
                      {(a.status === 'Submitted' || a.status === 'UnderReview') && (
                        <>
                          <Button
                            size="sm"
                            onClick={() => startReview(a, 'approve')}
                            disabled={busyId === a.id}
                            className="bg-emerald-600 hover:bg-emerald-700 text-white"
                          >
                            <Check className="w-3.5 h-3.5 mr-1" /> Approve
                          </Button>
                          <div className="flex gap-1.5">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => startReview(a, 'waitlist')}
                              disabled={busyId === a.id}
                            >
                              Waitlist
                            </Button>
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => startReview(a, 'reject')}
                              disabled={busyId === a.id}
                              className="text-rose-600 hover:bg-rose-50"
                            >
                              <X className="w-3.5 h-3.5" />
                            </Button>
                          </div>
                        </>
                      )}
                      {a.status === 'Submitted' && (
                        <button
                          type="button"
                          onClick={() => setStatus(a, 'UnderReview')}
                          className="text-xs text-blue-600 hover:underline inline-flex items-center gap-0.5"
                        >
                          Move to review <ChevronRight className="w-3 h-3" />
                        </button>
                      )}
                      {(a.status === 'Rejected' || a.status === 'Approved' || a.status === 'Waitlisted') && (
                        <button
                          type="button"
                          onClick={() => remove(a)}
                          disabled={busyId === a.id}
                          className="text-xs text-slate-500 hover:text-rose-600 disabled:opacity-50"
                        >
                          Delete record
                        </button>
                      )}
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}
        </main>
      </PageTransition>

      {/* Create application */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>New application</DialogTitle>
            <DialogDescription>
              Submit a prospective member for committee review.
            </DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div>
              <Label>First name</Label>
              <Input value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
            </div>
            <div>
              <Label>Last name</Label>
              <Input value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
            </div>
            <div>
              <Label>Email</Label>
              <Input type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </div>
            <div>
              <Label>Phone</Label>
              <Input value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </div>
            <div>
              <Label>Requested tier</Label>
              <Select
                value={form.requestedTier}
                onValueChange={(v) => setForm({ ...form, requestedTier: v as MemberTier })}
              >
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {MEMBER_TIERS.map((t) => (
                    <SelectItem key={t} value={t}>
                      {t} — ${TIER_DUES[t]}/mo
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Referred by</Label>
              <Input
                value={form.referredByName}
                onChange={(e) => setForm({ ...form, referredByName: e.target.value })}
                placeholder="Existing member name"
              />
            </div>
            <div className="md:col-span-2">
              <Label>Occupation</Label>
              <Input
                value={form.occupation}
                onChange={(e) => setForm({ ...form, occupation: e.target.value })}
              />
            </div>
            <div className="md:col-span-2">
              <Label>Reason for joining</Label>
              <Textarea
                value={form.reasonForJoining}
                onChange={(e) => setForm({ ...form, reasonForJoining: e.target.value })}
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Submitting
                </>
              ) : (
                'Submit application'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Review confirmation */}
      <Dialog open={reviewDialog !== null} onOpenChange={(o) => !o && setReviewDialog(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {reviewDialog?.action === 'approve' && `Approve ${reviewDialog.app.firstName}?`}
              {reviewDialog?.action === 'reject' && `Reject ${reviewDialog?.app.firstName}'s application?`}
              {reviewDialog?.action === 'waitlist' && `Waitlist ${reviewDialog?.app.firstName}?`}
            </DialogTitle>
            <DialogDescription>
              {reviewDialog?.action === 'approve' &&
                'Approving will add them as a Pending member with the requested tier — they activate when first dues are collected.'}
              {reviewDialog?.action === 'reject' && 'The application will be marked as rejected. They can reapply later.'}
              {reviewDialog?.action === 'waitlist' &&
                'The application goes to the waitlist queue and can be revisited when capacity allows.'}
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <Label>Committee notes (optional)</Label>
            <Textarea
              value={reviewerNotes}
              onChange={(e) => setReviewerNotes(e.target.value)}
              rows={3}
              placeholder="Context for the file..."
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setReviewDialog(null)}
              disabled={busyId === reviewDialog?.app.id}
            >
              Cancel
            </Button>
            <Button
              onClick={completeReview}
              disabled={busyId === reviewDialog?.app.id}
              className={
                reviewDialog?.action === 'approve'
                  ? 'bg-emerald-600 hover:bg-emerald-700'
                  : reviewDialog?.action === 'reject'
                    ? 'bg-rose-600 hover:bg-rose-700'
                    : ''
              }
            >
              {busyId === reviewDialog?.app.id ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Working
                </>
              ) : reviewDialog?.action === 'approve' ? (
                'Approve and onboard'
              ) : reviewDialog?.action === 'reject' ? (
                'Reject application'
              ) : (
                'Move to waitlist'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

function StatCard({
  icon,
  label,
  value,
  hint,
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
  hint?: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <div className="flex items-center justify-between mb-2">
        <div className="w-10 h-10 rounded-xl bg-slate-50 flex items-center justify-center">{icon}</div>
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500 mt-0.5">{label}</p>
      {hint && <p className="text-xs text-slate-400 mt-1">{hint}</p>}
    </div>
  );
}

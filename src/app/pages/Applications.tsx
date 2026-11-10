import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  AlertTriangle,
  Briefcase,
  Check,
  ChevronRight,
  ClipboardList,
  Clock,
  Copy,
  Globe,
  Loader2,
  Mail,
  Phone,
  Plus,
  RotateCcw,
  Search,
  UserCheck,
  X,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Textarea } from '@/app/components/ui/textarea';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/app/components/ui/dialog';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import {
  APPLICATION_STATUSES,
  approveApplication,
  computeApplicationStats,
  createApplication,
  deleteApplication,
  isOpen,
  rotateApplicationForm,
  saveApplicationForm,
  setApplicationStatus,
  type Application,
  type ApplicationForm,
  type ApplicationStatus,
} from '@/app/api/applications';
import { MEMBER_ROLES, type HouseholdRole } from '@/app/api/households';
import { MEMBER_TIERS, type MemberTier } from '@/app/api/members';
import { CADENCE_LABEL, tiersOnSale } from '@/app/api/catalog';
import { ApiError, servesDemoData } from '@/app/api/apiClient';
import { NONE, useApplicationForm, useApplications, useClubCatalog, useClubReads, useHouseholds } from '@/app/hooks/queries';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { orgCan, PERMISSION, type Permission } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';

const STATUS_BADGE: Record<ApplicationStatus, string> = {
  Submitted: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  UnderReview: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Approved: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Rejected: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Waitlisted: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
};

/** What an application without a requested tier says instead: the applicant was not sure yet. */
const NO_TIER = 'no tier chosen';

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

function newForm(tier: MemberTier): FormState {
  return { firstName: '', lastName: '', email: '', phone: '', requestedTier: tier, referredByName: '', occupation: '', reasonForJoining: '' };
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric', year: 'numeric' });
  } catch {
    return iso;
  }
}

function errorText(err: unknown, fallback: string): string {
  return err instanceof Error && err.message ? err.message : fallback;
}

/** Whether the signed-in member may do this, as WriteOnly decides it. */
function useMay(): (permission: Permission) => boolean {
  const org = useOrgOptional();
  return (permission) => !org || (!org.isReadOnly && orgCan(org.currentOrg ?? null, permission));
}

type Review = { app: Application; action: 'waitlist' | 'reject' };

interface ApproveState {
  app: Application;
  notes: string;
  tier: string;
  householdId: string;
  householdRole: HouseholdRole;
  rollUpBilling: boolean;
}

export default function Applications() {
  const { format: formatCurrency } = useClubMoney();
  const may = useMay();
  const canMoney = may(PERMISSION.ManageMoney);
  const canSettings = may(PERMISSION.ManageSettings);
  // The club's tiers on sale and their dues, from the price catalogue.
  const catalog = useClubCatalog('Membership').data;
  const tierOptions = useMemo(() => tiersOnSale(catalog ?? []), [catalog]);
  const applicationsQuery = useApplications();
  const reads = useClubReads(applicationsQuery);
  const applications: Application[] = applicationsQuery.data ?? NONE;
  usePageLoading(reads.loading);
  const households = useHouseholds().data ?? NONE;

  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | ApplicationStatus>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState<FormState>(newForm('Silver'));
  const [saving, setSaving] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [review, setReview] = useState<Review | null>(null);
  const [reviewerNotes, setReviewerNotes] = useState('');
  const [approving, setApproving] = useState<ApproveState | null>(null);

  const stats = useMemo(() => computeApplicationStats(applications), [applications]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return applications.filter((a) => {
      if (statusFilter !== 'All' && a.status !== statusFilter) return false;
      if (!q) return true;
      return `${a.firstName} ${a.lastName} ${a.email} ${a.occupation ?? ''} ${a.referredByName ?? ''}`.toLowerCase().includes(q);
    });
  }, [applications, search, statusFilter]);

  const openCreate = () => {
    setForm(newForm(tierOptions[0]?.name ?? 'Silver'));
    setDialogOpen(true);
  };

  const submit = async () => {
    if (!form.firstName.trim() || !form.lastName.trim() || !form.email.trim()) {
      toast.error('First name, last name and email are required.');
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
      toast.success('Application added');
      setDialogOpen(false);
      await reads.reload();
    } catch (err) {
      toast.error(errorText(err, 'The application was not saved.'));
    } finally {
      setSaving(false);
    }
  };

  /** A change to one application, read again afterwards whatever happened: someone may have changed it first. */
  const act = async (a: Application, run: () => Promise<unknown>, failed: string): Promise<boolean> => {
    setBusyId(a.id);
    try {
      await run();
      return true;
    } catch (err) {
      toast.error(err instanceof ApiError && err.status === 409 ? errorText(err, 'Someone changed this application first. It has been reloaded.') : errorText(err, failed));
      return false;
    } finally {
      setBusyId(null);
      await reads.reload();
    }
  };

  const move = (a: Application, status: ApplicationStatus, notes?: string) =>
    act(a, async () => {
      await setApplicationStatus(a.id, status, notes, a.version);
      toast.success(status === 'Submitted' ? `${a.firstName}'s application is open again` : `Application ${STATUS_LABEL[status].toLowerCase()}`);
    }, 'The application was not moved.');

  const startReview = (app: Application, action: Review['action']) => {
    setReviewerNotes(app.reviewerNotes ?? '');
    setReview({ app, action });
  };

  const completeReview = async () => {
    if (!review) return;
    const ok = await move(review.app, review.action === 'reject' ? 'Rejected' : 'Waitlisted', reviewerNotes || undefined);
    if (ok) setReview(null);
  };

  const startApprove = (app: Application) =>
    setApproving({
      app,
      notes: app.reviewerNotes ?? '',
      tier: app.requestedTier,
      householdId: app.householdId ?? '',
      householdRole: (app.householdRole as HouseholdRole | undefined) ?? 'Spouse',
      rollUpBilling: false,
    });

  const approve = async (state: ApproveState) => {
    const { app } = state;
    const ok = await act(app, async () => {
      const result = await approveApplication(app.id, {
        reviewerNotes: state.notes.trim() || undefined,
        tier: state.tier !== app.requestedTier ? state.tier : undefined,
        householdId: state.householdId || undefined,
        householdRole: state.householdId ? state.householdRole : undefined,
        rollUpBilling: state.householdId && canMoney ? state.rollUpBilling : undefined,
        version: app.version,
      });
      const name = result.memberName || `${app.firstName} ${app.lastName}`;
      toast.success(
        result.memberCreated ? `${name} is now a member` : result.approved ? `${name} was already a member: the application is linked to them` : `${name} was approved already`,
        {
          description: [
            result.moneyPending ? 'Their dues and initiation fee are left for someone who handles the club’s money.' : undefined,
            ...result.notes,
          ].filter(Boolean).join(' ') || undefined,
        },
      );
    }, 'The application was not approved.');
    if (ok) setApproving(null);
  };

  /** Approved by someone who could not charge accounts: someone who can starts the dues and the fee. */
  const finishMoney = (a: Application) =>
    act(a, async () => {
      const result = await approveApplication(a.id, {});
      toast.success(result.moneyPending ? 'The dues and fee are still to be set up.' : `${result.memberName}'s dues and fee are set up`);
    }, 'The dues and fee were not set up.');

  const remove = (a: Application) => {
    if (!confirm(`Delete the application from ${a.firstName} ${a.lastName}?`)) return;
    void act(a, async () => {
      await deleteApplication(a.id);
      toast.success('Application deleted');
    }, 'The application was not deleted.');
  };

  // How this person likes applications shown: rows, cards or a table, saved for them.
  const view = useViewPreference('applications', { layouts: ['list', 'grid', 'table'] });

  /** Who the applicant is on the sales side, and the member they became. */
  const links = (a: Application) => (
    <span className="flex flex-wrap items-center gap-1.5 text-xs">
      {a.source === 'form' && (
        <span className="inline-flex items-center gap-1 rounded-full bg-sky-50 px-2 py-0.5 font-medium text-sky-700 ring-1 ring-sky-200">
          <Globe className="h-3 w-3" aria-hidden /> Public form
        </span>
      )}
      {a.leadId && (
        <Link to={`/leads/${a.leadId}`} className="rounded-full bg-slate-100 px-2 py-0.5 font-medium text-slate-700 hover:bg-slate-200">
          Lead: {a.leadName ?? 'open'}
        </Link>
      )}
      {a.contactId && !a.leadId && (
        <Link to={`/contacts/${a.contactId}`} className="rounded-full bg-slate-100 px-2 py-0.5 font-medium text-slate-700 hover:bg-slate-200">
          Contact: {a.contactName ?? 'open'}
        </Link>
      )}
      {a.householdName && (
        <span className="rounded-full bg-teal-50 px-2 py-0.5 font-medium text-teal-700 ring-1 ring-teal-200">
          Joins the {a.householdName} household{a.householdRole ? ` as ${a.householdRole.toLowerCase()}` : ''}
        </span>
      )}
      {a.marketingConsent === 'granted' && (
        <span className="rounded-full bg-emerald-50 px-2 py-0.5 font-medium text-emerald-700">Agreed to news by email</span>
      )}
      {a.memberId && (
        <Link to={`/members/${a.memberId}`} className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 font-medium text-emerald-700 ring-1 ring-emerald-200 hover:bg-emerald-100">
          <UserCheck className="h-3 w-3" aria-hidden /> Member: {a.memberName ?? `${a.firstName} ${a.lastName}`}
        </Link>
      )}
      {a.moneyPending && (
        <span className="rounded-full bg-amber-50 px-2 py-0.5 font-medium text-amber-700 ring-1 ring-amber-200">Dues and fee not set up yet</span>
      )}
    </span>
  );

  /**
   * Approve, waitlist, reject, reopen, move to review, delete: the same wherever the
   * application is shown. Stacked down the side of a row, wrapping on a card, and in
   * one line in a table.
   */
  const applicationActions = (a: Application, layout: 'stacked' | 'wrap' | 'line' = 'wrap') => (
    <div
      className={
        layout === 'stacked'
          ? 'flex flex-row lg:flex-col gap-2 lg:items-end shrink-0'
          : layout === 'line'
            ? 'flex items-center justify-end gap-1.5'
            : 'flex flex-wrap items-center justify-end gap-1.5'
      }
    >
      {/* Reviewing applications is for whoever manages the club's members. */}
      {isOpen(a.status) && (
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <Button
            size="sm"
            onClick={() => startApprove(a)}
            disabled={busyId === a.id}
            className="bg-emerald-600 hover:bg-emerald-700 text-white"
            aria-label={`Approve ${a.firstName} ${a.lastName}`}
          >
            <Check className="w-3.5 h-3.5 mr-1" /> Approve
          </Button>
          <div className="flex gap-1.5">
            {a.status !== 'Waitlisted' && (
              <Button size="sm" variant="outline" onClick={() => startReview(a, 'waitlist')} disabled={busyId === a.id}>
                Waitlist
              </Button>
            )}
            <Button
              size="sm"
              variant="outline"
              onClick={() => startReview(a, 'reject')}
              disabled={busyId === a.id}
              className="text-rose-600 hover:bg-rose-50"
              aria-label={`Reject ${a.firstName} ${a.lastName}`}
            >
              <X className="w-3.5 h-3.5" />
            </Button>
          </div>
        </WriteOnly>
      )}
      {a.status === 'Submitted' && (
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <button
            type="button"
            onClick={() => void move(a, 'UnderReview')}
            className="text-xs text-blue-600 hover:underline inline-flex items-center gap-0.5"
          >
            Move to review <ChevronRight className="w-3 h-3" />
          </button>
        </WriteOnly>
      )}
      {(a.status === 'Waitlisted' || a.status === 'Rejected') && (
        <WriteOnly permission={PERMISSION.ClubMembers}>
          <button
            type="button"
            onClick={() => void move(a, 'Submitted')}
            disabled={busyId === a.id}
            className="text-xs text-blue-600 hover:underline inline-flex items-center gap-1 disabled:opacity-50"
            aria-label={`Reopen ${a.firstName} ${a.lastName}'s application`}
          >
            <RotateCcw className="w-3 h-3" /> Reopen
          </button>
        </WriteOnly>
      )}
      {a.status === 'Approved' && a.moneyPending && canMoney && (
        <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.ManageMoney]} all>
          <Button size="sm" variant="outline" onClick={() => void finishMoney(a)} disabled={busyId === a.id}>
            Set up dues and fee
          </Button>
        </WriteOnly>
      )}
      {(a.status === 'Rejected' || a.status === 'Approved' || a.status === 'Waitlisted') && (
        <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
          <button
            type="button"
            onClick={() => remove(a)}
            disabled={busyId === a.id}
            className="text-xs text-slate-500 hover:text-rose-600 disabled:opacity-50"
          >
            Delete record
          </button>
        </WriteOnly>
      )}
    </div>
  );

  const applicationColumns: RecordColumn<Application>[] = [
    {
      id: 'name',
      label: 'Applicant',
      required: true,
      cell: (a) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={`${a.firstName} ${a.lastName}`} size="sm" />
          <span className="truncate">{a.firstName} {a.lastName}</span>
        </span>
      ),
    },
    { id: 'status', label: 'Status', cell: (a) => <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[a.status]}`}>{STATUS_LABEL[a.status]}</span> },
    { id: 'tier', label: 'Requested tier', cell: (a) => a.requestedTier || '—' },
    { id: 'email', label: 'Email', cell: (a) => a.email },
    { id: 'submitted', label: 'Submitted', cell: (a) => formatDate(a.submittedAtUtc) },
    { id: 'links', label: 'Lead and member', byDefault: false, cell: (a) => links(a) },
    { id: 'phone', label: 'Phone', byDefault: false, cell: (a) => a.phone || '—' },
    { id: 'occupation', label: 'Occupation', byDefault: false, cell: (a) => a.occupation || '—' },
    { id: 'referredBy', label: 'Referred by', byDefault: false, cell: (a) => a.referredByName || '—' },
  ];

  const tierChoices = tierOptions.length > 0 ? tierOptions : MEMBER_TIERS.map((name) => ({ name, price: undefined, cadence: undefined }));

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main
          id={MAIN_CONTENT_ID}
          className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]"
          tabIndex={-1}
        >
          <PageHero
            icon={ClipboardList}
            tone="blue"
            title="Applications"
            subtitle="Review prospective members. Approving admits them at their tier, starts their dues and charges the initiation fee, in one step."
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={openCreate} className={heroPrimary('blue')}>
                  <Plus className="h-4 w-4" />
                  New application
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Awaiting decision', value: stats.submitted + stats.underReview, icon: Clock, tone: 'amber', hint: `${stats.submitted} submitted · ${stats.underReview} reviewing` },
              { label: 'Approved · 30d', value: stats.approvedThisMonth, icon: UserCheck, tone: 'emerald', hint: 'new members admitted' },
              { label: 'Waitlisted', value: stats.waitlisted, icon: AlertTriangle, tone: 'violet', hint: 'can be approved or reopened' },
              { label: 'Rejected · 30d', value: stats.rejectedThisMonth, icon: X, tone: 'rose', hint: 'closed in the last 30 days' },
            ]}
          />

          {canSettings && !servesDemoData() && <PublicFormPanel />}

          {/* Filters */}
          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search applicants..." className="pl-9 h-11 rounded-xl" />
            </div>
            <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as 'All' | ApplicationStatus)}>
              <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-48">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All statuses</SelectItem>
                {APPLICATION_STATUSES.map((s) => (
                  <SelectItem key={s} value={s}>{STATUS_LABEL[s]}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(applicationColumns)} className="md:ml-auto" />
          </div>

          <ClubReadState reads={reads} what="Applications">
            {reads.loading ? (
              <div className="flex items-center justify-center py-20 text-slate-500">
                <Loader2 className="h-6 w-6 animate-spin" />
              </div>
            ) : filtered.length === 0 ? (
              <EmptyState
                icon={ClipboardList}
                title={applications.length === 0 ? 'No applications yet' : 'No applications match these filters'}
                description={
                  applications.length === 0
                    ? 'Applications typed in here, started from a lead or sent on the club’s public form land here for review.'
                    : 'Try clearing filters or adjusting your search.'
                }
                variant="orange"
              />
            ) : (
              <ViewTransition layout={view.layout}>
                {view.layout === 'table' ? (
                  <RecordTable
                    label="Applications"
                    items={filtered}
                    view={view}
                    getKey={(a) => a.id}
                    columns={applicationColumns}
                    actions={(a) => applicationActions(a, 'line')}
                  />
                ) : view.layout === 'grid' ? (
                  <div className={`grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-4')}`}>
                    {filtered.map((a, index) => (
                      <StaggerItem
                        key={a.id}
                        index={index}
                        className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm hover:border-blue-200 ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                      >
                        <div className="flex items-start gap-3">
                          <InitialsAvatar name={`${a.firstName} ${a.lastName}`} />
                          <div className="min-w-0 flex-1">
                            <h3 className="font-semibold text-slate-900 truncate">{a.firstName} {a.lastName}</h3>
                            <p className="text-xs text-slate-500 truncate">{a.email}</p>
                          </div>
                          <span className={`shrink-0 text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[a.status]}`}>{STATUS_LABEL[a.status]}</span>
                        </div>
                        <div className="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                          <span className="rounded-full bg-slate-100 px-2 py-0.5 font-medium text-slate-600">Requested: {a.requestedTier || NO_TIER}</span>
                          <span className="inline-flex items-center gap-1"><Clock className="w-3 h-3" /> {formatDate(a.submittedAtUtc)}</span>
                          {a.occupation && <span className="inline-flex items-center gap-1 truncate"><Briefcase className="w-3 h-3" /> {a.occupation}</span>}
                        </div>
                        <div className="mt-2">{links(a)}</div>
                        {a.reasonForJoining && view.density !== 'compact' && (
                          <p className="mt-2 text-sm text-slate-600 line-clamp-2 italic">"{a.reasonForJoining}"</p>
                        )}
                        <div className="mt-auto pt-3">{applicationActions(a)}</div>
                      </StaggerItem>
                    ))}
                  </div>
                ) : (
                  <div className={view.density === 'compact' ? 'space-y-2' : 'space-y-3'}>
                    {filtered.map((a, index) => (
                      <StaggerItem
                        key={a.id}
                        index={index}
                        className={`rounded-2xl border border-slate-200 bg-white ${view.density === 'compact' ? 'p-3' : 'p-5'} shadow-sm hover:border-blue-200 ${liftOnHover}`}
                      >
                        <div className="flex flex-col lg:flex-row gap-4">
                          <div className="flex items-start gap-4 min-w-0 flex-1">
                            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-blue-100 to-cyan-100 flex items-center justify-center text-blue-700 font-semibold shrink-0">
                              {(a.firstName[0] ?? '?') + (a.lastName[0] ?? '')}
                            </div>
                            <div className="min-w-0 flex-1">
                              <div className="flex flex-wrap items-center gap-2">
                                <h3 className="font-semibold text-slate-900 truncate">{a.firstName} {a.lastName}</h3>
                                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[a.status]}`}>{STATUS_LABEL[a.status]}</span>
                                <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">Requested: {a.requestedTier || NO_TIER}</span>
                              </div>
                              <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-slate-500">
                                <span className="inline-flex items-center gap-1 truncate"><Mail className="w-3.5 h-3.5" /> {a.email}</span>
                                {a.phone && <span className="inline-flex items-center gap-1"><Phone className="w-3.5 h-3.5" /> {a.phone}</span>}
                                {a.occupation && <span className="inline-flex items-center gap-1 truncate"><Briefcase className="w-3.5 h-3.5" /> {a.occupation}</span>}
                                <span className="inline-flex items-center gap-1"><Clock className="w-3.5 h-3.5" /> Submitted {formatDate(a.submittedAtUtc)}</span>
                              </div>
                              <div className="mt-2">{links(a)}</div>
                              {a.referredByName && (
                                <p className="mt-2 text-xs text-slate-500">Referred by <span className="font-medium text-slate-700">{a.referredByName}</span></p>
                              )}
                              {a.reasonForJoining && <p className="mt-2 text-sm text-slate-600 line-clamp-2 italic">"{a.reasonForJoining}"</p>}
                              {a.reviewerNotes && (
                                <p className="mt-2 text-xs text-slate-500 bg-slate-50 rounded p-2"><span className="font-medium">Committee:</span> {a.reviewerNotes}</p>
                              )}
                            </div>
                          </div>
                          {applicationActions(a, 'stacked')}
                        </div>
                      </StaggerItem>
                    ))}
                  </div>
                )}
              </ViewTransition>
            )}
          </ClubReadState>
        </main>
      </PageTransition>

      {/* Create application */}
      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>New application</DialogTitle>
            <DialogDescription>Add a prospective member for committee review.</DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div>
              <Label>First name</Label>
              <Input aria-label="First name" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
            </div>
            <div>
              <Label>Last name</Label>
              <Input aria-label="Last name" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
            </div>
            <div>
              <Label>Email</Label>
              <Input aria-label="Email" type="email" value={form.email} onChange={(e) => setForm({ ...form, email: e.target.value })} />
            </div>
            <div>
              <Label>Phone</Label>
              <Input aria-label="Phone" value={form.phone} onChange={(e) => setForm({ ...form, phone: e.target.value })} />
            </div>
            <div>
              <Label>Requested tier</Label>
              <Select value={form.requestedTier} onValueChange={(v) => setForm({ ...form, requestedTier: v as MemberTier })}>
                <SelectTrigger aria-label="Requested tier">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {tierChoices.map((t) => (
                    <SelectItem key={t.name} value={t.name}>
                      {t.price !== undefined && t.cadence ? `${t.name} — ${formatCurrency(t.price)}/${CADENCE_LABEL[t.cadence]}` : t.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label>Referred by</Label>
              <Input aria-label="Referred by" value={form.referredByName} onChange={(e) => setForm({ ...form, referredByName: e.target.value })} placeholder="Existing member name" />
            </div>
            <div className="md:col-span-2">
              <Label>Occupation</Label>
              <Input aria-label="Occupation" value={form.occupation} onChange={(e) => setForm({ ...form, occupation: e.target.value })} />
            </div>
            <div className="md:col-span-2">
              <Label>Reason for joining</Label>
              <Textarea aria-label="Reason for joining" value={form.reasonForJoining} onChange={(e) => setForm({ ...form, reasonForJoining: e.target.value })} rows={3} />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={saving}>Cancel</Button>
            <Button onClick={() => void submit()} disabled={saving}>
              {saving ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Saving</> : 'Add application'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Waitlist or reject */}
      <Dialog open={review !== null} onOpenChange={(o) => !o && setReview(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>
              {review?.action === 'reject' ? `Reject ${review.app.firstName}'s application?` : `Waitlist ${review?.app.firstName}?`}
            </DialogTitle>
            <DialogDescription>
              {review?.action === 'reject'
                ? 'The application is marked rejected. It can be reopened later, and approved once it is.'
                : 'The application waits on the list. It can be approved or reopened when there is room.'}
            </DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <Label>Committee notes (optional)</Label>
            <Textarea aria-label="Committee notes (optional)" value={reviewerNotes} onChange={(e) => setReviewerNotes(e.target.value)} rows={3} placeholder="Context for the file..." />
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setReview(null)} disabled={busyId === review?.app.id}>Cancel</Button>
            <Button
              onClick={() => void completeReview()}
              disabled={busyId === review?.app.id}
              className={review?.action === 'reject' ? 'bg-rose-600 hover:bg-rose-700' : ''}
            >
              {busyId === review?.app.id ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Working</> : review?.action === 'reject' ? 'Reject application' : 'Move to waitlist'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Approve */}
      <Dialog open={approving !== null} onOpenChange={(o) => !o && setApproving(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Approve {approving?.app.firstName} {approving?.app.lastName}?</DialogTitle>
            <DialogDescription>
              {canMoney
                ? 'They become an active member at the tier below: their dues start, the tier’s initiation fee goes on their account, and a welcome email is sent.'
                : 'They become an active member at the tier below and get a welcome email. Their dues and initiation fee are left for someone who handles the club’s money.'}
            </DialogDescription>
          </DialogHeader>
          {approving && (
            <div className="space-y-4 py-2">
              <div>
                <Label>Tier</Label>
                {/* An applicant who chose no tier ("Not sure yet" on the public form) has none
                    to offer here: an empty choice breaks the select, so the reviewer picks one. */}
                <Select value={approving.tier} onValueChange={(v) => setApproving({ ...approving, tier: v })}>
                  <SelectTrigger aria-label="Tier to admit them at">
                    <SelectValue placeholder="Pick the tier to admit them at" />
                  </SelectTrigger>
                  <SelectContent>
                    {[...new Set([approving.app.requestedTier, ...tierChoices.map((t) => t.name)])].filter(Boolean).map((name) => (
                      <SelectItem key={name} value={name}>{name}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <div>
                  <Label>Household (optional)</Label>
                  <Select value={approving.householdId || 'none'} onValueChange={(v) => setApproving({ ...approving, householdId: v === 'none' ? '' : v })}>
                    <SelectTrigger aria-label="Household to join">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="none">None</SelectItem>
                      {households.map((h) => (
                        <SelectItem key={h.id} value={h.id}>The {h.name} household</SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
                {approving.householdId && (
                  <div>
                    <Label>As</Label>
                    <Select value={approving.householdRole} onValueChange={(v) => setApproving({ ...approving, householdRole: v as HouseholdRole })}>
                      <SelectTrigger aria-label="Their role in the household">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {MEMBER_ROLES.map((r) => (
                          <SelectItem key={r} value={r}>{r}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </div>
                )}
              </div>
              {approving.householdId && canMoney && (
                <div className="flex items-center justify-between gap-3 rounded-lg bg-slate-50 px-3 py-2">
                  <Label htmlFor="approve-rollup" className="text-sm font-normal text-slate-700">Bill their charges, dues and fee to the household&apos;s primary</Label>
                  <Switch id="approve-rollup" checked={approving.rollUpBilling} onCheckedChange={(on) => setApproving({ ...approving, rollUpBilling: on })} />
                </div>
              )}
              <div>
                <Label>Committee notes (optional)</Label>
                <Textarea aria-label="Committee notes" value={approving.notes} onChange={(e) => setApproving({ ...approving, notes: e.target.value })} rows={3} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setApproving(null)} disabled={busyId === approving?.app.id}>Cancel</Button>
            <Button onClick={() => approving && void approve(approving)} disabled={busyId === approving?.app.id || !approving?.tier} className="bg-emerald-600 hover:bg-emerald-700">
              {busyId === approving?.app.id ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Approving</> : 'Approve and admit'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

/**
 * The club's public application form, for whoever manages the workspace's settings: its
 * link (one per workspace), whether it takes applications, a new link when the old one
 * leaked, and what it says.
 */
function PublicFormPanel() {
  const formQuery = useApplicationForm();
  const form: ApplicationForm | null | undefined = formQuery.data;
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState<{ intro: string; successMessage: string; consentText: string } | null>(null);
  const url = form ? `${window.location.origin}${form.publicPath}` : '';

  const run = async (work: () => Promise<unknown>, done: string) => {
    setBusy(true);
    try {
      await work();
      toast.success(done);
      await formQuery.refetch();
      return true;
    } catch (err) {
      toast.error(errorText(err, 'The form was not changed.'));
      return false;
    } finally {
      setBusy(false);
    }
  };

  if (formQuery.isPending) return null;
  if (formQuery.isError) {
    return (
      <p className="mb-6 rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        The public application form&apos;s settings could not be loaded.{' '}
        <button type="button" className="font-medium underline" onClick={() => void formQuery.refetch()}>Try again</button>
      </p>
    );
  }

  return (
    <section aria-labelledby="public-form-heading" className="mb-6 rounded-2xl border border-sky-200/70 bg-gradient-to-br from-sky-50/70 via-white to-white p-4 shadow-sm">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="min-w-0">
          <h2 id="public-form-heading" className="flex items-center gap-2 text-sm font-semibold text-slate-900">
            <Globe className="h-4 w-4 text-sky-600" aria-hidden /> Public application form
          </h2>
          <p className="mt-0.5 text-xs text-slate-600">
            {form
              ? form.isActive ? 'Anyone with the link can apply, without signing in. Each application lands here as Submitted.' : 'Closed: the link says the form is not available.'
              : 'Open a form to let people apply from the club’s website. Applications land here, linked to their lead.'}
          </p>
        </div>
        {form ? (
          <div className="flex flex-wrap items-center gap-2">
            <Label htmlFor="public-form-active" className="text-xs text-slate-600">Taking applications</Label>
            <Switch id="public-form-active" checked={form.isActive} disabled={busy}
              onCheckedChange={(on) => void run(() => saveApplicationForm({ isActive: on }), on ? 'The form is open' : 'The form is closed')} />
          </div>
        ) : (
          <Button size="sm" disabled={busy} onClick={() => void run(() => saveApplicationForm({ isActive: true }), 'The form is open')}>
            Open the form
          </Button>
        )}
      </div>
      {form && (
        <div className="mt-3 flex flex-wrap items-center gap-2">
          <code className="min-w-0 max-w-full flex-1 truncate rounded-lg bg-white px-3 py-2 text-xs text-slate-700 ring-1 ring-slate-200">{url}</code>
          <Button size="sm" variant="outline" disabled={busy}
            onClick={() => { void navigator.clipboard?.writeText(url).then(() => toast.success('Link copied')).catch(() => toast.error('The link could not be copied.')); }}>
            <Copy className="h-3.5 w-3.5 mr-1" /> Copy link
          </Button>
          <Button size="sm" variant="outline" disabled={busy}
            onClick={() => setEditing({ intro: form.intro ?? '', successMessage: form.successMessage ?? '', consentText: form.consentText })}>
            Edit wording
          </Button>
          <Button size="sm" variant="outline" disabled={busy}
            onClick={() => { if (confirm('Give the form a new link? The old link stops working at once.')) void run(() => rotateApplicationForm(), 'The form has a new link'); }}>
            <RotateCcw className="h-3.5 w-3.5 mr-1" /> New link
          </Button>
        </div>
      )}
      <Dialog open={editing !== null} onOpenChange={(o) => !o && setEditing(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>The form&apos;s wording</DialogTitle>
            <DialogDescription>What applicants read. The consent sentence is kept with each answer they give.</DialogDescription>
          </DialogHeader>
          {editing && (
            <div className="space-y-3 py-2">
              <div>
                <Label htmlFor="form-intro">Introduction</Label>
                <Textarea id="form-intro" rows={3} value={editing.intro} onChange={(e) => setEditing({ ...editing, intro: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="form-consent">Consent to news by email</Label>
                <Textarea id="form-consent" rows={2} value={editing.consentText} onChange={(e) => setEditing({ ...editing, consentText: e.target.value })} />
              </div>
              <div>
                <Label htmlFor="form-thanks">Thank-you message</Label>
                <Input id="form-thanks" value={editing.successMessage} onChange={(e) => setEditing({ ...editing, successMessage: e.target.value })} />
              </div>
            </div>
          )}
          <DialogFooter>
            <Button variant="outline" onClick={() => setEditing(null)} disabled={busy}>Cancel</Button>
            <Button disabled={busy} onClick={() => {
              if (!editing) return;
              void run(() => saveApplicationForm(editing), 'The form’s wording is saved').then((ok) => { if (ok) setEditing(null); });
            }}>
              Save
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </section>
  );
}

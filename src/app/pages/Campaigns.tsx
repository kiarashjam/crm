import { useMemo, useState } from 'react';
import {
  Megaphone,
  Plus,
  Send,
  Trash2,
  Search,
  Users,
  Mail,
  Clock,
  CheckCircle2,
  Loader2,
  Pencil,
  FlaskConical,
  ListChecks,
  ShieldCheck,
  OctagonX,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import EmptyState from '@/app/components/EmptyState';
import { LoadFailed } from '@/app/components/LoadFailed';
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
  createCampaign,
  deleteCampaign,
  sendCampaign,
  sendCampaignTest,
  stopCampaign,
  updateCampaign,
  computeCampaignStats,
  campaignStatusLabel,
  isEditable,
  type Campaign,
  type CampaignPurpose,
  type CampaignAudience,
  type CampaignTotals,
  type SegmentFilter,
} from '@/app/api/campaigns';
import {
  MEMBER_TIERS,
  MEMBER_STATUSES,
  type MemberTier,
  type MemberStatus,
} from '@/app/api/members';
import { CATEGORIES, commStatusLabel, type CommCategory, type CommStatus } from '@/app/api/communications';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { useCampaignAudience, useCampaignRecipients, useCampaigns } from '@/app/hooks/queries/club';

const STATUS_BADGE: Record<Campaign['status'], string> = {
  Draft: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Scheduled: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Sending: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Sent: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Stopped: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Logged: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

const PURPOSE_LABEL: Record<CampaignPurpose, string> = { marketing: 'Marketing', service: 'Service' };

const RECIPIENT_BADGE: Record<CommStatus, string> = {
  queued: 'bg-slate-100 text-slate-600',
  sent: 'bg-blue-50 text-blue-700',
  delivered: 'bg-cyan-50 text-cyan-700',
  opened: 'bg-emerald-50 text-emerald-700',
  bounced: 'bg-rose-50 text-rose-700',
  suppressed: 'bg-amber-50 text-amber-800',
  skipped: 'bg-amber-50 text-amber-800',
  failed: 'bg-rose-50 text-rose-700',
  cancelled: 'bg-slate-100 text-slate-600',
  logged: 'bg-slate-100 text-slate-500',
};

function formatDateTime(iso?: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleString(undefined, { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
  } catch {
    return iso;
  }
}

/** A local date-time for the schedule field, from a UTC instant. */
function toLocalInput(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** One line of figures for a campaign that has gone out. */
function totalsLine(t: CampaignTotals): string {
  const notSent = t.suppressed + t.skipped + t.failed + (t.cancelled ?? 0);
  const parts = [`${t.recipients} member${t.recipients === 1 ? '' : 's'}`, `${t.sent} sent`, `${t.delivered} delivered`, `${t.opened} opened`];
  if (t.bounced) parts.push(`${t.bounced} bounced`);
  if (t.queued) parts.push(`${t.queued} queued`);
  if (notSent) parts.push(`${notSent} not sent`);
  return parts.join(' · ');
}

interface FormState {
  id: string | null;
  version?: string;
  name: string;
  category: CommCategory;
  purpose: CampaignPurpose;
  subject: string;
  body: string;
  tiers: MemberTier[];
  statuses: MemberStatus[];
  sendAt: string;
}

const EMPTY_FORM: FormState = {
  id: null, name: '', category: 'Announcement', purpose: 'marketing', subject: '', body: '', tiers: [], statuses: ['Active'], sendAt: '',
};

export default function Campaigns() {
  const campaignsQuery = useCampaigns();
  const campaigns = useMemo(() => campaignsQuery.data ?? [], [campaignsQuery.data]);
  const loading = campaignsQuery.isPending;
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [form, setForm] = useState<FormState | null>(null);
  const [busy, setBusy] = useState(false);
  const [confirming, setConfirming] = useState<Campaign | null>(null);
  const [testingId, setTestingId] = useState<string | null>(null);
  const [recipientsOf, setRecipientsOf] = useState<Campaign | null>(null);
  const [stopping, setStopping] = useState<Campaign | null>(null);

  const stats = useMemo(() => computeCampaignStats(campaigns), [campaigns]);
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return campaigns.filter((c) => (!q ? true : `${c.name} ${c.subject}`.toLowerCase().includes(q)));
  }, [campaigns, search]);

  const segmentOf = (f: FormState): SegmentFilter => ({
    tiers: f.tiers.length ? f.tiers : undefined,
    statuses: f.statuses.length ? f.statuses : undefined,
  });

  const openCreate = () => setForm({ ...EMPTY_FORM });
  const openEdit = (c: Campaign) => setForm({
    id: c.id, version: c.version, name: c.name, category: c.category, purpose: c.purpose, subject: c.subject, body: c.body,
    tiers: c.segment.tiers ?? [], statuses: c.segment.statuses ?? [], sendAt: toLocalInput(c.scheduledAtUtc),
  });

  const save = async () => {
    if (!form) return;
    if (!form.name.trim() || !form.subject.trim() || !form.body.trim()) {
      toast.error('A campaign needs a name, a subject and a message.');
      return;
    }
    const scheduledAtUtc = form.sendAt ? new Date(form.sendAt).toISOString() : null;
    if (scheduledAtUtc && Date.parse(scheduledAtUtc) <= Date.now()) {
      toast.error('Pick a time in the future to send it, or leave it empty and send it when you are ready.');
      return;
    }
    setBusy(true);
    try {
      const input = {
        name: form.name.trim(), category: form.category, purpose: form.purpose, subject: form.subject.trim(), body: form.body.trim(),
        segment: segmentOf(form), scheduledAtUtc,
      };
      if (form.id) await updateCampaign(form.id, input, form.version);
      else await createCampaign(input);
      toast.success(scheduledAtUtc ? `Scheduled for ${formatDateTime(scheduledAtUtc)}` : 'Saved as a draft');
      setForm(null);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The campaign could not be saved.');
    } finally {
      setBusy(false);
    }
  };

  const sendTest = async (c: Campaign) => {
    setTestingId(c.id);
    try {
      const result = await sendCampaignTest(c.id);
      // "queued": the mail server did not take it at once, and the outbox tries again shortly.
      toast.success(result.status === 'sent'
        ? `Test sent to ${result.to}. Members have not received it.`
        : `Test queued for ${result.to}: the mail server did not take it yet, and it is tried again shortly. Members have not received it.`);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The test could not be sent.');
    } finally {
      setTestingId(null);
    }
  };

  const remove = async (c: Campaign) => {
    if (!confirm(`Delete campaign "${c.name}"? Nothing has been sent from it.`)) return;
    try {
      await deleteCampaign(c.id);
      toast.success('Deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The campaign could not be deleted.');
    }
  };

  const view = useViewPreference('campaigns', { layouts: ['list', 'table'] });

  /** What can be done with a campaign: the same on a row and a table line. */
  const campaignActions = (c: Campaign) => {
    const sendable = isEditable(c.status) && c.channel === 'Email';
    return (
      <div className="flex flex-wrap items-center gap-1.5 shrink-0">
        {sendable && (
          <WriteOnly permission={PERMISSION.ClubMembers}>
            <Button size="sm" onClick={() => setConfirming(c)}>
              <Send className="mr-1 h-3.5 w-3.5" /> Send now
            </Button>
            <Button size="sm" variant="outline" onClick={() => void sendTest(c)} disabled={testingId === c.id}>
              {testingId === c.id ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <><FlaskConical className="mr-1 h-3.5 w-3.5" /> Send a test to me</>}
            </Button>
            <Button size="sm" variant="ghost" onClick={() => openEdit(c)} aria-label={`Edit ${c.name}`}>
              <Pencil className="h-3.5 w-3.5" />
            </Button>
          </WriteOnly>
        )}
        {c.status === 'Sending' && (
          <WriteOnly permission={PERMISSION.ClubMembers}>
            <Button size="sm" variant="outline" className="border-rose-200 text-rose-700 hover:bg-rose-50" onClick={() => setStopping(c)}>
              <OctagonX className="mr-1 h-3.5 w-3.5" /> Stop sending
            </Button>
          </WriteOnly>
        )}
        {(c.status === 'Sent' || c.status === 'Sending' || c.status === 'Stopped') && (
          <Button size="sm" variant="outline" onClick={() => setRecipientsOf(c)}>
            <ListChecks className="mr-1 h-3.5 w-3.5" /> Recipients
          </Button>
        )}
        {isEditable(c.status) && (
          <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
            <button
              type="button"
              onClick={() => void remove(c)}
              className="rounded p-2 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
              aria-label={`Delete ${c.name}`}
            >
              <Trash2 className="h-4 w-4" />
            </button>
          </WriteOnly>
        )}
      </div>
    );
  };

  /** Where it stands, in words: when it goes or went, and what came of it. */
  const progress = (c: Campaign): string => {
    if (c.status === 'Logged') {
      return `Logged ${formatDateTime(c.sentAtUtc)} for ${c.recipientCount} members before CadenceE could send: nobody received it`;
    }
    if (c.channel !== 'Email') return `${c.channel} is not available: CadenceE has no ${c.channel === 'Push' ? 'push' : 'SMS'} provider.`;
    switch (c.status) {
      case 'Scheduled': return `Sends ${formatDateTime(c.scheduledAtUtc)}`;
      case 'Sending': return c.totals && c.totals.recipients > 0 ? `Sending in the background · ${totalsLine(c.totals)}` : 'Sending in the background: the members are being worked through';
      case 'Sent': return c.totals ? `Sent ${formatDateTime(c.sentAtUtc)} · ${totalsLine(c.totals)}` : `Sent ${formatDateTime(c.sentAtUtc)}`;
      case 'Stopped': return c.totals ? `Stopped ${formatDateTime(c.sentAtUtc)} · ${totalsLine(c.totals)}` : `Stopped ${formatDateTime(c.sentAtUtc)}`;
      default: return 'Draft: not sent';
    }
  };

  const columns: RecordColumn<Campaign>[] = [
    { id: 'name', label: 'Campaign', required: true, cell: (c) => <span className="truncate">{c.name}</span> },
    { id: 'status', label: 'Status', cell: (c) => <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>{campaignStatusLabel(c.status)}</span> },
    { id: 'purpose', label: 'For', cell: (c) => PURPOSE_LABEL[c.purpose] },
    { id: 'subject', label: 'Subject', cell: (c) => <span className="line-clamp-1">{c.subject || '—'}</span> },
    { id: 'progress', label: 'Progress', cell: (c) => <span className="text-xs text-slate-600">{progress(c)}</span> },
    { id: 'segment', label: 'Segment', byDefault: false, cell: (c) => [c.segment.tiers?.join(', '), c.segment.statuses?.join(', ')].filter(Boolean).join(' · ') || 'Everyone' },
  ];

  return (
    <div className="min-h-screen flex flex-col bg-gradient-subtle">
      <AppHeader />
      <PageTransition>
        <main id={MAIN_CONTENT_ID} className="flex-1 w-full px-[var(--page-padding)] py-[var(--main-block-padding-y)]" tabIndex={-1}>
          <PageHero
            icon={Megaphone}
            tone="fuchsia"
            title="Campaigns"
            subtitle="Email a segment of members, now or at a time you set. Each member's message shows whether it arrived."
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={openCreate} className={heroPrimary('fuchsia')}>
                  <Plus className="h-4 w-4" /> New campaign
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Drafts', value: stats.drafts, icon: Megaphone, tone: 'fuchsia' },
              { label: 'Scheduled', value: stats.scheduled, icon: Clock, tone: 'blue', hint: 'send at their time' },
              { label: 'Sent', value: stats.sent, icon: CheckCircle2, tone: 'emerald', hint: 'campaigns' },
              { label: 'Emails sent', value: stats.emailsSent, icon: Mail, tone: 'amber', hint: 'all campaigns' },
            ]}
          />

          <div role="note" className="mb-6 flex items-start gap-3 rounded-xl border border-fuchsia-200 bg-fuchsia-50 px-4 py-3 text-sm text-fuchsia-900">
            <ShieldCheck className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
            <p>
              Marketing goes only to members who agreed to it by email: record consent on each member&apos;s page. Every
              marketing email carries an unsubscribe link. Service campaigns (the club closed on Monday) go to everyone they
              are for. Campaigns go by email: SMS and push are not available.
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3 mb-6">
            <div className="relative flex-1 min-w-[12rem] max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input value={search} onChange={(e) => setSearch(e.target.value)} placeholder="Search campaigns..." className="pl-9 h-11 rounded-xl" />
            </div>
            <ViewSwitcher view={view} columns={columnChoices(columns)} className="ml-auto" />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : campaignsQuery.isError && !campaignsQuery.data ? (
            <LoadFailed what="Campaigns" error={campaignsQuery.error} onRetry={() => void campaignsQuery.refetch()} retrying={campaignsQuery.isFetching} />
          ) : (
            <>
              {campaignsQuery.isError && (
                <LoadFailed stale what="Campaigns" error={campaignsQuery.error} onRetry={() => void campaignsQuery.refetch()} retrying={campaignsQuery.isFetching} className="mb-4" />
              )}
              {filtered.length === 0 ? (
                <EmptyState
                  icon={Megaphone}
                  title={campaigns.length === 0 ? 'No campaigns yet' : 'No campaigns match'}
                  description={campaigns.length === 0 ? 'Write a campaign for a segment of members.' : 'Try clearing the search.'}
                  actionLabel={campaigns.length === 0 ? 'New campaign' : undefined}
                  onAction={campaigns.length === 0 ? openCreate : undefined}
                  actionPermission={PERMISSION.ClubMembers}
                  variant="orange"
                />
              ) : (
                <ViewTransition layout={view.layout}>
                  {view.layout === 'table' ? (
                    <RecordTable label="Campaigns" items={filtered} view={view} getKey={(c) => c.id} columns={columns} actions={campaignActions} />
                  ) : (
                    <div className={view.density === 'compact' ? 'space-y-2' : 'space-y-3'}>
                      {filtered.map((c, index) => (
                        <StaggerItem
                          as="article"
                          key={c.id}
                          index={index}
                          className={`rounded-2xl border border-slate-200 bg-white ${view.density === 'compact' ? 'p-3' : 'p-5'} shadow-sm hover:border-fuchsia-200 ${liftOnHover}`}
                        >
                          <div className="flex flex-col gap-3 md:flex-row md:items-start">
                            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-fuchsia-100 to-purple-100 flex items-center justify-center text-fuchsia-700 shrink-0">
                              <Mail className="w-5 h-5" />
                            </div>
                            <div className="flex-1 min-w-0">
                              <div className="flex flex-wrap items-center gap-2 mb-1">
                                <h3 className="font-semibold text-slate-900 truncate">{c.name}</h3>
                                <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>{campaignStatusLabel(c.status)}</span>
                                <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">{PURPOSE_LABEL[c.purpose]} · {c.category}</span>
                              </div>
                              <p className="text-sm font-medium text-slate-700">{c.subject}</p>
                              {view.density !== 'compact' && <p className="text-sm text-slate-500 mt-0.5 line-clamp-2">{c.body}</p>}
                              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 text-xs text-slate-500">
                                {c.segment.tiers && c.segment.tiers.length > 0 && <span>Tiers: {c.segment.tiers.join(', ')}</span>}
                                {c.segment.statuses && c.segment.statuses.length > 0 && <span>Statuses: {c.segment.statuses.join(', ')}</span>}
                                <span>{progress(c)}</span>
                              </div>
                            </div>
                            {campaignActions(c)}
                          </div>
                        </StaggerItem>
                      ))}
                    </div>
                  )}
                </ViewTransition>
              )}
            </>
          )}
        </main>
      </PageTransition>

      {form && (
        <CampaignDialog
          form={form}
          onChange={setForm}
          onCancel={() => setForm(null)}
          onSave={() => void save()}
          busy={busy}
          segment={segmentOf(form)}
        />
      )}
      {confirming && <SendDialog campaign={confirming} onClose={() => setConfirming(null)} />}
      {stopping && <StopDialog campaign={stopping} onClose={() => setStopping(null)} />}
      {recipientsOf && <RecipientsDialog campaign={recipientsOf} onClose={() => setRecipientsOf(null)} />}
    </div>
  );
}

/** Those who may have it but whom the outbox would still refuse, in a sentence, or nothing when there are none. */
function unreachableLine(a: CampaignAudience, purpose: CampaignPurpose): string {
  const parts: string[] = [];
  if (a.suppressed) parts.push(`${a.suppressed} at an address on the suppression list (bounced, complained or unsubscribed)`);
  if (purpose === 'marketing' && a.doNotContact) parts.push(`${a.doNotContact} marked do not contact`);
  return parts.length ? `Left out: ${parts.join(', ')}. ` : '';
}

/** Who a campaign would reach, in a sentence: the figures the server works out for it. */
function AudienceLine({ segment, purpose }: { segment: SegmentFilter; purpose: CampaignPurpose }) {
  const audience = useCampaignAudience(segment, purpose);
  if (audience.isPending) return <span className="text-fuchsia-700">Working out who it reaches…</span>;
  if (audience.isError || !audience.data) return <span>Who it reaches could not be worked out.</span>;
  const a = audience.data;
  return (
    <span>
      <strong>{a.matching}</strong> member{a.matching === 1 ? ' matches' : 's match'}, <strong>{a.withAddress}</strong> with an email address
      {purpose === 'marketing' && <>, <strong>{a.agreed}</strong> agreed to marketing by email</>}.{' '}
      {unreachableLine(a, purpose)}
      It would go to <strong>{a.willReceive}</strong> address{a.willReceive === 1 ? '' : 'es'}.
    </span>
  );
}

function CampaignDialog({
  form, onChange, onCancel, onSave, busy, segment,
}: {
  form: FormState;
  onChange: (f: FormState) => void;
  onCancel: () => void;
  onSave: () => void;
  busy: boolean;
  segment: SegmentFilter;
}) {
  const toggle = <T extends string>(list: T[], value: T) => (list.includes(value) ? list.filter((x) => x !== value) : [...list, value]);
  return (
    <Dialog open onOpenChange={(open) => { if (!open) onCancel(); }}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>{form.id ? 'Edit campaign' : 'New campaign'}</DialogTitle>
          <DialogDescription>
            Write the email and choose who it is for. Leave the time empty to keep it as a draft and send it when you are
            ready, or set a time and it is sent then.
          </DialogDescription>
        </DialogHeader>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
          <div className="md:col-span-2">
            <Label htmlFor="campaign-name">Name</Label>
            <Input id="campaign-name" aria-label="Name" value={form.name} onChange={(e) => onChange({ ...form, name: e.target.value })} />
          </div>
          <div>
            <Label htmlFor="campaign-purpose">What is it?</Label>
            <Select value={form.purpose} onValueChange={(v) => onChange({ ...form, purpose: v as CampaignPurpose })}>
              <SelectTrigger id="campaign-purpose" aria-label="What is it?"><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="marketing">Marketing: news, offers, invitations</SelectItem>
                <SelectItem value="service">Service: something members need to know</SelectItem>
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label htmlFor="campaign-category">Category</Label>
            <Select value={form.category} onValueChange={(v) => onChange({ ...form, category: v as CommCategory })}>
              <SelectTrigger id="campaign-category" aria-label="Category"><SelectValue /></SelectTrigger>
              <SelectContent>
                {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="md:col-span-2 text-xs text-slate-500">
            Sent by email. SMS and push are not available: CadenceE has no SMS or push provider.
          </div>
          <div className="md:col-span-2">
            <Label htmlFor="campaign-subject">Subject</Label>
            <Input id="campaign-subject" aria-label="Subject" value={form.subject} onChange={(e) => onChange({ ...form, subject: e.target.value })} />
          </div>
          <div className="md:col-span-2">
            <Label htmlFor="campaign-body">Message</Label>
            <Textarea id="campaign-body" aria-label="Message" value={form.body} onChange={(e) => onChange({ ...form, body: e.target.value })} rows={5} />
            <p className="mt-1 text-xs text-slate-500">{'{{firstName}}'} becomes each member&apos;s first name.</p>
          </div>
          <div className="md:col-span-2">
            <Label>Segment: tiers</Label>
            <div className="flex flex-wrap gap-1.5 mt-1">
              {MEMBER_TIERS.map((t) => {
                const on = form.tiers.includes(t);
                return (
                  <button key={t} type="button" aria-pressed={on} onClick={() => onChange({ ...form, tiers: toggle(form.tiers, t) })}
                    className={`text-xs px-3 py-1.5 rounded-full transition ${on ? 'bg-fuchsia-100 text-fuchsia-800 ring-2 ring-fuchsia-300' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
                    {t}
                  </button>
                );
              })}
            </div>
          </div>
          <div className="md:col-span-2">
            <Label>Segment: statuses</Label>
            <div className="flex flex-wrap gap-1.5 mt-1">
              {MEMBER_STATUSES.map((s) => {
                const on = form.statuses.includes(s);
                return (
                  <button key={s} type="button" aria-pressed={on} onClick={() => onChange({ ...form, statuses: toggle(form.statuses, s) })}
                    className={`text-xs px-3 py-1.5 rounded-full transition ${on ? 'bg-fuchsia-100 text-fuchsia-800 ring-2 ring-fuchsia-300' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'}`}>
                    {s}
                  </button>
                );
              })}
            </div>
          </div>
          <div>
            <Label htmlFor="campaign-send-at">Send at (optional)</Label>
            <Input id="campaign-send-at" aria-label="Send at" type="datetime-local" value={form.sendAt} onChange={(e) => onChange({ ...form, sendAt: e.target.value })} />
          </div>
          <div className="md:col-span-2 bg-fuchsia-50 border border-fuchsia-200 rounded-lg px-3 py-2 text-sm text-fuchsia-900" aria-live="polite">
            <Users className="mr-1 inline h-3.5 w-3.5" aria-hidden />
            <AudienceLine segment={segment} purpose={form.purpose} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onCancel} disabled={busy}>Cancel</Button>
          <Button onClick={onSave} disabled={busy}>
            {busy ? <><Loader2 className="w-4 h-4 mr-2 animate-spin" /> Saving</> : form.sendAt ? 'Schedule' : 'Save draft'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** "Send now", once someone has seen who it will reach and who it will not. */
function SendDialog({ campaign, onClose }: { campaign: Campaign; onClose: () => void }) {
  const audience = useCampaignAudience(campaign.segment, campaign.purpose);
  const [sending, setSending] = useState(false);
  const send = async () => {
    setSending(true);
    try {
      const sent = await sendCampaign(campaign.id, campaign.version);
      // The server answers at once and works through the members in the background: the
      // list follows it until it is sent, and it can be stopped meanwhile.
      toast.success(`"${sent.name}" is sending. Its figures fill in as each member's email is queued.`);
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The campaign could not be sent.');
    } finally {
      setSending(false);
    }
  };
  const a = audience.data;
  return (
    <Dialog open onOpenChange={(open) => { if (!open && !sending) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Send &ldquo;{campaign.name}&rdquo; now?</DialogTitle>
          <DialogDescription>
            It is sent once. After that it can no longer be changed or sent again.
          </DialogDescription>
        </DialogHeader>
        <div className="py-2 text-sm text-slate-700" aria-live="polite">
          {audience.isPending ? (
            <span>Working out who it reaches…</span>
          ) : a ? (
            <>
              <p>It goes to <strong>{a.willReceive}</strong> address{a.willReceive === 1 ? '' : 'es'}.</p>
              {a.matching - a.willReceive > 0 && (
                <p className="mt-1 text-slate-500">
                  {a.matching - a.willReceive} of the {a.matching} members it is for will not get it
                  {campaign.purpose === 'marketing'
                    ? ': no consent to marketing by email, no address, an address shared with another member, on the suppression list, or marked do not contact.'
                    : ': no address, an address shared with another member, or on the suppression list.'}
                </p>
              )}
            </>
          ) : (
            <span>Who it reaches could not be worked out; the server checks each member as it sends.</span>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={sending}>Cancel</Button>
          <Button onClick={() => void send()} disabled={sending}>
            {sending ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Sending</> : 'Send now'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** "Stop sending", once someone has read what stopping does and does not undo. */
function StopDialog({ campaign, onClose }: { campaign: Campaign; onClose: () => void }) {
  const [stopping, setStopping] = useState(false);
  const stop = async () => {
    setStopping(true);
    try {
      await stopCampaign(campaign.id, campaign.version);
      toast.success(`"${campaign.name}" is stopped. Members it had not reached get nothing.`);
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The campaign could not be stopped.');
    } finally {
      setStopping(false);
    }
  };
  return (
    <Dialog open onOpenChange={(open) => { if (!open && !stopping) onClose(); }}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle>Stop sending &ldquo;{campaign.name}&rdquo;?</DialogTitle>
          <DialogDescription>
            Members it has not reached yet get nothing, and its emails still waiting to go are not sent. Emails the mail
            server has already taken cannot be called back. It cannot be sent again afterwards.
          </DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={stopping}>Keep sending</Button>
          <Button variant="destructive" onClick={() => void stop()} disabled={stopping}>
            {stopping ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Stopping</> : 'Stop sending'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** Every member a campaign was for, and where their message stands. */
function RecipientsDialog({ campaign, onClose }: { campaign: Campaign; onClose: () => void }) {
  const recipients = useCampaignRecipients(campaign.id);
  return (
    <Dialog open onOpenChange={(open) => { if (!open) onClose(); }}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>Recipients of &ldquo;{campaign.name}&rdquo;</DialogTitle>
          <DialogDescription>{campaign.totals ? totalsLine(campaign.totals) : 'Where each member’s message stands.'}</DialogDescription>
        </DialogHeader>
        {recipients.isPending ? (
          <div className="flex justify-center py-10"><Loader2 className="h-5 w-5 animate-spin" /></div>
        ) : recipients.isError && !recipients.data ? (
          <LoadFailed what="The recipients" error={recipients.error} onRetry={() => void recipients.refetch()} retrying={recipients.isFetching} />
        ) : (
          <div className="max-h-[60vh] overflow-auto">
            <table className="w-full text-sm" aria-label="Recipients">
              <thead className="sticky top-0 bg-white">
                <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                  <th className="px-3 py-2">Member</th>
                  <th className="px-3 py-2">Address</th>
                  <th className="px-3 py-2">Status</th>
                </tr>
              </thead>
              <tbody>
                {(recipients.data ?? []).map((r) => (
                  <tr key={r.id} className="border-t border-slate-100 align-top">
                    <td className="px-3 py-2 font-medium text-slate-900">{r.memberName}</td>
                    <td className="px-3 py-2 text-slate-600">{r.email ?? '—'}</td>
                    <td className="px-3 py-2">
                      <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${RECIPIENT_BADGE[r.status]}`}>{commStatusLabel(r.status)}</span>
                      {r.reason && <p className="mt-1 text-xs text-slate-500">{r.reason}</p>}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}

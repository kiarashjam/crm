import { useMemo, useState } from 'react';
import {
  MessageSquareText,
  Search,
  NotebookPen,
  Mail,
  Smartphone,
  Bell,
  Eye,
  Loader2,
  Trash2,
  CalendarDays,
  CheckCircle2,
  Ban,
  Send,
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
  logCommunication,
  deleteCommunication,
  computeCommStats,
  commStatusLabel,
  CHANNELS,
  CATEGORIES,
  NOT_SENT,
  type Communication,
  type CommChannel,
  type CommCategory,
  type CommStatus,
} from '@/app/api/communications';
import type { Member } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { useClubMembers, useCommunications } from '@/app/hooks/queries/club';
import EmailMemberDialog from '@/app/pages/members/EmailMemberDialog';

const CHANNEL_ICON: Record<CommChannel, React.ElementType> = {
  Email: Mail,
  SMS: Smartphone,
  Push: Bell,
};

const CHANNEL_COLOR: Record<CommChannel, string> = {
  Email: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  SMS: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Push: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
};

const COMM_STATUS_BADGE: Record<CommStatus, string> = {
  queued: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  sent: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  delivered: 'bg-cyan-50 text-cyan-700 ring-1 ring-cyan-200',
  opened: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  bounced: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  suppressed: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  skipped: 'bg-amber-50 text-amber-800 ring-1 ring-amber-200',
  failed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  cancelled: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  logged: 'bg-slate-100 text-slate-500 ring-1 ring-slate-200',
};

type StatusFilter = 'All' | 'delivered' | 'notSent' | 'logged';

function formatDateTime(iso?: string | null): string {
  if (!iso) return '';
  try {
    return new Date(iso).toLocaleString(undefined, {
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit',
    });
  } catch {
    return iso;
  }
}

/** When a message went, or when it was written when it did not. */
const whenOf = (c: Communication) => c.sentAtUtc ?? c.createdAtUtc;

export default function Communications() {
  const commsQuery = useCommunications();
  const comms = useMemo(() => commsQuery.data ?? [], [commsQuery.data]);
  const membersQuery = useClubMembers();
  const loading = commsQuery.isPending;
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [channelFilter, setChannelFilter] = useState<'All' | CommChannel>('All');
  const [categoryFilter, setCategoryFilter] = useState<'All' | CommCategory>('All');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('All');
  const [emailing, setEmailing] = useState(false);
  const [logging, setLogging] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const stats = useMemo(() => computeCommStats(comms), [comms]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return comms.filter((c) => {
      if (channelFilter !== 'All' && c.channel !== channelFilter) return false;
      if (categoryFilter !== 'All' && c.category !== categoryFilter) return false;
      if (statusFilter === 'delivered' && c.status !== 'delivered' && c.status !== 'opened') return false;
      if (statusFilter === 'notSent' && !NOT_SENT.includes(c.status)) return false;
      if (statusFilter === 'logged' && c.status !== 'logged') return false;
      if (!q) return true;
      return `${c.memberName} ${c.email ?? ''} ${c.subject} ${c.preview ?? ''} ${c.campaignName ?? ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [comms, search, channelFilter, categoryFilter, statusFilter]);

  const remove = async (c: Communication) => {
    if (!confirm('Delete this logged message? It was recorded by hand; nothing was sent from CadenceE.')) return;
    setDeletingId(c.id);
    try {
      await deleteCommunication(c.id);
      toast.success('Deleted');
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The message could not be deleted.');
    } finally {
      setDeletingId(null);
    }
  };

  // How this person likes the message log shown: a feed or a table, saved for them.
  const view = useViewPreference('communications', { layouts: ['list', 'table'] });

  /**
   * Only a message logged by hand can be deleted: one CadenceE sent, or did not send
   * for a reason, is the record of what went to whom.
   */
  const communicationActions = (c: Communication) => (c.status !== 'logged' ? null : (
    <WriteOnly permission={[PERMISSION.ClubMembers, PERMISSION.DeleteRecords]} all>
      <button
        type="button"
        onClick={() => void remove(c)}
        disabled={deletingId === c.id}
        className="rounded p-1.5 text-slate-500 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 shrink-0"
        aria-label={`Delete logged message to ${c.memberName}`}
      >
        {deletingId === c.id ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
      </button>
    </WriteOnly>
  ));

  const statusBadge = (c: Communication) => (
    <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${COMM_STATUS_BADGE[c.status]}`}>{commStatusLabel(c.status)}</span>
  );

  const communicationColumns: RecordColumn<Communication>[] = [
    {
      id: 'subject',
      label: 'Message',
      required: true,
      cell: (c) => {
        const Icon = CHANNEL_ICON[c.channel];
        return (
          <span className="flex items-center gap-2.5">
            <span className={`flex h-7 w-7 shrink-0 items-center justify-center rounded-lg ${CHANNEL_COLOR[c.channel]}`} aria-hidden>
              <Icon className="h-3.5 w-3.5" />
            </span>
            <span className="truncate">{c.subject}</span>
          </span>
        );
      },
    },
    { id: 'member', label: 'To', cell: (c) => c.memberName },
    { id: 'status', label: 'Status', cell: (c) => statusBadge(c) },
    { id: 'reason', label: 'Why not', byDefault: false, cell: (c) => c.reason || '—' },
    { id: 'category', label: 'Category', cell: (c) => c.category },
    { id: 'when', label: 'When', cell: (c) => formatDateTime(whenOf(c)) },
    { id: 'opened', label: 'Opened', byDefault: false, cell: (c) => (c.openedAtUtc ? formatDateTime(c.openedAtUtc) : '—') },
    { id: 'campaign', label: 'Campaign', byDefault: false, cell: (c) => c.campaignName || '—' },
  ];

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
            icon={MessageSquareText}
            tone="violet"
            title="Communications"
            subtitle="Every message the club sent a member, by a campaign or by someone on the team, and what came of it."
            actions={
              <WriteOnly permission={PERMISSION.ClubMembers}>
                <Button onClick={() => setEmailing(true)} className={heroPrimary('violet')}>
                  <Send className="h-4 w-4" />
                  Email a member
                </Button>
                <Button onClick={() => setLogging(true)} className={heroSecondary}>
                  <NotebookPen className="h-4 w-4" />
                  Log a message
                </Button>
              </WriteOnly>
            }
            stats={[
              { label: 'Last 7 days', value: stats.lastWeek, icon: CalendarDays, tone: 'violet', hint: 'messages' },
              { label: 'Delivered', value: stats.delivered, icon: CheckCircle2, tone: 'emerald', hint: 'emails, opened ones too' },
              { label: 'Opened', value: stats.opened, icon: Eye, tone: 'blue', hint: 'where opens are tracked' },
              { label: 'Not sent', value: stats.notSent, icon: Ban, tone: 'rose', hint: 'refused, bounced or failed' },
            ]}
          />

          <div role="note" className="mb-6 flex items-start gap-3 rounded-xl border border-violet-200 bg-violet-50 px-4 py-3 text-sm text-violet-900">
            <Mail className="mt-0.5 h-4 w-4 shrink-0" aria-hidden />
            <p>
              Emails go from the club&apos;s address and show whether each was delivered, opened or bounced, and why one
              was not sent. CadenceE cannot send SMS or push messages: log a text or a letter you sent yourself, and it
              reads &ldquo;Logged, not sent by CadenceE&rdquo;.
            </p>
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search by member, subject, campaign..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select value={statusFilter} onValueChange={(v) => setStatusFilter(v as StatusFilter)}>
              <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-40">
                <SelectValue placeholder="Status" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">Any status</SelectItem>
                <SelectItem value="delivered">Delivered</SelectItem>
                <SelectItem value="notSent">Not sent or bounced</SelectItem>
                <SelectItem value="logged">Logged by hand</SelectItem>
              </SelectContent>
            </Select>
            <Select value={channelFilter} onValueChange={(v) => setChannelFilter(v as 'All' | CommChannel)}>
              <SelectTrigger aria-label="Filter by channel" className="h-11 rounded-xl md:w-36">
                <SelectValue placeholder="Channel" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All channels</SelectItem>
                {CHANNELS.map((c) => (
                  <SelectItem key={c} value={c}>{c}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select value={categoryFilter} onValueChange={(v) => setCategoryFilter(v as 'All' | CommCategory)}>
              <SelectTrigger aria-label="Filter by category" className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All categories</SelectItem>
                {CATEGORIES.map((c) => (
                  <SelectItem key={c} value={c}>{c}</SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(communicationColumns)} className="md:ml-auto" />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : commsQuery.isError && !commsQuery.data ? (
            <LoadFailed what="Messages" error={commsQuery.error} onRetry={() => void commsQuery.refetch()} retrying={commsQuery.isFetching} />
          ) : (
            <>
              {commsQuery.isError && (
                <LoadFailed stale what="Messages" error={commsQuery.error} onRetry={() => void commsQuery.refetch()} retrying={commsQuery.isFetching} className="mb-4" />
              )}
              {filtered.length === 0 ? (
                <EmptyState
                  icon={MessageSquareText}
                  title={comms.length === 0 ? 'No messages yet' : 'No messages match these filters'}
                  description={
                    comms.length === 0
                      ? 'Email a member, or log a message you sent another way, so the team can see what went to whom.'
                      : 'Try clearing filters or adjusting your search.'
                  }
                  actionLabel={comms.length === 0 ? 'Email a member' : undefined}
                  onAction={comms.length === 0 ? () => setEmailing(true) : undefined}
                  actionPermission={PERMISSION.ClubMembers}
                  variant="orange"
                />
              ) : (
                <ViewTransition layout={view.layout}>
                  {view.layout === 'table' ? (
                    <RecordTable
                      label="Communications"
                      items={filtered}
                      view={view}
                      getKey={(c) => c.id}
                      columns={communicationColumns}
                      actions={communicationActions}
                    />
                  ) : (
                    <div className="space-y-2">
                      {filtered.map((c, index) => {
                        const Icon = CHANNEL_ICON[c.channel];
                        return (
                          <StaggerItem
                            as="article"
                            key={c.id}
                            index={index}
                            className={`rounded-xl border border-slate-200 bg-white ${view.density === 'compact' ? 'p-2.5' : 'p-4'} shadow-sm hover:border-violet-200 ${liftOnHover}`}
                          >
                            <div className="flex items-start gap-3">
                              <div className={`w-10 h-10 rounded-lg flex items-center justify-center shrink-0 ${CHANNEL_COLOR[c.channel]}`}>
                                <Icon className="w-4 h-4" />
                              </div>
                              <div className="min-w-0 flex-1">
                                <div className="flex flex-wrap items-center gap-2">
                                  <p className="font-semibold text-slate-900 truncate">{c.subject}</p>
                                  {statusBadge(c)}
                                  <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">
                                    {c.purpose === 'marketing' ? 'Marketing · ' : c.purpose === 'service' ? 'Service · ' : ''}{c.category}
                                  </span>
                                </div>
                                {c.preview && view.density !== 'compact' && (
                                  <p className="text-sm text-slate-500 mt-0.5 line-clamp-2">{c.preview}</p>
                                )}
                                {c.reason && <p className="text-xs text-rose-700 mt-1">{c.reason}</p>}
                                <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 text-xs text-slate-500">
                                  <span>
                                    To <span className="font-medium text-slate-600">{c.memberName}</span>
                                    {c.email ? ` · ${c.email}` : ''}
                                  </span>
                                  <span>{formatDateTime(whenOf(c))}</span>
                                  {c.openedAtUtc && (
                                    <span className="inline-flex items-center gap-1 text-emerald-700">
                                      <Eye className="w-3 h-3" /> opened {formatDateTime(c.openedAtUtc)}
                                    </span>
                                  )}
                                  {c.campaignName && <span>Campaign: {c.campaignName}</span>}
                                </div>
                              </div>
                              {communicationActions(c)}
                            </div>
                          </StaggerItem>
                        );
                      })}
                    </div>
                  )}
                </ViewTransition>
              )}
            </>
          )}
        </main>
      </PageTransition>

      {emailing && <EmailMemberDialog members={membersQuery.data} onClose={() => setEmailing(false)} />}
      {logging && <LogMessageDialog members={membersQuery.data} onClose={() => setLogging(false)} />}
    </div>
  );
}

/** A message the team sent some other way, recorded so everyone can see it. Nothing is sent. */
function LogMessageDialog({ members, onClose }: { members?: Member[]; onClose: () => void }) {
  const [memberId, setMemberId] = useState('');
  const [channel, setChannel] = useState<CommChannel>('SMS');
  const [category, setCategory] = useState<CommCategory>('Announcement');
  const [subject, setSubject] = useState('');
  const [preview, setPreview] = useState('');
  const [saving, setSaving] = useState(false);

  const save = async () => {
    const member = members?.find((m) => m.id === memberId);
    if (!member) {
      toast.error('Pick the member it went to.');
      return;
    }
    if (!subject.trim()) {
      toast.error('Say what it was about.');
      return;
    }
    setSaving(true);
    try {
      await logCommunication(
        { memberId: member.id, channel, category, subject: subject.trim(), preview: preview.trim() || undefined },
        `${member.firstName} ${member.lastName}`,
      );
      toast.success('Logged. CadenceE did not send it.');
      onClose();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'The message could not be logged.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open onOpenChange={(open) => { if (!open && !saving) onClose(); }}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>Log a message</DialogTitle>
          <DialogDescription>
            Record a text, a letter or an email you sent yourself, so the team can see it. Nothing is sent from here:
            CadenceE cannot send SMS or push.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4 py-2">
          <div>
            <Label htmlFor="log-member">To</Label>
            <Select value={memberId} onValueChange={setMemberId}>
              <SelectTrigger id="log-member" aria-label="To">
                <SelectValue placeholder={members ? 'Pick a member' : 'Loading members…'} />
              </SelectTrigger>
              <SelectContent>
                {(members ?? []).map((m) => (
                  <SelectItem key={m.id} value={m.id}>
                    {m.firstName} {m.lastName}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <Label htmlFor="log-channel">Sent by</Label>
              <Select value={channel} onValueChange={(v) => setChannel(v as CommChannel)}>
                <SelectTrigger id="log-channel" aria-label="Sent by"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CHANNELS.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
            <div>
              <Label htmlFor="log-category">Category</Label>
              <Select value={category} onValueChange={(v) => setCategory(v as CommCategory)}>
                <SelectTrigger id="log-category" aria-label="Category"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {CATEGORIES.map((c) => <SelectItem key={c} value={c}>{c}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </div>
          <div>
            <Label htmlFor="log-subject">What it was about</Label>
            <Input id="log-subject" aria-label="What it was about" value={subject} onChange={(e) => setSubject(e.target.value)} maxLength={300} />
          </div>
          <div>
            <Label htmlFor="log-preview">Note (optional)</Label>
            <Textarea id="log-preview" aria-label="Note" value={preview} onChange={(e) => setPreview(e.target.value)} rows={3} />
          </div>
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={saving}>Cancel</Button>
          <Button onClick={() => void save()} disabled={saving}>
            {saving ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" aria-hidden /> Saving</> : 'Log message'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

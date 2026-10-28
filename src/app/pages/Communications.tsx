import { useEffect, useMemo, useState } from 'react';
import {
  MessageSquareText,
  Search,
  Send,
  Mail,
  Smartphone,
  Bell,
  Eye,
  MousePointerClick,
  AlertCircle,
  Loader2,
  Trash2,
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
  getCommunications,
  sendCommunication,
  deleteCommunication,
  computeCommStats,
  CHANNELS,
  CATEGORIES,
  type Communication,
  type CommChannel,
  type CommCategory,
  type CommStatus,
} from '@/app/api/communications';
import { getMembers, type Member } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

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

const STATUS_BADGE: Record<CommStatus, string> = {
  Queued: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Sent: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Delivered: 'bg-cyan-50 text-cyan-700 ring-1 ring-cyan-200',
  Opened: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Clicked: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
  Failed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
  Bounced: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
};

function formatDateTime(iso: string): string {
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

function formatRate(r: number): string {
  return `${Math.round(r * 1000) / 10}%`;
}

export default function Communications() {
  const [comms, setComms] = useState<Communication[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [channelFilter, setChannelFilter] = useState<'All' | CommChannel>('All');
  const [categoryFilter, setCategoryFilter] = useState<'All' | CommCategory>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    memberId: '',
    channel: 'Email' as CommChannel,
    category: 'Announcement' as CommCategory,
    subject: '',
    preview: '',
  });
  const [sending, setSending] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [c, m] = await Promise.all([getCommunications(), getMembers()]);
      setComms(c);
      setMembers(m);
    } catch {
      toast.error('Failed to load communications');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeCommStats(comms), [comms]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return comms.filter((c) => {
      if (channelFilter !== 'All' && c.channel !== channelFilter) return false;
      if (categoryFilter !== 'All' && c.category !== categoryFilter) return false;
      if (!q) return true;
      return `${c.memberName} ${c.subject} ${c.preview} ${c.campaignName ?? ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [comms, search, channelFilter, categoryFilter]);

  const openSend = () => {
    setForm({
      memberId: members[0]?.id ?? '',
      channel: 'Email',
      category: 'Announcement',
      subject: '',
      preview: '',
    });
    setDialogOpen(true);
  };

  const send = async () => {
    const member = members.find((m) => m.id === form.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    if (!form.subject.trim()) {
      toast.error('Subject is required');
      return;
    }
    setSending(true);
    try {
      await sendCommunication({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        channel: form.channel,
        category: form.category,
        subject: form.subject.trim(),
        preview: form.preview.trim() || form.subject.trim(),
      });
      toast.success('Message sent');
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to send');
    } finally {
      setSending(false);
    }
  };

  const remove = async (c: Communication) => {
    if (!confirm('Delete this communication?')) return;
    setDeletingId(c.id);
    try {
      const ok = await deleteCommunication(c.id);
      if (ok) {
        toast.success('Deleted');
        await load();
      }
    } finally {
      setDeletingId(null);
    }
  };

  // How this person likes the message log shown: a feed, cards or a table, saved for them.
  const view = useViewPreference('communications', { layouts: ['list', 'grid', 'table'] });

  /**
   * Delete: the same on a row, a card and a table line. It used to draw an
   * invisible icon, so the button was there but could not be seen.
   */
  const communicationActions = (c: Communication) => (
    <button
      type="button"
      onClick={() => remove(c)}
      disabled={deletingId === c.id}
      className="rounded p-1.5 text-slate-300 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 shrink-0"
      aria-label={`Delete message to ${c.memberName}`}
    >
      {deletingId === c.id ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : (
        <Trash2 className="h-4 w-4" />
      )}
    </button>
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
    { id: 'status', label: 'Status', cell: (c) => <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>{c.status}</span> },
    { id: 'category', label: 'Category', cell: (c) => c.category },
    { id: 'sent', label: 'Sent', cell: (c) => formatDateTime(c.sentAtUtc) },
    { id: 'opened', label: 'Opened', byDefault: false, cell: (c) => (c.openedAtUtc ? formatDateTime(c.openedAtUtc) : '—') },
    { id: 'clicked', label: 'Clicked', byDefault: false, cell: (c) => (c.clickedAtUtc ? formatDateTime(c.clickedAtUtc) : '—') },
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
            subtitle="Every email, SMS, and push sent to members — with open and click tracking."
            actions={
              <Button onClick={openSend} className={heroPrimary('violet')}>
                <Send className="h-4 w-4" />
                Send message
              </Button>
            }
            stats={[
              { label: 'Sent · 7d', value: stats.sentThisWeek, icon: Send, tone: 'violet', hint: 'total in last week' },
              { label: 'Open rate', value: stats.openRate, icon: Eye, tone: 'emerald', hint: 'all-time emails opened', format: formatRate },
              { label: 'Click rate', value: stats.clickRate, icon: MousePointerClick, tone: 'amber', hint: 'clicks per message', format: formatRate },
              { label: 'Bounce rate', value: stats.bounceRate, icon: AlertCircle, tone: 'rose', hint: 'bounced or failed', format: formatRate },
            ]}
          />

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
            <Select
              value={channelFilter}
              onValueChange={(v) => setChannelFilter(v as 'All' | CommChannel)}
            >
              <SelectTrigger aria-label="Filter by channel" className="h-11 rounded-xl md:w-36">
                <SelectValue placeholder="Channel" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All channels</SelectItem>
                {CHANNELS.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <Select
              value={categoryFilter}
              onValueChange={(v) => setCategoryFilter(v as 'All' | CommCategory)}
            >
              <SelectTrigger aria-label="Filter by category" className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Category" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All categories</SelectItem>
                {CATEGORIES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {c}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(communicationColumns)} className="md:ml-auto" />
          </div>

          {/* Feed */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={MessageSquareText}
              title={comms.length === 0 ? 'No communications yet' : 'No messages match these filters'}
              description={
                comms.length === 0
                  ? 'Send your first message to a member. Open, click, and bounce events show up here automatically.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={comms.length === 0 ? 'Send message' : undefined}
              onAction={comms.length === 0 ? openSend : undefined}
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
            ) : view.layout === 'grid' ? (
              <div className={`grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-4')}`}>
                {filtered.map((c, index) => {
                  const Icon = CHANNEL_ICON[c.channel];
                  return (
                    <StaggerItem
                      key={c.id}
                      index={index}
                      className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm hover:border-violet-200 ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                    >
                      <div className="flex items-start gap-3">
                        <div className={`w-10 h-10 rounded-lg flex items-center justify-center shrink-0 ${CHANNEL_COLOR[c.channel]}`}>
                          <Icon className="w-4 h-4" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <p className="font-semibold text-slate-900 truncate">{c.subject}</p>
                          <p className="text-xs text-slate-500 truncate">To {c.memberName}</p>
                        </div>
                        <span className={`shrink-0 text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>{c.status}</span>
                      </div>
                      {view.density !== 'compact' && <p className="mt-2 text-sm text-slate-500 line-clamp-2">{c.preview}</p>}
                      <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-slate-500">
                        <span className="rounded-full bg-slate-100 px-2 py-0.5 font-medium text-slate-600">{c.category}</span>
                        {c.openedAtUtc && (
                          <span className="inline-flex items-center gap-1 text-emerald-700"><Eye className="w-3 h-3" /> opened</span>
                        )}
                        {c.clickedAtUtc && (
                          <span className="inline-flex items-center gap-1 text-violet-600"><MousePointerClick className="w-3 h-3" /> clicked</span>
                        )}
                      </div>
                      <div className="mt-auto flex items-center justify-between pt-3 text-xs text-slate-500">
                        <span>{formatDateTime(c.sentAtUtc)}</span>
                        {communicationActions(c)}
                      </div>
                    </StaggerItem>
                  );
                })}
              </div>
            ) : (
            <div className="space-y-2">
              {filtered.map((c, index) => {
                const Icon = CHANNEL_ICON[c.channel];
                return (
                  <StaggerItem
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
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>
                            {c.status}
                          </span>
                          <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">
                            {c.category}
                          </span>
                        </div>
                        <p className="text-sm text-slate-500 mt-0.5 line-clamp-2">{c.preview}</p>
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 text-xs text-slate-500">
                          <span>To <span className="font-medium text-slate-600">{c.memberName}</span></span>
                          <span>{formatDateTime(c.sentAtUtc)}</span>
                          {c.openedAtUtc && (
                            <span className="inline-flex items-center gap-1 text-emerald-700">
                              <Eye className="w-3 h-3" /> opened {formatDateTime(c.openedAtUtc)}
                            </span>
                          )}
                          {c.clickedAtUtc && (
                            <span className="inline-flex items-center gap-1 text-violet-600">
                              <MousePointerClick className="w-3 h-3" /> clicked {formatDateTime(c.clickedAtUtc)}
                            </span>
                          )}
                          {c.campaignName && (
                            <span className="font-mono text-[10px]">{c.campaignName}</span>
                          )}
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
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Send message</DialogTitle>
            <DialogDescription>
              Log a member-facing email, SMS, or push. Status will appear as delivery events arrive.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>To member</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger aria-label="To member">
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.email}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Channel</Label>
                <Select value={form.channel} onValueChange={(v) => setForm({ ...form, channel: v as CommChannel })}>
                  <SelectTrigger aria-label="Channel">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CHANNELS.map((c) => (
                      <SelectItem key={c} value={c}>
                        {c}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Category</Label>
                <Select
                  value={form.category}
                  onValueChange={(v) => setForm({ ...form, category: v as CommCategory })}
                >
                  <SelectTrigger aria-label="Category">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {CATEGORIES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {c}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
            </div>
            <div>
              <Label>Subject / message</Label>
              <Input aria-label="Subject / message"
                value={form.subject}
                onChange={(e) => setForm({ ...form, subject: e.target.value })}
              />
            </div>
            <div>
              <Label>Preview body</Label>
              <Textarea aria-label="Preview body"
                value={form.preview}
                onChange={(e) => setForm({ ...form, preview: e.target.value })}
                rows={3}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={sending}>
              Cancel
            </Button>
            <Button onClick={send} disabled={sending}>
              {sending ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Sending
                </>
              ) : (
                'Send'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

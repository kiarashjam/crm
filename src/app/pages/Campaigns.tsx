import { useEffect, useMemo, useState } from 'react';
import {
  Megaphone,
  Plus,
  Send,
  Trash2,
  Search,
  Users,
  Mail,
  Smartphone,
  Bell,
  Clock,
  CheckCircle2,
  Loader2,
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
  getCampaigns,
  createCampaign,
  deleteCampaign,
  sendCampaign,
  previewSegment,
  computeCampaignStats,
  type Campaign,
  type SegmentFilter,
} from '@/app/api/campaigns';
import {
  MEMBER_TIERS,
  MEMBER_STATUSES,
  type MemberTier,
  type MemberStatus,
} from '@/app/api/members';
import { CHANNELS, CATEGORIES, type CommChannel, type CommCategory } from '@/app/api/communications';

const STATUS_BADGE: Record<Campaign['status'], string> = {
  Draft: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
  Scheduled: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Sending: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Sent: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Failed: 'bg-rose-50 text-rose-700 ring-1 ring-rose-200',
};

const CHANNEL_ICON: Record<CommChannel, React.ElementType> = {
  Email: Mail,
  SMS: Smartphone,
  Push: Bell,
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

export default function Campaigns() {
  const [campaigns, setCampaigns] = useState<Campaign[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    name: '',
    channel: 'Email' as CommChannel,
    category: 'Announcement' as CommCategory,
    subject: '',
    body: '',
    tiers: [] as MemberTier[],
    statuses: ['Active'] as MemberStatus[],
    scheduledAt: '',
  });
  const [busy, setBusy] = useState(false);
  const [sendingId, setSendingId] = useState<string | null>(null);
  const [previewCount, setPreviewCount] = useState<number | null>(null);

  const load = async () => {
    try {
      setCampaigns(await getCampaigns());
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeCampaignStats(campaigns), [campaigns]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return campaigns.filter((c) =>
      !q ? true : `${c.name} ${c.subject}`.toLowerCase().includes(q),
    );
  }, [campaigns, search]);

  const openCreate = () => {
    setForm({
      name: '',
      channel: 'Email',
      category: 'Announcement',
      subject: '',
      body: '',
      tiers: [],
      statuses: ['Active'],
      scheduledAt: '',
    });
    setPreviewCount(null);
    setDialogOpen(true);
  };

  const buildSegment = (): SegmentFilter => ({
    tiers: form.tiers.length ? form.tiers : undefined,
    statuses: form.statuses.length ? form.statuses : undefined,
  });

  const updatePreview = async () => {
    const members = await previewSegment(buildSegment());
    setPreviewCount(members.length);
  };

  useEffect(() => {
    if (dialogOpen) void updatePreview();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [dialogOpen, form.tiers, form.statuses]);

  const submit = async () => {
    if (!form.name.trim() || !form.subject.trim()) {
      toast.error('Name and subject are required');
      return;
    }
    setBusy(true);
    try {
      await createCampaign({
        name: form.name.trim(),
        channel: form.channel,
        category: form.category,
        subject: form.subject.trim(),
        body: form.body.trim() || form.subject.trim(),
        segment: buildSegment(),
        scheduledAtUtc: form.scheduledAt ? new Date(form.scheduledAt).toISOString() : undefined,
      });
      toast.success('Campaign created');
      setDialogOpen(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const send = async (c: Campaign) => {
    if (!confirm(`Send "${c.name}" now? This writes a communication for every matched member.`)) return;
    setSendingId(c.id);
    try {
      const result = await sendCampaign(c.id);
      if (result) toast.success(`Sent to ${result.recipientCount} member${result.recipientCount === 1 ? '' : 's'}`);
      await load();
    } finally {
      setSendingId(null);
    }
  };

  const remove = async (c: Campaign) => {
    if (!confirm(`Delete campaign "${c.name}"?`)) return;
    await deleteCampaign(c.id);
    toast.success('Deleted');
    await load();
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
          <div className="relative bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 rounded-2xl overflow-hidden mb-8">
            <div className="absolute inset-0">
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-fuchsia-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-purple-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-fuchsia-500 to-purple-500 flex items-center justify-center shadow-2xl shadow-fuchsia-500/30">
                    <Megaphone className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Campaigns</h1>
                    <p className="text-slate-400 mt-1">
                      Segment members and send bulk emails, SMS, or push.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCreate}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-fuchsia-500 to-purple-500 hover:from-fuchsia-600 hover:to-purple-600 shadow-lg shadow-fuchsia-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" /> New campaign
                </Button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard icon={<Megaphone className="w-5 h-5 text-fuchsia-600" />} label="Drafts" value={String(stats.drafts)} />
            <StatCard icon={<Clock className="w-5 h-5 text-blue-600" />} label="Scheduled" value={String(stats.scheduled)} />
            <StatCard icon={<CheckCircle2 className="w-5 h-5 text-emerald-600" />} label="Sent" value={String(stats.sent)} />
            <StatCard icon={<Users className="w-5 h-5 text-amber-600" />} label="Recipients" value={String(stats.totalRecipients)} hint="All campaigns" />
          </div>

          <div className="flex gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search campaigns..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={Megaphone}
              title={campaigns.length === 0 ? 'No campaigns yet' : 'No campaigns match'}
              description={
                campaigns.length === 0
                  ? 'Create your first campaign to broadcast to a segment of members.'
                  : 'Try clearing the search.'
              }
              actionLabel={campaigns.length === 0 ? 'New campaign' : undefined}
              onAction={campaigns.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <div className="space-y-3">
              {filtered.map((c) => {
                const ChIcon = CHANNEL_ICON[c.channel];
                return (
                  <article
                    key={c.id}
                    className="rounded-2xl border border-slate-200 bg-white p-5 shadow-sm"
                  >
                    <div className="flex items-start gap-4">
                      <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-fuchsia-100 to-purple-100 flex items-center justify-center text-fuchsia-700 shrink-0">
                        <ChIcon className="w-5 h-5" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <div className="flex flex-wrap items-center gap-2 mb-1">
                          <h3 className="font-semibold text-slate-900 truncate">{c.name}</h3>
                          <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${STATUS_BADGE[c.status]}`}>
                            {c.status}
                          </span>
                          <span className="text-xs px-2 py-0.5 rounded-full font-medium bg-slate-100 text-slate-600">
                            {c.channel} · {c.category}
                          </span>
                        </div>
                        <p className="text-sm font-medium text-slate-700">{c.subject}</p>
                        <p className="text-sm text-slate-500 mt-0.5 line-clamp-2">{c.body}</p>
                        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 mt-2 text-xs text-slate-500">
                          {c.segment.tiers && c.segment.tiers.length > 0 && (
                            <span>Tiers: {c.segment.tiers.join(', ')}</span>
                          )}
                          {c.segment.statuses && c.segment.statuses.length > 0 && (
                            <span>Statuses: {c.segment.statuses.join(', ')}</span>
                          )}
                          {c.scheduledAtUtc && (
                            <span>Scheduled {formatDateTime(c.scheduledAtUtc)}</span>
                          )}
                          {c.sentAtUtc && (
                            <span>Sent {formatDateTime(c.sentAtUtc)} · {c.recipientCount} recipients</span>
                          )}
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        {c.status !== 'Sent' && c.status !== 'Sending' && (
                          <Button size="sm" onClick={() => send(c)} disabled={sendingId === c.id}>
                            {sendingId === c.id ? (
                              <Loader2 className="w-3.5 h-3.5 animate-spin" />
                            ) : (
                              <>
                                <Send className="w-3.5 h-3.5 mr-1" /> Send now
                              </>
                            )}
                          </Button>
                        )}
                        <button
                          type="button"
                          onClick={() => remove(c)}
                          className="rounded p-2 text-slate-400 hover:bg-rose-50 hover:text-rose-600"
                          aria-label="Delete"
                        >
                          <Trash2 className="w-4 h-4" />
                        </button>
                      </div>
                    </div>
                  </article>
                );
              })}
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-2xl">
          <DialogHeader>
            <DialogTitle>New campaign</DialogTitle>
            <DialogDescription>
              Pick a channel, write the message, define a segment, and either save as draft, schedule, or send now.
            </DialogDescription>
          </DialogHeader>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 py-2">
            <div className="md:col-span-2">
              <Label>Name</Label>
              <Input value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            </div>
            <div>
              <Label>Channel</Label>
              <Select
                value={form.channel}
                onValueChange={(v) => setForm({ ...form, channel: v as CommChannel })}
              >
                <SelectTrigger>
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
                <SelectTrigger>
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
            <div className="md:col-span-2">
              <Label>Subject</Label>
              <Input
                value={form.subject}
                onChange={(e) => setForm({ ...form, subject: e.target.value })}
              />
            </div>
            <div className="md:col-span-2">
              <Label>Body</Label>
              <Textarea
                value={form.body}
                onChange={(e) => setForm({ ...form, body: e.target.value })}
                rows={4}
              />
            </div>
            <div className="md:col-span-2">
              <Label>Segment — tiers</Label>
              <div className="flex flex-wrap gap-1.5 mt-1">
                {MEMBER_TIERS.map((t) => {
                  const on = form.tiers.includes(t);
                  return (
                    <button
                      key={t}
                      type="button"
                      onClick={() =>
                        setForm({
                          ...form,
                          tiers: on ? form.tiers.filter((x) => x !== t) : [...form.tiers, t],
                        })
                      }
                      className={`text-xs px-3 py-1.5 rounded-full transition ${
                        on
                          ? 'bg-fuchsia-100 text-fuchsia-800 ring-2 ring-fuchsia-300'
                          : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                      }`}
                    >
                      {t}
                    </button>
                  );
                })}
              </div>
            </div>
            <div className="md:col-span-2">
              <Label>Segment — statuses</Label>
              <div className="flex flex-wrap gap-1.5 mt-1">
                {MEMBER_STATUSES.map((s) => {
                  const on = form.statuses.includes(s);
                  return (
                    <button
                      key={s}
                      type="button"
                      onClick={() =>
                        setForm({
                          ...form,
                          statuses: on
                            ? form.statuses.filter((x) => x !== s)
                            : [...form.statuses, s],
                        })
                      }
                      className={`text-xs px-3 py-1.5 rounded-full transition ${
                        on
                          ? 'bg-fuchsia-100 text-fuchsia-800 ring-2 ring-fuchsia-300'
                          : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                      }`}
                    >
                      {s}
                    </button>
                  );
                })}
              </div>
            </div>
            <div className="md:col-span-2">
              <Label>Schedule for (optional)</Label>
              <Input
                type="datetime-local"
                value={form.scheduledAt}
                onChange={(e) => setForm({ ...form, scheduledAt: e.target.value })}
              />
            </div>
            <div className="md:col-span-2 bg-fuchsia-50 border border-fuchsia-200 rounded-lg px-3 py-2 text-sm text-fuchsia-900">
              {previewCount === null ? (
                <span className="text-fuchsia-700">Calculating segment...</span>
              ) : (
                <>
                  This campaign will reach <strong>{previewCount}</strong> member
                  {previewCount === 1 ? '' : 's'} based on the current filters.
                </>
              )}
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Saving
                </>
              ) : (
                'Save campaign'
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

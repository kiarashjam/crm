import { useEffect, useMemo, useState } from 'react';
import {
  ScanLine,
  Plus,
  Search,
  Clock,
  MapPin,
  Users,
  Sparkles,
  Trash2,
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
import { Switch } from '@/app/components/ui/switch';
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
  getVisits,
  checkIn,
  deleteVisit,
  computeVisitStats,
  VISIT_VENUES,
  type Visit,
  type VisitVenue,
} from '@/app/api/visits';
import { getMembers, type Member } from '@/app/api/members';

const VENUE_COLOR: Record<VisitVenue, string> = {
  Dining: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200',
  Bar: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Spa: 'bg-pink-50 text-pink-700 ring-1 ring-pink-200',
  Gym: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Coworking: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  Event: 'bg-fuchsia-50 text-fuchsia-700 ring-1 ring-fuchsia-200',
  Lounge: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
  Rooftop: 'bg-teal-50 text-teal-700 ring-1 ring-teal-200',
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

function timeAgo(iso: string): string {
  const diff = Date.now() - Date.parse(iso);
  if (diff < 60_000) return 'just now';
  const min = Math.floor(diff / 60_000);
  if (min < 60) return `${min}m ago`;
  const h = Math.floor(min / 60);
  if (h < 24) return `${h}h ago`;
  const d = Math.floor(h / 24);
  return `${d}d ago`;
}

export default function Visits() {
  const [visits, setVisits] = useState<Visit[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [venueFilter, setVenueFilter] = useState<'All' | VisitVenue>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    memberId: '',
    venue: 'Dining' as VisitVenue,
    guestCount: '0',
    awardPoints: true,
    notes: '',
  });
  const [saving, setSaving] = useState(false);
  const [deletingId, setDeletingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [v, m] = await Promise.all([getVisits(), getMembers()]);
      setVisits(v);
      setMembers(m);
    } catch {
      toast.error('Failed to load visits');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeVisitStats(visits), [visits]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return visits.filter((v) => {
      if (venueFilter !== 'All' && v.venue !== venueFilter) return false;
      if (!q) return true;
      return `${v.memberName} ${v.venue} ${v.notes ?? ''}`.toLowerCase().includes(q);
    });
  }, [visits, search, venueFilter]);

  const openCheckIn = () => {
    setForm({
      memberId: members[0]?.id ?? '',
      venue: 'Dining',
      guestCount: '0',
      awardPoints: true,
      notes: '',
    });
    setDialogOpen(true);
  };

  const submit = async () => {
    if (!form.memberId) {
      toast.error('Pick a member');
      return;
    }
    setSaving(true);
    try {
      const result = await checkIn({
        memberId: form.memberId,
        venue: form.venue,
        guestCount: Number(form.guestCount) || 0,
        notes: form.notes.trim() || undefined,
        awardPoints: form.awardPoints,
      });
      if (result) {
        toast.success(
          form.awardPoints && result.pointsAwarded > 0
            ? `Checked in — +${result.pointsAwarded} loyalty points`
            : 'Checked in',
        );
      }
      setDialogOpen(false);
      await load();
    } catch {
      toast.error('Failed to check in');
    } finally {
      setSaving(false);
    }
  };

  const remove = async (v: Visit) => {
    if (!confirm(`Delete this visit by ${v.memberName}?`)) return;
    setDeletingId(v.id);
    try {
      const ok = await deleteVisit(v.id);
      if (ok) {
        toast.success('Visit deleted');
        await load();
      }
    } finally {
      setDeletingId(null);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-cyan-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-blue-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-cyan-500 to-blue-500 flex items-center justify-center shadow-2xl shadow-cyan-500/30">
                    <ScanLine className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Visits</h1>
                    <p className="text-slate-400 mt-1">
                      Check-ins, attendance, and the touchpoints that earn loyalty.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openCheckIn}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-cyan-500 to-blue-500 hover:from-cyan-600 hover:to-blue-600 shadow-lg shadow-cyan-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  Check in
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<ScanLine className="w-5 h-5 text-cyan-600" />}
              label="Today"
              value={String(stats.today)}
              hint={`${stats.uniqueMembersToday} unique members`}
            />
            <StatCard
              icon={<Clock className="w-5 h-5 text-blue-600" />}
              label="This week"
              value={String(stats.thisWeek)}
              hint="Last 7 days"
            />
            <StatCard
              icon={<Sparkles className="w-5 h-5 text-amber-600" />}
              label="Points · today"
              value={stats.pointsAwardedToday.toLocaleString()}
              hint="Loyalty awarded"
            />
            <StatCard
              icon={<MapPin className="w-5 h-5 text-violet-600" />}
              label="Top venue · 7d"
              value={stats.topVenue ?? '—'}
              hint="Most visited area"
            />
          </div>

          {/* Filters */}
          <div className="flex flex-col md:flex-row gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search visits..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={venueFilter}
              onValueChange={(v) => setVenueFilter(v as 'All' | VisitVenue)}
            >
              <SelectTrigger className="h-11 rounded-xl md:w-44">
                <SelectValue placeholder="Venue" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All venues</SelectItem>
                {VISIT_VENUES.map((v) => (
                  <SelectItem key={v} value={v}>
                    {v}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Visit feed */}
          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={ScanLine}
              title={visits.length === 0 ? 'No check-ins yet' : 'No visits match these filters'}
              description={
                visits.length === 0
                  ? 'Use Check in to log a member arriving at any venue. Points are awarded automatically.'
                  : 'Try clearing filters or adjusting your search.'
              }
              actionLabel={visits.length === 0 ? 'Check in' : undefined}
              onAction={visits.length === 0 ? openCheckIn : undefined}
              variant="teal"
            />
          ) : (
            <div className="space-y-2">
              {filtered.map((v) => (
                <div
                  key={v.id}
                  className="rounded-xl border border-slate-200 bg-white p-4 shadow-sm hover:shadow-md transition-shadow flex items-center gap-4"
                >
                  <div
                    className={`w-10 h-10 rounded-lg flex items-center justify-center shrink-0 ${VENUE_COLOR[v.venue]}`}
                  >
                    <MapPin className="w-4 h-4" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="flex flex-wrap items-center gap-2">
                      <p className="font-semibold text-slate-900 truncate">{v.memberName}</p>
                      <span className="text-xs px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">
                        {v.memberTier}
                      </span>
                      <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${VENUE_COLOR[v.venue]}`}>
                        {v.venue}
                      </span>
                      {v.guestCount > 0 && (
                        <span className="inline-flex items-center gap-1 text-xs text-slate-500">
                          <Users className="w-3 h-3" /> +{v.guestCount} guest{v.guestCount > 1 ? 's' : ''}
                        </span>
                      )}
                    </div>
                    <div className="flex items-center gap-3 mt-1 text-xs text-slate-500">
                      <span>{timeAgo(v.visitedAtUtc)}</span>
                      <span>·</span>
                      <span>{formatDateTime(v.visitedAtUtc)}</span>
                      {v.notes && (
                        <>
                          <span>·</span>
                          <span className="italic truncate">{v.notes}</span>
                        </>
                      )}
                    </div>
                  </div>
                  {v.pointsAwarded > 0 && (
                    <div className="text-right shrink-0">
                      <p className="text-xs text-slate-400 uppercase tracking-wide">Earned</p>
                      <p className="font-semibold text-amber-600">+{v.pointsAwarded}</p>
                    </div>
                  )}
                  <button
                    type="button"
                    onClick={() => remove(v)}
                    disabled={deletingId === v.id}
                    className="rounded p-1.5 text-slate-400 hover:bg-rose-50 hover:text-rose-600 disabled:opacity-50 shrink-0"
                    aria-label="Delete visit"
                  >
                    {deletingId === v.id ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Trash2 className="h-4 w-4" />
                    )}
                  </button>
                </div>
              ))}
            </div>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Check in member</DialogTitle>
            <DialogDescription>
              Log an arrival. Points are awarded based on the venue and the member's tier.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} ({m.tier})
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Venue</Label>
                <Select
                  value={form.venue}
                  onValueChange={(v) => setForm({ ...form, venue: v as VisitVenue })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {VISIT_VENUES.map((v) => (
                      <SelectItem key={v} value={v}>
                        {v}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Guests</Label>
                <Input
                  type="number"
                  min="0"
                  value={form.guestCount}
                  onChange={(e) => setForm({ ...form, guestCount: e.target.value })}
                />
              </div>
            </div>
            <div className="flex items-center justify-between bg-slate-50 rounded-lg px-3 py-2">
              <div>
                <p className="text-sm font-medium text-slate-900">Award loyalty points</p>
                <p className="text-xs text-slate-500">
                  Adds a ledger entry with the tier multiplier applied.
                </p>
              </div>
              <Switch
                checked={form.awardPoints}
                onCheckedChange={(checked) => setForm({ ...form, awardPoints: checked })}
              />
            </div>
            <div>
              <Label>Notes</Label>
              <Textarea
                value={form.notes}
                onChange={(e) => setForm({ ...form, notes: e.target.value })}
                rows={2}
                placeholder="Optional..."
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
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Checking in
                </>
              ) : (
                'Check in'
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

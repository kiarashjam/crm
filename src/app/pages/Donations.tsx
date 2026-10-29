import { useEffect, useMemo, useState } from 'react';
import {
  HeartHandshake,
  Plus,
  Search,
  Trash2,
  CheckCircle2,
  DollarSign,
  Users,
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
  getDonations,
  recordDonation,
  acknowledgeDonation,
  deleteDonation,
  computeDonationStats,
  DONATION_CAUSES,
  CAUSE_LABELS,
  type Donation,
  type DonationCause,
} from '@/app/api/donations';
import { getMembers, type Member } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';

const CAUSE_COLOR: Record<DonationCause, string> = {
  StaffSupport: 'bg-orange-50 text-orange-700 ring-1 ring-orange-200',
  Scholarship: 'bg-blue-50 text-blue-700 ring-1 ring-blue-200',
  CommunityGrants: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200',
  Sustainability: 'bg-teal-50 text-teal-700 ring-1 ring-teal-200',
  CulinaryEducation: 'bg-amber-50 text-amber-700 ring-1 ring-amber-200',
  Arts: 'bg-violet-50 text-violet-700 ring-1 ring-violet-200',
  Other: 'bg-slate-100 text-slate-600 ring-1 ring-slate-200',
};

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
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

export default function Donations() {
  const [donations, setDonations] = useState<Donation[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [causeFilter, setCauseFilter] = useState<'All' | DonationCause>('All');
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({
    memberId: '',
    cause: 'StaffSupport' as DonationCause,
    amount: '',
    anonymous: false,
    message: '',
  });
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      const [d, m] = await Promise.all([getDonations(), getMembers()]);
      setDonations(d);
      setMembers(m);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeDonationStats(donations), [donations]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return donations.filter((d) => {
      if (causeFilter !== 'All' && d.cause !== causeFilter) return false;
      if (!q) return true;
      return `${d.memberName} ${d.message ?? ''} ${CAUSE_LABELS[d.cause]}`
        .toLowerCase()
        .includes(q);
    });
  }, [donations, search, causeFilter]);

  const openCreate = () => {
    setForm({ memberId: '', cause: 'StaffSupport', amount: '', anonymous: false, message: '' });
    setDialogOpen(true);
  };

  const submit = async () => {
    const member = members.find((m) => m.id === form.memberId);
    if (!member) {
      toast.error('Pick a member');
      return;
    }
    const amount = Number(form.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    setBusy(true);
    try {
      await recordDonation({
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        cause: form.cause,
        amount,
        isAnonymous: form.anonymous,
        message: form.message.trim() || undefined,
      });
      toast.success('Donation recorded');
      setDialogOpen(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const acknowledge = async (d: Donation) => {
    await acknowledgeDonation(d.id);
    toast.success('Acknowledged — thank-you logged');
    await load();
  };

  const remove = async (d: Donation) => {
    if (!confirm(`Delete this donation entry?`)) return;
    await deleteDonation(d.id);
    toast.success('Deleted');
    await load();
  };

  // How this person likes donations shown, saved for them. A table, as it always was, until they choose.
  const view = useViewPreference('donations', { layouts: ['table', 'list', 'grid'] });
  const donorName = (d: Donation) => (d.isAnonymous ? 'Anonymous' : d.memberName);

  const status = (d: Donation) =>
    d.acknowledged ? (
      <span className="inline-flex items-center gap-1 text-xs text-emerald-700">
        <CheckCircle2 className="w-3 h-3" /> Acknowledged
      </span>
    ) : (
      <span className="inline-flex items-center gap-1 rounded-full bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-700 ring-1 ring-amber-200">
        Awaiting thanks
      </span>
    );

  /** Acknowledge and delete, the same on a row, a card and a table line. */
  const donationActions = (d: Donation) => (
    <>
      {!d.acknowledged && (
        <Button size="sm" variant="outline" onClick={() => acknowledge(d)}>
          Acknowledge
        </Button>
      )}
      <button
        type="button"
        onClick={() => remove(d)}
        className="rounded p-1.5 text-slate-500 hover:bg-rose-50 hover:text-rose-600"
        aria-label={`Delete donation from ${donorName(d)}`}
      >
        <Trash2 className="w-4 h-4" />
      </button>
    </>
  );

  const donationColumns: RecordColumn<Donation>[] = [
    {
      id: 'donor',
      label: 'Donor',
      required: true,
      cell: (d) => (
        <span className="block">
          <span className="flex items-center gap-2.5">
            <InitialsAvatar name={d.isAnonymous ? '?' : d.memberName} size="sm" />
            <span className="truncate">{donorName(d)}</span>
          </span>
          {d.message && <span className="mt-1 block text-xs font-normal italic text-slate-500">"{d.message}"</span>}
        </span>
      ),
    },
    {
      id: 'cause',
      label: 'Cause',
      cell: (d) => (
        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${CAUSE_COLOR[d.cause]}`}>{CAUSE_LABELS[d.cause]}</span>
      ),
    },
    { id: 'amount', label: 'Amount', numeric: true, cell: (d) => <span className="font-semibold text-rose-700">{formatCurrency(d.amount)}</span> },
    { id: 'received', label: 'Received', cell: (d) => <span className="whitespace-nowrap">{formatDate(d.receivedAtUtc)}</span> },
    { id: 'status', label: 'Status', cell: status },
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
            icon={HeartHandshake}
            tone="rose"
            title="Donations"
            subtitle="Member contributions to staff support, scholarship, and community causes."
            actions={
              <Button onClick={openCreate} className={heroPrimary('rose')}>
                <Plus className="h-4 w-4" /> Record donation
              </Button>
            }
            stats={[
              { label: 'Total raised', value: stats.totalRaised, icon: DollarSign, tone: 'rose', format: formatCurrency },
              { label: 'Donors', value: stats.donorCount, icon: Users, tone: 'blue' },
              {
                label: 'Top cause',
                value: stats.topCause ? CAUSE_LABELS[stats.topCause.cause] : '—',
                icon: HeartHandshake,
                tone: 'amber',
                hint: stats.topCause ? formatCurrency(stats.topCause.amount) : undefined,
              },
              { label: 'To acknowledge', value: stats.awaitingAcknowledgement, icon: CheckCircle2, tone: 'emerald' },
            ]}
          />

          <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
            <div className="relative flex-1 max-w-md">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
              <Input
                value={search}
                onChange={(e) => setSearch(e.target.value)}
                placeholder="Search donations..."
                className="pl-9 h-11 rounded-xl"
              />
            </div>
            <Select
              value={causeFilter}
              onValueChange={(v) => setCauseFilter(v as 'All' | DonationCause)}
            >
              <SelectTrigger aria-label="Filter by cause" className="h-11 rounded-xl md:w-56">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="All">All causes</SelectItem>
                {DONATION_CAUSES.map((c) => (
                  <SelectItem key={c} value={c}>
                    {CAUSE_LABELS[c]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
            <ViewSwitcher view={view} columns={columnChoices(donationColumns)} className="md:ml-auto" />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <EmptyState
              icon={HeartHandshake}
              title={donations.length === 0 ? 'No donations yet' : 'No donations match'}
              description={
                donations.length === 0
                  ? 'Record member contributions to track impact and acknowledgement.'
                  : 'Try clearing filters.'
              }
              actionLabel={donations.length === 0 ? 'Record donation' : undefined}
              onAction={donations.length === 0 ? openCreate : undefined}
              variant="orange"
            />
          ) : (
            <ViewTransition layout={view.layout}>
              {view.layout === 'list' ? (
                <RecordList
                  label="Donations"
                  items={filtered}
                  view={view}
                  getKey={(d) => d.id}
                  leading={(d) => <InitialsAvatar name={d.isAnonymous ? '?' : d.memberName} />}
                  title={donorName}
                  subtitle={(d) => d.message ? `"${d.message}"` : CAUSE_LABELS[d.cause]}
                  columns={donationColumns}
                  actions={donationActions}
                />
              ) : view.layout === 'grid' ? (
                <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
                  {filtered.map((d, index) => (
                    <StaggerItem
                      key={d.id}
                      index={index}
                      className={`flex flex-col rounded-2xl border border-slate-200/80 bg-white shadow-sm ${view.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                    >
                      <div className="flex items-start gap-3">
                        <InitialsAvatar name={d.isAnonymous ? '?' : d.memberName} />
                        <div className="min-w-0 flex-1">
                          <p className="truncate font-semibold text-slate-900">{donorName(d)}</p>
                          <p className="text-xs text-slate-500">{formatDate(d.receivedAtUtc)}</p>
                        </div>
                        <p className="shrink-0 text-lg font-bold text-rose-700">{formatCurrency(d.amount)}</p>
                      </div>
                      <div className="mt-3 flex flex-wrap items-center gap-2">
                        <span className={`text-xs px-2 py-0.5 rounded-full font-medium ${CAUSE_COLOR[d.cause]}`}>{CAUSE_LABELS[d.cause]}</span>
                        {status(d)}
                      </div>
                      {d.message && <p className="mt-2 line-clamp-2 text-sm italic text-slate-500">"{d.message}"</p>}
                      <div className="mt-auto flex items-center justify-end gap-1 pt-3">{donationActions(d)}</div>
                    </StaggerItem>
                  ))}
                </div>
              ) : (
                <RecordTable
                  label="Donations"
                  items={filtered}
                  view={view}
                  getKey={(d) => d.id}
                  columns={donationColumns}
                  actions={donationActions}
                />
              )}
            </ViewTransition>
          )}
        </main>
      </PageTransition>

      <Dialog open={dialogOpen} onOpenChange={setDialogOpen}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Record donation</DialogTitle>
            <DialogDescription>Log a contribution from a member.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Donor</Label>
              <Select value={form.memberId} onValueChange={(v) => setForm({ ...form, memberId: v })}>
                <SelectTrigger aria-label="Donor">
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Cause</Label>
                <Select
                  value={form.cause}
                  onValueChange={(v) => setForm({ ...form, cause: v as DonationCause })}
                >
                  <SelectTrigger aria-label="Cause">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {DONATION_CAUSES.map((c) => (
                      <SelectItem key={c} value={c}>
                        {CAUSE_LABELS[c]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Amount ($)</Label>
                <Input aria-label="Amount ($)"
                  type="number"
                  min="0"
                  step="0.01"
                  value={form.amount}
                  onChange={(e) => setForm({ ...form, amount: e.target.value })}
                />
              </div>
            </div>
            <div className="flex items-center justify-between bg-slate-50 rounded-lg px-3 py-2">
              <div>
                <p className="text-sm font-medium text-slate-900">Anonymous</p>
                <p className="text-xs text-slate-500">Hide the donor name from public lists.</p>
              </div>
              <Switch
                checked={form.anonymous}
                onCheckedChange={(c) => setForm({ ...form, anonymous: c })}
              />
            </div>
            <div>
              <Label>Message</Label>
              <Textarea aria-label="Message"
                value={form.message}
                onChange={(e) => setForm({ ...form, message: e.target.value })}
                rows={2}
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setDialogOpen(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submit} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Recording
                </>
              ) : (
                'Record donation'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

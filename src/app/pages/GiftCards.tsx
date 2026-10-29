import { useEffect, useMemo, useState } from 'react';
import {
  Gift,
  Plus,
  Search,
  CreditCard,
  Copy,
  Check,
  Ban,
  Loader2,
  Sparkles,
  TrendingUp,
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
  Tabs,
  TabsList,
  TabsTrigger,
  TabsContent,
} from '@/app/components/ui/tabs';
import {
  getGiftCards,
  getRedemptions,
  issueGiftCard,
  redeemGiftCard,
  voidGiftCard,
  computeGiftCardStats,
  GIFT_CARD_STATUSES,
  type GiftCard,
  type GiftCardStatus,
  type GiftCardRedemption,
} from '@/app/api/giftCards';
import { getMembers, type Member } from '@/app/api/members';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';

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

export default function GiftCards() {
  const { format: formatCurrency, currency } = useClubMoney();
  const [cards, setCards] = useState<GiftCard[]>([]);
  const [redemptions, setRedemptions] = useState<GiftCardRedemption[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'All' | GiftCardStatus>('All');
  const [issueDialog, setIssueDialog] = useState(false);
  const [redeemDialog, setRedeemDialog] = useState<GiftCard | null>(null);
  const [copiedCode, setCopiedCode] = useState<string | null>(null);

  const [issueForm, setIssueForm] = useState({
    purchaserMemberId: '',
    purchaserName: '',
    recipientName: '',
    recipientEmail: '',
    faceValue: '100',
    expiresAtUtc: '',
    message: '',
  });
  const [redeemForm, setRedeemForm] = useState({
    amount: '',
    redeemedByName: '',
    appliedTo: '',
  });
  const [saving, setSaving] = useState(false);
  const [voidingId, setVoidingId] = useState<string | null>(null);

  const load = async () => {
    try {
      const [c, r, m] = await Promise.all([getGiftCards(), getRedemptions(), getMembers()]);
      setCards(c);
      setRedemptions(r);
      setMembers(m);
    } catch {
      toast.error('Failed to load gift cards');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeGiftCardStats(cards, redemptions), [cards, redemptions]);

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return cards.filter((c) => {
      if (statusFilter !== 'All' && c.status !== statusFilter) return false;
      if (!q) return true;
      return `${c.code} ${c.recipientName} ${c.purchaserName} ${c.recipientEmail ?? ''}`
        .toLowerCase()
        .includes(q);
    });
  }, [cards, search, statusFilter]);

  const openIssueDialog = () => {
    setIssueForm({
      purchaserMemberId: '',
      purchaserName: '',
      recipientName: '',
      recipientEmail: '',
      faceValue: '100',
      expiresAtUtc: '',
      message: '',
    });
    setIssueDialog(true);
  };

  const issue = async () => {
    if (!issueForm.recipientName.trim()) {
      toast.error('Recipient name is required');
      return;
    }
    const faceValue = Number(issueForm.faceValue);
    if (!faceValue || faceValue <= 0) {
      toast.error('Face value must be positive');
      return;
    }
    let purchaserName = issueForm.purchaserName.trim();
    if (issueForm.purchaserMemberId) {
      const member = members.find((m) => m.id === issueForm.purchaserMemberId);
      if (member) purchaserName = `${member.firstName} ${member.lastName}`;
    }
    if (!purchaserName) purchaserName = 'Walk-in purchaser';

    setSaving(true);
    try {
      const card = await issueGiftCard({
        purchaserMemberId: issueForm.purchaserMemberId || undefined,
        purchaserName,
        recipientName: issueForm.recipientName.trim(),
        recipientEmail: issueForm.recipientEmail.trim() || undefined,
        faceValue,
        expiresAtUtc: issueForm.expiresAtUtc
          ? new Date(issueForm.expiresAtUtc).toISOString()
          : undefined,
        message: issueForm.message.trim() || undefined,
      });
      if (card) toast.success(`Gift card issued — ${card.code}`);
      setIssueDialog(false);
      await load();
    } catch {
      toast.error('Failed to issue gift card');
    } finally {
      setSaving(false);
    }
  };

  const openRedeemDialog = (c: GiftCard) => {
    setRedeemForm({
      amount: c.remainingValue.toFixed(2),
      redeemedByName: c.recipientName,
      appliedTo: '',
    });
    setRedeemDialog(c);
  };

  const redeem = async () => {
    if (!redeemDialog) return;
    const amount = Number(redeemForm.amount);
    if (!amount || amount <= 0) {
      toast.error('Amount must be positive');
      return;
    }
    setSaving(true);
    try {
      const result = await redeemGiftCard({
        giftCardId: redeemDialog.id,
        amount,
        redeemedByName: redeemForm.redeemedByName.trim() || redeemDialog.recipientName,
        appliedTo: redeemForm.appliedTo.trim() || undefined,
      });
      if (result) {
        toast.success(`Redeemed ${formatCurrency(result.amount)}`);
      } else {
        toast.error('Could not redeem — card is not active or amount exceeds balance');
      }
      setRedeemDialog(null);
      await load();
    } catch {
      toast.error('Failed to redeem');
    } finally {
      setSaving(false);
    }
  };

  const doVoid = async (c: GiftCard) => {
    if (!confirm(`Void gift card ${c.code}? The remaining balance becomes unusable.`)) return;
    setVoidingId(c.id);
    try {
      const ok = await voidGiftCard(c.id);
      if (ok) {
        toast.success('Gift card voided');
        await load();
      }
    } finally {
      setVoidingId(null);
    }
  };

  const copyCode = async (code: string) => {
    try {
      await navigator.clipboard.writeText(code);
      setCopiedCode(code);
      setTimeout(() => setCopiedCode(null), 1500);
    } catch {
      // ignore
    }
  };

  // How this person likes gift cards and redemptions shown, saved for them.
  const view = useViewPreference('giftcards', { layouts: ['grid', 'list', 'table'] });
  const redemptionView = useViewPreference('giftcards.redemptions', { layouts: ['table', 'list', 'grid'] });

  const codeChip = (c: GiftCard) => (
    <button
      type="button"
      onClick={() => copyCode(c.code)}
      className="inline-flex items-center gap-1 rounded-md bg-slate-100 px-2 py-0.5 font-mono text-xs text-slate-700 transition hover:bg-slate-200"
      aria-label={`Copy code ${c.code}`}
    >
      {c.code}
      {copiedCode === c.code ? <Check className="w-3 h-3" /> : <Copy className="w-3 h-3" />}
    </button>
  );

  const statusBadge = (status: GiftCardStatus) => (
    <span
      className={`text-xs px-2 py-0.5 rounded-full font-medium ${
        status === 'Active' ? 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200' : 'bg-slate-100 text-slate-600 ring-1 ring-slate-200'
      }`}
    >
      {status}
    </span>
  );

  /** Redeem and void, the same on a row and a table line as on the card. */
  const cardActions = (c: GiftCard) =>
    c.status === 'Active' ? (
      <>
        <Button size="sm" variant="outline" onClick={() => openRedeemDialog(c)}>
          <CreditCard className="w-3.5 h-3.5 mr-1" /> Redeem
        </Button>
        <Button
          size="sm"
          variant="outline"
          onClick={() => doVoid(c)}
          disabled={voidingId === c.id}
          className="text-rose-600 hover:bg-rose-50"
          aria-label={`Void gift card ${c.code}`}
        >
          {voidingId === c.id ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Ban className="w-3.5 h-3.5" />}
        </Button>
      </>
    ) : null;

  const cardColumns: RecordColumn<GiftCard>[] = [
    {
      id: 'recipient',
      label: 'Recipient',
      required: true,
      cell: (c) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={c.recipientName} size="sm" />
          <span className="truncate">{c.recipientName}</span>
        </span>
      ),
    },
    { id: 'code', label: 'Code', cell: codeChip },
    { id: 'status', label: 'Status', cell: (c) => statusBadge(c.status) },
    { id: 'remaining', label: 'Balance', numeric: true, cell: (c) => <span className="font-semibold text-rose-700">{formatCurrency(c.remainingValue)}</span> },
    { id: 'face', label: 'Face value', numeric: true, cell: (c) => formatCurrency(c.faceValue) },
    { id: 'purchaser', label: 'From', cell: (c) => c.purchaserName },
    { id: 'issued', label: 'Issued', cell: (c) => <span className="whitespace-nowrap">{formatDate(c.issuedAtUtc)}</span> },
    { id: 'expires', label: 'Expires', byDefault: false, cell: (c) => (c.expiresAtUtc ? formatDate(c.expiresAtUtc) : '—') },
    { id: 'email', label: 'Recipient email', byDefault: false, cell: (c) => c.recipientEmail || '—' },
  ];

  const redemptionColumns: RecordColumn<GiftCardRedemption>[] = [
    { id: 'redeemedBy', label: 'Redeemed by', required: true, cell: (r) => r.redeemedByName },
    { id: 'code', label: 'Code', cell: (r) => <span className="font-mono text-xs text-slate-700">{r.giftCardCode}</span> },
    { id: 'appliedTo', label: 'Applied to', cell: (r) => r.appliedTo ?? '—' },
    { id: 'amount', label: 'Amount', numeric: true, cell: (r) => <span className="font-semibold text-rose-600">-{formatCurrency(r.amount)}</span> },
    { id: 'when', label: 'When', cell: (r) => <span className="whitespace-nowrap">{formatDate(r.redeemedAtUtc)}</span> },
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
            icon={Gift}
            tone="rose"
            title="Gift Cards"
            subtitle="Issue, track, and redeem house gift cards."
            actions={
              <Button onClick={openIssueDialog} className={heroPrimary('rose')}>
                <Plus className="h-4 w-4" />
                Issue gift card
              </Button>
            }
            stats={[
              { label: 'Total issued', value: stats.totalIssued, icon: TrendingUp, tone: 'rose', hint: 'all-time face value', format: formatCurrency },
              { label: 'Outstanding', value: stats.outstandingBalance, icon: Sparkles, tone: 'amber', hint: `${stats.activeCount} active cards`, format: formatCurrency },
              { label: 'Redeemed', value: stats.redeemedAllTime, icon: CreditCard, tone: 'emerald', hint: 'all-time redemptions', format: formatCurrency },
              { label: 'Cards', value: cards.length, icon: Gift, tone: 'violet', hint: 'issued cards on file' },
            ]}
          />

          <Tabs defaultValue="cards">
            <TabsList className="mb-4">
              <TabsTrigger value="cards">Cards ({cards.length})</TabsTrigger>
              <TabsTrigger value="redemptions">Redemptions ({redemptions.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="cards">
              <div className="flex flex-col md:flex-row md:flex-wrap md:items-center gap-3 mb-6">
                <div className="relative flex-1 max-w-md">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
                  <Input
                    value={search}
                    onChange={(e) => setSearch(e.target.value)}
                    placeholder="Search by code, recipient, purchaser..."
                    className="pl-9 h-11 rounded-xl"
                  />
                </div>
                <Select
                  value={statusFilter}
                  onValueChange={(v) => setStatusFilter(v as 'All' | GiftCardStatus)}
                >
                  <SelectTrigger aria-label="Filter by status" className="h-11 rounded-xl md:w-44">
                    <SelectValue placeholder="Status" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="All">All statuses</SelectItem>
                    {GIFT_CARD_STATUSES.map((s) => (
                      <SelectItem key={s} value={s}>
                        {s}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
                <ViewSwitcher view={view} columns={columnChoices(cardColumns)} className="md:ml-auto" />
              </div>

              {loading ? (
                <div className="flex items-center justify-center py-20 text-slate-500">
                  <Loader2 className="h-6 w-6 animate-spin" />
                </div>
              ) : filtered.length === 0 ? (
                <EmptyState
                  icon={Gift}
                  title={cards.length === 0 ? 'No gift cards yet' : 'No cards match these filters'}
                  description={
                    cards.length === 0
                      ? 'Issue your first gift card — members love them.'
                      : 'Try clearing filters or adjusting your search.'
                  }
                  actionLabel={cards.length === 0 ? 'Issue gift card' : undefined}
                  onAction={cards.length === 0 ? openIssueDialog : undefined}
                  variant="orange"
                />
              ) : (
                <ViewTransition layout={view.layout}>
                {view.layout === 'list' ? (
                  <RecordList
                    label="Gift cards"
                    items={filtered}
                    view={view}
                    getKey={(c) => c.id}
                    leading={(c) => <InitialsAvatar name={c.recipientName} />}
                    title={(c) => c.recipientName}
                    subtitle={(c) => `${formatCurrency(c.remainingValue)} of ${formatCurrency(c.faceValue)} · from ${c.purchaserName}`}
                    columns={cardColumns}
                    actions={cardActions}
                  />
                ) : view.layout === 'table' ? (
                  <RecordTable
                    label="Gift cards"
                    items={filtered}
                    view={view}
                    getKey={(c) => c.id}
                    columns={cardColumns}
                    actions={cardActions}
                  />
                ) : (
                <div className={`grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-4')}`}>
                  {filtered.map((c, index) => {
                    const used = c.faceValue - c.remainingValue;
                    const usedPct = c.faceValue > 0 ? (used / c.faceValue) * 100 : 0;
                    return (
                      <StaggerItem
                        as="article"
                        key={c.id}
                        index={index}
                        className={`rounded-2xl border border-slate-200 bg-white shadow-sm hover:border-rose-200 overflow-hidden ${liftOnHover}`}
                      >
                        <div className={`relative bg-gradient-to-br from-rose-500 via-pink-500 to-fuchsia-500 text-white ${view.density === 'compact' ? 'p-4' : 'p-5'}`}>
                          <div className="absolute top-3 right-3">
                            <span className={`text-xs px-2 py-0.5 rounded-full font-medium bg-white/20 backdrop-blur-sm`}>
                              {c.status}
                            </span>
                          </div>
                          <p className="text-xs uppercase tracking-wide opacity-80 mb-1">House Gift Card</p>
                          <p className="text-2xl font-bold mb-3">{formatCurrency(c.remainingValue)}</p>
                          <p className="text-xs opacity-80">of {formatCurrency(c.faceValue)} face value</p>
                          <button
                            type="button"
                            onClick={() => copyCode(c.code)}
                            className="mt-3 inline-flex items-center gap-1 text-xs font-mono bg-white/10 backdrop-blur-sm hover:bg-white/20 rounded-md px-2 py-1 transition"
                          >
                            {c.code}
                            {copiedCode === c.code ? (
                              <Check className="w-3 h-3" />
                            ) : (
                              <Copy className="w-3 h-3" />
                            )}
                          </button>
                        </div>
                        <div className={view.density === 'compact' ? 'p-3' : 'p-4'}>
                          <p className="text-xs text-slate-500 mb-1">Recipient</p>
                          <p className="font-semibold text-slate-900">{c.recipientName}</p>
                          {c.recipientEmail && (
                            <p className="text-xs text-slate-500">{c.recipientEmail}</p>
                          )}
                          <p className="text-xs text-slate-500 mt-3">
                            From <span className="font-medium text-slate-700">{c.purchaserName}</span>
                          </p>
                          {c.message && (
                            <p className="text-xs italic text-slate-600 bg-slate-50 rounded p-2 mt-2 line-clamp-2">
                              "{c.message}"
                            </p>
                          )}
                          <p className="text-xs text-slate-500 mt-3">
                            Issued {formatDate(c.issuedAtUtc)}
                            {c.expiresAtUtc && <> · Expires {formatDate(c.expiresAtUtc)}</>}
                          </p>
                          {usedPct > 0 && (
                            <div className="mt-3">
                              <div className="h-1 bg-slate-100 rounded-full overflow-hidden">
                                <div
                                  className="h-full bg-rose-500"
                                  style={{ width: `${usedPct}%` }}
                                />
                              </div>
                            </div>
                          )}
                          <div className="flex gap-2 mt-4">
                            {c.status === 'Active' && (
                              <>
                                <Button size="sm" variant="outline" onClick={() => openRedeemDialog(c)} className="flex-1">
                                  <CreditCard className="w-3.5 h-3.5 mr-1" /> Redeem
                                </Button>
                                <Button
                                  size="sm"
                                  variant="outline"
                                  onClick={() => doVoid(c)}
                                  disabled={voidingId === c.id}
                                  className="text-rose-600 hover:bg-rose-50"
                                  aria-label={`Void gift card ${c.code}`}
                                >
                                  {voidingId === c.id ? (
                                    <Loader2 className="w-3.5 h-3.5 animate-spin" />
                                  ) : (
                                    <Ban className="w-3.5 h-3.5" />
                                  )}
                                </Button>
                              </>
                            )}
                          </div>
                        </div>
                      </StaggerItem>
                    );
                  })}
                </div>
                )}
                </ViewTransition>
              )}
            </TabsContent>

            <TabsContent value="redemptions">
              {redemptions.length === 0 ? (
                <EmptyState
                  icon={CreditCard}
                  title="No redemptions yet"
                  description="Gift card redemptions appear here once members start using them."
                />
              ) : (
                <>
                  <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
                    <p className="text-sm text-slate-500">
                      {redemptions.length} {redemptions.length === 1 ? 'redemption' : 'redemptions'}
                    </p>
                    <ViewSwitcher view={redemptionView} columns={columnChoices(redemptionColumns)} />
                  </div>
                  <ViewTransition layout={redemptionView.layout}>
                    {redemptionView.layout === 'list' ? (
                      <RecordList
                        label="Redemptions"
                        items={redemptions}
                        view={redemptionView}
                        getKey={(r) => r.id}
                        leading={(r) => <InitialsAvatar name={r.redeemedByName} />}
                        title={(r) => r.redeemedByName}
                        subtitle={(r) => `-${formatCurrency(r.amount)}${r.appliedTo ? ` · ${r.appliedTo}` : ''}`}
                        columns={redemptionColumns}
                      />
                    ) : redemptionView.layout === 'grid' ? (
                      <div className={`grid sm:grid-cols-2 lg:grid-cols-3 ${gridDensity(redemptionView, 'gap-4', 'gap-3 xl:grid-cols-4')}`}>
                        {redemptions.map((r, index) => (
                          <StaggerItem
                            key={r.id}
                            index={index}
                            className={`rounded-2xl border border-slate-200/80 bg-white shadow-sm ${redemptionView.density === 'compact' ? 'p-3' : 'p-4'} ${liftOnHover}`}
                          >
                            <div className="flex items-start gap-3">
                              <InitialsAvatar name={r.redeemedByName} />
                              <div className="min-w-0 flex-1">
                                <p className="truncate font-semibold text-slate-900">{r.redeemedByName}</p>
                                <p className="font-mono text-xs text-slate-500">{r.giftCardCode}</p>
                              </div>
                              <p className="shrink-0 font-bold text-rose-600">-{formatCurrency(r.amount)}</p>
                            </div>
                            <p className="mt-3 text-xs text-slate-500">
                              {r.appliedTo ? `${r.appliedTo} · ` : ''}{formatDate(r.redeemedAtUtc)}
                            </p>
                          </StaggerItem>
                        ))}
                      </div>
                    ) : (
                      <RecordTable
                        label="Redemptions"
                        items={redemptions}
                        view={redemptionView}
                        getKey={(r) => r.id}
                        columns={redemptionColumns}
                      />
                    )}
                  </ViewTransition>
                </>
              )}
            </TabsContent>
          </Tabs>
        </main>
      </PageTransition>

      {/* Issue dialog */}
      <Dialog open={issueDialog} onOpenChange={setIssueDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Issue gift card</DialogTitle>
            <DialogDescription>
              Issue a new gift card. A unique code is generated automatically.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Purchaser (optional)</Label>
              <Select
                value={issueForm.purchaserMemberId}
                onValueChange={(v) => setIssueForm({ ...issueForm, purchaserMemberId: v === 'none' ? '' : v })}
              >
                <SelectTrigger aria-label="Purchaser (optional)">
                  <SelectValue placeholder="Existing member or walk-in" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="none">Walk-in (no member)</SelectItem>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            {!issueForm.purchaserMemberId && (
              <div>
                <Label>Purchaser name</Label>
                <Input aria-label="Purchaser name"
                  value={issueForm.purchaserName}
                  onChange={(e) => setIssueForm({ ...issueForm, purchaserName: e.target.value })}
                  placeholder="Walk-in purchaser"
                />
              </div>
            )}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Recipient name</Label>
                <Input aria-label="Recipient name"
                  value={issueForm.recipientName}
                  onChange={(e) => setIssueForm({ ...issueForm, recipientName: e.target.value })}
                />
              </div>
              <div>
                <Label>Recipient email</Label>
                <Input aria-label="Recipient email"
                  type="email"
                  value={issueForm.recipientEmail}
                  onChange={(e) => setIssueForm({ ...issueForm, recipientEmail: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Face value ({currency})</Label>
                <Input aria-label={`Face value (${currency})`}
                  type="number"
                  min="0"
                  step="0.01"
                  value={issueForm.faceValue}
                  onChange={(e) => setIssueForm({ ...issueForm, faceValue: e.target.value })}
                />
              </div>
              <div>
                <Label>Expires</Label>
                <Input aria-label="Expires"
                  type="date"
                  value={issueForm.expiresAtUtc}
                  onChange={(e) => setIssueForm({ ...issueForm, expiresAtUtc: e.target.value })}
                />
              </div>
            </div>
            <div>
              <Label>Message</Label>
              <Textarea aria-label="Message"
                value={issueForm.message}
                onChange={(e) => setIssueForm({ ...issueForm, message: e.target.value })}
                rows={2}
                placeholder="Optional message to the recipient..."
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setIssueDialog(false)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={issue} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Issuing
                </>
              ) : (
                'Issue gift card'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Redeem dialog */}
      <Dialog open={redeemDialog !== null} onOpenChange={(o) => !o && setRedeemDialog(null)}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Redeem gift card</DialogTitle>
            <DialogDescription>
              {redeemDialog && (
                <>
                  Card <span className="font-mono">{redeemDialog.code}</span> — balance{' '}
                  {formatCurrency(redeemDialog.remainingValue)}
                </>
              )}
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Amount ({currency})</Label>
              <Input aria-label={`Amount (${currency})`}
                type="number"
                min="0"
                step="0.01"
                max={redeemDialog?.remainingValue}
                value={redeemForm.amount}
                onChange={(e) => setRedeemForm({ ...redeemForm, amount: e.target.value })}
              />
            </div>
            <div>
              <Label>Redeemed by</Label>
              <Input aria-label="Redeemed by"
                value={redeemForm.redeemedByName}
                onChange={(e) => setRedeemForm({ ...redeemForm, redeemedByName: e.target.value })}
              />
            </div>
            <div>
              <Label>Applied to</Label>
              <Input aria-label="Applied to"
                value={redeemForm.appliedTo}
                onChange={(e) => setRedeemForm({ ...redeemForm, appliedTo: e.target.value })}
                placeholder="e.g. Library Bar tab"
              />
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setRedeemDialog(null)} disabled={saving}>
              Cancel
            </Button>
            <Button onClick={redeem} disabled={saving}>
              {saving ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Redeeming
                </>
              ) : (
                'Redeem'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

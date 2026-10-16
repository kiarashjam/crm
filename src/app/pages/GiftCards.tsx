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

export default function GiftCards() {
  const [cards, setCards] = useState<GiftCard[]>([]);
  const [redemptions, setRedemptions] = useState<GiftCardRedemption[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-rose-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-pink-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-rose-500 to-pink-500 flex items-center justify-center shadow-2xl shadow-rose-500/30">
                    <Gift className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Gift Cards</h1>
                    <p className="text-slate-400 mt-1">
                      Issue, track, and redeem house gift cards.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={openIssueDialog}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-rose-500 to-pink-500 hover:from-rose-600 hover:to-pink-600 shadow-lg shadow-rose-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" />
                  Issue gift card
                </Button>
              </div>
            </div>
          </div>

          {/* Stats */}
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard
              icon={<TrendingUp className="w-5 h-5 text-rose-600" />}
              label="Total issued"
              value={formatCurrency(stats.totalIssued)}
              hint="All-time face value"
            />
            <StatCard
              icon={<Sparkles className="w-5 h-5 text-amber-600" />}
              label="Outstanding"
              value={formatCurrency(stats.outstandingBalance)}
              hint={`${stats.activeCount} active cards`}
            />
            <StatCard
              icon={<CreditCard className="w-5 h-5 text-emerald-600" />}
              label="Redeemed"
              value={formatCurrency(stats.redeemedAllTime)}
              hint="All-time redemptions"
            />
            <StatCard
              icon={<Gift className="w-5 h-5 text-violet-600" />}
              label="Cards"
              value={String(cards.length)}
              hint="Issued cards on file"
            />
          </div>

          <Tabs defaultValue="cards">
            <TabsList className="mb-4">
              <TabsTrigger value="cards">Cards ({cards.length})</TabsTrigger>
              <TabsTrigger value="redemptions">Redemptions ({redemptions.length})</TabsTrigger>
            </TabsList>

            <TabsContent value="cards">
              <div className="flex flex-col md:flex-row gap-3 mb-6">
                <div className="relative flex-1 max-w-md">
                  <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
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
                  <SelectTrigger className="h-11 rounded-xl md:w-44">
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
              </div>

              {loading ? (
                <div className="flex items-center justify-center py-20 text-slate-400">
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
                <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-4">
                  {filtered.map((c) => {
                    const used = c.faceValue - c.remainingValue;
                    const usedPct = c.faceValue > 0 ? (used / c.faceValue) * 100 : 0;
                    return (
                      <article
                        key={c.id}
                        className="rounded-2xl border border-slate-200 bg-white shadow-sm hover:shadow-md transition-shadow overflow-hidden"
                      >
                        <div className="relative bg-gradient-to-br from-rose-500 via-pink-500 to-fuchsia-500 p-5 text-white">
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
                        <div className="p-4">
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
                          <p className="text-xs text-slate-400 mt-3">
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
                      </article>
                    );
                  })}
                </div>
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
                <div className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden">
                  <table className="w-full text-sm">
                    <thead className="bg-slate-50 border-b border-slate-200">
                      <tr className="text-left text-xs uppercase tracking-wide text-slate-500">
                        <th className="px-4 py-3 font-medium">Code</th>
                        <th className="px-4 py-3 font-medium">Redeemed by</th>
                        <th className="px-4 py-3 font-medium">Applied to</th>
                        <th className="px-4 py-3 font-medium text-right">Amount</th>
                        <th className="px-4 py-3 font-medium">When</th>
                      </tr>
                    </thead>
                    <tbody>
                      {redemptions.map((r) => (
                        <tr key={r.id} className="border-b border-slate-100 hover:bg-slate-50">
                          <td className="px-4 py-3 font-mono text-xs text-slate-700">{r.giftCardCode}</td>
                          <td className="px-4 py-3 text-slate-700">{r.redeemedByName}</td>
                          <td className="px-4 py-3 text-slate-500">{r.appliedTo ?? '—'}</td>
                          <td className="px-4 py-3 text-right font-semibold text-rose-600">
                            -{formatCurrency(r.amount)}
                          </td>
                          <td className="px-4 py-3 text-slate-500 whitespace-nowrap">
                            {formatDate(r.redeemedAtUtc)}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
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
                <SelectTrigger>
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
                <Input
                  value={issueForm.purchaserName}
                  onChange={(e) => setIssueForm({ ...issueForm, purchaserName: e.target.value })}
                  placeholder="Walk-in purchaser"
                />
              </div>
            )}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Recipient name</Label>
                <Input
                  value={issueForm.recipientName}
                  onChange={(e) => setIssueForm({ ...issueForm, recipientName: e.target.value })}
                />
              </div>
              <div>
                <Label>Recipient email</Label>
                <Input
                  type="email"
                  value={issueForm.recipientEmail}
                  onChange={(e) => setIssueForm({ ...issueForm, recipientEmail: e.target.value })}
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Face value ($)</Label>
                <Input
                  type="number"
                  min="0"
                  step="0.01"
                  value={issueForm.faceValue}
                  onChange={(e) => setIssueForm({ ...issueForm, faceValue: e.target.value })}
                />
              </div>
              <div>
                <Label>Expires</Label>
                <Input
                  type="date"
                  value={issueForm.expiresAtUtc}
                  onChange={(e) => setIssueForm({ ...issueForm, expiresAtUtc: e.target.value })}
                />
              </div>
            </div>
            <div>
              <Label>Message</Label>
              <Textarea
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
              <Label>Amount ($)</Label>
              <Input
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
              <Input
                value={redeemForm.redeemedByName}
                onChange={(e) => setRedeemForm({ ...redeemForm, redeemedByName: e.target.value })}
              />
            </div>
            <div>
              <Label>Applied to</Label>
              <Input
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

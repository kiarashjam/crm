import { useEffect, useMemo, useState } from 'react';
import {
  Wine,
  Plus,
  X,
  Receipt,
  DollarSign,
  Clock,
  Sparkles,
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
  getTabs,
  openTab,
  addItemToTab,
  removeItemFromTab,
  closeTab,
  voidTab,
  computeTabStats,
  TAB_VENUES,
  VENUE_LABELS,
  type OpenTab,
  type TabVenue,
} from '@/app/api/tabs';
import { getMembers, type Member } from '@/app/api/members';

function formatCurrency(n: number): string {
  return n.toLocaleString(undefined, { style: 'currency', currency: 'USD' });
}

function timeAgo(iso: string): string {
  const diff = Date.now() - Date.parse(iso);
  const min = Math.floor(diff / 60_000);
  if (min < 60) return `${min}m`;
  const h = Math.floor(min / 60);
  return `${h}h ${min % 60}m`;
}

export default function Tabs() {
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [openDialog, setOpenDialog] = useState(false);
  const [addingTo, setAddingTo] = useState<string | null>(null);

  const [openForm, setOpenForm] = useState({
    memberId: '',
    venue: 'Bar' as TabVenue,
    serverName: '',
  });
  const [itemForm, setItemForm] = useState({ name: '', quantity: '1', unitPrice: '' });
  const [busy, setBusy] = useState(false);

  const load = async () => {
    try {
      const [t, m] = await Promise.all([getTabs(), getMembers()]);
      setTabs(t);
      setMembers(m);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void load();
  }, []);

  const stats = useMemo(() => computeTabStats(tabs), [tabs]);
  const openTabs = useMemo(() => tabs.filter((t) => t.status === 'Open'), [tabs]);
  const recentClosed = useMemo(
    () => tabs.filter((t) => t.status === 'Closed').slice(0, 5),
    [tabs],
  );

  const submitOpen = async () => {
    if (!openForm.memberId) {
      toast.error('Pick a member');
      return;
    }
    setBusy(true);
    try {
      await openTab({
        memberId: openForm.memberId,
        venue: openForm.venue,
        serverName: openForm.serverName.trim() || undefined,
      });
      toast.success('Tab opened');
      setOpenDialog(false);
      await load();
    } finally {
      setBusy(false);
    }
  };

  const submitItem = async () => {
    if (!addingTo) return;
    if (!itemForm.name.trim()) {
      toast.error('Item name is required');
      return;
    }
    const qty = Number(itemForm.quantity) || 1;
    const price = Number(itemForm.unitPrice);
    if (!price || price <= 0) {
      toast.error('Price is required');
      return;
    }
    setBusy(true);
    try {
      await addItemToTab({
        tabId: addingTo,
        name: itemForm.name.trim(),
        quantity: qty,
        unitPrice: price,
      });
      setItemForm({ name: '', quantity: '1', unitPrice: '' });
      await load();
    } finally {
      setBusy(false);
    }
  };

  const removeItem = async (tabId: string, itemId: string) => {
    await removeItemFromTab(tabId, itemId);
    await load();
  };

  const close = async (t: OpenTab) => {
    if (!confirm(`Close tab? ${formatCurrency(t.total)} will post to ${t.memberName}'s house account.`)) return;
    const result = await closeTab(t.id);
    if (result) {
      toast.success(
        result.pointsAwarded
          ? `Tab closed · +${result.pointsAwarded} loyalty points`
          : 'Tab closed',
      );
      setAddingTo(null);
      await load();
    }
  };

  const doVoid = async (t: OpenTab) => {
    if (!confirm('Void this tab? No charge will post.')) return;
    await voidTab(t.id);
    toast.success('Tab voided');
    setAddingTo(null);
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
              <div className="absolute -top-20 -right-20 w-80 h-80 bg-amber-500/20 rounded-full blur-3xl animate-pulse" />
              <div className="absolute -bottom-20 -left-20 w-60 h-60 bg-orange-500/15 rounded-full blur-3xl" />
            </div>
            <div className="relative px-6 lg:px-8 py-8 lg:py-10">
              <div className="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-6">
                <div className="flex items-center gap-5">
                  <div className="w-16 h-16 rounded-2xl bg-gradient-to-br from-amber-500 to-orange-500 flex items-center justify-center shadow-2xl shadow-amber-500/30">
                    <Wine className="w-8 h-8 text-white" />
                  </div>
                  <div>
                    <h1 className="text-2xl lg:text-3xl font-bold text-white tracking-tight">Open Tabs</h1>
                    <p className="text-slate-400 mt-1">
                      Running checks at the bar and restaurant. Closing a tab posts a charge and awards points.
                    </p>
                  </div>
                </div>
                <Button
                  onClick={() => {
                    setOpenForm({ memberId: '', venue: 'Bar', serverName: '' });
                    setOpenDialog(true);
                  }}
                  className="inline-flex items-center gap-2 h-10 px-5 rounded-xl bg-gradient-to-r from-amber-500 to-orange-500 hover:from-amber-600 hover:to-orange-600 shadow-lg shadow-amber-500/30 font-semibold text-white"
                >
                  <Plus className="w-4 h-4" /> Open tab
                </Button>
              </div>
            </div>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
            <StatCard icon={<Wine className="w-5 h-5 text-amber-600" />} label="Open tabs" value={String(stats.openCount)} />
            <StatCard icon={<DollarSign className="w-5 h-5 text-rose-600" />} label="Open value" value={formatCurrency(stats.openValue)} />
            <StatCard icon={<Receipt className="w-5 h-5 text-emerald-600" />} label="Closed today" value={String(stats.closedToday)} />
            <StatCard icon={<DollarSign className="w-5 h-5 text-blue-600" />} label="Revenue today" value={formatCurrency(stats.revenueToday)} />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-400">
              <Loader2 className="h-6 w-6 animate-spin" />
            </div>
          ) : openTabs.length === 0 ? (
            <EmptyState
              icon={Wine}
              title="No open tabs"
              description="Open a tab when a member sits down — items add to the running total."
              actionLabel="Open tab"
              onAction={() => setOpenDialog(true)}
              variant="orange"
            />
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-4 mb-8">
              {openTabs.map((t) => (
                <article
                  key={t.id}
                  className="bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden"
                >
                  <div className="bg-gradient-to-r from-amber-500/10 to-orange-500/10 px-5 py-3 flex items-center justify-between border-b border-slate-100">
                    <div>
                      <p className="font-semibold text-slate-900">{t.memberName}</p>
                      <p className="text-xs text-slate-500">
                        {VENUE_LABELS[t.venue]}
                        {t.serverName && <> · {t.serverName}</>} · open {timeAgo(t.openedAtUtc)}
                      </p>
                    </div>
                    <span className="text-xs px-2 py-0.5 rounded-full bg-amber-100 text-amber-800 font-medium">
                      {t.memberTier}
                    </span>
                  </div>
                  <div className="px-5 py-3">
                    {t.items.length === 0 ? (
                      <p className="text-sm text-slate-400 py-3">No items yet.</p>
                    ) : (
                      <ul className="space-y-1.5">
                        {t.items.map((i) => (
                          <li key={i.id} className="flex items-center justify-between gap-2 text-sm">
                            <div className="min-w-0 flex-1">
                              <span className="text-slate-700">
                                {i.quantity > 1 && (
                                  <span className="text-slate-400">{i.quantity}× </span>
                                )}
                                {i.name}
                              </span>
                            </div>
                            <span className="font-medium text-slate-900">
                              {formatCurrency(i.quantity * i.unitPrice)}
                            </span>
                            <button
                              type="button"
                              onClick={() => removeItem(t.id, i.id)}
                              className="rounded p-0.5 text-slate-400 hover:text-rose-600"
                              aria-label="Remove item"
                            >
                              <X className="w-3.5 h-3.5" />
                            </button>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                  <div className="px-5 py-3 bg-slate-50 border-t border-slate-100">
                    {addingTo === t.id ? (
                      <div className="space-y-2">
                        <div className="grid grid-cols-2 gap-2">
                          <Input
                            placeholder="Item name"
                            value={itemForm.name}
                            onChange={(e) => setItemForm({ ...itemForm, name: e.target.value })}
                          />
                          <div className="flex gap-2">
                            <Input
                              placeholder="Qty"
                              type="number"
                              min="1"
                              value={itemForm.quantity}
                              onChange={(e) => setItemForm({ ...itemForm, quantity: e.target.value })}
                              className="w-16"
                            />
                            <Input
                              placeholder="Price"
                              type="number"
                              min="0"
                              step="0.01"
                              value={itemForm.unitPrice}
                              onChange={(e) => setItemForm({ ...itemForm, unitPrice: e.target.value })}
                            />
                          </div>
                        </div>
                        <div className="flex gap-2">
                          <Button size="sm" onClick={submitItem} disabled={busy} className="flex-1">
                            Add
                          </Button>
                          <Button size="sm" variant="outline" onClick={() => setAddingTo(null)}>
                            Done
                          </Button>
                        </div>
                      </div>
                    ) : (
                      <div className="flex items-center justify-between gap-2">
                        <div>
                          <p className="text-xs text-slate-500 uppercase tracking-wide">Running total</p>
                          <p className="text-xl font-bold text-slate-900">{formatCurrency(t.total)}</p>
                        </div>
                        <div className="flex gap-2">
                          <Button size="sm" variant="outline" onClick={() => setAddingTo(t.id)}>
                            <Plus className="w-3.5 h-3.5 mr-1" /> Item
                          </Button>
                          <Button
                            size="sm"
                            onClick={() => close(t)}
                            className="bg-emerald-600 hover:bg-emerald-700"
                          >
                            Close + post
                          </Button>
                          <Button
                            size="sm"
                            variant="outline"
                            onClick={() => doVoid(t)}
                            className="text-rose-600 hover:bg-rose-50"
                          >
                            Void
                          </Button>
                        </div>
                      </div>
                    )}
                  </div>
                </article>
              ))}
            </div>
          )}

          {recentClosed.length > 0 && (
            <section>
              <h2 className="font-semibold text-slate-900 mb-3 inline-flex items-center gap-2">
                <Clock className="w-4 h-4" /> Recently closed
              </h2>
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                {recentClosed.map((t) => (
                  <div key={t.id} className="px-4 py-3 flex items-center justify-between">
                    <div>
                      <p className="text-sm font-medium text-slate-900">{t.memberName}</p>
                      <p className="text-xs text-slate-500">
                        {VENUE_LABELS[t.venue]} · {t.items.length} items
                        {t.closedAtUtc && <> · closed {timeAgo(t.closedAtUtc)} ago</>}
                      </p>
                    </div>
                    <div className="text-right">
                      <p className="font-semibold text-slate-900">{formatCurrency(t.total)}</p>
                      {t.pointsAwarded ? (
                        <p className="text-xs text-amber-600 inline-flex items-center gap-1">
                          <Sparkles className="w-3 h-3" /> +{t.pointsAwarded} pts
                        </p>
                      ) : null}
                    </div>
                  </div>
                ))}
              </div>
            </section>
          )}
        </main>
      </PageTransition>

      <Dialog open={openDialog} onOpenChange={setOpenDialog}>
        <DialogContent className="max-w-lg">
          <DialogHeader>
            <DialogTitle>Open tab</DialogTitle>
            <DialogDescription>Start a running check for a member.</DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-2">
            <div>
              <Label>Member</Label>
              <Select
                value={openForm.memberId}
                onValueChange={(v) => setOpenForm({ ...openForm, memberId: v })}
              >
                <SelectTrigger>
                  <SelectValue placeholder="Pick a member" />
                </SelectTrigger>
                <SelectContent>
                  {members.map((m) => (
                    <SelectItem key={m.id} value={m.id}>
                      {m.firstName} {m.lastName} — {m.tier}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <Label>Venue</Label>
                <Select
                  value={openForm.venue}
                  onValueChange={(v) => setOpenForm({ ...openForm, venue: v as TabVenue })}
                >
                  <SelectTrigger>
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {TAB_VENUES.map((v) => (
                      <SelectItem key={v} value={v}>
                        {VENUE_LABELS[v]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </div>
              <div>
                <Label>Server (optional)</Label>
                <Input
                  value={openForm.serverName}
                  onChange={(e) => setOpenForm({ ...openForm, serverName: e.target.value })}
                />
              </div>
            </div>
          </div>
          <DialogFooter>
            <Button variant="outline" onClick={() => setOpenDialog(false)} disabled={busy}>
              Cancel
            </Button>
            <Button onClick={submitOpen} disabled={busy}>
              {busy ? (
                <>
                  <Loader2 className="w-4 h-4 mr-2 animate-spin" /> Opening
                </>
              ) : (
                'Open tab'
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
}: {
  icon: React.ReactNode;
  label: string;
  value: string;
}) {
  return (
    <div className="bg-white rounded-2xl border border-slate-200 p-5 shadow-sm">
      <div className="flex items-center justify-between mb-2">
        <div className="w-10 h-10 rounded-xl bg-slate-50 flex items-center justify-center">{icon}</div>
      </div>
      <p className="text-2xl font-bold text-slate-900">{value}</p>
      <p className="text-xs text-slate-500 mt-0.5">{label}</p>
    </div>
  );
}

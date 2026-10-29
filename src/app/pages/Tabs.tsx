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
import { usePageLoading } from '@/app/components/LoadingOverlay';
import PageHero, { heroPrimary } from '@/app/components/PageHero';
import { StaggerItem, liftOnHover } from '@/app/components/motion/Stagger';
import {
  InitialsAvatar, RecordList, RecordTable, ViewSwitcher, ViewTransition, columnChoices, gridDensity, useViewPreference,
  type RecordColumn,
} from '@/app/components/views';
import { useClubMoney } from '@/app/hooks/useClubMoney';

function timeAgo(iso: string): string {
  const diff = Date.now() - Date.parse(iso);
  const min = Math.floor(diff / 60_000);
  if (min < 60) return `${min}m`;
  const h = Math.floor(min / 60);
  return `${h}h ${min % 60}m`;
}

export default function Tabs() {
  const { format: formatCurrency } = useClubMoney();
  const [tabs, setTabs] = useState<OpenTab[]>([]);
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  usePageLoading(loading);
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

  // How this person likes open tabs shown: cards, a list or a table, saved for them.
  const view = useViewPreference('tabs', { layouts: ['grid', 'list', 'table'] });

  /** A tab's items, each removable. */
  const tabItems = (t: OpenTab) =>
    t.items.length === 0 ? (
      <p className="text-sm text-slate-500 py-3">No items yet.</p>
    ) : (
      <ul className="space-y-1.5">
        {t.items.map((i) => (
          <li key={i.id} className="flex items-center justify-between gap-2 text-sm">
            <div className="min-w-0 flex-1">
              <span className="text-slate-700">
                {i.quantity > 1 && (
                  <span className="text-slate-500">{i.quantity}× </span>
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
              className="-m-1 rounded p-1.5 text-slate-500 hover:text-rose-600"
              aria-label={`Remove ${i.name}`}
            >
              <X className="w-3.5 h-3.5" />
            </button>
          </li>
        ))}
      </ul>
    );

  /** The form that adds an item to the tab being edited. */
  const itemEditor = () => (
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
  );

  /** Add an item, close and post, or void: the same on a card, a row and a table line. */
  const tabActions = (t: OpenTab, wrap = true) => (
    <div className={wrap ? 'flex flex-wrap gap-2' : 'flex items-center gap-2'}>
      <Button size="sm" variant={addingTo === t.id ? 'secondary' : 'outline'} onClick={() => setAddingTo(t.id)}>
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
        className="text-rose-700 hover:bg-rose-50"
      >
        Void
      </Button>
    </div>
  );

  const tabColumns: RecordColumn<OpenTab>[] = [
    {
      id: 'member',
      label: 'Member',
      required: true,
      cell: (t) => (
        <span className="flex items-center gap-2.5">
          <InitialsAvatar name={t.memberName} size="sm" />
          <span className="truncate">{t.memberName}</span>
        </span>
      ),
    },
    { id: 'venue', label: 'Venue', cell: (t) => VENUE_LABELS[t.venue] },
    { id: 'opened', label: 'Open for', cell: (t) => timeAgo(t.openedAtUtc) },
    { id: 'items', label: 'Items', numeric: true, cell: (t) => t.items.reduce((n, i) => n + i.quantity, 0) },
    { id: 'total', label: 'Total', numeric: true, cell: (t) => <span className="font-semibold text-slate-900">{formatCurrency(t.total)}</span> },
    { id: 'tier', label: 'Tier', byDefault: false, cell: (t) => t.memberTier },
    { id: 'server', label: 'Server', byDefault: false, cell: (t) => t.serverName || '—' },
  ];

  const editing = view.layout !== 'grid' ? openTabs.find((t) => t.id === addingTo) : undefined;

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
            icon={Wine}
            tone="amber"
            title="Open Tabs"
            subtitle="Running checks at the bar and restaurant. Closing a tab posts a charge and awards points."
            actions={
              <Button
                onClick={() => {
                  setOpenForm({ memberId: '', venue: 'Bar', serverName: '' });
                  setOpenDialog(true);
                }}
                className={heroPrimary('amber')}
              >
                <Plus className="h-4 w-4" /> Open tab
              </Button>
            }
            stats={[
              { label: 'Open tabs', value: stats.openCount, icon: Wine, tone: 'amber' },
              { label: 'Open value', value: stats.openValue, icon: DollarSign, tone: 'rose', format: formatCurrency },
              { label: 'Closed today', value: stats.closedToday, icon: Receipt, tone: 'emerald' },
              { label: 'Revenue today', value: stats.revenueToday, icon: DollarSign, tone: 'blue', format: formatCurrency },
            ]}
          />

          {loading ? (
            <div className="flex items-center justify-center py-20 text-slate-500">
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
            <>
            <div className="mb-4 flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-slate-500">
                <span className="font-semibold text-slate-900">{openTabs.length}</span> open {openTabs.length === 1 ? 'tab' : 'tabs'}
              </p>
              <ViewSwitcher view={view} columns={columnChoices(tabColumns)} />
            </div>
            {/* In a list or a table, the tab being added to opens here, with its items. */}
            {editing && (
              <div className="mb-4 rounded-2xl border border-amber-200 bg-white shadow-sm overflow-hidden animate-in fade-in-0 slide-in-from-top-2 duration-200">
                <div className="bg-gradient-to-r from-amber-500/10 to-orange-500/10 px-5 py-3 flex items-center justify-between border-b border-slate-100">
                  <div>
                    <p className="font-semibold text-slate-900">{editing.memberName}'s tab</p>
                    <p className="text-xs text-slate-500">{VENUE_LABELS[editing.venue]} · {formatCurrency(editing.total)}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setAddingTo(null)}
                    className="rounded p-1 text-slate-500 hover:bg-white hover:text-slate-700"
                    aria-label="Close"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
                <div className="px-5 py-3">{tabItems(editing)}</div>
                <div className="px-5 py-3 bg-slate-50 border-t border-slate-100">{itemEditor()}</div>
              </div>
            )}
            <ViewTransition layout={view.layout} className="mb-8">
            {view.layout === 'list' ? (
              <RecordList
                label="Open tabs"
                items={openTabs}
                view={view}
                getKey={(t) => t.id}
                leading={(t) => <InitialsAvatar name={t.memberName} />}
                title={(t) => t.memberName}
                subtitle={(t) => `${VENUE_LABELS[t.venue]}${t.serverName ? ` · ${t.serverName}` : ''} · open ${timeAgo(t.openedAtUtc)}`}
                columns={tabColumns.filter((c) => c.id !== 'venue' && c.id !== 'opened')}
                actions={(t) => tabActions(t, false)}
              />
            ) : view.layout === 'table' ? (
              <RecordTable
                label="Open tabs"
                items={openTabs}
                view={view}
                getKey={(t) => t.id}
                columns={tabColumns}
                actions={(t) => tabActions(t, false)}
              />
            ) : (
            <div className={`grid grid-cols-1 lg:grid-cols-2 ${gridDensity(view, 'gap-4', 'gap-3 2xl:grid-cols-3')}`}>
              {openTabs.map((t, index) => (
                <StaggerItem
                  as="article"
                  key={t.id}
                  index={index}
                  className={`bg-white rounded-2xl border border-slate-200 shadow-sm overflow-hidden hover:border-amber-200 ${liftOnHover}`}
                >
                  <div className={`bg-gradient-to-r from-amber-500/10 to-orange-500/10 ${view.density === 'compact' ? 'px-4 py-2' : 'px-5 py-3'} flex items-center justify-between border-b border-slate-100`}>
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
                  <div className={view.density === 'compact' ? 'px-4 py-2' : 'px-5 py-3'}>
                    {tabItems(t)}
                  </div>
                  <div className={`${view.density === 'compact' ? 'px-4 py-2' : 'px-5 py-3'} bg-slate-50 border-t border-slate-100`}>
                    {addingTo === t.id ? (
                      itemEditor()
                    ) : (
                      <div className="flex flex-wrap items-center justify-between gap-2">
                        <div>
                          <p className="text-xs text-slate-500 uppercase tracking-wide">Running total</p>
                          <p className="text-xl font-bold text-slate-900">{formatCurrency(t.total)}</p>
                        </div>
                        {tabActions(t)}
                      </div>
                    )}
                  </div>
                </StaggerItem>
              ))}
            </div>
            )}
            </ViewTransition>
            </>
          )}

          {recentClosed.length > 0 && (
            <section>
              <h2 className="font-semibold text-slate-900 mb-3 inline-flex items-center gap-2">
                <Clock className="w-4 h-4" /> Recently closed
              </h2>
              <div className="bg-white rounded-2xl border border-slate-200 shadow-sm divide-y divide-slate-100">
                {recentClosed.map((t, index) => (
                  <StaggerItem key={t.id} index={index} className="px-4 py-3 flex items-center justify-between transition-colors hover:bg-slate-50">
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
                        <p className="text-xs text-amber-700 inline-flex items-center gap-1">
                          <Sparkles className="w-3 h-3" /> +{t.pointsAwarded} pts
                        </p>
                      ) : null}
                    </div>
                  </StaggerItem>
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
                <SelectTrigger aria-label="Member">
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
                  <SelectTrigger aria-label="Venue">
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
                <Input aria-label="Server (optional)"
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

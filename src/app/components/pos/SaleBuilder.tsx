// The house-charge screen's sale: items from the price catalogue with their quantities and VAT
// rates, where it was sold, how the member paid (on their house account, by card, cash or
// TWINT) and any discount with its reason, posted to the server in one go.
//
// The server prices every line from the catalogue, posts a charge per VAT rate through the
// ledger, records what was paid at the till and works out the points from the club's settings.
// What this card adds up is a preview of that; the answer the server gives is what it shows
// once posted. A sale keeps one Idempotency-Key until it is posted or changed, so pressing
// again after a dropped connection gets the first sale back instead of charging twice.

import { useMemo, useState } from 'react';
import { Loader2, Minus, Plus, Search, Trash2 } from 'lucide-react';
import { toast } from 'sonner';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { LoadFailed } from '@/app/components/LoadFailed';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION } from '@/app/api/organizations';
import { servesDemoData } from '@/app/api/apiClient';
import type { CatalogItem } from '@/app/api/catalog';
import type { Member } from '@/app/api/members';
import { TAB_VENUES, VENUE_LABELS, type TabVenue } from '@/app/api/tabs';
import { newSaleKey, postSale, SALE_TENDERS, SALES_DEMO_MESSAGE, TENDER_LABEL, type Sale, type SaleTender } from '@/app/api/sales';
import { useClubCatalog } from '@/app/hooks/queries';
import { useClubMoney } from '@/app/hooks/useClubMoney';

/** Kinds of catalogue item the house-charge screen rings up, as the server takes them. */
const SALE_KINDS = new Set(['MenuItem', 'Product', 'Fee']);

type DiscountMode = 'none' | 'amount' | 'percent';

const round2 = (n: number) => Math.round(n * 100) / 100;

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

export function SaleBuilder({ member, onPosted }: { member: Member; onPosted?: (sale: Sale) => void }) {
  const { format, currency } = useClubMoney();
  const catalogQuery = useClubCatalog();
  const demo = servesDemoData();
  const [venue, setVenue] = useState<TabVenue>('Bar');
  const [search, setSearch] = useState('');
  const [lines, setLines] = useState<{ itemId: string; quantity: number }[]>([]);
  const [discountMode, setDiscountMode] = useState<DiscountMode>('none');
  const [discountValue, setDiscountValue] = useState('');
  const [discountReason, setDiscountReason] = useState('');
  const [tender, setTender] = useState<SaleTender>('HouseAccount');
  const [notes, setNotes] = useState('');
  const [posting, setPosting] = useState(false);
  // One key per sale on the screen: kept for a retry of the same sale, new once it changes.
  const [saleKey, setSaleKey] = useState(newSaleKey);
  const changed = () => setSaleKey(newSaleKey());

  const items = useMemo(
    () => (catalogQuery.data ?? []).filter((i) => i.isActive && SALE_KINDS.has(i.kind)),
    [catalogQuery.data],
  );
  const byId = useMemo(() => new Map(items.map((i) => [i.id, i])), [items]);
  const found = useMemo(() => {
    const q = search.trim().toLowerCase();
    const matching = q
      ? items.filter((i) => `${i.name} ${i.category ?? ''} ${i.code ?? ''}`.toLowerCase().includes(q))
      : items;
    return matching.slice(0, 12);
  }, [items, search]);

  const rows = lines
    .map((l) => ({ ...l, item: byId.get(l.itemId) }))
    .filter((l): l is { itemId: string; quantity: number; item: CatalogItem } => l.item !== undefined);
  const subtotal = round2(rows.reduce((sum, r) => sum + round2(r.item.price * r.quantity), 0));
  const typed = Number(discountValue);
  const discount = discountMode === 'none' || !Number.isFinite(typed) || typed <= 0
    ? 0
    : discountMode === 'amount'
      ? Math.min(round2(typed), subtotal)
      : round2(subtotal * Math.min(typed, 100) / 100);
  const total = round2(subtotal - discount);
  const needsReason = discount > 0 && discountReason.trim().length === 0;
  // What the VAT in the total comes to at each rate, the discount taken off each line in proportion.
  const vat = useMemo(() => {
    const factor = subtotal > 0 ? total / subtotal : 0;
    const byRate = new Map<number, number>();
    for (const r of rows) {
      const gross = round2(r.item.price * r.quantity) * factor;
      byRate.set(r.item.vatRate, (byRate.get(r.item.vatRate) ?? 0) + gross);
    }
    return [...byRate.entries()]
      .filter(([rate]) => rate > 0)
      .sort((a, b) => b[0] - a[0])
      .map(([rate, gross]) => ({ rate, amount: round2(gross * rate / (100 + rate)) }));
  }, [rows, subtotal, total]);

  const add = (item: CatalogItem) => {
    setLines((current) => {
      const existing = current.find((l) => l.itemId === item.id);
      return existing
        ? current.map((l) => (l.itemId === item.id ? { ...l, quantity: Math.min(1000, l.quantity + 1) } : l))
        : [...current, { itemId: item.id, quantity: 1 }];
    });
    changed();
  };
  const setQuantity = (itemId: string, quantity: number) => {
    setLines((current) =>
      quantity <= 0 ? current.filter((l) => l.itemId !== itemId) : current.map((l) => (l.itemId === itemId ? { ...l, quantity } : l)));
    changed();
  };

  const post = async () => {
    if (rows.length === 0 || needsReason || posting) return;
    setPosting(true);
    try {
      const sale = await postSale(
        {
          memberId: member.id,
          venue,
          lines: rows.map((r) => ({ catalogItemId: r.itemId, quantity: r.quantity })),
          tender,
          discountAmount: discountMode === 'amount' && discount > 0 ? discount : undefined,
          discountPercent: discountMode === 'percent' && discount > 0 ? Math.min(typed, 100) : undefined,
          discountReason: discount > 0 ? discountReason.trim() : undefined,
          notes: notes.trim() || undefined,
        },
        saleKey,
      );
      const where = sale.owedAmount > 0
        ? sale.billedMemberId ? 'on the household primary\'s house account' : `on ${member.firstName} ${member.lastName}'s house account`
        : `paid by ${TENDER_LABEL[sale.tender]}`;
      toast.success(`${format(sale.total)} ${where}${sale.pointsAwarded > 0 ? ` · +${sale.pointsAwarded.toLocaleString()} points` : ''}`);
      setLines([]);
      setDiscountMode('none');
      setDiscountValue('');
      setDiscountReason('');
      setNotes('');
      setSaleKey(newSaleKey());
      onPosted?.(sale);
    } catch (err) {
      // The server says why (an item taken off sale, a member who has left); the sale stays on
      // screen with the same key, so trying again cannot post it twice.
      toast.error(message(err, 'Could not post the sale'));
    } finally {
      setPosting(false);
    }
  };

  const action = tender === 'HouseAccount'
    ? `Put ${format(total)} on the house account`
    : `Record ${format(total)} paid by ${TENDER_LABEL[tender]}`;

  return (
    <section aria-label="House charge" className="bg-white rounded-2xl border border-slate-200 shadow-sm p-5 mb-4">
      <div className="flex items-baseline justify-between gap-3 mb-3">
        <h3 className="text-base font-semibold text-slate-900">House charge</h3>
        <p className="text-xs text-slate-500">Prices, VAT and points come from the club&apos;s catalogue and settings.</p>
      </div>

      {demo && <p className="mb-3 rounded-lg bg-amber-50 p-3 text-sm text-amber-800">{SALES_DEMO_MESSAGE}</p>}

      <div className="mb-4">
        <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1.5">Where</p>
        <div className="flex flex-wrap gap-1.5" role="group" aria-label="Where it was sold">
          {TAB_VENUES.map((v) => (
            <button
              key={v}
              type="button"
              aria-pressed={venue === v}
              onClick={() => { setVenue(v); changed(); }}
              className={`rounded-full px-3 py-1 text-sm border ${venue === v ? 'bg-cyan-600 text-white border-cyan-600' : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'}`}
            >
              {VENUE_LABELS[v]}
            </button>
          ))}
        </div>
      </div>

      {catalogQuery.isError && catalogQuery.data === undefined ? (
        <LoadFailed what="The catalogue" error={catalogQuery.error} onRetry={() => void catalogQuery.refetch()} retrying={catalogQuery.isFetching} />
      ) : (
        <div className="mb-4">
          <div className="relative mb-2">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-500" />
            <Input
              aria-label="Find an item"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Find a drink, a dish, a product…"
              className="pl-9"
            />
          </div>
          {catalogQuery.isPending ? (
            <p className="text-sm text-slate-500 py-2"><Loader2 className="inline w-4 h-4 animate-spin" /> Reading the catalogue…</p>
          ) : items.length === 0 ? (
            <p className="text-sm text-slate-500 py-2">
              The catalogue has no menu items, products or fees on sale. Someone who manages settings adds them under Club settings.
            </p>
          ) : (
            <div className="flex flex-wrap gap-2">
              {found.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  aria-label={`Add ${item.name}`}
                  onClick={() => add(item)}
                  className="rounded-xl border border-slate-200 px-3 py-2 text-left hover:border-cyan-400 hover:bg-cyan-50"
                >
                  <span className="block text-sm font-medium text-slate-900">{item.name}</span>
                  <span className="block text-xs text-slate-500">
                    {format(item.price)} · {item.vatRate > 0 ? `VAT ${item.vatRate}%` : 'No VAT'}
                  </span>
                </button>
              ))}
            </div>
          )}
        </div>
      )}

      {rows.length > 0 && (
        <table className="w-full text-sm mb-4">
          <thead>
            <tr className="text-xs text-slate-500 text-left">
              <th className="font-medium py-1">Item</th>
              <th className="font-medium py-1">Quantity</th>
              <th className="font-medium py-1 text-right">VAT</th>
              <th className="font-medium py-1 text-right">Amount</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.itemId} className="border-t border-slate-100">
                <td className="py-1.5 text-slate-900">{r.item.name}</td>
                <td className="py-1.5">
                  <div className="flex items-center gap-1">
                    <Button type="button" size="icon" variant="outline" className="h-7 w-7" aria-label={`One fewer ${r.item.name}`}
                      onClick={() => setQuantity(r.itemId, r.quantity - 1)}>
                      <Minus className="w-3 h-3" />
                    </Button>
                    <Input
                      aria-label={`Quantity of ${r.item.name}`}
                      type="number"
                      min="1"
                      max="1000"
                      className="h-7 w-16 text-center"
                      value={r.quantity}
                      onChange={(e) => setQuantity(r.itemId, Math.min(1000, Math.max(0, Math.floor(Number(e.target.value) || 0))))}
                    />
                    <Button type="button" size="icon" variant="outline" className="h-7 w-7" aria-label={`One more ${r.item.name}`}
                      onClick={() => setQuantity(r.itemId, Math.min(1000, r.quantity + 1))}>
                      <Plus className="w-3 h-3" />
                    </Button>
                  </div>
                </td>
                <td className="py-1.5 text-right text-slate-500">{r.item.vatRate > 0 ? `${r.item.vatRate}%` : '—'}</td>
                <td className="py-1.5 text-right font-medium text-slate-900">{format(round2(r.item.price * r.quantity))}</td>
                <td className="py-1.5 text-right">
                  <Button type="button" size="icon" variant="ghost" className="h-7 w-7" aria-label={`Remove ${r.item.name}`}
                    onClick={() => setQuantity(r.itemId, 0)}>
                    <Trash2 className="w-3.5 h-3.5 text-slate-500" />
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div className="grid gap-3 md:grid-cols-2 mb-4">
        <div>
          <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1.5">Discount</p>
          <div className="flex gap-1.5 mb-2" role="group" aria-label="Discount">
            {([['none', 'None'], ['amount', currency], ['percent', '%']] as const).map(([mode, label]) => (
              <button
                key={mode}
                type="button"
                aria-pressed={discountMode === mode}
                onClick={() => { setDiscountMode(mode); changed(); }}
                className={`rounded-lg px-3 py-1 text-sm border ${discountMode === mode ? 'bg-slate-800 text-white border-slate-800' : 'bg-white text-slate-700 border-slate-200'}`}
              >
                {label}
              </button>
            ))}
          </div>
          {discountMode !== 'none' && (
            <div className="space-y-2">
              <Input
                aria-label={discountMode === 'amount' ? `Discount (${currency})` : 'Discount (%)'}
                type="number"
                min="0"
                step={discountMode === 'amount' ? '0.01' : '1'}
                value={discountValue}
                onChange={(e) => { setDiscountValue(e.target.value); changed(); }}
              />
              <div>
                <Label htmlFor="sale-discount-reason" className="text-xs">Reason for the discount (required)</Label>
                <Input
                  id="sale-discount-reason"
                  value={discountReason}
                  maxLength={500}
                  onChange={(e) => { setDiscountReason(e.target.value); changed(); }}
                  placeholder="Birthday, a dish sent back, a regular's round…"
                />
              </div>
            </div>
          )}
        </div>
        <div>
          <p className="text-xs font-medium text-slate-500 uppercase tracking-wide mb-1.5">Paid by</p>
          <div className="grid grid-cols-2 gap-1.5" role="group" aria-label="Paid by">
            {SALE_TENDERS.map((t) => (
              <button
                key={t}
                type="button"
                aria-pressed={tender === t}
                onClick={() => { setTender(t); changed(); }}
                className={`rounded-lg px-3 py-2 text-sm border ${tender === t ? 'bg-emerald-600 text-white border-emerald-600' : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'}`}
              >
                {TENDER_LABEL[t]}
              </button>
            ))}
          </div>
          <p className="text-xs text-slate-500 mt-1.5">
            {tender === 'HouseAccount'
              ? 'Owed on the house account, paid with the statement.'
              : 'Paid at the till: recorded on the account as charged and paid.'}
          </p>
        </div>
      </div>

      <div className="mb-4">
        <Label htmlFor="sale-notes" className="text-xs">Note (optional)</Label>
        <Input id="sale-notes" value={notes} maxLength={1000} onChange={(e) => setNotes(e.target.value)} />
      </div>

      <div className="flex flex-wrap items-end justify-between gap-3 border-t border-slate-100 pt-3">
        <dl className="text-sm text-slate-600 space-y-0.5" aria-label="Sale total">
          <div className="flex gap-2"><dt>Subtotal</dt><dd className="font-medium text-slate-900">{format(subtotal)}</dd></div>
          {discount > 0 && <div className="flex gap-2"><dt>Discount</dt><dd className="font-medium text-rose-600">−{format(discount)}</dd></div>}
          <div className="flex gap-2"><dt>Total</dt><dd className="font-semibold text-slate-900">{format(total)}</dd></div>
          {vat.map((v) => (
            <div key={v.rate} className="flex gap-2 text-xs text-slate-500"><dt>VAT {v.rate}% included</dt><dd>{format(v.amount)}</dd></div>
          ))}
        </dl>
        <WriteOnly permission={PERMISSION.FrontDesk}>
          <div className="text-right">
            {needsReason && <p className="text-xs text-rose-600 mb-1">Say why the discount was given.</p>}
            <Button type="button" onClick={post} disabled={demo || posting || rows.length === 0 || needsReason}>
              {posting ? <Loader2 className="w-4 h-4 animate-spin" /> : action}
            </Button>
          </div>
        </WriteOnly>
      </div>
    </section>
  );
}

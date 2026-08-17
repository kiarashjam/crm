import { useEffect, useMemo, useState } from 'react';
import { Package, Plus, Trash2, Loader2 } from 'lucide-react';
import { toast } from 'sonner';
import { Input } from '@/app/components/ui/input';
import { Button } from '@/app/components/ui/button';
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from '@/app/components/ui/select';
import {
  getProducts, getDealLineItems, addDealLineItem, updateDealLineItem, deleteDealLineItem,
  lineItemsTotal, updateDeal,
  type Product, type DealLineItem,
} from '@/app/api';
import { formatMoney, DEFAULT_CURRENCY } from '@/app/lib/money';

interface DealLineItemsCardProps {
  dealId: string;
  /** The parent deal's currency. Line items are amounts IN that currency, so
   *  formatting them as USD was simply wrong for any other. */
  currency?: string;
  className?: string;
  onValueSynced?: (total: number) => void;
}

export default function DealLineItemsCard({ dealId, currency, className, onValueSynced }: DealLineItemsCardProps) {
  const fmt = (n: number) => formatMoney(n, currency || DEFAULT_CURRENCY);
  const [items, setItems] = useState<DealLineItem[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [adding, setAdding] = useState(false);
  const [syncing, setSyncing] = useState(false);

  // Add-row form
  const [productId, setProductId] = useState('');
  const [name, setName] = useState('');
  const [qty, setQty] = useState('1');
  const [price, setPrice] = useState('');

  useEffect(() => {
    setLoading(true);
    Promise.all([getDealLineItems(dealId), getProducts()])
      .then(([li, ps]) => { setItems(li); setProducts(ps); })
      .catch(() => { /* empty */ })
      .finally(() => setLoading(false));
  }, [dealId]);

  const total = useMemo(() => lineItemsTotal(items), [items]);

  const pickProduct = (id: string) => {
    setProductId(id);
    const p = products.find((x) => x.id === id);
    if (p) { setName(p.name); setPrice(String(p.unitPrice)); }
  };

  const add = async () => {
    const q = parseFloat(qty) || 0;
    const up = parseFloat(price) || 0;
    if (!name.trim()) { toast.error('Enter a product name'); return; }
    if (q <= 0) { toast.error('Quantity must be at least 1'); return; }
    setAdding(true);
    try {
      const res = await addDealLineItem(dealId, { productId: productId || undefined, name: name.trim(), quantity: q, unitPrice: up });
      if (res) {
        setItems((prev) => [...prev, res]);
        setProductId(''); setName(''); setQty('1'); setPrice('');
      } else toast.error('Failed to add');
    } finally {
      setAdding(false);
    }
  };

  const patchItem = (id: string, patch: Partial<DealLineItem>) => {
    setItems((prev) => prev.map((l) => (l.id === id ? { ...l, ...patch } : l)));
  };
  const persistItem = (id: string, patch: Partial<DealLineItem>) => { void updateDealLineItem(id, patch); };

  const remove = async (id: string) => {
    setItems((prev) => prev.filter((l) => l.id !== id));
    const ok = await deleteDealLineItem(id);
    if (!ok) toast.error('Failed to remove');
  };

  const syncValue = async () => {
    setSyncing(true);
    try {
      const ok = await updateDeal(dealId, { value: String(total) });
      if (ok) { toast.success('Deal value updated to line-item total'); onValueSynced?.(total); }
      else toast.error('Failed to update deal value');
    } finally {
      setSyncing(false);
    }
  };

  return (
    <div className={className ?? 'rounded-2xl border border-slate-200 bg-white p-5 shadow-sm'}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 text-sm font-semibold text-slate-700">
          <Package className="h-4 w-4 text-indigo-500" /> Products &amp; line items
        </h3>
        {items.length > 0 && (
          <Button size="sm" variant="outline" onClick={syncValue} disabled={syncing} className="h-7 gap-1 text-xs">
            {syncing && <Loader2 className="h-3.5 w-3.5 animate-spin" />} Set deal value
          </Button>
        )}
      </div>

      {loading ? (
        <div className="py-4 text-center text-slate-400"><Loader2 className="mx-auto h-4 w-4 animate-spin" /></div>
      ) : (
        <>
          {items.length > 0 && (
            <div className="overflow-hidden rounded-lg border border-slate-100">
              <table className="w-full text-sm">
                <thead className="bg-slate-50 text-xs uppercase tracking-wider text-slate-400">
                  <tr>
                    <th className="px-3 py-2 text-left font-semibold">Item</th>
                    <th className="w-16 px-2 py-2 text-right font-semibold">Qty</th>
                    <th className="w-28 px-2 py-2 text-right font-semibold">Unit</th>
                    <th className="w-28 px-3 py-2 text-right font-semibold">Total</th>
                    <th className="w-8" />
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-50">
                  {items.map((l) => (
                    <tr key={l.id}>
                      <td className="px-3 py-1.5 text-slate-700">{l.name}</td>
                      <td className="px-2 py-1.5">
                        <Input
                          type="number" min={0} value={l.quantity}
                          onChange={(e) => patchItem(l.id, { quantity: parseFloat(e.target.value) || 0 })}
                          onBlur={(e) => persistItem(l.id, { quantity: parseFloat(e.target.value) || 0 })}
                          className="h-7 text-right"
                        />
                      </td>
                      <td className="px-2 py-1.5">
                        <Input
                          type="number" min={0} value={l.unitPrice}
                          onChange={(e) => patchItem(l.id, { unitPrice: parseFloat(e.target.value) || 0 })}
                          onBlur={(e) => persistItem(l.id, { unitPrice: parseFloat(e.target.value) || 0 })}
                          className="h-7 text-right"
                        />
                      </td>
                      <td className="px-3 py-1.5 text-right font-medium text-slate-800">{fmt(l.quantity * l.unitPrice)}</td>
                      <td className="px-1 py-1.5">
                        <button type="button" onClick={() => remove(l.id)} className="rounded p-1 text-slate-300 hover:bg-red-50 hover:text-red-600"><Trash2 className="h-3.5 w-3.5" /></button>
                      </td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr className="border-t border-slate-200 bg-slate-50/60">
                    <td colSpan={3} className="px-3 py-2 text-right text-xs font-semibold uppercase tracking-wider text-slate-400">Total</td>
                    <td className="px-3 py-2 text-right text-base font-bold text-slate-900">{fmt(total)}</td>
                    <td />
                  </tr>
                </tfoot>
              </table>
            </div>
          )}

          {/* Add row */}
          <div className="mt-3 grid grid-cols-[1fr_auto] gap-2 sm:grid-cols-[1.5fr_3rem_6rem_auto]">
            <div className="sm:col-span-1">
              <Select value={productId} onValueChange={pickProduct}>
                <SelectTrigger className="h-9"><SelectValue placeholder="Product or custom…" /></SelectTrigger>
                <SelectContent>
                  {products.map((p) => <SelectItem key={p.id} value={p.id}>{p.name} — {fmt(p.unitPrice)}</SelectItem>)}
                </SelectContent>
              </Select>
              <Input value={name} onChange={(e) => setName(e.target.value)} placeholder="Item name" className="mt-2 h-9" />
            </div>
            <Input type="number" min={1} value={qty} onChange={(e) => setQty(e.target.value)} placeholder="Qty" className="h-9" aria-label="Quantity" />
            <Input type="number" min={0} value={price} onChange={(e) => setPrice(e.target.value)} placeholder="Unit price" className="h-9" aria-label="Unit price" />
            <Button onClick={add} disabled={adding} className="h-9 gap-1.5">
              {adding ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} Add
            </Button>
          </div>
        </>
      )}
    </div>
  );
}

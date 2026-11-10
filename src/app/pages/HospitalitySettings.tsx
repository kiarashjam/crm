// The club's settings page: what the server applies, and nothing it does not.
//
// - The price catalogue: membership tiers (dues, cadence, initiation fee, VAT, loyalty
//   multiplier, benefits) and every other price (dues, fees, products, tickets, menu
//   items). The server takes its prices from here.
// - Points: base points per unit spent on a tab and points per visit by venue, each
//   times the member's tier multiplier.
// - Billing: what the daily run does on its own, what billing now would post, and the
//   runs so far.
//
// The page used to save tiers, dues, multipliers and points that nothing read, and said
// "Not applied yet" next to each. What is here now is applied; what was never used (the
// redemption rate, the referral and anniversary bonuses) has gone.

import { useEffect, useMemo, useState } from 'react';
import {
  Settings, Save, RotateCcw, Crown, Sparkles, Loader2, Coins, Plus, X, Receipt, Tag, CalendarClock, PlayCircle,
} from 'lucide-react';
import { toast } from 'sonner';
import AppHeader from '@/app/components/AppHeader';
import { PageTransition } from '@/app/components/PageTransition';
import { MAIN_CONTENT_ID } from '@/app/components/SkipLink';
import { Button } from '@/app/components/ui/button';
import { Input } from '@/app/components/ui/input';
import { Label } from '@/app/components/ui/label';
import { Switch } from '@/app/components/ui/switch';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/app/components/ui/select';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/app/components/ui/tabs';
import {
  updateHospitalitySettings, resetHospitalitySettings, DEFAULT_BILLING,
  type BillingSettings, type HospitalitySettings as Settings_,
} from '@/app/api/hospitalitySettings';
import {
  CATALOG_CADENCES, CATALOG_KIND_LABEL, CADENCE_LABEL, CHARGE_KINDS, VAT_RATES, createCatalogItem, updateCatalogItem,
  type CatalogCadence, type CatalogItem, type CatalogKind,
} from '@/app/api/catalog';
import { runBillingNow, type BillingRun } from '@/app/api/billing';
import { VISIT_VENUES, type VisitVenue } from '@/app/api/visits';
import { usePageLoading } from '@/app/components/LoadingOverlay';
import { ClubReadState } from '@/app/components/ClubReadState';
import PageHero, { heroPrimary, heroSecondary } from '@/app/components/PageHero';
import { setClubCurrency, useClubMoney } from '@/app/hooks/useClubMoney';
import { useRegional } from '@/app/hooks/useMoney';
import { CLUB_CURRENCIES, formatClubMoney, normaliseCurrency } from '@/app/lib/clubMoney';
import { WriteOnly } from '@/app/components/WriteOnly';
import { PERMISSION, orgCan } from '@/app/api/organizations';
import { useOrgOptional } from '@/app/contexts/OrgContext';
import { servesDemoData } from '@/app/api/apiClient';
import {
  NONE, useBillingPreview, useBillingRuns, useClubCatalog, useClubReads, useHospitalitySettings, useWorkspaceScope,
} from '@/app/hooks/queries';

/** Who may change the catalogue: the settings' managers, and the money's. */
const CATALOG_EDITORS = [PERMISSION.ManageSettings, PERMISSION.ManageMoney] as const;

const OTHER_KINDS: CatalogKind[] = ['Dues', 'Fee', 'Product', 'Ticket', 'MenuItem'];

const message = (err: unknown, fallback: string) => (err instanceof Error && err.message ? err.message : fallback);

const numberOr = (text: string, fallback: number) => {
  const n = Number(text);
  return Number.isFinite(n) ? n : fallback;
};

function formatDateTime(iso: string): string {
  try {
    return new Date(iso).toLocaleString(undefined, { day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' });
  } catch {
    return iso;
  }
}

function formatDay(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
  } catch {
    return iso;
  }
}

export default function HospitalitySettings() {
  const org = useOrgOptional()?.currentOrg ?? null;
  const demo = servesDemoData();
  const canSeeBilling = demo || orgCan(org, PERMISSION.ManageMoney) || orgCan(org, PERMISSION.ManageSettings);

  const settingsQuery = useHospitalitySettings();
  const catalogQuery = useClubCatalog();
  const runsQuery = useBillingRuns(canSeeBilling);
  const previewQuery = useBillingPreview(canSeeBilling);
  const reads = useClubReads(settingsQuery, catalogQuery);
  usePageLoading(reads.loading);

  // The settings being edited: what the server applies, until changed here. A change is
  // kept with the workspace it was made in, so switching workspace shows (and saves) that
  // workspace's settings rather than the edits made in the other.
  const ws = useWorkspaceScope();
  const scope = `${ws.orgId ?? ''}|${ws.userId ?? ''}`;
  const [edit, setEdit] = useState<{ scope: string; value: Settings_ } | null>(null);
  const draft = edit && edit.scope === scope ? edit.value : settingsQuery.data ?? null;
  const setDraft = (value: Settings_) => setEdit({ scope, value });
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState(false);

  const catalog = catalogQuery.data ?? NONE;
  const tiers = useMemo(() => catalog.filter((i) => i.kind === 'Membership').sort((a, b) => a.sortOrder - b.sortOrder), [catalog]);
  const others = useMemo(() => catalog.filter((i) => i.kind !== 'Membership'), [catalog]);

  const save = async () => {
    if (!draft) return;
    setSaving(true);
    try {
      const saved = await updateHospitalitySettings({
        currency: draft.currency,
        basePointsPerDollar: draft.basePointsPerDollar,
        visitPointsByVenue: draft.visitPointsByVenue,
        eventCheckInPoints: draft.eventCheckInPoints,
        billing: draft.billing,
      });
      setDraft(saved);
      setClubCurrency(saved.currency);
      toast.success('Settings saved');
      await settingsQuery.refetch();
    } catch (err) {
      // A value the server refuses (a grace period of 0, reminders out of order) says why.
      toast.error(message(err, 'Could not save the settings'));
    } finally {
      setSaving(false);
    }
  };

  const reset = async () => {
    if (!confirm('Reset the points rules and billing settings to their defaults? Prices and tiers stay as they are.')) return;
    setResetting(true);
    try {
      const fresh = await resetHospitalitySettings();
      setDraft(fresh);
      setClubCurrency(fresh.currency);
      toast.success('Settings reset');
      await settingsQuery.refetch();
    } catch (err) {
      toast.error(message(err, 'Could not reset the settings'));
    } finally {
      setResetting(false);
    }
  };

  const currency = normaliseCurrency(draft?.currency ?? 'CHF');
  // New prices are in the workspace's own currency, which every house account is kept in
  // (the server prices a new item in it). The club currency above only labels amounts: an
  // item priced in it, when the two differ, could be charged to no account.
  const accountCurrency = useRegional().currency;

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
            icon={Settings}
            tone="indigo"
            title="Hospitality Settings"
            subtitle="The club's prices and tiers, its points rules, and what billing does on its own. The server applies all of it."
            actions={
              <WriteOnly permission={PERMISSION.ManageSettings}>
                <Button variant="outline" onClick={reset} disabled={resetting || !draft} className={heroSecondary}>
                  {resetting ? <Loader2 className="h-4 w-4 animate-spin" /> : <><RotateCcw className="h-4 w-4" /> Reset to defaults</>}
                </Button>
                <Button onClick={save} disabled={saving || !draft} className={heroPrimary('indigo')}>
                  {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <><Save className="h-4 w-4" /> Save changes</>}
                </Button>
              </WriteOnly>
            }
          />

          <ClubReadState reads={reads} what="The club's settings">
            {!draft || reads.loading ? (
              <div className="flex items-center justify-center py-20 text-slate-500">
                <Loader2 className="h-6 w-6 animate-spin" />
              </div>
            ) : (
              <>
                <div className="mb-6 max-w-2xl rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
                  <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
                    <Coins className="h-4 w-4 text-emerald-600" /> Club currency
                  </h3>
                  <p className="mb-3 text-sm text-slate-500">
                    Every amount on the club pages and on printed statements is shown in this currency. Changing it
                    relabels amounts already entered; it does not convert them.
                  </p>
                  <Select value={currency} onValueChange={(v) => setDraft({ ...draft, currency: v })}>
                    <SelectTrigger aria-label="Club currency" className="max-w-xs">
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {CLUB_CURRENCIES.map((c) => (
                        <SelectItem key={c.code} value={c.code}>
                          {c.code} — {c.name}
                        </SelectItem>
                      ))}
                      {!CLUB_CURRENCIES.some((c) => c.code === currency) && <SelectItem value={currency}>{currency}</SelectItem>}
                    </SelectContent>
                  </Select>
                </div>

                <Tabs defaultValue="tiers">
                  <TabsList className="mb-4 max-w-full justify-start overflow-x-auto">
                    <TabsTrigger value="tiers">Membership tiers</TabsTrigger>
                    <TabsTrigger value="catalog">Price catalogue</TabsTrigger>
                    <TabsTrigger value="points">Points</TabsTrigger>
                    <TabsTrigger value="billing">Billing</TabsTrigger>
                  </TabsList>

                  <TabsContent value="tiers">
                    <p className="mb-3 max-w-3xl text-sm text-slate-500">
                      A new member starts with their tier&apos;s dues, and can be charged its initiation fee when added.
                      The multiplier sets the points a member earns. Changing a tier&apos;s dues changes what new members
                      and new subscriptions are charged; members who already pay keep their own dues. A tier taken off
                      sale stays with the members who have it.
                    </p>
                    <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
                      {tiers.map((tier) => (
                        <TierCard key={`${tier.id}-${tier.version}`} tier={tier} onSaved={() => void catalogQuery.refetch()} />
                      ))}
                      <WriteOnly permission={CATALOG_EDITORS}>
                        <NewItemCard kind="Membership" currency={accountCurrency} onSaved={() => void catalogQuery.refetch()} />
                      </WriteOnly>
                    </div>
                  </TabsContent>

                  <TabsContent value="catalog">
                    <CatalogTable items={others} currency={accountCurrency} onSaved={() => void catalogQuery.refetch()} />
                  </TabsContent>

                  <TabsContent value="points">
                    <PointsPanel draft={draft} onChange={setDraft} currency={currency} />
                  </TabsContent>

                  <TabsContent value="billing">
                    <BillingPanel
                      billing={draft.billing}
                      onChange={(billing) => setDraft({ ...draft, billing })}
                      canSee={canSeeBilling}
                      runs={runsQuery.data ?? NONE}
                      runsFailed={runsQuery.isError && !runsQuery.data}
                      onRetryRuns={() => void runsQuery.refetch()}
                      preview={previewQuery.data}
                      previewFailed={previewQuery.isError && !previewQuery.data}
                      onRetryPreview={() => void previewQuery.refetch()}
                      onBilled={async () => {
                        await Promise.all([runsQuery.refetch(), previewQuery.refetch()]);
                      }}
                    />
                  </TabsContent>
                </Tabs>
              </>
            )}
          </ClubReadState>
        </main>
      </PageTransition>
    </div>
  );
}

// ── tiers ─────────────────────────────────────────────────────────────────────

/** One tier, edited in place and saved on its own, against the version it was read at. */
function TierCard({ tier, onSaved }: { tier: CatalogItem; onSaved: () => void }) {
  const { format } = useClubMoney();
  const [name, setName] = useState(tier.name);
  const [price, setPrice] = useState(String(tier.price));
  const [cadence, setCadence] = useState<CatalogCadence>(tier.cadence ?? 'Monthly');
  const [fee, setFee] = useState(String(tier.initiationFee ?? 0));
  const [vat, setVat] = useState(String(tier.vatRate));
  const [multiplier, setMultiplier] = useState(String(tier.loyaltyMultiplier ?? 1));
  const [benefits, setBenefits] = useState<string[]>(tier.benefits);
  const [newBenefit, setNewBenefit] = useState('');
  const [busy, setBusy] = useState(false);

  const save = async (patch: Parameters<typeof updateCatalogItem>[1]) => {
    setBusy(true);
    try {
      await updateCatalogItem(tier.id, { ...patch, version: tier.version });
      toast.success(`${patch.name ?? tier.name} saved`);
      onSaved();
    } catch (err) {
      // Changed by someone else at the same moment, or a value refused: the server says which.
      toast.error(message(err, 'Could not save the tier'));
      onSaved();
    } finally {
      setBusy(false);
    }
  };

  return (
    <article className={`overflow-hidden rounded-2xl border bg-white shadow-sm ${tier.isActive ? 'border-slate-200' : 'border-dashed border-slate-300 opacity-80'}`}>
      <div className="h-2 bg-gradient-to-r from-indigo-400 to-indigo-600" />
      <div className="p-5">
        <div className="mb-4 flex items-center justify-between gap-2">
          <h3 className="inline-flex items-center gap-2 font-semibold text-slate-900">
            <Crown className="h-5 w-5 text-indigo-600" /> {tier.name}
          </h3>
          <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${tier.isActive ? 'bg-emerald-50 text-emerald-700' : 'bg-slate-100 text-slate-500'}`}>
            {tier.isActive ? 'On sale' : 'Off sale'}
          </span>
        </div>
        <div className="mb-4 grid grid-cols-2 gap-3">
          <div className="col-span-2">
            <Label className="text-xs">Name</Label>
            <Input aria-label={`${tier.name} name`} value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div>
            <Label className="text-xs">Dues ({tier.currency})</Label>
            <Input aria-label={`${tier.name} dues`} type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
          </div>
          <div>
            <Label className="text-xs">Every</Label>
            <Select value={cadence} onValueChange={(v) => setCadence(v as CatalogCadence)}>
              <SelectTrigger aria-label={`${tier.name} cadence`}><SelectValue /></SelectTrigger>
              <SelectContent>
                {CATALOG_CADENCES.map((c) => <SelectItem key={c} value={c}>{CADENCE_LABEL[c]}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div>
            <Label className="text-xs">Initiation fee ({tier.currency})</Label>
            <Input aria-label={`${tier.name} initiation fee`} type="number" min="0" step="0.01" value={fee} onChange={(e) => setFee(e.target.value)} />
          </div>
          <div>
            <Label className="text-xs">VAT included (%)</Label>
            <VatSelect value={vat} onChange={setVat} label={`${tier.name} VAT rate`} />
          </div>
          <div className="col-span-2">
            <Label className="text-xs">Loyalty multiplier (×)</Label>
            <Input aria-label={`${tier.name} loyalty multiplier`} type="number" min="0.01" step="0.05" value={multiplier}
              onChange={(e) => setMultiplier(e.target.value)} />
          </div>
        </div>
        <Label className="text-xs">Benefits</Label>
        <ul className="mb-2 mt-1 space-y-1">
          {benefits.map((b, i) => (
            <li key={`${b}-${i}`} className="flex items-center justify-between gap-2 rounded bg-slate-50 px-2 py-1.5 text-sm">
              <span className="text-slate-700">{b}</span>
              <button type="button" onClick={() => setBenefits(benefits.filter((_, j) => j !== i))}
                className="-m-1 rounded p-1.5 text-slate-500 hover:text-rose-600" aria-label="Remove benefit">
                <X className="h-3.5 w-3.5" />
              </button>
            </li>
          ))}
        </ul>
        <div className="mb-4 flex gap-2">
          <Input placeholder="Add a benefit..." value={newBenefit} onChange={(e) => setNewBenefit(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter' && newBenefit.trim()) {
                e.preventDefault();
                setBenefits([...benefits, newBenefit.trim()]);
                setNewBenefit('');
              }
            }} />
          <Button size="sm" variant="outline" aria-label={`Add a ${tier.name} benefit`}
            onClick={() => {
              if (!newBenefit.trim()) return;
              setBenefits([...benefits, newBenefit.trim()]);
              setNewBenefit('');
            }}>
            <Plus className="h-4 w-4" />
          </Button>
        </div>
        <p className="mb-3 text-xs text-slate-500">
          {format(numberOr(price, tier.price))} a {CADENCE_LABEL[cadence]}
          {numberOr(fee, 0) > 0 ? `, ${format(numberOr(fee, 0))} to join` : ''}.
          {tier.updatedByName ? ` Last changed by ${tier.updatedByName}.` : ''}
        </p>
        <WriteOnly permission={CATALOG_EDITORS}>
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={busy} onClick={() => void save({
              name: name.trim(), price: numberOr(price, tier.price), cadence, initiationFee: numberOr(fee, 0),
              vatRate: numberOr(vat, 0), loyaltyMultiplier: numberOr(multiplier, 1), benefits,
            })}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Save tier'}
            </Button>
            <Button size="sm" variant="outline" disabled={busy} onClick={() => void save({ isActive: !tier.isActive })}>
              {tier.isActive ? 'Take off sale' : 'Put on sale'}
            </Button>
          </div>
        </WriteOnly>
      </div>
    </article>
  );
}

function VatSelect({ value, onChange, label }: { value: string; onChange: (v: string) => void; label: string }) {
  const known = VAT_RATES.some((r) => String(r.rate) === value);
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger aria-label={label}><SelectValue /></SelectTrigger>
      <SelectContent>
        {VAT_RATES.map((r) => <SelectItem key={r.rate} value={String(r.rate)}>{r.label}</SelectItem>)}
        {!known && <SelectItem value={value}>{value}%</SelectItem>}
      </SelectContent>
    </Select>
  );
}

/** Adds an item of one kind: a tier, or any other price, in the workspace's currency (`currency`). */
function NewItemCard({ kind, currency, onSaved }: { kind: CatalogKind; currency: string; onSaved: () => void }) {
  const [open, setOpen] = useState(false);
  const [itemKind, setItemKind] = useState<CatalogKind>(kind);
  const [name, setName] = useState('');
  const [price, setPrice] = useState('');
  const [vat, setVat] = useState(kind === 'Membership' ? '0' : '8.1');
  const [cadence, setCadence] = useState<CatalogCadence>('Monthly');
  const [category, setCategory] = useState('');
  const [chargeKind, setChargeKind] = useState('');
  const [busy, setBusy] = useState(false);
  const recurring = itemKind === 'Membership' || itemKind === 'Dues';

  const add = async () => {
    if (!name.trim()) {
      toast.error('Give it a name');
      return;
    }
    setBusy(true);
    try {
      // No currency: the server prices it in the workspace's, as the house accounts are.
      await createCatalogItem({
        kind: itemKind,
        name: name.trim(),
        price: numberOr(price, 0),
        vatRate: numberOr(vat, 0),
        cadence: recurring ? cadence : undefined,
        category: category.trim() || undefined,
        chargeKind: !recurring && chargeKind ? chargeKind : undefined,
      });
      toast.success(`${name.trim()} added`);
      setOpen(false);
      setName('');
      setPrice('');
      onSaved();
    } catch (err) {
      toast.error(message(err, 'Could not add it'));
    } finally {
      setBusy(false);
    }
  };

  if (!open) {
    return (
      <button type="button" onClick={() => setOpen(true)}
        className="flex min-h-32 items-center justify-center gap-2 rounded-2xl border-2 border-dashed border-slate-300 bg-white/60 p-5 text-sm font-medium text-slate-600 hover:border-indigo-300 hover:text-indigo-700">
        <Plus className="h-4 w-4" /> {kind === 'Membership' ? 'Add a tier' : 'Add a price'}
      </button>
    );
  }
  return (
    <div className="rounded-2xl border border-indigo-200 bg-white p-5 shadow-sm">
      <div className="grid grid-cols-2 gap-3">
        {kind !== 'Membership' && (
          <div className="col-span-2">
            <Label className="text-xs">Kind</Label>
            <Select value={itemKind} onValueChange={(v) => setItemKind(v as CatalogKind)}>
              <SelectTrigger aria-label="Kind"><SelectValue /></SelectTrigger>
              <SelectContent>
                {OTHER_KINDS.map((k) => <SelectItem key={k} value={k}>{CATALOG_KIND_LABEL[k]}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        )}
        <div className="col-span-2">
          <Label className="text-xs">Name</Label>
          <Input aria-label="New item name" value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div>
          <Label className="text-xs">{recurring ? 'Price each period' : 'Price'} ({currency})</Label>
          <Input aria-label="New item price" type="number" min="0" step="0.01" value={price} onChange={(e) => setPrice(e.target.value)} />
        </div>
        <div>
          <Label className="text-xs">VAT included (%)</Label>
          <VatSelect value={vat} onChange={setVat} label="New item VAT rate" />
        </div>
        {recurring ? (
          <div className="col-span-2">
            <Label className="text-xs">Every</Label>
            <Select value={cadence} onValueChange={(v) => setCadence(v as CatalogCadence)}>
              <SelectTrigger aria-label="New item cadence"><SelectValue /></SelectTrigger>
              <SelectContent>
                {CATALOG_CADENCES.map((c) => <SelectItem key={c} value={c}>{CADENCE_LABEL[c]}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        ) : (
          <>
            <div>
              <Label className="text-xs">Category</Label>
              <Input aria-label="New item category" placeholder={itemKind === 'MenuItem' ? 'Bar, Dining…' : ''} value={category}
                onChange={(e) => setCategory(e.target.value)} />
            </div>
            <div>
              <Label className="text-xs">Charged as</Label>
              <Select value={chargeKind || 'auto'} onValueChange={(v) => setChargeKind(v === 'auto' ? '' : v)}>
                <SelectTrigger aria-label="Charged as"><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="auto">By its kind</SelectItem>
                  {CHARGE_KINDS.filter((k) => k !== 'Dues').map((k) => <SelectItem key={k} value={k}>{k}</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </>
        )}
      </div>
      <div className="mt-4 flex gap-2">
        <Button size="sm" onClick={() => void add()} disabled={busy}>{busy ? <Loader2 className="h-4 w-4 animate-spin" /> : 'Add'}</Button>
        <Button size="sm" variant="outline" onClick={() => setOpen(false)} disabled={busy}>Cancel</Button>
      </div>
    </div>
  );
}

// ── the rest of the catalogue ─────────────────────────────────────────────────

function CatalogTable({ items, currency, onSaved }: { items: readonly CatalogItem[]; currency: string; onSaved: () => void }) {
  const { format } = useClubMoney();
  const [busyId, setBusyId] = useState<string | null>(null);
  const [editing, setEditing] = useState<{ id: string; price: string } | null>(null);

  const change = async (item: CatalogItem, patch: Parameters<typeof updateCatalogItem>[1], done: string) => {
    setBusyId(item.id);
    try {
      await updateCatalogItem(item.id, { ...patch, version: item.version });
      toast.success(done);
      setEditing(null);
      onSaved();
    } catch (err) {
      toast.error(message(err, 'Could not change it'));
      onSaved();
    } finally {
      setBusyId(null);
    }
  };

  return (
    <div className="space-y-4">
      <p className="max-w-3xl text-sm text-slate-500">
        A charge, a tab item or a subscription rung up from here takes its price, name and VAT rate from the catalogue;
        changing a price changes what is charged from then on, never what already was. Event tickets are kept on each
        event for now.
      </p>
      <div className="overflow-x-auto rounded-2xl border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-slate-200 text-left text-xs uppercase tracking-wide text-slate-500">
              <th className="px-4 py-3">Item</th>
              <th className="px-4 py-3">Kind</th>
              <th className="px-4 py-3 text-right">Price</th>
              <th className="px-4 py-3 text-right">VAT</th>
              <th className="px-4 py-3">Charged as</th>
              <th className="px-4 py-3">On sale</th>
              <th className="px-4 py-3" />
            </tr>
          </thead>
          <tbody>
            {items.length === 0 && (
              <tr><td colSpan={7} className="px-4 py-6 text-center text-slate-500">No prices yet besides the tiers.</td></tr>
            )}
            {items.map((item) => (
              <tr key={item.id} className={`border-b border-slate-100 ${item.isActive ? '' : 'text-slate-400'}`}>
                <td className="px-4 py-2.5">
                  <span className="block font-medium text-slate-800">{item.name}</span>
                  {item.category && <span className="text-xs text-slate-500">{item.category}</span>}
                </td>
                <td className="px-4 py-2.5">{CATALOG_KIND_LABEL[item.kind]}</td>
                <td className="px-4 py-2.5 text-right">
                  {editing?.id === item.id ? (
                    <Input aria-label={`${item.name} price`} type="number" min="0" step="0.01" className="ml-auto h-8 w-28 text-right"
                      value={editing.price} onChange={(e) => setEditing({ id: item.id, price: e.target.value })} />
                  ) : (
                    <>
                      {format(item.price)}
                      {item.cadence ? <span className="text-xs text-slate-500"> /{CADENCE_LABEL[item.cadence]}</span> : null}
                    </>
                  )}
                </td>
                <td className="px-4 py-2.5 text-right">{item.vatRate ? `${item.vatRate}%` : '—'}</td>
                <td className="px-4 py-2.5">{item.chargeKind}</td>
                <td className="px-4 py-2.5">{item.isActive ? 'Yes' : 'No'}</td>
                <td className="px-4 py-2.5 text-right">
                  <WriteOnly permission={CATALOG_EDITORS}>
                    <div className="flex justify-end gap-2">
                      {editing?.id === item.id ? (
                        <Button size="sm" disabled={busyId === item.id}
                          onClick={() => void change(item, { price: numberOr(editing.price, item.price) }, `${item.name} repriced`)}>
                          Save
                        </Button>
                      ) : (
                        <Button size="sm" variant="outline" onClick={() => setEditing({ id: item.id, price: String(item.price) })}>
                          Reprice
                        </Button>
                      )}
                      <Button size="sm" variant="outline" disabled={busyId === item.id}
                        onClick={() => void change(item, { isActive: !item.isActive }, item.isActive ? `${item.name} taken off sale` : `${item.name} back on sale`)}>
                        {item.isActive ? 'Take off sale' : 'Put on sale'}
                      </Button>
                    </div>
                  </WriteOnly>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <WriteOnly permission={CATALOG_EDITORS}>
        <div className="max-w-xl">
          <NewItemCard kind="Fee" currency={currency} onSaved={onSaved} />
        </div>
      </WriteOnly>
    </div>
  );
}

// ── points ────────────────────────────────────────────────────────────────────

function PointsPanel({ draft, onChange, currency }: { draft: Settings_; onChange: (s: Settings_) => void; currency: string }) {
  const setVenue = (venue: VisitVenue, value: number) =>
    onChange({ ...draft, visitPointsByVenue: { ...draft.visitPointsByVenue, [venue]: value } });
  return (
    <div className="grid max-w-4xl grid-cols-1 gap-4 md:grid-cols-2">
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
          <Sparkles className="h-4 w-4 text-amber-500" /> Spending
        </h3>
        <p className="mb-4 text-sm text-slate-500">
          Closing a tab earns this many points for each {currency} spent, times the member&apos;s tier multiplier.
        </p>
        <Label>Base points per 1 {currency} spent</Label>
        <Input aria-label={`Base points per 1 ${currency} spent`} type="number" min="0" step="0.5"
          value={draft.basePointsPerDollar}
          onChange={(e) => onChange({ ...draft, basePointsPerDollar: numberOr(e.target.value, 0) })} />
      </div>
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
          <Tag className="h-4 w-4 text-cyan-500" /> Check-ins
        </h3>
        <p className="mb-4 text-sm text-slate-500">
          Checking a member in earns the venue&apos;s points, times their tier multiplier.
        </p>
        <div className="grid grid-cols-2 gap-3">
          {VISIT_VENUES.map((v) => (
            <div key={v}>
              <Label>{v}</Label>
              <Input aria-label={`${v} check-in points`} type="number" min="0" value={draft.visitPointsByVenue[v]}
                onChange={(e) => setVenue(v, Math.max(0, Math.floor(numberOr(e.target.value, 0))))} />
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}

// ── billing ───────────────────────────────────────────────────────────────────

/** A read that failed, said so, with a way to try it again. */
function ReadFailed({ what, onRetry }: { what: string; onRetry: () => void }) {
  return (
    <div className="flex flex-wrap items-center gap-3">
      <p className="text-sm text-rose-700">{what}</p>
      <Button size="sm" variant="outline" onClick={onRetry}>Try again</Button>
    </div>
  );
}

const STATUS_LABEL: Record<BillingRun['status'], string> = {
  running: 'Running',
  succeeded: 'Done',
  partly_failed: 'Done, with failures',
  failed: 'Stopped',
  stopped: 'Stopped part way',
};

function BillingPanel({
  billing, onChange, canSee, runs, runsFailed, onRetryRuns, preview, previewFailed, onRetryPreview, onBilled,
}: {
  billing: BillingSettings;
  onChange: (b: BillingSettings) => void;
  canSee: boolean;
  runs: readonly BillingRun[];
  runsFailed: boolean;
  onRetryRuns: () => void;
  preview: { subscriptions: number; periods: number; amountByCurrency: Record<string, number> } | undefined;
  previewFailed: boolean;
  onRetryPreview: () => void;
  onBilled: () => Promise<void>;
}) {
  const { format } = useClubMoney();
  const { locale } = useRegional();
  const [running, setRunning] = useState(false);
  const [reminderText, setReminderText] = useState(billing.reminderDays.join(', '));
  useEffect(() => setReminderText(billing.reminderDays.join(', ')), [billing.reminderDays]);
  const b = { ...DEFAULT_BILLING, ...billing };

  const billNow = async () => {
    setRunning(true);
    try {
      const run = await runBillingNow();
      toast.success(run.chargesPosted === 0
        ? 'Nothing was due: every period that has started is billed'
        : `Billed ${run.chargesPosted} period${run.chargesPosted === 1 ? '' : 's'} for ${run.subscriptionsBilled} subscription${run.subscriptionsBilled === 1 ? '' : 's'}`);
      if (run.failures.length > 0) toast.error(`${run.failures.length} could not be billed: ${run.failures[0]!.name}: ${run.failures[0]!.error}`);
      await onBilled();
    } catch (err) {
      toast.error(message(err, 'Could not bill what is due'));
    } finally {
      setRunning(false);
    }
  };

  const toggle = (label: string, hint: string, checked: boolean, set: (v: boolean) => void) => (
    <label className="flex items-start justify-between gap-4 py-2">
      <span>
        <span className="block text-sm font-medium text-slate-800">{label}</span>
        <span className="block text-xs text-slate-500">{hint}</span>
      </span>
      <Switch aria-label={label} checked={checked} onCheckedChange={set} />
    </label>
  );

  return (
    <div className="grid max-w-5xl grid-cols-1 gap-4 lg:grid-cols-2">
      <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
        <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
          <CalendarClock className="h-4 w-4 text-indigo-600" /> What billing does on its own
        </h3>
        <p className="mb-3 text-sm text-slate-500">
          Once a day, after 03:00, for what is switched on here. Everything starts off: turn on what the club is ready for,
          and save.
        </p>
        <div className="divide-y divide-slate-100">
          {toggle('Bill subscriptions automatically', 'Every period a running subscription has started is billed once, including any it is behind on.',
            b.automaticBilling, (v) => onChange({ ...b, automaticBilling: v }))}
          <div className="py-2">
            <Label className="text-sm">Days a member has to pay a bill</Label>
            <Input aria-label="Days a member has to pay a bill" type="number" min="0" max="365" className="mt-1 max-w-32"
              value={b.paymentTermsDays} onChange={(e) => onChange({ ...b, paymentTermsDays: Math.floor(numberOr(e.target.value, 0)) })} />
          </div>
          {toggle('Send payment reminders', 'An email to the member listing what is unpaid, once at each step, as a service message.',
            b.remindersEnabled, (v) => onChange({ ...b, remindersEnabled: v }))}
          <div className="py-2">
            <Label className="text-sm">Remind this many days after the due date</Label>
            <Input aria-label="Reminder days" className="mt-1 max-w-48" value={reminderText}
              onChange={(e) => setReminderText(e.target.value)}
              onBlur={() => {
                const days = reminderText.split(/[,\s]+/).filter(Boolean).map((d) => Math.floor(Number(d))).filter((d) => Number.isFinite(d));
                onChange({ ...b, reminderDays: days });
              }} />
            <p className="mt-1 text-xs text-slate-500">One to five numbers, each later than the one before, such as 3, 14, 30.</p>
          </div>
          {toggle('Lapse members who do not pay', 'An active member with a charge still unpaid after the grace period is set Lapsed, and set back once nothing is overdue.',
            b.lapseEnabled, (v) => onChange({ ...b, lapseEnabled: v }))}
          <div className="py-2">
            <Label className="text-sm">Grace period (days overdue)</Label>
            <Input aria-label="Grace period" type="number" min="1" max="365" className="mt-1 max-w-32" value={b.gracePeriodDays}
              onChange={(e) => onChange({ ...b, gracePeriodDays: Math.floor(numberOr(e.target.value, 1)) })} />
          </div>
          {toggle('Email monthly statements', "Last month's statement, as a PDF, to every member with something on their account.",
            b.monthlyStatements, (v) => onChange({ ...b, monthlyStatements: v }))}
          <div className="py-2">
            <Label className="text-sm">Send them on this day of the month</Label>
            <Input aria-label="Statement day" type="number" min="1" max="28" className="mt-1 max-w-32" value={b.statementDay}
              onChange={(e) => onChange({ ...b, statementDay: Math.floor(numberOr(e.target.value, 1)) })} />
          </div>
        </div>
      </div>

      <div className="space-y-4">
        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="mb-1 inline-flex items-center gap-2 font-semibold text-slate-900">
            <Receipt className="h-4 w-4 text-emerald-600" /> Due now
          </h3>
          {!canSee ? (
            <p className="text-sm text-slate-500">Whoever manages the money or the settings sees what is due here.</p>
          ) : previewFailed ? (
            // Not "reading" for ever: the read failed, and saying so is what tells the club
            // it does not know what turning automatic billing on would bill.
            <ReadFailed what="What is due could not be read." onRetry={onRetryPreview} />
          ) : !preview ? (
            <p className="text-sm text-slate-500">Reading what is due…</p>
          ) : preview.periods === 0 ? (
            <p className="text-sm text-slate-500">Nothing: every period that has started is billed.</p>
          ) : (
            <p className="text-sm text-slate-700">
              {preview.periods} period{preview.periods === 1 ? '' : 's'} for {preview.subscriptions} subscription
              {preview.subscriptions === 1 ? '' : 's'}:{' '}
              {Object.entries(preview.amountByCurrency).map(([c, amount]) => formatClubMoney(amount, c, locale)).join(', ')}.
            </p>
          )}
          <WriteOnly permission={PERMISSION.ManageMoney}>
            <Button className="mt-3" onClick={() => void billNow()} disabled={running}>
              {running ? <Loader2 className="h-4 w-4 animate-spin" /> : <><PlayCircle className="h-4 w-4" /> Bill what is due now</>}
            </Button>
          </WriteOnly>
        </div>

        <div className="rounded-2xl border border-slate-200 bg-white p-6 shadow-sm">
          <h3 className="mb-3 font-semibold text-slate-900">Runs</h3>
          {!canSee ? (
            <p className="text-sm text-slate-500">Whoever manages the money or the settings sees the runs here.</p>
          ) : runsFailed ? (
            <ReadFailed what="The runs could not be read." onRetry={onRetryRuns} />
          ) : runs.length === 0 ? (
            <p className="text-sm text-slate-500">No run yet.</p>
          ) : (
            <ul className="space-y-3">
              {runs.map((run) => (
                <li key={run.id} className="rounded-xl border border-slate-100 bg-slate-50 p-3 text-sm">
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <span className="font-medium text-slate-800">
                      {run.trigger === 'schedule' ? `Daily run${run.scheduledFor ? `, ${formatDay(run.scheduledFor)}` : ''}` : `Started by ${run.startedByName ?? 'someone'}`}
                    </span>
                    <span className={`rounded-full px-2 py-0.5 text-xs font-medium ${run.status === 'succeeded' ? 'bg-emerald-50 text-emerald-700' : run.status === 'running' ? 'bg-sky-50 text-sky-700' : 'bg-rose-50 text-rose-700'}`}>
                      {STATUS_LABEL[run.status] ?? run.status}
                    </span>
                  </div>
                  <p className="mt-1 text-xs text-slate-500">{formatDateTime(run.startedAtUtc)}</p>
                  <p className="mt-1 text-slate-700">
                    Billed {run.chargesPosted} period{run.chargesPosted === 1 ? '' : 's'} ({format(run.amountBilled)})
                    {' · '}{run.overdueCharges} overdue
                    {run.remindersQueued ? ` · ${run.remindersQueued} reminded` : ''}
                    {run.membersLapsed ? ` · ${run.membersLapsed} lapsed` : ''}
                    {run.membersReinstated ? ` · ${run.membersReinstated} reinstated` : ''}
                    {run.statementsQueued ? ` · ${run.statementsQueued} statements` : ''}
                  </p>
                  {run.failures.length > 0 && (
                    <ul className="mt-2 list-disc pl-5 text-xs text-rose-700">
                      {run.failures.slice(0, 5).map((f, i) => <li key={i}>{f.name}: {f.error}</li>)}
                    </ul>
                  )}
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

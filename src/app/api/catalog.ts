// The club's price catalogue: membership tiers and their dues, other dues, fees,
// products, tickets and menu items, each with a price (VAT included), a currency, a VAT
// rate and whether it is still on sale.
//
// The server takes its prices from here: a new member's dues and initiation fee from
// their tier, a charge or a tab item rung up from an item, a subscription started from a
// tier or dues item. The tier dues used to be a table in this file's neighbour
// (members.ts) and the settings page's own list, which the server never read.

import { apiWithFallback, authFetchJson, type RequestOptions } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { DEFAULT_CLUB_CURRENCY } from '@/app/lib/clubMoney';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type CatalogKind = 'Membership' | 'Dues' | 'Fee' | 'Product' | 'Ticket' | 'MenuItem';
export type CatalogCadence = 'Monthly' | 'Quarterly' | 'SemiAnnual' | 'Annual';

export interface CatalogItem {
  id: string;
  kind: CatalogKind;
  name: string;
  description?: string;
  category?: string;
  code?: string;
  /** VAT included; for a tier or dues, for each period of its cadence. */
  price: number;
  currency: string;
  /** Percent, included in the price: 8.1 is 8.1%. */
  vatRate: number;
  cadence?: CatalogCadence;
  initiationFee?: number;
  loyaltyMultiplier?: number;
  benefits: string[];
  chargeKind: string;
  isActive: boolean;
  sortOrder: number;
  createdAtUtc: string;
  updatedAtUtc: string;
  updatedByName?: string;
  version: string;
}

/** An item to add, or the fields of one to change: a field left out is kept. */
export type CatalogItemInput = Partial<Omit<CatalogItem, 'id' | 'createdAtUtc' | 'updatedAtUtc' | 'updatedByName'>>;

export const CATALOG_KINDS: CatalogKind[] = ['Membership', 'Dues', 'Fee', 'Product', 'Ticket', 'MenuItem'];

export const CATALOG_KIND_LABEL: Record<CatalogKind, string> = {
  Membership: 'Membership tier',
  Dues: 'Dues',
  Fee: 'Fee',
  Product: 'Product',
  Ticket: 'Ticket',
  MenuItem: 'Menu item',
};

export const CATALOG_CADENCES: CatalogCadence[] = ['Monthly', 'Quarterly', 'SemiAnnual', 'Annual'];

export const CADENCE_LABEL: Record<CatalogCadence, string> = {
  Monthly: 'month',
  Quarterly: 'quarter',
  SemiAnnual: 'half year',
  Annual: 'year',
};

/** The Swiss VAT rates since 2024, and none for something exempt. */
export const VAT_RATES: { rate: number; label: string }[] = [
  { rate: 8.1, label: '8.1% standard' },
  { rate: 2.6, label: '2.6% reduced' },
  { rate: 3.8, label: '3.8% accommodation' },
  { rate: 0, label: 'No VAT' },
];

/** What a charge from an item is posted as on the house account. */
export const CHARGE_KINDS = ['Dues', 'Dining', 'Bar', 'Event', 'Spa', 'Booking', 'Retail', 'Other'];

/** A workspace's tiers on sale, in their order: the ones a member can be given. */
export function tiersOnSale(items: readonly CatalogItem[]): CatalogItem[] {
  return items.filter((i) => i.kind === 'Membership' && i.isActive).sort((a, b) => a.sortOrder - b.sortOrder);
}

/** The tier of this name (any case), on sale or not. */
export function tierNamed(items: readonly CatalogItem[], name: string | undefined): CatalogItem | undefined {
  const wanted = (name ?? '').trim().toLowerCase();
  return items.find((i) => i.kind === 'Membership' && i.name.toLowerCase() === wanted);
}

/** A tier's loyalty multiplier, 1 for one the catalogue does not have. */
export function multiplierOf(items: readonly CatalogItem[], tier: string | undefined): number {
  return tierNamed(items, tier)?.loyaltyMultiplier ?? 1;
}

// ── demo ──────────────────────────────────────────────────────────────────────

const now = new Date().toISOString();
const tier = (name: string, price: number, fee: number, multiplier: number, benefits: string[], sortOrder: number): CatalogItem => ({
  id: `cat-tier-${name.toLowerCase()}`,
  kind: 'Membership',
  name,
  price,
  currency: DEFAULT_CLUB_CURRENCY,
  vatRate: 0,
  cadence: 'Monthly',
  initiationFee: fee,
  loyaltyMultiplier: multiplier,
  benefits,
  chargeKind: 'Dues',
  isActive: true,
  sortOrder,
  createdAtUtc: now,
  updatedAtUtc: now,
  version: '00000000-0000-0000-0000-000000000000',
});

const seed: CatalogItem[] = [
  tier('Bronze', 49, 0, 1, ['Coworking access', 'Member rate on events', 'Member directory'], 0),
  tier('Silver', 129, 250, 1.25, ['All Bronze benefits', 'Spa access', 'Fitness studio', 'Rooftop access'], 1),
  tier('Gold', 299, 1000, 1.5, ['All Silver benefits', 'Private dining priority', 'Guest passes (4/mo)', 'Tennis court'], 2),
  tier('Platinum', 599, 2500, 2, ['All Gold benefits', 'Unlimited guest passes', 'Comp spa treatment monthly', 'Founders events'], 3),
  {
    id: 'cat-guest-fee', kind: 'Fee', name: 'Guest fee', price: 25, currency: DEFAULT_CLUB_CURRENCY, vatRate: 8.1, benefits: [],
    chargeKind: 'Other', isActive: true, sortOrder: 0, createdAtUtc: now, updatedAtUtc: now, version: '00000000-0000-0000-0000-000000000000',
  },
  {
    id: 'cat-negroni', kind: 'MenuItem', name: 'Negroni', category: 'Bar', price: 18.5, currency: DEFAULT_CLUB_CURRENCY, vatRate: 8.1, benefits: [],
    chargeKind: 'Bar', isActive: true, sortOrder: 0, createdAtUtc: now, updatedAtUtc: now, version: '00000000-0000-0000-0000-000000000000',
  },
];

const catalogStore = createMockStore<CatalogItem>({
  storageKey: 'crm.mock.catalog.v1',
  seed,
  idOf: (i) => i.id,
});

function sortItems(items: CatalogItem[]): CatalogItem[] {
  return [...items].sort((a, b) =>
    CATALOG_KINDS.indexOf(a.kind) - CATALOG_KINDS.indexOf(b.kind) || a.sortOrder - b.sortOrder || a.name.localeCompare(b.name));
}

// ── calls ─────────────────────────────────────────────────────────────────────

/** The catalogue, of one kind or all, items taken off sale included. */
export async function getCatalog(kind?: CatalogKind, req?: RequestOptions): Promise<CatalogItem[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<CatalogItem[]>(`/api/catalog${kind ? `?kind=${kind}` : ''}`, { signal: req?.signal });
      return Array.isArray(res) ? res.map((i) => ({ ...i, benefits: i.benefits ?? [] })) : [];
    },
    async () => {
      await delay(80);
      return sortItems(catalogStore.list().filter((i) => !kind || i.kind === kind));
    },
  );
}

export async function createCatalogItem(input: CatalogItemInput): Promise<CatalogItem> {
  return apiWithFallback(
    () => authFetchJson<CatalogItem>('/api/catalog', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(100);
      const kind = input.kind ?? 'Product';
      const stamp = new Date().toISOString();
      return catalogStore.add({
        id: mockId('cat'),
        kind,
        name: input.name?.trim() ?? '',
        description: input.description,
        category: input.category,
        code: input.code,
        price: input.price ?? 0,
        currency: input.currency ?? DEFAULT_CLUB_CURRENCY,
        vatRate: input.vatRate ?? 0,
        cadence: kind === 'Membership' || kind === 'Dues' ? input.cadence ?? 'Monthly' : undefined,
        initiationFee: kind === 'Membership' ? input.initiationFee ?? 0 : undefined,
        loyaltyMultiplier: kind === 'Membership' ? input.loyaltyMultiplier ?? 1 : undefined,
        benefits: input.benefits ?? [],
        chargeKind: input.chargeKind ?? (kind === 'Membership' || kind === 'Dues' ? 'Dues' : 'Other'),
        isActive: input.isActive ?? true,
        sortOrder: input.sortOrder ?? catalogStore.list().filter((i) => i.kind === kind).length,
        createdAtUtc: stamp,
        updatedAtUtc: stamp,
        version: mockId('v'),
      });
    },
  );
}

/** Changes an item; a renamed tier is renamed on every member who has it. A stale copy is refused (409). */
export async function updateCatalogItem(id: string, patch: CatalogItemInput): Promise<CatalogItem> {
  return apiWithFallback(
    () => authFetchJson<CatalogItem>(`/api/catalog/${id}`, { method: 'PUT', body: JSON.stringify(patch) }),
    async () => {
      await delay(100);
      const updated = catalogStore.update(id, { ...patch, version: mockId('v'), updatedAtUtc: new Date().toISOString() });
      if (!updated) throw new Error('That catalogue item was not found.');
      return updated;
    },
  );
}

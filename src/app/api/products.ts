// Products catalog + deal line items.
//
// A deal's single `value` field can't express multiple products/quantities.
// Line items add that breakdown; the catalog provides reusable products. Both
// have real-backend and demo (localStorage) implementations.

import { isUsingRealApi, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export interface Product {
  id: string;
  name: string;
  sku?: string;
  unitPrice: number;
  currency?: string;
}

export interface DealLineItem {
  id: string;
  dealId: string;
  productId?: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

const productStore = createMockStore<Product>({
  storageKey: 'crm.mock.products.v1',
  seed: [
    { id: 'prod-1', name: 'Starter plan (annual)', sku: 'PLAN-START', unitPrice: 1200 },
    { id: 'prod-2', name: 'Growth plan (annual)', sku: 'PLAN-GROWTH', unitPrice: 4800 },
    { id: 'prod-3', name: 'Onboarding & setup', sku: 'SVC-ONB', unitPrice: 1500 },
    { id: 'prod-4', name: 'Premium support (annual)', sku: 'SVC-SUP', unitPrice: 2400 },
  ],
  idOf: (p) => p.id,
});

const lineItemStore = createMockStore<DealLineItem>({
  storageKey: 'crm.mock.dealLineItems.v1',
  seed: [],
  idOf: (l) => l.id,
});

// ---- Catalog ----

export async function getProducts(): Promise<Product[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<Product[]>('/api/products');
    return Array.isArray(res) ? res : [];
  }
  await delay(120);
  return [...productStore.list()].sort((a, b) => a.name.localeCompare(b.name));
}

export async function createProduct(input: Omit<Product, 'id'>): Promise<Product | null> {
  if (isUsingRealApi()) {
    return authFetchJson<Product>('/api/products', { method: 'POST', body: JSON.stringify(input) });
  }
  await delay(150);
  return productStore.add({ id: mockId('prod'), ...input });
}

export async function deleteProduct(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/products/${id}`, { method: 'DELETE' });
    return res.status === 204 || res.ok;
  }
  await delay(120);
  return productStore.remove(id);
}

// ---- Line items ----

export async function getDealLineItems(dealId: string): Promise<DealLineItem[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<DealLineItem[]>(`/api/deals/${dealId}/line-items`);
    return Array.isArray(res) ? res : [];
  }
  await delay(120);
  return lineItemStore.list().filter((l) => l.dealId === dealId);
}

export interface LineItemInput {
  productId?: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export async function addDealLineItem(dealId: string, input: LineItemInput): Promise<DealLineItem | null> {
  if (isUsingRealApi()) {
    return authFetchJson<DealLineItem>(`/api/deals/${dealId}/line-items`, { method: 'POST', body: JSON.stringify(input) });
  }
  await delay(150);
  return lineItemStore.add({ id: mockId('li'), dealId, ...input });
}

export async function updateDealLineItem(id: string, patch: Partial<LineItemInput>): Promise<DealLineItem | null> {
  if (isUsingRealApi()) {
    return authFetchJson<DealLineItem>(`/api/deals/line-items/${id}`, { method: 'PUT', body: JSON.stringify(patch) });
  }
  await delay(100);
  return lineItemStore.update(id, patch);
}

export async function deleteDealLineItem(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/deals/line-items/${id}`, { method: 'DELETE' });
    return res.status === 204 || res.ok;
  }
  await delay(100);
  return lineItemStore.remove(id);
}

export function lineItemsTotal(items: DealLineItem[]): number {
  return items.reduce((s, l) => s + l.quantity * l.unitPrice, 0);
}

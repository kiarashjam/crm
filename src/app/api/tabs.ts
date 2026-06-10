// Open tabs / running checks.
//
// A member opens a tab at the bar, restaurant, or spa; staff add items as
// they go; closing the tab converts it to a Charge on the member's house
// account and writes a loyalty ledger entry sized to the total.

import { apiWithFallback, authFetch, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { createCharge, type ChargeKind } from './charges';
import { addLoyaltyEntry, TIER_MULTIPLIERS } from './loyalty';
import { getMembers, type MemberTier } from './members';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type TabVenue = 'Bar' | 'Dining' | 'Spa' | 'Rooftop' | 'PoolDeck';
export type TabStatus = 'Open' | 'Closed' | 'Voided';

export interface TabItem {
  id: string;
  name: string;
  quantity: number;
  unitPrice: number;
  addedAtUtc: string;
}

export interface OpenTab {
  id: string;
  memberId: string;
  memberName: string;
  memberTier: MemberTier;
  venue: TabVenue;
  serverName?: string;
  items: TabItem[];
  status: TabStatus;
  openedAtUtc: string;
  closedAtUtc?: string;
  pointsAwarded?: number;
  total: number;
  notes?: string;
}

export const TAB_VENUES: TabVenue[] = ['Bar', 'Dining', 'Spa', 'Rooftop', 'PoolDeck'];
export const VENUE_LABELS: Record<TabVenue, string> = {
  Bar: 'Library Bar',
  Dining: 'Main Dining',
  Spa: 'Spa',
  Rooftop: 'Rooftop',
  PoolDeck: 'Pool Deck',
};

const VENUE_TO_CHARGE_KIND: Record<TabVenue, ChargeKind> = {
  Bar: 'Bar',
  Dining: 'Dining',
  Spa: 'Spa',
  Rooftop: 'Dining',
  PoolDeck: 'Dining',
};

const hour = 3_600_000;

function calculateTotal(items: TabItem[]): number {
  return Math.round(items.reduce((s, i) => s + i.quantity * i.unitPrice, 0) * 100) / 100;
}

const tab1Items: TabItem[] = [
  { id: 'ti-1a', name: 'Negroni', quantity: 2, unitPrice: 22, addedAtUtc: new Date(Date.now() - 1 * hour).toISOString() },
  { id: 'ti-1b', name: 'Olives', quantity: 1, unitPrice: 12, addedAtUtc: new Date(Date.now() - 50 * 60_000).toISOString() },
];

const tab2Items: TabItem[] = [
  { id: 'ti-2a', name: 'Tasting menu', quantity: 2, unitPrice: 145, addedAtUtc: new Date(Date.now() - 30 * 60_000).toISOString() },
  { id: 'ti-2b', name: 'Wine pairing', quantity: 2, unitPrice: 95, addedAtUtc: new Date(Date.now() - 28 * 60_000).toISOString() },
];

const tabSeed: OpenTab[] = [
  {
    id: 'tab-seed-1',
    memberId: 'mem-seed-2',
    memberName: 'Daniel Okafor',
    memberTier: 'Gold',
    venue: 'Bar',
    serverName: 'Mariah',
    items: tab1Items,
    status: 'Open',
    openedAtUtc: new Date(Date.now() - 1 * hour).toISOString(),
    total: calculateTotal(tab1Items),
  },
  {
    id: 'tab-seed-2',
    memberId: 'mem-seed-1',
    memberName: 'Amelia Hartwell',
    memberTier: 'Platinum',
    venue: 'Dining',
    serverName: 'Tomas',
    items: tab2Items,
    status: 'Open',
    openedAtUtc: new Date(Date.now() - 35 * 60_000).toISOString(),
    total: calculateTotal(tab2Items),
  },
];

const tabStore = createMockStore<OpenTab>({
  storageKey: 'crm.mock.tabs.v1',
  seed: tabSeed,
  idOf: (t) => t.id,
});

export async function getTabs(): Promise<OpenTab[]> {
  return apiWithFallback(
    async () => {
      const res = await authFetchJson<OpenTab[]>('/api/tabs');
      return Array.isArray(res) ? res : [];
    },
    async () => {
      await delay(100);
      return [...tabStore.list()].sort(
        (a, b) => Date.parse(b.openedAtUtc) - Date.parse(a.openedAtUtc),
      );
    },
  );
}

export interface OpenTabInput {
  memberId: string;
  venue: TabVenue;
  serverName?: string;
}

export async function openTab(input: OpenTabInput): Promise<OpenTab | null> {
  const members = await getMembers();
  const member = members.find((m) => m.id === input.memberId);
  if (!member) return null;
  return apiWithFallback(
    () => authFetchJson<OpenTab>('/api/tabs', { method: 'POST', body: JSON.stringify(input) }),
    async () => {
      await delay(140);
      return tabStore.add({
        id: mockId('tab'),
        memberId: member.id,
        memberName: `${member.firstName} ${member.lastName}`,
        memberTier: member.tier,
        venue: input.venue,
        serverName: input.serverName,
        items: [],
        status: 'Open',
        openedAtUtc: new Date().toISOString(),
        total: 0,
      });
    },
  );
}

export interface AddItemInput {
  tabId: string;
  name: string;
  quantity: number;
  unitPrice: number;
}

export async function addItemToTab(input: AddItemInput): Promise<OpenTab | null> {
  const tab = tabStore.byId(input.tabId);
  if (!tab || tab.status !== 'Open') return null;
  const newItem: TabItem = {
    id: mockId('ti'),
    name: input.name,
    quantity: input.quantity,
    unitPrice: input.unitPrice,
    addedAtUtc: new Date().toISOString(),
  };
  const items = [...tab.items, newItem];
  return apiWithFallback(
    () =>
      authFetchJson<OpenTab>(`/api/tabs/${input.tabId}/items`, {
        method: 'POST',
        body: JSON.stringify(newItem),
      }),
    async () => {
      await delay(100);
      return tabStore.update(input.tabId, { items, total: calculateTotal(items) });
    },
  );
}

export async function removeItemFromTab(tabId: string, itemId: string): Promise<OpenTab | null> {
  const tab = tabStore.byId(tabId);
  if (!tab || tab.status !== 'Open') return null;
  const items = tab.items.filter((i) => i.id !== itemId);
  return apiWithFallback(
    async () => {
      const res = await authFetch(`/api/tabs/${tabId}/items/${itemId}`, { method: 'DELETE' });
      if (!(res.status === 204 || res.ok)) throw new Error('failed');
      return tabStore.byId(tabId) ?? null;
    },
    async () => {
      await delay(100);
      return tabStore.update(tabId, { items, total: calculateTotal(items) });
    },
  );
}

/**
 * Close the tab. Converts the running total into a charge on the member's
 * house account and writes a loyalty entry sized by the total × multiplier.
 */
export async function closeTab(tabId: string): Promise<OpenTab | null> {
  const tab = tabStore.byId(tabId);
  if (!tab || tab.status !== 'Open') return null;
  const total = calculateTotal(tab.items);
  if (total > 0) {
    await createCharge({
      memberId: tab.memberId,
      memberName: tab.memberName,
      kind: VENUE_TO_CHARGE_KIND[tab.venue],
      description: `${VENUE_LABELS[tab.venue]} tab (${tab.items.length} items)`,
      amount: total,
    });
  }
  // 10 points / $ × tier multiplier.
  const points = Math.floor(total * 10 * TIER_MULTIPLIERS[tab.memberTier]);
  if (points > 0) {
    await addLoyaltyEntry({
      memberId: tab.memberId,
      memberName: tab.memberName,
      kind: 'Earned',
      reason: tab.venue === 'Bar' ? 'BarPurchase' : 'DiningPurchase',
      points,
      note: `${VENUE_LABELS[tab.venue]} tab — ${tab.items.length} items`,
    });
  }
  return apiWithFallback(
    () => authFetchJson<OpenTab>(`/api/tabs/${tabId}/close`, { method: 'POST' }),
    async () => {
      await delay(140);
      return tabStore.update(tabId, {
        status: 'Closed',
        closedAtUtc: new Date().toISOString(),
        pointsAwarded: points,
      });
    },
  );
}

export async function voidTab(tabId: string): Promise<OpenTab | null> {
  return apiWithFallback(
    () => authFetchJson<OpenTab>(`/api/tabs/${tabId}/void`, { method: 'POST' }),
    async () => {
      await delay(100);
      return tabStore.update(tabId, { status: 'Voided' });
    },
  );
}

export interface TabStats {
  openCount: number;
  openValue: number;
  closedToday: number;
  revenueToday: number;
}

export function computeTabStats(tabs: OpenTab[]): TabStats {
  const startOfToday = new Date();
  startOfToday.setHours(0, 0, 0, 0);
  const startMs = startOfToday.getTime();
  let openCount = 0;
  let openValue = 0;
  let closedToday = 0;
  let revenueToday = 0;
  for (const t of tabs) {
    if (t.status === 'Open') {
      openCount++;
      openValue += t.total;
    } else if (t.status === 'Closed' && t.closedAtUtc && Date.parse(t.closedAtUtc) >= startMs) {
      closedToday++;
      revenueToday += t.total;
    }
  }
  return {
    openCount,
    openValue: Math.round(openValue * 100) / 100,
    closedToday,
    revenueToday: Math.round(revenueToday * 100) / 100,
  };
}

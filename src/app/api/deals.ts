import type { Deal } from './types';
import { mockDeals } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

const STORAGE_KEY = 'crm_deals';

function getStored(): Deal[] | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as Deal[];
  } catch {
    return null;
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

type DealRaw = { id: string; name: string; value: string; stage?: string | null; companyId?: string | null; contactId?: string | null; expectedCloseDateUtc?: string | null; isWon?: boolean | null; lastActivityAtUtc?: string | null };
function mapDeal(d: DealRaw): Deal {
  return {
    id: d.id,
    name: d.name,
    value: d.value,
    stage: d.stage ?? undefined,
    companyId: d.companyId ?? undefined,
    contactId: d.contactId ?? undefined,
    expectedCloseDateUtc: d.expectedCloseDateUtc ?? undefined,
    isWon: d.isWon ?? undefined,
    lastActivityAtUtc: d.lastActivityAtUtc ?? undefined,
  };
}

/** Get all deals (real API or mock). */
export async function getDeals(): Promise<Deal[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; name: string; value: string; stage?: string | null; companyId?: string | null; expectedCloseDateUtc?: string | null; isWon?: boolean | null }[]>('/api/deals');
    return Array.isArray(list) ? list.map(mapDeal) : [];
  }
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockDeals];
}

/** Search deals by name or value. */
export async function searchDeals(query: string): Promise<Deal[]> {
  if (isUsingRealApi()) {
    const q = query?.trim() ? encodeURIComponent(query.trim()) : '';
    const list = await authFetchJson<DealRaw[]>(`/api/deals/search?q=${q}`);
    return Array.isArray(list) ? list.map(mapDeal) : [];
  }
  await delay(200);
  const deals = await getDeals();
  const q = query.trim().toLowerCase();
  if (!q) return deals;
  return deals.filter(
    (d) =>
      d.name.toLowerCase().includes(q) ||
      d.value.toLowerCase().includes(q)
  );
}

/** Create a deal. */
export async function createDeal(params: { name: string; value: string; stage?: string; companyId?: string; contactId?: string; expectedCloseDateUtc?: string }): Promise<Deal | null> {
  if (isUsingRealApi()) {
    const deal = await authFetchJson<{ id: string; name: string; value: string; stage?: string | null; companyId?: string | null; contactId?: string | null; expectedCloseDateUtc?: string | null; isWon?: boolean | null }>('/api/deals', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  return null;
}

/** Update a deal (stage, value, contact, expected close date, won/lost). */
export async function updateDeal(id: string, params: Partial<{ name: string; value: string; stage: string; companyId: string; contactId: string; expectedCloseDateUtc: string; isWon: boolean }>): Promise<Deal | null> {
  if (isUsingRealApi()) {
    const deal = await authFetchJson<DealRaw>(`/api/deals/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  return null;
}

/** Delete a deal. */
export async function deleteDeal(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/deals/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

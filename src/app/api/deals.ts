import type { Deal } from './types';
import { mockDeals } from './mockData';
import { isUsingRealApi, authFetchJson } from './apiClient';

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

function mapDeal(d: { id: string; name: string; value: string; stage?: string | null; companyId?: string | null }): Deal {
  return { id: d.id, name: d.name, value: d.value, stage: d.stage ?? undefined, companyId: d.companyId ?? undefined };
}

/** Get all deals (real API or mock). */
export async function getDeals(): Promise<Deal[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; name: string; value: string; stage?: string | null; companyId?: string | null }[]>('/api/deals');
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
    const list = await authFetchJson<{ id: string; name: string; value: string; stage?: string | null; companyId?: string | null }[]>(`/api/deals/search?q=${q}`);
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

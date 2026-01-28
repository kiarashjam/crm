import type { Deal } from './types';
import { mockDeals } from './mockData';

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

/** Get all deals (demo: from mock or stored). */
export async function getDeals(): Promise<Deal[]> {
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockDeals];
}

/** Search deals by name or value. */
export async function searchDeals(query: string): Promise<Deal[]> {
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

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

import type { Deal, PagedResult, PaginationParams } from './types';
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

type DealRaw = { id: string; name: string; value: string; currency?: string | null; stage?: string | null; pipelineId?: string | null; dealStageId?: string | null; companyId?: string | null; contactId?: string | null; assigneeId?: string | null; expectedCloseDateUtc?: string | null; isWon?: boolean | null; lastActivityAtUtc?: string | null };
function mapDeal(d: DealRaw): Deal {
  return {
    id: d.id,
    name: d.name,
    value: d.value,
    currency: d.currency ?? undefined,
    stage: d.stage ?? undefined,
    pipelineId: d.pipelineId ?? undefined,
    dealStageId: d.dealStageId ?? undefined,
    companyId: d.companyId ?? undefined,
    contactId: d.contactId ?? undefined,
    assigneeId: d.assigneeId ?? undefined,
    expectedCloseDateUtc: d.expectedCloseDateUtc ?? undefined,
    isWon: d.isWon ?? undefined,
    lastActivityAtUtc: d.lastActivityAtUtc ?? undefined,
  };
}

interface ApiPagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

/** Get deals with pagination and optional search (real API or mock). */
export async function getDealsPaged(
  params: PaginationParams = {}
): Promise<PagedResult<Deal>> {
  const { page = 1, pageSize = 20, search } = params;
  
  if (isUsingRealApi()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
    }
    const result = await authFetchJson<ApiPagedResult<DealRaw>>(`/api/deals?${queryParams}`);
    return {
      items: Array.isArray(result?.items) ? result.items.map(mapDeal) : [],
      totalCount: result?.totalCount ?? 0,
      page: result?.page ?? page,
      pageSize: result?.pageSize ?? pageSize,
      totalPages: result?.totalPages ?? 0,
      hasNextPage: result?.hasNextPage ?? false,
      hasPreviousPage: result?.hasPreviousPage ?? false,
    };
  }
  
  // Mock implementation with client-side pagination
  await delay(300);
  let deals = getStored() ?? [...mockDeals];
  
  // Apply search filter
  if (search?.trim()) {
    const qLower = search.trim().toLowerCase();
    deals = deals.filter(
      (d) =>
        d.name.toLowerCase().includes(qLower) ||
        d.value.toLowerCase().includes(qLower)
    );
  }
  
  // Apply pagination
  const totalCount = deals.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const startIndex = (page - 1) * pageSize;
  const items = deals.slice(startIndex, startIndex + pageSize);
  
  return {
    items,
    totalCount,
    page,
    pageSize,
    totalPages,
    hasNextPage: page < totalPages,
    hasPreviousPage: page > 1,
  };
}

/** Get all deals (real API or mock) - non-paginated for backward compatibility. */
export async function getDeals(): Promise<Deal[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<DealRaw[]>('/api/deals/all');
    return Array.isArray(list) ? list.map(mapDeal) : [];
  }
  await delay(300);
  const stored = getStored();
  return stored ?? [...mockDeals];
}

/** Search deals by name or value (non-paginated, for backward compatibility). */
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
export async function createDeal(params: { name: string; value: string; currency?: string; stage?: string; pipelineId?: string; dealStageId?: string; companyId?: string; contactId?: string; assigneeId?: string; expectedCloseDateUtc?: string }): Promise<Deal | null> {
  if (isUsingRealApi()) {
    const deal = await authFetchJson<DealRaw>('/api/deals', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  return null;
}

/** Update a deal (stage, value, contact, expected close date, won/lost, pipeline, assignee). */
export async function updateDeal(id: string, params: Partial<{ name: string; value: string; currency: string; stage: string; pipelineId: string; dealStageId: string; companyId: string; contactId: string; assigneeId: string; expectedCloseDateUtc: string; isWon: boolean }>): Promise<Deal | null> {
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

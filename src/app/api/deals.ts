import type {
  Deal, DealBoard, DealBoardColumn, DealBoardQuery, DealBoardSummary, DealStageChange, PagedResult, PaginationParams,
} from './types';
import type { MoneyRow, WeightedRow } from './reporting';
import { mockDeals } from './mockData';
import { servesDemoData, authFetchJson, authFetch } from './apiClient';
import { createMockStore, demoPatch, mockId } from './mockStore';
import { DEFAULT_CURRENCY, parseMoneyInput } from '@/app/lib/money';
import { getCurrentUser } from '@/app/lib/auth';
import { demoBoard, demoBoardColumn } from './dealBoardDemo';

const dealStore = createMockStore<Deal>({
  storageKey: 'crm.mock.deals.v1',
  seed: mockDeals,
  idOf: (d) => d.id,
});

/**
 * A demo deal with its amount as a number. Deals saved in this browser before amounts
 * were numbers have only their text, so it is read once here, the way the server read
 * its own older deals, and every page then reads the number.
 */
function withAmount(d: Deal): Deal {
  return d.amount === undefined ? { ...d, amount: parseMoneyInput(d.value) } : d;
}

function getStored(): Deal[] | null {
  return dealStore.list()?.map(withAmount) ?? null;
}

/** The text older servers and integrations read, written from the number as the server writes it. */
function valueText(amount: number): string {
  return Number.isInteger(amount) ? String(amount) : amount.toFixed(2);
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

export type DealRaw = {
  id: string; name: string; value: string;
  amount?: number | null;
  currency?: string | null; stage?: string | null;
  pipelineId?: string | null; dealStageId?: string | null;
  companyId?: string | null; contactId?: string | null;
  assigneeId?: string | null; expectedCloseDateUtc?: string | null;
  isWon?: boolean | null; lastActivityAtUtc?: string | null;
  description?: string | null; probability?: number | null;
  assigneeName?: string | null; companyName?: string | null;
  contactName?: string | null; pipelineName?: string | null;
  dealStageName?: string | null;
  createdAtUtc?: string | null; updatedAtUtc?: string | null;
  closedReason?: string | null; closedAtUtc?: string | null;
  isPrivate?: boolean | null;
  stageEnteredAtUtc?: string | null; idleSinceUtc?: string | null;
  isStale?: boolean | null; staleAfterDays?: number | null;
  version?: string | null;
};
export function mapDeal(d: DealRaw): Deal {
  return {
    id: d.id,
    name: d.name,
    value: d.value,
    // A server from before amounts were numbers sends only the text.
    amount: typeof d.amount === 'number' ? d.amount : d.amount === null ? null : parseMoneyInput(d.value),
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
    description: d.description ?? undefined,
    probability: d.probability ?? undefined,
    assigneeName: d.assigneeName ?? undefined,
    companyName: d.companyName ?? undefined,
    contactName: d.contactName ?? undefined,
    pipelineName: d.pipelineName ?? undefined,
    dealStageName: d.dealStageName ?? undefined,
    createdAtUtc: d.createdAtUtc ?? undefined,
    updatedAtUtc: d.updatedAtUtc ?? undefined,
    closedReason: d.closedReason ?? undefined,
    closedAtUtc: d.closedAtUtc ?? undefined,
    isPrivate: d.isPrivate ?? false,
    stageEnteredAtUtc: d.stageEnteredAtUtc ?? undefined,
    idleSinceUtc: d.idleSinceUtc ?? undefined,
    isStale: d.isStale ?? undefined,
    staleAfterDays: d.staleAfterDays ?? undefined,
    version: d.version ?? undefined,
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
  params: PaginationParams & { companyId?: string; contactId?: string } = {}
): Promise<PagedResult<Deal>> {
  const { page = 1, pageSize = 20, search, companyId, contactId } = params;
  
  if (!servesDemoData()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
    }
    if (companyId) {
      queryParams.set('companyId', companyId);
    }
    if (contactId) {
      queryParams.set('contactId', contactId);
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
  let deals = getStored() ?? mockDeals.map(withAmount);
  
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
  if (!servesDemoData()) {
    const list = await authFetchJson<DealRaw[]>('/api/deals/all');
    return Array.isArray(list) ? list.map(mapDeal) : [];
  }
  await delay(300);
  const stored = getStored();
  return stored ?? mockDeals.map(withAmount);
}

/** Search deals by name or value (non-paginated, for backward compatibility). */
export async function searchDeals(query: string): Promise<Deal[]> {
  if (!servesDemoData()) {
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

/** Get a single deal by ID. */
export async function getDealById(id: string): Promise<Deal | null> {
  if (!servesDemoData()) {
    const deal = await authFetchJson<DealRaw>(`/api/deals/${id}`);
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  const deals = await getDeals();
  return deals.find((d) => d.id === id) ?? null;
}

/**
 * Create a deal. The amount is a number to the cent; the server writes the text older
 * clients read from it. Without a currency the deal is priced in the workspace's.
 */
export async function createDeal(params: { name: string; amount: number; currency?: string; stage?: string; pipelineId?: string; dealStageId?: string; companyId?: string; contactId?: string; assigneeId?: string; expectedCloseDateUtc?: string; description?: string; probability?: number }): Promise<Deal | null> {
  if (!servesDemoData()) {
    const deal = await authFetchJson<DealRaw>('/api/deals', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  const created: Deal = {
    id: mockId('deal'),
    name: params.name,
    amount: params.amount,
    value: valueText(params.amount),
    currency: params.currency ?? DEFAULT_CURRENCY,
    stage: params.stage,
    pipelineId: params.pipelineId,
    dealStageId: params.dealStageId,
    companyId: params.companyId,
    contactId: params.contactId,
    assigneeId: params.assigneeId,
    expectedCloseDateUtc: params.expectedCloseDateUtc,
    description: params.description,
    probability: params.probability,
    createdAtUtc: new Date().toISOString(),
  } as Deal;
  return dealStore.add(created);
}

/**
 * Fields a deal update can send. A field left out is kept; the company, contact, owner,
 * expected close date, description and probability are taken off when sent as null.
 * `version` is the deal as the edit read it: with it, a save made after someone else
 * saved the deal is refused (409, see lib/editConflict.ts). An edit dialog sends it; a
 * change to one field that reads nothing else, such as a move on the board, does not.
 */
export type DealUpdate = Partial<{
  name: string; amount: number; currency: string; stage: string; pipelineId: string; dealStageId: string;
  companyId: string | null; contactId: string | null; assigneeId: string | null; expectedCloseDateUtc: string | null;
  isWon: boolean; description: string | null; probability: number | null; closedReason: string; isPrivate: boolean;
  version: string;
}>;

/** Update a deal (stage, amount, contact, expected close date, won/lost, pipeline, assignee, description, probability, close reason). */
export async function updateDeal(id: string, params: DealUpdate): Promise<Deal | null> {
  if (!servesDemoData()) {
    const deal = await authFetchJson<DealRaw>(`/api/deals/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return deal ? mapDeal(deal) : null;
  }
  await delay(200);
  const money = typeof params.amount === 'number' ? { amount: params.amount, value: valueText(params.amount) } : {};
  const updated = dealStore.update(id, { ...demoPatch<Deal>(params), ...money, updatedAtUtc: new Date().toISOString() });
  return updated ? withAmount(updated) : updated;
}

/** Delete a deal. */
export async function deleteDeal(id: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/deals/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return dealStore.remove(id);
}

type BoardColumnRaw = Omit<DealBoardColumn, 'deals' | 'dealStageId' | 'probability'> & {
  dealStageId?: string | null; probability?: number | null; deals: DealRaw[];
};
type BoardRaw = Omit<DealBoard, 'columns' | 'pipelineId' | 'pipelineName' | 'summary'> & {
  pipelineId?: string | null; pipelineName?: string | null; columns: BoardColumnRaw[];
  summary: Omit<DealBoardSummary, 'winRate'> & { winRate?: number | null };
};

function boardParams(query: DealBoardQuery, extra: Record<string, string | number> = {}): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries({ ...query, ...extra })) {
    if (value === undefined || value === null || value === '' || value === false) continue;
    params.set(key, String(value));
  }
  return params.toString();
}

function mapColumn(c: BoardColumnRaw): DealBoardColumn {
  return {
    ...c,
    dealStageId: c.dealStageId ?? undefined,
    probability: c.probability ?? undefined,
    totals: (c.totals ?? []) as MoneyRow[],
    weighted: (c.weighted ?? []) as WeightedRow[],
    deals: (c.deals ?? []).map(mapDeal),
  };
}

function demoCurrency(): string {
  return DEFAULT_CURRENCY;
}

/**
 * The board for one pipeline: its stages with the first cards of each, every stage's
 * count, money per currency, weighted value and stale deals, and the pipeline's figures.
 * The filters narrow the columns; the summary is the whole pipeline. In demo mode it is
 * worked out here from this browser's deals, by the same rules.
 */
export async function getDealBoard(query: DealBoardQuery = {}): Promise<DealBoard> {
  if (!servesDemoData()) {
    const raw = await authFetchJson<BoardRaw>(`/api/deals/board?${boardParams(query)}`);
    return {
      ...raw,
      pipelineId: raw.pipelineId ?? undefined,
      pipelineName: raw.pipelineName ?? undefined,
      columns: (raw.columns ?? []).map(mapColumn),
      summary: { ...raw.summary, winRate: raw.summary.winRate ?? undefined },
    };
  }
  await delay(150);
  return demoBoard(getStored() ?? mockDeals.map(withAmount), query, demoCurrency(), getCurrentUser()?.id);
}

/** The next cards of one board column, after the first `skip`, with the board's filters and order. */
export async function getDealBoardColumn(query: DealBoardQuery, column: string, skip: number): Promise<{ deals: Deal[]; hasMore: boolean }> {
  if (!servesDemoData()) {
    const raw = await authFetchJson<{ key: string; deals: DealRaw[]; hasMore: boolean }>(
      `/api/deals/board/column?${boardParams(query, { column, skip })}`);
    return { deals: (raw.deals ?? []).map(mapDeal), hasMore: !!raw.hasMore };
  }
  await delay(100);
  return demoBoardColumn(getStored() ?? mockDeals.map(withAmount), query, column, skip, getCurrentUser()?.id);
}

type StageChangeRaw = Omit<DealStageChange, 'fromStageId' | 'fromStageName' | 'toStageId' | 'toStageName' | 'changedByName' | 'leftAtUtc'> & {
  fromStageId?: string | null; fromStageName?: string | null; toStageId?: string | null; toStageName?: string | null;
  changedByName?: string | null; leftAtUtc?: string | null;
};

/**
 * The stages a deal has been in, newest first: when it came into each, from where, who
 * moved it and when it left. Demo deals keep no history.
 */
export async function getDealStageHistory(id: string): Promise<DealStageChange[]> {
  if (servesDemoData()) return [];
  const list = await authFetchJson<StageChangeRaw[]>(`/api/deals/${id}/stage-history`);
  return (Array.isArray(list) ? list : []).map((c) => ({
    id: c.id,
    fromStageId: c.fromStageId ?? undefined,
    fromStageName: c.fromStageName ?? undefined,
    toStageId: c.toStageId ?? undefined,
    toStageName: c.toStageName ?? undefined,
    changedByUserId: c.changedByUserId,
    changedByName: c.changedByName ?? undefined,
    changedAtUtc: c.changedAtUtc,
    leftAtUtc: c.leftAtUtc ?? undefined,
    isCreation: !!c.isCreation,
  }));
}

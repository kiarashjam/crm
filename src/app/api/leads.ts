import type { Lead, PagedResult, PaginationParams } from './types';
import { mockLeads } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';
import { createMockStore, mockId } from './mockStore';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

const leadStore = createMockStore<Lead>({
  storageKey: 'crm.mock.leads.v1',
  seed: mockLeads,
  idOf: (l) => l.id,
});

type LeadRaw = {
  id: string;
  name: string;
  email: string;
  phone?: string | null;
  companyId?: string | null;
  companyName?: string | null;
  source?: string | null;
  status: string;
  leadSourceId?: string | null;
  leadStatusId?: string | null;
  leadScore?: number | null;
  lastContactedAt?: string | null;
  description?: string | null;
  lifecycleStage?: string | null;
  isConverted?: boolean;
  convertedAtUtc?: string | null;
  createdAtUtc?: string | null;
  assignedToUserId?: string | null;
  assignedToName?: string | null;
};

function mapLead(d: LeadRaw): Lead {
  return {
    id: d.id,
    name: d.name,
    email: d.email,
    phone: d.phone ?? undefined,
    companyId: d.companyId ?? undefined,
    companyName: d.companyName ?? undefined,
    source: d.source ?? undefined,
    status: d.status,
    leadSourceId: d.leadSourceId ?? undefined,
    leadStatusId: d.leadStatusId ?? undefined,
    leadScore: d.leadScore ?? undefined,
    lastContactedAt: d.lastContactedAt ?? undefined,
    description: d.description ?? undefined,
    lifecycleStage: d.lifecycleStage ?? undefined,
    isConverted: d.isConverted ?? false,
    convertedAtUtc: d.convertedAtUtc ?? undefined,
    createdAtUtc: d.createdAtUtc ?? undefined,
    assignedToId: d.assignedToUserId ?? undefined,
    assignedToName: d.assignedToName ?? undefined,
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

export interface LeadListParams extends PaginationParams {
  status?: string;
  source?: string;
  converted?: 'all' | 'active' | 'converted';
  sortBy?: 'name' | 'email' | 'status' | 'createdAt';
  sortDir?: 'asc' | 'desc';
}

export interface LeadStats {
  total: number;
  converted: number;
  active: number;
  newLeads: number;
  contacted: number;
  qualified: number;
  conversionRate: number;
  thisWeek: number;
  hotLeads: number;
}

function applyMockLeadFilters(leads: Lead[], params: LeadListParams): Lead[] {
  let result = [...leads];
  const { search, status, source, converted, sortBy = 'createdAt', sortDir = 'desc' } = params;

  if (search?.trim()) {
    const q = search.trim().toLowerCase();
    result = result.filter(
      (l) =>
        l.name.toLowerCase().includes(q) ||
        l.email.toLowerCase().includes(q) ||
        (l.phone && l.phone.includes(q)),
    );
  }
  if (status && status !== 'all') {
    result = result.filter((l) => l.status === status);
  }
  if (source && source !== 'all') {
    result = result.filter((l) => l.source === source);
  }
  if (converted === 'converted') {
    result = result.filter((l) => l.isConverted);
  } else if (converted === 'active') {
    result = result.filter((l) => !l.isConverted);
  }

  const toTs = (value?: string) => {
    if (!value) return Number.NEGATIVE_INFINITY;
    const ts = Date.parse(value);
    return Number.isNaN(ts) ? Number.NEGATIVE_INFINITY : ts;
  };

  result.sort((a, b) => {
    let cmp = 0;
    switch (sortBy) {
      case 'email':
        cmp = a.email.localeCompare(b.email, undefined, { sensitivity: 'base' });
        break;
      case 'status':
        cmp = a.status.localeCompare(b.status, undefined, { sensitivity: 'base' });
        break;
      case 'createdAt':
        cmp = toTs(a.createdAtUtc) - toTs(b.createdAtUtc);
        break;
      default:
        cmp = a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
    }
    if (cmp === 0) cmp = a.name.localeCompare(b.name, undefined, { sensitivity: 'base' });
    return sortDir === 'asc' ? cmp : -cmp;
  });

  return result;
}

/** Get leads with pagination, search, filters, and sorting. */
export async function getLeadsPaged(params: LeadListParams = {}): Promise<PagedResult<Lead>> {
  const {
    page = 1,
    pageSize = 20,
    search,
    status,
    source,
    converted,
    sortBy = 'createdAt',
    sortDir = 'desc',
  } = params;

  if (isUsingRealApi()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
      sortBy,
      sortDir,
    });
    if (search?.trim()) queryParams.set('search', search.trim());
    if (status && status !== 'all') queryParams.set('status', status);
    if (source && source !== 'all') queryParams.set('source', source);
    if (converted && converted !== 'all') queryParams.set('converted', converted);

    const result = await authFetchJson<ApiPagedResult<LeadRaw>>(`/api/leads?${queryParams}`);
    return {
      items: Array.isArray(result?.items) ? result.items.map(mapLead) : [],
      totalCount: result?.totalCount ?? 0,
      page: result?.page ?? page,
      pageSize: result?.pageSize ?? pageSize,
      totalPages: result?.totalPages ?? 0,
      hasNextPage: result?.hasNextPage ?? false,
      hasPreviousPage: result?.hasPreviousPage ?? false,
    };
  }

  await delay(300);
  const filtered = applyMockLeadFilters(leadStore.list(), params);
  const totalCount = filtered.length;
  const totalPages = Math.ceil(totalCount / pageSize) || 0;
  const startIndex = (page - 1) * pageSize;
  const items = filtered.slice(startIndex, startIndex + pageSize);

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

/** Aggregate lead statistics for the current organization. */
export async function getLeadStats(): Promise<LeadStats> {
  if (isUsingRealApi()) {
    const s = await authFetchJson<{
      total: number;
      converted: number;
      active: number;
      newLeads: number;
      contacted: number;
      qualified: number;
      conversionRate: number;
      thisWeek: number;
      hotLeads: number;
    }>('/api/leads/stats');
    return {
      total: s?.total ?? 0,
      converted: s?.converted ?? 0,
      active: s?.active ?? 0,
      newLeads: s?.newLeads ?? 0,
      contacted: s?.contacted ?? 0,
      qualified: s?.qualified ?? 0,
      conversionRate: s?.conversionRate ?? 0,
      thisWeek: s?.thisWeek ?? 0,
      hotLeads: s?.hotLeads ?? 0,
    };
  }
  await delay(150);
  const leads = leadStore.list();
  const total = leads.length;
  const converted = leads.filter((l) => l.isConverted).length;
  const active = total - converted;
  const newLeads = leads.filter((l) => l.status === 'New').length;
  const contacted = leads.filter(
    (l) => l.status === 'Contacted' || l.status === 'Attempted Contact' || l.status === 'Connected',
  ).length;
  const qualified = leads.filter((l) => l.status === 'Qualified').length;
  const oneWeekAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
  const thisWeek = leads.filter((l) => l.createdAtUtc && Date.parse(l.createdAtUtc) >= oneWeekAgo).length;
  const hotLeads = leads.filter((l) => (l.leadScore ?? 0) >= 70 && !l.isConverted).length;
  return {
    total,
    converted,
    active,
    newLeads,
    contacted,
    qualified,
    conversionRate: total > 0 ? Math.round((converted / total) * 100) : 0,
    thisWeek,
    hotLeads,
  };
}

/** Get a single lead by id. */
export async function getLeadById(id: string): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>(`/api/leads/${id}`);
    return lead ? mapLead(lead) : null;
  }
  await delay(150);
  return leadStore.list().find((l) => l.id === id) ?? null;
}

/** Get all leads (real API or mock) - non-paginated for export and legacy callers. */
export async function getLeads(): Promise<Lead[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<LeadRaw[]>('/api/leads/all');
    return Array.isArray(list) ? list.map(mapLead) : [];
  }
  await delay(300);
  return leadStore.list();
}

/** Search leads by name or email (non-paginated, for backward compatibility). */
export async function searchLeads(query: string): Promise<Lead[]> {
  if (isUsingRealApi()) {
    const q = query?.trim() ? encodeURIComponent(query.trim()) : '';
    const list = await authFetchJson<LeadRaw[]>(`/api/leads/search?q=${q}`);
    return Array.isArray(list) ? list.map(mapLead) : [];
  }
  await delay(200);
  const leads = await getLeads();
  const q = query.trim().toLowerCase();
  if (!q) return leads;
  return leads.filter((l) => l.name.toLowerCase().includes(q) || l.email.toLowerCase().includes(q));
}

/** Create a lead. */
export async function createLead(params: {
  name: string;
  email: string;
  phone?: string;
  companyId?: string;
  source?: string;
  status?: string;
  leadSourceId?: string;
  leadStatusId?: string;
  leadScore?: number;
  lastContactedAt?: string;
  description?: string;
  lifecycleStage?: string;
}): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>('/api/leads', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  const created: Lead = {
    id: mockId('lead'),
    name: params.name,
    email: params.email,
    phone: params.phone,
    companyId: params.companyId,
    source: params.source,
    status: params.status ?? 'New',
    leadSourceId: params.leadSourceId,
    leadStatusId: params.leadStatusId,
    leadScore: params.leadScore,
    lastContactedAt: params.lastContactedAt,
    description: params.description,
    lifecycleStage: params.lifecycleStage,
    isConverted: false,
    convertedAtUtc: undefined,
    createdAtUtc: new Date().toISOString(),
  };
  return leadStore.add(created);
}

/** Update a lead. */
export async function updateLead(
  id: string,
  params: Partial<{
    name: string;
    email: string;
    phone: string;
    companyId: string;
    source: string;
    status: string;
    leadSourceId: string;
    leadStatusId: string;
    leadScore: number;
    lastContactedAt: string;
    description: string;
    lifecycleStage: string;
  }>,
): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>(`/api/leads/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  return leadStore.update(id, params as Partial<Lead>);
}

/**
 * Assign (or unassign) a lead's owner. Persisted server-side so every member of
 * the organization sees the same assignment — pass null to clear it. Falls back
 * to the local mock store in demo mode (no backend configured).
 */
export async function assignLead(id: string, assignedToUserId: string | null): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>(`/api/leads/${id}/assign`, {
      method: 'PUT',
      body: JSON.stringify({ assignedToUserId }),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  return leadStore.update(id, { assignedToId: assignedToUserId ?? undefined } as Partial<Lead>);
}

/** Delete a lead. */
export async function deleteLead(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/leads/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return leadStore.remove(id);
}

/** Convert a lead to company/contact/deal (create or attach existing). */
export interface ConvertLeadRequest {
  createContact: boolean;
  createDeal: boolean;
  dealName?: string;
  dealValue?: string;
  dealStage?: string;
  pipelineId?: string;
  dealStageId?: string;
  createNewCompany?: boolean;
  newCompanyName?: string;
  existingCompanyId?: string;
  existingContactId?: string;
  existingDealId?: string;
}

export interface ConvertLeadResult {
  companyId?: string | null;
  contactId?: string | null;
  dealId?: string | null;
}

export async function convertLead(leadId: string, request: ConvertLeadRequest): Promise<ConvertLeadResult | null> {
  if (isUsingRealApi()) {
    const result = await authFetchJson<{
      companyId?: string | null;
      contactId?: string | null;
      dealId?: string | null;
    }>(`/api/leads/${leadId}/convert`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return result
      ? { companyId: result.companyId ?? null, contactId: result.contactId ?? null, dealId: result.dealId ?? null }
      : null;
  }
  await delay(200);
  return null;
}

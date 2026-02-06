import type { Lead, PagedResult, PaginationParams } from './types';
import { mockLeads } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

type LeadRaw = { id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string; leadSourceId?: string | null; leadStatusId?: string | null; leadScore?: number | null; lastContactedAt?: string | null; description?: string | null; lifecycleStage?: string | null; isConverted?: boolean; convertedAtUtc?: string | null };
function mapLead(d: LeadRaw): Lead {
  return {
    id: d.id,
    name: d.name,
    email: d.email,
    phone: d.phone ?? undefined,
    companyId: d.companyId ?? undefined,
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

/** Get leads with pagination and optional search (real API or mock). */
export async function getLeadsPaged(
  params: PaginationParams = {}
): Promise<PagedResult<Lead>> {
  const { page = 1, pageSize = 20, search } = params;
  
  if (isUsingRealApi()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
    }
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
  
  // Mock implementation with client-side pagination
  await delay(300);
  let leads = [...mockLeads];
  
  // Apply search filter
  if (search?.trim()) {
    const qLower = search.trim().toLowerCase();
    leads = leads.filter(
      (l) =>
        l.name.toLowerCase().includes(qLower) ||
        l.email.toLowerCase().includes(qLower)
    );
  }
  
  // Apply pagination
  const totalCount = leads.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const startIndex = (page - 1) * pageSize;
  const items = leads.slice(startIndex, startIndex + pageSize);
  
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

/** Get all leads (real API or mock) - non-paginated for backward compatibility. */
export async function getLeads(): Promise<Lead[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<LeadRaw[]>('/api/leads/all');
    return Array.isArray(list) ? list.map(mapLead) : [];
  }
  await delay(300);
  return [...mockLeads];
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
export async function createLead(params: { name: string; email: string; phone?: string; companyId?: string; source?: string; status?: string; leadSourceId?: string; leadStatusId?: string; leadScore?: number; lastContactedAt?: string; description?: string; lifecycleStage?: string }): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>('/api/leads', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  return null;
}

/** Update a lead. */
export async function updateLead(id: string, params: Partial<{ name: string; email: string; phone: string; companyId: string; source: string; status: string; leadSourceId: string; leadStatusId: string; leadScore: number; lastContactedAt: string; description: string; lifecycleStage: string }>): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<LeadRaw>(`/api/leads/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  return null;
}

/** Delete a lead. */
export async function deleteLead(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/leads/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
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
    const result = await authFetchJson<{ companyId?: string | null; contactId?: string | null; dealId?: string | null }>(`/api/leads/${leadId}/convert`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return result ? { companyId: result.companyId ?? null, contactId: result.contactId ?? null, dealId: result.dealId ?? null } : null;
  }
  await delay(200);
  return null;
}

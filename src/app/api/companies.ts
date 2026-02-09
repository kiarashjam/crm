import type { Company, PagedResult, PaginationParams } from './types';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';
import { mockCompanies } from './mockData';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

type CompanyRaw = {
  id: string;
  name: string;
  domain?: string | null;
  industry?: string | null;
  size?: string | null;
  description?: string | null;
  website?: string | null;
  location?: string | null;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
};
function mapCompany(c: CompanyRaw): Company {
  return {
    id: c.id,
    name: c.name,
    domain: c.domain ?? undefined,
    industry: c.industry ?? undefined,
    size: c.size ?? undefined,
    description: c.description ?? undefined,
    website: c.website ?? undefined,
    location: c.location ?? undefined,
    createdAtUtc: c.createdAtUtc ?? undefined,
    updatedAtUtc: c.updatedAtUtc ?? undefined,
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

/** Get companies with pagination and optional search (real API or mock). */
export async function getCompaniesPaged(
  params: PaginationParams = {}
): Promise<PagedResult<Company>> {
  const { page = 1, pageSize = 20, search } = params;
  
  if (isUsingRealApi()) {
    const queryParams = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize),
    });
    if (search?.trim()) {
      queryParams.set('search', search.trim());
    }
    const result = await authFetchJson<ApiPagedResult<CompanyRaw>>(`/api/companies?${queryParams}`);
    return {
      items: Array.isArray(result?.items) ? result.items.map(mapCompany) : [],
      totalCount: result?.totalCount ?? 0,
      page: result?.page ?? page,
      pageSize: result?.pageSize ?? pageSize,
      totalPages: result?.totalPages ?? 0,
      hasNextPage: result?.hasNextPage ?? false,
      hasPreviousPage: result?.hasPreviousPage ?? false,
    };
  }
  
  // Mock implementation with client-side pagination
  await delay(200);
  let companies = [...mockCompanies];
  
  // Apply search filter
  if (search?.trim()) {
    const qLower = search.trim().toLowerCase();
    companies = companies.filter(
      (c) =>
        c.name.toLowerCase().includes(qLower) ||
        (c.domain && c.domain.toLowerCase().includes(qLower)) ||
        (c.industry && c.industry.toLowerCase().includes(qLower))
    );
  }
  
  // Apply pagination
  const totalCount = companies.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const startIndex = (page - 1) * pageSize;
  const items = companies.slice(startIndex, startIndex + pageSize);
  
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

/** Get all companies (real API or mock) - non-paginated for backward compatibility. */
export async function getCompanies(): Promise<Company[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<CompanyRaw[]>('/api/companies/all');
    return Array.isArray(list) ? list.map(mapCompany) : [];
  }
  await delay(200);
  return [...mockCompanies];
}

/** Create a company. */
export async function createCompany(params: { name: string; domain?: string; industry?: string; size?: string; description?: string; website?: string; location?: string }): Promise<Company | null> {
  if (isUsingRealApi()) {
    const company = await authFetchJson<CompanyRaw>('/api/companies', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return company ? mapCompany(company) : null;
  }
  await delay(200);
  return null;
}

/** Update a company. */
export async function updateCompany(id: string, params: { name?: string; domain?: string; industry?: string; size?: string; description?: string; website?: string; location?: string }): Promise<Company | null> {
  if (isUsingRealApi()) {
    const company = await authFetchJson<CompanyRaw>(`/api/companies/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return company ? mapCompany(company) : null;
  }
  await delay(200);
  return null;
}

// HP-7: Server-side per-company statistics
export interface CompanyStatsItem {
  companyId: string;
  contactCount: number;
  dealCount: number;
  totalDealValue: number;
}

/** Fetch per-company stats (contact count, deal count, deal value). */
export async function getCompanyStats(): Promise<CompanyStatsItem[]> {
  if (isUsingRealApi()) {
    const result = await authFetchJson<CompanyStatsItem[]>('/api/companies/stats');
    return Array.isArray(result) ? result : [];
  }
  // Mock: return empty array (mock companies don't have linked contacts/deals)
  await delay(100);
  return [];
}

/** Delete a company. */
export async function deleteCompany(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/companies/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

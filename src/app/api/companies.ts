import type { Company, CurrencyTotal, PagedResult, PaginationParams } from './types';
import { servesDemoData, authFetchJson, authFetch, ensureOk } from './apiClient';
import { mockCompanies } from './mockData';
import { exportCounts, type ExportResult } from './contacts';
import { downloadCsv, downloadFile } from '@/app/lib/exportCsv';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

export type CompanyRaw = {
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
export function mapCompany(c: CompanyRaw): Company {
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

export type CompanySortField = 'name' | 'createdAt' | 'contacts' | 'deals';

/** What the companies list asks for. The server applies all of it to the whole workspace. */
export interface CompanyListFilters {
  search?: string;
  /** One industry, matched ignoring case. */
  industry?: string;
  /** false: only companies with no industry. */
  hasIndustry?: boolean;
  size?: string;
  sortBy?: CompanySortField;
  sortDir?: 'asc' | 'desc';
}

/** A company in the list, with its active contacts and the deals the viewer may see counted on the server. */
export interface CompanyListItem extends Company {
  contactCount: number;
  dealCount: number;
  /** One total per currency, largest first. Never added across currencies. */
  dealTotals: CurrencyTotal[];
}

/** The figures above the companies list, over the whole workspace. */
export interface CompanySummary {
  total: number;
  withContacts: number;
  withDeals: number;
  addedLast7Days: number;
  dealTotals: CurrencyTotal[];
  /** The industries the workspace's companies are in, as typed, most companies first. */
  industries: { industry: string; count: number }[];
  withoutIndustry: number;
}

type CompanyListItemRaw = CompanyRaw & { contactCount?: number; dealCount?: number; dealTotals?: CurrencyTotal[] | null };

function mapListItem(c: CompanyListItemRaw): CompanyListItem {
  return {
    ...mapCompany(c),
    contactCount: c.contactCount ?? 0,
    dealCount: c.dealCount ?? 0,
    dealTotals: Array.isArray(c.dealTotals) ? c.dealTotals : [],
  };
}

function companyListQuery(filters: CompanyListFilters): URLSearchParams {
  const query = new URLSearchParams();
  if (filters.search?.trim()) query.set('search', filters.search.trim());
  if (filters.industry?.trim()) query.set('industry', filters.industry.trim());
  if (filters.hasIndustry !== undefined) query.set('hasIndustry', String(filters.hasIndustry));
  if (filters.size) query.set('size', filters.size);
  if (filters.sortBy) query.set('sortBy', filters.sortBy);
  if (filters.sortDir) query.set('sortDir', filters.sortDir);
  return query;
}

/** Demo mode: the same filters and order as the server, over the sample companies. */
function demoCompanyList(filters: CompanyListFilters): CompanyListItem[] {
  let companies = [...mockCompanies];
  if (filters.search?.trim()) {
    const qLower = filters.search.trim().toLowerCase();
    companies = companies.filter(
      (c) =>
        c.name.toLowerCase().includes(qLower) ||
        (c.domain && c.domain.toLowerCase().includes(qLower)) ||
        (c.industry && c.industry.toLowerCase().includes(qLower))
    );
  }
  if (filters.industry?.trim()) {
    const industry = filters.industry.trim().toLowerCase();
    companies = companies.filter((c) => c.industry?.trim().toLowerCase() === industry);
  } else if (filters.hasIndustry !== undefined) {
    companies = companies.filter((c) => !!c.industry?.trim() === filters.hasIndustry);
  }
  if (filters.size) companies = companies.filter((c) => c.size === filters.size);
  const dir = filters.sortDir === 'desc' ? -1 : 1;
  const time = (v?: string) => (v ? new Date(v).getTime() : 0);
  companies.sort((a, b) => dir * (filters.sortBy === 'createdAt' ? time(a.createdAtUtc) - time(b.createdAtUtc) : a.name.localeCompare(b.name))
    || a.name.localeCompare(b.name));
  // The sample companies have no contacts or deals linked.
  return companies.map((c) => ({ ...c, contactCount: 0, dealCount: 0, dealTotals: [] }));
}

/** Get companies with pagination, filters and order, each with its counts (real API or mock). */
export async function getCompaniesPaged(
  params: PaginationParams & CompanyListFilters = {}
): Promise<PagedResult<CompanyListItem>> {
  const { page = 1, pageSize = 20, ...filters } = params;

  if (!servesDemoData()) {
    const queryParams = companyListQuery(filters);
    queryParams.set('page', String(page));
    queryParams.set('pageSize', String(pageSize));
    const result = await authFetchJson<ApiPagedResult<CompanyListItemRaw>>(`/api/companies?${queryParams}`);
    return {
      items: Array.isArray(result?.items) ? result.items.map(mapListItem) : [],
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
  const companies = demoCompanyList(filters);
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

/** The figures above the companies list, counted over the whole workspace. */
export async function getCompanySummary(): Promise<CompanySummary> {
  if (!servesDemoData()) {
    const summary = await authFetchJson<CompanySummary>('/api/companies/summary');
    return {
      total: summary?.total ?? 0,
      withContacts: summary?.withContacts ?? 0,
      withDeals: summary?.withDeals ?? 0,
      addedLast7Days: summary?.addedLast7Days ?? 0,
      dealTotals: Array.isArray(summary?.dealTotals) ? summary.dealTotals : [],
      industries: Array.isArray(summary?.industries) ? summary.industries : [],
      withoutIndustry: summary?.withoutIndustry ?? 0,
    };
  }
  const all = demoCompanyList({});
  const counts = new Map<string, { industry: string; count: number }>();
  for (const c of all) {
    const industry = c.industry?.trim();
    if (!industry) continue;
    const key = industry.toLowerCase();
    const row = counts.get(key) ?? { industry, count: 0 };
    row.count++;
    counts.set(key, row);
  }
  const weekAgo = Date.now() - 7 * 24 * 60 * 60 * 1000;
  return {
    total: all.length,
    withContacts: 0,
    withDeals: 0,
    addedLast7Days: all.filter((c) => c.createdAtUtc && new Date(c.createdAtUtc).getTime() >= weekAgo).length,
    dealTotals: [],
    industries: [...counts.values()].sort((a, b) => b.count - a.count || a.industry.localeCompare(b.industry)),
    withoutIndustry: all.filter((c) => !c.industry?.trim()).length,
  };
}

/** Every company the list shows with these filters, as a CSV download: all of them, not one page. */
export async function exportCompanies(filters: CompanyListFilters): Promise<ExportResult | null> {
  const day = new Date().toISOString().slice(0, 10);
  if (!servesDemoData()) {
    const res = await authFetch(`/api/companies/export?${companyListQuery(filters)}`);
    await ensureOk(res);
    downloadFile(await res.blob(), `companies-${day}.csv`);
    return exportCounts(res);
  }
  const rows = demoCompanyList(filters);
  downloadCsv(`companies-${day}`, rows, [
    { header: 'Name', value: (c) => c.name },
    { header: 'Domain', value: (c) => c.domain },
    { header: 'Industry', value: (c) => c.industry },
    { header: 'Size', value: (c) => c.size },
    { header: 'Website', value: (c) => c.website },
    { header: 'Location', value: (c) => c.location },
    { header: 'Added', value: (c) => c.createdAtUtc },
  ]);
  return { exported: rows.length, total: rows.length };
}

/** Get all companies (real API or mock) - non-paginated for backward compatibility. */
export async function getCompanies(): Promise<Company[]> {
  if (!servesDemoData()) {
    const list = await authFetchJson<CompanyRaw[]>('/api/companies/all');
    return Array.isArray(list) ? list.map(mapCompany) : [];
  }
  await delay(200);
  return [...mockCompanies];
}

/** Create a company. */
export async function createCompany(params: { name: string; domain?: string; industry?: string; size?: string; description?: string; website?: string; location?: string }): Promise<Company | null> {
  if (!servesDemoData()) {
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
  if (!servesDemoData()) {
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
  /** The company's deals, one total per currency, largest first. Never added across currencies. */
  dealTotals: CurrencyTotal[];
}

/** Fetch per-company stats (contact count, deal count, deal value per currency). */
export async function getCompanyStats(): Promise<CompanyStatsItem[]> {
  if (!servesDemoData()) {
    const result = await authFetchJson<CompanyStatsItem[]>('/api/companies/stats');
    // A server from before the totals were per currency sends none: no figure, not a wrong one.
    return Array.isArray(result) ? result.map((s) => ({ ...s, dealTotals: Array.isArray(s.dealTotals) ? s.dealTotals : [] })) : [];
  }
  // Mock: return empty array (mock companies don't have linked contacts/deals)
  await delay(100);
  return [];
}

/** Delete a company. */
export async function deleteCompany(id: string): Promise<boolean> {
  if (!servesDemoData()) {
    const res = await authFetch(`/api/companies/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

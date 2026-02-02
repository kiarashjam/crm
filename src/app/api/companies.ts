import type { Company } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';
import { mockCompanies } from './mockData';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

/** Get all companies (real API or mock). */
export async function getCompanies(): Promise<Company[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; name: string }[]>('/api/companies');
    return Array.isArray(list) ? list.map((c) => ({ id: c.id, name: c.name })) : [];
  }
  await delay(200);
  return [...mockCompanies];
}

/** Create a company. */
export async function createCompany(params: { name: string }): Promise<Company | null> {
  if (isUsingRealApi()) {
    const company = await authFetchJson<{ id: string; name: string }>('/api/companies', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return company ? { id: company.id, name: company.name } : null;
  }
  await delay(200);
  return null;
}

/** Update a company. */
export async function updateCompany(id: string, params: { name: string }): Promise<Company | null> {
  if (isUsingRealApi()) {
    const company = await authFetchJson<{ id: string; name: string }>(`/api/companies/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return company ? { id: company.id, name: company.name } : null;
  }
  await delay(200);
  return null;
}

import type { Lead } from './types';
import { mockLeads } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function mapLead(d: { id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string }): Lead {
  return {
    id: d.id,
    name: d.name,
    email: d.email,
    phone: d.phone ?? undefined,
    companyId: d.companyId ?? undefined,
    source: d.source ?? undefined,
    status: d.status,
  };
}

/** Get all leads (real API or mock). */
export async function getLeads(): Promise<Lead[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string }[]>('/api/leads');
    return Array.isArray(list) ? list.map(mapLead) : [];
  }
  await delay(300);
  return [...mockLeads];
}

/** Search leads by name or email. */
export async function searchLeads(query: string): Promise<Lead[]> {
  if (isUsingRealApi()) {
    const q = query?.trim() ? encodeURIComponent(query.trim()) : '';
    const list = await authFetchJson<{ id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string }[]>(`/api/leads/search?q=${q}`);
    return Array.isArray(list) ? list.map(mapLead) : [];
  }
  await delay(200);
  const leads = await getLeads();
  const q = query.trim().toLowerCase();
  if (!q) return leads;
  return leads.filter((l) => l.name.toLowerCase().includes(q) || l.email.toLowerCase().includes(q));
}

/** Create a lead. */
export async function createLead(params: { name: string; email: string; phone?: string; companyId?: string; source?: string; status?: string }): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<{ id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string }>('/api/leads', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return lead ? mapLead(lead) : null;
  }
  await delay(200);
  return null;
}

/** Update a lead. */
export async function updateLead(id: string, params: Partial<{ name: string; email: string; phone: string; companyId: string; source: string; status: string }>): Promise<Lead | null> {
  if (isUsingRealApi()) {
    const lead = await authFetchJson<{ id: string; name: string; email: string; phone?: string | null; companyId?: string | null; source?: string | null; status: string }>(`/api/leads/${id}`, {
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

/** Convert a lead to contact and/or deal. */
export interface ConvertLeadRequest {
  createContact: boolean;
  createDeal: boolean;
  dealName?: string;
  dealValue?: string;
  dealStage?: string;
}

export interface ConvertLeadResult {
  contactId?: string | null;
  dealId?: string | null;
}

export async function convertLead(leadId: string, request: ConvertLeadRequest): Promise<ConvertLeadResult | null> {
  if (isUsingRealApi()) {
    const result = await authFetchJson<{ contactId?: string | null; dealId?: string | null }>(`/api/leads/${leadId}/convert`, {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return result ? { contactId: result.contactId ?? null, dealId: result.dealId ?? null } : null;
  }
  await delay(200);
  return null;
}

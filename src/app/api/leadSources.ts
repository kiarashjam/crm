import type { LeadSource } from './types';
import { servesDemoData, authFetchJson, type RequestOptions } from './apiClient';

/** Get all lead sources for the current org (requires X-Organization-Id). */
export async function getLeadSources(req?: RequestOptions): Promise<LeadSource[]> {
  if (!servesDemoData()) {
    const list = await authFetchJson<LeadSource[]>('/api/leadsources', req);
    return Array.isArray(list) ? list : [];
  }
  return [];
}

import type { LeadSource } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

/** Get all lead sources for the current org (requires X-Organization-Id). */
export async function getLeadSources(): Promise<LeadSource[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<LeadSource[]>('/api/leadsources');
    return Array.isArray(list) ? list : [];
  }
  return [];
}

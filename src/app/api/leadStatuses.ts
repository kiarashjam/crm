import type { LeadStatus } from './types';
import { isUsingRealApi, authFetchJson } from './apiClient';

/** Get all lead statuses for the current org (requires X-Organization-Id). */
export async function getLeadStatuses(): Promise<LeadStatus[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<LeadStatus[]>('/api/leadstatuses');
    return Array.isArray(list) ? list : [];
  }
  return [];
}

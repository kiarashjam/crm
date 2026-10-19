import { authFetchJson } from './apiClient';

export interface SearchHit {
  id: string;
  title: string;
  detail?: string | null;
}

export interface GlobalSearchResult {
  leads: { id: string; name: string; email: string; status?: string }[];
  contacts: { id: string; name?: string; email?: string }[];
  companies: { id: string; name: string }[];
  deals: { id: string; name: string; value?: string }[];
  activities: SearchHit[];
  tasks: SearchHit[];
}

/** Global search across leads, contacts, companies, and deals. */
export async function globalSearch(query: string): Promise<GlobalSearchResult> {
  const q = encodeURIComponent((query || '').trim());
  const res = await authFetchJson<GlobalSearchResult>(`/api/search?q=${q}`);
  return {
    leads: Array.isArray(res?.leads) ? res.leads : [],
    contacts: Array.isArray(res?.contacts) ? res.contacts : [],
    companies: Array.isArray(res?.companies) ? res.companies : [],
    deals: Array.isArray(res?.deals) ? res.deals : [],
    activities: Array.isArray(res?.activities) ? res.activities : [],
    tasks: Array.isArray(res?.tasks) ? res.tasks : [],
  };
}

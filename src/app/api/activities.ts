import type { Activity, PagedResult } from './types';
import { mockActivities } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface ActivityRaw {
  id: string;
  type: string;
  subject?: string | null;
  body?: string | null;
  contactId?: string | null;
  dealId?: string | null;
  leadId?: string | null;
  createdAt: string;
}

function mapActivity(d: ActivityRaw): Activity {
  return {
    id: d.id,
    type: d.type,
    subject: d.subject ?? undefined,
    body: d.body ?? undefined,
    contactId: d.contactId ?? undefined,
    dealId: d.dealId ?? undefined,
    leadId: d.leadId ?? undefined,
    createdAt: d.createdAt,
  };
}

export interface GetActivitiesPagedOptions {
  page?: number;
  pageSize?: number;
  search?: string;
  type?: string;
}

/** Get paginated activities with optional search. */
export async function getActivitiesPaged(options?: GetActivitiesPagedOptions): Promise<PagedResult<Activity>> {
  if (isUsingRealApi()) {
    const params = new URLSearchParams();
    if (options?.page) params.set('page', options.page.toString());
    if (options?.pageSize) params.set('pageSize', options.pageSize.toString());
    if (options?.search) params.set('search', options.search);
    if (options?.type) params.set('type', options.type);
    const q = params.toString() ? `?${params.toString()}` : '';
    const result = await authFetchJson<PagedResult<ActivityRaw>>(`/api/activities${q}`);
    return {
      items: (result?.items ?? []).map(mapActivity),
      totalCount: result?.totalCount ?? 0,
      page: result?.page ?? 1,
      pageSize: result?.pageSize ?? 20,
      totalPages: result?.totalPages ?? 1,
      hasNextPage: result?.hasNextPage ?? false,
      hasPreviousPage: result?.hasPreviousPage ?? false,
    };
  }
  await delay(200);
  // Mock pagination
  const page = options?.page ?? 1;
  const pageSize = options?.pageSize ?? 20;
  let filtered = [...mockActivities];
  if (options?.search) {
    const s = options.search.toLowerCase();
    filtered = filtered.filter(a => 
      a.subject?.toLowerCase().includes(s) || 
      a.body?.toLowerCase().includes(s) ||
      a.type.toLowerCase().includes(s)
    );
  }
  if (options?.type) {
    filtered = filtered.filter(a => a.type.toLowerCase() === options.type!.toLowerCase());
  }
  const totalCount = filtered.length;
  const totalPages = Math.ceil(totalCount / pageSize);
  const items = filtered.slice((page - 1) * pageSize, page * pageSize);
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

/** Get all activities (real API or mock). */
export async function getActivities(): Promise<Activity[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; createdAt: string }[]>('/api/activities');
    return Array.isArray(list) ? list.map(mapActivity) : [];
  }
  await delay(200);
  return [...mockActivities];
}

/** Get activities by contact. */
export async function getActivitiesByContact(contactId: string): Promise<Activity[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; createdAt: string }[]>(`/api/activities/contact/${contactId}`);
    return Array.isArray(list) ? list.map(mapActivity) : [];
  }
  await delay(200);
  return mockActivities.filter((a) => a.contactId === contactId);
}

/** Get activities by deal. */
export async function getActivitiesByDeal(dealId: string): Promise<Activity[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; leadId?: string | null; createdAt: string }[]>(`/api/activities/deal/${dealId}`);
    return Array.isArray(list) ? list.map(mapActivity) : [];
  }
  await delay(200);
  return mockActivities.filter((a) => a.dealId === dealId);
}

/** Get activities by lead. */
export async function getActivitiesByLead(leadId: string): Promise<Activity[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; leadId?: string | null; createdAt: string }[]>(`/api/activities/lead/${leadId}`);
    return Array.isArray(list) ? list.map(mapActivity) : [];
  }
  await delay(200);
  return mockActivities.filter((a) => a.leadId === leadId);
}

/** Create an activity. */
export async function createActivity(params: { type: string; subject?: string; body?: string; contactId?: string; dealId?: string; leadId?: string }): Promise<Activity | null> {
  if (isUsingRealApi()) {
    const activity = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; createdAt: string }>('/api/activities', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return activity ? mapActivity(activity) : null;
  }
  await delay(200);
  return null;
}

/** Delete an activity. */
export async function deleteActivity(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/activities/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

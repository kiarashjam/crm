import type { Activity } from './types';
import { mockActivities } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function mapActivity(d: { id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; createdAt: string }): Activity {
  return {
    id: d.id,
    type: d.type,
    subject: d.subject ?? undefined,
    body: d.body ?? undefined,
    contactId: d.contactId ?? undefined,
    dealId: d.dealId ?? undefined,
    createdAt: d.createdAt,
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
    const list = await authFetchJson<{ id: string; type: string; subject?: string | null; body?: string | null; contactId?: string | null; dealId?: string | null; createdAt: string }[]>(`/api/activities/deal/${dealId}`);
    return Array.isArray(list) ? list.map(mapActivity) : [];
  }
  await delay(200);
  return mockActivities.filter((a) => a.dealId === dealId);
}

/** Create an activity. */
export async function createActivity(params: { type: string; subject?: string; body?: string; contactId?: string; dealId?: string }): Promise<Activity | null> {
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

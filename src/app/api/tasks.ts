import type { TaskItem } from './types';
import { mockTasks } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function mapTask(d: { id: string; title: string; description?: string | null; dueDateUtc?: string | null; completed: boolean; leadId?: string | null; dealId?: string | null }): TaskItem {
  return {
    id: d.id,
    title: d.title,
    description: d.description ?? undefined,
    dueDateUtc: d.dueDateUtc ?? undefined,
    completed: d.completed,
    leadId: d.leadId ?? undefined,
    dealId: d.dealId ?? undefined,
  };
}

/** Get all tasks (real API or mock). */
export async function getTasks(overdueOnly?: boolean): Promise<TaskItem[]> {
  if (isUsingRealApi()) {
    const q = overdueOnly ? '?overdueOnly=true' : '';
    const list = await authFetchJson<{ id: string; title: string; description?: string | null; dueDateUtc?: string | null; completed: boolean; leadId?: string | null; dealId?: string | null }[]>(`/api/tasks${q}`);
    return Array.isArray(list) ? list.map(mapTask) : [];
  }
  await delay(200);
  if (overdueOnly) {
    const now = new Date();
    return mockTasks.filter((t) => !t.completed && t.dueDateUtc && new Date(t.dueDateUtc) < now);
  }
  return [...mockTasks];
}

/** Create a task. */
export async function createTask(params: { title: string; description?: string; dueDateUtc?: string; leadId?: string; dealId?: string }): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<{ id: string; title: string; description?: string | null; dueDateUtc?: string | null; completed: boolean; leadId?: string | null; dealId?: string | null }>('/api/tasks', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Update a task. */
export async function updateTask(id: string, params: Partial<{ title: string; description: string; dueDateUtc: string; completed: boolean; leadId: string; dealId: string }>): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<{ id: string; title: string; description?: string | null; dueDateUtc?: string | null; completed: boolean; leadId?: string | null; dealId?: string | null }>(`/api/tasks/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Delete a task. */
export async function deleteTask(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/tasks/${id}`, { method: 'DELETE' });
    return res.status === 204;
  }
  await delay(200);
  return false;
}

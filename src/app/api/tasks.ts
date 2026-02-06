import type { TaskItem, TaskStats, TaskStatusType, TaskPriorityType, PagedResult } from './types';
import { mockTasks } from './mockData';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

interface TaskRaw {
  id: string;
  title: string;
  description?: string | null;
  dueDateUtc?: string | null;
  reminderDateUtc?: string | null;
  status: string;
  priority: string;
  completed: boolean;
  leadId?: string | null;
  dealId?: string | null;
  contactId?: string | null;
  assigneeId?: string | null;
  assigneeName?: string | null;
  leadName?: string | null;
  dealName?: string | null;
  contactName?: string | null;
  notes?: string | null;
  createdAtUtc?: string | null;
  updatedAtUtc?: string | null;
  completedAtUtc?: string | null;
}

function mapTask(d: TaskRaw): TaskItem {
  return {
    id: d.id,
    title: d.title,
    description: d.description ?? undefined,
    dueDateUtc: d.dueDateUtc ?? undefined,
    reminderDateUtc: d.reminderDateUtc ?? undefined,
    status: (d.status as TaskStatusType) || 'todo',
    priority: (d.priority as TaskPriorityType) || 'none',
    completed: d.completed,
    leadId: d.leadId ?? undefined,
    dealId: d.dealId ?? undefined,
    contactId: d.contactId ?? undefined,
    assigneeId: d.assigneeId ?? undefined,
    assigneeName: d.assigneeName ?? undefined,
    leadName: d.leadName ?? undefined,
    dealName: d.dealName ?? undefined,
    contactName: d.contactName ?? undefined,
    notes: d.notes ?? undefined,
    createdAtUtc: d.createdAtUtc ?? undefined,
    updatedAtUtc: d.updatedAtUtc ?? undefined,
    completedAtUtc: d.completedAtUtc ?? undefined,
  };
}

export interface GetTasksOptions {
  overdueOnly?: boolean;
  status?: TaskStatusType;
  priority?: TaskPriorityType;
  assigneeId?: string;
  leadId?: string;
  dealId?: string;
  contactId?: string;
}

export interface GetTasksPagedOptions {
  page?: number;
  pageSize?: number;
  search?: string;
  overdueOnly?: boolean;
  status?: TaskStatusType;
  priority?: TaskPriorityType;
}

/** Get paginated tasks with optional search and filters. */
export async function getTasksPaged(options?: GetTasksPagedOptions): Promise<PagedResult<TaskItem>> {
  if (isUsingRealApi()) {
    const params = new URLSearchParams();
    if (options?.page) params.set('page', options.page.toString());
    if (options?.pageSize) params.set('pageSize', options.pageSize.toString());
    if (options?.search) params.set('search', options.search);
    if (options?.overdueOnly) params.set('overdueOnly', 'true');
    if (options?.status) params.set('status', options.status);
    if (options?.priority) params.set('priority', options.priority);
    const q = params.toString() ? `?${params.toString()}` : '';
    const result = await authFetchJson<PagedResult<TaskRaw>>(`/api/tasks${q}`);
    return {
      items: (result?.items ?? []).map(mapTask),
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
  let filtered = [...mockTasks];
  if (options?.search) {
    const s = options.search.toLowerCase();
    filtered = filtered.filter(t => 
      t.title.toLowerCase().includes(s) || 
      t.description?.toLowerCase().includes(s) ||
      t.notes?.toLowerCase().includes(s)
    );
  }
  if (options?.overdueOnly) {
    const now = new Date();
    filtered = filtered.filter(t => !t.completed && t.dueDateUtc && new Date(t.dueDateUtc) < now);
  }
  if (options?.status) {
    filtered = filtered.filter(t => t.status === options.status);
  }
  if (options?.priority) {
    filtered = filtered.filter(t => t.priority === options.priority);
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

/** Get all tasks with optional filters. */
export async function getTasks(options?: GetTasksOptions): Promise<TaskItem[]> {
  if (isUsingRealApi()) {
    const params = new URLSearchParams();
    if (options?.overdueOnly) params.set('overdueOnly', 'true');
    if (options?.status) params.set('status', options.status);
    if (options?.priority) params.set('priority', options.priority);
    if (options?.assigneeId) params.set('assigneeId', options.assigneeId);
    if (options?.leadId) params.set('leadId', options.leadId);
    if (options?.dealId) params.set('dealId', options.dealId);
    if (options?.contactId) params.set('contactId', options.contactId);
    const q = params.toString() ? `?${params.toString()}` : '';
    const response = await authFetchJson<TaskRaw[] | PagedResult<TaskRaw>>(`/api/tasks${q}`);
    // Handle both array response and paginated response formats
    if (Array.isArray(response)) {
      return response.map(mapTask);
    }
    // If it's a paginated response, extract items
    if (response && 'items' in response && Array.isArray(response.items)) {
      return response.items.map(mapTask);
    }
    return [];
  }
  await delay(200);
  let filtered = [...mockTasks];
  if (options?.overdueOnly) {
    const now = new Date();
    filtered = filtered.filter((t) => !t.completed && t.dueDateUtc && new Date(t.dueDateUtc) < now);
  }
  if (options?.leadId) {
    filtered = filtered.filter((t) => t.leadId === options.leadId);
  }
  if (options?.dealId) {
    filtered = filtered.filter((t) => t.dealId === options.dealId);
  }
  return filtered;
}

/** Get tasks by lead ID. */
export async function getTasksByLead(leadId: string): Promise<TaskItem[]> {
  if (isUsingRealApi()) {
    const response = await authFetchJson<TaskRaw[] | PagedResult<TaskRaw>>(`/api/tasks/by-lead/${leadId}`);
    if (Array.isArray(response)) {
      return response.map(mapTask);
    }
    if (response && 'items' in response && Array.isArray(response.items)) {
      return response.items.map(mapTask);
    }
    return [];
  }
  await delay(200);
  return mockTasks.filter((t) => t.leadId === leadId);
}

/** Get tasks by deal ID. */
export async function getTasksByDeal(dealId: string): Promise<TaskItem[]> {
  if (isUsingRealApi()) {
    const response = await authFetchJson<TaskRaw[] | PagedResult<TaskRaw>>(`/api/tasks/by-deal/${dealId}`);
    if (Array.isArray(response)) {
      return response.map(mapTask);
    }
    if (response && 'items' in response && Array.isArray(response.items)) {
      return response.items.map(mapTask);
    }
    return [];
  }
  await delay(200);
  return mockTasks.filter((t) => t.dealId === dealId);
}

/** Get tasks by contact ID. */
export async function getTasksByContact(contactId: string): Promise<TaskItem[]> {
  if (isUsingRealApi()) {
    const response = await authFetchJson<TaskRaw[] | PagedResult<TaskRaw>>(`/api/tasks/by-contact/${contactId}`);
    if (Array.isArray(response)) {
      return response.map(mapTask);
    }
    if (response && 'items' in response && Array.isArray(response.items)) {
      return response.items.map(mapTask);
    }
    return [];
  }
  await delay(200);
  return [];
}

/** Get task statistics. */
export async function getTaskStats(): Promise<TaskStats> {
  if (isUsingRealApi()) {
    const stats = await authFetchJson<TaskStats>('/api/tasks/stats');
    return stats ?? {
      total: 0,
      todo: 0,
      inProgress: 0,
      completed: 0,
      cancelled: 0,
      overdue: 0,
      dueToday: 0,
      highPriority: 0,
    };
  }
  await delay(100);
  // Mock stats
  const now = new Date();
  const today = new Date(now);
  today.setHours(0, 0, 0, 0);
  return {
    total: mockTasks.length,
    todo: mockTasks.filter((t) => t.status === 'todo').length,
    inProgress: mockTasks.filter((t) => t.status === 'in_progress').length,
    completed: mockTasks.filter((t) => t.status === 'completed').length,
    cancelled: 0,
    overdue: mockTasks.filter((t) => t.status !== 'completed' && t.dueDateUtc && new Date(t.dueDateUtc) < now).length,
    dueToday: mockTasks.filter((t) => t.status !== 'completed' && t.dueDateUtc && new Date(t.dueDateUtc).toDateString() === today.toDateString()).length,
    highPriority: mockTasks.filter((t) => t.status !== 'completed' && t.priority === 'high').length,
  };
}

export interface CreateTaskParams {
  title: string;
  description?: string;
  dueDateUtc?: string;
  reminderDateUtc?: string;
  status?: TaskStatusType;
  priority?: TaskPriorityType;
  leadId?: string;
  dealId?: string;
  contactId?: string;
  assigneeId?: string;
  notes?: string;
}

/** Create a task. */
export async function createTask(params: CreateTaskParams): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>('/api/tasks', {
      method: 'POST',
      body: JSON.stringify(params),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  // Mock create
  const newTask: TaskItem = {
    id: crypto.randomUUID(),
    title: params.title,
    description: params.description,
    dueDateUtc: params.dueDateUtc,
    reminderDateUtc: params.reminderDateUtc,
    status: params.status ?? 'todo',
    priority: params.priority ?? 'none',
    completed: params.status === 'completed',
    leadId: params.leadId,
    dealId: params.dealId,
    contactId: params.contactId,
    assigneeId: params.assigneeId,
    notes: params.notes,
    createdAtUtc: new Date().toISOString(),
  };
  return newTask;
}

export interface UpdateTaskParams {
  title?: string;
  description?: string;
  dueDateUtc?: string;
  reminderDateUtc?: string;
  status?: TaskStatusType;
  priority?: TaskPriorityType;
  completed?: boolean;
  leadId?: string;
  dealId?: string;
  contactId?: string;
  assigneeId?: string;
  notes?: string;
  clearDueDate?: boolean;
  clearReminderDate?: boolean;
  clearAssignee?: boolean;
  clearLead?: boolean;
  clearDeal?: boolean;
  clearContact?: boolean;
}

/** Update a task. */
export async function updateTask(id: string, params: UpdateTaskParams): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>(`/api/tasks/${id}`, {
      method: 'PUT',
      body: JSON.stringify(params),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Update task status only. */
export async function updateTaskStatus(id: string, status: TaskStatusType): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>(`/api/tasks/${id}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Assign or unassign a task. */
export async function assignTask(id: string, assigneeId: string | null): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>(`/api/tasks/${id}/assign`, {
      method: 'PATCH',
      body: JSON.stringify({ assigneeId }),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Link or unlink a task to a lead. */
export async function linkTaskToLead(id: string, leadId: string | null): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>(`/api/tasks/${id}/link-lead`, {
      method: 'PATCH',
      body: JSON.stringify({ leadId }),
    });
    return task ? mapTask(task) : null;
  }
  await delay(200);
  return null;
}

/** Link or unlink a task to a deal. */
export async function linkTaskToDeal(id: string, dealId: string | null): Promise<TaskItem | null> {
  if (isUsingRealApi()) {
    const task = await authFetchJson<TaskRaw>(`/api/tasks/${id}/link-deal`, {
      method: 'PATCH',
      body: JSON.stringify({ dealId }),
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
  return true;
}

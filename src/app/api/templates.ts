import type { Template, PagedResult } from './types';
import { mockTemplates } from './mockData';
import { isUsingRealApi, authFetchJson } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

// Extended Template interface with new fields
export interface ExtendedTemplate extends Template {
  content?: string;
  brandTone?: string;
  length?: string;
  isSharedWithOrganization: boolean;
  isSystemTemplate: boolean;
  userId?: string;
  userName?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

export interface CreateTemplateRequest {
  title: string;
  description?: string;
  category?: string;
  copyTypeId: string;
  goal: string;
  content?: string;
  brandTone?: string;
  length?: string;
  isSharedWithOrganization?: boolean;
}

export interface UpdateTemplateRequest {
  title?: string;
  description?: string;
  category?: string;
  copyTypeId?: string;
  goal?: string;
  content?: string;
  brandTone?: string;
  length?: string;
  isSharedWithOrganization?: boolean;
}

interface TemplateApiResponse {
  id: string;
  title: string;
  description: string;
  category: string;
  copyTypeId: string;
  goal: string;
  content?: string;
  brandTone?: string;
  length?: string;
  useCount: number;
  isSharedWithOrganization: boolean;
  isSystemTemplate: boolean;
  userId?: string;
  userName?: string;
  createdAtUtc: string;
  updatedAtUtc?: string;
}

function mapTemplate(d: TemplateApiResponse): ExtendedTemplate {
  return {
    id: d.id,
    title: d.title,
    description: d.description,
    category: d.category,
    copyTypeId: d.copyTypeId as Template['copyTypeId'],
    goal: d.goal,
    useCount: d.useCount,
    content: d.content,
    brandTone: d.brandTone,
    length: d.length,
    isSharedWithOrganization: d.isSharedWithOrganization,
    isSystemTemplate: d.isSystemTemplate,
    userId: d.userId,
    userName: d.userName,
    createdAtUtc: d.createdAtUtc,
    updatedAtUtc: d.updatedAtUtc,
  };
}

function mapLegacyTemplate(d: Template): ExtendedTemplate {
  return {
    ...d,
    isSharedWithOrganization: false,
    isSystemTemplate: true,
    createdAtUtc: new Date().toISOString(),
  };
}

export interface GetTemplatesPagedOptions {
  page?: number;
  pageSize?: number;
  search?: string;
  category?: string;
}

/** Get paginated templates with optional search and category filter. */
export async function getTemplatesPaged(options?: GetTemplatesPagedOptions): Promise<PagedResult<ExtendedTemplate>> {
  if (isUsingRealApi()) {
    const params = new URLSearchParams();
    if (options?.page) params.set('page', options.page.toString());
    if (options?.pageSize) params.set('pageSize', options.pageSize.toString());
    if (options?.search) params.set('search', options.search);
    if (options?.category) params.set('category', options.category);
    const q = params.toString() ? `?${params.toString()}` : '';
    const result = await authFetchJson<PagedResult<TemplateApiResponse>>(`/api/templates${q}`);
    return {
      items: (result?.items ?? []).map(mapTemplate),
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
  let filtered = mockTemplates.map(mapLegacyTemplate);
  if (options?.search) {
    const s = options.search.toLowerCase();
    filtered = filtered.filter(t => 
      t.title.toLowerCase().includes(s) || 
      t.description.toLowerCase().includes(s) ||
      t.category.toLowerCase().includes(s)
    );
  }
  if (options?.category) {
    filtered = filtered.filter(t => t.category.toLowerCase() === options.category!.toLowerCase());
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

/** Get all templates (real API or mock). */
export async function getTemplates(): Promise<ExtendedTemplate[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<TemplateApiResponse[]>('/api/templates');
    return Array.isArray(list) ? list.map(mapTemplate) : [];
  }
  await delay(200);
  return mockTemplates.map(mapLegacyTemplate);
}

/** Get a single template by id (real API or mock). */
export async function getTemplateById(id: string): Promise<ExtendedTemplate | null> {
  if (isUsingRealApi()) {
    try {
      const t = await authFetchJson<TemplateApiResponse>(`/api/templates/${id}`);
      return t ? mapTemplate(t) : null;
    } catch {
      return null;
    }
  }
  await delay(150);
  const found = mockTemplates.find((t) => t.id === id);
  return found ? mapLegacyTemplate(found) : null;
}

/** Create a new template. */
export async function createTemplate(request: CreateTemplateRequest): Promise<ExtendedTemplate> {
  if (isUsingRealApi()) {
    const created = await authFetchJson<TemplateApiResponse>('/api/templates', {
      method: 'POST',
      body: JSON.stringify(request),
    });
    return mapTemplate(created);
  }
  await delay(300);
  // Mock: return a new template with generated ID
  const newTemplate: ExtendedTemplate = {
    id: `template-${Date.now()}`,
    title: request.title,
    description: request.description ?? '',
    category: request.category ?? 'Custom',
    copyTypeId: request.copyTypeId as Template['copyTypeId'],
    goal: request.goal,
    useCount: 0,
    content: request.content,
    brandTone: request.brandTone,
    length: request.length,
    isSharedWithOrganization: request.isSharedWithOrganization ?? false,
    isSystemTemplate: false,
    createdAtUtc: new Date().toISOString(),
  };
  return newTemplate;
}

/** Update an existing template. */
export async function updateTemplate(id: string, request: UpdateTemplateRequest): Promise<ExtendedTemplate> {
  if (isUsingRealApi()) {
    const updated = await authFetchJson<TemplateApiResponse>(`/api/templates/${id}`, {
      method: 'PUT',
      body: JSON.stringify(request),
    });
    return mapTemplate(updated);
  }
  await delay(300);
  // Mock: return updated template
  const existing = mockTemplates.find((t) => t.id === id);
  if (!existing) throw new Error('Template not found');
  return {
    ...mapLegacyTemplate(existing),
    ...request,
    updatedAtUtc: new Date().toISOString(),
  } as ExtendedTemplate;
}

/** Delete a template. */
export async function deleteTemplate(id: string): Promise<void> {
  if (isUsingRealApi()) {
    await authFetchJson(`/api/templates/${id}`, {
      method: 'DELETE',
    });
    return;
  }
  await delay(200);
  // Mock: just resolve
}

/** Increment use count for a template. */
export async function incrementTemplateUseCount(id: string): Promise<void> {
  if (isUsingRealApi()) {
    await authFetchJson(`/api/templates/${id}/use`, {
      method: 'POST',
    });
    return;
  }
  await delay(100);
  // Mock: no-op
}

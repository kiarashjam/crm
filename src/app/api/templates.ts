import type { Template } from './types';
import { mockTemplates } from './mockData';
import { isUsingRealApi, authFetchJson } from './apiClient';

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

function mapTemplate(d: { id: string; title: string; description: string; category: string; copyTypeId: string; goal: string; useCount: number }): Template {
  return {
    id: d.id,
    title: d.title,
    description: d.description,
    category: d.category,
    copyTypeId: d.copyTypeId as Template['copyTypeId'],
    goal: d.goal,
    useCount: d.useCount,
  };
}

/** Get all templates (real API or mock). */
export async function getTemplates(): Promise<Template[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<{ id: string; title: string; description: string; category: string; copyTypeId: string; goal: string; useCount: number }[]>('/api/templates');
    return Array.isArray(list) ? list.map(mapTemplate) : [];
  }
  await delay(200);
  return [...mockTemplates];
}

/** Get a single template by id (real API or mock). */
export async function getTemplateById(id: string): Promise<Template | null> {
  if (isUsingRealApi()) {
    try {
      const t = await authFetchJson<{ id: string; title: string; description: string; category: string; copyTypeId: string; goal: string; useCount: number }>(`/api/templates/${id}`);
      return t ? mapTemplate(t) : null;
    } catch {
      return null;
    }
  }
  await delay(150);
  return mockTemplates.find((t) => t.id === id) ?? null;
}

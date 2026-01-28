import type { Template } from './types';
import { mockTemplates } from './mockData';

/** Get all templates. */
export async function getTemplates(): Promise<Template[]> {
  await delay(200);
  return [...mockTemplates];
}

/** Get a single template by id. */
export async function getTemplateById(id: string): Promise<Template | null> {
  await delay(150);
  return mockTemplates.find((t) => t.id === id) ?? null;
}

function delay(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}

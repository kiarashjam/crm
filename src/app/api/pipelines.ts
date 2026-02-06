import type { Pipeline } from './types';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

type PipelineRaw = { id: string; organizationId: string; name: string; displayOrder: number; dealStages?: { id: string; pipelineId: string; name: string; displayOrder: number; isWon: boolean; isLost: boolean }[] };
function mapPipeline(p: PipelineRaw): Pipeline {
  return {
    id: p.id,
    organizationId: p.organizationId,
    name: p.name,
    displayOrder: p.displayOrder,
    dealStages: p.dealStages?.map((s) => ({ id: s.id, pipelineId: s.pipelineId, name: s.name, displayOrder: s.displayOrder, isWon: s.isWon, isLost: s.isLost })),
  };
}

/** Get all pipelines for the current org (requires X-Organization-Id). */
export async function getPipelines(): Promise<Pipeline[]> {
  if (isUsingRealApi()) {
    const list = await authFetchJson<PipelineRaw[]>('/api/pipelines');
    return Array.isArray(list) ? list.map(mapPipeline) : [];
  }
  return [];
}

/** Get one pipeline by id. */
export async function getPipeline(id: string): Promise<Pipeline | null> {
  if (isUsingRealApi()) {
    const p = await authFetchJson<PipelineRaw>(`/api/pipelines/${id}`);
    return p ? mapPipeline(p) : null;
  }
  return null;
}

/** Create a pipeline (Owner/Manager only). */
export async function createPipeline(params: { name: string; displayOrder?: number }): Promise<Pipeline | null> {
  if (!isUsingRealApi()) return null;
  const p = await authFetchJson<PipelineRaw>('/api/pipelines', {
    method: 'POST',
    body: JSON.stringify({ name: params.name, displayOrder: params.displayOrder ?? 0 }),
  });
  return p ? mapPipeline(p) : null;
}

/** Update a pipeline (Owner/Manager only). */
export async function updatePipeline(id: string, params: { name?: string; displayOrder?: number }): Promise<Pipeline | null> {
  if (!isUsingRealApi()) return null;
  const p = await authFetchJson<PipelineRaw>(`/api/pipelines/${id}`, {
    method: 'PUT',
    body: JSON.stringify(params),
  });
  return p ? mapPipeline(p) : null;
}

/** Delete a pipeline (Owner/Manager only). */
export async function deletePipeline(id: string): Promise<boolean> {
  if (!isUsingRealApi()) return false;
  const res = await authFetch(`/api/pipelines/${id}`, { method: 'DELETE' });
  return res.ok;
}

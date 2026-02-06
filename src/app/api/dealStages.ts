import type { DealStage } from './types';
import { isUsingRealApi, authFetchJson, authFetch } from './apiClient';

type DealStageRaw = { id: string; pipelineId: string; name: string; displayOrder: number; isWon: boolean; isLost: boolean };

function mapStage(s: DealStageRaw): DealStage {
  return {
    id: s.id,
    pipelineId: s.pipelineId,
    name: s.name,
    displayOrder: s.displayOrder,
    isWon: s.isWon,
    isLost: s.isLost,
  };
}

/** Get deal stages for a pipeline (GET /api/dealstages?pipelineId=...). */
export async function getDealStagesByPipeline(pipelineId: string): Promise<DealStage[]> {
  if (!isUsingRealApi()) return [];
  const list = await authFetchJson<DealStageRaw[]>(`/api/dealstages?pipelineId=${encodeURIComponent(pipelineId)}`);
  return Array.isArray(list) ? list.map(mapStage) : [];
}

/** Create a deal stage (Owner/Manager only). */
export async function createDealStage(params: {
  pipelineId: string;
  name: string;
  displayOrder?: number;
  isWon?: boolean;
  isLost?: boolean;
}): Promise<DealStage | null> {
  if (!isUsingRealApi()) return null;
  const s = await authFetchJson<DealStageRaw>('/api/dealstages', {
    method: 'POST',
    body: JSON.stringify({
      pipelineId: params.pipelineId,
      name: params.name,
      displayOrder: params.displayOrder ?? 0,
      isWon: params.isWon ?? false,
      isLost: params.isLost ?? false,
    }),
  });
  return s ? mapStage(s) : null;
}

/** Update a deal stage (Owner/Manager only). */
export async function updateDealStage(
  id: string,
  params: { name?: string; displayOrder?: number; isWon?: boolean; isLost?: boolean }
): Promise<DealStage | null> {
  if (!isUsingRealApi()) return null;
  const s = await authFetchJson<DealStageRaw>(`/api/dealstages/${id}`, {
    method: 'PUT',
    body: JSON.stringify(params),
  });
  return s ? mapStage(s) : null;
}

/** Delete a deal stage (Owner/Manager only). */
export async function deleteDealStage(id: string): Promise<boolean> {
  if (!isUsingRealApi()) return false;
  const res = await authFetch(`/api/dealstages/${id}`, { method: 'DELETE' });
  return res.ok;
}

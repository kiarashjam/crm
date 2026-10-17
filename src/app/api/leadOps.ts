import { authFetch, authFetchJson, getApiBaseUrl } from './apiClient';

export interface ScoringRule {
  id: string;
  field: string;
  matchValue?: string | null;
  points: number;
  isActive: boolean;
}

export interface AssignmentRule {
  id: string;
  name: string;
  kind: string;
  priority: number;
  sourceMatch?: string | null;
  territoryId?: string | null;
  queueId?: string | null;
  memberUserIds: string[];
  isActive: boolean;
}

export interface Territory {
  id: string;
  name: string;
  countries?: string | null;
  industries?: string | null;
}

export interface LeadQueue {
  id: string;
  name: string;
  memberUserIds: string[];
}

export interface CaptureForm {
  id: string;
  name: string;
  publicToken: string;
  isActive: boolean;
  successMessage?: string | null;
  publicPath: string;
}

export interface ConversionSlice {
  key: string;
  created: number;
  qualified: number;
  converted: number;
  conversionRate: number;
  avgHoursToFirstResponse?: number | null;
  avgHoursToConvert?: number | null;
}

export interface ConversionReport {
  bySource: ConversionSlice[];
  byOwner: ConversionSlice[];
}

export interface LeadStatusEvent {
  id: string;
  fromStatus?: string | null;
  toStatus: string;
  changedByUserId?: string | null;
  changedAtUtc: string;
}

const emptyId = '00000000-0000-0000-0000-000000000000';

export function getScoringRules() {
  return authFetchJson<ScoringRule[]>('/api/lead-ops/scoring');
}

export function saveScoringRules(rules: ScoringRule[]) {
  return authFetchJson<ScoringRule[]>('/api/lead-ops/scoring', { method: 'PUT', body: JSON.stringify({ rules }) });
}

export function getAssignmentRules() {
  return authFetchJson<AssignmentRule[]>('/api/lead-ops/assignment');
}

export function saveAssignmentRule(rule: Partial<AssignmentRule> & { name: string; kind: string }) {
  return authFetchJson<AssignmentRule>('/api/lead-ops/assignment', {
    method: 'POST',
    body: JSON.stringify({ id: rule.id ?? emptyId, priority: 0, memberUserIds: [], isActive: true, ...rule }),
  });
}

export function deleteAssignmentRule(id: string) {
  return authFetch(`/api/lead-ops/assignment/${id}`, { method: 'DELETE' });
}

export function getTerritories() {
  return authFetchJson<Territory[]>('/api/lead-ops/territories');
}

export function saveTerritory(territory: Partial<Territory> & { name: string }) {
  return authFetchJson<Territory>('/api/lead-ops/territories', {
    method: 'POST',
    body: JSON.stringify({ id: territory.id ?? emptyId, ...territory }),
  });
}

export function getQueues() {
  return authFetchJson<LeadQueue[]>('/api/lead-ops/queues');
}

export function saveQueue(queue: Partial<LeadQueue> & { name: string }) {
  return authFetchJson<LeadQueue>('/api/lead-ops/queues', {
    method: 'POST',
    body: JSON.stringify({ id: queue.id ?? emptyId, memberUserIds: [], ...queue }),
  });
}

export function getSla() {
  return authFetchJson<{ hours: number }>('/api/lead-ops/sla');
}

export function saveSla(hours: number) {
  return authFetchJson<{ hours: number }>('/api/lead-ops/sla', { method: 'PUT', body: JSON.stringify({ hours }) });
}

export function getCaptureForms() {
  return authFetchJson<CaptureForm[]>('/api/lead-ops/forms');
}

export function createCaptureForm(name: string) {
  return authFetchJson<CaptureForm>('/api/lead-ops/forms', {
    method: 'POST',
    body: JSON.stringify({ id: emptyId, name, publicToken: '', isActive: true, publicPath: '' }),
  });
}

export function getConversionReport() {
  return authFetchJson<ConversionReport>('/api/lead-ops/conversion');
}

export function getLeadHistory(id: string) {
  return authFetchJson<LeadStatusEvent[]>(`/api/lead-ops/history/${id}`);
}

export function bulkUpdateLeads(body: {
  ids: string[];
  status?: string;
  source?: string;
  assignedToUserId?: string;
  queueId?: string;
}) {
  return authFetchJson<{ updated: number; missing: number }>('/api/lead-ops/bulk', {
    method: 'POST',
    body: JSON.stringify(body),
  });
}

/** A public-form request that failed, with the HTTP status (0 when it never got an answer). */
export class PublicCaptureError extends Error {
  readonly status: number;
  constructor(message: string, status: number) {
    super(message);
    this.name = 'PublicCaptureError';
    this.status = status;
  }
}

async function publicFetch(path: string, init?: RequestInit): Promise<Response> {
  const base = getApiBaseUrl();
  if (!base) throw new PublicCaptureError('VITE_API_URL is not set', 0);
  try {
    return await fetch(`${base}${path}`, init);
  } catch {
    // Offline, DNS, CORS: no answer at all — not the same as "no such form".
    throw new PublicCaptureError('Could not reach the server', 0);
  }
}

export async function getPublicCaptureForm(token: string) {
  const res = await publicFetch(`/api/public/capture/${encodeURIComponent(token)}`);
  if (!res.ok) throw new PublicCaptureError('Form not available', res.status);
  return res.json() as Promise<{ name: string; successMessage?: string | null }>;
}

/** Submits the form. Resolves to the thank-you message the workspace configured. */
export async function submitPublicCapture(token: string, body: {
  name: string;
  email: string;
  phone?: string;
  jobTitle?: string;
  country?: string;
  company?: string;
  /** The honeypot. Always empty when a person fills the form in. */
  website?: string;
}): Promise<string | null> {
  const res = await publicFetch(`/api/public/capture/${encodeURIComponent(token)}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) throw new PublicCaptureError('Could not save', res.status);
  try {
    const receipt = await res.json() as { message?: string | null };
    return receipt?.message?.trim() || null;
  } catch {
    return null;
  }
}

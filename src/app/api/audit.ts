// Audit trail.
//
// A chronological record of who changed what. The real backend records events
// server-side on every mutation; `logAudit` also lets the client record
// notable actions. Demo mode seeds a realistic history and appends locally.

import { isUsingRealApi, authFetchJson } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getCurrentUser } from '@/app/lib/auth';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type AuditEntity = 'lead' | 'contact' | 'company' | 'deal' | 'task' | 'sequence' | 'automation';
export type AuditAction =
  | 'created' | 'updated' | 'deleted' | 'status_changed'
  | 'assigned' | 'email_sent' | 'merged' | 'converted';

export interface AuditEvent {
  id: string;
  entityType: AuditEntity;
  entityId?: string;
  entityLabel?: string;
  action: AuditAction;
  summary: string;
  actorName?: string;
  createdAtUtc: string;
}

const hoursAgo = (h: number) => new Date(Date.now() - h * 3_600_000).toISOString();

const store = createMockStore<AuditEvent>({
  storageKey: 'crm.mock.audit.v1',
  seed: [
    { id: 'au-1', entityType: 'deal', entityId: '1', entityLabel: 'Website redesign', action: 'status_changed', summary: 'Stage changed to Negotiation', actorName: 'Demo User', createdAtUtc: hoursAgo(3) },
    { id: 'au-2', entityType: 'lead', entityId: '1', entityLabel: 'Alex Turner', action: 'email_sent', summary: 'Sent “Quick hello”', actorName: 'Demo User', createdAtUtc: hoursAgo(6) },
    { id: 'au-3', entityType: 'contact', entityId: '2', entityLabel: 'Sarah Johnson', action: 'updated', summary: 'Updated phone number', actorName: 'Demo User', createdAtUtc: hoursAgo(20) },
    { id: 'au-4', entityType: 'lead', entityId: '3', entityLabel: 'Mike Williams', action: 'created', summary: 'Lead created from web form', actorName: 'System', createdAtUtc: hoursAgo(28) },
    { id: 'au-5', entityType: 'deal', entityId: '2', entityLabel: 'Annual contract', action: 'assigned', summary: 'Assigned to Demo User', actorName: 'Admin', createdAtUtc: hoursAgo(50) },
    { id: 'au-6', entityType: 'contact', entityId: '4', entityLabel: 'Duplicate Inc', action: 'merged', summary: 'Merged 2 duplicate contacts', actorName: 'Demo User', createdAtUtc: hoursAgo(72) },
    { id: 'au-7', entityType: 'lead', entityId: '5', entityLabel: 'Jordan Lee', action: 'converted', summary: 'Converted to contact + deal', actorName: 'Demo User', createdAtUtc: hoursAgo(96) },
  ],
  idOf: (e) => e.id,
});

export interface AuditQuery {
  entityType?: AuditEntity;
  entityId?: string;
  limit?: number;
}

export async function getAuditLog(query: AuditQuery = {}): Promise<AuditEvent[]> {
  if (isUsingRealApi()) {
    const q = new URLSearchParams();
    if (query.entityType) q.set('entityType', query.entityType);
    if (query.entityId) q.set('entityId', query.entityId);
    if (query.limit) q.set('limit', String(query.limit));
    const res = await authFetchJson<AuditEvent[]>(`/api/audit?${q.toString()}`);
    return Array.isArray(res) ? res : [];
  }
  await delay(120);
  let rows = [...store.list()].sort((a, b) => Date.parse(b.createdAtUtc) - Date.parse(a.createdAtUtc));
  if (query.entityType) rows = rows.filter((r) => r.entityType === query.entityType);
  if (query.entityId) rows = rows.filter((r) => r.entityId === query.entityId);
  if (query.limit) rows = rows.slice(0, query.limit);
  return rows;
}

export async function logAudit(
  input: Omit<AuditEvent, 'id' | 'createdAtUtc' | 'actorName'> & { actorName?: string },
): Promise<AuditEvent | null> {
  if (isUsingRealApi()) {
    // The backend records audit server-side; client logging is best-effort.
    try {
      return await authFetchJson<AuditEvent>('/api/audit', { method: 'POST', body: JSON.stringify(input) });
    } catch {
      return null;
    }
  }
  await delay(40);
  return store.add({
    id: mockId('au'),
    createdAtUtc: new Date().toISOString(),
    actorName: input.actorName ?? getCurrentUser()?.name ?? 'You',
    ...input,
  });
}

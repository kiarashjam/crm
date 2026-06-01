// File attachments on records (lead/contact/company/deal).
//
// Real backend stores the file and returns a download URL. Demo mode reads the
// file into a data URL (small files only, to respect localStorage limits) so
// uploads + downloads work offline.

import { isUsingRealApi, authFetch, authFetchJson, getApiBaseUrl } from './apiClient';
import { createMockStore, mockId } from './mockStore';
import { getCurrentUser } from '@/app/lib/auth';

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export type AttachmentEntity = 'lead' | 'contact' | 'company' | 'deal';

export interface Attachment {
  id: string;
  entityType: AttachmentEntity;
  recordId: string;
  fileName: string;
  contentType: string;
  size: number;
  /** Real-backend download URL. */
  url?: string;
  /** Demo-mode inline content for small files. */
  dataUrl?: string;
  uploadedAtUtc: string;
  uploadedByName?: string;
}

/** Largest file we inline into localStorage in demo mode (~1.5 MB). */
const MAX_INLINE_BYTES = 1_500_000;

const store = createMockStore<Attachment>({
  storageKey: 'crm.mock.attachments.v1',
  seed: [],
  idOf: (a) => a.id,
});

function readAsDataUrl(file: File): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

export async function getAttachments(entityType: AttachmentEntity, recordId: string): Promise<Attachment[]> {
  if (isUsingRealApi()) {
    const res = await authFetchJson<Attachment[]>(`/api/attachments?entityType=${entityType}&recordId=${recordId}`);
    return Array.isArray(res) ? res : [];
  }
  await delay(120);
  return store
    .list()
    .filter((a) => a.entityType === entityType && a.recordId === recordId)
    .sort((a, b) => Date.parse(b.uploadedAtUtc) - Date.parse(a.uploadedAtUtc));
}

export async function uploadAttachment(entityType: AttachmentEntity, recordId: string, file: File): Promise<Attachment | null> {
  if (isUsingRealApi()) {
    const fd = new FormData();
    fd.append('file', file);
    fd.append('entityType', entityType);
    fd.append('recordId', recordId);
    return authFetchJson<Attachment>('/api/attachments', { method: 'POST', body: fd });
  }
  await delay(300);
  const dataUrl = file.size <= MAX_INLINE_BYTES ? await readAsDataUrl(file) : undefined;
  return store.add({
    id: mockId('att'),
    entityType,
    recordId,
    fileName: file.name,
    contentType: file.type || 'application/octet-stream',
    size: file.size,
    dataUrl,
    uploadedAtUtc: new Date().toISOString(),
    uploadedByName: getCurrentUser()?.name,
  });
}

export async function deleteAttachment(id: string): Promise<boolean> {
  if (isUsingRealApi()) {
    const res = await authFetch(`/api/attachments/${id}`, { method: 'DELETE' });
    return res.status === 204 || res.ok;
  }
  await delay(120);
  return store.remove(id);
}

/** Resolve a usable href for downloading/opening an attachment. */
export function attachmentHref(a: Attachment): string | undefined {
  if (a.dataUrl) return a.dataUrl;
  if (!a.url) return undefined;
  if (a.url.startsWith('http') || a.url.startsWith('data:')) return a.url;
  const base = getApiBaseUrl();
  return base ? `${base}${a.url.startsWith('/') ? a.url : `/${a.url}`}` : a.url;
}
